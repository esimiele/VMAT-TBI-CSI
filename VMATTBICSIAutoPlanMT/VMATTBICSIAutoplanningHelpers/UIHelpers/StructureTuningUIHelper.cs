using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using System.Windows;
using System.Windows.Controls;
using System.Text;
using TSManipulationType = VMATTBICSIAutoPlanningHelpers.Enums.TSManipulationType;
using VMATTBICSIAutoPlanningHelpers.Helpers;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public class StructureTuningUIHelper
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
            string[] types = new string[] { "--select--", 
                                            "AVOIDANCE", 
                                            "CAVITY", 
                                            "CONTRAST_AGENT", 
                                            "CTV", 
                                            "EXTERNAL", 
                                            "GTV", 
                                            "IRRAD_VOLUME",
                                            "ORGAN", 
                                            "PTV", 
                                            "TREATED_VOLUME", 
                                            "SUPPORT", 
                                            "FIXATION",
                                            "CONTROL", 
                                            "DOSE_REGION" };
            
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

            if (!string.Equals(listItem.Item2, "--select--")) str_cb.Items.Add("--select--");
            //this code is used to fix the issue where the structure exists in the structure set, but doesn't populate as the default option in the combo box.
            int index = 0;
            //j is initially 1 because we already added "--select--" to the combo box
            int j = 1;
            foreach (Structure s in selectedSS.Structures)
            {
                str_cb.Items.Add(s.Id);
                if (string.Equals(s.Id.ToLower(),listItem.Item2.ToLower())) index = j;
                j++;
            }
            //if the structure does not exist in the structure set, add the requested structure id to the combobox option and set the selected index to the last item
            if (!selectedSS.Structures.Any(x => string.Equals(x.Id.ToLower(), listItem.Item2.ToLower())))
            {
                str_cb.Items.Add(listItem.Item2);
                str_cb.SelectedIndex = str_cb.Items.Count - 1;
            }
            else
            {
                str_cb.SelectedIndex = index;
            }
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

        public StackPanel GetTSManipulationHeader(StackPanel theSP)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(40, 0, 5, 5);

            Label strName = new Label();
            strName.Content = "Structure Name";
            strName.HorizontalAlignment = HorizontalAlignment.Center;
            strName.VerticalAlignment = VerticalAlignment.Top;
            strName.Width = 150;
            strName.FontSize = 14;
            strName.Margin = new Thickness(27, 0, 0, 0);

            Label spareType = new Label();
            spareType.Content = "Sparing Type";
            spareType.HorizontalAlignment = HorizontalAlignment.Center;
            spareType.VerticalAlignment = VerticalAlignment.Top;
            spareType.Width = 150;
            spareType.FontSize = 14;
            spareType.Margin = new Thickness(10, 0, 0, 0);

            Label marginLabel = new Label();
            marginLabel.Content = "Margin (cm)";
            marginLabel.HorizontalAlignment = HorizontalAlignment.Center;
            marginLabel.VerticalAlignment = VerticalAlignment.Top;
            marginLabel.Width = 150;
            marginLabel.FontSize = 14;
            marginLabel.Margin = new Thickness(0, 0, 0, 0);

            sp.Children.Add(strName);
            sp.Children.Add(spareType);
            sp.Children.Add(marginLabel);

            return sp;
        }

        public StackPanel AddTSManipulation(StackPanel theSP, List<string> structureIds, Tuple<string, TSManipulationType, double> listItem, string clearBtnPrefix, int clearSpareBtnCounter, SelectionChangedEventHandler typeChngHndl, RoutedEventHandler clearEvtHndl)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(40, 0, 5, 5);

            ComboBox str_cb = new ComboBox();
            str_cb.Name = "str_cb";
            str_cb.Width = 150;
            str_cb.Height = sp.Height - 5;
            str_cb.HorizontalAlignment = HorizontalAlignment.Left;
            str_cb.VerticalAlignment = VerticalAlignment.Top;
            str_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            str_cb.Margin = new Thickness(5, 5, 0, 0);

            str_cb.Items.Add("--select--");
            //this code is used to fix the issue where the structure exists in the structure set, but doesn't populate as the default option in the combo box.
            int index = 0;
            //j is initially 1 because we already added "--select--" to the combo box
            int j = 1;
            foreach (string itr in structureIds)
            {
                str_cb.Items.Add(itr);
                if (itr.ToLower() == listItem.Item1.ToLower()) index = j;
                j++;
            }
            str_cb.SelectedIndex = index;
            sp.Children.Add(str_cb);

            ComboBox type_cb = new ComboBox();
            type_cb.Name = "type_cb";
            type_cb.Width = 150;
            type_cb.Height = sp.Height - 5;
            type_cb.HorizontalAlignment = HorizontalAlignment.Left;
            type_cb.VerticalAlignment = VerticalAlignment.Top;
            type_cb.Margin = new Thickness(5, 5, 0, 0);
            type_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            string[] types = new string[] { "--select--", 
                                            "Crop target from structure", 
                                            "Contour overlap with target", 
                                            "Crop from Body", 
                                            "Mean Dose < Rx Dose", 
                                            "Dmax ~ Rx Dose" };
            foreach (string s in types) type_cb.Items.Add(s);
            if (types.Any(x => string.Equals(x, listItem.Item2.ToString()))) type_cb.Text = listItem.Item2.ToString();
            else type_cb.Text = "--select--";
            type_cb.SelectionChanged += typeChngHndl;
            sp.Children.Add(type_cb);

            TextBox addMargin = new TextBox();
            addMargin.Name = "addMargin_tb";
            addMargin.Width = 120;
            addMargin.Height = sp.Height - 5;
            addMargin.HorizontalAlignment = HorizontalAlignment.Left;
            addMargin.VerticalAlignment = VerticalAlignment.Top;
            addMargin.TextAlignment = TextAlignment.Center;
            addMargin.VerticalContentAlignment = VerticalAlignment.Center;
            addMargin.Margin = new Thickness(5, 5, 0, 0);
            addMargin.Text = Convert.ToString(listItem.Item3);
            if (listItem.Item2 != TSManipulationType.CropTargetFromStructure && listItem.Item2 != TSManipulationType.CropFromBody) addMargin.Visibility = Visibility.Hidden;
            sp.Children.Add(addMargin);

            Button clearStructBtn = new Button();
            clearStructBtn.Name = clearBtnPrefix + clearSpareBtnCounter;
            clearStructBtn.Content = "Clear";
            clearStructBtn.Click += clearEvtHndl;
            clearStructBtn.Width = 50;
            clearStructBtn.Height = sp.Height - 5;
            clearStructBtn.HorizontalAlignment = HorizontalAlignment.Left;
            clearStructBtn.VerticalAlignment = VerticalAlignment.Top;
            clearStructBtn.Margin = new Thickness(10, 5, 0, 0);
            sp.Children.Add(clearStructBtn);

            return sp;
        }

        public (List<Tuple<string, TSManipulationType, double>>, StringBuilder) ParseTSManipulationList(StackPanel theSP)
        {
            StringBuilder sb = new StringBuilder();
            List<Tuple<string, TSManipulationType, double>> TSManipulationList = new List<Tuple<string, TSManipulationType, double>> { };
            string structure = "";
            string spareType = "";
            double margin = -1000.0;
            bool firstCombo = true;
            bool headerObj = true;
            foreach (object obj in theSP.Children)
            {
                //skip over the header row
                if (!headerObj)
                {
                    foreach (object obj1 in ((StackPanel)obj).Children)
                    {
                        if (obj1.GetType() == typeof(ComboBox))
                        {
                            //first combo box is the structure and the second is the sparing type
                            if (firstCombo)
                            {
                                structure = (obj1 as ComboBox).SelectedItem.ToString();
                                firstCombo = false;
                            }
                            else spareType = (obj1 as ComboBox).SelectedItem.ToString();
                        }
                        //try to parse the margin value as a double
                        else if (obj1.GetType() == typeof(TextBox)) if (!string.IsNullOrWhiteSpace((obj1 as TextBox).Text)) double.TryParse((obj1 as TextBox).Text, out margin);
                    }
                    if (structure == "--select--" || spareType == "--select--")
                    {
                        sb.AppendLine("Error! \nStructure or Sparing Type not selected! \nSelect an option and try again");
                        return (new List<Tuple<string, TSManipulationType, double>> { }, sb);
                    }
                    //margin will not be assigned from the default value (-1000) if the input is empty, a whitespace, or NaN
                    else if (margin == -1000.0)
                    {
                        sb.AppendLine("Error! \nEntered margin value is invalid! \nEnter a new margin and try again");
                        return (new List<Tuple<string, TSManipulationType, double>> { }, sb);
                    }
                    //only add the current row to the structure sparing list if all the parameters were successful parsed
                    else TSManipulationList.Add(Tuple.Create(structure, TSManipulationTypeHelper.GetTSManipulationType(spareType), margin));
                    firstCombo = true;
                    margin = -1000.0;
                }
                else headerObj = false;
            }

            return (TSManipulationList, sb);
        }

        public (List<Tuple<string, string>>, StringBuilder) ParseCreateTSStructureList(StackPanel theSP)
        {
            StringBuilder sb = new StringBuilder();
            List<Tuple<string, string>> TSStructureList = new List<Tuple<string, string>> { };
            string dcmType = "";
            string structure = "";
            bool firstCombo = true;
            bool headerObj = true;
            foreach (object obj in theSP.Children)
            {
                //skip over the header row
                if (!headerObj)
                {
                    foreach (object obj1 in ((StackPanel)obj).Children)
                    {
                        if (obj1.GetType() == typeof(ComboBox))
                        {
                            //first combo box is the structure and the second is the sparing type
                            if (firstCombo)
                            {
                                dcmType = (obj1 as ComboBox).SelectedItem.ToString();
                                firstCombo = false;
                            }
                            else structure = (obj1 as ComboBox).SelectedItem.ToString();
                        }
                    }
                    if (dcmType == "--select--" || structure == "--select--")
                    {
                        sb.AppendLine("Error! \nStructure or DICOM Type not selected! \nSelect an option and try again");
                        return (new List<Tuple<string, string>> { }, sb);
                    }
                    //only add the current row to the structure sparing list if all the parameters were successful parsed
                    else TSStructureList.Add(Tuple.Create(dcmType, structure));
                    firstCombo = true;
                }
                else headerObj = false;
            }

            return (TSStructureList, sb);
        }
    }
}
