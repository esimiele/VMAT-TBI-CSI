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
        //general patient info
        public string MRN { set { mrn = value; } }
        public void SetPlanType(PlanType type) { planType = type; }
        public string Template { set => template = value; }
        public string StructureSet { set => selectedSS = value; }
        public bool ChangesSaved { set => changesSaved = value; }
        public string User { set => userId = value; }
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

        //called after TS generation and beam placement are performed to copy the immediate log output from MTProgress window to the log file
        public void AppendLogOutput(string info, StringBuilder s)
        {
            _logFromOperations.AppendLine(info);
            _logFromOperations.Append(s);
            _logFromOperations.AppendLine("");
        }

        public void AppendLogOutput(string info)
        {
            _logFromOperations.AppendLine(info);
            _logFromOperations.AppendLine("");
        }

        public void LogError(string error, bool suppressMessage = false)
        {
            if(!suppressMessage) MessageBox.Show(error);
            _logFromErrors.AppendLine(error);
            _logFromErrors.AppendLine("");
        }

        public void LogError(StringBuilder error, bool suppressMessage = false)
        {
            if(!suppressMessage) MessageBox.Show(error.ToString());
            _logFromErrors.Append(error);
            _logFromErrors.AppendLine("");
        }

        public bool Dump()
        {
            string type;
            if (planType == PlanType.VMAT_TBI) type = "TBI";
            else type = "CSI";

            if(string.IsNullOrEmpty(logPath))
            {
                MessageBox.Show("Log file path not set during script configuration! Please select a folder to writes the log file!");
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
            if (!changesSaved)
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
            sb.AppendLine(String.Format(DateTime.Now.ToString()));
            sb.AppendLine(String.Format("User={0}", userId));
            sb.AppendLine(String.Format("Patient={0}", mrn));
            sb.AppendLine(String.Format("Plan type={0}", planType));
            if (opType == ScriptOperationType.General) sb.Append(BuildGeneralOpLog());
            else if (opType == ScriptOperationType.GeneratePrelimTargets) sb.Append(BuildPrelimTargetOpLog());
            else sb.AppendLine("");

            sb.AppendLine("Errors and warnings:");
            sb.Append(_logFromErrors);
            sb.AppendLine(String.Format(""));

            sb.AppendLine("Detailed log output:");
            sb.Append(_logFromOperations);
            sb.AppendLine(String.Format(""));
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

        private StringBuilder BuildGeneralOpLog()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(String.Format("Structure set={0}", selectedSS));
            sb.AppendLine(String.Format("Template={0}", template));
            sb.AppendLine(String.Format(""));
            sb.AppendLine(String.Format("Prescriptions:"));
            foreach (Tuple<string, string, int, DoseValue, double> itr in prescriptions) sb.AppendLine(String.Format("    {{{0},{1},{2},{3},{4}}}", itr.Item1, itr.Item2, itr.Item3, itr.Item4.Dose, itr.Item5));
            sb.AppendLine(String.Format(""));

            sb.AppendLine(String.Format("Added TS structures:"));
            foreach (string itr in addedStructures) sb.AppendLine("    " + itr);
            sb.AppendLine(String.Format(""));

            sb.AppendLine(String.Format("Structure manipulations:"));
            foreach (Tuple<string, TSManipulationType, double> itr in structureManipulations) sb.AppendLine(String.Format("    {{{0},{1},{2}}}", itr.Item1, itr.Item2.ToString(), itr.Item3));
            sb.AppendLine(String.Format(""));

            sb.AppendLine(String.Format("Isocenter names:"));
            foreach (Tuple<string, List<string>> itr in isoNames)
            {
                sb.AppendLine(String.Format("    {0}", itr.Item1));
                foreach (string s in itr.Item2)
                {
                    sb.AppendLine(String.Format("        {0}", s));
                }
            }
            sb.AppendLine(String.Format(""));

            sb.AppendLine(String.Format("Plan UIDs:"));
            foreach (string itr in planUIDs)
            {
                sb.AppendLine(String.Format("    {0}", itr));
            }
            sb.AppendLine(String.Format(""));

            sb.AppendLine("TS Targets:");
            foreach (Tuple<string, string> itr in tsTargets)
            {
                sb.AppendLine(String.Format("    {{{0},{1}}}", itr.Item1, itr.Item2));
            }
            sb.AppendLine(String.Format(""));

            sb.AppendLine("Normalization volumes:");
            foreach (Tuple<string, string> itr in normVolumes)
            {
                sb.AppendLine(String.Format("    {{{0},{1}}}", itr.Item1, itr.Item2));
            }
            sb.AppendLine(String.Format(""));

            sb.AppendLine(String.Format("Optimization constraints:"));
            foreach (Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>> itr in optimizationConstraints)
            {
                sb.AppendLine(String.Format("    {0}", itr.Item1));
                foreach (Tuple<string, OptimizationObjectiveType, double, double, int> itr1 in itr.Item2)
                {
                    sb.AppendLine(String.Format("        {{{0},{1},{2},{3},{4}}}", itr1.Item1, itr1.Item2.ToString(), itr1.Item3, itr1.Item4, itr1.Item5));
                }
            }
            sb.AppendLine(String.Format(""));
            return sb;
        }

        private StringBuilder BuildPrelimTargetOpLog()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(String.Format("Added preliminary targets:"));
            foreach (string itr in addedPrelimTargets) sb.AppendLine("    " + itr);
            sb.AppendLine(String.Format(""));
            return sb;
        }
    }
}
