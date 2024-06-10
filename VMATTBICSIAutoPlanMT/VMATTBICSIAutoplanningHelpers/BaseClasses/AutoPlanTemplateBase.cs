using System;
using System.Collections.Generic;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Models;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.BaseClasses
{
    public abstract class AutoPlanTemplateBase
    {
        //this is only here for the display name data binding. All other references to the template name use the explicit get method
        public string TemplateName { get; set; } = string.Empty;
        public List<PlanTargetsModel> PlanTargets { get; set; } = new List<PlanTargetsModel>();
        public List<RequestedTSStructureModel> CreateTSStructures { get; set; } = new List<RequestedTSStructureModel> { };
        public List<RequestedTSManipulationModel> TSManipulations { get; set; } = new List<RequestedTSManipulationModel> { };

        public List<PlanObjectiveModel> PlanObjectives { get; set; } = new List<PlanObjectiveModel> { };
        public List<RequestedPlanMetricModel> RequestedPlanMetrics { get; set; } = new List<RequestedPlanMetricModel> { };
        public List<RequestedOptimizationTSStructureModel> RequestedOptimizationTSStructures { get; set; } = new List<RequestedOptimizationTSStructureModel> { };

        //requested items to be printed after each successful iteration of the optimization loop
        //requested cooler and heater structures to be added after each iteration of the optimization loop (IF CERTAIN CRITERIA ARE MET!)
        //structure id, low dose (%), high dose (%), Volume (%), priority, List of criteria that must be met for the requested TS structure to be added (all constraints are AND)
        //NOTE! THE LOW DOSE AND HIGH DOSE VALUES ARE USED FOR GENERATING HEATER STRUCTURES. 
        //FOR COOLER STRUCTURES, THE LOW DOSE VALUE IS USED TO CONVERT AN ISODOSE LEVEL TO STRUCTURE WHEREAS THE HIGH DOSE VALUE IS USED TO GENERATE THE OPTIMIZATION CONSTRAINT
        //protected List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> requestedOptTSStructures = new List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> { };
    }
}
