using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using PlanType = VMATTBICSIAutoPlanningHelpers.Enums.PlanType;
using VMATTBICSIAutoPlanningHelpers.Helpers;

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
        public static string PrintPlanObjectives(List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> planObj)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(GetPlanObjectivesHeader());
            foreach (Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation> itr in planObj)
            {
                //"structure Id", "constraint type", "dose (cGy or %)", "volume (%)", "Dose display (absolute or relative)"
                sb.AppendLine(String.Format("{0, -16} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-9} |", itr.Item1, itr.Item2.ToString(), itr.Item3, itr.Item4, itr.Item5));
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

            foreach (ExternalPlanSetup itr in plans)
            {
                sb.AppendLine($"     Plan: {itr.Id}, PTV V{itr.TotalDose.Dose}cGy = {targetCoverage:0.0}%");
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
        public static string PrintAdditionalPlanDoseInfo(List<Tuple<string, string, double, string>> requestedInfo, 
                                                         ExternalPlanSetup plan, 
                                                         List<Tuple<string, string>> normalizationVolumes)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(Environment.NewLine + $"Additional infomation for plan: {plan.Id}");
            foreach (Tuple<string, string, double, string> itr in requestedInfo)
            {
                if (itr.Item1.Contains("<plan>"))
                {
                    if (itr.Item2 == "Dmax") sb.AppendLine($"Plan global Dmax = {100 * (plan.Dose.DoseMax3D.Dose / plan.TotalDose.Dose):0.0}%");
                    else sb.AppendLine($"Cannot retrive metric ({itr.Item1},{itr.Item2},{itr.Item3},{itr.Item4})! Skipping!");
                }
                else
                {
                    string structureId;
                    if (itr.Item1.Contains("<target>"))
                    {
                        structureId = OptimizationLoopHelper.GetNormaliztionVolumeIdForPlan(plan.Id, normalizationVolumes);
                    }
                    else structureId = itr.Item1;
                    Structure structure = StructureTuningHelper.GetStructureFromId(structureId, plan.StructureSet);
                    if (structure != null)
                    {
                        if (itr.Item2.Contains("max") || itr.Item2.Contains("min"))
                        {
                            sb.AppendLine(String.Format("{0} {1} = {2:0.0}{3}",
                                            structure.Id,
                                            itr.Item2,
                                            plan.GetDoseAtVolume(structure, itr.Item2 == "Dmax" ? 0.0 : 100.0, VolumePresentation.Relative, itr.Item4 == "Relative" ? DoseValuePresentation.Relative : DoseValuePresentation.Absolute).Dose,
                                            itr.Item4 == "Relative" ? "%" : "cGy"));
                        }
                        else
                        {
                            if (itr.Item2 == "D")
                            {
                                //dose at specified volume requested
                                sb.AppendLine(String.Format("{0} {1}{2}% = {3:0.0}{4}",
                                            structure.Id,
                                            itr.Item2,
                                            itr.Item3,
                                            plan.GetDoseAtVolume(structure, itr.Item3, VolumePresentation.Relative, itr.Item4 == "Relative" ? DoseValuePresentation.Relative : DoseValuePresentation.Absolute).Dose,
                                            itr.Item4 == "Relative" ? "%" : "cGy"));
                            }
                            else
                            {
                                //volume at specified dose requested
                                sb.AppendLine(String.Format("{0} {1}{2}% = {3:0.0}{4}",
                                            structure.Id,
                                            itr.Item2,
                                            itr.Item3,
                                            plan.GetVolumeAtDose(structure, new DoseValue(itr.Item3, DoseValue.DoseUnit.Percent), itr.Item4 == "Relative" ? VolumePresentation.Relative : VolumePresentation.AbsoluteCm3),
                                            itr.Item4 == "Relative" ? "%" : "cc"));
                            }
                        }
                    }
                    else sb.AppendLine($"Cannot retrive metric ({itr.Item1},{itr.Item2},{itr.Item3},{itr.Item4})! Skipping!");
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
        public static string PrintRequestedTSStructures(List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> requestedTSstructures)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Requested tuning structures:");
            sb.AppendLine("--------------------------------------------------------------------------");
            sb.AppendLine(String.Format("{0, -16} | {1, -9} | {2, -10} | {3, -5} | {4, -8} | {5, -10} |", "structure Id", "low D (%)", "high D (%)", "V (%)", "priority", "constraint"));
            sb.AppendLine("--------------------------------------------------------------------------");

            foreach (Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>> itr in requestedTSstructures)
            {
                sb.Append(String.Format("{0, -16} | {1, -9:N1} | {2,-10:N1} | {3,-5:N1} | {4,-8} |", itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5));
                if (!itr.Item6.Any()) sb.AppendLine(String.Format(" {0,-10} |", "none"));
                else
                {
                    int index = 0;
                    foreach (Tuple<string, double, string, double> itr1 in itr.Item6)
                    {
                        if (index == 0)
                        {
                            if (itr1.Item1.Contains("Dmax")) sb.Append(String.Format(" {0,-10} |", $"{itr1.Item1}{itr1.Item3}{itr1.Item4}%"));
                            else if (itr1.Item1.Contains("V")) sb.Append(String.Format(" {0,-10} |", $"{itr1.Item1}{itr1.Item2}%{itr1.Item3}{itr1.Item4}%"));
                            else sb.Append(String.Format(" {0,-10} |", $"{itr1.Item1}"));
                        }
                        else
                        {
                            if (itr1.Item1.Contains("Dmax")) sb.Append(String.Format(" {0,-59} | {1,-10} |", " ", $"{itr1.Item1}{itr1.Item3}{itr1.Item4}%"));
                            else if (itr1.Item1.Contains("V")) sb.Append(String.Format(" {0,-59} | {1,-10} |", " ", $"{itr1.Item1}{itr1.Item2}%{itr1.Item3}{itr1.Item4}%"));
                            else sb.Append(String.Format(" {0,-59} | {1,-10} |", " ", $"{itr1.Item1}"));
                        }
                        index++;
                        if (index <= itr.Item6.Count) sb.Append(Environment.NewLine);
                    }
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Helper method to print the optimization constraints for the plan to the optimization loop UI
        /// </summary>
        /// <param name="planId"></param>
        /// <param name="constraints"></param>
        /// <returns></returns>
        public static string PrintPlanOptimizationConstraints(string planId, List<Tuple<string, OptimizationObjectiveType, double, double, int>> constraints)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(GetOptimizationObjectivesHeader(planId));
            foreach (Tuple<string, OptimizationObjectiveType, double, double, int> itr in constraints)
            {
                sb.AppendLine(String.Format("{0, -16} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", itr.Item1, itr.Item2.ToString(), itr.Item3, itr.Item4, itr.Item5));
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
                                                                      List<Tuple<string, OptimizationObjectiveType, double, double, int>> optParams, 
                                                                      List<Tuple<Structure, DVHData, double, double, double, int>> diffPlanOpt, 
                                                                      double totalCostPlanOpt)
        {
            StringBuilder sb = new StringBuilder();
            //print the results of the quality check for this optimization
            sb.AppendLine(GetOptimizationResultsHeader(plan.Id));
            int index = 0;
            //structure, dvh data, current dose obj, dose diff^2, cost, current priority, priority difference
            foreach (Tuple<Structure, DVHData, double, double, double, int> itr in diffPlanOpt)
            {
                //"structure Id", "constraint type", "dose diff^2 (cGy^2)", "current priority", "cost", "cost (%)"
                sb.AppendLine(String.Format("{0, -16} | {1, -16} | {2, -20:N1} | {3, -16} | {4, -12:N1} | {5, -9:N1} |",
                                                itr.Item1.Id, optParams.ElementAt(index).Item2.ToString(), itr.Item4, itr.Item6, itr.Item5, 100 * itr.Item5 / totalCostPlanOpt));
                index++;
            }
            return sb.ToString();
        }
    }
}
