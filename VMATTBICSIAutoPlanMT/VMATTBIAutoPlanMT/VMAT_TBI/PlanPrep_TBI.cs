using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System.Text;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBIAutoPlanMT.VMAT_TBI
{
    public class PlanPrep_TBI : PlanPrepBase
    {
        //data members
        private List<ExternalPlanSetup> appaPlans = new List<ExternalPlanSetup> { };
        private bool removeFlash;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="vmat"></param>
        /// <param name="appa"></param>
        /// <param name="flash"></param>
        /// <param name="closePW"></param>
        public PlanPrep_TBI(ExternalPlanSetup vmat, List<ExternalPlanSetup> appa, bool autoRecalc, bool flash, bool closePW)
        {
            //copy arguments into local variables
            VMATPlan = vmat;
            appaPlans = appa;
            _autoDoseRecalculation = autoRecalc;
            removeFlash = flash;
            SetCloseOnFinish(closePW, 3000);
        }

        #region Run Control
        /// <summary>
        /// Run control
        /// </summary>
        /// <returns></returns>
        public override bool Run()
        {
            UpdateUILabel("Running:");
            if(_recalculateDoseOnly)
            {
                if (recalcNeeded && ReCalculateDose()) return true;
                UpdateUILabel("Finished!");
                ProvideUIUpdate(100, "Finished calculating dose!");
                ProvideUIUpdate($"Run time: {GetElapsedTime()} (mm:ss)");
            }
            else
            {
                if (PreliminaryChecks()) return true;
                if (removeFlash)
                {
                    if (RemoveFlashRunSequence()) return true;
                }
                if (SeparatePlans()) return true;
                if (_autoDoseRecalculation && recalcNeeded && ReCalculateDose()) return true;
                UpdateUILabel("Finished!");
                ProvideUIUpdate(100, "Finished separating plans!");
                ProvideUIUpdate($"Run time: {GetElapsedTime()} (mm:ss)");
            }
            
            return false;
        }
        #endregion

        #region Preliminary Checks
        /// <summary>
        /// Preliminary checks
        /// </summary>
        /// <returns></returns>
        private bool PreliminaryChecks()
        {
            UpdateUILabel("Preliminary Checks:");
            ProvideUIUpdate($"Checking {VMATPlan.Id} ({VMATPlan.UID}) is valid for preparation");
            if (CheckBeamNameFormatting(VMATPlan)) return true;
            if (removeFlash || CheckIfDoseRecalcNeeded(VMATPlan)) recalcNeeded = true;
            if(appaPlans.Any())
            {
                foreach(ExternalPlanSetup itr in appaPlans)
                {
                    ProvideUIUpdate($"Checking {itr.Id} ({itr.UID}) is valid for preparation");
                    if (CheckBeamNameFormatting(itr)) return true;
                }
            }
            ProvideUIUpdate(100, "Preliminary checks complete");
            return false;
        }
        #endregion

        #region Separate the plans
        /// <summary>
        /// Helper utility method to separate the VMAT and AP/PA isocenters into separate plans
        /// </summary>
        /// <returns></returns>
        private bool SeparatePlans()
        {
            UpdateUILabel("Separating plans:");
            int percentComplete = 0;
            int calcItems = 3;
            ProvideUIUpdate(0, "Initializing...");
            List<List<Beam>> vmatBeamsPerIso = PlanPrepHelper.ExtractBeamsPerIso(VMATPlan);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved list of beams for each isocenter for plan: {VMATPlan.Id}");

            numVMATIsos = vmatBeamsPerIso.Count;
            if (appaPlans.Any())
            {
                numIsos = appaPlans.Count() + numVMATIsos;
                ProvideUIUpdate(100 * ++percentComplete / ++calcItems, $"Retrieved list of beams for each isocenter for appa plans");
            }

            //get the isocenter names using the isoNameHelper class
            List<IsocenterModel> isoNames = new List<IsocenterModel>(IsoNameHelper.GetTBIVMATIsoNames(numVMATIsos, numIsos));
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved isocenter names for plan: {VMATPlan.Id}");

            if (appaPlans.Any())
            {
                isoNames.AddRange(IsoNameHelper.GetTBIAPPAIsoNames(numVMATIsos, numIsos));
                ProvideUIUpdate(100 * ++percentComplete / ++calcItems, $"Retrieved isocenter names for appa plans");
            }

            ProvideUIUpdate($"Separating isocenters in plan {VMATPlan.Id} into separate plans");
            if (SeparatePlan(VMATPlan, vmatBeamsPerIso, isoNames)) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Successfully separated isocenters in plan {VMATPlan.Id}");

            return false;
        }
        #endregion

        #region Remove Flash Structure Operation
        /// <summary>
        /// Controller for removing flash from the structure set
        /// </summary>
        /// <returns></returns>
        private bool RemoveFlashRunSequence()
        {
            UpdateUILabel("Removing Flash:");
            int percentComplete = 0;
            int calcItems = 6;
            List<ExternalPlanSetup> plans = new List<ExternalPlanSetup> { VMATPlan };
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Added VMAT plan ({VMATPlan.Id}) to list of plans requiring dose recalculation");

            if (appaPlans.Any())
            {
                foreach (ExternalPlanSetup itr in appaPlans)
                {
                    plans.Add(itr);
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Added APPA plan {itr.Id} to list of plans requiring dose recalculation");
                }
            }

            (bool isError, IEnumerable<ExternalPlanSetup> otherPlans) = CheckExistingPlansUsingSameSSWIthDoseCalculated(VMATPlan, VMATPlan.StructureSet);
            if (isError) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Finished checking for existing plans that have dose calculated and use the same structure set!");

            if (otherPlans.Any())
            {
                plans.AddRange(otherPlans);
                separatedPlans.AddRange(otherPlans);
                ProvideUIUpdate("Added found existing plans to list of plans requiring dose recalculation");
            }
            if (ResetCalculationMatrix(plans)) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Finished resetting the dose calculation matrix for all applicable plans");

            if (RemoveFlashStructures()) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Finished removing all structures used to create flash");

            if (CopyHumanBodyOntoBody()) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Finished copying human_body structure onto body");

            return false;
        }

        /// <summary>
        /// Simple method to grab all structures involved with flash and remove them from the structure set
        /// </summary>
        /// <returns></returns>
        private bool RemoveFlashStructures()
        {
            int percentComplete = 0;
            int calcItems = 2;
            ProvideUIUpdate("Removing flash structures");

            StructureSet ss = VMATPlan.StructureSet;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved structure set ({ss.Id}) used for plan: {VMATPlan.Id}");

            List<Structure> flashStr = ss.Structures.Where(x => x.Id.ToLower().Contains("flash") && !x.IsEmpty).ToList();
            calcItems += flashStr.Count;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved list of structures used to create flash");

            //can't remove directly from flashStr because the vector size would change on each loop iteration
            foreach (Structure itr in flashStr)
            {
                if (ss.CanRemoveStructure(itr))
                {
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Removing structure {itr.Id} from structure set");
                    ss.RemoveStructure(itr);
                }
                else ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Warning! Could not remove structure: {itr.Id}! Skipping!");
            }
            return false;
        }

        /// <summary>
        /// Helper method to grab the body copy structure (human_body) and copy it back onto the main body structure. Then remove the body
        /// copy structure
        /// </summary>
        /// <returns></returns>
        private bool CopyHumanBodyOntoBody()
        {
            ProvideUIUpdate("Copying human_body structure onto body structure");
            int percentComplete = 0;
            int calcItems = 3;
            StructureSet ss = VMATPlan.StructureSet;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved structure set ({ss.Id}) used for plan: {VMATPlan.Id}");

            //from the generateTS class, the human_body structure was a copy of the body structure BEFORE flash was added. Therefore, if this structure still exists, we can just copy it back onto the body
            if (StructureTuningHelper.DoesStructureExistInSS("human_body", ss, true))
            {
                ProvideUIUpdate($"Human_body structure exists in structure set");
                Structure body = StructureTuningHelper.GetStructureFromId("body", ss);
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved body structure: {body.Id}");

                Structure bodyCopy = StructureTuningHelper.GetStructureFromId("human_body", ss);
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved human_body structure: {bodyCopy.Id}");

                (bool fail, StringBuilder message) = ContourHelper.CopyStructureOntoStructure(bodyCopy, body);
                if(fail)
                {
                    ProvideUIUpdate(message.ToString(), true);
                    return true;
                }
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Copied {bodyCopy.Id} onto {body.Id}");

                if (ss.CanRemoveStructure(bodyCopy))
                {
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Removing {bodyCopy.Id} structure from structure set");
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

        /// <summary>
        /// Method to look for other plans that have dose calculated that reference the supplied structure set
        /// </summary>
        /// <param name="thePlan"></param>
        /// <param name="ss"></param>
        /// <returns></returns>
        private (bool, IEnumerable<ExternalPlanSetup>) CheckExistingPlansUsingSameSSWIthDoseCalculated(ExternalPlanSetup thePlan, StructureSet ss)
        {
            List<Course> courses = thePlan.Course.Patient.Courses.ToList();
            ProvideUIUpdate("Checking for existing plans that have dose calculated and use the same structure set");
            bool isError = false;
            //remove the structures used to generate flash in the plan
            (IEnumerable<ExternalPlanSetup> otherPlans, StringBuilder planIdList) = OptimizationLoopHelper.GetOtherPlansWithSameSSWithCalculatedDose(courses, ss);

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

        /// <summary>
        /// Simple helper method to reset the dose calculation matrix for each of the supplied plans
        /// </summary>
        /// <param name="plans"></param>
        /// <returns></returns>
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
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Reset dose calculation matrix for plan: {itr.Id}");
            }
            return false;
        }
        #endregion
    }
}
