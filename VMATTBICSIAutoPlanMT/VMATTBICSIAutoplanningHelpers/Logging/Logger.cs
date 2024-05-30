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
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpers.Logging
{
    public class Logger
    {
        #region Set methods
        //general patient info
        public string MRN { set { mrn = value; } }
        public string Template { set => template = value; }
        public string StructureSet { set => selectedSS = value; }
        public bool ChangesSaved { set => changesSaved = value; }
        public string User { set => userId = value; }
        public string LogPath { get { return logPath; } set { logPath = value; } }
        //plan ID, target Id, numFx, dosePerFx, cumulative dose
        public List<Prescription> Prescriptions { set => prescriptions = new List<Prescription>(value); }
        public List<string> AddedPrelimTargetsStructures { set => addedPrelimTargets = new List<string>(value); }
        //ts generation and manipulation
        public List<string> AddedStructures { set => addedStructures = new List<string>(value); }
        public List<RequestedTSManipulation> StructureManipulations { get; set; } = new List<RequestedTSManipulation>();
     
        //plan id, list<original target id, ts target id>
        public Dictionary<string, string> TSTargets { set => tsTargets = new Dictionary<string, string>(value); }
        //plan id, normalization volume for plan
        public Dictionary<string,string> NormalizationVolumes { set => normVolumes = new Dictionary<string,string>(value); }
        //plan Id, list of isocenter names for this plan
        public List<PlanIsocenters> IsoNames { set => isoNames = new List<PlanIsocenters>(value); }
        //plan generation and beam placement
        public List<string> PlanUIDs { set => planUIDs = new List<string>(value); }
        //optimization setup
        //plan ID, <structure, constraint type, dose cGy, volume %, priority>
        public List<PlanOptimizationSetup> OptimizationConstraints { get; set; } = new List<PlanOptimizationSetup>();
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
        List<Prescription> prescriptions;
        private List<string> addedPrelimTargets;
        private List<string> addedStructures;
        private Dictionary<string, string> tsTargets;
        private Dictionary<string, string> normVolumes;
        private List<PlanIsocenters> isoNames;
        private List<string> planUIDs;
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
            prescriptions = new List<Prescription> { };
            addedPrelimTargets = new List<string> { };
            addedStructures = new List<string> { };
            tsTargets = new Dictionary<string, string> { };
            normVolumes = new Dictionary<string, string> { };
            isoNames = new List<PlanIsocenters> { };
            planUIDs = new List<string> { };
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
            foreach (Prescription itr in prescriptions) sb.AppendLine($"    {{{itr.PlanId},{itr.TargetId},{itr.NumberOfFractions},{itr.DoseValue.Dose},{itr.CumulativeDoseToTarget}}}");
            sb.AppendLine("");

            sb.AppendLine("Added TS structures:");
            foreach (string itr in addedStructures) sb.AppendLine($"    {itr}");
            sb.AppendLine("");

            sb.AppendLine("Structure manipulations:");
            foreach (RequestedTSManipulation itr in StructureManipulations) sb.AppendLine($"    {{{itr.StructureId},{itr.ManipulationType},{itr.MarginInCM}}}");
            sb.AppendLine("");

            sb.AppendLine("Isocenter names:");
            foreach (PlanIsocenters itr in isoNames)
            {
                sb.AppendLine($"    {itr.PlanId}");
                foreach (string s in itr.IsocenterIds)
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
            foreach (KeyValuePair<string,string> itr in tsTargets)
            {
                sb.AppendLine($"    {{{itr.Key},{itr.Value}}}");
            }
            sb.AppendLine("");

            sb.AppendLine("Normalization volumes:");
            foreach (KeyValuePair<string, string> itr in normVolumes)
            {
                sb.AppendLine($"    {{{itr.Key},{itr.Value}}}");
            }
            sb.AppendLine("");

            sb.AppendLine("Optimization constraints:");
            foreach (PlanOptimizationSetup itr in OptimizationConstraints)
            {
                sb.AppendLine($"    {itr.PlanId}");
                foreach (OptimizationConstraint itr1 in itr.OptimizationConstraints)
                {
                    sb.AppendLine($"        {{{itr1.StructureId},{itr1.ConstraintType},{itr1.QueryDose},{itr1.QueryVolume},{itr1.Priority}}}");
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
