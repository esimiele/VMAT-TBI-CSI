using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class TSCooler : RequestedOptimizationTSStructure
    {
        public double UpperDoseValue { get; set; } = double.NaN;
        public TSCooler(string structure, double high, double optDose, int priority, double optVol = 0.0)
        {
            TSStructureId = structure;
            UpperDoseValue = high;
            Constraints = new List<OptimizationConstraint>
            {
                new OptimizationConstraint(TSStructureId, Enums.OptimizationObjectiveType.Upper, optDose, Enums.Units.cGy, optVol, priority),
            };
        }
    }
}
