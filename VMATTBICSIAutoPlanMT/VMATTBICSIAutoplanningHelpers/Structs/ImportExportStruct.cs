using System;
using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.Structs
{
    public class ImportExportStruct
    {
        public Tuple<string, string, int> AriaDBDaemon { get; set; } = new Tuple<string, string, int> ("","",0 );
        public string AriaDBAET { get; set; } = "";        // AE title of VMS DB Daemon
        public string AriaDBIP { get; set; } = "";         // IP address of server hosting the DB Daemon
        public int AriaDBPort { get; set; } = 0;           //port daemon is listening to
        public string VMSFileAET { get; set; } = "";       // AE title of VMS File Daemon
        public string VMSFileIP { get; set; } = "";        // IP address of server hosting the DB Daemon
        public int VMSFIlePort { get; set; } = 0;          //port daemon is listening to
        public string LocalAET { get; set; } = "";         // local AE title
        public int LocalPort { get; set; } = 0;            //local port

        public ImgExportFormat ExportFormat { get; set; } = ImgExportFormat.PNG;
        public string WriteLocation { get; set; } = "";
        public string ImportLocation { get; set; } = "";
    }
}
