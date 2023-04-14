using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;

namespace VMATTBICSIAutoplanningHelpers.UIHelpers
{
    public class RingUIHelper
    {
        public StackPanel GetRingHeader(double theWidth)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theWidth;
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(30, 0, 5, 5);

            Label strName = new Label();
            strName.Content = "Target Id";
            strName.HorizontalAlignment = HorizontalAlignment.Center;
            strName.VerticalAlignment = VerticalAlignment.Top;
            strName.Width = 100;
            strName.FontSize = 14;
            strName.Margin = new Thickness(40, 0, 0, 0);

            Label spareType = new Label();
            spareType.Content = "Margin (cm)";
            spareType.HorizontalAlignment = HorizontalAlignment.Center;
            spareType.VerticalAlignment = VerticalAlignment.Top;
            spareType.Width = 90;
            spareType.FontSize = 14;
            spareType.Margin = new Thickness(5, 0, 0, 0);

            Label volLabel = new Label();
            volLabel.Content = "Thickness (cm)";
            volLabel.HorizontalAlignment = HorizontalAlignment.Center;
            volLabel.VerticalAlignment = VerticalAlignment.Top;
            volLabel.Width = 100;
            volLabel.FontSize = 14;
            volLabel.Margin = new Thickness(10, 0, 0, 0);

            Label doseLabel = new Label();
            doseLabel.Content = "Dose (cGy)";
            doseLabel.HorizontalAlignment = HorizontalAlignment.Center;
            doseLabel.VerticalAlignment = VerticalAlignment.Top;
            doseLabel.Width = 80;
            doseLabel.FontSize = 14;
            doseLabel.Margin = new Thickness(15, 0, 0, 0);

            sp.Children.Add(strName);
            sp.Children.Add(spareType);
            sp.Children.Add(volLabel);
            sp.Children.Add(doseLabel);
            return sp;
        }

        public StackPanel AddRing(StackPanel theSP, List<string> targetIds, Tuple<string, double, double, double> listItem, string clearBtnPrefix, int clearSpareBtnCounter, RoutedEventHandler clearEvtHndl, bool addTargetEvenIfNotInSS = false)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(40, 0, 5, 5);

            ComboBox str_cb = new ComboBox();
            str_cb.Name = "str_cb";
            str_cb.Width = 120;
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
            foreach (string itr in targetIds)
            {
                str_cb.Items.Add(itr);
                if (itr.ToLower() == listItem.Item1.ToLower()) index = j;
                j++;
            }
            if (addTargetEvenIfNotInSS && !targetIds.Any(x => string.Equals(x.ToLower(), listItem.Item1.ToLower())))
            {
                str_cb.Items.Add(listItem.Item1);
                str_cb.SelectedIndex = str_cb.Items.Count - 1;
            }
            else str_cb.SelectedIndex = index;
            str_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            sp.Children.Add(str_cb);

            TextBox addMargin = new TextBox();
            addMargin.Name = "addMargin_tb";
            addMargin.Width = 100;
            addMargin.Height = sp.Height - 5;
            addMargin.HorizontalAlignment = HorizontalAlignment.Left;
            addMargin.VerticalAlignment = VerticalAlignment.Top;
            addMargin.TextAlignment = TextAlignment.Center;
            addMargin.VerticalContentAlignment = VerticalAlignment.Center;
            addMargin.Margin = new Thickness(5, 5, 0, 0);
            addMargin.Text = Convert.ToString(listItem.Item2);
            sp.Children.Add(addMargin);

            TextBox addThickness = new TextBox();
            addThickness.Name = "addThickness_tb";
            addThickness.Width = 100;
            addThickness.Height = sp.Height - 5;
            addThickness.HorizontalAlignment = HorizontalAlignment.Left;
            addThickness.VerticalAlignment = VerticalAlignment.Top;
            addThickness.TextAlignment = TextAlignment.Center;
            addThickness.VerticalContentAlignment = VerticalAlignment.Center;
            addThickness.Margin = new Thickness(5, 5, 0, 0);
            addThickness.Text = Convert.ToString(listItem.Item3);
            sp.Children.Add(addThickness);

            TextBox addDose = new TextBox();
            addDose.Name = "addDose_tb";
            addDose.Width = 100;
            addDose.Height = sp.Height - 5;
            addDose.HorizontalAlignment = HorizontalAlignment.Left;
            addDose.VerticalAlignment = VerticalAlignment.Top;
            addDose.TextAlignment = TextAlignment.Center;
            addDose.VerticalContentAlignment = VerticalAlignment.Center;
            addDose.Margin = new Thickness(5, 5, 0, 0);
            addDose.Text = Convert.ToString(listItem.Item4);
            sp.Children.Add(addDose);

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

        public (List<Tuple<string, double, double, double>>, StringBuilder) ParseCreateRingList(StackPanel theSP)
        {
            StringBuilder sb = new StringBuilder();
            List<Tuple<string, double, double, double>> CreateRingList = new List<Tuple<string, double, double, double>> { };
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
                        return (new List<Tuple<string, double, double, double>> { }, sb);
                    }
                    //margin will not be assigned from the default value (-1000) if the input is empty, a whitespace, or NaN
                    else if (margin <= 0.0 || thickness <= 0.0 || dose <= 0.0)
                    {
                        sb.AppendLine("Error! \nEntered margin, thickness, or dose value(s) is/are invalid for ring! \nEnter new values and try again");
                        return (new List<Tuple<string, double, double, double>> { }, sb);
                    }
                    //only add the current row to the structure sparing list if all the parameters were successful parsed
                    else CreateRingList.Add(Tuple.Create(target, margin, thickness, dose));
                    margin = -1000.0;
                    thickness = -1000.0;
                    dose = -1000.0;
                    txtBxNum = 1;
                }
                else headerObj = false;
            }
            return (CreateRingList, sb);
        }
    }
}
