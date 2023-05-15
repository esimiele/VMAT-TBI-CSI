using System.IO;
using System.Linq;

namespace VMATTBICSIAutoPlanningHelpers.Logging
{
    public static class LogHelper
    {
        public static string GetFullLogFileFromExistingMRN(string mrn, string logFilePath)
        {
            string logName = "";
            if (Directory.Exists(logFilePath + "\\preparation\\"))
            {
                logName = Directory.GetFiles(logFilePath + "\\preparation\\", ".", SearchOption.AllDirectories).FirstOrDefault(x => x.Contains(mrn));
            }
            return logName;
        }
    }
}
