using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.UtilityClasses
{
    public class RequestedOptimizationTSStructure
    {
        public string StructureId { get; set; } = string.Empty;
        public double Volume { get; set; } = double.NaN;
        public Units Units { get; set; } = Units.None;
        public int Priority { get; set; } = -1;
        public List<OptTSCreationCriteria> CreationCriteria { get; set; } = new List<OptTSCreationCriteria> { };
    }
}
