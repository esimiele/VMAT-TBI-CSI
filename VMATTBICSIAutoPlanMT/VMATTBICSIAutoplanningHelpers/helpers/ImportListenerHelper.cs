using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Structs;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class ImportListenerHelper
    {
        public static string GetImportListenerExePath()
        {
            string path = Assembly.GetExecutingAssembly().Location;
            return Directory.GetFiles(Path.GetDirectoryName(path), "*.exe").FirstOrDefault(x => x.Contains("ImportListener"));

        }

        public static bool LaunchImportListener(string listener, ImportExportDataStruct IEData, string mrn)
        {
            if (!string.IsNullOrEmpty(listener))
            {
                ProcessStartInfo p = new ProcessStartInfo(listener);
                p.Arguments = $"{IEData.ImportLocation} {mrn} {IEData.AriaDBDaemon.Item1} {IEData.AriaDBDaemon.Item2} {IEData.AriaDBDaemon.Item3} {IEData.LocalDaemon.Item1} {IEData.LocalDaemon.Item3} {3600.0}";
                Process.Start(p);
                return false;
            }
            else return true;
        }
    }
}
