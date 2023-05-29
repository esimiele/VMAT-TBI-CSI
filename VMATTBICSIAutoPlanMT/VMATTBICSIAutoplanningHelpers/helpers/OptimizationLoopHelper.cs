using System;
using System.Collections.Generic;
using System.Linq;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMS.TPS.Common.Model.API;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class OptimizationLoopHelper
    {
        public static (bool, double) CheckPlanHotspot(ExternalPlanSetup plan, double threshold)
        {
            double dmax = plan.Dose.DoseMax3D.Dose / plan.TotalDose.Dose;
            if (plan.IsDoseValid && dmax > threshold) return (true, dmax);
            return (false, dmax);
        }

        public static List<Tuple<string, OptimizationObjectiveType, double, double, int>> ScaleHeaterCoolerOptConstraints(double planTotalDose, double sumTotalDose, List<Tuple<string, OptimizationObjectiveType, double, double, int>> originalConstraints)
        {
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> updatedOpt = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
            foreach (Tuple<string, OptimizationObjectiveType, double, double, int> itr in originalConstraints)
            {
                updatedOpt.Add(Tuple.Create(itr.Item1, itr.Item2, itr.Item3 * planTotalDose / sumTotalDose, itr.Item4, itr.Item5));
            }
            return updatedOpt;
        }

        public static List<Tuple<string, OptimizationObjectiveType, double, double, int>> IncreaseOptConstraintPrioritiesForFinalOpt(List<Tuple<string, OptimizationObjectiveType, double, double, int>> updatedOpt)
        {
            //go through the current list of optimization objects and add all of them to finalObj vector. ADD COMMENTS!
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> finalObj = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
            double maxPriority = (double)updatedOpt.Max(x => x.Item5);
            foreach (Tuple<string, OptimizationObjectiveType, double, double, int> itr in updatedOpt)
            {
                //get maximum priority and assign it to the cooler structure to really push the hotspot down. Also lower dose objective
                if (itr.Item1.ToLower().Contains("ts_cooler"))
                {
                    finalObj.Add(new Tuple<string, OptimizationObjectiveType, double, double, int>(itr.Item1, itr.Item2, 0.98 * itr.Item3, itr.Item4, Math.Max(itr.Item5, (int)(0.9 * maxPriority))));
                }
                else finalObj.Add(itr);
            }
            return finalObj;
        }

        public static string GetNormaliztionVolumeIdForPlan(string planId, List<Tuple<string,string>> normalizationVolumes)
        {
            string normStructureId = "";
            if (normalizationVolumes.Any())
            {
                if (normalizationVolumes.Any(x => string.Equals(x.Item1, planId)))
                {
                    normStructureId = normalizationVolumes.FirstOrDefault(x => string.Equals(x.Item1, planId)).Item2;
                }
            }
            return normStructureId;
        }
    }
}
