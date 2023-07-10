using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Prompts;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System.Text;

namespace VMATTBIAutoPlanMT.VMAT_TBI
{
    public class PlanPrep_TBI : PlanPrepBase
    {
        //common variables
        //empty vectors to hold the isocenter position of one beam from each isocenter and the names of each isocenter
        private ExternalPlanSetup appaPlan;
        private bool removeFlash;

        public PlanPrep_TBI(ExternalPlanSetup vmat, ExternalPlanSetup appa, bool flash)
        {
            //copy arguments into local variables
            VMATPlan = vmat;
            appaPlan = appa;
            removeFlash = flash;
            //if there is more than one AP/PA legs plan in the list, this indicates that the user already separated these plans. Don't separate them in this script
            //if (appa.Count() > 1) legsSeparated = true;
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

        #region Separate the vmat plan
        public bool SeparatePlans()
        {
            List<List<Beam>> vmatBeamsPerIso = PlanPrepHelper.ExtractBeamsPerIso(VMATPlan);
            numVMATIsos = vmatBeamsPerIso.Count;
            List<List<Beam>> appaBeamsPerIso = new List<List<Beam>> { };
            if (appaPlan != null)
            {
                appaBeamsPerIso = PlanPrepHelper.ExtractBeamsPerIso(appaPlan);
                numIsos = appaBeamsPerIso.Count + numVMATIsos;
            }

            //get the isocenter names using the isoNameHelper class
            List<string> isoNames = new List<string>(IsoNameHelper.GetTBIVMATIsoNames(numVMATIsos, numIsos));
            if (appaPlan != null) isoNames.AddRange(IsoNameHelper.GetTBIAPPAIsoNames(numVMATIsos, numIsos));

            if (SeparateVMATPlan(VMATPlan, vmatBeamsPerIso, isoNames)) return true;
            if (appaPlan != null && SeparateAPPAPlan(appaPlan, appaBeamsPerIso, isoNames)) return true;
            return false;
        }

        private bool SeparateAPPAPlan(ExternalPlanSetup appaPlan, List<List<Beam>> appaBeamsPerIso, List<string> isoNames)
        {
            //counter for indexing names
            //loop through the list of beams in each isocenter
            int count = numVMATIsos;
            //do the same as above, but for the AP/PA legs plan
            foreach (List<Beam> beams in appaBeamsPerIso)
            {
                ExternalPlanSetup newplan = (ExternalPlanSetup)appaPlan.Course.CopyPlanSetup(appaPlan);
                List<Beam> beamsToRemove = new List<Beam> { };
                newplan.Id = String.Format("{0} {1}", count + 1, isoNames.ElementAt(count).Contains("upper") ? "Upper Legs" : "Lower Legs");
                //newplan.AddReferencePoint(newplan.StructureSet.Structures.First(x => x.Id.ToLower() == "ptv_body"), null, newplan.Id, newplan.Id);
                separatedPlans.Add(newplan);
                foreach (Beam b in newplan.Beams)
                {
                    //if the current beam in newPlan is NOT found in the beams list, then remove it from the current new plan
                    if (!beams.Where(x => x.Id == b.Id).Any() && !b.IsSetupField) beamsToRemove.Add(b);
                }
                if (RemoveExtraBeams(newplan, beamsToRemove)) return true;
                count++;
            }
            return false;
        }
        #endregion

        #region Remove Flash Structure Operation
        private bool RemoveFlashRunSequence()
        {
            List<ExternalPlanSetup> plans = new List<ExternalPlanSetup> { VMATPlan };
            if (appaPlan != null) plans.Add(appaPlan);
            (bool isError, List<ExternalPlanSetup> otherPlans) = CheckExistingPlansUsingSameSSWIthDoseCalculated(VMATPlan.Course.Patient.Courses.ToList(), VMATPlan.StructureSet);
            if (isError) return true;
            if (otherPlans.Any())
            {
                plans.AddRange(otherPlans);
                separatedPlans.AddRange(otherPlans);
            }
            if (ResetCalculationMatrix(plans)) return true;
            if (RemoveFlashStructures()) return true;
            if (CopyHumanBodyOntoBody()) return true;
            return false;
        }

        private bool RemoveFlashStructures()
        {
            StructureSet ss = VMATPlan.StructureSet;
            List<Structure> flashStr = ss.Structures.Where(x => x.Id.ToLower().Contains("flash") && !x.IsEmpty).ToList();
            List<Structure> removeMe = new List<Structure>(flashStr);
            //can't remove directly from flashStr because the vector size would change on each loop iteration
            foreach (Structure s in removeMe)
            {
                if (ss.CanRemoveStructure(s)) ss.RemoveStructure(s);
                else ProvideUIUpdate($"Warning! Could not remove structure: {s.Id}! Skipping!");
            }
            return false;
        }

        private bool CopyHumanBodyOntoBody()
        {
            StructureSet ss = VMATPlan.StructureSet;
            //from the generateTS class, the human_body structure was a copy of the body structure BEFORE flash was added. Therefore, if this structure still exists, we can just copy it back onto the body
            if (StructureTuningHelper.DoesStructureExistInSS("human_body", ss, true))
            {
                Structure body = StructureTuningHelper.GetStructureFromId("body", ss);
                Structure bodyCopy = StructureTuningHelper.GetStructureFromId("human_body", ss);
                body.SegmentVolume = bodyCopy.Margin(0.0);
                if (ss.CanRemoveStructure(bodyCopy)) ss.RemoveStructure(bodyCopy);
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
                    ProvideUIUpdate(planIdList.ToString());
                }
            }
            return (isError, otherPlans);
        }

        private bool ResetCalculationMatrix(List<ExternalPlanSetup> plans)
        {
            foreach (ExternalPlanSetup itr in plans)
            {
                string calcModel = itr.GetCalculationModel(CalculationType.PhotonVolumeDose);
                itr.ClearCalculationModel(CalculationType.PhotonVolumeDose);
                itr.SetCalculationModel(CalculationType.PhotonVolumeDose, calcModel);
            }
            return false;
        }
        #endregion

        //public override bool GetShiftNote()
        //{
        //    //loop through each beam in the vmat plan, grab the isocenter position of the beam. Compare the z position of each isocenter to the list of z positions in the vector. 
        //    //If no match is found, this is a new isocenter. Add it to the stack. If it is not unique, this beam belongs to an existing isocenter group --> ignore it
        //    //also grab instances of each beam in each isocenter and save them (used for separating the plans later)
        //    List<ExternalPlanSetup> plans = new List<ExternalPlanSetup> { appaPlan, vmatPlan };



        //    //create the message
        //    string message = "";
        //    if (couchSurface != null) message += "***Bars out***\r\n";
        //    else message += "No couch surface structure found in plan!\r\n";
        //    //check if AP/PA plans are in FFS orientation
        //    if (appaPlan.Any() && appaPlan.Where(x => x.TreatmentOrientation != PatientOrientation.FeetFirstSupine).Any())
        //    {
        //        message += "The following AP/PA plans are NOT in the FFS orientation:\r\n";
        //        foreach (ExternalPlanSetup p in appaPlan) if (p.TreatmentOrientation != PatientOrientation.FeetFirstSupine) message += p.Id + "\r\n";
        //        message += "WARNING! THE COUCH SHIFTS FOR THESE PLANS WILL NOT BE ACCURATE!\r\n";
        //    }
        //    if (numIsos > numVMATIsos) message += "VMAT TBI setup per procedure. Please ensure the matchline on Spinning Manny and the bag matches\r\n";
        //    else message += "VMAT TBI setup per procedure. No Spinning Manny.\r\r\n";
        //    message += String.Format("TT = {0:0.0} cm for all plans\r\n", TT);
        //    message += "Dosimetric shifts SUP to INF:\r\n";

        //    //write the first set of shifts from CT ref before the loop. 12-23-2020 support added for the case where the lat/vert shifts are non-zero
        //    if (Math.Abs(shiftsBetweenIsos.ElementAt(0).Item1) >= 0.1 || Math.Abs(shiftsBetweenIsos.ElementAt(0).Item2) >= 0.1)
        //    {
        //        message += String.Format("{0} iso shift from CT REF:", names.ElementAt(0)) + Environment.NewLine;
        //        if (Math.Abs(shiftsBetweenIsos.ElementAt(0).Item1) >= 0.1) message += String.Format("X = {0:0.0} cm {1}", Math.Abs(shiftsBetweenIsos.ElementAt(0).Item1), (shiftsBetweenIsos.ElementAt(0).Item1) > 0 ? "LEFT" : "RIGHT") + Environment.NewLine;
        //        if (Math.Abs(shiftsBetweenIsos.ElementAt(0).Item2) >= 0.1) message += String.Format("Y = {0:0.0} cm {1}", Math.Abs(shiftsBetweenIsos.ElementAt(0).Item2), (shiftsBetweenIsos.ElementAt(0).Item2) > 0 ? "POST" : "ANT") + Environment.NewLine;
        //        message += String.Format("Z = {0:0.0} cm {1}", shiftsBetweenIsos.ElementAt(0).Item3, Math.Abs(shiftsBetweenIsos.ElementAt(0).Item3) > 0 ? "SUP" : "INF") + Environment.NewLine;
        //    }
        //    else message += String.Format("{0} iso shift from CT ref = {1:0.0} cm {2} ({3:0.0} cm {4} from CT ref)\r\n", names.ElementAt(0), Math.Abs(shiftsBetweenIsos.ElementAt(0).Item3), shiftsBetweenIsos.ElementAt(0).Item3 > 0 ? "SUP" : "INF", Math.Abs(shiftsFromBBs.ElementAt(0).Item3), shiftsFromBBs.ElementAt(0).Item3 > 0 ? "SUP" : "INF");

        //    for (int i = 1; i < numIsos; i++)
        //    {
        //        if (i == numVMATIsos)
        //        {
        //            //if numVMATisos == numIsos this message won't be displayed. Otherwise, we have exhausted the vmat isos and need to add these lines to the shift note
        //            message += "Rotate Spinning Manny, shift to opposite Couch Lat\r\n";
        //            message += "Upper Leg iso - same Couch Lng as Pelvis iso\r\n";
        //            //let the therapists know that they need to shift couch lateral to the opposite side if the initial lat shift was non-zero
        //            if (Math.Abs(shiftsBetweenIsos.ElementAt(0).Item1) >= 0.1) message += "Shift couch lateral to opposite side!\r\n";
        //        }
        //        //shift messages when the current isocenter is NOT the number of vmat isocenters (i.e., the first ap/pa isocenter). First case is for the vmat isocenters, the second case is when the isocenters are ap/pa (but not the first ap/pa isocenter)
        //        else if (i < numVMATIsos) message += String.Format("{0} iso shift from {1} iso = {2:0.0} cm {3} ({4:0.0} cm {5} from CT ref)\r\n", names.ElementAt(i), names.ElementAt(i - 1), Math.Abs(shiftsBetweenIsos.ElementAt(i).Item3), shiftsBetweenIsos.ElementAt(i).Item3 > 0 ? "SUP" : "INF", Math.Abs(shiftsFromBBs.ElementAt(i).Item3), shiftsFromBBs.ElementAt(i).Item3 > 0 ? "SUP" : "INF");
        //        else message += String.Format("{0} iso shift from {1} iso = {2:0.0} cm {3} ({4:0.0} cm {5} from CT ref)\r\n", names.ElementAt(i), names.ElementAt(i - 1), Math.Abs(shiftsBetweenIsos.ElementAt(i).Item3), shiftsBetweenIsos.ElementAt(i).Item3 > 0 ? "INF" : "SUP", Math.Abs(shiftsFromBBs.ElementAt(i).Item3), shiftsFromBBs.ElementAt(i).Item3 > 0 ? "INF" : "SUP");
        //    }

        //    //copy to clipboard and inform the user it's done
        //    Clipboard.SetText(message);
        //    MessageBox.Show("Shifts have been copied to the clipboard! \r\nPaste them into the journal note!");
        //    return false;
        //}
    }
}
