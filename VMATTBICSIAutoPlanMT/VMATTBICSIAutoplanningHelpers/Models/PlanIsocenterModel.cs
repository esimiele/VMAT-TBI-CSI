using System.Collections.Generic;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class PlanIsocenterModel
    {
        public string PlanId { get; set; } = string.Empty;
        public List<IsocenterModel> Isocenters { get; set; } = new List<IsocenterModel> { };

        public PlanIsocenterModel() { }

        public PlanIsocenterModel(string planid, IEnumerable<IsocenterModel> isos)
        {
            PlanId = planid;
            Isocenters = new List<IsocenterModel>(isos);
        }

        public PlanIsocenterModel(string planId, IsocenterModel isocenter)
        {
            PlanId = planId;
            Isocenters.Add(isocenter);
        }
    }
}
