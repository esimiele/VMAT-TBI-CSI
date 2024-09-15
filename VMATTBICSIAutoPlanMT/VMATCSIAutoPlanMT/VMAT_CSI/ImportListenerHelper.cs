using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATCSIAutoPlanMT.VMAT_CSI
{
    public static class ImportListenerHelper
    {
        public static string GetImportListenerExePath()
        {
            string path = Assembly.GetExecutingAssembly().Location;
            return Directory.GetFiles(Path.GetDirectoryName(path), "*.exe").FirstOrDefault(x => x.Contains("ImportListener"));

        }

        public static bool LaunchImportListener(string listener, ImportExportDataModel IEData, string mrn)
        {
            if (!string.IsNullOrEmpty(listener))
            {
                ProcessStartInfo p = new ProcessStartInfo(listener);
                p.Arguments = $"{IEData.ImportLocation} {mrn} {IEData.AriaDBDaemon.AETitle} {IEData.AriaDBDaemon.IP} {IEData.AriaDBDaemon.Port} {IEData.LocalDaemon.AETitle} {IEData.LocalDaemon.Port} {3600.0}";
                Process.Start(p);
                return false;
            }
            else return true;
        }
    }
}
