using System.Collections.Generic;
using System.Linq;

namespace VMATTBICSIAutoPlanningHelpers.UtilityClasses
{
    public class RequestedOptimizationTSStructure
    {
        public string TSStructureId { get; set; } = string.Empty;
        public List<OptimizationConstraint> Constraints { get; set; } = new List<OptimizationConstraint>();
        public List<OptTSCreationCriteria> CreationCriteria { get; set; } = new List<OptTSCreationCriteria> { };
        public bool AllCriteriaMet() 
        {
            return CreationCriteria.All(x => x.CriteriaMet());
        }
    }
}
