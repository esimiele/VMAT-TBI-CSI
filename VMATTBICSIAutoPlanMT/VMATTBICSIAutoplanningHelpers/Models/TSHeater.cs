using System.Collections.Generic;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class TSHeater : RequestedOptimizationTSStructure
    {
        public double LowerDoseValue { get; set; } = double.NaN;
        public double UpperDoseValue { get; set; } = double.NaN;
        public TSHeater(string structure, double low, double high, int priority, double optVol = 100.0) 
        { 
            TSStructureId = structure;
            LowerDoseValue = low;
            UpperDoseValue = high;
            Constraints = new List<OptimizationConstraint>
            {
                new OptimizationConstraint(TSStructureId, Enums.OptimizationObjectiveType.Lower, 100.0, Enums.Units.Percent, optVol, priority),
                new OptimizationConstraint(TSStructureId, Enums.OptimizationObjectiveType.Upper, 101.0, Enums.Units.Percent, 0.0, priority),
            };
        }
    }
}
