using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using VMATAutoPlanMT.VMAT_CSI;
using VMS.TPS.Common.Model.API;
using System.Windows.Navigation;

namespace VMATAutoPlanMT.helpers
{
    internal class TargetsUIHelper
    {

        public List<Tuple<string, double, string>> AddTargetDefaults(autoPlanTemplate template, StructureSet selectedSS)
        {
            List<Tuple<string, double, string>> tmpList = new List<Tuple<string, double, string>> { Tuple.Create("--select--", 0.0, "--select--") };
            List<Tuple<string, double, string>> targetList = new List<Tuple<string, double, string>> { };
            if (template != null)
            {
                tmpList = new List<Tuple<string, double, string>>(template.targets);
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

        public int clearTarget(StackPanel theSP, Button btn)
        {
            //same deal as the clear sparing structure button (clearStructBtn_click)
            int i = 0;
            int k = 0;
            foreach (object obj in theSP.Children)
            {
                foreach (object obj1 in ((StackPanel)obj).Children)
                {
                    if (obj1.Equals(btn)) k = i;
                }
                if (k > 0) break;
                i++;
            }
            return k;
        }

        public StackPanel get_target_header(double width)
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

        public StackPanel add_target_volumes(double width, Tuple<string, double, string> listItem, string clearBtnNamePrefix, int counter, List<string> planIDs, SelectionChangedEventHandler typeChngHndl, RoutedEventHandler clearEvtHndl)
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
            //string[] types = new string[] { itr.Item3, "--Add New--" };
            foreach (string p in planIDs) planId_cb.Items.Add(p);
            planId_cb.Text = listItem.Item3;
            planId_cb.SelectionChanged += typeChngHndl;
            //planId_cb.SelectionChanged += new SelectionChangedEventHandler(type_cb_change);
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
    }
}
