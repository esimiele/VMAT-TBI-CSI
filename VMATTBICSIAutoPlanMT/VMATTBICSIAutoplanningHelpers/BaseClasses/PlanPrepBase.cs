﻿using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using SimpleProgressWindow;
using System.Text;

namespace VMATTBICSIAutoPlanningHelpers.BaseClasses
{
    public class PlanPrepBase : SimpleMTbase
    {
        protected ExternalPlanSetup VMATPlan = null;
        public int numVMATIsos = 0;
        public int numIsos;
        public List<Tuple<double, double, double>> isoPositions = new List<Tuple<double, double, double>> { };
        public List<List<Beam>> vmatBeamsPerIso = new List<List<Beam>> { };
        public List<ExternalPlanSetup> separatedPlans = new List<ExternalPlanSetup> { };
        protected bool recalcNeeded = false;

        protected bool CheckBeamNameFormatting(ExternalPlanSetup plan)
        {
            StringBuilder sb = new StringBuilder();
            bool beamFormatError = false;
            int percentComplete = 0;
            int calcItems = plan.Beams.Count(x => !x.IsSetupField);
            foreach (Beam b in plan.Beams.Where(x => !x.IsSetupField))
            {
                if (b.Id.Length < 2 || !int.TryParse(b.Id.Substring(0, 2).ToString(), out int dummy))
                {
                    sb.AppendLine(b.Id);
                    beamFormatError = true;
                }
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Beam {b.Id} formatting ok");
            }
            if (beamFormatError)
            {
                ProvideUIUpdate("The following beams are not in the correct format:");
                ProvideUIUpdate(sb.ToString());
                ProvideUIUpdate("Make sure there is a space after the beam number! Please fix and try again!", true);
            }
            return beamFormatError;
        }

        protected bool CheckIfDoseRecalcNeeded(ExternalPlanSetup thePlan)
        {
            if (thePlan.GetCalculationOptions(thePlan.GetCalculationModel(CalculationType.PhotonVolumeDose)).Any(x => string.Equals(x.Key, "PlanDoseCalculation")))
            {
                if (thePlan.GetCalculationOptions(thePlan.GetCalculationModel(CalculationType.PhotonVolumeDose)).First(x => string.Equals(x.Key, "PlanDoseCalculation")).Value == "ON")
                {
                    ProvideUIUpdate("Dose recalculation required for all separated plans!");
                    return true;
                }
            }
            ProvideUIUpdate("Dose recalculation NOT required for all separated plans");
            return false;
        }

        protected bool SeparateVMATPlan(ExternalPlanSetup plan, List<List<Beam>> beamsPerIso, List<string> names)
        {
            UpdateUILabel("Separating plans:");
            int count = 0;
            foreach (List<Beam> beams in beamsPerIso)
            {
                //copy the plan, set the plan id based on the counter, and make a empty list to hold the beams that need to be removed
                ExternalPlanSetup newplan = (ExternalPlanSetup)plan.Course.CopyPlanSetup(plan);
                newplan.Id = $"{count + 1} {names.ElementAt(count)}";
                ProvideUIUpdate($"Created new plan: {newplan.Id}");
                List<Beam> removeMe = new List<Beam> { };
                //can't add reference point to plan because it must be open in Eclipse for ESAPI to perform this function. Need to fix in v16
                //newplan.AddReferencePoint(newplan.StructureSet.Structures.First(x => x.Id.ToLower() == "ptv_body"), null, newplan.Id, newplan.Id);
                //add the current plan copy to the separatedPlans list
                separatedPlans.Add(newplan);
                ProvideUIUpdate($"Added {newplan.Id} to list of plans requiring dose recalculation");
                //loop through each beam in the plan copy and compare it to the list of beams in the current isocenter
                foreach (Beam b in newplan.Beams)
                {
                    //if the current beam in newPlan is NOT found in the beams list, add it to the removeMe list. This logic has to be applied. You can't directly remove the beams in this loop as ESAPI will
                    //complain that the enumerable that it is using to index the loop changes on each iteration (i.e., newplan.Beams changes with each iteration). Do NOT add setup beams to the removeMe list. The
                    //idea is to have dosi add one set of setup fields to the original plan and then not remove those for each created plan. Unfortunately, dosi will have to manually adjust the iso position for
                    //the setup fields in each new plan (no way to adjust the existing isocenter of an existing beam, it has to be re-added)
                    if (!beams.Any(x => string.Equals(x.Id, b.Id)) && !b.IsSetupField)
                    {
                        ProvideUIUpdate($"Added {b.Id} to list of beams to remove from {newplan.Id}");
                        removeMe.Add(b);
                    }
                }
                ProvideUIUpdate($"Removing excess beams from {newplan.Id}");
                //now remove the beams for the current plan copy
                try 
                {
                    foreach (Beam b in removeMe)
                    {
                        ProvideUIUpdate($"Removing beam {b.Id} from {newplan.Id}");
                        newplan.RemoveBeam(b);
                    }
                }
                catch (Exception e) 
                { 
                    ProvideUIUpdate($"Failed to remove beams in plan {newplan.Id}");
                    ProvideUIUpdate($"{e.Message}");
                    ProvideUIUpdate(e.StackTrace, true);
                    return true;
                }
                count++;
            }
            return false;
        }

        public bool ReCalculateDose()
        {
            //loop through each plan in the separatedPlans list (generated in the separate method above) and calculate dose for each plan
            foreach (ExternalPlanSetup p in separatedPlans) p.CalculateDose();
            return false;
        }
    }
}
