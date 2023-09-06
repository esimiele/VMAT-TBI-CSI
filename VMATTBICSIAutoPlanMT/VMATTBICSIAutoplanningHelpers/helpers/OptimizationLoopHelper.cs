using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMS.TPS.Common.Model.API;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class OptimizationLoopHelper
    {
        /// <summary>
        /// Helper method to query all external beam plans to see if those plans use the supplied structure set and have dose calculated
        /// </summary>
        /// <param name="courses"></param>
        /// <param name="ss"></param>
        /// <returns></returns>
        public static (List<ExternalPlanSetup>, StringBuilder) GetOtherPlansWithSameSSWithCalculatedDose(List<Course> courses, StructureSet ss)
        {
            List<ExternalPlanSetup> otherPlans = new List<ExternalPlanSetup> { };
            StringBuilder sb = new StringBuilder();
            foreach (Course c in courses)
            {
                foreach (ExternalPlanSetup p in c.ExternalPlanSetups)
                {
                    if (p.IsDoseValid && p.StructureSet == ss)
                    {
                        sb.AppendLine($"Course: {c.Id}, Plan: {p.Id}");
                        otherPlans.Add(p);
                    }
                }
            }
            return (otherPlans, sb);
        }

        /// <summary>
        /// Helper method to check the supplied plan hotspot and see if it is greater than the supplied threshold
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public static (bool, double) CheckPlanHotspot(ExternalPlanSetup plan, double threshold)
        {
            double dmax = plan.Dose.DoseMax3D.Dose / plan.TotalDose.Dose;
            if (plan.IsDoseValid && dmax > threshold) return (true, dmax);
            return (false, dmax);
        }

        /// <summary>
        /// Helper method to take the supplied optimization constraints for the heater/cooler structures and scale dose by the ratio of the plan dose to the
        /// plan sum dose (used for sequential optimization)
        /// </summary>
        /// <param name="planTotalDose"></param>
        /// <param name="sumTotalDose"></param>
        /// <param name="originalConstraints"></param>
        /// <returns></returns>
        public static List<Tuple<string, OptimizationObjectiveType, double, double, int>> ScaleHeaterCoolerOptConstraints(double planTotalDose, 
                                                                                                                          double sumTotalDose, 
                                                                                                                          List<Tuple<string, OptimizationObjectiveType, double, double, int>> originalConstraints)
        {
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> updatedOpt = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
            foreach (Tuple<string, OptimizationObjectiveType, double, double, int> itr in originalConstraints)
            {
                updatedOpt.Add(Tuple.Create(itr.Item1, itr.Item2, itr.Item3 * planTotalDose / sumTotalDose, itr.Item4, itr.Item5));
            }
            return updatedOpt;
        }

        /// <summary>
        /// Helper method to take the supplied optimization constraints and increase the priority of the existing ts cooler constraints
        /// </summary>
        /// <param name="updatedOpt"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Helper method to get the normalization volume for the supplied plan Id
        /// </summary>
        /// <param name="planId"></param>
        /// <param name="normalizationVolumes"></param>
        /// <returns></returns>
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
