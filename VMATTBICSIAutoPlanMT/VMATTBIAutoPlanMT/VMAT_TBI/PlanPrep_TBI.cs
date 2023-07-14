using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System.Text;

namespace VMATTBIAutoPlanMT.VMAT_TBI
{
    public class PlanPrep_TBI : PlanPrepBase
    {
        private ExternalPlanSetup appaPlan;
        private bool removeFlash;

        public PlanPrep_TBI(ExternalPlanSetup vmat, ExternalPlanSetup appa, bool flash, bool closePW)
        {
            //copy arguments into local variables
            VMATPlan = vmat;
            appaPlan = appa;
            removeFlash = flash;
            SetCloseOnFinish(closePW, 3000);
        }

        #region Run Control
        public override bool Run()
        {
            UpdateUILabel("Running:");
            if (PreliminaryChecks()) return true;
            if(removeFlash)
            {
                if (RemoveFlashRunSequence()) return true;
            }
            if (SeparatePlans()) return true;
            if (recalcNeeded && ReCalculateDose()) return true;
            UpdateUILabel("Finished!");
            ProvideUIUpdate(100, "Finished separating plans!");
            ProvideUIUpdate($"Run time: {GetElapsedTime()} (mm:ss)");
            return false;
        }
        #endregion

        #region Preliminary Checks
        private bool PreliminaryChecks()
        {
            UpdateUILabel("Preliminary Checks:");
            ProvideUIUpdate($"Checking {VMATPlan.Id} ({VMATPlan.UID}) is valid for preparation");
            if (CheckBeamNameFormatting(VMATPlan)) return true;
            if (removeFlash || CheckIfDoseRecalcNeeded(VMATPlan)) recalcNeeded = true;
            if(appaPlan != null)
            {
                ProvideUIUpdate($"Checking {appaPlan.Id} ({appaPlan.UID}) is valid for preparation");
                if (CheckBeamNameFormatting(appaPlan)) return true;
                if (!recalcNeeded && CheckIfDoseRecalcNeeded(appaPlan)) recalcNeeded = true;
            }
            ProvideUIUpdate(100, "Preliminary checks complete");
            return false;
        }
        #endregion

        #region Separate the plans
        private bool SeparatePlans()
        {
            UpdateUILabel("Separating plans:");
            int percentComplete = 0;
            int calcItems = 2;
            ProvideUIUpdate(0, "Initializing...");
            List<List<Beam>> vmatBeamsPerIso = PlanPrepHelper.ExtractBeamsPerIso(VMATPlan);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved list of beams for each isocenter for plan: {VMATPlan.Id}");

            numVMATIsos = vmatBeamsPerIso.Count;
            List<List<Beam>> appaBeamsPerIso = new List<List<Beam>> { };
            if (appaPlan != null)
            {
                appaBeamsPerIso = PlanPrepHelper.ExtractBeamsPerIso(appaPlan);
                numIsos = appaBeamsPerIso.Count + numVMATIsos;
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved list of beams for each isocenter for plan: {appaPlan.Id}");
            }

            //get the isocenter names using the isoNameHelper class
            List<string> isoNames = new List<string>(IsoNameHelper.GetTBIVMATIsoNames(numVMATIsos, numIsos));
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved isocenter names for plan: {VMATPlan.Id}");

            if (appaPlan != null)
            {
                isoNames.AddRange(IsoNameHelper.GetTBIAPPAIsoNames(numVMATIsos, numIsos));
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved isocenter names for plan: {appaPlan.Id}");
            }

            ProvideUIUpdate($"Separating isocenters in plan {VMATPlan.Id} into separate plans");
            if (SeparateVMATPlan(VMATPlan, vmatBeamsPerIso, isoNames)) return true;
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Successfully separated isocenters in plan {VMATPlan.Id}");

            if (appaPlan != null)
            {
                ProvideUIUpdate($"Separating isocenters in plan {appaPlan.Id} into separate plans");
                if(SeparateAPPAPlan(appaPlan, appaBeamsPerIso, isoNames)) return true;
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Successfully separated isocenters in plan {appaPlan.Id}");
            }
            return false;
        }

        private bool SeparateAPPAPlan(ExternalPlanSetup appaPlan, List<List<Beam>> appaBeamsPerIso, List<string> isoNames)
        {
            int percentComplete = 0;
            int calcItems = 4 * appaBeamsPerIso.Count;
            //counter for indexing names
            int count = numVMATIsos;
            //do the same as above, but for the AP/PA legs plan
            foreach (List<Beam> beams in appaBeamsPerIso)
            {
                ExternalPlanSetup newplan = (ExternalPlanSetup)appaPlan.Course.CopyPlanSetup(appaPlan);
                List<Beam> beamsToRemove = new List<Beam> { };
                newplan.Id = String.Format("{0} {1}", count + 1, isoNames.ElementAt(count).Contains("upper") ? "Upper Legs" : "Lower Legs");
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Created new plan {newplan.Id} as copy of {appaPlan.Id}");
                
                //newplan.AddReferencePoint(newplan.StructureSet.Structures.First(x => x.Id.ToLower() == "ptv_body"), null, newplan.Id, newplan.Id);
                separatedPlans.Add(newplan);
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Added {newplan.Id} to list of separated plans");
                foreach (Beam b in newplan.Beams)
                {
                    //if the current beam in newPlan is NOT found in the beams list, then remove it from the current new plan
                    if (!beams.Where(x => x.Id == b.Id).Any() && !b.IsSetupField)
                    {
                        ProvideUIUpdate($"Added {b.Id} to list of beams to remove from {newplan.Id}");
                        beamsToRemove.Add(b);
                    }
                }
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Finished identifying beams that need to be removed from {newplan.Id}");

                if (RemoveExtraBeams(newplan, beamsToRemove)) return true;
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Removed excess beams from {newplan.Id}");

                count++;
            }
            return false;
        }
        #endregion

        #region Remove Flash Structure Operation
        private bool RemoveFlashRunSequence()
        {
            UpdateUILabel("Removing Flash:");
            int percentComplete = 0;
            int calcItems = 6;
            List<ExternalPlanSetup> plans = new List<ExternalPlanSetup> { VMATPlan };
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Added VMAT plan ({VMATPlan.Id}) to list of plans requiring dose recalculation");

            if (appaPlan != null)
            {
                plans.Add(appaPlan);
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Added APPA plan ({appaPlan.Id}) to list of plans requiring dose recalculation");
            }

            (bool isError, List<ExternalPlanSetup> otherPlans) = CheckExistingPlansUsingSameSSWIthDoseCalculated(VMATPlan.Course.Patient.Courses.ToList(), VMATPlan.StructureSet);
            if (isError) return true;
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Finished checking for existing plans that have dose calculated and use the same structure set!");

            if (otherPlans.Any())
            {
                plans.AddRange(otherPlans);
                separatedPlans.AddRange(otherPlans);
                ProvideUIUpdate("Added found existing plans to list of plans requiring dose recalculation");
            }
            if (ResetCalculationMatrix(plans)) return true;
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Finished resetting the dose calculation matrix for all applicable plans");

            if (RemoveFlashStructures()) return true;
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Finished removing all structures used to create flash");

            if (CopyHumanBodyOntoBody()) return true;
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Finished copying human_body structure onto body");

            return false;
        }

        private bool RemoveFlashStructures()
        {
            int percentComplete = 0;
            int calcItems = 3;
            ProvideUIUpdate("Removing flash structures");

            StructureSet ss = VMATPlan.StructureSet;
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved structure set ({ss.Id}) used for plan: {VMATPlan.Id}");

            List<Structure> flashStr = ss.Structures.Where(x => x.Id.ToLower().Contains("flash") && !x.IsEmpty).ToList();
            calcItems += flashStr.Count;
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved list of structures used to create flash");
            //List<Structure> removeMe = new List<Structure>(flashStr);
            //ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Copied list of flash structures to a dummy list that can be used for iteration");

            //can't remove directly from flashStr because the vector size would change on each loop iteration
            foreach (Structure itr in flashStr)
            {
                if (ss.CanRemoveStructure(itr))
                {
                    ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Removing structure {itr.Id} from structure set");
                    ss.RemoveStructure(itr);
                }
                else ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Warning! Could not remove structure: {itr.Id}! Skipping!");
            }
            return false;
        }

        private bool CopyHumanBodyOntoBody()
        {
            ProvideUIUpdate("Copying human_body structure onto body structure");
            int percentComplete = 0;
            int calcItems = 3;
            StructureSet ss = VMATPlan.StructureSet;
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved structure set ({ss.Id}) used for plan: {VMATPlan.Id}");

            //from the generateTS class, the human_body structure was a copy of the body structure BEFORE flash was added. Therefore, if this structure still exists, we can just copy it back onto the body
            if (StructureTuningHelper.DoesStructureExistInSS("human_body", ss, true))
            {
                ProvideUIUpdate($"Human_body structure exists in structure set");
                Structure body = StructureTuningHelper.GetStructureFromId("body", ss);
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved body structure: {body.Id}");

                Structure bodyCopy = StructureTuningHelper.GetStructureFromId("human_body", ss);
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved human_body structure: {bodyCopy.Id}");

                (bool fail, StringBuilder message) = ContourHelper.CopyStructureOntoStructure(bodyCopy, body);
                if(fail)
                {
                    ProvideUIUpdate(message.ToString(), true);
                    return true;
                }
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Copied {bodyCopy.Id} onto {body.Id}");

                if (ss.CanRemoveStructure(bodyCopy))
                {
                    ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Removing {bodyCopy.Id} structure from structure set");
                    ss.RemoveStructure(bodyCopy);
                }
                else
                {
                    ProvideUIUpdate("Warning! Cannot remove HUMAN_BODY structure! Skipping!");
                }
            }
            else ProvideUIUpdate("WARNING! 'HUMAN_BODY' structure not found! Be sure to recontour the body structure!");
            return false;
        }

        private (bool, List<ExternalPlanSetup>) CheckExistingPlansUsingSameSSWIthDoseCalculated(List<Course> courses, StructureSet ss)
        {
            ProvideUIUpdate("Checking for existing plans that have dose calculated and use the same structure set");
            bool isError = false;
            //remove the structures used to generate flash in the plan
            (List<ExternalPlanSetup> otherPlans, StringBuilder planIdList) = OptimizationLoopHelper.GetOtherPlansWithSameSSWithCalculatedDose(courses, ss);

            if (otherPlans.Any())
            {
                if (otherPlans.Any(x => x.ApprovalStatus != PlanSetupApprovalStatus.UnApproved))
                {
                    List<ExternalPlanSetup> badPlans = otherPlans.Where(x => x.ApprovalStatus != PlanSetupApprovalStatus.UnApproved).ToList();
                    foreach (ExternalPlanSetup itr in badPlans)
                    {
                        ProvideUIUpdate($"Error! Plan {itr.Id} has approval status {itr.ApprovalStatus} and cannot be modified! Skipping Flash removal!",true);
                        isError = true;
                    }
                }
                else
                {
                    ProvideUIUpdate($"The following plans have dose calculated and use the structure set: {ss.Id}");
                    ProvideUIUpdate(planIdList.ToString());
                }
            }
            else
            {
                ProvideUIUpdate($"No other plans found that have dose calculated and use the structure set: {ss.Id}!");
            }
            return (isError, otherPlans);
        }

        private bool ResetCalculationMatrix(List<ExternalPlanSetup> plans)
        {
            ProvideUIUpdate("Resetting dose calculation matrices");
            int percentComplete = 0;
            int calcItems = 4 * plans.Count;
            foreach (ExternalPlanSetup itr in plans)
            {
                string calcModel = itr.GetCalculationModel(CalculationType.PhotonVolumeDose);
                itr.ClearCalculationModel(CalculationType.PhotonVolumeDose);
                itr.SetCalculationModel(CalculationType.PhotonVolumeDose, calcModel);
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Reset dose calculation matrix for plan: {itr.Id}");
            }
            return false;
        }
        #endregion
    }
}
