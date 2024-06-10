using System.Collections.Generic;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class TSHeaterStructureModel : RequestedOptimizationTSStructureModel
    {
        public double LowerDoseValue { get; set; } = double.NaN;
        public double UpperDoseValue { get; set; } = double.NaN;
        public TSHeaterStructureModel(string structure, double low, double high, int priority, double optVol = 100.0) 
        { 
            TSStructureId = structure;
            LowerDoseValue = low;
            UpperDoseValue = high;
            Constraints = new List<OptimizationConstraintModel>
            {
                new OptimizationConstraintModel(TSStructureId, Enums.OptimizationObjectiveType.Lower, 100.0, Enums.Units.Percent, optVol, priority),
                new OptimizationConstraintModel(TSStructureId, Enums.OptimizationObjectiveType.Upper, 101.0, Enums.Units.Percent, 0.0, priority),
            };
        }

        public TSHeaterStructureModel(string structure, double low, double high, int priority, IEnumerable<OptTSCreationCriteriaModel> createCriteria, double optVol = 100.0)
        {
            TSStructureId = structure;
            LowerDoseValue = low;
            UpperDoseValue = high;
            CreationCriteria = new List<OptTSCreationCriteriaModel>(createCriteria);
            Constraints = new List<OptimizationConstraintModel>
            {
                new OptimizationConstraintModel(TSStructureId, Enums.OptimizationObjectiveType.Lower, 100.0, Enums.Units.Percent, optVol, priority),
                new OptimizationConstraintModel(TSStructureId, Enums.OptimizationObjectiveType.Upper, 101.0, Enums.Units.Percent, 0.0, priority),
            };
        }
    }
}
