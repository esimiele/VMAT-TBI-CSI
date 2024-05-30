using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class PlanOptimizationSetup
    {
        public string PlanId { get; set; } = string.Empty;
        public List<OptimizationConstraint> OptimizationConstraints { get; set; } = new List<OptimizationConstraint>();
        public PlanOptimizationSetup() { }
        public PlanOptimizationSetup(string id, IEnumerable<OptimizationConstraint> constraints)
        { 
            PlanId = id;
            OptimizationConstraints = new List<OptimizationConstraint>(constraints);
        }
    }
}
