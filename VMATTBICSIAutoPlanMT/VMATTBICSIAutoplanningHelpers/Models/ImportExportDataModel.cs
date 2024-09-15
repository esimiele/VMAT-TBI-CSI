using System;
using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    /// <summary>
    /// Helper class to store the relevant information regarding image export
    /// </summary>
    public class ImportExportDataModel
    {
        //AE title, IP, port
        public DaemonModel AriaDBDaemon { get; set; } = new DaemonModel();
        public DaemonModel VMSFileDaemon { get; set; } = new DaemonModel();
        public DaemonModel LocalDaemon { get; set; } = new DaemonModel();

        public ImgExportFormat ExportFormat { get; set; } = ImgExportFormat.PNG;
        public string WriteLocation { get; set; } = "";
        public string ImportLocation { get; set; } = "";
    }
}
