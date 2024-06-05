using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class PrescriptionModel
    {
        public string PlanId { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public int NumberOfFractions { get; set; } = -1;
        public DoseValue DosePerFraction { get; set; } = new DoseValue();
        public double CumulativeDoseToTarget { get; set; } = double.NaN;

        public PrescriptionModel(string planId, 
                            string targetId, 
                            int numberOfFractions, 
                            DoseValue doseValue, 
                            double cumulativeDoseToTarget)
        {
            PlanId = planId;
            TargetId = targetId;
            NumberOfFractions = numberOfFractions;
            DosePerFraction = doseValue;
            CumulativeDoseToTarget = cumulativeDoseToTarget;
        }
    }
}
