using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Models;
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
        public static (IEnumerable<ExternalPlanSetup>, StringBuilder) GetOtherPlansWithSameSSWithCalculatedDose(IEnumerable<Course> courses, StructureSet ss)
        {
            IEnumerable<ExternalPlanSetup> otherPlans = courses.SelectMany(x => x.ExternalPlanSetups).Where(x => x.IsDoseValid && x.Beams.Any() && string.Equals(x.StructureSet.UID,ss.UID));
            StringBuilder sb = new StringBuilder();

            foreach (ExternalPlanSetup p in otherPlans)
            {
                sb.AppendLine($"Course: {p.Course.Id}, Plan: {p.Id}");
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
            if (dmax > threshold) return (true, dmax);
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
        public static List<OptimizationConstraintModel> ScaleHeaterCoolerOptConstraints(double planTotalDose, 
                                                                                   double sumTotalDose, 
                                                                                   List<OptimizationConstraintModel> originalConstraints)
        {
            List<OptimizationConstraintModel> updatedOpt = new List<OptimizationConstraintModel> { };
            foreach (OptimizationConstraintModel itr in originalConstraints)
            {
                double dose = itr.QueryDose;
                if (itr.QueryDoseUnits == Units.Percent) dose *= sumTotalDose / 100;
                updatedOpt.Add(new OptimizationConstraintModel(itr.StructureId, itr.ConstraintType, dose * planTotalDose / sumTotalDose, Units.cGy, itr.QueryVolume, itr.Priority));
            }
            return updatedOpt;
        }

        /// <summary>
        /// Helper method to take the supplied optimization constraints and increase the priority of the existing ts cooler constraints
        /// </summary>
        /// <param name="updatedOpt"></param>
        /// <returns></returns>
        public static List<OptimizationConstraintModel> IncreaseOptConstraintPrioritiesForFinalOpt(List<OptimizationConstraintModel> updatedOpt)
        {
            //go through the current list of optimization objects and add all of them to finalObj vector. ADD COMMENTS!
            List<OptimizationConstraintModel> finalObj = new List<OptimizationConstraintModel> { };
            double maxPriority = (double)updatedOpt.Max(x => x.Priority);
            foreach (OptimizationConstraintModel itr in updatedOpt)
            {
                //get maximum priority and assign it to the cooler structure to really push the hotspot down. Also lower dose objective
                if (itr.StructureId.ToLower().Contains("ts_cooler"))
                {
                    finalObj.Add(new OptimizationConstraintModel(itr.StructureId, itr.ConstraintType, 0.98 * itr.QueryDose, itr.QueryDoseUnits, itr.QueryVolume, Math.Max(itr.Priority, (int)(0.9 * maxPriority))));
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
        public static string GetNormaliztionVolumeIdForPlan(string planId, Dictionary<string, string> normalizationVolumes)
        {
            string normStructureId = "";
            if (normalizationVolumes.Any())
            {
                if (normalizationVolumes.Any(x => string.Equals(x.Key, planId)))
                {
                    normStructureId = normalizationVolumes.FirstOrDefault(x => string.Equals(x.Key, planId)).Value;
                }
            }
            return normStructureId;
        }
    }
}
