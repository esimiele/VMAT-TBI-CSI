using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows;
using System.Collections.Generic;
using VMS.TPS.Common.Model.API;
using VMATTBICSIAutoplanningHelpers.TemplateClasses;

namespace VMATTBICSIAutoplanningHelpers.Helpers
{
    public class TemplateBuilder
    {
        public string GenerateTemplatePreviewText(CSIAutoPlanTemplate prospectiveTemplate)
        {
            string output = "";
            output += String.Format(" {0}", DateTime.Now.ToString()) + Environment.NewLine;

            output += String.Format(" Template ID: {0}", prospectiveTemplate.GetTemplateName()) + Environment.NewLine;
            output += String.Format(" Initial Dose per fraction: {0} cGy", prospectiveTemplate.GetInitialRxDosePerFx()) + Environment.NewLine;
            output += String.Format(" Initial number of fractions: {0}", prospectiveTemplate.GetInitialRxNumFx()) + Environment.NewLine;
            output += String.Format(" Boost Dose per fraction: {0} cGy", prospectiveTemplate.GetBoostRxDosePerFx()) + Environment.NewLine;
            output += String.Format(" Boost number of fractions: {0}", prospectiveTemplate.GetBoostRxNumFx()) + Environment.NewLine;

            if (prospectiveTemplate.GetTargets().Any())
            {
                output += String.Format(" {0} targets:", prospectiveTemplate.GetTemplateName()) + Environment.NewLine;
                output += String.Format("  {0, -15} | {1, -8} | {2, -14} |", "structure Id", "Rx (cGy)", "Plan Id") + Environment.NewLine;
                foreach (Tuple<string, double, string> tgt in prospectiveTemplate.GetTargets()) output += String.Format("  {0, -15} | {1, -8} | {2,-14:N1} |" + Environment.NewLine, tgt.Item1, tgt.Item2, tgt.Item3);
                output += Environment.NewLine;
            }
            else output += String.Format(" No targets set for template: {0}", prospectiveTemplate.GetTemplateName()) + Environment.NewLine + Environment.NewLine;

            if (prospectiveTemplate.GetCreateTSStructures().Any())
            {
                output += String.Format(" {0} additional tuning structures:", prospectiveTemplate.GetTemplateName()) + Environment.NewLine;
                output += String.Format("  {0, -10} | {1, -15} |", "DICOM type", "Structure Id") + Environment.NewLine;
                foreach (Tuple<string, string> ts in prospectiveTemplate.GetCreateTSStructures()) output += String.Format("  {0, -10} | {1, -15} |" + Environment.NewLine, ts.Item1, ts.Item2);
                output += Environment.NewLine;
            }
            else output += String.Format(" No additional tuning structures for template: {0}", prospectiveTemplate.GetTemplateName()) + Environment.NewLine + Environment.NewLine;

            if (prospectiveTemplate.GetCreateRings().Any())
            {
                output += String.Format(" {0} ring structures:", prospectiveTemplate.GetTemplateName()) + Environment.NewLine;
                output += String.Format("  {0, -15} | {1, -11} | {2, -14} | {3,-10} |", "target Id", "margin (cm)", "thickness (cm)", "dose (cGy)") + Environment.NewLine;
                foreach (Tuple<string, double, double, double> ring in prospectiveTemplate.GetCreateRings()) output += String.Format("  {0, -15} | {1, -11} | {2, -14} | {3,-10} |" + Environment.NewLine, ring.Item1, ring.Item2, ring.Item3, ring.Item4);
                output += Environment.NewLine;
            }
            else output += String.Format(" No requested ring structures for template: {0}", prospectiveTemplate.GetTemplateName()) + Environment.NewLine + Environment.NewLine;

            if (prospectiveTemplate.GetTSManipulations().Any())
            {
                output += String.Format(" {0} additional tuning structure manipulations:", prospectiveTemplate.GetTemplateName()) + Environment.NewLine;
                output += String.Format("  {0, -15} | {1, -19} | {2, -11} |", "structure Id", "sparing type", "margin (cm)") + Environment.NewLine;
                foreach (Tuple<string, string, double> spare in prospectiveTemplate.GetTSManipulations()) output += String.Format("  {0, -15} | {1, -19} | {2,-11:N1} |" + Environment.NewLine, spare.Item1, spare.Item2, spare.Item3);
                output += Environment.NewLine;
            }
            else output += String.Format(" No additional sparing structures for template: {0}", prospectiveTemplate.GetTemplateName()) + Environment.NewLine + Environment.NewLine;

            if (prospectiveTemplate.GetInitOptimizationConstraints().Any())
            {
                output += String.Format(" {0} template initial plan optimization parameters:", prospectiveTemplate.GetTemplateName()) + Environment.NewLine;
                output += String.Format("  {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority") + Environment.NewLine;
                foreach (Tuple<string, string, double, double, int> opt in prospectiveTemplate.GetInitOptimizationConstraints()) output += String.Format("  {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |" + Environment.NewLine, opt.Item1, opt.Item2, opt.Item3, opt.Item4, opt.Item5);
                output += Environment.NewLine;
            }
            else output += String.Format(" No iniital plan optimization constraints for template: {0}", prospectiveTemplate.GetTemplateName()) + Environment.NewLine + Environment.NewLine;

            if (prospectiveTemplate.GetBoostOptimizationConstraints().Any())
            {
                output += String.Format(" {0} template boost plan optimization parameters:", prospectiveTemplate.GetTemplateName()) + Environment.NewLine;
                output += String.Format("  {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority") + Environment.NewLine;
                foreach (Tuple<string, string, double, double, int> opt in prospectiveTemplate.GetBoostOptimizationConstraints()) output += String.Format("  {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |" + Environment.NewLine, opt.Item1, opt.Item2, opt.Item3, opt.Item4, opt.Item5);
            }
            else output += String.Format(" No boost plan optimization constraints for template: {0}", prospectiveTemplate.GetTemplateName()) + Environment.NewLine + Environment.NewLine;

            output += "-----------------------------------------------------------------------------" + Environment.NewLine;
            return output;
        }

        public string GenerateSerializedTemplate(CSIAutoPlanTemplate prospectiveTemplate)
        {
            string output = ":begin template case configuration:" + Environment.NewLine;
            output += "%template name" + Environment.NewLine;
            output += String.Format("template name={0}", prospectiveTemplate.GetTemplateName()) + Environment.NewLine;
            output += "%initial dose per fraction(cGy) and num fractions" + Environment.NewLine;
            output += String.Format("initial dose per fraction={0}", prospectiveTemplate.GetInitialRxDosePerFx()) + Environment.NewLine;
            output += String.Format("initial num fx={0}", prospectiveTemplate.GetInitialRxDosePerFx()) + Environment.NewLine;
            if (prospectiveTemplate.GetBoostRxDosePerFx() > 0.1)
            {
                output += "%boost dose per fraction(cGy) and num fractions" + Environment.NewLine;
                output += String.Format("boost dose per fraction={0}", prospectiveTemplate.GetBoostRxDosePerFx()) + Environment.NewLine;
                output += String.Format("boost num fx={0}", prospectiveTemplate.GetBoostRxNumFx()) + Environment.NewLine;
            }
            output += "%" + Environment.NewLine;
            output += "%" + Environment.NewLine;

            if (prospectiveTemplate.GetTargets().Any())
            {
                foreach (Tuple<string, double, string> itr in prospectiveTemplate.GetTargets()) output += String.Format("add target{{{0},{1},{2}}}", itr.Item1, itr.Item2, itr.Item3) + Environment.NewLine;
                output += "%" + Environment.NewLine;
                output += "%" + Environment.NewLine;
            }

            if (prospectiveTemplate.GetCreateTSStructures().Any())
            {
                foreach (Tuple<string, string> itr in prospectiveTemplate.GetCreateTSStructures()) output += String.Format("add TS{{{0},{1}}}", itr.Item1, itr.Item2) + Environment.NewLine;
                output += "%" + Environment.NewLine;
                output += "%" + Environment.NewLine;
            }

            if (prospectiveTemplate.GetCreateRings().Any())
            {
                foreach (Tuple<string, double, double, double> itr in prospectiveTemplate.GetCreateRings()) output += String.Format("create ring{{{0},{1},{2},{3}}}", itr.Item1, itr.Item2, itr.Item3, itr.Item4) + Environment.NewLine;
                output += "%" + Environment.NewLine;
                output += "%" + Environment.NewLine;
            }

            if (prospectiveTemplate.GetTSManipulations().Any())
            {
                foreach (Tuple<string, string, double> itr in prospectiveTemplate.GetTSManipulations()) output += String.Format("add sparing structure{{{0},{1},{2}}}", itr.Item1, itr.Item2, itr.Item3) + Environment.NewLine;
                output += "%" + Environment.NewLine;
                output += "%" + Environment.NewLine;
            }

            if (prospectiveTemplate.GetInitOptimizationConstraints().Any())
            {
                foreach (Tuple<string, string, double, double, int> itr in prospectiveTemplate.GetInitOptimizationConstraints()) output += String.Format("add init opt constraint{{{0},{1},{2},{3},{4}}}", itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5) + Environment.NewLine;
                output += "%" + Environment.NewLine;
                output += "%" + Environment.NewLine;
            }

            if (prospectiveTemplate.GetBoostOptimizationConstraints().Any())
            {
                foreach (Tuple<string, string, double, double, int> itr in prospectiveTemplate.GetBoostOptimizationConstraints()) output += String.Format("add boost opt constraint{{{0},{1},{2},{3},{4}}}", itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5) + Environment.NewLine;
                output += "%" + Environment.NewLine;
                output += "%" + Environment.NewLine;
            }
            output += "%" + Environment.NewLine;
            output += "%" + Environment.NewLine;

            output += ":end template case configuration:";
            return output;
        }
    }
}
