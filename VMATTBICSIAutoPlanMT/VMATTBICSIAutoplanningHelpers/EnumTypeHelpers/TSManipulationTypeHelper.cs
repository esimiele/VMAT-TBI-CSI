using TSManipulationType = VMATTBICSIAutoPlanningHelpers.Enums.TSManipulationType;

namespace VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers
{
    public static class TSManipulationTypeHelper
    {
        /// <summary>
        /// Helper utility method to take the supplied string representation of the manipulation type and convert it to an enum
        /// </summary>
        /// <param name="manipulation"></param>
        /// <returns></returns>
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
