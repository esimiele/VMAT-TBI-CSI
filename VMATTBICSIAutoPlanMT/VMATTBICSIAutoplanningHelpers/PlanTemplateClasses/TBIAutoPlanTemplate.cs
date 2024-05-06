using System;
using System.Collections.Generic;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.UtilityClasses;

namespace VMATTBICSIAutoPlanningHelpers.PlanTemplateClasses 
{
    public class TBIAutoPlanTemplate : AutoPlanTemplateBase
    {
        #region Get Methods
        //this is only here for the display name data binding. All other references to the template name use the explicit get method
        public double GetInitialRxDosePerFx() { return initialRxDosePerFx; }
        public int GetInitialRxNumFx() { return initialRxNumFx; }
        public List<OptimizationConstraint> InitialOptimizationConstraints { get; set; } = new List<OptimizationConstraint>();
        #endregion

        #region Set Methods
        public void SetInitRxDosePerFx(double value) { initialRxDosePerFx = value; }
        public void SetInitialRxNumFx(int value) { initialRxNumFx = value; }
        #endregion

        private double initialRxDosePerFx = 0.1;
        private int initialRxNumFx = 1;

        /// <summary>
        /// Constructor
        /// </summary>
        public TBIAutoPlanTemplate()
        {
        }

        /// <summary>
        /// Overloaded constructor taking an int as input
        /// </summary>
        /// <param name="count"></param>
        public TBIAutoPlanTemplate(int count)
        {
            TemplateName = String.Format("Template: {0}", count);
        }

        /// <summary>
        /// Overloaded constructor taking a string as input
        /// </summary>
        /// <param name="name"></param>
        public TBIAutoPlanTemplate(string name)
        {
            TemplateName = name;
        }
    }
}
