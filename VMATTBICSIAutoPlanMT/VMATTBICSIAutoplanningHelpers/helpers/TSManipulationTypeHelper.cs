using TSManipulationType = VMATTBICSIAutoPlanningHelpers.Enums.TSManipulationType;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class TSManipulationTypeHelper
    {
        public static TSManipulationType GetTSManipulationType(string manipulation)
        {
            manipulation = manipulation.Replace(" ", "").ToLower();
            if (string.Equals(manipulation, "croptargetfromstructure")) return TSManipulationType.CropTargetFromStructure;
            else if (string.Equals(manipulation, "contouroverlapwithtarget")) return TSManipulationType.ContourOverlapWithTarget;
            else if (string.Equals(manipulation, "cropfrombody")) return TSManipulationType.CropFromBody;
            else if (string.Equals(manipulation, "contoursubstructure")) return TSManipulationType.ContourSubStructure;
            else if (string.Equals(manipulation, "contourouterstructure")) return TSManipulationType.ContourOuterStructure;
            else return TSManipulationType.None;
        }
    }
}
