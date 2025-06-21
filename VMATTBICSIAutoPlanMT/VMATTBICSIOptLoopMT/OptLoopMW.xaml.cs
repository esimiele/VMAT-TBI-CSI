using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using Microsoft.Win32;
using VMS.TPS.Common.Model.API;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.UIHelpers;
using VMATTBICSIAutoPlanningHelpers.Prompts;
using VMATTBICSIOptLoopMT.VMAT_CSI;
using VMATTBICSIOptLoopMT.VMAT_TBI;
using VMATTBICSIOptLoopMT.Prompts;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using PlanType = VMATTBICSIAutoPlanningHelpers.Enums.PlanType;
using VMATTBICSIAutoPlanningHelpers.Logging;
using System.Diagnostics;
using VMATTBICSIAutoPlanningHelpers.Models;
using VMATTBICSIAutoPlanningHelpers.DataContainers;
using VMATTBICSIAutoPlanningHelpers.Interfaces;

namespace VMATTBICSIOptLoopMT
{
    public partial class OptLoopMW : Window
    {
        public List<PlanObjectiveModel> planObj = new List<PlanObjectiveModel> { };
        public List<RequestedOptimizationTSStructureModel> requestedTSstructures = new List<RequestedOptimizationTSStructureModel>();
        public List<RequestedPlanMetricModel> planDoseInfo = new List<RequestedPlanMetricModel> { };
        public List<PlanOptimizationSetupModel> optConstraintsFromLogs = new List<PlanOptimizationSetupModel> { };
        //ATTENTION! THE FOLLOWING LINE HAS TO BE FORMATTED THIS WAY, OTHERWISE THE DATA BINDING WILL NOT WORK!
        public ObservableCollection<AutoPlanTemplateBase> PlanTemplates { get; set; }
        public AutoPlanTemplateBase selectedTemplate;

        private VMS.TPS.Common.Model.API.Application app = null;
        //configuration file
        private string configFile = "";
        //point this to the directory holding the documentation files
        private string documentationPath;
        //default number of optimizations to perform
        private string defautlNumOpt = "3";
        //default plan normaliBzation (i.e., PTV100% = 90%) 
        private string defaultPlanNorm = "90";
        //run coverage check
        private bool runCoverageCheckOption = false;
        //run additional optimization option
        private bool runAdditionalOptOption = true;
        //copy and save each optimized plan
        private bool copyAndSaveOption = false;
        //is demo
        private bool demo = false;
        //log file directory
        private string logFilePath;
        //decision threshold
        private double threshold = 0.15;
        //lower dose limit
        private double lowDoseLimit = 0.1;
        private List<string> reminders = new List<string> { };
        private List<ExternalPlanSetup> plans = new List<ExternalPlanSetup> { };
        private StructureSet selectedSS;
        private Patient pi = null;
        private Course theCourse = null;
        private bool runCoverageCheck = false;
        private bool runOneMoreOpt = false;
        private bool copyAndSavePlanItr = false;
        private bool useFlash = false;
        private bool logFileLoaded = false;
        
        private string selectedTemplateName = "";
        //to be read from the plan prep log files
        private PlanType planType;
        private List<string> planUIDs = new List<string> { };
        private List<PrescriptionModel> prescriptions = new List<PrescriptionModel> { };
        //plan id, volume id
        private Dictionary<string, string> normalizationVolumes = new Dictionary<string, string> { };
        //list<original target id, ts target id>
        private Dictionary<string, string> tsTargets = new Dictionary<string, string> { };

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="args"></param>
        public OptLoopMW(string[] args)
        {
            InitializeComponent();
            InitializeScript(args);
        }

        /// <summary>
        /// Script initialization including generating the connection to Aria, loading the patient, and displaying the script configuration
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool InitializeScript(string[] args)
        {
            try { app = VMS.TPS.Common.Model.API.Application.CreateApplication(); }
            catch (Exception e) { MessageBox.Show($"Warning! Could not generate Aria application instance because: {e.Message}"); }

            AssignDefaultLogAndDocPaths();
            string tmpLogPath = ConfigurationHelper.ReadLogPathFromConfigurationFile(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\log_configuration.ini");
            if (!string.IsNullOrEmpty(tmpLogPath)) logFilePath = tmpLogPath;

            PlanTemplates = new ObservableCollection<AutoPlanTemplateBase>() { };
            DataContext = this;
            if(app != null) LoadPatient(args.ToList());
            DisplayConfigurationParameters();
            return false;
        }

        private void AssignDefaultLogAndDocPaths()
        {
            logFilePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\logs\\";
            documentationPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\documentation\\";
        }

        #region help and info buttons
        private void Help_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(documentationPath + "VMAT-TBI-CSI_OptLoop_Guide.pdf")) MessageBox.Show("VMAT-TBI-CSI_OptLoop_Guide PDF file does not exist!");
            else Process.Start(documentationPath + "VMAT-TBI-CSI_OptLoop_Guide.pdf");
        }

        private void QuickStart_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(documentationPath + "VMAT-TBI-CSI_OptLoop_QuickStartGuide.pdf")) MessageBox.Show("VMAT-TBI-CSI_OptLoop_QuickStartGuide PDF file does not exist!");
            else Process.Start(documentationPath + "VMAT-TBI-CSI_OptLoop_QuickStartGuide.pdf");
        }

        private void targetNormInfo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This is used to set the plan normalization. What percentage of the PTV volume should recieve the prescription dose?");
        }
        #endregion

        #region load and open patient
        /// <summary>
        /// Utility method to load a patient into the script. Attempt to read the log file from the preparation script
        /// </summary>
        /// <param name="patmrn"></param>
        private void LoadPatient(List<string> startupArgs)
        {
            if (app == null) return;
            string mrn = string.Empty;
            if (startupArgs.Any(x => string.Equals("-m", x)))
            {
                int index = startupArgs.IndexOf("-m");
                mrn = startupArgs.ElementAt(index + 1);
            }
            string fullLogName;
            bool cancel = false;
            if (string.IsNullOrEmpty(mrn))
            {
                (bool, string, PlanType, string) result = PromptUserForPatientSelection();
                cancel = result.Item1;
                mrn = result.Item2;
                planType = result.Item3;
                planTypeLabel.Content = planType;
                fullLogName = result.Item4;
            }
            else
            {
                if(LogHelper.GetNumberofMatchingLogFilesForMRN(mrn, logFilePath) == 1) fullLogName = LogHelper.GetFullLogFileFromExistingMRN(mrn, logFilePath);
                else
                {
                    //if log file not found or if more than one log file found (if patient receives both csi and tbi)
                    (bool, string, PlanType, string) result = PromptUserForPatientSelection(mrn);
                    cancel = result.Item1;
                    mrn = result.Item2;
                    planType = result.Item3;
                    planTypeLabel.Content = planType;
                    fullLogName = result.Item4;
                }
            }
            if (!cancel)
            {
                if (!string.IsNullOrEmpty(mrn))
                {
                    if (!string.IsNullOrEmpty(fullLogName) && !LoadLogFile(fullLogName)) logFileLoaded = true;
                    LoadConfigurationSettingsForPlanType(planType);

                    pi = OpenPatient(mrn);
                    if(!ReferenceEquals(pi, null))
                    {
                        PatMRNLabel.Content = pi.Id;
                        plans = GetPlans();
                        if (plans.Any())
                        {
                            if (!plans.All(x => string.Equals(plans.First().StructureSet.UID, x.StructureSet.UID)))
                            {
                                MessageBox.Show("Error! Base plan and boost plan do NOT share the same structure set! Update plan selection and try again");
                                plans = new List<ExternalPlanSetup>();
                                return;
                            }
                            selectedSS = plans.First().StructureSet;
                            UpdateNormalizationComboBoxes(selectedSS.Structures.Select(x => x.Id));

                            UpdateAvailablePlans(theCourse.ExternalPlanSetups.Where(x => x.Beams.Any(y => !y.IsSetupField) && !x.Id.ToLower().Contains("leg")).Select(x => x.Id));

                            planObjectiveHeader.Background = System.Windows.Media.Brushes.PaleVioletRed;
                        }
                    }
                    LoadTemplatePlanChoices(planType);

                    if (planType == PlanType.VMAT_TBI && reminders.Any(x => x.ToLower().Contains("base dose")))
                    {
                        if (plans.Any() && !plans.First().Course.ExternalPlanSetups.Any(x => x.Id.ToLower().Contains("leg"))) reminders.Remove(reminders.First(x => x.ToLower().Contains("base dose")));
                    }
                    selectPatientBtn.Background = System.Windows.Media.Brushes.LightGray;
                }
                else MessageBox.Show($"Entered MRN: {mrn} is invalid! Please re-enter and try again");
            }
            else if (pi == null) selectPatientBtn.Background = System.Windows.Media.Brushes.PaleVioletRed;
        }

        private void UpdateNormalizationComboBoxes(IEnumerable<string> structureIds)
        {
            initNormVolumeCB.Items.Clear();
            bstNormVolumeCB.Items.Clear();
            initNormVolumeCB.Items.Add("--select--");
            foreach (string sid in structureIds.Where(x => x.ToLower().Contains("ptv")))
            {
                initNormVolumeCB.Items.Add(sid);
            }
            if (planType == PlanType.VMAT_CSI && plans.Count > 1)
            {
                bstNormVolumeCB.Items.Add("--select--");
                foreach (string sid in structureIds.Where(x => x.ToLower().Contains("ptv")))
                {
                    bstNormVolumeCB.Items.Add(sid);
                }
            }
        }

        private void UpdateAvailablePlans(IEnumerable<string> planIds)
        {
            basePlanIdCB.Items.Clear();
            boostPlanIdCB.Items.Clear();
            foreach (string id in planIds)
            {
                basePlanIdCB.Items.Add(id);
            }
            if (plans.Any()) basePlanIdCB.SelectedIndex = basePlanIdCB.Items.IndexOf(plans.First().Id);
            else basePlanIdCB.SelectedIndex = 0;

            if (planType == PlanType.VMAT_CSI && plans.Count > 1)
            {
                foreach (string id in planIds)
                {
                    boostPlanIdCB.Items.Add(id);
                }
                if (plans.Any()) boostPlanIdCB.SelectedIndex = boostPlanIdCB.Items.IndexOf(plans.Last().Id);
                else boostPlanIdCB.SelectedIndex = 0;
            }
        }

        private (bool, string, PlanType, string) PromptUserForPatientSelection(string patmrn = "")
        {
            //open the patient with the user-entered MRN number
            bool cancel = false;
            string mrn = patmrn;
            string fullLogName = "";
            PlanType pType = PlanType.None;
            SelectPatient sp = new SelectPatient(logFilePath, mrn);
            sp.ShowDialog();
            if (!sp.selectionMade)
            {
                cancel = true;
                return (cancel, mrn, PlanType.None, fullLogName); ;
            }
            else
            {
                (string, PlanType, string) result = sp.GetPatientSelection();
                mrn = result.Item1;
                pType = result.Item2;
                fullLogName = result.Item3;
            }

            return (cancel, mrn, pType, fullLogName);
        }

        private Patient OpenPatient(string pat_mrn)
        {
            try
            {
                ClearEverything();
                app.ClosePatient();
                return app.OpenPatientById(pat_mrn);
            }
            catch
            {
                MessageBox.Show("No such patient exists!");
                return null;
            }
        }

        /// <summary>
        /// Helper method to retrieve the structure set and list of plans
        /// </summary>
        /// <returns></returns>
        private List<ExternalPlanSetup> GetPlans()
        {
            List<ExternalPlanSetup> thePlans = new List<ExternalPlanSetup> { };
            //grab an instance of the VMAT TBI plan. Return null if it isn't found
            if (pi == null) return thePlans;
            if (planUIDs.Any())
            {
                //should automatically be in order in terms of cumulative Rx (lowest to highest)
                foreach (string uid in planUIDs)
                {
                    ExternalPlanSetup tmp = pi.Courses.SelectMany(x => x.ExternalPlanSetups).FirstOrDefault(x => string.Equals(x.UID, uid));
                    if (tmp != null) thePlans.Add(tmp);
                }
                if (thePlans.Any())
                {
                    if (!thePlans.All(x => string.Equals(x.Course.Id, thePlans.First().Course.Id)))
                    {
                        MessageBox.Show("Error! Plans parsed from log file belong to separate courses! They must exist in the source course! Please fix and try again");
                        return new List<ExternalPlanSetup> { };
                    }
                    theCourse = thePlans.First().Course;
                    return thePlans;
                }
                else MessageBox.Show("Warning! I found plan UIDs in the log file, but found not matching plans in Eclipse. Attempting to manually resolve plans");
            }
            //simple logic to try and guess which plans are which
            List<Course> courses = pi.Courses.Where(x => x.Id.ToLower().Contains("csi") || x.Id.ToLower().Contains("tbi")).ToList();
            if (!courses.Any()) return thePlans;
            if (courses.Count > 1)
            {
                SelectItemPrompt SIP = new SelectItemPrompt("Please select a course:", courses.Select(x => x.Id).ToList());
                SIP.ShowDialog();
                if (!SIP.GetSelection()) return thePlans;
                theCourse = courses.FirstOrDefault(x => string.Equals(x.Id, SIP.GetSelectedItem()));
            }
            else theCourse = courses.First();
            
            thePlans = theCourse.ExternalPlanSetups.Where(x => x.Beams.Any(y => !y.IsSetupField) && !x.Id.ToLower().Contains("leg")).ToList();
            if (!thePlans.Any())
            {
                MessageBox.Show($"Error! No plans found in course: {theCourse.Id}! Unable to determine which plan(s) should be used for optimization! Exiting!");
                return new List<ExternalPlanSetup> { };
            }
            if (thePlans.Count > 1)
            {
                if (planType == PlanType.VMAT_TBI)
                {
                    SelectItemPrompt SIP = new SelectItemPrompt("Please select a plan to optimize:", thePlans.Select(x => x.Id).ToList());
                    SIP.ShowDialog();
                    if (!SIP.GetSelection()) return new List<ExternalPlanSetup> { };
                    ExternalPlanSetup thePlan = thePlans.First(x => string.Equals(x.Id, SIP.GetSelectedItem()));
                    thePlans = new List<ExternalPlanSetup> { thePlan };
                }
                else
                {
                    SelectItemPrompt SIP = new SelectItemPrompt("Does this CSI case include a sequential boost?", new List<string> { "No", "Yes"});
                    SIP.ShowDialog();
                    if (!SIP.GetSelection()) return new List<ExternalPlanSetup> { };
                    if(string.Equals(SIP.GetSelectedItem(), "yes", StringComparison.OrdinalIgnoreCase))
                    {
                        return thePlans;
                    }
                    else
                    {
                        UpdateAvailablePlans(thePlans.Select(x => x.Id));
                        thePlans = new List<ExternalPlanSetup> { };
                    }
                }
            }

            return thePlans;
        }
        #endregion

        #region button events
        private void SelectPatient_Click(object sender, RoutedEventArgs e)
        {
            LoadPatient(new List<string> { "-m", "" });
        }

        private void GetOptFromPlan_Click(object sender, RoutedEventArgs e)
        {
            if (pi != null && plans.Any()) PopulateOptimizationTab(optimizationParamSP);
        }

        private void GetOptFromLogs_Click(object sender, RoutedEventArgs e)
        {
            if (pi != null && plans.Any())
            {
                if (logFileLoaded)
                {
                    if (optConstraintsFromLogs.Any())
                    {
                        ClearAllItemsFromUIList(optimizationParamSP);
                        foreach (PlanOptimizationSetupModel itr in optConstraintsFromLogs) AddListItemsToUI(itr.OptimizationConstraints, itr.PlanId, optimizationParamSP);
                    }
                    else MessageBox.Show("No optimization constraints present in log file!");
                }
                else MessageBox.Show("Log file was not loaded! Can't show optimization constraints from log file!");
            }
        }

        private void AddItem_Click(object sender, RoutedEventArgs e)
        {
            if (!plans.Any()) return;
            (StackPanel, ScrollViewer) SPAndSV = GetSPAndSV(sender as Button);
            if(SPAndSV.Item1.Name.ToLower().Contains("optimization"))
            {
                ExternalPlanSetup thePlan = null;
                if (plans.Count > 1)
                {
                    SelectItemPrompt SIP = new SelectItemPrompt("Please selct a plan to add a constraint!", plans.Select(x => x.Id).ToList());
                    //SUI.itemCombo.Items.Add("Both");
                    SIP.ShowDialog();
                    if (SIP.GetSelection()) thePlan = plans.FirstOrDefault(x => string.Equals(x.Id, SIP.GetSelectedItem()));
                    else return;
                    if (thePlan == null) { MessageBox.Show("Plan not found! Exiting!"); return; }
                }
                else thePlan = plans.First();

                List<OptimizationConstraintModel> tmp = new List<OptimizationConstraintModel> { new OptimizationConstraintModel("--select--", OptimizationObjectiveType.None, 0.0, Units.cGy, 0.0, 0) };
                List<List<OptimizationConstraintModel>> tmpList = new List<List<OptimizationConstraintModel>> { };
                if (SPAndSV.Item1.Children.Count > 0)
                {
                    List<PlanOptimizationSetupModel> optParametersListList = OptimizationSetupUIHelper.ParseOptConstraints(SPAndSV.Item1, false).Item1;
                    foreach (PlanOptimizationSetupModel itr in optParametersListList)
                    {
                        if (string.Equals(itr.PlanId, thePlan.Id))
                        {
                            tmp = new List<OptimizationConstraintModel>(itr.OptimizationConstraints)
                            {
                                new OptimizationConstraintModel("--select--", OptimizationObjectiveType.None, 0.0, Units.cGy, 0.0, 0)
                            };
                            tmpList.Add(tmp);
                        }
                        else tmpList.Add(itr.OptimizationConstraints);
                    }
                }
                else
                {
                    tmpList.Add(tmp);
                }
                ClearAllItemsFromUIList(SPAndSV.Item1);
                int count = 0;
                foreach (List<OptimizationConstraintModel> itr in tmpList) AddListItemsToUI(itr, plans.ElementAt(count++).Id, SPAndSV.Item1);
            }
            else
            {
                List<PlanObjectiveModel> tmp = new List<PlanObjectiveModel> 
                { 
                    new PlanObjectiveModel("--select--", OptimizationObjectiveType.None, 0.0, Units.Percent, 0.0, Units.Percent) 
                };
                AddListItemsToUI(tmp, "", SPAndSV.Item1); 
                planObjectiveHeader.Background = System.Windows.Media.Brushes.ForestGreen;
                optimizationSetupHeader.Background = System.Windows.Media.Brushes.PaleVioletRed;
            }
            SPAndSV.Item2.ScrollToBottom();
        }

        private void ClearAllItems_Click(object sender, RoutedEventArgs e) 
        {
            ClearAllItemsFromUIList(GetSPAndSV(sender as Button).Item1);
        }

        private void ClearItem_Click(object sender, EventArgs e)
        {
            StackPanel theSP = GetSPAndSV(sender as Button).Item1;
            if (GeneralUIHelper.ClearRow(sender, theSP)) ClearAllItemsFromUIList(theSP);
        }
        #endregion

        #region UI manipulation
        private void Plans_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pi == null || planType == PlanType.None || basePlanIdCB.Items.Count == 0) return;
            if (planType == PlanType.VMAT_TBI) plans = new List<ExternalPlanSetup> { theCourse.ExternalPlanSetups.First(x => string.Equals(x.Id, basePlanIdCB.SelectedItem.ToString(), StringComparison.OrdinalIgnoreCase))};
            else
            {
                if(plans.Count > 1)
                {
                    if (boostPlanIdCB.Items.Count == 0)
                    {
                        //MessageBox.Show("Error! Boost plan Id combobox is not populated. Unable to assign plans to list! Exiting");
                        //plans = new List<ExternalPlanSetup>();
                        return;
                    }
                    //figure out which cb was changed
                    plans = new List<ExternalPlanSetup> { theCourse.ExternalPlanSetups.First(x => string.Equals(x.Id, basePlanIdCB.SelectedItem.ToString(), StringComparison.OrdinalIgnoreCase)), theCourse.ExternalPlanSetups.First(x => string.Equals(x.Id, boostPlanIdCB.SelectedItem.ToString(), StringComparison.OrdinalIgnoreCase))};
                    if (!plans.All(x => string.Equals(plans.First().StructureSet.UID, x.StructureSet.UID)))
                    {
                        MessageBox.Show("Error! Base plan and boost plan do NOT share the same structure set! Update plan selection and try again");
                        basePlanIdCB.SelectedIndex = 0;
                        boostPlanIdCB.SelectedIndex = 0;
                        plans = new List<ExternalPlanSetup>();
                        return;
                    }
                }
                else
                {
                    plans = new List<ExternalPlanSetup> { theCourse.ExternalPlanSetups.First(x => string.Equals(x.Id, basePlanIdCB.SelectedItem.ToString(), StringComparison.OrdinalIgnoreCase))};
                }
            }
            if(ReferenceEquals(selectedSS, null) || !string.Equals(selectedSS.UID, plans.First().StructureSet.UID))
            {
                selectedSS = plans.First().StructureSet;
                UpdateNormalizationComboBoxes(selectedSS.Structures.Select(x => x.Id));
            }
            PopulateRx();
            PopulateOptimizationTab(optimizationParamSP);
        }

        private void Templates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pi == null) return;
            selectedTemplate = templateList.SelectedItem as AutoPlanTemplateBase;
            if (selectedTemplate == null) return;
            UpdateSelectedTemplate();
        }

        private void UpdateSelectedTemplate()
        {
            if (selectedTemplate != null && selectedSS != null)
            {
                ClearAllItemsFromUIList(planObjectiveParamSP);
                //requires a structure set to properly function
                planObj = new List<PlanObjectiveModel>(PlanObjectiveHelper.ConstructPlanObjectives(selectedTemplate.PlanObjectives, selectedSS, tsTargets));
                PopulatePlanObjectivesTab(planObjectiveParamSP);
                planDoseInfo = new List<RequestedPlanMetricModel>(selectedTemplate.RequestedPlanMetrics);
                requestedTSstructures = new List<RequestedOptimizationTSStructureModel>(selectedTemplate.RequestedOptimizationTSStructures);
                if (selectedTemplate.PlanObjectives.Any())
                {
                    planObjectiveHeader.Background = System.Windows.Media.Brushes.ForestGreen;
                    optimizationSetupHeader.Background = System.Windows.Media.Brushes.PaleVioletRed;
                }
            }
            else
            {
                templateList.UnselectAll();
                planObj = new List<PlanObjectiveModel>();
                ClearAllItemsFromUIList(planObjectiveParamSP);
                planDoseInfo = new List<RequestedPlanMetricModel>();
                requestedTSstructures = new List<RequestedOptimizationTSStructureModel> { };
                planObjectiveHeader.Background = System.Windows.Media.Brushes.PaleVioletRed;
                optimizationSetupHeader.Background = System.Windows.Media.Brushes.DarkGray;
            }
        }

        private (StackPanel, ScrollViewer) GetSPAndSV(Button theBTN)
        {
            StackPanel theSP;
            ScrollViewer theScroller;
            if (theBTN.Name.ToLower().Contains("optimization"))
            {
                theSP = optimizationParamSP;
                theScroller = optimizationParamScroller;
            }
            else
            {
                theSP = planObjectiveParamSP;
                theScroller = planObjectiveParamScroller;
            }
            return (theSP, theScroller);
        }

        /// <summary>
        /// UI helper method to clear all selected parameters
        /// </summary>
        private void ClearEverything()
        {
            //clear all existing content from the main window
            templateList.UnselectAll();
            selectedSS = null;
            plans = new List<ExternalPlanSetup> { };
            initDosePerFxTB.Text = initNumFxTB.Text = initRxTB.Text = "";
            ClearAllItemsFromUIList(optimizationParamSP);
            ClearAllItemsFromUIList(planObjectiveParamSP);
        }

        /// <summary>
        /// UI helper method to populate the prescription text boxes in the UI
        /// </summary>
        private void PopulateRx()
        {
            //populate the prescription text boxes
            initDosePerFxTB.Text = plans.First().DosePerFraction.Dose.ToString();
            initNumFxTB.Text = plans.First().NumberOfFractions.ToString();
            initRxTB.Text = plans.First().TotalDose.Dose.ToString();
            
            //boost plan
            if(plans.Count > 1)
            {
                boostDosePerFxTB.Text = plans.Last().DosePerFraction.Dose.ToString();
                boostNumFxTB.Text = plans.Last().NumberOfFractions.ToString();
                boostRxTB.Text = plans.Last().TotalDose.Dose.ToString();
            }
            else
            {
                boostDosePerFxTB.Text = "";
                boostNumFxTB.Text = "";
                boostRxTB.Text = "";
            }
            if (normalizationVolumes.Any())
            {
                if (normalizationVolumes.TryGetValue(basePlanIdCB.SelectedItem.ToString(), out var vol) && selectedSS.Structures.Where(x => x.Id.ToLower().Contains("ptv")).Any(x => string.Equals(vol, x.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    initNormVolumeCB.SelectedIndex = initNormVolumeCB.Items.IndexOf(vol);
                }
                else initNormVolumeCB.SelectedIndex = 0;
                if (planType == PlanType.VMAT_CSI && plans.Count > 1)
                {
                    if (normalizationVolumes.TryGetValue(boostPlanIdCB.SelectedItem.ToString(), out var bstVol) && selectedSS.Structures.Where(x => x.Id.ToLower().Contains("ptv")).Any(x => string.Equals(bstVol, x.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        bstNormVolumeCB.SelectedIndex = bstNormVolumeCB.Items.IndexOf(bstVol);
                    }
                    else bstNormVolumeCB.SelectedIndex = 0;
                }
            }
            else
            {
                initNormVolumeCB.SelectedIndex = 0;
                if (planType == PlanType.VMAT_CSI && plans.Count > 1) bstNormVolumeCB.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Helper method to population the Optimization Setup tab with the optimization constraints that are currently assigned to the plan
        /// </summary>
        /// <param name="theSP"></param>
        private void PopulateOptimizationTab(StackPanel theSP)
        {
            //clear the current list of optimization constraints and ones obtained from the plan to the user
            ClearAllItemsFromUIList(theSP);
            foreach(ExternalPlanSetup itr in plans) AddListItemsToUI(OptimizationSetupHelper.ReadConstraintsFromPlan(itr), itr.Id, theSP);
        }

        private void PopulatePlanObjectivesTab(StackPanel theSP)
        {
            //clear the current list of optimization constraints and ones obtained from the plan to the user
            ClearAllItemsFromUIList(theSP);
            AddListItemsToUI(planObj, "", theSP);
        }

        private void AddOptimizationConstraintsHeader(StackPanel theSP)
        {
            theSP.Children.Add(OptimizationSetupUIHelper.GetOptHeader(theSP.Width));
        }

        private void AddPlanObjectivesHeader(StackPanel theSP)
        {
            theSP.Children.Add(PlanObjectiveSetupUIHelper.GetObjHeader(theSP.Width));
        }

        private void AddListItemsToUI(IEnumerable<IPlanConstraint> defaultList, string planId, StackPanel theSP)
        {
            int counter = 0;
            string clearBtnNamePrefix;
            if (theSP.Name.ToLower().Contains("optimization"))
            {
                clearBtnNamePrefix = "clearOptimizationConstraintBtn";
                theSP.Children.Add(OptimizationSetupUIHelper.AddPlanIdtoOptList(theSP, planId));
                AddOptimizationConstraintsHeader(theSP);
            }
            else
            {
                //do NOT add plan ID to plan objectives
                clearBtnNamePrefix = "clearPlanObjectiveBtn";
                //need special logic here because the entire stack panel is not cleared everytime a new item is added to the list
                if(theSP.Children.Count == 0) AddPlanObjectivesHeader(theSP);
            }
            foreach(IPlanConstraint itr in defaultList)
            {
                counter++;
                theSP.Children.Add(OptimizationSetupUIHelper.AddOptVolume(theSP, 
                                                                          selectedSS, 
                                                                          itr, 
                                                                          clearBtnNamePrefix, 
                                                                          counter, 
                                                                          new RoutedEventHandler(this.ClearItem_Click), 
                                                                          theSP.Name.Contains("template")));
            }
        }

        /// <summary>
        /// Helper UI method to clear all items from the UI
        /// </summary>
        /// <param name="theSP"></param>
        private void ClearAllItemsFromUIList(StackPanel theSP)
        {
            theSP.Children.Clear();
            if(!theSP.Name.ToLower().Contains("optimization"))
            {
                planObjectiveHeader.Background = System.Windows.Media.Brushes.PaleVioletRed;
                optimizationSetupHeader.Background = System.Windows.Media.Brushes.DarkGray;
            }
        }
        #endregion

        #region start optimization
        /// <summary>
        /// Event to start the optimization loop
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartOpt_Click(object sender, RoutedEventArgs e)
        {
            (bool prelimChecksFail, double planNorm, int numOptimizations) = PreliminaryChecksOptimizationLoopStart();
            if (prelimChecksFail) return;

            List<PlanObjectiveModel> objectives = PlanObjectiveSetupUIHelper.ParsePlanObjectives(planObjectiveParamSP);
            if (!objectives.Any())
            {
                MessageBox.Show("Error! Missing plan objectives! Please add plan objectives and try again!");
                return;
            }
            if (reminders.Any())
            {
                ReminderPrompt rp = new ReminderPrompt(reminders);
                rp.ShowDialog();
                if (!rp.DialogResult.HasValue || !rp.DialogResult.Value) return;
                if (!rp.ConfirmAll)
                {
                    MessageBox.Show("Error! Not all reminders confirmed and signed off. Exiting.");
                    return;
                }
            }

            (List<PlanOptimizationSetupModel>, StringBuilder) parsedOptimizationConstraints = OptimizationSetupUIHelper.ParseOptConstraints(optimizationParamSP);
            if (!parsedOptimizationConstraints.Item1.Any())
            {
                MessageBox.Show(parsedOptimizationConstraints.Item2.ToString());
                return;
            }

            normalizationVolumes.Clear();
            normalizationVolumes.Add(basePlanIdCB.SelectedItem.ToString(), initNormVolumeCB.SelectedItem.ToString());
            if(planType == PlanType.VMAT_CSI && plans.Count > 1) normalizationVolumes.Add(boostPlanIdCB.SelectedItem.ToString(), bstNormVolumeCB.SelectedItem.ToString());

            //determine if flash was used to prep the plan
            if (parsedOptimizationConstraints.Item1.Any(x => x.OptimizationConstraints.Any(y => y.StructureId.ToLower().Contains("flash")))) useFlash = true;

            //assign optimization constraints
            pi.BeginModifications();
            if (AssignRequestedOptimizationConstraints(parsedOptimizationConstraints.Item1)) return;

            //does the user want to run the initial dose coverage check?
            runCoverageCheck = runCoverageCk.IsChecked.Value;
            //does the user want to run one additional optimization to reduce hotspots?
            runOneMoreOpt = runAdditionalOpt.IsChecked.Value;
            //does the user want to copy and save each plan after it's optimized (so the user can choose between the various plans)?
            copyAndSavePlanItr = copyAndSave.IsChecked.Value;

            //create a new instance of the structure dataContainer and assign the optimization loop parameters entered by the user to the various data members
            OptDataContainer data = new OptDataContainer(plans, //ui
                                                         prescriptions, //from logs. If not found or empty, overridden with plan normalization volume dictionary at runtime
                                                         normalizationVolumes, //from ui
                                                         objectives, //from preparation logs and ui (if template selected)
                                                         requestedTSstructures, //from template
                                                         planDoseInfo, //from template
                                                         planType, //determined from logs and user input
                                                         planNorm, //from ui
                                                         numOptimizations, //from ui
                                                         runCoverageCheck, // from ui
                                                         runOneMoreOpt, //from ui
                                                         copyAndSavePlanItr, //from ui
                                                         useFlash, //determined at runtime
                                                         threshold, //set in general config file
                                                         lowDoseLimit, //set in general config file
                                                         demo, //set in general config file
                                                         logFilePath, //set in general log file config or internally in code
                                                         app); //constructed at runtime

            //start the optimization loop (all saving to the database is performed in the progressWindow class)
            //use a bit of polymorphism
            OptimizationLoopBase optLoop;
            if(planType == PlanType.VMAT_CSI) optLoop = new VMATCSIOptimization(data);
            else optLoop = new VMATTBIOptimization(data);
            optLoop.Execute();
        }

        /// <summary>
        /// Helper to take the parsed constraints from the UI and assign them to VMAT plan
        /// </summary>
        /// <param name="constraints"></param>
        /// <returns></returns>
        private bool AssignRequestedOptimizationConstraints(List<PlanOptimizationSetupModel> constraints)
        {
            foreach (PlanOptimizationSetupModel itr in constraints)
            {
                ExternalPlanSetup thePlan = null;
                //additional check if the plan was not found in the list of VMATplans
                thePlan = plans.FirstOrDefault(x => string.Equals(x.Id, itr.PlanId));
                if (thePlan != null)
                {
                    OptimizationSetupHelper.RemoveOptimizationConstraintsFromPLan(thePlan);
                    OptimizationSetupHelper.AssignOptConstraints(itr.OptimizationConstraints, thePlan, true, 0.0);
                }
                else
                {
                    MessageBox.Show($"Error! Could not find requested plan: {itr.PlanId}! Exiting");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Preliminary checks to ensure the requested optimization loop settings are good to go
        /// </summary>
        /// <returns></returns>
        private (bool, double, int) PreliminaryChecksOptimizationLoopStart()
        {
            double planNorm = 0.0;
            int numOptimizations = -1;
            if (!plans.Any())
            {
                MessageBox.Show("No plans found!");
                return (true, planNorm, numOptimizations);
            }
            if (!plans.All(x => string.Equals(x.Course.Id, plans.First().Course.Id)))
            {
                MessageBox.Show("Error! Plans parsed from log file belong to separate courses! They must exist in the source course! Please fix and try again");
                return (true, planNorm, numOptimizations);
            }
            if (!plans.All(x => string.Equals(plans.First().StructureSet.UID, x.StructureSet.UID)))
            {
                MessageBox.Show("Error! Base plan and boost plan do NOT share the same structure set! Update plan selection and try again");
                return (true, planNorm, numOptimizations);
            }
            if(planType == PlanType.VMAT_CSI && plans.Count > 1 && plans.All(x => string.Equals(x.UID, plans.First().UID)))
            {
                MessageBox.Show("Error! Base and boost plans are the same! Cannot proceed with optimization! Fix and try again");
                return (true, planNorm, numOptimizations);
            }
            if (planObjectiveParamSP.Children.Count == 0)
            {
                MessageBox.Show("No plan objectives present! Please add plan objectives and try again!");
                return (true, planNorm, numOptimizations);
            }
            if (optimizationParamSP.Children.Count == 0)
            {
                MessageBox.Show("No optimization parameters present to assign to the VMAT plan!");
                return (true, planNorm, numOptimizations);
            }
            if (string.Equals(initNormVolumeCB.SelectedItem.ToString(),"--select--", StringComparison.OrdinalIgnoreCase) || (planType == PlanType.VMAT_CSI && plans.Count > 1 && string.Equals(bstNormVolumeCB.SelectedItem.ToString(), "--select--",StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Error! Normalization volumes are not appropriately assigned! Please fix and try again!");
                return (true, planNorm, numOptimizations);
            }
            if (!int.TryParse(numOptLoops.Text, out numOptimizations))
            {
                MessageBox.Show("Error! Invalid input for number of optimization loops! \nFix and try again.");
                return (true, planNorm, numOptimizations);
            }
            if (!double.TryParse(targetNormTB.Text, out planNorm))
            {
                MessageBox.Show("Error! Target normalization is NaN \nFix and try again.");
                return (true, planNorm, numOptimizations);
            }
            if (planNorm < 0.0 || planNorm > 100.0)
            {
                MessageBox.Show("Error! Target normalization is is either < 0% or > 100% \nExiting!");
                return (true, planNorm, numOptimizations);
            }
            if (numOptimizations < 1)
            {
                MessageBox.Show("Number of requested optimizations needs to be greater than or equal to 1.\nExiting!");
                return (true, planNorm, numOptimizations);
            }
            return (false, planNorm, numOptimizations);
        }
        #endregion

        #region script and configuration
        /// <summary>
        /// Simple helper method print the loaded configuration parameters to the UI on the Script Configuration tab
        /// </summary>
        private void DisplayConfigurationParameters()
        {
            configTB.Text = "";
            configTB.Text = $"{DateTime.Now}" + Environment.NewLine;
            if (!string.Equals(configFile, "")) configTB.Text += $"Configuration file: {configFile}" + Environment.NewLine + Environment.NewLine;
            else configTB.Text += "Configuration file: none" + Environment.NewLine + Environment.NewLine;
            configTB.Text += $"Documentation path: {documentationPath}" + Environment.NewLine + Environment.NewLine;
            configTB.Text += $"Log file path: {logFilePath}" + Environment.NewLine + Environment.NewLine;
            configTB.Text += "Default run parameters:" + Environment.NewLine;
            configTB.Text += $"Demo mode: {demo}" + Environment.NewLine;
            configTB.Text += $"Run coverage check: {runCoverageCheckOption}" + Environment.NewLine;
            configTB.Text += $"Run additional optimization: {runAdditionalOptOption}" + Environment.NewLine;
            configTB.Text += $"Copy and save each optimized plan: {copyAndSaveOption}" + Environment.NewLine;
            configTB.Text += $"Plan normalization: {defaultPlanNorm}% (i.e., PTV V100% = {defaultPlanNorm}%)" + Environment.NewLine;
            configTB.Text += $"Decision threshold: {threshold}" + Environment.NewLine;
            configTB.Text += $"Relative lower dose limit: {lowDoseLimit}" + Environment.NewLine + Environment.NewLine;

            if(PlanTemplates.Any()) configTB.Text += ConfigurationUIHelper.PrintPlanTemplateConfigurationParameters(PlanTemplates.ToList()).ToString();
            configScroller.ScrollToTop();

            //set the default parameters for the optimization loop
            runCoverageCk.IsChecked = runCoverageCheckOption;
            numOptLoops.Text = defautlNumOpt;
            runAdditionalOpt.IsChecked = runAdditionalOptOption;
            copyAndSave.IsChecked = copyAndSaveOption;
            targetNormTB.Text = defaultPlanNorm;
        }

        /// <summary>
        /// Simple method to load a new configuration .ini file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadNewConfigFile_Click(object sender, RoutedEventArgs e)
        {
            configFile = "";
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\";
            openFileDialog.Filter = "ini files (*.ini)|*.ini|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog().Value) 
            { 
                if (!LoadConfigurationSettings(openFileDialog.FileName)) 
                { 
                    if (pi != null) DisplayConfigurationParameters(); 
                } 
                else MessageBox.Show("Error! Selected file is NOT valid!"); 
            }
        }

        /// <summary>
        /// Method to determine which set of configuration parameters to load depending on the type of plan being considered
        /// </summary>
        /// <param name="type"></param>
        private void LoadConfigurationSettingsForPlanType(PlanType type)
        {
            List<string> configurationFiles = new List<string> { };
            if (type == PlanType.VMAT_CSI)
            {
                configurationFiles.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\VMAT_CSI_config.ini");
                configurationFiles.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\CSI_optimization_config.ini");
            }
            else
            {
                configurationFiles.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\VMAT_TBI_config.ini");
                configurationFiles.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\TBI_optimization_config.ini");
            }
            foreach (string itr in configurationFiles)
            {
                if (File.Exists(itr)) LoadConfigurationSettings(itr);
            }
        }

        /// <summary>
        /// Utility method to read the configuration .ini file and load the requested settings into memory
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private bool LoadConfigurationSettings(string file)
        {
            configFile = file;
            try
            {
                reminders.Clear();
                using (StreamReader reader = new StreamReader(configFile))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrEmpty(line) && line.Substring(0, 1) != "%")
                        {
                            //useful info on this line
                            if (line.Contains("="))
                            {
                                string parameter = line.Substring(0, line.IndexOf("="));
                                string value = line.Substring(line.IndexOf("=") + 1, line.Length - line.IndexOf("=") - 1);
                                if (double.TryParse(value, out double result))
                                {
                                    if (parameter == "default number of optimizations") defautlNumOpt = value;
                                    else if (parameter == "default plan normalization") defaultPlanNorm = value;
                                    else if (parameter == "decision threshold") threshold = result;
                                    else if (parameter == "relative lower dose limit") lowDoseLimit = result;
                                }
                                else if (parameter == "documentation path")
                                {
                                    if (!string.IsNullOrEmpty(value))
                                    {
                                        string path = ConfigurationHelper.VerifyPathIntegrity(value);
                                        if (!string.IsNullOrEmpty(path)) documentationPath = path;
                                    }
                                }
                                else if (parameter == "demo") 
                                { 
                                    if (value != "") demo = bool.Parse(value); 
                                }
                                else if (parameter == "run coverage check") 
                                { 
                                    if (value != "") runCoverageCheckOption = bool.Parse(value); 
                                }
                                else if (parameter == "run additional optimization") 
                                { 
                                    if (value != "") runAdditionalOptOption = bool.Parse(value); 
                                }
                                else if (parameter == "copy and save each plan") 
                                {
                                    if (value != "") copyAndSaveOption = bool.Parse(value); 
                                }
                            }
                            else if (line.Contains("add reminder"))
                            {
                                reminders.Add(line.Substring(line.IndexOf("{") + 1, line.IndexOf("}") - line.IndexOf("{") - 1));
                            }
                        }
                    }
                    reader.Close();
                }
                return false;
            }
            catch (Exception e) 
            { 
                MessageBox.Show($"Error could not load configuration file because: {e.Message}\n\nAssuming default parameters"); 
                return true;
            }
        }

        /// <summary>
        /// Helper method to read all the plan template files from the appropriate directory depending on the plan type being considered
        /// </summary>
        /// <param name="type"></param>
        private void LoadTemplatePlanChoices(PlanType type)
        {
            int count = 1;
            SearchOption option = SearchOption.AllDirectories;
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\templates\\";
            PlanTemplates.Clear();
            if (type == PlanType.VMAT_CSI) path += "CSI\\";
            else path += "TBI\\";
            try
            {
                foreach (string itr in Directory.GetFiles(path, "*.ini", option).OrderBy(x => x))
                {
                    if(type == PlanType.VMAT_CSI) PlanTemplates.Add(ConfigurationHelper.ReadCSITemplatePlan(itr, count++));
                    else PlanTemplates.Add(ConfigurationHelper.ReadTBITemplatePlan(itr, count++));

                }
            }
            catch(Exception e)
            {
                MessageBox.Show($"Error could not load plan template file because: {e.Message}!");
            }
            selectedTemplate = PlanTemplates.FirstOrDefault(x => string.Equals(x.TemplateName, selectedTemplateName));
            if (selectedTemplate != null) templateList.SelectedItem = selectedTemplate;
        }

        /// <summary>
        /// Utility method to read the log file from the preparation script for the selected patient 
        /// and store the information so it can be used by this script
        /// </summary>
        /// <param name="fullLogName"></param>
        /// <returns></returns>
        private bool LoadLogFile(string fullLogName)
        {
            try
            {
                prescriptions = new List<PrescriptionModel> { };
                planUIDs = new List<string> { };
                tsTargets = new Dictionary<string, string> { };
                normalizationVolumes = new Dictionary<string, string> { };
                optConstraintsFromLogs = new List<PlanOptimizationSetupModel> { };
                using (StreamReader reader = new StreamReader(fullLogName))
                {
                    string line;
                    while (!(line = reader.ReadLine()).Equals("Errors and warnings:"))
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            //useful info on this line
                            if (line.Contains("="))
                            {
                                string parameter = line.Substring(0, line.IndexOf("="));
                                string value = line.Substring(line.IndexOf("=") + 1, line.Length - line.IndexOf("=") - 1);
                                if (parameter == "Plan type")
                                {
                                    if(value.Contains("CSI")) planType = PlanType.VMAT_CSI;
                                    else planType = PlanType.VMAT_TBI;
                                    planTypeLabel.Content = planType;
                                }
                                else if (parameter == "Template")
                                {
                                    //plan objectives will be updated in OpenPatient method
                                    selectedTemplateName = value;
                                }
                            }
                            else if (line.Contains("Prescriptions:"))
                            {
                                while (!string.IsNullOrEmpty((line = reader.ReadLine().Trim())))
                                {
                                    prescriptions.Add(LogHelper.ParsePrescriptionsFromLogFile(line));
                                }
                            }
                            else if (line.Contains("Plan UIDs:"))
                            {
                                while (!string.IsNullOrEmpty((line = reader.ReadLine().Trim())))
                                {
                                    planUIDs.Add(line);
                                }
                            }
                            else if (line.Contains("TS Targets:"))
                            {
                                while (!string.IsNullOrEmpty((line = reader.ReadLine().Trim())))
                                {
                                    KeyValuePair<string, string> tsTGT = LogHelper.ParseKeyValuePairFromLogFile(line);
                                    tsTargets.Add(tsTGT.Key, tsTGT.Value);
                                }
                            }
                            else if (line.Contains("Normalization volumes:"))
                            {
                                while (!string.IsNullOrEmpty((line = reader.ReadLine().Trim())))
                                {
                                    KeyValuePair<string, string> normVol = LogHelper.ParseKeyValuePairFromLogFile(line);
                                    normalizationVolumes.Add(normVol.Key, normVol.Value);
                                }
                            }
                            else if (line.Contains("Optimization constraints:"))
                            {
                                string planId = "";
                                List<OptimizationConstraintModel> tmpConstraints = new List<OptimizationConstraintModel> { };
                                while (!string.IsNullOrEmpty((line = reader.ReadLine().Trim())))
                                {
                                    if (!line.Contains("{"))
                                    {
                                        if(tmpConstraints.Any())
                                        {
                                            optConstraintsFromLogs.Add(new PlanOptimizationSetupModel(planId, new List<OptimizationConstraintModel>(tmpConstraints)));
                                        }
                                        planId = line;
                                        tmpConstraints = new List<OptimizationConstraintModel> { };
                                    }
                                    else
                                    {
                                        tmpConstraints.Add(ConfigurationHelper.ParseOptimizationConstraint(line));
                                    }
                                }
                                if (tmpConstraints.Any())
                                {
                                    optConstraintsFromLogs.Add(new PlanOptimizationSetupModel(planId, new List<OptimizationConstraintModel>(tmpConstraints)));
                                }
                            }
                        }
                    }
                }
                return false;
            }
            catch (Exception e) 
            { 
                MessageBox.Show($"Error could not load log file because: {e.Message}");
                return true;
            }
        }
        #endregion

        /// <summary>
        /// Window closing even
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //be sure to close the patient before closing the application. Not doing so will result in unclosed timestamps in eclipse
            if(app != null)
            {
                app.ClosePatient();
                app.Dispose();
            }
        }
    }
}
