using System;
using System.Collections.Generic;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.UtilityClasses;

namespace VMATTBICSIAutoPlanningHelpers.PlanTemplateClasses
{
    public class CSIAutoPlanTemplate : AutoPlanTemplateBase
    {
        #region Get Methods
        public double GetInitialRxDosePerFx() { return initialRxDosePerFx; }
        public int GetInitialRxNumFx() { return initialRxNumFx; }
        public double GetBoostRxDosePerFx() { return boostRxDosePerFx; }
        public int GetBoostRxNumFx() { return boostRxNumFx; }

        public List<TSRing> Rings { get; set; } = new List<TSRing>();
        public List<string> GetCropAndOverlapStructures() { return cropAndOverlapStructures; }
        public List<OptimizationConstraint> InitialOptimizationConstraints { get; set; } = new List<OptimizationConstraint> { };
        public List<OptimizationConstraint> BoostOptimizationConstraints { get; set; } = new List<OptimizationConstraint> { };
        #endregion

        #region Set Methods
        public void SetInitRxDosePerFx(double value) { initialRxDosePerFx = value; }
        public void SetInitialRxNumFx(int value) { initialRxNumFx = value; }
        public void SetBoostRxDosePerFx(double value) { boostRxDosePerFx = value; }
        public void SetBoostRxNumFx(int value) { boostRxNumFx = value; }
        public void SetCropAndOverlapStructures(List<string> value) { cropAndOverlapStructures = new List<string>(value); }
        #endregion

        private double initialRxDosePerFx = 0.1;
        private int initialRxNumFx = 1;
        private double boostRxDosePerFx = 0.1;
        private int boostRxNumFx = 1;
        
        //list of structures that should be cropped from targets and overlap with targets also contoured. these manipulations will be used to update the optimization constraints for all targets
        private List<string> cropAndOverlapStructures = new List<string> { };
        //structure, constraint type, dose cGy, volume %, priority

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
            TemplateName = $"Template: {count}";
        }

        /// <summary>
        /// Overloaded constructor taking a string as input
        /// </summary>
        /// <param name="name"></param>
        public CSIAutoPlanTemplate(string name)
        {
            TemplateName = name;
        }
    }
}
