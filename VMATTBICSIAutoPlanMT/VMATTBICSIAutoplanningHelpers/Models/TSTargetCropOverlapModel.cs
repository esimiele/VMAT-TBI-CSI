using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class TSTargetCropOverlapModel
    {
        public string PlanId { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public string ManipulationTargetId { get; set; } = string.Empty;
        public TSManipulationType ManipulationType { get; set; } = TSManipulationType.None;

        public TSTargetCropOverlapModel(string planId, string targetId, string manipulationTargetId, TSManipulationType manipulationType)
        {
            PlanId = planId;
            TargetId = targetId;
            ManipulationTargetId = manipulationTargetId;
            ManipulationType = manipulationType;
        }
    }
}
