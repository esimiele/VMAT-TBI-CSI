using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows;
using System.Text;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class TargetsUIHelper
    {
        /// <summary>
        /// Helper method to add the default targets in the supplied auto plan template to a list that can be used to populate the UI
        /// </summary>
        /// <param name="template"></param>
        /// <returns></returns>
        public static List<Tuple<string, double, string>> AddTargetDefaults(AutoPlanTemplateBase template)
        {
            List<Tuple<string, double, string>> targetList = new List<Tuple<string, double, string>> { };
            if (template != null)
            {
                foreach (Tuple<string, double, string> itr in template.GetTargets()) targetList.Add(itr);
            }
            else targetList = new List<Tuple<string, double, string>> { Tuple.Create("--select--", 0.0, "--select--") };
            return targetList;
        }

        /// <summary>
        /// Helper method to search through the structure set and try to retrieve any potential targets
        /// </summary>
        /// <param name="selectedSS"></param>
        /// <returns></returns>
        public static List<Tuple<string, double, string>> ScanSSAndAddTargets(StructureSet selectedSS)
        {
            List<Structure> tgt = selectedSS.Structures.Where(x => x.Id.ToLower().Contains("ptv") && !x.Id.ToLower().Contains("ts_") && x.ApprovalHistory.First().Equals(StructureApprovalStatus.Approved)).ToList();
            if (!tgt.Any()) return new List<Tuple<string, double, string>> { };
            List<Tuple<string, double, string>> targetList = new List<Tuple<string, double, string>> { };
            double tgtRx;
            foreach (Structure itr in tgt)
            {
                if (!double.TryParse(itr.Id.Substring(itr.Id.IndexOf("_") + 1, itr.Id.Length - (itr.Id.IndexOf("_") + 1)), out tgtRx)) tgtRx = 0.1;
                targetList.Add(new Tuple<string, double, string>(itr.Id, tgtRx, ""));
            }
            return targetList;
        }

        /// <summary>
        /// Helper method to add the header information to the Set Targets tab
        /// </summary>
        /// <param name="width"></param>
        /// <returns></returns>
        public static StackPanel GetTargetHeader(double width)
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = width,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(25, 0, 5, 5)
            };

            Label strName = new Label
            {
                Content = "Target Id",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 100,
                FontSize = 14,
                Margin = new Thickness(50, 0, 0, 0)
            };

            Label spareType = new Label
            {
                Content = "Total Rx (cGy)",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 130,
                FontSize = 14,
                Margin = new Thickness(20, 0, 0, 0)
            };

            Label marginLabel = new Label
            {
                Content = "Plan Id",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 150,
                FontSize = 14,
                Margin = new Thickness(25, 0, 0, 0)
            };

            sp.Children.Add(strName);
            sp.Children.Add(spareType);
            sp.Children.Add(marginLabel);
            return sp;
        }

        /// <summary>
        /// Helper method to add a target item to the Set Targets tab
        /// </summary>
        /// <param name="width"></param>
        /// <param name="listItem"></param>
        /// <param name="clearBtnNamePrefix"></param>
        /// <param name="counter"></param>
        /// <param name="planIDs"></param>
        /// <param name="typeChngHndl"></param>
        /// <param name="clearEvtHndl"></param>
        /// <param name="addTargetEvenIfNotInSS"></param>
        /// <returns></returns>
        public static StackPanel AddTargetVolumes(double width, 
                                                  Tuple<string, double, string> listItem, 
                                                  string clearBtnNamePrefix, 
                                                  int counter, 
                                                  List<string> planIDs, 
                                                  SelectionChangedEventHandler typeChngHndl, 
                                                  RoutedEventHandler clearEvtHndl, 
                                                  bool addTargetEvenIfNotInSS = false)
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = width,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(25, 0, 5, 5)
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

            str_cb.Items.Add(listItem.Item1);
            str_cb.Items.Add("--Add New--");
            str_cb.SelectedIndex = 0;
            str_cb.SelectionChanged += typeChngHndl;
            sp.Children.Add(str_cb);

            TextBox RxDose_tb = new TextBox
            {
                Name = "RxDose_tb",
                Width = 120,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 5, 0, 0),
                Text = listItem.Item2.ToString()
            };
            sp.Children.Add(RxDose_tb);

            ComboBox planId_cb = new ComboBox
            {
                Name = "planId_cb",
                Width = 150,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(5, 5, 0, 0),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            foreach (string p in planIDs) planId_cb.Items.Add(p);
            planId_cb.Text = listItem.Item3;
            planId_cb.SelectionChanged += typeChngHndl;
            sp.Children.Add(planId_cb);

            Button clearStructBtn = new Button
            {
                Name = clearBtnNamePrefix + counter,
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
        /// Helper method to parse the Set targets tab and return a list of targets for the plan(s)
        /// </summary>
        /// <param name="theSP"></param>
        /// <returns></returns>
        public static (List<Tuple<string, double, string>>, StringBuilder) ParseTargets(StackPanel theSP)
        {
            StringBuilder sb = new StringBuilder();
            List<Tuple<string, double, string>> listTargets = new List<Tuple<string, double, string>> { };
            string structure = "";
            double tgtRx = -1000.0;
            string planID = "";
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
                            else planID = (obj1 as ComboBox).SelectedItem.ToString();
                        }
                        //try to parse the target Rx as a double value
                        else if (obj1.GetType() == typeof(TextBox))
                        {
                            if (!string.IsNullOrWhiteSpace((obj1 as TextBox).Text)) double.TryParse((obj1 as TextBox).Text, out tgtRx);
                        }
                    }
                    if (structure == "--select--" || planID == "--select--")
                    {
                        sb.AppendLine("Error! \nStructure or plan not selected! \nSelect an option and try again");
                        return (listTargets, sb);
                    }
                    //margin will not be assigned from the default value (-1000) if the input is empty, a whitespace, or NaN
                    else if (tgtRx == -1000.0)
                    {
                        sb.AppendLine("Error! \nEntered Rx value is invalid! \nEnter a new Rx and try again");
                        return (listTargets, sb);
                    }
                    else
                    {
                        if (planID.Length > 13)
                        {
                            //MessageBox.Show(String.Format("Error! Plan Id '{0}' is greater than maximum length allowed by Eclipse (13)! Exiting!", planID));
                            planID = planID.Substring(0, 13);
                        }
                        listTargets.Add(Tuple.Create(structure, tgtRx, planID));
                    }
                    firstCombo = true;
                    tgtRx = -1000.0;
                }
                else headerObj = false;
            }

            //sort the targets based on requested plan Id (alphabetically)
            listTargets.Sort(delegate (Tuple<string, double, string> x, Tuple<string, double, string> y) { return x.Item3.CompareTo(y.Item3); });
            return (listTargets, sb);
        }
    }
}
