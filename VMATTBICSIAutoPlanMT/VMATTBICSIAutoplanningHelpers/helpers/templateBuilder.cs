using System;
using System.Linq;
using VMATTBICSIAutoPlanningHelpers.PlanTemplateClasses;
using VMATTBICSIAutoPlanningHelpers.Enums;
using System.Text;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class TemplateBuilder
    {
        /// <summary>
        /// Helper method to generate a text preview of the prospective plan template
        /// </summary>
        /// <param name="prospectiveTemplate"></param>
        /// <returns></returns>
        public static StringBuilder GenerateTemplatePreviewText(AutoPlanTemplateBase prospectiveTemplate)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine($" {DateTime.Now}");
            output.AppendLine($" Template ID: {prospectiveTemplate.GetTemplateName()}");
            output.AppendLine(PrintRxDosePerFxAndNumFx(prospectiveTemplate).ToString());
            output.AppendLine(PrintCommonTemplateSetupInfo(prospectiveTemplate).ToString());
            if (prospectiveTemplate is CSIAutoPlanTemplate) output.AppendLine(PrintCSIPlanSpecificInfo(prospectiveTemplate as CSIAutoPlanTemplate).ToString());
            else output.AppendLine(PrintTBIPlanSpecificInfo(prospectiveTemplate as TBIAutoPlanTemplate).ToString());
            output.AppendLine("-------------------------------------------------------------------------");
            return output;
        }

        /// <summary>
        /// Helper method to print the dose per fractions and number of fractions for the prospective plan template
        /// </summary>
        /// <param name="prospectiveTemplate"></param>
        /// <returns></returns>
        private static StringBuilder PrintRxDosePerFxAndNumFx(AutoPlanTemplateBase prospectiveTemplate)
        {
            StringBuilder output = new StringBuilder();
            if (prospectiveTemplate is CSIAutoPlanTemplate)
            {
                output.AppendLine($" Initial Dose per fraction: {(prospectiveTemplate as CSIAutoPlanTemplate).GetInitialRxDosePerFx()} cGy");
                output.AppendLine($" Initial Dose per fraction: {(prospectiveTemplate as CSIAutoPlanTemplate).GetInitialRxNumFx()} cGy");
                output.AppendLine($" Boost Dose per fraction: {(prospectiveTemplate as CSIAutoPlanTemplate).GetBoostRxDosePerFx()} cGy");
                output.AppendLine($" Boost Dose per fraction: {(prospectiveTemplate as CSIAutoPlanTemplate).GetBoostRxNumFx()} cGy");
            }
            else
            {
                output.AppendLine($" Initial Dose per fraction: {(prospectiveTemplate as TBIAutoPlanTemplate).GetInitialRxDosePerFx()} cGy");
                output.AppendLine($" Initial number of fractions: {(prospectiveTemplate as TBIAutoPlanTemplate).GetInitialRxNumFx()}");
            }
            return output;
        }

        /// <summary>
        /// Helper method to print the plan template parameters that are common to both CSI and TBI plans
        /// </summary>
        /// <param name="prospectiveTemplate"></param>
        /// <returns></returns>
        private static StringBuilder PrintCommonTemplateSetupInfo(AutoPlanTemplateBase prospectiveTemplate)
        {
            StringBuilder output = new StringBuilder();
            if (prospectiveTemplate.GetTargets().Any())
            {
                output.AppendLine($" Initial Dose per fraction: {prospectiveTemplate.GetTemplateName()} cGy");
                output.AppendLine(String.Format("  {0, -15} | {1, -8} | {2, -14} |", "structure Id", "Rx (cGy)", "Num Fx", "Plan Id"));
                foreach (Tuple<string, double, string> tgt in prospectiveTemplate.GetTargets()) output.AppendLine(String.Format("  {0, -15} | {1, -8} | {2,-14:N1} |", tgt.Item1, tgt.Item2, tgt.Item3));
            }
            else output.AppendLine($" No targets set for template: {prospectiveTemplate.GetTemplateName()}");
            output.AppendLine("");

            if (prospectiveTemplate.GetCreateTSStructures().Any())
            {
                output.AppendLine($" Initial Dose per fraction: {prospectiveTemplate.GetTemplateName()} cGy");
                output.AppendLine(String.Format("  {0, -10} | {1, -15} |", "DICOM type", "Structure Id"));
                foreach (Tuple<string, string> ts in prospectiveTemplate.GetCreateTSStructures()) output.AppendLine(String.Format("  {0, -10} | {1, -15} |" + Environment.NewLine, ts.Item1, ts.Item2));
            }
            else output.AppendLine($" No additional tuning structures for template: {prospectiveTemplate.GetTemplateName()}");
            output.AppendLine("");

            if (prospectiveTemplate.GetTSManipulations().Any())
            {
                output.AppendLine($" {prospectiveTemplate.GetTemplateName()} additional tuning structure manipulations:");
                output.AppendLine(String.Format("  {0, -15} | {1, -23} | {2, -11} |", "structure Id", "manipulation type", "margin (cm)"));
                foreach (Tuple<string, TSManipulationType, double> spare in prospectiveTemplate.GetTSManipulations()) output.AppendLine(String.Format("  {0, -15} | {1, -23} | {2,-11:N1} |", spare.Item1, spare.Item2.ToString(), spare.Item3));
            }
            else output.AppendLine($" No additional sparing structures for template: {prospectiveTemplate.GetTemplateName()}");
            output.AppendLine("");

            return output;
        }

        /// <summary>
        /// Helper method to pring the plan template info that is specific to CSI plans
        /// </summary>
        /// <param name="prospectiveTemplate"></param>
        /// <returns></returns>
        private static StringBuilder PrintCSIPlanSpecificInfo(CSIAutoPlanTemplate prospectiveTemplate)
        {
            StringBuilder output = new StringBuilder();
            if (prospectiveTemplate.GetCreateRings().Any())
            {
                output.AppendLine($" {prospectiveTemplate.GetTemplateName()} ring structures:");
                output.AppendLine(String.Format("  {0, -15} | {1, -11} | {2, -14} | {3,-10} |", "target Id", "margin (cm)", "thickness (cm)", "dose (cGy)"));
                foreach (Tuple<string, double, double, double> ring in prospectiveTemplate.GetCreateRings()) output.AppendLine(String.Format("  {0, -15} | {1, -11} | {2, -14} | {3,-10} |", ring.Item1, ring.Item2, ring.Item3, ring.Item4));
            }
            else output.AppendLine($" No requested ring structures for template: {prospectiveTemplate.GetTemplateName()}");
            output.AppendLine("");

            if (prospectiveTemplate.GetCropAndOverlapStructures().Any())
            {
                output.AppendLine($" {prospectiveTemplate.GetTemplateName()} requested structures for crop/overlap with targets:");
                output.AppendLine(String.Format("  {0, -15}", "structure Id"));
                foreach (string cropOverlap in prospectiveTemplate.GetCropAndOverlapStructures()) output.AppendLine($"  {cropOverlap}");
            }
            else output.AppendLine($" No structures requested for crop/overlap with targets for template: {prospectiveTemplate.GetTemplateName()}");
            output.AppendLine("");

            if (prospectiveTemplate.GetInitOptimizationConstraints().Any())
            {
                output.AppendLine($" Initial Dose per fraction: {prospectiveTemplate.GetTemplateName()} cGy");
                output.AppendLine(String.Format("  {0, -14} | {1, -15} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority"));
                foreach (Tuple<string, OptimizationObjectiveType, double, double, int> opt in prospectiveTemplate.GetInitOptimizationConstraints()) output.AppendLine(String.Format("  {0, -14} | {1, -15} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", opt.Item1, opt.Item2.ToString(), opt.Item3, opt.Item4, opt.Item5));
            }
            else output.AppendLine(" No iniital plan optimization constraints for template: {prospectiveTemplate.GetTemplateName()}");
            output.AppendLine("");

            if (prospectiveTemplate.GetBoostOptimizationConstraints().Any())
            {
                output.AppendLine($" Initial Dose per fraction: {prospectiveTemplate.GetTemplateName()} cGy");
                output.AppendLine(String.Format("  {0, -14} | {1, -15} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority"));
                foreach (Tuple<string, OptimizationObjectiveType, double, double, int> opt in prospectiveTemplate.GetBoostOptimizationConstraints()) output.AppendLine(String.Format("  {0, -14} | {1, -15} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", opt.Item1, opt.Item2.ToString(), opt.Item3, opt.Item4, opt.Item5));
            }
            else output.AppendLine($" No boost plan optimization constraints for template: {prospectiveTemplate.GetTemplateName()}");
            output.AppendLine("");

            return output;
        }

        /// <summary>
        /// Helper method to pring the plan template info that is specific to TBI plans
        /// </summary>
        /// <param name="prospectiveTemplate"></param>
        /// <returns></returns>
        private static StringBuilder PrintTBIPlanSpecificInfo(TBIAutoPlanTemplate prospectiveTemplate)
        {
            StringBuilder output = new StringBuilder();
            if (prospectiveTemplate.GetInitOptimizationConstraints().Any())
            {
                output.AppendLine($" {prospectiveTemplate.GetTemplateName()} template initial plan optimization parameters:");
                output.AppendLine(String.Format("  {0, -14} | {1, -15} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority"));
                foreach (Tuple<string, OptimizationObjectiveType, double, double, int> opt in prospectiveTemplate.GetInitOptimizationConstraints()) output.AppendLine(String.Format("  {0, -14} | {1, -15} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", opt.Item1, opt.Item2.ToString(), opt.Item3, opt.Item4, opt.Item5));
            }
            else output.AppendLine($" No iniital plan optimization constraints for template: {prospectiveTemplate.GetTemplateName()}");
            output.AppendLine("");

            return output;
        }

        #region serialize template parameters
        /// <summary>
        /// Helper method to serialize the parameters in the supplied prospective plan template object
        /// </summary>
        /// <param name="prospectiveTemplate"></param>
        /// <returns></returns>
        public static StringBuilder GenerateSerializedTemplate(AutoPlanTemplateBase prospectiveTemplate)
        {
            //a bit ugly, but it makes the resulting serialized template look nice
            StringBuilder output = new StringBuilder();
            output.AppendLine(":begin template case configuration:");
            output.AppendLine("%template name");
            output.AppendLine($"template name={prospectiveTemplate.GetTemplateName()}");
            if (prospectiveTemplate is CSIAutoPlanTemplate) output.Append(SerializeCSIRxTemplate(prospectiveTemplate as CSIAutoPlanTemplate));
            else output.Append(SerializeTBIRxTemplate(prospectiveTemplate as TBIAutoPlanTemplate));
            output.Append(SerializeCommonTemplateParameters(prospectiveTemplate));
            if (prospectiveTemplate is CSIAutoPlanTemplate) output.Append(SerializeCSITemplateParameters(prospectiveTemplate as CSIAutoPlanTemplate));
            else output.Append(SerializeTBITemplateParameters(prospectiveTemplate as TBIAutoPlanTemplate));
            output.AppendLine("%");
            output.AppendLine("%");
            output.Append(":end template case configuration:");
            return output;
        }

        /// <summary>
        /// Helper method to serialize the parameters that are specific to CSI plans
        /// </summary>
        /// <param name="prospectiveTemplate"></param>
        /// <returns></returns>
        private static StringBuilder SerializeCSITemplateParameters(CSIAutoPlanTemplate prospectiveTemplate)
        {
            StringBuilder output = new StringBuilder();
            if (prospectiveTemplate.GetCreateRings().Any())
            {
                foreach (Tuple<string, double, double, double> itr in prospectiveTemplate.GetCreateRings()) output.AppendLine($"create ring{{{itr.Item1},{itr.Item2},{itr.Item3},{itr.Item4}}}");
                output.AppendLine("%");
                output.AppendLine("%");
            }
            else output.AppendLine("%");

            if (prospectiveTemplate.GetCropAndOverlapStructures().Any())
            {
                foreach (string itr in prospectiveTemplate.GetCropAndOverlapStructures()) output.AppendLine($"crop and contour overlap with targets{{{itr}}}");
                output.AppendLine("%");
                output.AppendLine("%");
            }
            else output.AppendLine("%");

            if (prospectiveTemplate.GetInitOptimizationConstraints().Any())
            {
                foreach (Tuple<string, OptimizationObjectiveType, double, double, int> itr in prospectiveTemplate.GetInitOptimizationConstraints()) output.AppendLine($"add init opt constraint{{{itr.Item1},{itr.Item2.ToString()},{itr.Item3},{itr.Item4},{itr.Item5}}}");
                output.AppendLine("%");
                output.AppendLine("%");
            }
            else output.AppendLine("%");

            if (prospectiveTemplate.GetBoostOptimizationConstraints().Any())
            {
                foreach (Tuple<string, OptimizationObjectiveType, double, double, int> itr in prospectiveTemplate.GetBoostOptimizationConstraints()) output.AppendLine($"add boost opt constraint{{{itr.Item1},{itr.Item2.ToString()},{itr.Item3},{itr.Item4},{itr.Item5}}}");
                output.AppendLine("%");
                output.AppendLine("%");
            }
            else output.AppendLine("%");
            return output;
        }

        /// <summary>
        /// Helper method to serialize the parameters that are specific to TBI plans
        /// </summary>
        /// <param name="prospectiveTemplate"></param>
        /// <returns></returns>
        private static StringBuilder SerializeTBITemplateParameters(TBIAutoPlanTemplate prospectiveTemplate)
        {
            StringBuilder output = new StringBuilder();
            if (prospectiveTemplate.GetInitOptimizationConstraints().Any())
            {
                foreach (Tuple<string, OptimizationObjectiveType, double, double, int> itr in prospectiveTemplate.GetInitOptimizationConstraints()) output.AppendLine($"add init opt constraint{{{itr.Item1},{itr.Item2.ToString()},{itr.Item3},{itr.Item4},{itr.Item5}}}");
                output.AppendLine("%");
                output.AppendLine("%");
            }
            else output.AppendLine("%");
            return output;
        }

        /// <summary>
        /// Helper method to serialize the parameters that are common to both CSI and TBI plans
        /// </summary>
        /// <param name="prospectiveTemplate"></param>
        /// <returns></returns>
        private static StringBuilder SerializeCommonTemplateParameters(AutoPlanTemplateBase prospectiveTemplate)
        {
            StringBuilder output = new StringBuilder();
            if (prospectiveTemplate.GetTargets().Any())
            {
                foreach (Tuple<string, double, string> itr in prospectiveTemplate.GetTargets()) output.AppendLine($"add target{{{itr.Item1},{itr.Item2},{itr.Item3}}}");
                output.AppendLine("%");
                output.AppendLine("%");
            }
            else output.AppendLine("%");

            if (prospectiveTemplate.GetCreateTSStructures().Any())
            {
                foreach (Tuple<string, string> itr in prospectiveTemplate.GetCreateTSStructures()) output.AppendLine($"add TS{{{itr.Item1},{itr.Item2}}}");
                output.AppendLine("%");
                output.AppendLine("%");
            }
            else output.AppendLine("%");

            if (prospectiveTemplate.GetTSManipulations().Any())
            {
                foreach (Tuple<string, TSManipulationType, double> itr in prospectiveTemplate.GetTSManipulations()) output.AppendLine($"add sparing structure{{{itr.Item1},{itr.Item2.ToString()},{itr.Item3}}}");
                output.AppendLine("%");
                output.AppendLine("%");
            }
            else output.AppendLine("%");
            return output; 
        }

        /// <summary>
        /// Helper method to serialize the dose per fraction and number of fractions for CSI plans
        /// </summary>
        /// <param name="prospectiveTemplate"></param>
        /// <returns></returns>
        private static StringBuilder SerializeCSIRxTemplate(CSIAutoPlanTemplate prospectiveTemplate)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("%initial dose per fraction(cGy) and num fractions");
            output.AppendLine($"initial dose per fraction={prospectiveTemplate.GetInitialRxDosePerFx()}");
            output.AppendLine($"initial num fx={prospectiveTemplate.GetInitialRxDosePerFx()}");
            if (prospectiveTemplate.GetBoostRxDosePerFx() > 0.1)
            {
                output.AppendLine("%boost dose per fraction(cGy) and num fractions");
                output.AppendLine($"boost dose per fraction={prospectiveTemplate.GetBoostRxDosePerFx()}");
                output.AppendLine($"boost num fx={prospectiveTemplate.GetBoostRxNumFx()}");
            }
            output.AppendLine("%");
            output.AppendLine("%");
            return output;
        }

        /// <summary>
        /// Helper method to serialize the dose per fraction and number of fractions for TBI plans
        /// </summary>
        /// <param name="prospectiveTemplate"></param>
        /// <returns></returns>
        private static StringBuilder SerializeTBIRxTemplate(TBIAutoPlanTemplate prospectiveTemplate)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("%initial dose per fraction(cGy) and num fractions");
            output.AppendLine($"dose per fraction={prospectiveTemplate.GetInitialRxDosePerFx()}");
            output.AppendLine($"num fx={prospectiveTemplate.GetInitialRxDosePerFx()}");
            output.AppendLine("%");
            output.AppendLine("%");
            return output;
        }
        #endregion
    }
}
