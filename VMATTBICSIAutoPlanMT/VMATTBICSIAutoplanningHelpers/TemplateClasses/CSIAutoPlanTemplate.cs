using System;
using System.Collections.Generic;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoplanningHelpers.TemplateClasses
{
    public class CSIAutoPlanTemplate
    {
        #region Get Methods
        public string GetTemplateName() { return templateName; }
        public double GetInitialRxDosePerFx() { return initialRxDosePerFx; }
        public int GetInitialRxNumFx() { return initialRxNumFx; }
        public double GetBoostRxDosePerFx() { return boostRxDosePerFx; }
        public int GetBoostRxNumFx() { return boostRxNumFx; }
        public List<Tuple<string, double, string>> GetTargets() { return targets; }
        public List<Tuple<string, string, double>> GetTSManipulations() { return TSManipulations; }
        public List<Tuple<string, string, double, double, int>> GetInitOptimizationConstraints() { return initOptConstraints; }
        public List<Tuple<string, string, double, double, int>> GetBoostOptimizationConstraints() { return bstOptConstraints; }
        public List<string> GetCropAndOverlapStructures() { return cropAndOverlapStructures; }
        public List<Tuple<string, string>> GetCreateTSStructures() { return createTSStructures; }
        public List<Tuple<string, string, double, double, DoseValuePresentation>> GetPlanObjectives() { return planObj; }
        public List<Tuple<string, string, double, string>> GetRequestedPlanDoseInfo() { return requestedPlanDoseInfo; }
        public List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> GetRequestedOptTSStructures() { return requestedOptTSStructures; }
        #endregion

        #region Set Methods
        public void SetTemplateName(string value) { templateName = value; }
        public void SetInitRxDosePerFx(double value) { initialRxDosePerFx = value; }
        public void SetInitialRxNumFx(int value) { initialRxNumFx = value; }
        public void SetBoostRxDosePerFx(double value) { boostRxDosePerFx = value; }
        public void SetBoostRxNumFx(int value) { boostRxNumFx = value; }
        public void SetTargets(List<Tuple<string, double, string>> value) { targets = new List<Tuple<string, double, string>>(value); }
        public void SetTSManipulations(List<Tuple<string, string, double>> value) { TSManipulations = new List<Tuple<string, string, double>>(value); }
        public void SetInitOptimizationConstraints(List<Tuple<string, string, double, double, int>> value) { initOptConstraints = new List<Tuple<string, string, double, double, int>>(value); }
        public void SetBoostOptimizationConstraints(List<Tuple<string, string, double, double, int>> value) { bstOptConstraints = new List<Tuple<string, string, double, double, int>>(value); }
        public void SetCropAndOverlapStructures(List<string> value) { cropAndOverlapStructures = new List<string>(value); }
        public void SetCreateTSStructures(List<Tuple<string, string>> value) { createTSStructures = new List<Tuple<string, string>>(value); }
        public void SetPlanObjectives(List<Tuple<string, string, double, double, DoseValuePresentation>> value) { planObj = new List<Tuple<string, string, double, double, DoseValuePresentation>>(value); }
        public void SetRequestedPlanDoseInfo(List<Tuple<string, string, double, string>> value) { requestedPlanDoseInfo = new List<Tuple<string, string, double, string>>(value); }
        public void SetRequestedOptTSStructures(List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> value) { requestedOptTSStructures = new List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>>(value); }
        #endregion

        private string templateName;
        private double initialRxDosePerFx = 0.1;
        private int initialRxNumFx = 1;
        private double boostRxDosePerFx = 0.1;
        private int boostRxNumFx = 1;
        //structure ID, Rx dose, plan Id
        private List<Tuple<string, double, string>> targets = new List<Tuple<string, double, string>> { };
        //structure ID, sparing type, margin
        private List<Tuple<string, string, double>> TSManipulations = new List<Tuple<string, string, double>> { };
        //structure, constraint type, dose cGy, volume %, priority
        private List<Tuple<string, string, double, double, int>> initOptConstraints = new List<Tuple<string, string, double, double, int>> { };
        private List<Tuple<string, string, double, double, int>> bstOptConstraints = new List<Tuple<string, string, double, double, int>> { };
        //list of structures that should be cropped from targets and overlap with targets also contoured. these manipulations will be used to update the optimization constraints for all targets
        private List<string> cropAndOverlapStructures = new List<string> { };
        //DICOM type, structure ID
        private List<Tuple<string, string>> createTSStructures = new List<Tuple<string, string>> { };
        // plan objectives (ONLY ONE PER TEMPLATE!)
        private List<Tuple<string, string, double, double, DoseValuePresentation>> planObj = new List<Tuple<string, string, double, double, DoseValuePresentation>> { };
        //requested items to be printed after each successful iteration of the optimization loop
        //structure id, constraint type, dose value (query type), units on dose (VOLUME WILL ALWAYS BE RELATIVE!)
        private List<Tuple<string, string, double, string>> requestedPlanDoseInfo = new List<Tuple<string, string, double, string>> { };
        //requested cooler and heater structures to be added after each iteration of the optimization loop (IF CERTAIN CRITERIA ARE MET!)
        //structure id, low dose (%), high dose (%), Volume (%), priority, List of criteria that must be met for the requested TS structure to be added (all constraints are AND)
        //NOTE! THE LOW DOSE AND HIGH DOSE VALUES ARE USED FOR GENERATING HEATER STRUCTURES. 
        //FOR COOLER STRUCTURES, THE LOW DOSE VALUE IS USED TO CONVERT AN ISODOSE LEVEL TO STRUCTURE WHEREAS THE HIGH DOSE VALUE IS USED TO GENERATE THE OPTIMIZATION CONSTRAINT
        private List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> requestedOptTSStructures = new List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>>{};

        public CSIAutoPlanTemplate()
        {
        }

        public CSIAutoPlanTemplate(int count)
        {
            templateName = String.Format("Template: {0}", count);
        }

        public CSIAutoPlanTemplate(string name)
        {
            templateName = name;
        }
    }
}
