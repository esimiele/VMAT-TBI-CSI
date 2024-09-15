using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class TSCoolerStructureModel : RequestedOptimizationTSStructureModel
    {
        public double UpperDoseValue { get; set; } = double.NaN;
        public TSCoolerStructureModel(string structure, double high, double optDose, int priority, double optVol = 0.0)
        {
            TSStructureId = structure;
            UpperDoseValue = high;
            Constraints = new List<OptimizationConstraintModel>
            {
                new OptimizationConstraintModel(TSStructureId, Enums.OptimizationObjectiveType.Upper, optDose, Enums.Units.Percent, optVol, priority),
            };
        }

        public TSCoolerStructureModel(string structure, double high, double optDose, int priority, IEnumerable<OptTSCreationCriteriaModel> createCriteria, double optVol = 0.0)
        {
            TSStructureId = structure;
            UpperDoseValue = high;
            CreationCriteria = new List<OptTSCreationCriteriaModel>(createCriteria);
            Constraints = new List<OptimizationConstraintModel>
            {
                new OptimizationConstraintModel(TSStructureId, Enums.OptimizationObjectiveType.Upper, optDose, Enums.Units.Percent, optVol, priority),
            };
        }
    }
}
