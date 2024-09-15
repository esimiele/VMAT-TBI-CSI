using System;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Interfaces;
using VMATTBICSIAutoPlanningHelpers.Models;
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
        /// <param name="plan"></param>
        /// <param name="goal"></param>
        /// <param name="theStructure"></param>
        /// <param name="dvh"></param>
        /// <returns></returns>
        public static double GetDifferenceFromGoal(ExternalPlanSetup plan, IPlanConstraint goal, Structure theStructure, DVHData dvh)
        {
            //generic type function to accept both optimization constraint and plan objective tuples as arguments
            double diff = 0.0;
            //calculate the dose difference between the actual plan dose and the optimization dose constraint (separate based on constraint type). If the difference is less than 0, truncate the dose difference to 0
            if (goal.ConstraintType == OptimizationObjectiveType.Upper)
            {
                diff = plan.GetDoseAtVolume(theStructure, goal.QueryVolume, VolumePresentation.Relative, goal.QueryDoseUnits == Units.Percent ? DoseValuePresentation.Relative : DoseValuePresentation.Absolute).Dose - goal.QueryDose;
            }
            else if (goal.ConstraintType == OptimizationObjectiveType.Lower)
            {
                diff = goal.QueryDose - plan.GetDoseAtVolume(theStructure, goal.QueryVolume, VolumePresentation.Relative, goal.QueryDoseUnits == Units.Percent ? DoseValuePresentation.Relative : DoseValuePresentation.Absolute).Dose;
            }
            else if (goal.ConstraintType == OptimizationObjectiveType.Mean)
            {
                diff = dvh.MeanDose.Dose - goal.QueryDose;
            }
            if (diff <= 0.0) diff = 0.0;
            return diff;
        }
    }
}
