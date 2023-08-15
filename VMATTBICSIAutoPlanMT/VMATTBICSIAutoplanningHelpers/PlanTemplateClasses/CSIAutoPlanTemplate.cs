using System;
using System.Collections.Generic;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;

namespace VMATTBICSIAutoPlanningHelpers.PlanTemplateClasses
{
    public class CSIAutoPlanTemplate : AutoPlanTemplateBase
    {
        #region Get Methods
        public double GetInitialRxDosePerFx() { return initialRxDosePerFx; }
        public int GetInitialRxNumFx() { return initialRxNumFx; }
        public double GetBoostRxDosePerFx() { return boostRxDosePerFx; }
        public int GetBoostRxNumFx() { return boostRxNumFx; }
        public List<Tuple<string, double, double, double>> GetCreateRings() { return createRings; }
        public List<string> GetCropAndOverlapStructures() { return cropAndOverlapStructures; }
        public List<Tuple<string, OptimizationObjectiveType, double, double, int>> GetInitOptimizationConstraints() { return initOptConstraints; }
        public List<Tuple<string, OptimizationObjectiveType, double, double, int>> GetBoostOptimizationConstraints() { return bstOptConstraints; }
        #endregion

        #region Set Methods
        public void SetInitRxDosePerFx(double value) { initialRxDosePerFx = value; }
        public void SetInitialRxNumFx(int value) { initialRxNumFx = value; }
        public void SetBoostRxDosePerFx(double value) { boostRxDosePerFx = value; }
        public void SetBoostRxNumFx(int value) { boostRxNumFx = value; }
        public void SetCreateRings(List<Tuple<string, double, double, double>> value) { createRings = new List<Tuple<string, double, double, double>>(value); }
        public void SetCropAndOverlapStructures(List<string> value) { cropAndOverlapStructures = new List<string>(value); }
        public void SetInitOptimizationConstraints(List<Tuple<string, OptimizationObjectiveType, double, double, int>> value) { initOptConstraints = new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(value); }
        public void SetBoostOptimizationConstraints(List<Tuple<string, OptimizationObjectiveType, double, double, int>> value) { bstOptConstraints = new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(value); }
        #endregion

        private double initialRxDosePerFx = 0.1;
        private int initialRxNumFx = 1;
        private double boostRxDosePerFx = 0.1;
        private int boostRxNumFx = 1;
        
        //target to create ring from, margin, thickness, dose level (cGy)
        private List<Tuple<string, double, double, double>> createRings = new List<Tuple<string, double, double, double>> { };
        //list of structures that should be cropped from targets and overlap with targets also contoured. these manipulations will be used to update the optimization constraints for all targets
        private List<string> cropAndOverlapStructures = new List<string> { };
        //structure, constraint type, dose cGy, volume %, priority
        private List<Tuple<string, OptimizationObjectiveType, double, double, int>> initOptConstraints = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
        private List<Tuple<string, OptimizationObjectiveType, double, double, int>> bstOptConstraints = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };

        /// <summary>
        /// Constructor
        /// </summary>
        public CSIAutoPlanTemplate()
        {
        }

        /// <summary>
        /// Overloaded constructor taking an int as input
        /// </summary>
        /// <param name="count"></param>
        public CSIAutoPlanTemplate(int count)
        {
            templateName = $"Template: {count}";
        }

        /// <summary>
        /// Overloaded constructor taking a string as input
        /// </summary>
        /// <param name="name"></param>
        public CSIAutoPlanTemplate(string name)
        {
            templateName = name;
        }
    }
}
