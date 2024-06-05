using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.PlanTemplateClasses;
using VMATTBICSIAutoPlanningHelpers.Models;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class ConfigurationUIHelper
    {
        /// <summary>
        /// Helper method to print the parameters contained in each of the supplied plan templates. Necessary method for optimization loop UI
        /// </summary>
        /// <param name="templates"></param>
        /// <returns></returns>
        public static StringBuilder PrintPlanTemplateConfigurationParameters(List<AutoPlanTemplateBase> templates)
        {
            if (templates.First() is CSIAutoPlanTemplate) return PrintCSIPlanTemplateConfigurationParameters(templates.Cast<CSIAutoPlanTemplate>().ToList());
            else return PrintTBIPlanTemplateConfigurationParameters(templates.Cast<TBIAutoPlanTemplate>().ToList());
        }

        /// <summary>
        /// Helper method to print the CSI plan template parameters
        /// </summary>
        /// <param name="templates"></param>
        /// <returns></returns>
        public static StringBuilder PrintCSIPlanTemplateConfigurationParameters(List<CSIAutoPlanTemplate> templates) 
        { 
            StringBuilder sb = new StringBuilder();
            foreach (CSIAutoPlanTemplate itr in templates.Where(x => !string.Equals(x.TemplateName, "--select--")))
            {
                sb.AppendLine("-----------------------------------------------------------------------------");

                sb.AppendLine($" Template ID: {itr.TemplateName}");
                sb.AppendLine($" Initial Dose per fraction: {itr.InitialRxDosePerFx} cGy");
                sb.AppendLine($" Initial number of fractions: {itr.InitialRxNumberOfFractions}");
                sb.AppendLine($" Boost Dose per fraction: {itr.BoostRxDosePerFx} cGy");
                sb.AppendLine($" Boost number of fractions: {itr.BoostRxNumberOfFractions}");

                sb.Append(PrintTargetsTSParameters(itr));

                if (itr.Rings.Any())
                {
                    sb.AppendLine(String.Format(" {0} ring structures:", itr.TemplateName));
                    sb.AppendLine(String.Format("  {0, -15} | {1, -11} | {2, -14} | {3,-10} |", "target Id", "margin (cm)", "thickness (cm)", "dose (cGy)"));
                    foreach (Models.TSRingStructureModel ring in itr.Rings) sb.AppendLine(String.Format("  {0, -15} | {1, -11} | {2, -14} | {3,-10} |", ring.TargetId, ring.MarginFromTargetInCM, ring.RingThicknessInCM, ring.DoseLevel));
                    sb.AppendLine(Environment.NewLine);
                }
                else sb.AppendLine(String.Format(" No requested ring structures for template: {0}", itr.TemplateName));

                if (itr.CropAndOverlapStructures.Any())
                {
                    sb.AppendLine(String.Format(" {0} requested structures for crop/overlap with targets:", itr.TemplateName));
                    sb.AppendLine(String.Format("  {0, -15}", "structure Id"));
                    foreach (string cropOverlap in itr.CropAndOverlapStructures) sb.AppendLine(String.Format("  {0}", cropOverlap));
                    sb.AppendLine(Environment.NewLine);
                }
                else sb.AppendLine(String.Format(" No structures requested for crop/overlap with targets for template: {0}", itr.TemplateName));

                if (itr.InitialOptimizationConstraints.Any())
                {
                    sb.AppendLine($" {itr.TemplateName} template initial plan optimization parameters:");
                    sb.AppendLine(String.Format("  {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority"));
                    foreach (OptimizationConstraintModel opt in itr.InitialOptimizationConstraints) sb.AppendLine(String.Format("  {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", opt.StructureId, opt.ConstraintType, opt.QueryDose, opt.QueryVolume, opt.Priority));
                    sb.AppendLine(Environment.NewLine);
                }
                else sb.AppendLine($" No iniital plan optimization constraints for template: {itr.TemplateName}");

                if (itr.BoostOptimizationConstraints.Any())
                {
                    sb.AppendLine($" {itr.TemplateName} template boost plan optimization parameters:");
                    sb.AppendLine(String.Format("  {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority"));
                    foreach (OptimizationConstraintModel opt in itr.BoostOptimizationConstraints) sb.AppendLine(String.Format("  {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", opt.StructureId, opt.ConstraintType, opt.QueryDose, opt.QueryVolume, opt.Priority));
                    sb.AppendLine(Environment.NewLine);
                }
                else sb.AppendLine($" No boost plan optimization constraints for template: {itr.TemplateName}");

                sb.Append(PrintPlanObjRequestedInfo(itr));
            }
            return sb;
        }

        /// <summary>
        /// Helper method to print the TBI plan template parameters
        /// </summary>
        /// <param name="templates"></param>
        /// <returns></returns>
        public static StringBuilder PrintTBIPlanTemplateConfigurationParameters(List<TBIAutoPlanTemplate> templates)
        {
            StringBuilder sb = new StringBuilder();
            foreach (TBIAutoPlanTemplate itr in templates.Where(x => !string.Equals(x.TemplateName, "--select--")))
            {
                sb.AppendLine("----------------------------------------------------------------------------");

                sb.AppendLine($" Template ID: {itr.TemplateName}");
                sb.AppendLine($" Initial Dose per fraction: {itr.InitialRxDosePerFx} cGy");
                sb.AppendLine($" Initial number of fractions: {itr.InitialRxNumberOfFractions}");

                sb.Append(PrintTargetsTSParameters(itr));

                if (itr.InitialOptimizationConstraints.Any())
                {
                    sb.AppendLine($" {itr.TemplateName} template initial plan optimization parameters:");
                    sb.AppendLine(String.Format("  {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority"));
                    foreach (OptimizationConstraintModel opt in itr.InitialOptimizationConstraints) sb.AppendLine(String.Format("  {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", opt.StructureId, opt.ConstraintType, opt.QueryDose, opt.QueryVolume, opt.Priority));
                    sb.AppendLine(Environment.NewLine);
                }
                else sb.AppendLine($" No iniital plan optimization constraints for template: {itr.TemplateName}");

                sb.Append(PrintPlanObjRequestedInfo(itr));
            }
            return sb;
        }

        /// <summary>
        /// Helper method to print the targets and tuning structure information
        /// </summary>
        /// <param name="itr"></param>
        /// <returns></returns>
        private static StringBuilder PrintTargetsTSParameters(AutoPlanTemplateBase itr)
        {
            StringBuilder sb = new StringBuilder();
            if (itr.PlanTargets.Any())
            {
                sb.AppendLine($" {itr.TemplateName} targets:");
                sb.AppendLine(String.Format("  {0, -15} | {1, -8} | {3, -14} |", "structure Id", "Rx (cGy)", "Num Fx", "Plan Id"));
                foreach (PlanTargetsModel tgt in itr.PlanTargets)
                {
                    foreach(TargetModel planTargets in tgt.Targets)
                    {
                        sb.AppendLine(String.Format("  {0, -15} | {1, -8} | {2,-14:N1} |", planTargets.TargetId, planTargets.TargetRxDose, tgt.PlanId));
                    }
                }
                sb.AppendLine(Environment.NewLine);
            }
            else sb.AppendLine($" No targets set for template: {itr.TemplateName}");

            if (itr.CreateTSStructures.Any())
            {
                sb.AppendLine($" {itr.TemplateName} additional tuning structures:");
                sb.AppendLine(String.Format("  {0, -10} | {1, -15} |", "DICOM type", "Structure Id"));
                foreach (RequestedTSStructureModel ts in itr.CreateTSStructures) sb.AppendLine(String.Format("  {0, -10} | {1, -15} |", ts.DICOMType, ts.StructureId));
                sb.AppendLine(Environment.NewLine);
            }
            else sb.AppendLine($" No additional tuning structures for template: {itr.TemplateName}");

            if (itr.TSManipulations.Any())
            {
                sb.AppendLine($" {itr.TemplateName} additional sparing structures:");
                sb.AppendLine(String.Format("  {0, -15} | {1, -26} | {2, -11} |", "structure Id", "sparing type", "margin (cm)"));
                foreach (RequestedTSManipulationModel spare in itr.TSManipulations) sb.AppendLine(String.Format("  {0, -15} | {1, -26} | {2,-11:N1} |", spare.StructureId, spare.ManipulationType, spare.MarginInCM));
                sb.AppendLine(Environment.NewLine);
            }
            else sb.AppendLine($" No additional sparing structures for template: {itr.TemplateName}");
            return sb;
        }

        /// <summary>
        /// Helper method to print the plan objectives contained in the supplied auto plan template object
        /// </summary>
        /// <param name="itr"></param>
        /// <returns></returns>
        private static StringBuilder PrintPlanObjRequestedInfo(AutoPlanTemplateBase itr)
        {
            StringBuilder sb = new StringBuilder();
            if (itr.RequestedPlanMetrics.Any())
            {
                sb.AppendLine($" {itr.TemplateName} template requested dosimetric info after each iteration:");
                sb.AppendLine(String.Format(" {0, -15} | {1, -6} | {2, -9} |", "structure Id", "metric", "dose type"));

                foreach (RequestedPlanMetricModel info in itr.RequestedPlanMetrics)
                {
                    if (info.DVHMetric == DVHMetric.Dmax || info.DVHMetric == DVHMetric.Dmin) sb.AppendLine(String.Format(" {0, -15} | {1, -6} | {2, -9} |", info.StructureId, info.DVHMetric, info.QueryResultUnits));
                    else sb.AppendLine(String.Format(" {0, -15} | {1, -6} | {2, -9} |", info.StructureId, $"{info.DVHMetric} {info.QueryValue} {info.QueryUnits}", info.QueryResultUnits));
                }
                sb.AppendLine(Environment.NewLine);
            }
            else sb.AppendLine($" No requested dosimetric info for template: {itr.TemplateName}");

            if (itr.PlanObjectives.Any())
            {
                sb.AppendLine($" {itr.TemplateName} template plan objectives:");
                sb.AppendLine(String.Format(" {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -9} |", "structure Id", "constraint type", "dose", "volume (%)", "dose type"));
                foreach (PlanObjectiveModel obj in itr.PlanObjectives)
                {
                    sb.AppendLine(String.Format(" {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-9} |", obj.StructureId, obj.ConstraintType, obj.QueryDose, obj.QueryVolume, obj.QueryDoseUnits));
                }
                sb.AppendLine(Environment.NewLine);
            }
            else sb.AppendLine($" No plan objectives for template: {itr.TemplateName}");

            if (itr.RequestedOptimizationTSStructures.Any())
            {
                sb.AppendLine($" {itr.TemplateName} template requested tuning structures:");
                sb.AppendLine(String.Format(" {0, -15} | {1, -9} | {2, -10} | {3, -5} | {4, -8} | {5, -10} |", "structure Id", "low D (%)", "high D (%)", "V (%)", "priority", "constraint"));
                foreach (RequestedOptimizationTSStructureModel ts in itr.RequestedOptimizationTSStructures)
                {
                    if(ts.GetType() == typeof(TSCoolerStructureModel)) sb.Append(String.Format(" {0, -15} | {1, -9:N1} | {2,-10:N1} | {3,-5:N1} | {4,-8} |", ts.TSStructureId, "", (ts as TSCoolerStructureModel).UpperDoseValue, ts.Constraints.First().QueryVolume, ts.Constraints.First().Priority));
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
            }
            else sb.AppendLine($" No requested heater/cooler structures for template: {itr.TemplateName}");
            return sb;
        }
    }
}
