using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using PlanType = VMATTBICSIAutoPlanningHelpers.Enums.PlanType;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.Models;
using VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class OptimizationLoopUIHelper
    {
        /// <summary>
        /// Helper method to print the header of the optimization objectives for the supplied plan id to the optimization loop UI
        /// </summary>
        /// <param name="planId"></param>
        /// <returns></returns>
        public static string GetOptimizationObjectivesHeader(string planId)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Environment.NewLine + $"Updated optimization constraints for plan: {planId}");
            sb.AppendLine("-------------------------------------------------------------------------");
            sb.AppendLine(String.Format("{0, -16} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority"));
            sb.Append("-------------------------------------------------------------------------");
            return sb.ToString();
        }

        /// <summary>
        /// Helper method to print the header of the optimization results for the supplied plan id to the optimization loop UI
        /// </summary>
        /// <param name="planId"></param>
        /// <returns></returns>
        public static string GetOptimizationResultsHeader(string planId)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Results of optimization for plan: {planId}");
            sb.AppendLine("---------------------------------------------------------------------------------------------------------");
            sb.AppendLine(String.Format("{0, -16} | {1, -16} | {2, -20} | {3, -16} | {4, -12} | {5, -9} |", "structure Id", "constraint type", "dose diff^2 (cGy^2)", "current priority", "cost", "cost (%)"));
            sb.Append("---------------------------------------------------------------------------------------------------------");
            return sb.ToString();
        }

        /// <summary>
        /// Helper method to print the header information for the requested planning objectives to the optimization loop UI
        /// </summary>
        /// <returns></returns>
        public static string GetPlanObjectivesHeader()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Plan objectives:");
            sb.AppendLine("--------------------------------------------------------------------------");
            sb.AppendLine(String.Format("{0, -16} | {1, -16} | {2, -10} | {3, -10} | {4, -9} |", "structure Id", "constraint type", "dose", "volume (%)", "dose type"));
            sb.Append("--------------------------------------------------------------------------");
            return sb.ToString();
        }

        /// <summary>
        /// Helper method to print the requested planning objectives to the optimization loop UI
        /// </summary>
        /// <param name="planObj"></param>
        /// <returns></returns>
        public static string PrintPlanObjectives(List<PlanObjectiveModel> planObj)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(GetPlanObjectivesHeader());
            foreach (PlanObjectiveModel itr in planObj)
            {
                //"structure Id", "constraint type", "dose (cGy or %)", "volume (%)", "Dose display (absolute or relative)"
                sb.AppendLine(String.Format("{0, -16} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-9} |", itr.StructureId, itr.ConstraintType, itr.QueryDose, itr.QueryVolume, itr.QueryDoseUnits));
            }
            sb.Append(Environment.NewLine);
            return sb.ToString();
        }

        /// <summary>
        /// Helper method to print the global run configuration settings for this optimization loop run
        /// </summary>
        /// <param name="plans"></param>
        /// <param name="type"></param>
        /// <param name="coverageCheck"></param>
        /// <param name="numOpt"></param>
        /// <param name="oneMoreOpt"></param>
        /// <param name="copyAndSavePlans"></param>
        /// <param name="targetCoverage"></param>
        /// <returns></returns>
        public static string GetRunSetupInfoHeader(List<ExternalPlanSetup> plans, 
                                                   PlanType type, 
                                                   bool useFlash,
                                                   Dictionary<string,string> normVolumes,
                                                   bool coverageCheck, 
                                                   int numOpt, 
                                                   bool oneMoreOpt, 
                                                   bool copyAndSavePlans, 
                                                   double targetCoverage)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("---------------------------------------------------------------------------------------------------------");
            sb.AppendLine($"Date: {DateTime.Now}");
            sb.AppendLine($"Plan type: {type}");
            sb.AppendLine("Optimization loop settings:");
            sb.AppendLine($"Run coverage check: {coverageCheck}");
            sb.AppendLine($"Max number of optimizations: {numOpt}");
            sb.AppendLine($"Run additional optimization to lower hotspots: {oneMoreOpt}");
            sb.AppendLine($"Copy and save each optimized plan: {copyAndSavePlans}");
            sb.AppendLine($"Plan normalization:");

            foreach (KeyValuePair<string,string> itr in normVolumes)
            {
                sb.AppendLine($"     Plan: {itr.Key}, Normalization volume: {itr.Value}, Normalization: V{plans.First(x => string.Equals(itr.Key, x.Id, StringComparison.OrdinalIgnoreCase)).TotalDose.Dose}cGy = {targetCoverage:0.0}%");
            }

            if(type == PlanType.VMAT_TBI && useFlash)
            {
                sb.AppendLine("");
                sb.AppendLine("I found structures in the optimization list that have the keyword 'flash'!");
                sb.AppendLine("I'm assuming you want to include flash in the optimization! Stop the loop if this is a mistake!");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Helper method to print the requested additional information about the achieved plan quality following an optimization loop iteration
        /// </summary>
        /// <param name="requestedInfo"></param>
        /// <param name="plan"></param>
        /// <param name="normalizationVolumes"></param>
        /// <returns></returns>
        public static string PrintAdditionalPlanDoseInfo(List<RequestedPlanMetricModel> requestedInfo, 
                                                         ExternalPlanSetup plan, 
                                                         Dictionary<string, string> normalizationVolumes)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(Environment.NewLine + $"Additional infomation for plan: {plan.Id}");
            foreach (RequestedPlanMetricModel itr in requestedInfo)
            {
                if (itr.StructureId.Contains("<plan>"))
                {
                    if (itr.DVHMetric == DVHMetric.Dmax) sb.AppendLine($"Plan global Dmax = {100 * (plan.Dose.DoseMax3D.Dose / plan.TotalDose.Dose):0.0}%");
                    else sb.AppendLine($"Cannot retrive metric ({itr.StructureId},{itr.DVHMetric},{itr.QueryResultUnits})! Skipping!");
                }
                else
                {
                    string structureId;
                    if (itr.StructureId.Contains("<target>"))
                    {
                        structureId = OptimizationLoopHelper.GetNormaliztionVolumeIdForPlan(plan.Id, normalizationVolumes);
                        if(string.IsNullOrEmpty(structureId))
                        {
                            //no normalization volume found for plan. Should only occur for plan sum for VMAT CSI --> grab TS_PTV_CSI structure
                            structureId = "TS_PTV_CSI";
                        }
                    }
                    else structureId = itr.StructureId;
                    Structure structure = StructureTuningHelper.GetStructureFromId(structureId, plan.StructureSet);
                    if (structure != null)
                    {
                        if (itr.DVHMetric == DVHMetric.Dmax || itr.DVHMetric == DVHMetric.Dmin)
                        {
                            sb.AppendLine(String.Format("{0} {1} = {2:0.0}{3}",
                                            structure.Id,
                                            itr.DVHMetric,
                                            plan.GetDoseAtVolume(structure, itr.DVHMetric == DVHMetric.Dmax ? 0.0 : 100.0, VolumePresentation.Relative, itr.QueryResultUnits == Units.Percent ? DoseValuePresentation.Relative : DoseValuePresentation.Absolute).Dose,
                                            itr.QueryResultUnits));
                        }
                        else
                        {
                            if (itr.DVHMetric == DVHMetric.DoseAtVolume)
                            {
                                //dose at specified volume requested
                                sb.AppendLine(String.Format("{0} {1}{2}{3} = {4:0.0}{5}",
                                            structure.Id,
                                            itr.DVHMetric,
                                            itr.QueryValue,
                                            itr.QueryUnits,
                                            plan.GetDoseAtVolume(structure, itr.QueryValue, itr.QueryUnits == Units.Percent ? VolumePresentation.Relative : VolumePresentation.AbsoluteCm3, itr.QueryResultUnits == Units.Percent ? DoseValuePresentation.Relative : DoseValuePresentation.Absolute).Dose,
                                            itr.QueryResultUnits));
                            }
                            else
                            {
                                //volume at specified dose requested
                                sb.AppendLine(String.Format("{0} {1}{2}{3} = {4:0.0}{5}",
                                            structure.Id,
                                            itr.DVHMetric,
                                            itr.QueryValue,
                                            itr.QueryUnits,
                                            plan.GetVolumeAtDose(structure, new DoseValue(itr.QueryValue, UnitsTypeHelper.GetDoseUnit(itr.QueryUnits)), itr.QueryResultUnits == Units.Percent ? VolumePresentation.Relative : VolumePresentation.AbsoluteCm3),
                                            itr.QueryResultUnits));
                            }
                        }
                    }
                    else sb.AppendLine($"Cannot retrive metric ({itr.StructureId},{itr.DVHMetric},{itr.QueryValue},{itr.QueryUnits})! Skipping!");
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Helper method to print the requested optimization tuning structures and their constraints/requirements for generation to the 
        /// optimization loop UI
        /// </summary>
        /// <param name="requestedTSstructures"></param>
        /// <returns></returns>
        public static string PrintRequestedTSStructures(List<RequestedOptimizationTSStructureModel> requestedTSstructures)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Requested tuning structures:");
            sb.AppendLine("--------------------------------------------------------------------------");
            sb.AppendLine(String.Format("{0, -16} | {1, -9} | {2, -10} | {3, -5} | {4, -8} | {5, -10} |", "structure Id", "low D (%)", "high D (%)", "V (%)", "priority", "constraint"));
            sb.AppendLine("--------------------------------------------------------------------------");
            foreach (RequestedOptimizationTSStructureModel ts in requestedTSstructures)
            {
                if (ts.GetType() == typeof(TSCoolerStructureModel)) sb.Append(String.Format(" {0, -15} | {1, -9:N1} | {2,-10:N1} | {3,-5:N1} | {4,-8} |", ts.TSStructureId, "", (ts as TSCoolerStructureModel).UpperDoseValue, ts.Constraints.First().QueryVolume, ts.Constraints.First().Priority));
                else sb.Append(String.Format(" {0, -15} | {1, -9:N1} | {2,-10:N1} | {3,-5:N1} | {4,-8} |", ts.TSStructureId, (ts as TSHeaterStructureModel).LowerDoseValue, (ts as TSHeaterStructureModel).UpperDoseValue, ts.Constraints.First().QueryVolume, ts.Constraints.First().Priority));

                if (!ts.CreationCriteria.Any()) sb.AppendLine(String.Format(" {0,-10} |", "none"));
                else
                {
                    int count = 0;
                    foreach (OptTSCreationCriteriaModel ts1 in ts.CreationCriteria)
                    {
                        if (count == 0)
                        {
                            if (ts1.DVHMetric == DVHMetric.DoseAtVolume || ts1.DVHMetric == DVHMetric.VolumeAtDose) sb.AppendLine(String.Format(" {0,-10} |", $"{ts1.DVHMetric}{ts1.QueryValue}{ts1.QueryUnits} {ts1.Operator} {ts1.Limit}{ts1.QueryResultUnits}"));
                            else if (ts1.CreateForFinalOptimization) sb.AppendLine(String.Format(" {0,-10} |", $"FinalOpt"));
                            else sb.AppendLine(String.Format(" {0,-10} |", $"{ts1.DVHMetric} {ts1.Operator} {ts1.Limit}{ts1.QueryResultUnits}"));
                        }
                        else
                        {
                            if (ts1.DVHMetric == DVHMetric.DoseAtVolume || ts1.DVHMetric == DVHMetric.VolumeAtDose) sb.AppendLine(String.Format(" {0,-59} | {1,-10} |", " ", $"{ts1.DVHMetric}{ts1.QueryValue}{ts1.QueryUnits} {ts1.Operator} {ts1.Limit}{ts1.QueryResultUnits}"));
                            else if (ts1.CreateForFinalOptimization) sb.AppendLine(String.Format(" {0,-59} | {1,-10} |", " ", $"FinalOpt"));
                            else sb.AppendLine(String.Format(" {0,-59} | {1,-10} |", " ", $"{ts1.DVHMetric} {ts1.Operator} {ts1.Limit}{ts1.QueryResultUnits}"));
                        }
                        count++;
                    }
                }
            }
            sb.AppendLine(Environment.NewLine);
            return sb.ToString();
        }

        /// <summary>
        /// Helper method to print the optimization constraints for the plan to the optimization loop UI
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="constraints"></param>
        /// <returns></returns>
        public static string PrintPlanOptimizationConstraints(ExternalPlanSetup plan, List<OptimizationConstraintModel> constraints)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(GetOptimizationObjectivesHeader(plan.Id));
            foreach (OptimizationConstraintModel itr in constraints)
            {
                double dose = itr.QueryDose;
                if (itr.QueryDoseUnits == Units.Percent) dose *= plan.TotalDose.Dose / 100;
                sb.AppendLine(String.Format("{0, -16} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", itr.StructureId, itr.ConstraintType, dose, itr.QueryVolume, itr.Priority));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Helper method to print the plan quality results versus the requested optimization constraints to the optimization loop UI
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="optParams"></param>
        /// <param name="diffPlanOpt"></param>
        /// <param name="totalCostPlanOpt"></param>
        /// <returns></returns>
        public static string PrintPlanOptimizationResultVsConstraints(ExternalPlanSetup plan, 
                                                                      List<OptimizationConstraintModel> optParams, 
                                                                      List<PlanOptConstraintsDeviationModel> diffPlanOpt, 
                                                                      double totalCostPlanOpt)
        {
            StringBuilder sb = new StringBuilder();
            //print the results of the quality check for this optimization
            sb.AppendLine(GetOptimizationResultsHeader(plan.Id));
            int index = 0;
            foreach (PlanOptConstraintsDeviationModel itr in diffPlanOpt)
            {
                //"structure Id", "constraint type", "dose diff^2 (cGy^2)", "current priority", "cost", "cost (%)"
                sb.AppendLine(String.Format("{0, -16} | {1, -16} | {2, -20:N1} | {3, -16} | {4, -12:N1} | {5, -9:N1} |",
                                                itr.Structure.Id, optParams.ElementAt(index).ConstraintType.ToString(), itr.DoseDifferenceSquared, itr.Prioirty, itr.OptimizationCost, 100 * itr.OptimizationCost / totalCostPlanOpt));
                index++;
            }
            return sb.ToString();
        }
    }
}
