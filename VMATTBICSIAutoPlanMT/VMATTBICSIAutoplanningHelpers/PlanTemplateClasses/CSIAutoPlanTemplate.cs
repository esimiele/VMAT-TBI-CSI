using System;
using System.Collections.Generic;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpers.PlanTemplateClasses
{
    public class CSIAutoPlanTemplate : AutoPlanTemplateBase
    {
        #region Properties
        public double InitialRxDosePerFx { get; set; } = 0.1;
        public int InitialRxNumberOfFractions { get; set; } = 1;
        public double BoostRxDosePerFx { get; set; } = 0.1;
        public int BoostRxNumberOfFractions { get; set; } = 1;
        public List<TSRing> Rings { get; set; } = new List<TSRing>();
        public List<string> CropAndOverlapStructures { get; set; } = new List<string> { };
        public List<OptimizationConstraint> InitialOptimizationConstraints { get; set; } = new List<OptimizationConstraint> { };
        public List<OptimizationConstraint> BoostOptimizationConstraints { get; set; } = new List<OptimizationConstraint> { };
        #endregion

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
