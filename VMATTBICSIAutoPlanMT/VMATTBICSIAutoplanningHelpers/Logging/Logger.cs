using System;
using System.Collections.Generic;
using System.Windows;
using System.Text;
using System.IO;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoplanningHelpers.Logging
{
    public class Logger
    {
        //general patient info
        public string MRN { set { mrn = value; }  }
        public string PlanType { set { planType = value; } }
        public string Template { set { template = value; } }
        public string StructureSet { set { selectedSS = value; } }
        public bool ChangesSaved { set { changesSaved = value; } }
        public string User { set { userId = value; } }
        //targets and prescription
        //structure ID, Rx dose, plan Id
        public List<Tuple<string, double, string>> Targets { set { targets = new List<Tuple<string, double, string>>(value); } }
        //plan ID, target Id, numFx, dosePerFx, cumulative dose
        public List<Tuple<string, string, int, DoseValue, double>> Prescriptions { set { prescriptions = new List<Tuple<string, string, int, DoseValue, double>>(value); } }
        //ts generation and manipulation
        public List<string> AddedStructures { set { addedStructures = new List<string>(value); } }
        //structure ID, sparing type, margin
        public List<Tuple<string, string, double>> StructureManipulations { set { structureManipulations = new List<Tuple<string, string, double>>(value); } }
        //plan id, normalization volume for plan
        public List<Tuple<string, string>> NormalizationVolumes { set { normVolumes = new List<Tuple<string, string>>(value); } }
        //plan Id, list of isocenter names for this plan
        public List<Tuple<string, List<string>>> IsoNames { set { isoNames = new List<Tuple<string, List<string>>>(value); } }
        //plan generation and beam placement
        public List<string> PlanUIDs { set { planUIDs = new List<string>(value); } }
        //optimization setup
        //plan ID, <structure, constraint type, dose cGy, volume %, priority>
        public List<Tuple<string, List<Tuple<string, string, double, double, int>>>> OptimizationConstraints { set { optimizationConstraints = new List<Tuple<string, List<Tuple<string, string, double, double, int>>>>(value); } }

        //path to location to write log file
        private string logPath = "";
        //stringbuilder object to log output from ts generation/manipulation and beam placement
        private StringBuilder _logFromOperations;
        private StringBuilder _logFromErrors;
        private string userId;
        private string mrn;
        private string planType;
        private string template;
        private string selectedSS;
        bool changesSaved = false;
        public List<Tuple<string, double, string>> targets;
        List<Tuple<string, string, int, DoseValue, double>> prescriptions;
        private List<string> addedStructures;
        private List<Tuple<string, string, double>> structureManipulations { get; set; }
        private List<Tuple<string, string>> normVolumes { get; set; }
        private List<Tuple<string, List<string>>> isoNames { get; set; }
        private List<string> planUIDs { get; set; }
        private List<Tuple<string, List<Tuple<string, string, double, double, int>>>> optimizationConstraints { get; set; }

        public Logger(string path, string type, string patient)
        {
            logPath = path;
            planType = type;
            mrn = patient;

            selectedSS = "";
            targets = new List<Tuple<string, double, string>> { };
            prescriptions = new List<Tuple<string, string, int, DoseValue, double>> { };
            addedStructures = new List<string> { };
            structureManipulations = new List<Tuple<string, string, double>> { };
            normVolumes = new List<Tuple<string, string>> { };
            isoNames = new List<Tuple<string, List<string>>> { };
            planUIDs = new List<string> { };
            optimizationConstraints = new List<Tuple<string, List<Tuple<string, string, double, double, int>>>> { };
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
            string type = "CSI";
            if (planType.Contains("TBI")) type = "TBI";

            logPath += "\\preparation\\" + type + "\\" + mrn + "\\";
            string fileName = logPath + mrn + ".txt";
            if (!changesSaved)
            {
                logPath += "unsaved" + "\\";
                fileName = logPath + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt";
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
            sb.AppendLine(String.Format("Structure set={0}", selectedSS));
            sb.AppendLine(String.Format("Template={0}", template));
            sb.AppendLine(String.Format(""));
            sb.AppendLine(String.Format("Prescriptions:"));

            foreach (Tuple<string, string, int, DoseValue, double> itr in prescriptions) sb.AppendLine(String.Format("    {{{0},{1},{2},{3},{4}}}",itr.Item1,itr.Item2,itr.Item3,itr.Item4.Dose,itr.Item5));
            sb.AppendLine(String.Format(""));

            sb.AppendLine(String.Format("Added structures:"));
            foreach (string itr in addedStructures) sb.AppendLine("    " + itr);
            sb.AppendLine(String.Format(""));

            sb.AppendLine(String.Format("Structure manipulations:"));
            foreach (Tuple<string, string, double> itr in structureManipulations) sb.AppendLine(String.Format("    {{{0},{1},{2}}}", itr.Item1, itr.Item2, itr.Item3));
            sb.AppendLine(String.Format(""));

            sb.AppendLine(String.Format("Isocenter names:"));
            foreach (Tuple<string, List<string>> itr in isoNames)
            {
                sb.AppendLine(String.Format("    {0}", itr.Item1));
                foreach(string s in itr.Item2)
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

            sb.AppendLine("Normalization volumes:");
            foreach(Tuple<string,string> itr in normVolumes)
            {
                sb.AppendLine(String.Format("    {{{0},{1}}}", itr.Item1, itr.Item2));
            }
            sb.AppendLine(String.Format(""));

            sb.AppendLine(String.Format("Optimization constraints:"));
            foreach (Tuple<string, List<Tuple<string, string, double, double, int>>> itr in optimizationConstraints)
            {
                sb.AppendLine(String.Format("    {0}", itr.Item1));
                foreach(Tuple<string, string, double, double, int> itr1 in itr.Item2)
                {
                    sb.AppendLine(String.Format("        {{{0},{1},{2},{3},{4}}}", itr1.Item1, itr1.Item2, itr1.Item3, itr1.Item4, itr1.Item5));
                }
            }
            sb.AppendLine(String.Format(""));

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
    }
}
