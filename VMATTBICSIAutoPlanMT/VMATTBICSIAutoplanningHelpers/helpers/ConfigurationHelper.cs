using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VMATTBICSIAutoplanningHelpers.TemplateClasses;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoplanningHelpers.helpers
{
    public class ConfigurationHelper
    {
        public CSIAutoPlanTemplate readTemplatePlan(string file, int count)
        {
            CSIAutoPlanTemplate tempTemplate = new CSIAutoPlanTemplate(count);
            using (StreamReader reader = new StreamReader(file))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrEmpty(line) && line.Substring(0, 1) != "%")
                    {
                        if (line.Equals(":begin template case configuration:"))
                        {
                            //preparation
                            List<Tuple<string, string, double>> spareStruct_temp = new List<Tuple<string, string, double>> { };
                            List<Tuple<string, string>> TSstructures_temp = new List<Tuple<string, string>> { };
                            List<Tuple<string, string, double, double, int>> initOptConst_temp = new List<Tuple<string, string, double, double, int>> { };
                            List<Tuple<string, string, double, double, int>> bstOptConst_temp = new List<Tuple<string, string, double, double, int>> { };
                            List<Tuple<string, double, string>> targets_temp = new List<Tuple<string, double, string>> { };
                            //optimization loop
                            List<Tuple<string, string, double, double, DoseValuePresentation>> planObj_temp = new List<Tuple<string, string, double, double, DoseValuePresentation>> { };
                            List<Tuple<string, string, double, string>> planDoseInfo_temp = new List<Tuple<string, string, double, string>> { };
                            List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> requestedTSstructures_temp = new List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> { };
                            //parse the data specific to the myeloablative case setup
                            while (!(line = reader.ReadLine()).Equals(":end template case configuration:"))
                            {
                                if (line.Substring(0, 1) != "%")
                                {
                                    if (line.Contains("="))
                                    {
                                        string parameter = line.Substring(0, line.IndexOf("="));
                                        string value = line.Substring(line.IndexOf("=") + 1, line.Length - line.IndexOf("=") - 1);
                                        if (parameter == "template name") tempTemplate.templateName = value;
                                        else if (parameter == "initial dose per fraction") { if (double.TryParse(value, out double initDPF)) tempTemplate.initialRxDosePerFx = initDPF; }
                                        else if (parameter == "initial num fx") { if (int.TryParse(value, out int initFx)) tempTemplate.initialRxNumFx = initFx; }
                                        else if (parameter == "boost dose per fraction") { if (double.TryParse(value, out double bstDPF)) tempTemplate.boostRxDosePerFx = bstDPF; }
                                        else if (parameter == "boost num fx") { if (int.TryParse(value, out int bstFx)) tempTemplate.boostRxNumFx = bstFx; }
                                    }
                                    else if (line.Contains("add sparing structure")) spareStruct_temp.Add(parseSparingStructure(line));
                                    else if (line.Contains("add init opt constraint")) initOptConst_temp.Add(parseOptimizationConstraint(line));
                                    else if (line.Contains("add boost opt constraint")) bstOptConst_temp.Add(parseOptimizationConstraint(line));
                                    else if (line.Contains("add TS")) TSstructures_temp.Add(parseTS(line));
                                    else if (line.Contains("add target")) targets_temp.Add(parseTargets(line));
                                    else if (line.Contains("add optimization TS structure")) requestedTSstructures_temp.Add(ParseTSstructure(line));
                                    else if (line.Contains("add plan objective")) planObj_temp.Add(ParsePlanObjective(line));
                                    else if (line.Contains("add plan dose info")) planDoseInfo_temp.Add(ParseRequestedPlanDoseInfo(line));
                                }
                            }

                            if(spareStruct_temp.Any()) tempTemplate.spareStructures = new List<Tuple<string, string, double>>(spareStruct_temp);
                            if(TSstructures_temp.Any()) tempTemplate.TS_structures = new List<Tuple<string, string>>(TSstructures_temp);
                            if(initOptConst_temp.Any()) tempTemplate.init_constraints = new List<Tuple<string, string, double, double, int>>(initOptConst_temp);
                            if(bstOptConst_temp.Any()) tempTemplate.bst_constraints = new List<Tuple<string, string, double, double, int>>(bstOptConst_temp);
                            if(targets_temp.Any()) tempTemplate.targets = new List<Tuple<string, double, string>>(targets_temp);
                            if(planObj_temp.Any()) tempTemplate.planObj = new List<Tuple<string, string, double, double, DoseValuePresentation>>(planObj_temp);
                            if(requestedTSstructures_temp.Any()) tempTemplate.requestedTSstructures = new List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>>(requestedTSstructures_temp);
                            if(planDoseInfo_temp.Any()) tempTemplate.planDoseInfo = new List<Tuple<string, string, double, string>>(planDoseInfo_temp);
                        }
                    }
                }
                reader.Close();
            }
            return tempTemplate;
        }

        //very useful helper method to remove everything in the input string 'line' up to a given character 'cropChar'
        public string cropLine(string line, string cropChar) { return line.Substring(line.IndexOf(cropChar) + 1, line.Length - line.IndexOf(cropChar) - 1); }

        public Tuple<string, string> parseTS(string line)
        {
            //known array format --> can take shortcuts in parsing the data
            //structure id, sparing type, added margin in cm (ignored if sparing type is Dmax ~ Rx Dose)
            string dicomType;
            string TSstructure;
            line = cropLine(line, "{");
            dicomType = line.Substring(0, line.IndexOf(","));
            line = cropLine(line, ",");
            TSstructure = line.Substring(0, line.IndexOf("}"));
            return Tuple.Create(dicomType, TSstructure);
        }

        public Tuple<string, double, string> parseTargets(string line)
        {
            //known array format --> can take shortcuts in parsing the data
            //structure id, sparing type, added margin in cm (ignored if sparing type is Dmax ~ Rx Dose)
            string structure;
            string planId;
            double val = 0.0;
            line = cropLine(line, "{");
            structure = line.Substring(0, line.IndexOf(","));
            line = cropLine(line, ",");
            val = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = cropLine(line, ",");
            planId = line.Substring(0, line.IndexOf("}"));
            return Tuple.Create(structure, val, planId);
        }

        public Tuple<string, string, double> parseSparingStructure(string line)
        {
            //known array format --> can take shortcuts in parsing the data
            //structure id, sparing type, added margin in cm (ignored if sparing type is Dmax ~ Rx Dose)
            string structure;
            string spareType;
            double val = 0.0;
            line = cropLine(line, "{");
            structure = line.Substring(0, line.IndexOf(","));
            line = cropLine(line, ",");
            spareType = line.Substring(0, line.IndexOf(","));
            line = cropLine(line, ",");
            val = double.Parse(line.Substring(0, line.IndexOf("}")));
            return Tuple.Create(structure, spareType, val);
        }

        public Tuple<string, string, double, double, int> parseOptimizationConstraint(string line)
        {
            //known array format --> can take shortcuts in parsing the data
            //structure id, constraint type, dose (cGy), volume (%), priority
            string structure;
            string constraintType;
            double doseVal;
            double volumeVal;
            int priorityVal;
            line = cropLine(line, "{");
            structure = line.Substring(0, line.IndexOf(","));
            line = cropLine(line, ",");
            constraintType = line.Substring(0, line.IndexOf(","));
            line = cropLine(line, ",");
            doseVal = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = cropLine(line, ",");
            volumeVal = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = cropLine(line, ",");
            priorityVal = int.Parse(line.Substring(0, line.IndexOf("}")));
            return Tuple.Create(structure, constraintType, doseVal, volumeVal, priorityVal);
        }

        public Tuple<string, string, double, string> ParseRequestedPlanDoseInfo(string line)
        {
            line = cropLine(line, "{");
            string structure = line.Substring(0, line.IndexOf(","));
            line = cropLine(line, ",");
            string constraintType;
            double doseVal = 0.0;
            string representation;
            string constraintTypeTmp = line.Substring(0, line.IndexOf(","));
            if (line.Substring(0, 1) == "D")
            {
                //only add for final optimization (i.e., one more optimization requested where current calculated dose is used as intermediate)
                if (constraintTypeTmp.Contains("max")) constraintType = "Dmax";
                else if (constraintTypeTmp.Contains("min")) constraintType = "Dmin";
                else
                {
                    constraintType = "D";
                    constraintTypeTmp = cropLine(constraintTypeTmp, "D");
                    doseVal = double.Parse(constraintTypeTmp);
                }
            }
            else
            {
                constraintType = "V";
                constraintTypeTmp = cropLine(constraintTypeTmp, "V");
                doseVal = double.Parse(constraintTypeTmp);
            }
            line = cropLine(line, ",");
            if (line.Contains("Relative")) representation = "Relative";
            else representation = "Absolute";

            return Tuple.Create(structure, constraintType, doseVal, representation);
        }

        public Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>> ParseTSstructure(string line)
        {
            //type (Dmax or V), dose value for volume constraint (N/A for Dmax), equality or inequality, volume (%) or dose (%)
            List<Tuple<string, double, string, double>> constraints = new List<Tuple<string, double, string, double>> { };
            string structure = "";
            double lowDoseLevel = 0.0;
            double upperDoseLevel = 0.0;
            double volumeVal = 0.0;
            int priority = 0;
            try
            {
                line = cropLine(line, "{");
                structure = line.Substring(0, line.IndexOf(","));
                line = cropLine(line, ",");
                lowDoseLevel = double.Parse(line.Substring(0, line.IndexOf(",")));
                line = cropLine(line, ",");
                upperDoseLevel = double.Parse(line.Substring(0, line.IndexOf(",")));
                line = cropLine(line, ",");
                volumeVal = double.Parse(line.Substring(0, line.IndexOf(",")));
                line = cropLine(line, ",");
                priority = int.Parse(line.Substring(0, line.IndexOf(",")));
                line = cropLine(line, "{");

                while (!string.IsNullOrEmpty(line) && line.Substring(0, 1) != "}")
                {
                    string constraintType = "";
                    double doseVal = 0.0;
                    string inequality = "";
                    double queryVal = 0.0;
                    if (line.Substring(0, 1) == "f")
                    {
                        //only add for final optimization (i.e., one more optimization requested where current calculated dose is used as intermediate)
                        constraintType = "finalOpt";
                        if (!line.Contains(",")) line = cropLine(line, "}");
                        else line = cropLine(line, ",");
                    }
                    else
                    {
                        if (line.Substring(0, 1) == "V")
                        {
                            constraintType = "V";
                            line = cropLine(line, "V");
                            int index = 0;
                            while (line.ElementAt(index).ToString() != ">" && line.ElementAt(index).ToString() != "<") index++;
                            doseVal = double.Parse(line.Substring(0, index));
                            line = line.Substring(index, line.Length - index);
                        }
                        else
                        {
                            constraintType = "Dmax";
                            line = cropLine(line, "x");
                        }
                        inequality = line.Substring(0, 1);

                        if (!line.Contains(",")) { queryVal = double.Parse(line.Substring(1, line.IndexOf("}") - 1)); line = cropLine(line, "}"); }
                        else
                        {
                            queryVal = double.Parse(line.Substring(1, line.IndexOf(",") - 1));
                            line = cropLine(line, ",");
                        }
                    }
                    constraints.Add(Tuple.Create(constraintType, doseVal, inequality, queryVal));
                }

                return Tuple.Create(structure, lowDoseLevel, upperDoseLevel, volumeVal, priority, new List<Tuple<string, double, string, double>>(constraints));
            }
            catch (Exception e) { MessageBox.Show(String.Format("Error could not parse TS structure: {0}\nBecause: {1}", line, e.Message)); return Tuple.Create("", 0.0, 0.0, 0.0, 0, new List<Tuple<string, double, string, double>> { }); }
        }

        public Tuple<string, string, double, double, DoseValuePresentation> ParsePlanObjective(string line)
        {
            string structure = "";
            string constraintType = "";
            double doseVal = 0.0;
            double volumeVal = 0.0;
            DoseValuePresentation dvp;
            line = cropLine(line, "{");
            structure = line.Substring(0, line.IndexOf(","));
            line = cropLine(line, ",");
            constraintType = line.Substring(0, line.IndexOf(","));
            line = cropLine(line, ",");
            doseVal = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = cropLine(line, ",");
            volumeVal = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = cropLine(line, ",");
            if (line.Contains("Relative")) dvp = DoseValuePresentation.Relative;
            else dvp = DoseValuePresentation.Absolute;
            return Tuple.Create(structure, constraintType, doseVal, volumeVal, dvp);
        }

    }
}
