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
        public StackPanel AddTemplateTSHeader(StackPanel theSP)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Margin = new Thickness(5, 0, 5, 5);

            Label dcmType = new Label();
            dcmType.Content = "DICOM Type";
            dcmType.HorizontalAlignment = HorizontalAlignment.Center;
            dcmType.VerticalAlignment = VerticalAlignment.Top;
            dcmType.Width = 115;
            dcmType.FontSize = 14;
            dcmType.Margin = new Thickness(5, 0, 0, 0);

            Label strName = new Label();
            strName.Content = "Structure Name";
            strName.HorizontalAlignment = HorizontalAlignment.Center;
            strName.VerticalAlignment = VerticalAlignment.Top;
            strName.Width = 150;
            strName.FontSize = 14;
            strName.Margin = new Thickness(80, 0, 0, 0);

            sp.Children.Add(dcmType);
            sp.Children.Add(strName);

            return sp;
        }

        public List<Tuple<string,string>> ParseTSStructureList(StackPanel theSP)
        {
            return new UIHelpers.StructureTuningUIHelper().ParseCreateTSStructureList(theSP);
        }

        public StackPanel AddTSVolume(StackPanel theSP, StructureSet selectedSS, Tuple<string, string> listItem, string clearBtnPrefix, int clearBtnCounter, RoutedEventHandler clearEvtHndl)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Margin = new Thickness(5, 0, 5, 5);

            ComboBox type_cb = new ComboBox();
            type_cb.Name = "type_cb";
            type_cb.Width = 150;
            type_cb.Height = sp.Height - 5;
            type_cb.HorizontalAlignment = HorizontalAlignment.Left;
            type_cb.VerticalAlignment = VerticalAlignment.Top;
            type_cb.Margin = new Thickness(45, 5, 0, 0);
            type_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            string[] types = new string[] { "--select--", "AVOIDANCE", "CAVITY", "CONTRAST_AGENT", "CTV", "EXTERNAL", "GTV", "IRRAD_VOLUME", "ORGAN", "PTV", "TREATED_VOLUME", "SUPPORT", "FIXATION", "CONTROL", "DOSE_REGION" };
            foreach (string s in types) type_cb.Items.Add(s);
            type_cb.Text = listItem.Item1;
            sp.Children.Add(type_cb);

            ComboBox str_cb = new ComboBox();
            str_cb.Name = "str_cb";
            str_cb.Width = 150;
            str_cb.Height = sp.Height - 5;
            str_cb.HorizontalAlignment = HorizontalAlignment.Left;
            str_cb.VerticalAlignment = VerticalAlignment.Top;
            str_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            str_cb.Margin = new Thickness(50, 5, 0, 0);

            if(listItem.Item2 != "--select--") str_cb.Items.Add("--select--");
            //this code is used to fix the issue where the structure exists in the structure set, but doesn't populate as the default option in the combo box.
            int index = 0;
            //j is initially 1 because we already added "--select--" to the combo box
            int j = 1;
            foreach (Structure s in selectedSS.Structures)
            {
                str_cb.Items.Add(s.Id);
                if (s.Id.ToLower() == listItem.Item2.ToLower()) index = j;
                j++;
            }
            //if the structure does not exist in the structure set, add the requested structure id to the combobox option and set the selected index to the last item
            if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == listItem.Item2.ToLower()) == null)
            {
                str_cb.Items.Add(listItem.Item2);
                str_cb.SelectedIndex = str_cb.Items.Count - 1;
            }
            else str_cb.SelectedIndex = index;
            sp.Children.Add(str_cb);

            Button clearStructBtn = new Button();
            clearStructBtn.Name = clearBtnPrefix + clearBtnCounter;
            clearStructBtn.Content = "Clear";
            clearStructBtn.Click += clearEvtHndl;
            clearStructBtn.Width = 50;
            clearStructBtn.Height = sp.Height - 5;
            clearStructBtn.HorizontalAlignment = HorizontalAlignment.Left;
            clearStructBtn.VerticalAlignment = VerticalAlignment.Top;
            clearStructBtn.Margin = new Thickness(20, 5, 0, 0);
            sp.Children.Add(clearStructBtn);

            return sp;
        }

        public string GenerateTemplatePreviewText(CSIAutoPlanTemplate prospectiveTemplate)
        {
            string output = "";
            output += String.Format(" {0}", DateTime.Now.ToString()) + Environment.NewLine;

            output += String.Format(" Template ID: {0}", prospectiveTemplate.TemplateName) + Environment.NewLine;
            output += String.Format(" Initial Dose per fraction: {0} cGy", prospectiveTemplate.initialRxDosePerFx) + Environment.NewLine;
            output += String.Format(" Initial number of fractions: {0}", prospectiveTemplate.initialRxNumFx) + Environment.NewLine;
            output += String.Format(" Boost Dose per fraction: {0} cGy", prospectiveTemplate.boostRxDosePerFx) + Environment.NewLine;
            output += String.Format(" Boost number of fractions: {0}", prospectiveTemplate.boostRxNumFx) + Environment.NewLine;

            if (prospectiveTemplate.targets.Any())
            {
                output += String.Format(" {0} targets:", prospectiveTemplate.TemplateName) + Environment.NewLine;
                output += String.Format("  {0, -15} | {1, -8} | {2, -14} |", "structure Id", "Rx (cGy)", "Plan Id") + Environment.NewLine;
                foreach (Tuple<string, double, string> tgt in prospectiveTemplate.targets) output += String.Format("  {0, -15} | {1, -8} | {2,-14:N1} |" + Environment.NewLine, tgt.Item1, tgt.Item2, tgt.Item3);
                output += Environment.NewLine;
            }
            else output += String.Format(" No targets set for template: {0}", prospectiveTemplate.TemplateName) + Environment.NewLine + Environment.NewLine;

            if (prospectiveTemplate.createTSStructures.Any())
            {
                output += String.Format(" {0} additional tuning structures:", prospectiveTemplate.TemplateName) + Environment.NewLine;
                output += String.Format("  {0, -10} | {1, -15} |", "DICOM type", "Structure Id") + Environment.NewLine;
                foreach (Tuple<string, string> ts in prospectiveTemplate.createTSStructures) output += String.Format("  {0, -10} | {1, -15} |" + Environment.NewLine, ts.Item1, ts.Item2);
                output += Environment.NewLine;
            }
            else output += String.Format(" No additional tuning structures for template: {0}", prospectiveTemplate.TemplateName) + Environment.NewLine + Environment.NewLine;

            if (prospectiveTemplate.TSManipulations.Any())
            {
                output += String.Format(" {0} additional sparing structures:", prospectiveTemplate.TemplateName) + Environment.NewLine;
                output += String.Format("  {0, -15} | {1, -19} | {2, -11} |", "structure Id", "sparing type", "margin (cm)") + Environment.NewLine;
                foreach (Tuple<string, string, double> spare in prospectiveTemplate.TSManipulations) output += String.Format("  {0, -15} | {1, -19} | {2,-11:N1} |" + Environment.NewLine, spare.Item1, spare.Item2, spare.Item3);
                output += Environment.NewLine;
            }
            else output += String.Format(" No additional sparing structures for template: {0}", prospectiveTemplate.TemplateName) + Environment.NewLine + Environment.NewLine;

            if (prospectiveTemplate.init_constraints.Any())
            {
                output += String.Format(" {0} template initial plan optimization parameters:", prospectiveTemplate.TemplateName) + Environment.NewLine;
                output += String.Format("  {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority") + Environment.NewLine;
                foreach (Tuple<string, string, double, double, int> opt in prospectiveTemplate.init_constraints) output += String.Format("  {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |" + Environment.NewLine, opt.Item1, opt.Item2, opt.Item3, opt.Item4, opt.Item5);
                output += Environment.NewLine;
            }
            else output += String.Format(" No iniital plan optimization constraints for template: {0}", prospectiveTemplate.TemplateName) + Environment.NewLine + Environment.NewLine;

            if (prospectiveTemplate.bst_constraints.Any())
            {
                output += String.Format(" {0} template boost plan optimization parameters:", prospectiveTemplate.TemplateName) + Environment.NewLine;
                output += String.Format("  {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority") + Environment.NewLine;
                foreach (Tuple<string, string, double, double, int> opt in prospectiveTemplate.bst_constraints) output += String.Format("  {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |" + Environment.NewLine, opt.Item1, opt.Item2, opt.Item3, opt.Item4, opt.Item5);
            }
            else output += String.Format(" No boost plan optimization constraints for template: {0}", prospectiveTemplate.TemplateName) + Environment.NewLine + Environment.NewLine;

            output += "-----------------------------------------------------------------------------" + Environment.NewLine;
            return output;
        }

        public string GenerateSerializedTemplate(CSIAutoPlanTemplate prospectiveTemplate)
        {
            string output = ":begin template case configuration:" + Environment.NewLine;
            output += "%template name" + Environment.NewLine;
            output += String.Format("template name={0}", prospectiveTemplate.TemplateName) + Environment.NewLine;
            output += "%initial dose per fraction(cGy) and num fractions" + Environment.NewLine;
            output += String.Format("initial dose per fraction={0}", prospectiveTemplate.initialRxDosePerFx) + Environment.NewLine;
            output += String.Format("initial num fx={0}", prospectiveTemplate.initialRxDosePerFx) + Environment.NewLine;
            if (prospectiveTemplate.boostRxDosePerFx > 0.1)
            {
                output += "%boost dose per fraction(cGy) and num fractions" + Environment.NewLine;
                output += String.Format("boost dose per fraction={0}", prospectiveTemplate.boostRxDosePerFx) + Environment.NewLine;
                output += String.Format("boost num fx={0}", prospectiveTemplate.boostRxNumFx) + Environment.NewLine;
            }
            output += "%" + Environment.NewLine;
            output += "%" + Environment.NewLine;

            if (prospectiveTemplate.targets.Any())
            {
                foreach (Tuple<string, double, string> itr in prospectiveTemplate.targets) output += String.Format("add target{{{0},{1},{2}}}", itr.Item1, itr.Item2, itr.Item3) + Environment.NewLine;
                output += "%" + Environment.NewLine;
                output += "%" + Environment.NewLine;
            }

            if (prospectiveTemplate.createTSStructures.Any())
            {
                foreach (Tuple<string, string> itr in prospectiveTemplate.createTSStructures) output += String.Format("add TS{{{0},{1}}}", itr.Item1, itr.Item2) + Environment.NewLine;
                output += "%" + Environment.NewLine;
                output += "%" + Environment.NewLine;
            }

            if (prospectiveTemplate.TSManipulations.Any())
            {
                foreach (Tuple<string, string, double> itr in prospectiveTemplate.TSManipulations) output += String.Format("add sparing structure{{{0},{1},{2}}}", itr.Item1, itr.Item2, itr.Item3) + Environment.NewLine;
                output += "%" + Environment.NewLine;
                output += "%" + Environment.NewLine;
            }

            if (prospectiveTemplate.init_constraints.Any())
            {
                foreach (Tuple<string, string, double, double, int> itr in prospectiveTemplate.init_constraints) output += String.Format("add init opt constraint{{{0},{1},{2},{3},{4}}}", itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5) + Environment.NewLine;
                output += "%" + Environment.NewLine;
                output += "%" + Environment.NewLine;
            }

            if (prospectiveTemplate.bst_constraints.Any())
            {
                foreach (Tuple<string, string, double, double, int> itr in prospectiveTemplate.bst_constraints) output += String.Format("add boost opt constraint{{{0},{1},{2},{3},{4}}}", itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5) + Environment.NewLine;
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
