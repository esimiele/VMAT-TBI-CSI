using System;
using System.Collections.Generic;
using System.Windows;
using System.Text;
using System.IO;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.Enums;
using PlanType = VMATTBICSIAutoPlanningHelpers.Enums.PlanType;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using DialogResult = System.Windows.Forms.DialogResult;
using System.Reflection;

namespace VMATTBICSIAutoPlanningHelpers.Logging
{
    public class Logger
    {
        #region Set methods
        //general patient info
        public string MRN { set { mrn = value; } }
        public void SetPlanType(PlanType type) { planType = type; }
        public string Template { set => template = value; }
        public string StructureSet { set => selectedSS = value; }
        public bool ChangesSaved { set => changesSaved = value; }
        public string User { set => userId = value; }
        public string LogPath { get { return logPath; } set { logPath = value; } }
        //plan ID, target Id, numFx, dosePerFx, cumulative dose
        public List<Tuple<string, string, int, DoseValue, double>> Prescriptions { set => prescriptions = new List<Tuple<string, string, int, DoseValue, double>>(value); }
        public List<string> AddedPrelimTargetsStructures { set => addedPrelimTargets = new List<string>(value); }
        //ts generation and manipulation
        public List<string> AddedStructures { set => addedStructures = new List<string>(value); }
        //structure ID, sparing type, margin
        public List<Tuple<string, TSManipulationType, double>> StructureManipulations { set => structureManipulations = new List<Tuple<string, TSManipulationType, double>>(value); }
        //plan id, list<original target id, ts target id>
        public List<Tuple<string, string>> TSTargets { set => tsTargets = new List<Tuple<string, string>>(value); }
        //plan id, normalization volume for plan
        public List<Tuple<string, string>> NormalizationVolumes { set => normVolumes = new List<Tuple<string, string>>(value); }
        //plan Id, list of isocenter names for this plan
        public List<Tuple<string, List<string>>> IsoNames { set => isoNames = new List<Tuple<string, List<string>>>(value); }
        //plan generation and beam placement
        public List<string> PlanUIDs { set => planUIDs = new List<string>(value); }
        //optimization setup
        //plan ID, <structure, constraint type, dose cGy, volume %, priority>
        public List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> OptimizationConstraints { set => optimizationConstraints = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>(value); }
        public ScriptOperationType OpType { set => opType = value; }
        #endregion

        //path to location to write log file
        private string logPath = "";
        //stringbuilder object to log output from ts generation/manipulation and beam placement
        private StringBuilder _logFromOperations;
        private StringBuilder _logFromErrors;
        private string userId;
        private string mrn;
        private PlanType planType;
        private string template;
        private string selectedSS;
        bool changesSaved = false;
        List<Tuple<string, string, int, DoseValue, double>> prescriptions;
        private List<string> addedPrelimTargets;
        private List<string> addedStructures;
        private List<Tuple<string, TSManipulationType, double>> structureManipulations;
        private List<Tuple<string, string>> tsTargets;
        private List<Tuple<string, string>> normVolumes;
        private List<Tuple<string, List<string>>> isoNames;
        private List<string> planUIDs;
        private List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> optimizationConstraints;
        private ScriptOperationType opType = ScriptOperationType.General;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path"></param>
        /// <param name="theType"></param>
        /// <param name="patient"></param>
        public Logger(string path, PlanType theType, string patient)
        {
            logPath = path;
            planType = theType;
            mrn = patient;

            selectedSS = "";
            prescriptions = new List<Tuple<string, string, int, DoseValue, double>> { };
            addedPrelimTargets = new List<string> { };
            addedStructures = new List<string> { };
            structureManipulations = new List<Tuple<string, TSManipulationType, double>> { };
            tsTargets = new List<Tuple<string, string>> { };
            normVolumes = new List<Tuple<string, string>> { };
            isoNames = new List<Tuple<string, List<string>>> { };
            planUIDs = new List<string> { };
            optimizationConstraints = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };
            _logFromOperations = new StringBuilder();
            _logFromErrors = new StringBuilder();
        }

        /// <summary>
        /// Helper method to copy a stringbuilder object to the logs. First argument is the operation type as a string and the second argument
        /// is the detailed logs from that operation
        /// </summary>
        /// <param name="info"></param>
        /// <param name="s"></param>
        public void AppendLogOutput(string info, StringBuilder s)
        {
            _logFromOperations.AppendLine(info);
            _logFromOperations.Append(s);
            _logFromOperations.AppendLine("");
        }

        /// <summary>
        /// Simple method to copy a string to the logs
        /// </summary>
        /// <param name="info"></param>
        public void AppendLogOutput(string info)
        {
            _logFromOperations.AppendLine(info);
            _logFromOperations.AppendLine("");
        }

        /// <summary>
        /// Helper method to record an error or warning that occurred during script operation. Also optional to suppress the message box
        /// warning
        /// </summary>
        /// <param name="error"></param>
        /// <param name="suppressMessage"></param>
        public void LogError(string error, bool suppressMessage = false)
        {
            if(!suppressMessage) MessageBox.Show(error);
            _logFromErrors.AppendLine(error);
            _logFromErrors.AppendLine("");
        }

        /// <summary>
        /// Same as above except the input argument is a string builder instead of a string
        /// </summary>
        /// <param name="error"></param>
        /// <param name="suppressMessage"></param>
        public void LogError(StringBuilder error, bool suppressMessage = false)
        {
            if(!suppressMessage) MessageBox.Show(error.ToString());
            _logFromErrors.Append(error);
            _logFromErrors.AppendLine("");
        }

        #region Dump logs
        /// <summary>
        /// Method to build and write the log file. Includes both cases where changes were and were not made to the database
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public bool Dump()
        {
            string type;
            if (planType == PlanType.VMAT_TBI) type = "TBI";
            else type = "CSI";

            if(string.IsNullOrEmpty(logPath))
            {
                LogError("Log file path not set during script configuration! Please select a folder to write the log file!");
                FolderBrowserDialog FBD = new FolderBrowserDialog
                {
                    SelectedPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                };
                if (FBD.ShowDialog() == DialogResult.OK)
                {
                    logPath = FBD.SelectedPath;
                }
                else return true;
            }

            logPath += "\\preparation\\" + type + "\\" + mrn + "\\";
            string fileName = logPath + mrn + ".txt";
            if (!changesSaved && opType != ScriptOperationType.ExportCT)
            {
                logPath += "unsaved" + "\\";
                fileName = logPath + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt";
            }
            else if(opType != ScriptOperationType.General)
            {
                logPath += "MISC" + "\\";
                fileName = logPath + opType.ToString() + ".txt";
            }

            if (!Directory.Exists(logPath))
            {
                Directory.CreateDirectory(logPath);
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(DateTime.Now.ToString());
            sb.AppendLine($"User={userId}");
            sb.AppendLine($"Patient={mrn}");
            sb.AppendLine($"Plan type={planType}");
            //consider the operation that was performed when constructing the log file
            if (opType == ScriptOperationType.General) sb.Append(BuildGeneralOpLog());
            else if (opType == ScriptOperationType.GeneratePrelimTargets) sb.Append(BuildPrelimTargetOpLog());
            else sb.AppendLine("");

            sb.AppendLine("Errors and warnings:");
            sb.Append(_logFromErrors);
            sb.AppendLine("");

            sb.AppendLine("Detailed log output:");
            sb.Append(_logFromOperations);
            sb.AppendLine("");
            try
            {
                File.WriteAllText(fileName, sb.ToString());
            }
            catch (Exception e) 
            { 
                throw new Exception(e.Message); 
            }
            return false;
        }

        /// <summary>
        /// Method to build the log file for a normal, general run of the preparation script where the focus was to prepare the structure
        /// set for optimization
        /// </summary>
        /// <returns></returns>
        private StringBuilder BuildGeneralOpLog()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Structure set={selectedSS}");
            sb.AppendLine($"Template={template}");
            sb.AppendLine("");
            sb.AppendLine("Prescriptions:");
            foreach (Tuple<string, string, int, DoseValue, double> itr in prescriptions) sb.AppendLine($"    {{{itr.Item1},{itr.Item2},{itr.Item3},{itr.Item4.Dose},{itr.Item5}}}");
            sb.AppendLine("");

            sb.AppendLine("Added TS structures:");
            foreach (string itr in addedStructures) sb.AppendLine($"    {itr}");
            sb.AppendLine("");

            sb.AppendLine("Structure manipulations:");
            foreach (Tuple<string, TSManipulationType, double> itr in structureManipulations) sb.AppendLine($"    {{{itr.Item1},{itr.Item2},{itr.Item3}}}");
            sb.AppendLine("");

            sb.AppendLine("Isocenter names:");
            foreach (Tuple<string, List<string>> itr in isoNames)
            {
                sb.AppendLine($"    {itr.Item1}");
                foreach (string s in itr.Item2)
                {
                    sb.AppendLine($"        {s}");
                }
            }
            sb.AppendLine("");

            sb.AppendLine("Plan UIDs:");
            foreach (string itr in planUIDs)
            {
                sb.AppendLine($"    {itr}");
            }
            sb.AppendLine("");

            sb.AppendLine("TS Targets:");
            foreach (Tuple<string, string> itr in tsTargets)
            {
                sb.AppendLine($"    {{{itr.Item1},{itr.Item2}}}");
            }
            sb.AppendLine("");

            sb.AppendLine("Normalization volumes:");
            foreach (Tuple<string, string> itr in normVolumes)
            {
                sb.AppendLine($"    {{{itr.Item1},{itr.Item2}}}");
            }
            sb.AppendLine("");

            sb.AppendLine("Optimization constraints:");
            foreach (Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>> itr in optimizationConstraints)
            {
                sb.AppendLine($"    {itr.Item1}");
                foreach (Tuple<string, OptimizationObjectiveType, double, double, int> itr1 in itr.Item2)
                {
                    sb.AppendLine($"        {{{itr1.Item1},{itr1.Item2},{itr1.Item3},{itr1.Item4},{itr1.Item5}}}");
                }
            }
            sb.AppendLine("");
            return sb;
        }

        /// <summary>
        /// Method to build the log file when the script was only used to generate the preliminary targets
        /// </summary>
        /// <returns></returns>
        private StringBuilder BuildPrelimTargetOpLog()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Added preliminary targets:");
            foreach (string itr in addedPrelimTargets) sb.AppendLine("    " + itr);
            sb.AppendLine("");
            return sb;
        }
        #endregion
    }
}
