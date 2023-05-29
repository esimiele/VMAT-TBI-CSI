using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class PlanEvaluationHelper
    {
        public static double GetDifferenceFromGoal<T>(ExternalPlanSetup plan, Tuple<string, OptimizationObjectiveType, double, double, T> goal, Structure s, DVHData dvh)
        {
            //generic type function to accept both optimization constraint and plan objective tuples as arguments
            double diff = 0.0;
            //calculate the dose difference between the actual plan dose and the optimization dose constraint (separate based on constraint type). If the difference is less than 0, truncate the dose difference to 0
            if (goal.Item2 == OptimizationObjectiveType.Upper)
            {
                diff = plan.GetDoseAtVolume(s, goal.Item4, VolumePresentation.Relative, DoseValuePresentation.Absolute).Dose - goal.Item3;
            }
            else if (goal.Item2 == OptimizationObjectiveType.Lower)
            {
                diff = goal.Item3 - plan.GetDoseAtVolume(s, goal.Item4, VolumePresentation.Relative, DoseValuePresentation.Absolute).Dose;
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
