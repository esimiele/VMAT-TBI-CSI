using System;
using System.Collections.Generic;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.BaseClasses
{
    public abstract class AutoPlanTemplateBase
    {
        #region Get methods
        //this is only here for the display name data binding. All other references to the template name use the explicit get method
        public string TemplateName { get { return templateName; } }
        public string GetTemplateName() { return templateName; }
        public List<Tuple<string, double, string>> GetTargets() { return targets; }
        public List<Tuple<string, string>> GetCreateTSStructures() { return createTSStructures; }
        public List<Tuple<string, TSManipulationType, double>> GetTSManipulations() { return TSManipulations; }
        public List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> GetPlanObjectives() { return planObj; }
        public List<Tuple<string, string, double, string>> GetRequestedPlanDoseInfo() { return requestedPlanDoseInfo; }
        //ID, lower dose level (%), dose (%) to be used in optimization constraint, volume (%), priority, conditions (<D, V, or DMax>, query value, equality operator, constraint value)
        public List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> GetRequestedOptTSStructures() { return requestedOptTSStructures; }
        #endregion

        #region Set methods
        public void SetTemplateName(string value) { templateName = value; }
        public void SetTargets(List<Tuple<string, double, string>> value) { targets = new List<Tuple<string, double, string>>(value); }
        public void SetCreateTSStructures(List<Tuple<string, string>> value) { createTSStructures = new List<Tuple<string, string>>(value); }
        public void SetTSManipulations(List<Tuple<string, TSManipulationType, double>> value) { TSManipulations = new List<Tuple<string, TSManipulationType, double>>(value); }
        public void SetPlanObjectives(List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> value) { planObj = new List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>>(value); }
        public void SetRequestedPlanDoseInfo(List<Tuple<string, string, double, string>> value) { requestedPlanDoseInfo = new List<Tuple<string, string, double, string>>(value); }
        public void SetRequestedOptTSStructures(List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> value) { requestedOptTSStructures = new List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>>(value); }
        #endregion

        #region data members
        protected string templateName;
        //structure ID, Rx dose, plan Id
        protected List<Tuple<string, double, string>> targets = new List<Tuple<string, double, string>> { };
        //DICOM type, structure ID
        protected List<Tuple<string, string>> createTSStructures = new List<Tuple<string, string>> { };
        //structure ID, sparing type, margin
        protected List<Tuple<string, TSManipulationType, double>> TSManipulations = new List<Tuple<string, TSManipulationType, double>> { };
        // plan objectives (ONLY ONE PER TEMPLATE!)
        protected List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> planObj = new List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> { };
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
