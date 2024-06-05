using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class PlanTargetsModel
    {
        public string PlanId { get; set; } = string.Empty;
        public List<TargetModel> Targets { get; set; } = new List<TargetModel>();

        public PlanTargetsModel(string plan, IEnumerable<TargetModel> tgts) 
        {
            PlanId = plan;
            Targets = new List<TargetModel>(tgts);
        }

        public PlanTargetsModel(string plan, TargetModel tgts)
        {
            PlanId = plan;
            Targets.Add(tgts);
        }

        public PlanTargetsModel(string plan, double tgtRx, string tgtId)
        {
            PlanId = plan;
            Targets = new List<TargetModel>
            {
                new TargetModel(tgtId, tgtRx)
            };
        }
    }
}
