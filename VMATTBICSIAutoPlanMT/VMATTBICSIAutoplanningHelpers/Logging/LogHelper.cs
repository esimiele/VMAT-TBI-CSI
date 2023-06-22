using System;
using System.IO;
using System.Linq;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.Logging
{
    public static class LogHelper
    {
        /// <summary>
        /// Helper method to get the full log file path for a given patient mrn and initial log path specified in the log configuration .ini file
        /// </summary>
        /// <param name="mrn"></param>
        /// <param name="logFilePath"></param>
        /// <returns></returns>
        public static string GetFullLogFileFromExistingMRN(string mrn, string logFilePath)
        {
            string logName = "";
            if (Directory.Exists(logFilePath + "\\preparation\\"))
            {
                logName = Directory.GetFiles(logFilePath + "\\preparation\\", ".", SearchOption.AllDirectories).FirstOrDefault(x => x.Contains(mrn));
            }
            return logName;
        }

        /// <summary>
        /// Helper method to parse the prescription information from the log files generated from the preparation script
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static Tuple<string, string, int, DoseValue, double> ParsePrescriptionsFromLogFile(string line)
        {
            string planId;
            string targetId;
            int numFx;
            double dosePerFx;
            double RxDose;
            line = ConfigurationHelper.CropLine(line, "{");
            planId = line.Substring(0, line.IndexOf(","));
            line = ConfigurationHelper.CropLine(line, ",");
            targetId = line.Substring(0, line.IndexOf(","));
            line = ConfigurationHelper.CropLine(line, ",");
            numFx = int.Parse(line.Substring(0, line.IndexOf(",")));
            line = ConfigurationHelper.CropLine(line, ",");
            dosePerFx = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = ConfigurationHelper.CropLine(line, ",");
            RxDose = double.Parse(line.Substring(0, line.IndexOf("}")));
            return Tuple.Create(planId, targetId, numFx, new DoseValue(dosePerFx, DoseValue.DoseUnit.cGy), RxDose);
        }

        /// <summary>
        /// Helper method to parse the normalization volumes from the log files generated from the preparation script
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static Tuple<string, string> ParseKeyValuePairFromLogFile(string line)
        {
            string planId;
            string volumeId;
            line = ConfigurationHelper.CropLine(line, "{");
            planId = line.Substring(0, line.IndexOf(","));
            line = ConfigurationHelper.CropLine(line, ",");
            volumeId = line.Substring(0, line.IndexOf("}"));
            return Tuple.Create(planId, volumeId);
        }
    }
}
