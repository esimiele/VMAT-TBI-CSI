using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class PlanTarget
    {
        public string TargetId { get; set; } = string.Empty;
        public double TargetRxDose { get; set; } = double.NaN;
        public string PlanId { get; set; } = string.Empty;

        public PlanTarget(string target, double dose, string plan) 
        {
            TargetId = target;
            TargetRxDose = dose;
            PlanId = plan;
        }
    }
}
