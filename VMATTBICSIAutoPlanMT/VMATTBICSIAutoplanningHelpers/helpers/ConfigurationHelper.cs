using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Models;
using VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers;
using VMATTBICSIAutoPlanningHelpers.PlanTemplateClasses;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class ConfigurationHelper
    {
        /// <summary>
        /// Set the default path for the log files (/bin/VMAT-TBI-CSI/logs)
        /// </summary>
        /// <returns></returns>
        public static string GetDefaultLogPath()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\logs\\";
        }

        /// <summary>
        /// Set the default path for the documentation files (/bin/VMAT-TBI-CSI/documentation)
        /// </summary>
        /// <returns></returns>
        public static string GetDefaultDocumentationPath()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\documentation\\";
        }

        /// <summary>
        /// Simple helper method to read the requested log file path from the log configuration file. Returns the parse path (empty string if it was unable to parse)
        /// </summary>
        /// <param name="configFile"></param>
        /// <returns></returns>
        public static string ReadLogPathFromConfigurationFile(string configFile)
        {
            string logPath = "";
            try
            {
                using (StreamReader reader = new StreamReader(configFile))
                {
                    //setup temporary vectors to hold the parsed data
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        //this line contains useful information (i.e., it is not a comment)
                        if (!string.IsNullOrEmpty(line) && line.Substring(0, 1) != "%")
                        {
                            //useful info on this line in the format of parameter=value
                            //parse parameter and value separately using '=' as the delimeter
                            if (line.Contains("="))
                            {
                                //default configuration parameters
                                string parameter = line.Substring(0, line.IndexOf("="));
                                string value = line.Substring(line.IndexOf("=") + 1, line.Length - line.IndexOf("=") - 1);
                                if (parameter == "log file path")
                                {
                                    if(!string.IsNullOrEmpty(value)) logPath = VerifyPathIntegrity(value);
                                }
                            }
                        }
                    }
                    reader.Close();
                }
            }
            //let the user know if the data parsing failed
            catch (Exception e)
            {
                MessageBox.Show($"Error could not load configuration file because: {e.Message}\n\nAssuming default parameters");
                MessageBox.Show(e.StackTrace);
            }
            return logPath;
        }

        /// <summary>
        /// Helper function to parse a template .ini file and build a new instance of CSIAutoPlanTemplate 
        /// </summary>
        /// <param name="file"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static CSIAutoPlanTemplate ReadCSITemplatePlan(string file, int count)
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
                            List<RequestedTSManipulationModel> TSManipulation_temp = new List<RequestedTSManipulationModel> { };
                            List<RequestedTSStructureModel> TSstructures_temp = new List<RequestedTSStructureModel> { };
                            List<TSRingStructureModel> createRings_temp = new List<TSRingStructureModel> { };
                            List<OptimizationConstraintModel> initOptConst_temp = new List<OptimizationConstraintModel> { };
                            List<OptimizationConstraintModel> bstOptConst_temp = new List<OptimizationConstraintModel> { };
                            List<PlanTargetsModel> targets_temp = new List<PlanTargetsModel> { };
                            List<string> cropAndContourOverlapStructures_temp = new List<string> { };
                            //optimization loop
                            List<PlanObjectiveModel> planObj_temp = new List<PlanObjectiveModel> { };
                            List<RequestedPlanMetricModel> planDoseInfo_temp = new List<RequestedPlanMetricModel> { };
                            List<RequestedOptimizationTSStructureModel> requestedTSstructures_temp = new List<RequestedOptimizationTSStructureModel> { };
                            //parse the data specific to the myeloablative case setup
                            while (!(line = reader.ReadLine()).Equals(":end template case configuration:"))
                            {
                                if (line.Substring(0, 1) != "%")
                                {
                                    if (line.Contains("="))
                                    {
                                        string parameter = line.Substring(0, line.IndexOf("="));
                                        string value = line.Substring(line.IndexOf("=") + 1, line.Length - line.IndexOf("=") - 1);
                                        if (parameter == "template name") tempTemplate.TemplateName = value;
                                        else if (parameter == "initial dose per fraction")
                                        {
                                            if (double.TryParse(value, out double initDPF)) tempTemplate.InitialRxDosePerFx = initDPF;
                                        }
                                        else if (parameter == "initial num fx")
                                        {
                                            if (int.TryParse(value, out int initFx)) tempTemplate.InitialRxNumberOfFractions = initFx;
                                        }
                                        else if (parameter == "boost dose per fraction")
                                        {
                                            if (double.TryParse(value, out double bstDPF)) tempTemplate.BoostRxDosePerFx = bstDPF;
                                        }
                                        else if (parameter == "boost num fx")
                                        {
                                            if (int.TryParse(value, out int bstFx)) tempTemplate.BoostRxNumberOfFractions = bstFx;
                                        }
                                    }
                                    else if (line.Contains("add TS manipulation")) TSManipulation_temp.Add(ParseTSManipulation(line));
                                    else if (line.Contains("create ring")) createRings_temp.Add(ParseCreateRing(line));
                                    else if (line.Contains("crop and contour")) cropAndContourOverlapStructures_temp.Add(ParseCropAndContourOverlapStruct(line));
                                    else if (line.Contains("add init opt constraint")) initOptConst_temp.Add(ParseOptimizationConstraint(line));
                                    else if (line.Contains("add boost opt constraint")) bstOptConst_temp.Add(ParseOptimizationConstraint(line));
                                    else if (line.Contains("create TS")) TSstructures_temp.Add(ParseCreateTS(line));
                                    else if (line.Contains("add target")) targets_temp.Add(ParseTargets(line));
                                    else if (line.Contains("add optimization TS structure")) requestedTSstructures_temp.Add(ParseOptimizationTSstructure(line));
                                    else if (line.Contains("add plan objective")) planObj_temp.Add(ParsePlanObjective(line));
                                    else if (line.Contains("add requested plan metric")) planDoseInfo_temp.Add(ParseRequestedPlanDoseInfo(line));
                                }
                            }

                            if (TSManipulation_temp.Any()) tempTemplate.TSManipulations = new List<RequestedTSManipulationModel>(TSManipulation_temp);
                            if (createRings_temp.Any()) tempTemplate.Rings = new List<TSRingStructureModel>(createRings_temp);
                            if (cropAndContourOverlapStructures_temp.Any()) tempTemplate.CropAndOverlapStructures = new List<string>(cropAndContourOverlapStructures_temp);
                            if (TSstructures_temp.Any()) tempTemplate.CreateTSStructures = TSstructures_temp;
                            if (initOptConst_temp.Any()) tempTemplate.InitialOptimizationConstraints = new List<OptimizationConstraintModel>(initOptConst_temp);
                            if (bstOptConst_temp.Any()) tempTemplate.BoostOptimizationConstraints = new List<OptimizationConstraintModel>(bstOptConst_temp);
                            if (targets_temp.Any()) tempTemplate.PlanTargets = new List<PlanTargetsModel>(TargetsHelper.GroupTargetsByPlanIdAndOrderByTargetRx(targets_temp));
                            if (planObj_temp.Any()) tempTemplate.PlanObjectives = new List<PlanObjectiveModel>(planObj_temp);
                            if (requestedTSstructures_temp.Any()) tempTemplate.RequestedOptimizationTSStructures = new List<RequestedOptimizationTSStructureModel>(requestedTSstructures_temp);
                            if (planDoseInfo_temp.Any()) tempTemplate.RequestedPlanMetrics = new List<RequestedPlanMetricModel>(planDoseInfo_temp);
                        }
                    }
                }
                reader.Close();
            }
            return tempTemplate;
        }

        /// <summary>
        /// Helper function to parse a template .ini file and create a new instance of TBIAutoPlanTemplate
        /// </summary>
        /// <param name="file"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static TBIAutoPlanTemplate ReadTBITemplatePlan(string file, int count)
        {
            TBIAutoPlanTemplate tempTemplate = new TBIAutoPlanTemplate(count);
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
                            List<RequestedTSManipulationModel> TSManipulation_temp = new List<RequestedTSManipulationModel> { };
                            List<RequestedTSStructureModel> TSstructures_temp = new List<RequestedTSStructureModel> { };
                            List<OptimizationConstraintModel> initOptConst_temp = new List<OptimizationConstraintModel> { };
                            List<PlanTargetsModel> targets_temp = new List<PlanTargetsModel> { };
                            //optimization loop
                            List<PlanObjectiveModel> planObj_temp = new List<PlanObjectiveModel> { };
                            List<RequestedPlanMetricModel> planDoseInfo_temp = new List<RequestedPlanMetricModel> { };
                            List<RequestedOptimizationTSStructureModel> requestedTSstructures_temp = new List<RequestedOptimizationTSStructureModel> { };
                            //parse the data specific to the myeloablative case setup
                            while (!(line = reader.ReadLine()).Equals(":end template case configuration:"))
                            {
                                if (line.Substring(0, 1) != "%")
                                {
                                    if (line.Contains("="))
                                    {
                                        string parameter = line.Substring(0, line.IndexOf("="));
                                        string value = line.Substring(line.IndexOf("=") + 1, line.Length - line.IndexOf("=") - 1);
                                        if (parameter == "template name") tempTemplate.TemplateName = value;
                                        else if (parameter == "dose per fraction")
                                        {
                                            if (double.TryParse(value, out double initDPF)) tempTemplate.InitialRxDosePerFx = initDPF;
                                        }
                                        else if (parameter == "num fx")
                                        {
                                            if (int.TryParse(value, out int initFx)) tempTemplate.InitialRxNumberOfFractions = initFx;
                                        }
                                    }
                                    else if (line.Contains("add TS manipulation")) TSManipulation_temp.Add(ParseTSManipulation(line));
                                    else if (line.Contains("add opt constraint")) initOptConst_temp.Add(ParseOptimizationConstraint(line));
                                    else if (line.Contains("create TS")) TSstructures_temp.Add(ParseCreateTS(line));
                                    else if (line.Contains("add target")) targets_temp.Add(ParseTargets(line));
                                    else if (line.Contains("add optimization TS structure")) requestedTSstructures_temp.Add(ParseOptimizationTSstructure(line));
                                    else if (line.Contains("add plan objective")) planObj_temp.Add(ParsePlanObjective(line));
                                    else if (line.Contains("add requested plan metric")) planDoseInfo_temp.Add(ParseRequestedPlanDoseInfo(line));
                                }
                            }

                            if (TSManipulation_temp.Any()) tempTemplate.TSManipulations = new List<RequestedTSManipulationModel>(TSManipulation_temp);
                            if (TSstructures_temp.Any()) tempTemplate.CreateTSStructures = TSstructures_temp;
                            if (initOptConst_temp.Any()) tempTemplate.InitialOptimizationConstraints = new List<OptimizationConstraintModel>(initOptConst_temp);
                            if (targets_temp.Any()) tempTemplate.PlanTargets = new List<PlanTargetsModel>(TargetsHelper.GroupTargetsByPlanIdAndOrderByTargetRx(targets_temp));
                            if (planObj_temp.Any()) tempTemplate.PlanObjectives = new List<PlanObjectiveModel>(planObj_temp);
                            if (requestedTSstructures_temp.Any()) tempTemplate.RequestedOptimizationTSStructures = new List<RequestedOptimizationTSStructureModel>(requestedTSstructures_temp);
                            if (planDoseInfo_temp.Any()) tempTemplate.RequestedPlanMetrics = new List<RequestedPlanMetricModel>(planDoseInfo_temp);
                        }
                    }
                }
                reader.Close();
            }
            return tempTemplate;
        }

        /// <summary>
        /// Helper function to crop a string using a specified cropping character. All characters in the supplied string will be removed up to the first instance of the
        /// supplied character and the remainder will be returned
        /// </summary>
        /// <param name="line"></param>
        /// <param name="cropChar"></param>
        /// <returns></returns>
        public static string CropLine(string line, string cropChar) { return line.Substring(line.IndexOf(cropChar) + 1, line.Length - line.IndexOf(cropChar) - 1); }

        /// <summary>
        /// Helper function to parse requested jaw positions for the fields. As the fields are added to each isocenter, the jaws will be set in the order the jaw positions were parsed from the
        /// configuration file
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static (bool, VRect<double>) ParseJawPositions(string line)
        {
            bool fail = false;
            List<double> tmp = new List<double> { };
            VRect<double> jawPos = new VRect<double> { };
            line = CropLine(line, "{");
            //second character should not be the end brace (indicates the last element in the array)
            while (line.Contains(","))
            {
                tmp.Add(double.Parse(line.Substring(0, line.IndexOf(","))));
                line = CropLine(line, ",");
            }
            tmp.Add(double.Parse(line.Substring(0, line.IndexOf("}"))));
            if (tmp.Count != 4) fail = true;
            else jawPos = new VRect<double>(tmp.ElementAt(0), tmp.ElementAt(1), tmp.ElementAt(2), tmp.ElementAt(3));
            return (fail, jawPos);
        }

        /// <summary>
        /// Helper function to parse a requested tuning structure from the template and configuration files
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static RequestedTSStructureModel ParseCreateTS(string line)
        {
            //known array format --> can take shortcuts in parsing the data
            //structure id, sparing type, added margin in cm (ignored if sparing type is Dmax ~ Rx Dose)
            string dicomType;
            string TSstructure;
            line = CropLine(line, "{");
            dicomType = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            TSstructure = line.Substring(0, line.IndexOf("}"));
            return new RequestedTSStructureModel(dicomType, TSstructure);
        }

        /// <summary>
        /// Helper function to parse requested Daemon settings
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static DaemonModel ParseDaemonSettings(string line)
        {
            string AETitle;
            string IP;
            int port = -1;
            line = CropLine(line, "{");
            AETitle = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            IP = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            int.TryParse(line.Substring(0, line.IndexOf("}")), out port);
            return new DaemonModel(AETitle, IP, port);
        }

        /// <summary>
        /// Helper function to parse a requested ring structure from a template file
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static TSRingStructureModel ParseCreateRing(string line)
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
            return new TSRingStructureModel(structure, margin, thickness, dose);
        }

        /// <summary>
        /// Helper method to parse the requested targets from a template plan fille
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static PlanTargetsModel ParseTargets(string line)
        {
            string structure;
            string planId;
            double rx;
            line = CropLine(line, "{");
            structure = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            rx = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = CropLine(line, ",");
            planId = line.Substring(0, line.IndexOf("}"));
            return new PlanTargetsModel(planId, new TargetModel(structure, rx));
        }

        /// <summary>
        /// Helper method to parse a requested tuning structure manipulation from a template plan file
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static RequestedTSManipulationModel ParseTSManipulation(string line)
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
            return new RequestedTSManipulationModel(structure, TSManipulationTypeHelper.GetTSManipulationType(spareType), val);
        }

        /// <summary>
        /// Helper method to parse a 'crop and contour overlap with target structure' from a template plan file
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private static string ParseCropAndContourOverlapStruct(string line)
        {
            string structure;
            line = CropLine(line, "{");
            structure = line.Substring(0, line.IndexOf("}"));
            return structure;
        }

        /// <summary>
        /// Helper method to parse an optimization constraint from a template plan file
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static OptimizationConstraintModel ParseOptimizationConstraint(string line)
        {
            //known array format --> can take shortcuts in parsing the data
            //structure id, constraint type, dose (cGy), volume (%), priority
            string structure;
            string constraintType;
            double doseVal;
            double volumeVal;
            int priorityVal;
            line = CropLine(line, "{");
            structure = line.Substring(0, line.IndexOf(",")).Trim();
            line = CropLine(line, ",");
            constraintType = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            doseVal = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = CropLine(line, ",");
            volumeVal = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = CropLine(line, ",");
            priorityVal = int.Parse(line.Substring(0, line.IndexOf("}")));
            return new OptimizationConstraintModel(structure, OptimizationTypeHelper.GetObjectiveType(constraintType), doseVal, Units.cGy, volumeVal, priorityVal);
        }

        /// <summary>
        /// Helper method to parse requested plan information that will be reported following each iteration of the optimization loop
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static RequestedPlanMetricModel ParseRequestedPlanDoseInfo(string line)
        {
            line = CropLine(line, "{");
            string structure = line.Substring(0, line.IndexOf(","));
            line = CropLine(line, ",");
            DVHMetric metric = DVHMetricTypeHelper.GetDVHMetricType(line.Substring(0, line.IndexOf(",")));
            if (metric == DVHMetric.None) return null;
            line = CropLine(line, ",");
            if(metric == DVHMetric.Dmax || metric == DVHMetric.Dmin)
            {
                Units queryResultUnits = UnitsTypeHelper.GetUnitsType(line.Substring(0, line.IndexOf("}")));
                return new RequestedPlanMetricModel(structure, metric, queryResultUnits);
            }
            else
            {
                double queryVal = double.Parse(line.Substring(0, line.IndexOf(",")));
                line = CropLine(line, ",");
                Units queryValueUnits = UnitsTypeHelper.GetUnitsType(line.Substring(0, line.IndexOf(",")));
                line = CropLine(line, ",");
                Units queryResultUnits = UnitsTypeHelper.GetUnitsType(line.Substring(0, line.IndexOf("}")));
                return new RequestedPlanMetricModel(structure, metric, queryVal, queryValueUnits, queryResultUnits); 
            }
        }

        public static List<OptTSCreationCriteriaModel> ParseOptTSCreationCriteria(string line)
        {
            List<OptTSCreationCriteriaModel> constraints = new List<OptTSCreationCriteriaModel> { };
            line = line.Trim('{', '}');
            if (string.IsNullOrEmpty(line)) return constraints;
            List<string> splitStr = line.Split(',').ToList();
            foreach(string itr in splitStr)
            {
                string itrTrim = itr.Trim();
                if (string.Equals(itrTrim, "finalopt", StringComparison.OrdinalIgnoreCase))
                {
                    constraints.Add(new OptTSCreationCriteriaModel(true));
                }
                else
                {
                    List<string> splitConstraint = itrTrim.Split(' ').ToList();
                    string stat = splitConstraint.ElementAt(0);
                    if (string.Equals(stat, "dmax", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(stat, "dmin", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(stat, "dmean", StringComparison.OrdinalIgnoreCase))
                    {
                        DVHMetric metric = DVHMetricTypeHelper.GetDVHMetricType(stat);
                        InequalityOperator op = InequalityOperatorHelper.GetInequalityOperator(splitConstraint.ElementAt(1));
                        if (double.TryParse(splitConstraint.ElementAt(2), out double limit))
                        {
                            Units resultUnits = UnitsTypeHelper.GetUnitsType(splitConstraint.ElementAt(3));
                            constraints.Add(new OptTSCreationCriteriaModel(metric, op, limit, resultUnits));
                        }
                    }
                    else
                    {
                        DVHMetric metric = DVHMetric.None;
                        if (string.Equals(stat, "d", StringComparison.OrdinalIgnoreCase))
                        {
                            metric = DVHMetric.DoseAtVolume;
                        }
                        else if (string.Equals(stat, "v", StringComparison.OrdinalIgnoreCase))
                        {
                            metric = DVHMetric.VolumeAtDose;
                        }
                        if (metric != DVHMetric.None && double.TryParse(splitConstraint.ElementAt(1), out double queryVal))
                        {
                            Units queryUnits = UnitsTypeHelper.GetUnitsType(splitConstraint.ElementAt(2));
                            InequalityOperator op = InequalityOperatorHelper.GetInequalityOperator(splitConstraint.ElementAt(3));
                            if (double.TryParse(splitConstraint.ElementAt(4), out double limit))
                            {
                                Units resultUnits = UnitsTypeHelper.GetUnitsType(splitConstraint.ElementAt(5));
                                constraints.Add(new OptTSCreationCriteriaModel(metric, queryVal, queryUnits, op, limit, resultUnits));
                            }
                        }
                    }
                }

            }
            
            return constraints;
        }

        /// <summary>
        /// Helper method to parse a requested heater or cooler tuning structure that should be created after each iteration of the optimization loop
        /// provded certain conditions are met
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static RequestedOptimizationTSStructureModel ParseOptimizationTSstructure(string line)
        {
            string structure;
            double lowDoseLevel;
            double upperDoseLevel;
            double volumeVal;
            int priority;
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

            RequestedOptimizationTSStructureModel requestedOptimizationTSStructure = null;
            if (structure.ToLower().Contains("heater"))
            {
                requestedOptimizationTSStructure = new TSHeaterStructureModel(structure, lowDoseLevel, upperDoseLevel, priority, ParseOptTSCreationCriteria(CropLine(line,",")));
            }
            else if (structure.ToLower().Contains("cooler"))
            {
                requestedOptimizationTSStructure = new TSCoolerStructureModel(structure, lowDoseLevel, upperDoseLevel, priority, ParseOptTSCreationCriteria(CropLine(line, ",")));
            }
            return requestedOptimizationTSStructure;
        }

        /// <summary>
        /// Helper method to parse a plan objective from a plan template file
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static PlanObjectiveModel ParsePlanObjective(string line)
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
            return new PlanObjectiveModel(structure, OptimizationTypeHelper.GetObjectiveType(constraintType), doseVal, dvp == DoseValuePresentation.Absolute ? Units.cGy : Units.Percent, volumeVal, Units.Percent);
        }

        /// <summary>
        /// Helper method to verify the integrity of a user supplied path. If the path exists, return the supplied path ensuring the last character in the path is '\'. If it does not, return an empty string
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string VerifyPathIntegrity(string value)
        {
            string result = "";
            if (Directory.Exists(value))
            {
                result = value;
                if (result.LastIndexOf("\\") != result.Length - 1) result += "\\";
            }
            return result;
        }
    }
}