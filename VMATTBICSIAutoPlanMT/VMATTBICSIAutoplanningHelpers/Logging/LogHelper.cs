﻿using System;
using System.IO;
using System.Linq;
using System.Text;
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
                if(Directory.GetFiles(logFilePath + "\\preparation\\", ".", SearchOption.AllDirectories).Any(x => x.Contains(mrn + ".txt")))
                {
                    logName = Directory.GetFiles(logFilePath + "\\preparation\\", ".", SearchOption.AllDirectories).First(x => x.Contains(mrn + ".txt"));
                }
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

        /// <summary>
        /// Helper method to parse the initial vmat plan (CSI or TBI) UID from the log file and match it to the corresponding plan in Eclipse
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static (string, StringBuilder) LoadVMATPlanUIDFromLogFile(string file)
        {
            StringBuilder sb = new StringBuilder();
            string initPlanUID = "";
            try
            {
                using (StreamReader reader = new StreamReader(file))
                {
                    string line;
                    while (!(line = reader.ReadLine()).Equals("Errors and warnings:"))
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            //useful info on this line
                            if (line.Contains("Plan UIDs:"))
                            {
                                //only ready the first plan UID --> CSI-init or vmat plan for TBI
                                if (!string.IsNullOrEmpty((line = reader.ReadLine().Trim())))
                                {
                                    initPlanUID = line;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                sb.AppendLine($"Could not retrieve plan UIDs from log file because: {e.Message}");
                sb.AppendLine(e.StackTrace);
            }
            return (initPlanUID, sb);
        }
    }
}
