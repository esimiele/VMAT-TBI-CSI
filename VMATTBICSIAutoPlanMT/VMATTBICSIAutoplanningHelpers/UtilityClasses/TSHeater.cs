using System.Collections.Generic;

namespace VMATTBICSIAutoPlanningHelpers.UtilityClasses
{
    public class TSHeater : RequestedOptimizationTSStructure
    {
        public double LowerDoseValue { get; set; } = double.NaN;
        public double UpperDoseValue { get; set; } = double.NaN;
        public TSHeater(string structure, double low, double high, double optDose, int priority, double optVol = 100.0) 
        { 
            TSStructureId = structure;
            LowerDoseValue = low;
            UpperDoseValue = high;
            Constraints = new List<OptimizationConstraint>
            {
                new OptimizationConstraint(TSStructureId, Enums.OptimizationObjectiveType.Lower, optDose, Enums.Units.cGy, optVol, priority),
            };
        }
    }
}
