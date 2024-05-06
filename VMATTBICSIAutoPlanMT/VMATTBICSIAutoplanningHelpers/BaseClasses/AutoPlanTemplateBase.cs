using System;
using System.Collections.Generic;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.UtilityClasses;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.BaseClasses
{
    public abstract class AutoPlanTemplateBase
    {
        #region Get methods
        //this is only here for the display name data binding. All other references to the template name use the explicit get method
        public string TemplateName { get; set; } = string.Empty;
        public List<PlanTarget> PlanTargets { get; set; } = new List<PlanTarget>();
        public List<RequestedTSStructure> CreateTSStructures { get; set; } = new List<RequestedTSStructure> { };
        public List<RequestedTSManipulation> TSManipulations { get; set; } = new List<RequestedTSManipulation> { };

        public List<PlanObjective> PlanObjectives { get; set; } = new List<PlanObjective> { };
        public List<Tuple<string, string, double, string>> GetRequestedPlanDoseInfo() { return requestedPlanDoseInfo; }
        //ID, lower dose level (%), dose (%) to be used in optimization constraint, volume (%), priority, conditions (<D, V, or DMax>, query value, equality operator, constraint value)
        public List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> GetRequestedOptTSStructures() { return requestedOptTSStructures; }
        #endregion

        #region Set methods
        public void SetRequestedPlanDoseInfo(List<Tuple<string, string, double, string>> value) { requestedPlanDoseInfo = new List<Tuple<string, string, double, string>>(value); }
        public void SetRequestedOptTSStructures(List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> value) { requestedOptTSStructures = new List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>>(value); }
        #endregion

        #region data members
        //requested items to be printed after each successful iteration of the optimization loop
        //structure id, constraint type, dose value (query type), units on dose (VOLUME WILL ALWAYS BE RELATIVE!)
        protected List<Tuple<string, string, double, string>> requestedPlanDoseInfo = new List<Tuple<string, string, double, string>> { };
        //requested cooler and heater structures to be added after each iteration of the optimization loop (IF CERTAIN CRITERIA ARE MET!)
        //structure id, low dose (%), high dose (%), Volume (%), priority, List of criteria that must be met for the requested TS structure to be added (all constraints are AND)
        //NOTE! THE LOW DOSE AND HIGH DOSE VALUES ARE USED FOR GENERATING HEATER STRUCTURES. 
        //FOR COOLER STRUCTURES, THE LOW DOSE VALUE IS USED TO CONVERT AN ISODOSE LEVEL TO STRUCTURE WHEREAS THE HIGH DOSE VALUE IS USED TO GENERATE THE OPTIMIZATION CONSTRAINT
        protected List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> requestedOptTSStructures = new List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> { };
        #endregion
    }
}
