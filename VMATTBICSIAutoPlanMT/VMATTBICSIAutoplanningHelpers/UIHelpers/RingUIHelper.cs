using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class RingUIHelper
    {
        /// <summary>
        /// Helper method to print the add ring header information to the UI
        /// </summary>
        /// <param name="theWidth"></param>
        /// <returns></returns>
        public static StackPanel GetRingHeader(double theWidth)
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = theWidth,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(30, 0, 5, 5)
            };

            Label strName = new Label
            {
                Content = "Target Id",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 100,
                FontSize = 14,
                Margin = new Thickness(40, 0, 0, 0)
            };

            Label spareType = new Label
            {
                Content = "Margin (cm)",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 90,
                FontSize = 14,
                Margin = new Thickness(5, 0, 0, 0)
            };

            Label volLabel = new Label
            {
                Content = "Thickness (cm)",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 100,
                FontSize = 14,
                Margin = new Thickness(10, 0, 0, 0)
            };

            Label doseLabel = new Label
            {
                Content = "Dose (cGy)",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 80,
                FontSize = 14,
                Margin = new Thickness(15, 0, 0, 0)
            };

            sp.Children.Add(strName);
            sp.Children.Add(spareType);
            sp.Children.Add(volLabel);
            sp.Children.Add(doseLabel);
            return sp;
        }

        /// <summary>
        /// Helper method to add a ring item to the UI
        /// </summary>
        /// <param name="theSP"></param>
        /// <param name="targetIds"></param>
        /// <param name="listItem"></param>
        /// <param name="clearBtnPrefix"></param>
        /// <param name="clearSpareBtnCounter"></param>
        /// <param name="clearEvtHndl"></param>
        /// <param name="addTargetEvenIfNotInSS"></param>
        /// <returns></returns>
        public static StackPanel AddRing(StackPanel theSP, 
                                         List<string> targetIds, 
                                         TSRingStructureModel item, 
                                         string clearBtnPrefix, 
                                         int clearSpareBtnCounter, 
                                         RoutedEventHandler clearEvtHndl, 
                                         bool addTargetEvenIfNotInSS = false)
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = theSP.Width,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(40, 0, 5, 5)
            };

            ComboBox str_cb = new ComboBox
            {
                Name = "str_cb",
                Width = 120,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5, 5, 0, 0)
            };

            str_cb.Items.Add("--select--");
            //this code is used to fix the issue where the structure exists in the structure set, but doesn't populate as the default option in the combo box.
            int index = 0;
            //j is initially 1 because we already added "--select--" to the combo box
            int j = 1;
            foreach (string itr in targetIds)
            {
                str_cb.Items.Add(itr);
                if (itr.ToLower() == item.TargetId.ToLower()) index = j;
                j++;
            }
            if (addTargetEvenIfNotInSS && !targetIds.Any(x => string.Equals(x.ToLower(), item.TargetId.ToLower())))
            {
                str_cb.Items.Add(item.TargetId);
                str_cb.SelectedIndex = str_cb.Items.Count - 1;
            }
            else str_cb.SelectedIndex = index;
            str_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            sp.Children.Add(str_cb);

            TextBox addMargin = new TextBox
            {
                Name = "addMargin_tb",
                Width = 100,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 5, 0, 0),
                Text = Convert.ToString(item.MarginFromTargetInCM)
            };
            sp.Children.Add(addMargin);

            TextBox addThickness = new TextBox
            {
                Name = "addThickness_tb",
                Width = 100,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 5, 0, 0),
                Text = Convert.ToString(item.RingThicknessInCM)
            };
            sp.Children.Add(addThickness);

            TextBox addDose = new TextBox
            {
                Name = "addDose_tb",
                Width = 100,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 5, 0, 0),
                Text = Convert.ToString(item.DoseLevel)
            };
            sp.Children.Add(addDose);

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
        /// Helper method to parse the create rings subtab and return a list of rings that should be created as part of TS generation and manipulation
        /// </summary>
        /// <param name="theSP"></param>
        /// <returns></returns>
        public static (List<TSRingStructureModel>, StringBuilder) ParseCreateRingList(StackPanel theSP)
        {
            StringBuilder sb = new StringBuilder();
            List<TSRingStructureModel> rings = new List<TSRingStructureModel>();
            string target = "";
            double margin = -1000.0;
            double thickness = -1000.0;
            double dose = -1000.0;
            bool headerObj = true;
            int txtBxNum = 1;
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
                            target = (obj1 as ComboBox).SelectedItem.ToString();
                        }
                        //try to parse the margin value as a double
                        else if (obj1.GetType() == typeof(TextBox))
                        {
                            if (!string.IsNullOrWhiteSpace((obj1 as TextBox).Text))
                            {
                                if(txtBxNum == 1) double.TryParse((obj1 as TextBox).Text, out margin);
                                else if(txtBxNum == 2) double.TryParse((obj1 as TextBox).Text, out thickness);
                                else double.TryParse((obj1 as TextBox).Text, out dose);
                            }
                            txtBxNum++;
                        }
                    }
                    if (target == "--select--")
                    {
                        sb.AppendLine("Error! \nTarget not selected for ring! \nSelect an option and try again");
                        return (null, sb);
                    }
                    //margin will not be assigned from the default value (-1000) if the input is empty, a whitespace, or NaN
                    else if (margin <= 0.0 || thickness <= 0.0 || dose <= 0.0)
                    {
                        sb.AppendLine("Error! \nEntered margin, thickness, or dose value(s) is/are invalid for ring! \nEnter new values and try again");
                        return (null, sb);
                    }
                    //only add the current row to the structure sparing list if all the parameters were successful parsed
                    else rings.Add(new TSRingStructureModel(target, margin, thickness, dose));
                    margin = -1000.0;
                    thickness = -1000.0;
                    dose = -1000.0;
                    txtBxNum = 1;
                }
                else headerObj = false;
            }
            return (rings, sb);
        }
    }
}
