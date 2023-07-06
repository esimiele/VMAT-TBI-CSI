using System;
using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.Structs
{
    public class ImportExportDataStruct
    {
        //AE title, IP, port
        public Tuple<string, string, int> AriaDBDaemon { get; set; } = new Tuple<string, string, int> ("","",-1);
        public Tuple<string, string, int> VMSFileDaemon { get; set; } = new Tuple<string, string, int> ("","",-1);
        public Tuple<string, string, int> LocalDaemon { get; set; } = new Tuple<string, string, int> ("","",-1);

        public ImgExportFormat ExportFormat { get; set; } = ImgExportFormat.PNG;
        public string WriteLocation { get; set; } = "";
        public string ImportLocation { get; set; } = "";
    }
}
