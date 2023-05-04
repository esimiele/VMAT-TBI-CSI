using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows;
using System.Text;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoplanningHelpers.PlanTemplateClasses;

namespace VMATTBICSIAutoplanningHelpers.UIHelpers
{
    public class TargetsUIHelper
    {
        public List<Tuple<string, double, string>> AddTargetDefaults(CSIAutoPlanTemplate template, StructureSet selectedSS)
        {
            List<Tuple<string, double, string>> tmpList = new List<Tuple<string, double, string>> { Tuple.Create("--select--", 0.0, "--select--") };
            List<Tuple<string, double, string>> targetList = new List<Tuple<string, double, string>> { };
            if (template != null)
            {
                tmpList = new List<Tuple<string, double, string>>(template.GetTargets());
                foreach (Tuple<string, double, string> itr in tmpList) if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == itr.Item1.ToLower()) != null || itr.Item1.ToLower() == "ptv_csi") targetList.Add(itr);
            }
            else targetList = new List<Tuple<string, double, string>>(tmpList);
            return targetList;
        }

        public List<Tuple<string, double, string>> ScanSSAndAddTargets(StructureSet selectedSS)
        {
            List<Structure> tgt = selectedSS.Structures.Where(x => x.Id.ToLower().Contains("ptv") && !x.Id.ToLower().Contains("ts_")).ToList();
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

        public StackPanel GetTargetHeader(double width)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = width;
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(25, 0, 5, 5);

            Label strName = new Label();
            strName.Content = "Target Id";
            strName.HorizontalAlignment = HorizontalAlignment.Center;
            strName.VerticalAlignment = VerticalAlignment.Top;
            strName.Width = 100;
            strName.FontSize = 14;
            strName.Margin = new Thickness(45, 0, 0, 0);

            Label spareType = new Label();
            spareType.Content = "Prescription (cGy)";
            spareType.HorizontalAlignment = HorizontalAlignment.Center;
            spareType.VerticalAlignment = VerticalAlignment.Top;
            spareType.Width = 130;
            spareType.FontSize = 14;
            spareType.Margin = new Thickness(20, 0, 0, 0);

            Label marginLabel = new Label();
            marginLabel.Content = "Plan Id";
            marginLabel.HorizontalAlignment = HorizontalAlignment.Center;
            marginLabel.VerticalAlignment = VerticalAlignment.Top;
            marginLabel.Width = 150;
            marginLabel.FontSize = 14;
            marginLabel.Margin = new Thickness(30, 0, 0, 0);

            sp.Children.Add(strName);
            sp.Children.Add(spareType);
            sp.Children.Add(marginLabel);
            return sp;
        }

        public StackPanel AddTargetVolumes(double width, Tuple<string, double, string> listItem, string clearBtnNamePrefix, int counter, List<string> planIDs, SelectionChangedEventHandler typeChngHndl, RoutedEventHandler clearEvtHndl)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = width;
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(25, 0, 5, 5);

            ComboBox str_cb = new ComboBox();
            str_cb.Name = "str_cb";
            str_cb.Width = 150;
            str_cb.Height = sp.Height - 5;
            str_cb.HorizontalAlignment = HorizontalAlignment.Left;
            str_cb.VerticalAlignment = VerticalAlignment.Top;
            str_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            str_cb.Margin = new Thickness(5, 5, 0, 0);

            str_cb.Items.Add(listItem.Item1);
            str_cb.Items.Add("--Add New--");
            str_cb.SelectedIndex = 0;
            str_cb.SelectionChanged += typeChngHndl;
            sp.Children.Add(str_cb);

            TextBox RxDose_tb = new TextBox();
            RxDose_tb.Name = "RxDose_tb";
            RxDose_tb.Width = 120;
            RxDose_tb.Height = sp.Height - 5;
            RxDose_tb.HorizontalAlignment = HorizontalAlignment.Left;
            RxDose_tb.VerticalAlignment = VerticalAlignment.Top;
            RxDose_tb.TextAlignment = TextAlignment.Center;
            RxDose_tb.VerticalContentAlignment = VerticalAlignment.Center;
            RxDose_tb.Margin = new Thickness(5, 5, 0, 0);
            RxDose_tb.Text = listItem.Item2.ToString();
            sp.Children.Add(RxDose_tb);

            ComboBox planId_cb = new ComboBox();
            planId_cb.Name = "planId_cb";
            planId_cb.Width = 150;
            planId_cb.Height = sp.Height - 5;
            planId_cb.HorizontalAlignment = HorizontalAlignment.Left;
            planId_cb.VerticalAlignment = VerticalAlignment.Top;
            planId_cb.Margin = new Thickness(5, 5, 0, 0);
            planId_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            foreach (string p in planIDs) planId_cb.Items.Add(p);
            planId_cb.Text = listItem.Item3;
            planId_cb.SelectionChanged += typeChngHndl;
            sp.Children.Add(planId_cb);

            Button clearStructBtn = new Button();
            clearStructBtn.Name = clearBtnNamePrefix + counter;
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

        public (List<Tuple<string, double, string>>, StringBuilder) ParseTargets(StackPanel theSP, StructureSet selectedSS)
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
                        //only add the current row to the structure sparing list if all the parameters were successful parsed
                        if (!structure.ToLower().Contains("ctv_spine") && !structure.ToLower().Contains("ctv_brain") && !structure.ToLower().Contains("ptv_spine") && !structure.ToLower().Contains("ptv_brain") && !structure.ToLower().Contains("ptv_csi"))
                        {
                            //if the requested target does not have an id that contains ctv, ptv, brain, spine, or ptv_csi, check to make sure it actually exists in the structure set before proceeding
                            Structure unknownStructure = selectedSS.Structures.FirstOrDefault(x => x.Id == structure);
                            if (unknownStructure == null || unknownStructure.IsEmpty)
                            {
                                sb.AppendLine(String.Format("Error! Structure: {0} not found or is empty! Please remove and try again!", structure));
                                return (listTargets, sb);
                            }
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
