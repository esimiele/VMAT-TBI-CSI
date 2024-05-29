using System;
using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.UtilityClasses
{
    /// <summary>
    /// Helper class to store the relevant information regarding image export
    /// </summary>
    public class ImportExportData
    {
        //AE title, IP, port
        public Daemon AriaDBDaemon { get; set; } = new Daemon();
        public Daemon VMSFileDaemon { get; set; } = new Daemon();
        public Daemon LocalDaemon { get; set; } = new Daemon();

        public ImgExportFormat ExportFormat { get; set; } = ImgExportFormat.PNG;
        public string WriteLocation { get; set; } = "";
        public string ImportLocation { get; set; } = "";
    }
}
