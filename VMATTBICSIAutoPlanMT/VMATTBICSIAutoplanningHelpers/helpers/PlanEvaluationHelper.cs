using System;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class PlanEvaluationHelper
    {
        /// <summary>
        /// Simple helper method to compute the difference between the supplied objective/goal and the achieved value in the plan. 
        /// Works for both supplied optimization objectives and plan objectives
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="plan"></param>
        /// <param name="goal"></param>
        /// <param name="theStructure"></param>
        /// <param name="dvh"></param>
        /// <returns></returns>
        public static double GetDifferenceFromGoal<T>(ExternalPlanSetup plan, Tuple<string, OptimizationObjectiveType, double, double, T> goal, Structure theStructure, DVHData dvh)
        {
            //generic type function to accept both optimization constraint and plan objective tuples as arguments
            double diff = 0.0;
            //calculate the dose difference between the actual plan dose and the optimization dose constraint (separate based on constraint type). If the difference is less than 0, truncate the dose difference to 0
            if (goal.Item2 == OptimizationObjectiveType.Upper)
            {
                diff = plan.GetDoseAtVolume(theStructure, goal.Item4, VolumePresentation.Relative, DoseValuePresentation.Absolute).Dose - goal.Item3;
            }
            else if (goal.Item2 == OptimizationObjectiveType.Lower)
            {
                diff = goal.Item3 - plan.GetDoseAtVolume(theStructure, goal.Item4, VolumePresentation.Relative, DoseValuePresentation.Absolute).Dose;
            }
            else if (goal.Item2 == OptimizationObjectiveType.Mean)
            {
                diff = dvh.MeanDose.Dose - goal.Item3;
            }
            if (diff <= 0.0) diff = 0.0;
            return diff;
        }
    }
}
