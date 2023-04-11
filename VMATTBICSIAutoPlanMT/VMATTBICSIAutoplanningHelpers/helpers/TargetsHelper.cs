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

        //plan Rx dose
        public double GetHighestRxForPlan(List<Tuple<string, string, int, DoseValue, double>> prescriptions, string plandId)
        {
            double dose = 0.0;
            List<Tuple<string, string, int, DoseValue, double>> tmpList = prescriptions.OrderBy(x => x.Item5).ToList();
            if (tmpList.Any(x => string.Equals(x.Item1.ToLower(), plandId.ToLower())))
            {
                Tuple<string, string, int, DoseValue, double> rx = prescriptions.Last(x => string.Equals(x.Item1.ToLower(), plandId.ToLower()));
                dose = rx.Item3 * rx.Item4.Dose;
            }
            return dose;
        }

        //target id, target prescription dose
        public (string, double) GetAppropriateTargetForRing(List<Tuple<string, string, int, DoseValue, double>> prescriptions, double ringDose)
        {
            string targetId = "";
            double targetRx = 0.0;
            List<Tuple<string, double>> sortedTargets = new TargetsHelper().GetSortedTargetsByRxDose(prescriptions);
            if (sortedTargets.Any(x => x.Item2 > ringDose))
            {
                Tuple<string, double> tmp = sortedTargets.First(y => y.Item2 > ringDose);
                targetId = tmp.Item1;
                targetRx = tmp.Item2;
            }
            return (targetId, targetRx);
        }

        //targetId, cumulative Rx dose
        public List<Tuple<string, double>> GetSortedTargetsByRxDose(List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            List<Tuple<string, double>> sortedTargets = new List<Tuple<string, double>> { };
            if (!prescriptions.Any()) return sortedTargets;
            //sort by cumulative dose to targets
            List<Tuple<string, string, int, DoseValue, double>> tmpList = prescriptions.OrderBy(x => x.Item5).ToList();

            foreach (Tuple<string, string, int, DoseValue, double> itr in tmpList)
            {
                sortedTargets.Add(Tuple.Create(itr.Item2, itr.Item5));
            }
            return sortedTargets;
        }

        //planId, targetId
        public List<Tuple<string, string>> GetPlanTargetList(List<Tuple<string, double, string>> targetList)
        {
            //for this list, item1 is the target, item 2 is the cumulated dose (cGy), and item 3 is the plan
            List<Tuple<string, string>> plansTargets = new List<Tuple<string, string>> { };
            if (!targetList.Any()) return plansTargets;
            //sort by cumulative dose to targets
            List<Tuple<string, double, string>> tmpList = targetList.OrderBy(x => x.Item2).ToList();
            string tmpTarget = tmpList.First().Item1;
            string tmpPlan = tmpList.First().Item3;

            foreach (Tuple<string, double, string> itr in tmpList)
            {
                //check if this is the start of a new plan, if so, the the previous target was the highest dose target in the previous plan
                if (!string.Equals(itr.Item3, tmpPlan))
                {
                    plansTargets.Add(Tuple.Create<string, string>(tmpPlan, tmpTarget));
                    tmpPlan = itr.Item3;
                }
                tmpTarget = itr.Item1;
            }
            plansTargets.Add(Tuple.Create<string, string>(tmpPlan, tmpTarget));
            return plansTargets;
        }

        //list of target IDs
        public List<string> GetAllTargets(List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            List<string> targets = new List<string> { };
            foreach (Tuple<string, string, int, DoseValue, double> itr in prescriptions)
            {
                targets.Add(itr.Item2);
            }
            return targets;
        }

        //target structure
        public Structure GetTargetForPlan(StructureSet ss, string targetId, bool useFlash, PlanType type)
        {
            Structure target = null;
            if (string.IsNullOrEmpty(targetId))
            {
                //case where no targetId is supplied --> use default target for all plans
                if(type == PlanType.VMAT_TBI)
                {
                    //flash should only be present for vmat tbi plans
                    if (useFlash) target = ss.Structures.FirstOrDefault(x => x.Id.ToLower() == "ts_ptv_flash");
                    else target = ss.Structures.FirstOrDefault(x => x.Id.ToLower() == "ts_ptv_vmat");
                }
                else target = ss.Structures.FirstOrDefault(x => x.Id.ToLower() == "ts_ptv_csi");
            }
            else
            {
                target = ss.Structures.FirstOrDefault(x => x.Id.ToLower() == targetId.ToLower());
            }
            return target;
        }
    }
}
