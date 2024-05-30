using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class RequestedTSManipulation
    {
        public string StructureId { get; set; } = string.Empty;
        public TSManipulationType ManipulationType { get; set; } = TSManipulationType.None;
        public double MarginInCM { get; set; } = double.NaN;

        public RequestedTSManipulation(string structureId, TSManipulationType manipulationType, double marginInCM)
        {
            StructureId = structureId;
            ManipulationType = manipulationType;
            MarginInCM = marginInCM;
        }
    }
}
