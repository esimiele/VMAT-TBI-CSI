using System;
using System.Collections.Generic;
using VMS.TPS.Common.Model.API;
using OptimizationObjectiveType = VMATTBICSIAutoPlanningHelpers.Enums.OptimizationObjectiveType;

namespace VMATTBICSIAutoPlanningHelpers.Structs
{
    /// <summary>
    /// Data struct to hold the results of the plan evaluation following an optimization
    /// </summary>
    public struct EvalPlanStruct
    {
        //difference between current dose values for each structure in the optimization list and the optimization constraint(s) for that structure
        public List<Tuple<Structure, DVHData, double, double, double, int>> diffPlanOpt;
        //same for plan objectives
        public List<Tuple<Structure, DVHData, double, double>> diffPlanObj;
        //vector to hold the updated optimization objectives (to be assigned to the plan)
        public List<Tuple<string, OptimizationObjectiveType, double, double, int>> updatedObj;
        //the total cost sum(dose diff^2 * priority) for all structures in the optimization objective vector list
        public double totalCostPlanOpt;
        //same for plan objective vector
        public double totalCostPlanObj;
        //did plan meet all plan objectives?
        public bool allObjectivesMet;
        //bool to indicate plan evaluation failed or the user killed the optimization loop while evaluation was going on
        public bool wasKilled;
        //simple constructure method to initialize the data members. Need to have this here because you can't initialize data members directly within a data structure
        public void Construct()
        {
            //vector to hold the results from the optimization for a particular OPTIMIZATION objective
            //structure, dvh data, current dose obj, dose diff^2, cost, current priority
            diffPlanOpt = new List<Tuple<Structure, DVHData, double, double, double, int>> { };
            //vector to hold the results from the optimization for a particular PLAN objective
            diffPlanObj = new List<Tuple<Structure, DVHData, double, double>> { };
            //vector to hold the updated optimization objectives (following adjustment in the evaluateAndUpdatePlan method)
            updatedObj = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
            allObjectivesMet = false;
            wasKilled = false;
        }
    }
}
