using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using System.Windows;
using System.Windows.Controls;
using System.Text;
using TSManipulationType = VMATTBICSIAutoPlanningHelpers.Enums.TSManipulationType;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class StructureTuningUIHelper
    {
        #region TS Generation
        /// <summary>
        /// Helper method to add the header information for TS Generation sub tab
        /// </summary>
        /// <param name="theSP"></param>
        /// <returns></returns>
        public static StackPanel AddTemplateTSHeader(StackPanel theSP)
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = theSP.Width,
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 5)
            };

            Label dcmType = new Label
            {
                Content = "DICOM Type",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 115,
                FontSize = 14,
                Margin = new Thickness(10, 0, 0, 0)
            };

            Label strName = new Label
            {
                Content = "Structure Name",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 150,
                FontSize = 14,
                Margin = new Thickness(80, 0, 0, 0)
            };

            sp.Children.Add(dcmType);
            sp.Children.Add(strName);

            return sp;
        }

        /// <summary>
        /// Helper method to add a requested tuning structure item to the TS Generation sub tab
        /// </summary>
        /// <param name="theSP"></param>
        /// <param name="selectedSS"></param>
        /// <param name="listItem"></param>
        /// <param name="clearBtnPrefix"></param>
        /// <param name="clearBtnCounter"></param>
        /// <param name="clearEvtHndl"></param>
        /// <returns></returns>
        public static StackPanel AddTSVolume(StackPanel theSP, 
                                             StructureSet selectedSS, 
                                             RequestedTSStructureModel listItem, 
                                             string clearBtnPrefix, 
                                             int clearBtnCounter, 
                                             RoutedEventHandler clearEvtHndl)
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = theSP.Width,
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 5)
            };

            ComboBox type_cb = new ComboBox
            {
                Name = "type_cb",
                Width = 150,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(45, 5, 0, 0),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
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
            type_cb.Text = listItem.DICOMType;
            sp.Children.Add(type_cb);

            ComboBox str_cb = new ComboBox();
            str_cb.Name = "str_cb";
            str_cb.Width = 150;
            str_cb.Height = sp.Height - 5;
            str_cb.HorizontalAlignment = HorizontalAlignment.Left;
            str_cb.VerticalAlignment = VerticalAlignment.Top;
            str_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            str_cb.Margin = new Thickness(50, 5, 0, 0);

            if (!string.Equals(listItem.StructureId, "--select--")) str_cb.Items.Add("--select--");
            //this code is used to fix the issue where the structure exists in the structure set, but doesn't populate as the default option in the combo box.
            int index = 0;
            //j is initially 1 because we already added "--select--" to the combo box
            int j = 1;
            foreach (Structure s in selectedSS.Structures)
            {
                str_cb.Items.Add(s.Id);
                if (string.Equals(s.Id.ToLower(),listItem.StructureId.ToLower())) index = j;
                j++;
            }
            //if the structure does not exist in the structure set, add the requested structure id to the combobox option and set the selected index to the last item
            if (!selectedSS.Structures.Any(x => string.Equals(x.Id.ToLower(), listItem.StructureId.ToLower())))
            {
                str_cb.Items.Add(listItem.StructureId);
                str_cb.SelectedIndex = str_cb.Items.Count - 1;
            }
            else
            {
                str_cb.SelectedIndex = index;
            }
            sp.Children.Add(str_cb);

            Button clearStructBtn = new Button
            {
                Name = clearBtnPrefix + clearBtnCounter,
                Content = "Clear",
                Width = 50,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(20, 5, 0, 0)
            };
            clearStructBtn.Click += clearEvtHndl;
            sp.Children.Add(clearStructBtn);

            return sp;
        }

        /// <summary>
        /// Helper method to parse the requested tuning structure generations from the TS Generation sub tab. Returns the list of requested
        /// TS structure generations
        /// </summary>
        /// <param name="theSP"></param>
        /// <returns></returns>
        public static (List<RequestedTSStructureModel>, StringBuilder) ParseCreateTSStructureList(StackPanel theSP)
        {
            StringBuilder sb = new StringBuilder();
            List<RequestedTSStructureModel> TSStructureList = new List<RequestedTSStructureModel> { };
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
                        return (new List<RequestedTSStructureModel> { }, sb);
                    }
                    //only add the current row to the structure sparing list if all the parameters were successful parsed
                    else TSStructureList.Add(new RequestedTSStructureModel(dcmType, structure));
                    firstCombo = true;
                }
                else headerObj = false;
            }
            return (TSStructureList, sb);
        }
        #endregion

        #region TS Manipulation
        /// <summary>
        /// Helper method to build the header information for TS Manipulation to add to the TS Manipulation sub tab
        /// </summary>
        /// <param name="theSP"></param>
        /// <returns></returns>
        public static StackPanel GetTSManipulationHeader(StackPanel theSP)
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = 450,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition());

            Label strName = new Label
            {
                Content = "Structure Name",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 150,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 0)
            };

            Label spareType = new Label
            {
                Content = "Sparing Type",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 150,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 0)
            };

            Label marginLabel = new Label
            {
                Content = "Margin (cm)",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 140,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 0)
            };
            g.Children.Add(strName);
            g.Children.Add(spareType);
            g.Children.Add(marginLabel);

            Grid.SetColumn(strName, 0);
            Grid.SetColumn(spareType, 1);
            Grid.SetColumn(marginLabel, 2);

            sp.Children.Add(g);

            return sp;
        }

        /// <summary>
        /// Helper method to add a structure manipulation item to the TS Manipulation sub tab
        /// </summary>
        /// <param name="theSP"></param>
        /// <param name="structureIds"></param>
        /// <param name="listItem"></param>
        /// <param name="clearBtnPrefix"></param>
        /// <param name="clearSpareBtnCounter"></param>
        /// <param name="typeChngHndl"></param>
        /// <param name="clearEvtHndl"></param>
        /// <param name="skipStructureIdCheck"></param>
        /// <returns></returns>
        public static StackPanel AddTSManipulation(StackPanel theSP, 
                                                   List<string> structureIds, 
                                                   RequestedTSManipulationModel listItem, 
                                                   string clearBtnPrefix, 
                                                   int clearSpareBtnCounter, 
                                                   SelectionChangedEventHandler typeChngHndl, 
                                                   RoutedEventHandler clearEvtHndl, 
                                                   bool skipStructureIdCheck)
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = theSP.Width,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(30, 0, 5, 5)
            };

            ComboBox str_cb = new ComboBox
            {
                Name = "str_cb",
                Width = 150,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5, 5, 0, 0)
            };

            str_cb.Items.Add("--select--");
            if(skipStructureIdCheck)
            {
                foreach (string itr in structureIds)
                {
                    str_cb.Items.Add(itr);
                }
                str_cb.Items.Add(listItem.StructureId);
                str_cb.SelectedIndex = str_cb.Items.Count - 1;
            }
            else
            {
                //this code is used to fix the issue where the structure exists in the structure set, but doesn't populate as the default option in the combo box.
                int index = 0;
                //j is initially 1 because we already added "--select--" to the combo box
                int j = 1;
                foreach (string itr in structureIds)
                {
                    str_cb.Items.Add(itr);
                    if (itr.ToLower() == listItem.StructureId.ToLower()) index = j;
                    j++;
                }
                str_cb.SelectedIndex = index;
            }
            sp.Children.Add(str_cb);

            ComboBox type_cb = new ComboBox
            {
                Name = "type_cb",
                Width = 150,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(5, 5, 0, 0),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            
            //add the possible manipulation types to the manipulation type combo box
            foreach (TSManipulationType s in Enum.GetValues(typeof(TSManipulationType))) type_cb.Items.Add(s);
            if ((int)listItem.ManipulationType <= type_cb.Items.Count) type_cb.SelectedIndex = (int)listItem.ManipulationType;
            else type_cb.SelectedIndex = 0;
            type_cb.SelectionChanged += typeChngHndl;
            sp.Children.Add(type_cb);

            TextBox addMargin = new TextBox
            {
                Name = "addMargin_tb",
                Width = 110,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 5, 0, 0),
                Text = String.Format("{0:0.0}", listItem.MarginInCM)
            };
            if (listItem.ManipulationType == TSManipulationType.None) addMargin.Visibility = Visibility.Hidden;
            sp.Children.Add(addMargin);

            Button clearStructBtn = new Button
            {
                Name = clearBtnPrefix + clearSpareBtnCounter,
                Content = "Clear",
                Width = 50,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 5, 0, 0)
            };
            clearStructBtn.Click += clearEvtHndl;
            sp.Children.Add(clearStructBtn);

            return sp;
        }

        /// <summary>
        /// Helper method to take the parsed list of structure manipulations and evaluate if the structures exist or will exist (post L/R union) in the 
        /// structure set
        /// </summary>
        /// <param name="manipulationListIds"></param>
        /// <param name="idsPostUnion"></param>
        /// <param name="ss"></param>
        /// <returns></returns>
        public static (List<string>, StringBuilder) VerifyTSManipulationIntputIntegrity(List<string> manipulationListIds, 
                                                                                        List<string> idsPostUnion, 
                                                                                        StructureSet ss)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Warning! The following structures are null or empty and can't be used for TS manipulation:");
            List<string> missingEmptyList = new List<string> { };
            foreach (string itr in manipulationListIds)
            {
                //check to ensure the structures in the manipulationListIds are actually present in the selected structure set
                //and are actually contoured.
                if (StructureTuningHelper.DoesStructureExistInSS(itr, ss))
                {
                    //already exists in current structure set, check if it is empty
                    if (!StructureTuningHelper.DoesStructureExistInSS(itr, ss, true))
                    {
                        //it's in the structure set, but it's not contoured
                        missingEmptyList.Add(itr);
                        sb.AppendLine(itr);
                    }
                }
                else if (!idsPostUnion.Any(x => string.Equals(x.ToLower(), itr.ToLower())))
                {
                    //check if this structure will be unioned in the generateTS class
                    missingEmptyList.Add(itr);
                    sb.AppendLine(itr);
                }
            }

            return (missingEmptyList, sb);
        }

        /// <summary>
        /// Helper method to parse the requested tuning structure manipulations from the TS Manipulation sub tab. Returns the list of requested
        /// structure manipulations
        /// </summary>
        /// <param name="theSP"></param>
        /// <returns></returns>
        public static (List<RequestedTSManipulationModel>, StringBuilder) ParseTSManipulationList(StackPanel theSP)
        {
            StringBuilder sb = new StringBuilder();
            List<RequestedTSManipulationModel> TSManipulationList = new List<RequestedTSManipulationModel> { };
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
                        else if (obj1.GetType() == typeof(TextBox))
                        {
                            //try to parse the margin value as a double
                            if (!string.IsNullOrWhiteSpace((obj1 as TextBox).Text)) double.TryParse((obj1 as TextBox).Text, out margin);
                        }
                    }
                    if (structure == "--select--" || spareType == "--select--")
                    {
                        sb.AppendLine("Error! \nStructure or Sparing Type not selected! \nSelect an option and try again");
                        return (new List<RequestedTSManipulationModel> { }, sb);
                    }
                    //margin will not be assigned from the default value (-1000) if the input is empty, a whitespace, or NaN
                    else if (margin == -1000.0)
                    {
                        sb.AppendLine("Error! \nEntered margin value is invalid! \nEnter a new margin and try again");
                        return (new List<RequestedTSManipulationModel> { }, sb);
                    }
                    //only add the current row to the structure sparing list if all the parameters were successful parsed
                    else TSManipulationList.Add(new RequestedTSManipulationModel(structure, TSManipulationTypeHelper.GetTSManipulationType(spareType), margin));
                    firstCombo = true;
                    margin = -1000.0;
                }
                else headerObj = false;
            }
            return (TSManipulationList, sb);
        }
        #endregion
    }
}
