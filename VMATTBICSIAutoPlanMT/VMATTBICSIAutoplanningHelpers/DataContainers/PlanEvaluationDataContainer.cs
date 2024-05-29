using System;
using System.Collections.Generic;
using System.Linq;
using VMATTBICSIAutoPlanningHelpers.UtilityClasses;
using VMS.TPS.Common.Model.API;

namespace VMATTBICSIAutoPlanningHelpers.DataContainers
{
    public class PlanEvaluationDataContainer
    {
        //difference between current dose values for each structure in the optimization list and the optimization constraint(s) for that structure
        public List<Tuple<Structure, DVHData, double, double, double, int>> PlanDifferenceFromOptConstraints { get; set; } = new List<Tuple<Structure, DVHData, double, double, double, int>> { };
        //same for plan objectives
        public List<Tuple<Structure, DVHData, double, double>> PlanDifferenceFromPlanObjectives { get; set; } = new List<Tuple<Structure, DVHData, double, double>> { };
        //vector to hold the updated optimization objectives (to be assigned to the plan)
        public List<OptimizationConstraint> UpdatedOptimizationObjectives { get; set; } = new List<OptimizationConstraint> { };
        //the total cost sum(dose diff^2 * priority) for all structures in the optimization objective vector list
        public double TotalOptimizationCostOptConstraints { get; set; } = double.NaN;
        //same for plan objective vector
        public double TotalOptimizationCostPlanObjectives { get; set; } = double.NaN;
        //did plan meet all plan objectives?
        public bool AllPlanObjectivesMet { get; set; } = false;
        //bool to indicate plan evaluation failed or the user killed the optimization loop while evaluation was going on
        public bool OptimizationKilledByUser { get; set; } = false;
    }
}
