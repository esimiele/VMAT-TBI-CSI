using System.Collections.Generic;

namespace VMATTBICSIAutoPlanningHelpers.UtilityClasses
{
    public class PlanIsocenters
    {
        public string PlanId { get; set; } = string.Empty;
        public List<string> IsocenterIds { get; set; } = new List<string>();
        public Dictionary<string, int> IsoIdNumBeams { get; set; } = new Dictionary<string, int> { };

        public PlanIsocenters(string planid, IEnumerable<string> isoids)
        {
            PlanId = planid;
            IsocenterIds = new List<string>(isoids);
        }
    }
}
