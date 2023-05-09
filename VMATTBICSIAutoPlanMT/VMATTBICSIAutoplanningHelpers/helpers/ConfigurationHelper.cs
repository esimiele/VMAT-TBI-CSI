using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.PlanTemplateClasses;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class ConfigurationHelper
    {
        public static CSIAutoPlanTemplate ReadTemplatePlan(string file, int count)
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
                            List<Tuple<string, TSManipulationType, double>> TSManipulation_temp = new List<Tuple<string, TSManipulationType, double>> { };
                            List<Tuple<string, string>> TSstructures_temp = new List<Tuple<string, string>> { };
                            List<Tuple<string, double, double, double>> createRings_temp = new List<Tuple<string, double, double, double>> { };
                            List<Tuple<string, OptimizationObjectiveType, double, double, int>> initOptConst_temp = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
                            List<Tuple<string, OptimizationObjectiveType, double, double, int>> bstOptConst_temp = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
                            List<Tuple<string, double, string>> targets_temp = new List<Tuple<string, double, string>> { };
                            List<string> cropAndContourOverlapStructures_temp = new List<string> { };
                            //optimization loop
                            List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> planObj_temp = new List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> { };
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
                                        if (parameter == "template name") tempTemplate.SetTemplateName(value);
                                        else if (parameter == "initial dose per fraction")
                                        {
                                            if (double.TryParse(value, out double initDPF)) tempTemplate.SetInitRxDosePerFx(initDPF);
                                        }
                                        else if (parameter == "initial num fx")
                                        {
                                            if (int.TryParse(value, out int initFx)) tempTemplate.SetInitialRxNumFx(initFx);
                                        }
                                        else if (parameter == "boost dose per fraction")
                                        {
                                            if (double.TryParse(value, out double bstDPF)) tempTemplate.SetBoostRxDosePerFx(bstDPF);
                                        }
                                        else if (parameter == "boost num fx")
                                        {
                                            if (int.TryParse(value, out int bstFx)) tempTemplate.SetBoostRxNumFx(bstFx);
                                        }
                                    }
                                    else if (line.Contains("add TS manipulation")) TSManipulation_temp.Add(ParseTSManipulation(line));
                                    else if (line.Contains("create ring")) createRings_temp.Add(ParseCreateRing(line));
                                    else if (line.Contains("crop and contour")) cropAndContourOverlapStructures_temp.Add(ParseCropAndContourOverlapStruct(line));
                                    else if (line.Contains("add init opt constraint")) initOptConst_temp.Add(ParseOptimizationConstraint(line));
                                    else if (line.Contains("add boost opt constraint")) bstOptConst_temp.Add(ParseOptimizationConstraint(line));
                                    else if (line.Contains("create TS")) TSstructures_temp.Add(ParseCreateTS(line));
                                    else if (line.Contains("add target")) targets_temp.Add(ParseTargets(line));
                                    else if (line.Contains("add optimization TS structure")) requestedTSstructures_temp.Add(ParseTSstructure(line));
                                    else if (line.Contains("add plan objective")) planObj_temp.Add(ParsePlanObjective(line));
                                    else if (line.Contains("add plan dose info")) planDoseInfo_temp.Add(ParseRequestedPlanDoseInfo(line));
                                }
                            }

                            if(TSManipulation_temp.Any()) tempTemplate.SetTSManipulations(TSManipulation_temp);
                            if(createRings_temp.Any()) tempTemplate.SetCreateRings(createRings_temp);
                            if(cropAndContourOverlapStructures_temp.Any()) tempTemplate.SetCropAndOverlapStructures(cropAndContourOverlapStructures_temp);
                            if(TSstructures_temp.Any()) tempTemplate.SetCreateTSStructures(TSstructures_temp);
                            if(initOptConst_temp.Any()) tempTemplate.SetInitOptimizationConstraints(initOptConst_temp);
                            if(bstOptConst_temp.Any()) tempTemplate.SetBoostOptimizationConstraints(bstOptConst_temp);
                            if(targets_temp.Any()) tempTemplate.SetTargets(targets_temp);
                            if(planObj_temp.Any()) tempTemplate.SetPlanObjectives(planObj_temp);
                            if(requestedTSstructures_temp.Any()) tempTemplate.SetRequestedOptTSStructures(requestedTSstructures_temp);
                            if(planDoseInfo_temp.Any()) tempTemplate.SetRequestedPlanDoseInfo(planDoseInfo_temp);
                        }
                    }
                }
                reader.Close();
            }
            return tempTemplate;
        }

        //very useful helper method to remove everything in the input string 'line' up to a given character 'cropChar'
        public static string CropLine(string line, string cropChar) { return line.Substring(line.IndexOf(cropChar) + 1, line.Length - line.IndexOf(cropChar) - 1); }

        public static Tuple<string, string> ParseCreateTS(string line)
        {
            //known array format --> can take shortcuts in parsing the data
            //structure id, sparing type, added margin in cm (ignored if sparing type is Dmax ~ Rx Dose)
            string dicomType;
            string TSstructure;
            line = CropLine(line, "{");
            dicomType = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            TSstructure = line.Substring(0, line.IndexOf("}"));
            return Tuple.Create(dicomType, TSstructure);
        }

        private static Tuple<string, double, double, double> ParseCreateRing(string line)
        {
            string structure;
            double margin;
            double thickness;
            double dose;
            line = CropLine(line, "{");
            structure = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            margin = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = CropLine(line, ",");
            thickness = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = CropLine(line, ",");
            dose = double.Parse(line.Substring(0, line.IndexOf("}")));
            return Tuple.Create(structure, margin, thickness, dose);
        }

        private static Tuple<string, double, string> ParseTargets(string line)
        {
            //known array format --> can take shortcuts in parsing the data
            //structure id, sparing type, added margin in cm (ignored if sparing type is Dmax ~ Rx Dose)
            string structure;
            string planId;
            double val;
            line = CropLine(line, "{");
            structure = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            val = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = CropLine(line, ",");
            planId = line.Substring(0, line.IndexOf("}"));
            return Tuple.Create(structure, val, planId);
        }

        public static Tuple<string, TSManipulationType, double> ParseTSManipulation(string line)
        {
            //known array format --> can take shortcuts in parsing the data
            //structure id, sparing type, added margin in cm (ignored if sparing type is Dmax ~ Rx Dose)
            string structure;
            string spareType;
            double val;
            line = CropLine(line, "{");
            structure = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            spareType = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            val = double.Parse(line.Substring(0, line.IndexOf("}")));
            return Tuple.Create(structure, TSManipulationTypeHelper.GetTSManipulationType(spareType), val);
        }

        private static string ParseCropAndContourOverlapStruct(string line)
        {
            string structure;
            line = CropLine(line, "{");
            structure = line.Substring(0, line.IndexOf("}"));
            return structure;
        }

        private static Tuple<string, OptimizationObjectiveType, double, double, int> ParseOptimizationConstraint(string line)
        {
            //known array format --> can take shortcuts in parsing the data
            //structure id, constraint type, dose (cGy), volume (%), priority
            string structure;
            string constraintType;
            double doseVal;
            double volumeVal;
            int priorityVal;
            line = CropLine(line, "{");
            structure = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            constraintType = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            doseVal = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = CropLine(line, ",");
            volumeVal = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = CropLine(line, ",");
            priorityVal = int.Parse(line.Substring(0, line.IndexOf("}")));
            return Tuple.Create(structure, OptimizationTypeHelper.GetObjectiveType(constraintType), doseVal, volumeVal, priorityVal);
        }

        private static Tuple<string, string, double, string> ParseRequestedPlanDoseInfo(string line)
        {
            line = CropLine(line, "{");
            string structure = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
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
                    constraintTypeTmp = CropLine(constraintTypeTmp, "D");
                    doseVal = double.Parse(constraintTypeTmp);
                }
            }
            else
            {
                constraintType = "V";
                constraintTypeTmp = CropLine(constraintTypeTmp, "V");
                doseVal = double.Parse(constraintTypeTmp);
            }
            line = CropLine(line, ",");
            if (line.Contains("Relative")) representation = "Relative";
            else representation = "Absolute";

            return Tuple.Create(structure, constraintType, doseVal, representation);
        }

        private static Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>> ParseTSstructure(string line)
        {
            //type (Dmax or V), dose value for volume constraint (N/A for Dmax), equality or inequality, volume (%) or dose (%)
            List<Tuple<string, double, string, double>> constraints = new List<Tuple<string, double, string, double>> { };
            string structure;
            double lowDoseLevel;
            double upperDoseLevel;
            double volumeVal;
            int priority;
            try
            {
                line = CropLine(line, "{");
                structure = line.Substring(0, line.IndexOf(","));
                line = CropLine(line, ",");
                lowDoseLevel = double.Parse(line.Substring(0, line.IndexOf(",")));
                line = CropLine(line, ",");
                upperDoseLevel = double.Parse(line.Substring(0, line.IndexOf(",")));
                line = CropLine(line, ",");
                volumeVal = double.Parse(line.Substring(0, line.IndexOf(",")));
                line = CropLine(line, ",");
                priority = int.Parse(line.Substring(0, line.IndexOf(",")));
                line = CropLine(line, "{");

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
                        if (!line.Contains(",")) line = CropLine(line, "}");
                        else line = CropLine(line, ",");
                    }
                    else
                    {
                        if (line.Substring(0, 1) == "V")
                        {
                            constraintType = "V";
                            line = CropLine(line, "V");
                            int index = 0;
                            while (line.ElementAt(index).ToString() != ">" && line.ElementAt(index).ToString() != "<") index++;
                            doseVal = double.Parse(line.Substring(0, index));
                            line = line.Substring(index, line.Length - index);
                        }
                        else
                        {
                            constraintType = "Dmax";
                            line = CropLine(line, "x");
                        }
                        inequality = line.Substring(0, 1);

                        if (!line.Contains(",")) 
                        { 
                            queryVal = double.Parse(line.Substring(1, line.IndexOf("}") - 1)); 
                            line = CropLine(line, "}"); 
                        }
                        else
                        {
                            queryVal = double.Parse(line.Substring(1, line.IndexOf(",") - 1));
                            line = CropLine(line, ",");
                        }
                    }
                    constraints.Add(Tuple.Create(constraintType, doseVal, inequality, queryVal));
                }

                return Tuple.Create(structure, lowDoseLevel, upperDoseLevel, volumeVal, priority, new List<Tuple<string, double, string, double>>(constraints));
            }
            catch (Exception e) 
            {
                MessageBox.Show(String.Format("Error could not parse TS structure: {0}\nBecause: {1}", line, e.Message)); 
                return Tuple.Create("", 0.0, 0.0, 0.0, 0, new List<Tuple<string, double, string, double>> { }); 
            }
        }

        private static Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation> ParsePlanObjective(string line)
        {
            string structure;
            string constraintType;
            double doseVal;
            double volumeVal;
            DoseValuePresentation dvp;
            line = CropLine(line, "{");
            structure = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            constraintType = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            doseVal = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = CropLine(line, ",");
            volumeVal = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = CropLine(line, ",");
            if (line.Contains("Relative")) dvp = DoseValuePresentation.Relative;
            else dvp = DoseValuePresentation.Absolute;
            return Tuple.Create(structure, OptimizationTypeHelper.GetObjectiveType(constraintType), doseVal, volumeVal, dvp);
        }

        public static Tuple<string,string,int,DoseValue,double> ParsePrescriptionsFromLogFile(string line)
        {
            string planId;
            string targetId;
            int numFx;
            double dosePerFx;
            double RxDose;
            line = CropLine(line, "{");
            planId = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            targetId = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            numFx = int.Parse(line.Substring(0, line.IndexOf(",")));
            line = CropLine(line, ",");
            dosePerFx = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = CropLine(line, ",");
            RxDose = double.Parse(line.Substring(0, line.IndexOf("}")));
            return Tuple.Create(planId, targetId, numFx, new DoseValue(dosePerFx, DoseValue.DoseUnit.cGy), RxDose);
        }

        public static Tuple<string,string> ParseNormalizationVolumeFromLogFile(string line)
        {
            string planId;
            string volumeId;
            line = CropLine(line, "{");
            planId = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            volumeId = line.Substring(0, line.IndexOf("}"));
            return Tuple.Create(planId, volumeId);
        }
    }
}
