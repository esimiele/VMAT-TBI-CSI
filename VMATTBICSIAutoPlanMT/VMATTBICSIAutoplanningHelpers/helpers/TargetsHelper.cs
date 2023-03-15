using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoplanningHelpers.Helpers
{
    public class TargetsHelper
    {
        //planId, targetId
        public List<Tuple<string, string>> GetPlanTargetList(List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            List<Tuple<string, string>> plansTargets = new List<Tuple<string, string>> { };
            if (!prescriptions.Any()) return plansTargets;
            //sort by cumulative dose to targets
            List<Tuple<string, string, int, DoseValue, double>> tmpList = prescriptions.OrderBy(x => x.Item5).ToList();
            string tmpPlan = tmpList.First().Item1;
            string tmpTarget = tmpList.First().Item2;

            foreach (Tuple<string, string, int, DoseValue, double> itr in tmpList)
            {
                //check if this is the start of a new plan, if so, the the previous target was the highest dose target in the previous plan
                if (!string.Equals(itr.Item1, tmpPlan))
                {
                    plansTargets.Add(Tuple.Create<string, string>(tmpPlan, tmpTarget));
                    tmpPlan = itr.Item1;
                }
                tmpTarget = itr.Item2;
            }
            plansTargets.Add(Tuple.Create<string, string>(tmpPlan, tmpTarget));
            return plansTargets;
        }

        public List<string> GetAllTargets(List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            List<string> targets = new List<string> { };
            foreach (Tuple<string, string, int, DoseValue, double> itr in prescriptions)
            {
                targets.Add(itr.Item2);
            }
            return targets;
        }

        public Structure GetTargetForPlan(StructureSet ss, string targetId, bool useFlash)
        {
            Structure target = null;
            if (string.IsNullOrEmpty(targetId))
            {
                //if (!useFlash) target = plan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToLower() == "ts_ptv_vmat");
                if (useFlash) target = ss.Structures.FirstOrDefault(x => x.Id.ToLower() == "ts_ptv_flash");
                else target = ss.Structures.FirstOrDefault(x => x.Id.ToLower() == "ts_ptv_csi");
            }
            else
            {
                target = ss.Structures.FirstOrDefault(x => x.Id.ToLower() == targetId);
            }
            return target;
        }
    }
}
