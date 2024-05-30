using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class PlanFieldJunctions
    {
        public ExternalPlanSetup PlanSetup { get; set; } = null;
        public List<Structure> FieldJunctionStructures { get; set; } = new List<Structure>();
        public PlanFieldJunctions() { }
        public PlanFieldJunctions(ExternalPlanSetup p, IEnumerable<Structure> jnxs) 
        {
            PlanSetup = p;
            FieldJunctionStructures = new List<Structure>(jnxs);
        }
    }
}
