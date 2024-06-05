using System;
using System.Linq;
using VMATTBICSIAutoPlanningHelpers.PlanTemplateClasses;
using VMATTBICSIAutoPlanningHelpers.Enums;
using System.Text;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Models;

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
            output.AppendLine($" Template ID: {prospectiveTemplate.TemplateName}");
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
                output.AppendLine($" Initial Dose per fraction: {(prospectiveTemplate as CSIAutoPlanTemplate).InitialRxDosePerFx} cGy");
                output.AppendLine($" Initial Dose per fraction: {(prospectiveTemplate as CSIAutoPlanTemplate).InitialRxNumberOfFractions} cGy");
                output.AppendLine($" Boost Dose per fraction: {(prospectiveTemplate as CSIAutoPlanTemplate).BoostRxDosePerFx} cGy");
                output.AppendLine($" Boost Dose per fraction: {(prospectiveTemplate as CSIAutoPlanTemplate).BoostRxNumberOfFractions} cGy");
            }
            else
            {
                output.AppendLine($" Initial Dose per fraction: {(prospectiveTemplate as TBIAutoPlanTemplate).InitialRxDosePerFx} cGy");
                output.AppendLine($" Initial number of fractions: {(prospectiveTemplate as TBIAutoPlanTemplate).InitialRxNumberOfFractions}");
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
            if (prospectiveTemplate.PlanTargets.Any())
            {
                output.AppendLine($" Initial Dose per fraction: {prospectiveTemplate.TemplateName} cGy");
                output.AppendLine(String.Format("  {0, -15} | {1, -8} | {2, -14} |", "structure Id", "Rx (cGy)", "Num Fx", "Plan Id"));
                foreach (PlanTargetsModel tgt in prospectiveTemplate.PlanTargets)
                {
                    foreach(TargetModel itr in tgt.Targets)
                    {
                        output.AppendLine(String.Format("  {0, -15} | {1, -8} | {2,-14:N1} |", itr.TargetId, itr.TargetRxDose, tgt.PlanId));
                    }
                }
            }
            else output.AppendLine($" No targets set for template: {prospectiveTemplate.TemplateName}");
            output.AppendLine("");

            if (prospectiveTemplate.CreateTSStructures.Any())
            {
                output.AppendLine($" Initial Dose per fraction: {prospectiveTemplate.TemplateName} cGy");
                output.AppendLine(String.Format("  {0, -10} | {1, -15} |", "DICOM type", "Structure Id"));
                foreach (RequestedTSStructureModel ts in prospectiveTemplate.CreateTSStructures) output.AppendLine(String.Format("  {0, -10} | {1, -15} |" + Environment.NewLine, ts.DICOMType, ts.StructureId));
            }
            else output.AppendLine($" No additional tuning structures for template: {prospectiveTemplate.TemplateName}");
            output.AppendLine("");

            if (prospectiveTemplate.TSManipulations.Any())
            {
                output.AppendLine($" {prospectiveTemplate.TemplateName} additional tuning structure manipulations:");
                output.AppendLine(String.Format("  {0, -15} | {1, -23} | {2, -11} |", "structure Id", "manipulation type", "margin (cm)"));
                foreach (RequestedTSManipulationModel spare in prospectiveTemplate.TSManipulations) output.AppendLine(String.Format("  {0, -15} | {1, -23} | {2,-11:N1} |", spare.StructureId, spare.ManipulationType, spare.MarginInCM));
            }
            else output.AppendLine($" No additional sparing structures for template: {prospectiveTemplate.TemplateName}");
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
            if (prospectiveTemplate.Rings.Any())
            {
                output.AppendLine($" {prospectiveTemplate.TemplateName} ring structures:");
                output.AppendLine(String.Format("  {0, -15} | {1, -11} | {2, -14} | {3,-10} |", "target Id", "margin (cm)", "thickness (cm)", "dose (cGy)"));
                foreach (TSRingStructureModel ring in prospectiveTemplate.Rings) output.AppendLine(String.Format("  {0, -15} | {1, -11} | {2, -14} | {3,-10} |", ring.TargetId, ring.MarginFromTargetInCM, ring.RingThicknessInCM, ring.DoseLevel));
            }
            else output.AppendLine($" No requested ring structures for template: {prospectiveTemplate.TemplateName}");
            output.AppendLine("");

            if (prospectiveTemplate.CropAndOverlapStructures.Any())
            {
                output.AppendLine($" {prospectiveTemplate.TemplateName} requested structures for crop/overlap with targets:");
                output.AppendLine(String.Format("  {0, -15}", "structure Id"));
                foreach (string cropOverlap in prospectiveTemplate.CropAndOverlapStructures) output.AppendLine($"  {cropOverlap}");
            }
            else output.AppendLine($" No structures requested for crop/overlap with targets for template: {prospectiveTemplate.TemplateName}");
            output.AppendLine("");

            if (prospectiveTemplate.InitialOptimizationConstraints.Any())
            {
                output.AppendLine($" Initial Dose per fraction: {prospectiveTemplate.TemplateName} cGy");
                output.AppendLine(String.Format("  {0, -14} | {1, -15} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority"));
                foreach (OptimizationConstraintModel opt in prospectiveTemplate.InitialOptimizationConstraints) output.AppendLine(String.Format("  {0, -14} | {1, -15} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", opt.StructureId, opt.ConstraintType, opt.QueryDose, opt.QueryVolume, opt.Priority));
            }
            else output.AppendLine(" No iniital plan optimization constraints for template: {prospectiveTemplate.GetTemplateName()}");
            output.AppendLine("");

            if (prospectiveTemplate.BoostOptimizationConstraints.Any())
            {
                output.AppendLine($" Initial Dose per fraction: {prospectiveTemplate.TemplateName} cGy");
                output.AppendLine(String.Format("  {0, -14} | {1, -15} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority"));
                foreach (OptimizationConstraintModel opt in prospectiveTemplate.BoostOptimizationConstraints) output.AppendLine(String.Format("  {0, -14} | {1, -15} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", opt.StructureId, opt.ConstraintType, opt.QueryDose, opt.QueryVolume, opt.Priority));
            }
            else output.AppendLine($" No boost plan optimization constraints for template: {prospectiveTemplate.TemplateName}");
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
            if (prospectiveTemplate.InitialOptimizationConstraints.Any())
            {
                output.AppendLine($" {prospectiveTemplate.TemplateName} template initial plan optimization parameters:");
                output.AppendLine(String.Format("  {0, -14} | {1, -15} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority"));
                foreach (OptimizationConstraintModel opt in prospectiveTemplate.InitialOptimizationConstraints) output.AppendLine(String.Format("  {0, -14} | {1, -15} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", opt.StructureId, opt.ConstraintType, opt.QueryDose, opt.QueryVolume, opt.Priority));
            }
            else output.AppendLine($" No iniital plan optimization constraints for template: {prospectiveTemplate.TemplateName}");
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
            output.AppendLine($"template name={prospectiveTemplate.TemplateName}");
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
            if (prospectiveTemplate.Rings.Any())
            {
                foreach (TSRingStructureModel itr in prospectiveTemplate.Rings) output.AppendLine($"create ring{{{itr.TargetId},{itr.MarginFromTargetInCM},{itr.RingThicknessInCM},{itr.DoseLevel}}}");
                output.AppendLine("%");
                output.AppendLine("%");
            }
            else output.AppendLine("%");

            if (prospectiveTemplate.CropAndOverlapStructures.Any())
            {
                foreach (string itr in prospectiveTemplate.CropAndOverlapStructures) output.AppendLine($"crop and contour overlap with targets{{{itr}}}");
                output.AppendLine("%");
                output.AppendLine("%");
            }
            else output.AppendLine("%");

            if (prospectiveTemplate.InitialOptimizationConstraints.Any())
            {
                foreach (OptimizationConstraintModel itr in prospectiveTemplate.InitialOptimizationConstraints) output.AppendLine($"add init opt constraint{{{itr.StructureId},{itr.ConstraintType},{itr.QueryDose},{itr.QueryVolume},{itr.Priority}}}");
                output.AppendLine("%");
                output.AppendLine("%");
            }
            else output.AppendLine("%");

            if (prospectiveTemplate.BoostOptimizationConstraints.Any())
            {
                foreach (OptimizationConstraintModel itr in prospectiveTemplate.BoostOptimizationConstraints) output.AppendLine($"add boost opt constraint{{{itr.StructureId},{itr.ConstraintType},{itr.QueryDose},{itr.QueryVolume},{itr.Priority}}}");
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
            if (prospectiveTemplate.InitialOptimizationConstraints.Any())
            {
                foreach (OptimizationConstraintModel itr in prospectiveTemplate.InitialOptimizationConstraints) output.AppendLine($"add init opt constraint{{{itr.StructureId},{itr.ConstraintType},{itr.QueryDose},{itr.QueryVolume},{itr.Priority}}}");
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
            if (prospectiveTemplate.PlanTargets.Any())
            {
                foreach (PlanTargetsModel itr in prospectiveTemplate.PlanTargets)
                {
                    foreach(TargetModel tgt in itr.Targets)
                    {
                        output.AppendLine($"add target{{{tgt.TargetId},{tgt.TargetRxDose},{itr.PlanId}}}");
                    }
                }
                output.AppendLine("%");
                output.AppendLine("%");
            }
            else output.AppendLine("%");

            if (prospectiveTemplate.CreateTSStructures.Any())
            {
                foreach (RequestedTSStructureModel itr in prospectiveTemplate.CreateTSStructures) output.AppendLine($"add TS{{{itr.DICOMType},{itr.StructureId}}}");
                output.AppendLine("%");
                output.AppendLine("%");
            }
            else output.AppendLine("%");

            if (prospectiveTemplate.TSManipulations.Any())
            {
                foreach (RequestedTSManipulationModel itr in prospectiveTemplate.TSManipulations) output.AppendLine($"add sparing structure{{{itr.StructureId},{itr.ManipulationType},{itr.MarginInCM}}}");
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
            output.AppendLine($"initial dose per fraction={prospectiveTemplate.InitialRxDosePerFx}");
            output.AppendLine($"initial num fx={prospectiveTemplate.InitialRxNumberOfFractions}");
            if (prospectiveTemplate.BoostRxDosePerFx > 0.1)
            {
                output.AppendLine("%boost dose per fraction(cGy) and num fractions");
                output.AppendLine($"boost dose per fraction={prospectiveTemplate.BoostRxDosePerFx}");
                output.AppendLine($"boost num fx={prospectiveTemplate.BoostRxNumberOfFractions}");
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
            output.AppendLine($"dose per fraction={prospectiveTemplate.InitialRxDosePerFx}");
            output.AppendLine($"num fx={prospectiveTemplate.InitialRxNumberOfFractions}");
            output.AppendLine("%");
            output.AppendLine("%");
            return output;
        }
        #endregion
    }
}
