using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers
{
    public static class ExportFormatTypeHelper
    {
        /// <summary>
        /// Simple helper method to convert the string representation of the export type to the enum representation
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static ImgExportFormat GetExportFormatType(string type)
        {
            if (string.Equals(type, "dcm")) return ImgExportFormat.DICOM;
            else return ImgExportFormat.PNG;
        }
    }
}
