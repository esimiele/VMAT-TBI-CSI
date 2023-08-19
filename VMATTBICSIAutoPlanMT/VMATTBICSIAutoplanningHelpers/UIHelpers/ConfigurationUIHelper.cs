using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.PlanTemplateClasses;
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
            foreach (CSIAutoPlanTemplate itr in templates.Where(x => !string.Equals(x.GetTemplateName(), "--select--")))
            {
                sb.AppendLine("-----------------------------------------------------------------------------");

                sb.AppendLine($" Template ID: {itr.GetTemplateName()}");
                sb.AppendLine($" Initial Dose per fraction: {itr.GetInitialRxDosePerFx()} cGy");
                sb.AppendLine($" Initial number of fractions: {itr.GetInitialRxNumFx()}");
                sb.AppendLine($" Boost Dose per fraction: {itr.GetBoostRxDosePerFx()} cGy");
                sb.AppendLine($" Boost number of fractions: {itr.GetBoostRxNumFx()}");

                sb.Append(PrintTargetsTSParameters(itr));

                if (itr.GetCreateRings().Any())
                {
                    sb.AppendLine(String.Format(" {0} ring structures:", itr.GetTemplateName()));
                    sb.AppendLine(String.Format("  {0, -15} | {1, -11} | {2, -14} | {3,-10} |", "target Id", "margin (cm)", "thickness (cm)", "dose (cGy)"));
                    foreach (Tuple<string, double, double, double> ring in itr.GetCreateRings()) sb.AppendLine(String.Format("  {0, -15} | {1, -11} | {2, -14} | {3,-10} |", ring.Item1, ring.Item2, ring.Item3, ring.Item4));
                    sb.AppendLine(Environment.NewLine);
                }
                else sb.AppendLine(String.Format(" No requested ring structures for template: {0}", itr.GetTemplateName()));

                if (itr.GetCropAndOverlapStructures().Any())
                {
                    sb.AppendLine(String.Format(" {0} requested structures for crop/overlap with targets:", itr.GetTemplateName()));
                    sb.AppendLine(String.Format("  {0, -15}", "structure Id"));
                    foreach (string cropOverlap in itr.GetCropAndOverlapStructures()) sb.AppendLine(String.Format("  {0}", cropOverlap));
                    sb.AppendLine(Environment.NewLine);
                }
                else sb.AppendLine(String.Format(" No structures requested for crop/overlap with targets for template: {0}", itr.GetTemplateName()));

                if (itr.GetInitOptimizationConstraints().Any())
                {
                    sb.AppendLine($" {itr.GetTemplateName()} template initial plan optimization parameters:");
                    sb.AppendLine(String.Format("  {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority"));
                    foreach (Tuple<string, OptimizationObjectiveType, double, double, int> opt in itr.GetInitOptimizationConstraints()) sb.AppendLine(String.Format("  {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", opt.Item1, opt.Item2.ToString(), opt.Item3, opt.Item4, opt.Item5));
                    sb.AppendLine(Environment.NewLine);
                }
                else sb.AppendLine($" No iniital plan optimization constraints for template: {itr.GetTemplateName()}");

                if (itr.GetBoostOptimizationConstraints().Any())
                {
                    sb.AppendLine($" {itr.GetTemplateName()} template boost plan optimization parameters:");
                    sb.AppendLine(String.Format("  {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority"));
                    foreach (Tuple<string, OptimizationObjectiveType, double, double, int> opt in itr.GetBoostOptimizationConstraints()) sb.AppendLine(String.Format("  {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", opt.Item1, opt.Item2.ToString(), opt.Item3, opt.Item4, opt.Item5));
                    sb.AppendLine(Environment.NewLine);
                }
                else sb.AppendLine($" No boost plan optimization constraints for template: {itr.GetTemplateName()}");

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
            foreach (TBIAutoPlanTemplate itr in templates.Where(x => !string.Equals(x.GetTemplateName(), "--select--")))
            {
                sb.AppendLine("----------------------------------------------------------------------------");

                sb.AppendLine($" Template ID: {itr.GetTemplateName()}");
                sb.AppendLine($" Initial Dose per fraction: {itr.GetInitialRxDosePerFx()} cGy");
                sb.AppendLine($" Initial number of fractions: {itr.GetInitialRxNumFx()}");

                sb.Append(PrintTargetsTSParameters(itr));

                if (itr.GetInitOptimizationConstraints().Any())
                {
                    sb.AppendLine($" {itr.GetTemplateName()} template initial plan optimization parameters:");
                    sb.AppendLine(String.Format("  {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority"));
                    foreach (Tuple<string, OptimizationObjectiveType, double, double, int> opt in itr.GetInitOptimizationConstraints()) sb.AppendLine(String.Format("  {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", opt.Item1, opt.Item2.ToString(), opt.Item3, opt.Item4, opt.Item5));
                    sb.AppendLine(Environment.NewLine);
                }
                else sb.AppendLine($" No iniital plan optimization constraints for template: {itr.GetTemplateName()}");

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
            if (itr.GetTargets().Any())
            {
                sb.AppendLine($" {itr.GetTemplateName()} targets:");
                sb.AppendLine(String.Format("  {0, -15} | {1, -8} | {3, -14} |", "structure Id", "Rx (cGy)", "Num Fx", "Plan Id"));
                foreach (Tuple<string, double, string> tgt in itr.GetTargets()) sb.AppendLine(String.Format("  {0, -15} | {1, -8} | {2,-14:N1} |", tgt.Item1, tgt.Item2, tgt.Item3));
                sb.AppendLine(Environment.NewLine);
            }
            else sb.AppendLine($" No targets set for template: {itr.GetTemplateName()}");

            if (itr.GetCreateTSStructures().Any())
            {
                sb.AppendLine($" {itr.GetTemplateName()} additional tuning structures:");
                sb.AppendLine(String.Format("  {0, -10} | {1, -15} |", "DICOM type", "Structure Id"));
                foreach (Tuple<string, string> ts in itr.GetCreateTSStructures()) sb.AppendLine(String.Format("  {0, -10} | {1, -15} |", ts.Item1, ts.Item2));
                sb.AppendLine(Environment.NewLine);
            }
            else sb.AppendLine($" No additional tuning structures for template: {itr.GetTemplateName()}");

            if (itr.GetTSManipulations().Any())
            {
                sb.AppendLine($" {itr.GetTemplateName()} additional sparing structures:");
                sb.AppendLine(String.Format("  {0, -15} | {1, -26} | {2, -11} |", "structure Id", "sparing type", "margin (cm)"));
                foreach (Tuple<string, TSManipulationType, double> spare in itr.GetTSManipulations()) sb.AppendLine(String.Format("  {0, -15} | {1, -26} | {2,-11:N1} |", spare.Item1, spare.Item2.ToString(), spare.Item3));
                sb.AppendLine(Environment.NewLine);
            }
            else sb.AppendLine($" No additional sparing structures for template: {itr.GetTemplateName()}");
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
            if (itr.GetRequestedPlanDoseInfo().Any())
            {
                sb.AppendLine($" {itr.GetTemplateName()} template requested dosimetric info after each iteration:");
                sb.AppendLine(String.Format(" {0, -15} | {1, -6} | {2, -9} |", "structure Id", "metric", "dose type"));

                foreach (Tuple<string, string, double, string> info in itr.GetRequestedPlanDoseInfo())
                {
                    if (info.Item2.Contains("max") || info.Item2.Contains("min")) sb.AppendLine(String.Format(" {0, -15} | {1, -6} | {2, -9} |", info.Item1, info.Item2, info.Item4));
                    else sb.AppendLine(String.Format(" {0, -15} | {1, -6} | {2, -9} |", info.Item1, $"{info.Item2}{info.Item3}%", info.Item4));
                }
                sb.AppendLine(Environment.NewLine);
            }
            else sb.AppendLine($" No requested dosimetric info for template: {itr.GetTemplateName()}");

            if (itr.GetPlanObjectives().Any())
            {
                sb.AppendLine($" {itr.GetTemplateName()} template plan objectives:");
                sb.AppendLine(String.Format(" {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -9} |", "structure Id", "constraint type", "dose", "volume (%)", "dose type"));
                foreach (Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation> obj in itr.GetPlanObjectives())
                {
                    sb.AppendLine(String.Format(" {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-9} |", obj.Item1, obj.Item2.ToString(), obj.Item3, obj.Item4, obj.Item5));
                }
                sb.AppendLine(Environment.NewLine);
            }
            else sb.AppendLine($" No plan objectives for template: {itr.GetTemplateName()}");

            if (itr.GetRequestedOptTSStructures().Any())
            {
                sb.AppendLine($" {itr.GetTemplateName()} template requested tuning structures:");
                sb.AppendLine(String.Format(" {0, -15} | {1, -9} | {2, -10} | {3, -5} | {4, -8} | {5, -10} |", "structure Id", "low D (%)", "high D (%)", "V (%)", "priority", "constraint"));
                foreach (Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>> ts in itr.GetRequestedOptTSStructures())
                {
                    sb.Append(String.Format(" {0, -15} | {1, -9:N1} | {2,-10:N1} | {3,-5:N1} | {4,-8} |", ts.Item1, ts.Item2, ts.Item3, ts.Item4, ts.Item5));
                    if (!ts.Item6.Any()) sb.AppendLine(String.Format(" {0,-10} |", "none"));
                    else
                    {
                        int count = 0;
                        foreach (Tuple<string, double, string, double> ts1 in ts.Item6)
                        {
                            if (count == 0)
                            {
                                if (ts1.Item1.Contains("Dmax")) sb.AppendLine(String.Format(" {0,-10} |", $"{ts1.Item1}{ts1.Item3}{ts1.Item4}%"));
                                else if (ts1.Item1.Contains("V")) sb.AppendLine(String.Format(" {0,-10} |", $"{ts1.Item1}{ts1.Item2}%{ts1.Item3}{ts1.Item4}%"));
                                else sb.AppendLine(String.Format(" {0,-10} |", $"{ts1.Item1}"));
                            }
                            else
                            {
                                if (ts1.Item1.Contains("Dmax")) sb.AppendLine(String.Format(" {0,-59} | {1,-10} |", " ", $"{ts1.Item1}{ts1.Item3}{ts1.Item4}%"));
                                else if (ts1.Item1.Contains("V")) sb.AppendLine(String.Format(" {0,-59} | {1,-10} |", " ", $"{ts1.Item1}{ts1.Item2}%{ts1.Item3}{ts1.Item4}%"));
                                else sb.AppendLine(String.Format(" {0,-59} | {1,-10} |", " ", $"{ts1.Item1}"));
                            }
                            count++;
                        }
                    }
                }
                sb.AppendLine(Environment.NewLine);
            }
            else sb.AppendLine($" No requested heater/cooler structures for template: {itr.GetTemplateName()}");
            return sb;
        }
    }
}
