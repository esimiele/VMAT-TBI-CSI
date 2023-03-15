using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using System.Windows;
using System.Windows.Controls;

namespace VMATTBICSIAutoplanningHelpers.UIHelpers
{
    public class StructureTuningUIHelper
    {
        public StackPanel getSpareStructHeader(StackPanel theSP)
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

        public StackPanel addSpareStructVolume(StackPanel theSP, List<string> structureIds, Tuple<string, string, double> listItem, string clearBtnPrefix, int clearSpareBtnCounter, SelectionChangedEventHandler typeChngHndl, RoutedEventHandler clearEvtHndl)
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
            string[] types = new string[] { "--select--", "Crop target from structure", "Contour overlap", "Crop from Body", "Mean Dose < Rx Dose", "Dmax ~ Rx Dose" };
            foreach (string s in types) type_cb.Items.Add(s);
            if (types.FirstOrDefault(x => x == listItem.Item2) != null) type_cb.Text = listItem.Item2;
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
            if (listItem.Item2 != "Mean Dose < Rx Dose" && listItem.Item2 != "Crop target from structure" && listItem.Item2 != "Crop from Body") addMargin.Visibility = Visibility.Hidden;
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

        public List<Tuple<Structure, Structure, string>> checkStructuresToUnion(StructureSet selectedSS)
        {
            //left structure, right structure, unioned structure name
            List<Tuple<Structure, Structure, string>> structuresToUnion = new List<Tuple<Structure, Structure, string>> { };
            List<Structure> LStructs = selectedSS.Structures.Where(x => x.Id.Substring(x.Id.Length - 2, 2).ToLower() == "_l" || x.Id.Substring(x.Id.Length - 2, 2).ToLower() == " l").ToList();
            List<Structure> RStructs = selectedSS.Structures.Where(x => x.Id.Substring(x.Id.Length - 2, 2).ToLower() == "_r" || x.Id.Substring(x.Id.Length - 2, 2).ToLower() == " r").ToList();
            foreach (Structure itr in LStructs)
            {
                Structure RStruct = RStructs.FirstOrDefault(x => x.Id.Substring(0, x.Id.Length - 2) == itr.Id.Substring(0, itr.Id.Length - 2));
                string newName = AddProperEndingToName(itr.Id.Substring(0, itr.Id.Length - 2).ToLower());
                if (RStruct != null && selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == newName && !x.IsEmpty) == null)
                {
                    if (selectedSS.Structures.FirstOrDefault(x => x.Id == newName) == null) structuresToUnion.Add(new Tuple<Structure, Structure, string>(itr, RStruct, newName));
                }
            }
            return structuresToUnion;
        }

        private string AddProperEndingToName(string initName)
        {
            string unionedName;
            if (initName.Substring(initName.Length - 1, 1) == "y" && initName.Substring(initName.Length - 2, 2) != "ey") unionedName = initName.Substring(0, initName.Length - 1) + "ies";
            else if (initName.Substring(initName.Length - 1, 1) == "s") unionedName = initName + "es";
            else unionedName = initName + "s";
            return unionedName;
        }

        public (bool, string) unionLRStructures(Tuple<Structure, Structure, string> itr, StructureSet selectedSS)
        {
            Structure newStructure = null;
            string newName = itr.Item3;
            try
            {
                Structure existStructure = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == newName);
                //a structure already exists in the structure set with the intended name
                if (existStructure != null) newStructure = existStructure;
                else newStructure = selectedSS.AddStructure("CONTROL", newName);
                newStructure.SegmentVolume = itr.Item1.Margin(0.0);
                newStructure.SegmentVolume = newStructure.Or(itr.Item2.Margin(0.0));
            }
            catch (Exception except) { string message = String.Format("Warning! Could not add structure: {0}\nBecause: {1}", newName, except.Message); return (true, message); }
            return (false, "");
        }

        public List<Tuple<string, string, double>> parseSpareStructList(StackPanel theSP)
        {
            List<Tuple<string, string, double>> structureSpareList = new List<Tuple<string, string, double>> { };
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
                        MessageBox.Show("Error! \nStructure or Sparing Type not selected! \nSelect an option and try again");
                        return new List<Tuple<string, string, double>> { };
                    }
                    //margin will not be assigned from the default value (-1000) if the input is empty, a whitespace, or NaN
                    else if (margin == -1000.0)
                    {
                        MessageBox.Show("Error! \nEntered margin value is invalid! \nEnter a new margin and try again");
                        return new List<Tuple<string, string, double>> { };
                    }
                    //only add the current row to the structure sparing list if all the parameters were successful parsed
                    else structureSpareList.Add(Tuple.Create(structure, spareType, margin));
                    firstCombo = true;
                    margin = -1000.0;
                }
                else headerObj = false;
            }

            return structureSpareList;
        }
    }
}
