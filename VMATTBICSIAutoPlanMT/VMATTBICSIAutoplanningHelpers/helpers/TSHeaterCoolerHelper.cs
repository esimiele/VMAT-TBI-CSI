using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers;
using VMATTBICSIAutoPlanningHelpers.Models;
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
        public static string GenerateCooler(ExternalPlanSetup plan, TSCoolerStructureModel ts)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Generating cooler structure: {ts.TSStructureId} now");
            //create an empty optiization objective
            StructureSet ss = plan.StructureSet;
            //grab the relevant dose, dose leve, priority, etc. parameters
            PlanningItemDose d = plan.Dose;
            DoseValue dv = new DoseValue(ts.UpperDoseValue * plan.TotalDose.Dose / 100, DoseValue.DoseUnit.cGy);
            if (ss.CanAddStructure("CONTROL", ts.TSStructureId))
            {
                //add the cooler structure to the structure list and convert the doseLevel isodose volume to a structure. Add this new structure to the list with a max dose objective of Rx * 105% and give it a priority of 80
                Structure coolerStructure = ss.AddStructure("CONTROL", ts.TSStructureId);
                coolerStructure.ConvertDoseLevelToStructure(d, dv);
                if (coolerStructure.IsEmpty)
                {
                    sb.AppendLine($"Cooler structure ({ts.TSStructureId}) is empty! Attempting to remove.");
                    if (ss.CanRemoveStructure(coolerStructure)) ss.RemoveStructure(coolerStructure);
                }
            }
            return sb.ToString();
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
        public static string GenerateHeater(ExternalPlanSetup plan, Structure target, TSHeaterStructureModel ts)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Generating heater structure: {ts.TSStructureId} now");
            //similar to the generateCooler method
            StructureSet ss = plan.StructureSet;
            PlanningItemDose d = plan.Dose;
            DoseValue dv = new DoseValue(ts.LowerDoseValue * plan.TotalDose.Dose / 100, DoseValue.DoseUnit.cGy);
            if (ss.CanAddStructure("CONTROL", ts.TSStructureId))
            {
                //segment lower isodose volume
                Structure heaterStructure = ss.AddStructure("CONTROL", ts.TSStructureId);
                heaterStructure.ConvertDoseLevelToStructure(d, dv);
                //segment higher isodose volume
                Structure dummy = ss.AddStructure("CONTROL", "dummy");
                dummy.ConvertDoseLevelToStructure(d, new DoseValue(ts.UpperDoseValue * plan.TotalDose.Dose / 100, DoseValue.DoseUnit.cGy));
                //subtract the higher isodose volume from the heater structure and assign it to the heater structure. 
                //This is the heater structure that will be used for optimization. Create a new optimization objective for this tunning structure
                ContourHelper.CropStructureFromStructure(heaterStructure, dummy, 0.0);
                //clean up
                ss.RemoveStructure(dummy);
                //only keep the overlapping regions of the heater structure with the taget structure
                ContourHelper.ContourOverlap(target, heaterStructure, 0.0);
                if (heaterStructure.IsEmpty)
                {
                    sb.AppendLine($"Heater structure {ts.TSStructureId} is empty! Attempting to remove.");
                    if (ss.CanRemoveStructure(heaterStructure)) ss.RemoveStructure(heaterStructure);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Helper method to take the supplied constraints and determine if all of the constraints were met in the supplied plan
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="target"></param>
        /// <param name="constraints"></param>
        /// <param name="isFinalOptimization"></param>
        /// <returns></returns>
        //public static bool AllHeaterCoolerTSConstraintsMet(ExternalPlanSetup plan, Structure target, List<OptTSCreationCriteria> criteria, bool isFinalOptimization)
        //{
        //    if (constraints.Any())
        //    {
        //        //if any conditions were requested for a particular heater or cooler structure, ensure all of the conditions were met prior to adding the heater/cooler structure
        //        foreach (OptTSCreationCriteria itr in constraints)
        //        {
        //            if (itr.DVHMetric == DVHMetric.Dmax)
        //            {
        //                //dmax constraint
        //                if (plan.Dose.DoseMax3D.Dose / plan.TotalDose.Dose <= itr.Item4 / 100) return false;
        //            }
        //            else if (itr.DVHMetric == DVHMetric.VolumeAtDose)
        //            {
        //                //volume constraint
        //                if (itr.Item3 == ">")
        //                {
        //                    if (plan.GetVolumeAtDose(target, new DoseValue(itr.Item2, DoseValue.DoseUnit.Percent), VolumePresentation.Relative) <= itr.Item4) return false;
        //                }
        //                else
        //                {
        //                    if (plan.GetVolumeAtDose(target, new DoseValue(itr.Item2, DoseValue.DoseUnit.Percent), VolumePresentation.Relative) >= itr.Item4) return false;
        //                }
        //            }
        //            else if (!isFinalOptimization) return false;
        //        }
        //    }
        //    return true;
        //}

        public static void EvaluateHeaterCoolerCreationCriteria(ExternalPlanSetup plan, Structure target, List<OptTSCreationCriteriaModel> criteria)
        {
            if(criteria.Any())
            {
                foreach(OptTSCreationCriteriaModel itr in criteria)
                {
                    if (itr.DVHMetric == DVHMetric.Dmax)
                    {
                        //dmax constraint
                        itr.QueryResult = plan.Dose.DoseMax3D.Dose;
                        if (itr.QueryResultUnits == Units.Percent) itr.QueryResult *= (100 / plan.TotalDose.Dose);
                        else if (itr.QueryResultUnits == Units.Gy) itr.QueryResult /= 100;
                    }
                    else if (itr.DVHMetric == DVHMetric.Dmean)
                    {
                        itr.QueryResult = plan.GetDVHCumulativeData(target, 
                                                                    itr.QueryResultUnits == Units.Percent ? DoseValuePresentation.Relative : DoseValuePresentation.Absolute, 
                                                                    VolumePresentation.Relative, 0.1).MeanDose.Dose;
                    }
                    else if (itr.DVHMetric == DVHMetric.Dmin)
                    {
                        itr.QueryResult = plan.GetDVHCumulativeData(target, 
                                                                    itr.QueryResultUnits == Units.Percent ? DoseValuePresentation.Relative : DoseValuePresentation.Absolute, 
                                                                    VolumePresentation.Relative, 0.1).MinDose.Dose;
                    }
                    else if (itr.DVHMetric == DVHMetric.VolumeAtDose)
                    {
                        itr.QueryResult = plan.GetVolumeAtDose(target, 
                                                               new DoseValue(itr.QueryValue, UnitsTypeHelper.GetDoseUnit(itr.QueryUnits)), 
                                                               itr.QueryResultUnits == Units.Percent ? VolumePresentation.Relative : VolumePresentation.AbsoluteCm3);
                    }
                    else if (itr.DVHMetric == DVHMetric.DoseAtVolume)
                    {
                        itr.QueryResult = plan.GetDoseAtVolume(target, 
                                                               itr.QueryValue, 
                                                               itr.QueryUnits == Units.Percent ? VolumePresentation.Relative : VolumePresentation.AbsoluteCm3, 
                                                               itr.QueryResultUnits == Units.Percent ? DoseValuePresentation.Relative : DoseValuePresentation.Absolute).Dose;
                    }
                }
            }
        }
    }
}
