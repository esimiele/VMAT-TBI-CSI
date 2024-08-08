using System.Collections.Generic;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpers.DataContainers
{
    public class PlanEvaluationDataContainer
    {
        //difference between current dose values for each structure in the optimization list and the optimization constraint(s) for that structure
        public List<PlanOptConstraintsDeviationModel> PlanDifferenceFromOptConstraints { get; set; } = new List<PlanOptConstraintsDeviationModel> { };
        //same for plan objectives
        public List<PlanObjectivesDeviationModel> PlanDifferenceFromPlanObjectives { get; set; } = new List<PlanObjectivesDeviationModel> { };
        //vector to hold the updated optimization objectives (to be assigned to the plan)
        public List<OptimizationConstraintModel> UpdatedOptimizationObjectives { get; set; } = new List<OptimizationConstraintModel> { };
        //the total cost sum(dose diff^2 * priority) for all structures in the optimization objective vector list
        public double TotalOptimizationCostOptConstraints { get; set; } = double.NaN;
        //did plan meet all plan objectives?
        public bool AllPlanObjectivesMet { get; set; } = false;
        //bool to indicate plan evaluation failed or the user killed the optimization loop while evaluation was going on
        public bool OptimizationKilledByUser { get; set; } = false;
    }
}
