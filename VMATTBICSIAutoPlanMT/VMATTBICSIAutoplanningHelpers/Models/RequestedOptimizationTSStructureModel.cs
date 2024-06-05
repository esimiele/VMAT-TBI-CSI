using System.Collections.Generic;
using System.Linq;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public abstract class RequestedOptimizationTSStructureModel
    {
        public string TSStructureId { get; set; } = string.Empty;
        public List<OptimizationConstraintModel> Constraints { get; set; } = new List<OptimizationConstraintModel>();
        public List<OptTSCreationCriteriaModel> CreationCriteria { get; set; } = new List<OptTSCreationCriteriaModel> { };
        public bool AllCriteriaMet(bool isFinalOpt) 
        {
            return CreationCriteria.All(x => x.CriteriaMet(isFinalOpt));
        }
    }
}
