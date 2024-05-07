using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.UtilityClasses
{
    public class Prescription
    {
        public string PlanId { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public int NumberOfFractions { get; set; } = -1;
        public DoseValue DoseValue { get; set; } = new DoseValue();
        public double CumulativeDoseToTarget { get; set; } = double.NaN;

        public Prescription(string planId, 
                            string targetId, 
                            int numberOfFractions, 
                            DoseValue doseValue, 
                            double cumulativeDoseToTarget)
        {
            PlanId = planId;
            TargetId = targetId;
            NumberOfFractions = numberOfFractions;
            DoseValue = doseValue;
            CumulativeDoseToTarget = cumulativeDoseToTarget;
        }
    }
}
