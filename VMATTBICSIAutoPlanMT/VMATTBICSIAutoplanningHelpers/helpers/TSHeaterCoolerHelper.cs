using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class TSHeaterCoolerHelper
    {
        /// <summary>
        /// Helper method to generate a TS cooler structure
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="doseLevel"></param>
        /// <param name="requestedDoseConstraint"></param>
        /// <param name="volume"></param>
        /// <param name="name"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public static (string, Tuple<string, OptimizationObjectiveType, double, double, int>) GenerateCooler(ExternalPlanSetup plan, double doseLevel, double requestedDoseConstraint, double volume, string name, int priority)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Generating cooler structure: {name} now");
            //create an empty optiization objective
            Tuple<string, OptimizationObjectiveType, double, double, int> cooler = null;
            StructureSet ss = plan.StructureSet;
            //grab the relevant dose, dose leve, priority, etc. parameters
            PlanningItemDose d = plan.Dose;
            DoseValue dv = new DoseValue(doseLevel * plan.TotalDose.Dose, DoseValue.DoseUnit.cGy);
            if (ss.CanAddStructure("CONTROL", name))
            {
                //add the cooler structure to the structure list and convert the doseLevel isodose volume to a structure. Add this new structure to the list with a max dose objective of Rx * 105% and give it a priority of 80
                Structure coolerStructure = ss.AddStructure("CONTROL", name);
                coolerStructure.ConvertDoseLevelToStructure(d, dv);
                if (coolerStructure.IsEmpty)
                {
                    sb.AppendLine($"Cooler structure ({name}) is empty! Attempting to remove.");
                    if (ss.CanRemoveStructure(coolerStructure)) ss.RemoveStructure(coolerStructure);
                }
                else cooler = Tuple.Create(name, OptimizationObjectiveType.Upper, requestedDoseConstraint * plan.TotalDose.Dose, volume, priority);
            }
            return (sb.ToString(), cooler);
        }

        /// <summary>
        /// Helper method to generate a TS heater structure
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="target"></param>
        /// <param name="doseLevelLow"></param>
        /// <param name="doseLevelHigh"></param>
        /// <param name="volume"></param>
        /// <param name="name"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public static (string, Tuple<string, OptimizationObjectiveType, double, double, int>) GenerateHeater(ExternalPlanSetup plan, Structure target, double doseLevelLow, double doseLevelHigh, double volume, string name, int priority)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Generating heater structure: {name} now");
            //similar to the generateCooler method
            Tuple<string, OptimizationObjectiveType, double, double, int> heater = null;
            StructureSet ss = plan.StructureSet;
            PlanningItemDose d = plan.Dose;
            DoseValue dv = new DoseValue(doseLevelLow * plan.TotalDose.Dose, DoseValue.DoseUnit.cGy);
            if (ss.CanAddStructure("CONTROL", name))
            {
                //segment lower isodose volume
                Structure heaterStructure = ss.AddStructure("CONTROL", name);
                heaterStructure.ConvertDoseLevelToStructure(d, dv);
                //segment higher isodose volume
                Structure dummy = ss.AddStructure("CONTROL", "dummy");
                dummy.ConvertDoseLevelToStructure(d, new DoseValue(doseLevelHigh * plan.TotalDose.Dose, DoseValue.DoseUnit.cGy));
                //subtract the higher isodose volume from the heater structure and assign it to the heater structure. 
                //This is the heater structure that will be used for optimization. Create a new optimization objective for this tunning structure
                ContourHelper.CropStructureFromStructure(heaterStructure, dummy, 0.0);
                //clean up
                ss.RemoveStructure(dummy);
                //only keep the overlapping regions of the heater structure with the taget structure
                ContourHelper.ContourOverlap(target, heaterStructure, 0.0);
                if (heaterStructure.IsEmpty)
                {
                    sb.AppendLine($"Heater structure {name} is empty! Attempting to remove.");
                    if (ss.CanRemoveStructure(heaterStructure)) ss.RemoveStructure(heaterStructure);
                }
                else
                {
                    //heaters generally need to increase the dose to regions of the target NOT receiving the Rx dose --> always set the dose objective to the Rx dose
                    heater = Tuple.Create(name, OptimizationObjectiveType.Lower, plan.TotalDose.Dose, volume, priority);
                }
            }
            return (sb.ToString(), heater);
        }

        /// <summary>
        /// Helper method to take the supplied constraints and determine if all of the constraints were met in the supplied plan
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="target"></param>
        /// <param name="constraints"></param>
        /// <param name="isFinalOptimization"></param>
        /// <returns></returns>
        public static bool AllHeaterCoolerTSConstraintsMet(ExternalPlanSetup plan, Structure target, List<Tuple<string, double, string, double>> constraints, bool isFinalOptimization)
        {
            if (constraints.Any())
            {
                //if any conditions were requested for a particular heater or cooler structure, ensure all of the conditions were met prior to adding the heater/cooler structure
                foreach (Tuple<string, double, string, double> itr in constraints)
                {
                    if (itr.Item1.Contains("Dmax"))
                    {
                        //dmax constraint
                        if (plan.Dose.DoseMax3D.Dose / plan.TotalDose.Dose <= itr.Item4 / 100) return false;
                    }
                    else if (itr.Item1.Contains("V"))
                    {
                        //volume constraint
                        if (itr.Item3 == ">")
                        {
                            if (plan.GetVolumeAtDose(target, new DoseValue(itr.Item2, DoseValue.DoseUnit.Percent), VolumePresentation.Relative) <= itr.Item4) return false;
                        }
                        else
                        {
                            if (plan.GetVolumeAtDose(target, new DoseValue(itr.Item2, DoseValue.DoseUnit.Percent), VolumePresentation.Relative) >= itr.Item4) return false;
                        }
                    }
                    else if (!isFinalOptimization) return false;
                }
            }
            return true;
        }
    }
}
