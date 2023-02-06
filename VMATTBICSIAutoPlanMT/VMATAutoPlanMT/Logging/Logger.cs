using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATAutoPlanMT.Logging
{
    class Logger
    {
        string logPath = "";
        //general patient info
        public string mrn { get; set; }
        public string planType { get; set; }
        public string template { get; set; }
        public string selectedSS { get; set; }

        //targets and prescription
        //structure ID, Rx dose, plan Id
        public List<Tuple<string, double, string>> targets { get; set; }
        //plan ID, target Id, numFx, dosePerFx, cumulative dose
        public List<Tuple<string, string, int, DoseValue, double>> prescriptions { get; set; }

        //ts generation and manipulation
        public List<string> addedStructures { get; set; }
        //structure ID, sparing type, margin
        public List<Tuple<string, string, double>> structureManipulations { get; set; }
        //plan Id, list of isocenter names for this plan
        public List<Tuple<string, List<string>>> isoNames { get; set; }

        //plan generation and beam placement
        public List<string> planUIDs { get; set; }

        //optimization setup
        //plan ID, <structure, constraint type, dose cGy, volume %, priority>
        public List<Tuple<string, List<Tuple<string, string, double, double, int>>>> optimizationConstraints { get; set; }
        
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
            isoNames = new List<Tuple<string, List<string>>> { };
            planUIDs = new List<string> { };
            optimizationConstraints = new List<Tuple<string, List<Tuple<string, string, double, double, int>>>> { };
        }

        public bool Dump()
        {
            if (!Directory.Exists(logPath + "\\preparation\\")) Directory.CreateDirectory(logPath + "\\preparation\\");
            string fileName = logPath + "\\preparation\\" + mrn + ".txt";

            StringBuilder sb = new StringBuilder();
            sb.Append(String.Format(DateTime.Now.ToString()));
            sb.Append(Environment.NewLine);
            sb.Append(String.Format("patient={0}", mrn));
            sb.Append(Environment.NewLine);

            sb.Append(String.Format("plan type={0}", planType));
            sb.Append(Environment.NewLine);
            sb.Append(String.Format("structure set={0}", selectedSS));
            sb.Append(Environment.NewLine);

            sb.Append(String.Format("template={0}", template));
            sb.Append(Environment.NewLine);

            sb.Append(String.Format(""));
            sb.Append(Environment.NewLine);

            sb.Append(String.Format("prescriptions:"));
            sb.Append(Environment.NewLine);

            foreach (Tuple<string, string, int, DoseValue, double> itr in prescriptions) sb.Append(String.Format("    {{{0},{1},{2},{3},{4}}}",itr.Item1,itr.Item2,itr.Item3,itr.Item4,itr.Item5) + Environment.NewLine);
            sb.Append(String.Format(""));
            sb.Append(Environment.NewLine);

            sb.Append(String.Format("Added structures:"));
            sb.Append(Environment.NewLine);

            foreach (string itr in addedStructures) sb.Append("    " + itr + Environment.NewLine);
            sb.Append(String.Format(""));
            sb.Append(Environment.NewLine);

            sb.Append(String.Format("Structure manipulations:"));
            sb.Append(Environment.NewLine);

            foreach (Tuple<string, string, double> itr in structureManipulations) sb.Append(String.Format("    {{{0},{1},{2}}}", itr.Item1, itr.Item2, itr.Item3) + Environment.NewLine);
            sb.Append(String.Format(""));
            sb.Append(Environment.NewLine);

            sb.Append(String.Format("isocenter names:"));
            sb.Append(Environment.NewLine);

            foreach (Tuple<string, List<string>> itr in isoNames)
            {
                sb.Append(String.Format("    {0}", itr.Item1) + Environment.NewLine);
                foreach(string s in itr.Item2)
                {
                    sb.Append(String.Format("        {0}", s) + Environment.NewLine);
                }
            }
            sb.Append(String.Format(""));
            sb.Append(Environment.NewLine);

            sb.Append(String.Format("plan UIDs:"));
            sb.Append(Environment.NewLine);

            foreach (string itr in planUIDs)
            {
                sb.Append(String.Format("    {0}", itr) + Environment.NewLine);
            }
            sb.Append(String.Format(""));
            sb.Append(Environment.NewLine);

            sb.Append(String.Format("Optimization constraints:"));
            sb.Append(Environment.NewLine);

            foreach (Tuple<string, List<Tuple<string, string, double, double, int>>> itr in optimizationConstraints)
            {
                sb.Append(String.Format("    {0}", itr.Item1) + Environment.NewLine);
                foreach(Tuple<string, string, double, double, int> itr1 in itr.Item2)
                {
                    sb.Append(String.Format("        {{{0},{1},{2},{3},{4}}}", itr1.Item1, itr1.Item2, itr1.Item3, itr1.Item4, itr1.Item5) + Environment.NewLine);
                }
            }
            try
            {
                File.WriteAllText(fileName, sb.ToString());
            }
            catch (Exception e) { throw new Exception(e.Message); }
            return false;
        }
    }
}
