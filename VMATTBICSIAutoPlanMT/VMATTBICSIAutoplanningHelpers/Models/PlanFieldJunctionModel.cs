using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class PlanFieldJunctionModel
    {
        public ExternalPlanSetup PlanSetup { get; set; } = null;
        public List<Structure> FieldJunctionStructures { get; set; } = new List<Structure>();
        public PlanFieldJunctionModel() { }
        public PlanFieldJunctionModel(ExternalPlanSetup p, IEnumerable<Structure> jnxs) 
        {
            PlanSetup = p;
            FieldJunctionStructures = new List<Structure>(jnxs);
        }
    }
}
