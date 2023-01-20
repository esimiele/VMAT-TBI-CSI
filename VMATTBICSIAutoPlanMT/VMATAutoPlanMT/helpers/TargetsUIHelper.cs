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
            //clear entire list if there are only two entries (header + 1 real entry)
            //if (theSP.Children.Count < 3) clear_targets_list(btn);
            //else theSP.Children.RemoveAt(k);
        }
    }
}
