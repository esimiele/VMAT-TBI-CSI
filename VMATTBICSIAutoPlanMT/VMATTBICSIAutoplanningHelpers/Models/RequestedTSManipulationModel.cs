using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class RequestedTSManipulationModel
    {
        public string StructureId { get; set; } = string.Empty;
        public TSManipulationType ManipulationType { get; set; } = TSManipulationType.None;
        public double MarginInCM { get; set; } = double.NaN;

        public RequestedTSManipulationModel(string structureId, TSManipulationType manipulationType, double marginInCM)
        {
            StructureId = structureId;
            ManipulationType = manipulationType;
            MarginInCM = marginInCM;
        }
    }
}
