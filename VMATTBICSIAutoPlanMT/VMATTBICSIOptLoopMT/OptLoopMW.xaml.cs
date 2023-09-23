using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using Microsoft.Win32;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.Structs;
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

namespace VMATTBICSIOptLoopMT
{
    public partial class OptLoopMW : Window
    {
        //configuration file
        string configFile = "";
        //point this to the directory holding the documentation files
        string documentationPath;
        //default number of optimizations to perform
        string defautlNumOpt = "3";
        //default plan normaliBzation (i.e., PTV100% = 90%) 
        string defaultPlanNorm = "90";
        //run coverage check
        bool runCoverageCheckOption = false;
        //run additional optimization option
        bool runAdditionalOptOption = true;
        //copy and save each optimized plan
        bool copyAndSaveOption = false;
        //is demo
        bool demo = false;
        //log file directory
        string logFilePath;
        //decision threshold
        double threshold = 0.15;
        //lower dose limit
        double lowDoseLimit = 0.1;

        //plan id, list<structure id, optimization objective type, dose, volume, priority>
        List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> optConstraintsFromLogs = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };

        //structure, constraint type, dose, relative volume, dose value presentation (unless otherwise specified)
        //note, if the constraint type is "mean", the relative volume value is ignored
        public List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> planObj = new List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> { };

        //ID, lower dose level, upper dose level, volume (%), priority, list of criteria that must be met to add the requested cooler/heater structures
        public List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> requestedTSstructures = new List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>>{ };

        //structure id(or can put '<plan>' to get the plan dose value), metric requested(Dmax, Dmin, D<vol %>, V<dose %>), query value, return value representation(dose or volume as absolute or relative)
        public List<Tuple<string, string, double, string>> planDoseInfo = new List<Tuple<string, string, double, string>> { };

        private VMS.TPS.Common.Model.API.Application app = null;

        List<ExternalPlanSetup> plans;
        StructureSet selectedSS;
        Patient pi = null;
        bool runCoverageCheck = false;
        bool runOneMoreOpt = false;
        bool copyAndSavePlanItr = false;
        bool useFlash = false;
        bool logFileLoaded = false;
        //ATTENTION! THE FOLLOWING LINE HAS TO BE FORMATTED THIS WAY, OTHERWISE THE DATA BINDING WILL NOT WORK!
        public ObservableCollection<AutoPlanTemplateBase> PlanTemplates { get; set; }
        public AutoPlanTemplateBase selectedTemplate;
        string selectedTemplateName = "";
        //to be read from the plan prep log files
        PlanType planType;
        List<string> planUIDs = new List<string> { };
        //plan id, target id, num fx, dose per fx, cumulative rx for this target
        List<Tuple<string, string, int, DoseValue, double>> prescriptions = new List<Tuple<string, string, int, DoseValue, double>> { };
        //plan id, volume id
        List<Tuple<string, string>> normalizationVolumes = new List<Tuple<string, string>> { };
        //list<original target id, ts target id>
        private List<Tuple<string, string>> tsTargets = new List<Tuple<string, string>> { };

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
            if(app != null)
            {
                string patmrn = "";
                if (args.Any()) patmrn = args[0];

                LoadPatient(patmrn);
            }
            
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
        private void LoadPatient(string patmrn)
        {
            if (app == null) return;
            string currentMRN;
            if (pi != null) currentMRN = pi.Id;
            else currentMRN = "-1";
            string mrn = patmrn;
            string fullLogName;
            bool cancel = false;
            if (string.IsNullOrEmpty(patmrn))
            {
                (bool, string, string) result = PromptUserForPatientSelection();
                cancel = result.Item1;
                mrn = result.Item2;
                fullLogName = result.Item3;
            }
            else
            {
                fullLogName = LogHelper.GetFullLogFileFromExistingMRN(mrn, logFilePath);
            }
            if (!cancel)
            {
                if (!string.IsNullOrEmpty(mrn))
                {
                    if(!string.Equals(mrn,currentMRN))
                    {
                        planUIDs = new List<string> { };
                        if (!string.IsNullOrEmpty(fullLogName))
                        {
                            if (!LoadLogFile(fullLogName)) logFileLoaded = true;
                        }
                        LoadConfigurationSettingsForPlanType(planType);
                        OpenPatient(mrn);
                        LoadTemplatePlanChoices(planType);
                        selectPatientBtn.Background = System.Windows.Media.Brushes.DarkGray;
                    }
                }
                else MessageBox.Show($"Entered MRN: {mrn} is invalid! Please re-enter and try again");
            }
            else if (pi == null) selectPatientBtn.Background = System.Windows.Media.Brushes.PaleVioletRed;
        }

        private (bool, string, string) PromptUserForPatientSelection()
        {
            //open the patient with the user-entered MRN number
            bool cancel = false;
            string mrn = "";
            string fullLogName = "";
            SelectPatient sp = new SelectPatient(logFilePath);
            sp.ShowDialog();
            if (!sp.selectionMade)
            {
                cancel = true;
                return (cancel, mrn, fullLogName); ;
            }
            else
            {
                (string, string) result = sp.GetPatientMRN();
                mrn = result.Item1;
                fullLogName = result.Item2;
            }

            return (cancel, mrn, fullLogName);
        }

        private void OpenPatient(string pat_mrn)
        {
            try
            {
                ClearEverything();
                app.ClosePatient();
                pi = app.OpenPatientById(pat_mrn);
                PatMRNLabel.Content = pat_mrn;
                //grab instances of the course and VMAT tbi plans that were created using the binary plug in script. This is explicitly here to let the user know if there is a problem with the course OR plan
                //Course c = pi.Courses.FirstOrDefault(x => x.Id.ToLower() == "vmat tbi");
                (plans, selectedSS) = GetStructureSetAndPlans();
                if (!plans.Any())
                {
                    MessageBox.Show("No plans found!");
                    return;
                }
                //ensure the correct plan target is selected and all requested objectives have a matching structure that exists in the structure set (needs to be done after structure set has been assinged)
                PopulateOptimizationTab(optimizationParamSP);

                //populate the prescription text boxes with the prescription stored in the VMAT TBI plan
                PopulateRx();

                planObjectiveHeader.Background = System.Windows.Media.Brushes.PaleVioletRed;
            }
            catch
            {
                MessageBox.Show("No such patient exists!");
            }
        }
        #endregion

        #region button events
        private void SelectPatient_Click(object sender, RoutedEventArgs e)
        {
            LoadPatient("");
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
                        foreach (Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>> itr in optConstraintsFromLogs) AddListItemsToUI(itr.Item2, itr.Item1, optimizationParamSP);
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

                List<Tuple<string, OptimizationObjectiveType, double, double, int>> tmp = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { Tuple.Create("--select--", OptimizationObjectiveType.None, 0.0, 0.0, 0) };
                List<List<Tuple<string, OptimizationObjectiveType, double, double, int>>> tmpList = new List<List<Tuple<string, OptimizationObjectiveType, double, double, int>>> { };
                if (SPAndSV.Item1.Children.Count > 0)
                {
                    List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> optParametersListList = OptimizationSetupUIHelper.ParseOptConstraints(SPAndSV.Item1, false).Item1;
                    foreach (Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>> itr in optParametersListList)
                    {
                        if (itr.Item1 == thePlan.Id)
                        {
                            tmp = new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(itr.Item2)
                            {
                                Tuple.Create("--select--", OptimizationObjectiveType.None, 0.0, 0.0, 0)
                            };
                            tmpList.Add(tmp);
                        }
                        else tmpList.Add(itr.Item2);
                    }
                }
                else
                {
                    tmpList.Add(tmp);
                }
                ClearAllItemsFromUIList(SPAndSV.Item1);
                int count = 0;
                foreach (List<Tuple<string, OptimizationObjectiveType, double, double, int>> itr in tmpList) AddListItemsToUI(itr, plans.ElementAt(count++).Id, SPAndSV.Item1);
            }
            else
            {
                List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> tmp = new List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> 
                { 
                    Tuple.Create("--select--", OptimizationObjectiveType.None, 0.0, 0.0, DoseValuePresentation.Relative) 
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
                planObj = new List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>>(PlanObjectiveHelper.ConstructPlanObjectives(selectedTemplate.GetPlanObjectives(), selectedSS, tsTargets));
                PopulatePlanObjectivesTab(planObjectiveParamSP);
                planDoseInfo = new List<Tuple<string, string, double, string>>(selectedTemplate.GetRequestedPlanDoseInfo());
                requestedTSstructures = new List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>>(selectedTemplate.GetRequestedOptTSStructures());
                if (selectedTemplate.GetPlanObjectives().Any())
                {
                    planObjectiveHeader.Background = System.Windows.Media.Brushes.ForestGreen;
                    optimizationSetupHeader.Background = System.Windows.Media.Brushes.PaleVioletRed;
                }
            }
            else
            {
                templateList.UnselectAll();
                planObj = new List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>>();
                ClearAllItemsFromUIList(planObjectiveParamSP);
                planDoseInfo = new List<Tuple<string, string, double, string>>();
                requestedTSstructures = new List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>>();
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
        /// Helper method to retrieve the structure set and list of plans
        /// </summary>
        /// <returns></returns>
        private (List<ExternalPlanSetup>, StructureSet) GetStructureSetAndPlans()
        {
            List<ExternalPlanSetup> thePlans = new List<ExternalPlanSetup> { };
            StructureSet ss = null;
            //grab an instance of the VMAT TBI plan. Return null if it isn't found
            if (pi == null) return (thePlans, ss);
            if(planUIDs.Any())
            {
                //should automatically be in order in terms of cumulative Rx (lowest to highest)
                foreach(string uid in planUIDs)
                {
                    ExternalPlanSetup tmp = pi.Courses.SelectMany(x => x.ExternalPlanSetups).FirstOrDefault(x => string.Equals(x.UID, uid));
                    if(tmp != null) thePlans.Add(tmp);
                }
            }
            else
            {
                //simple logic to try and guess which plans are which
                Course theCourse = null;
                List<Course> courses = pi.Courses.Where(x => x.Id.ToLower().Contains("vmat csi") || x.Id.ToLower().Contains("vmat tbi")).ToList();
                if (!courses.Any()) return (thePlans, ss);
                if (courses.Count > 1)
                {
                    SelectItemPrompt SIP = new SelectItemPrompt("Please select a course:", courses.Select(x => x.Id).ToList());
                    SIP.ShowDialog();
                    if (!SIP.GetSelection()) return (thePlans, ss);
                    theCourse = courses.FirstOrDefault(x => string.Equals(x.Id, SIP.GetSelectedItem()));
                }
                else theCourse = courses.First();
                if (theCourse.Id.ToLower().Contains("csi"))
                {
                    planType = PlanType.VMAT_CSI;
                    planTypeLabel.Content = "VMAT CSI";
                }
                else
                {
                    planType = PlanType.VMAT_TBI;
                    planTypeLabel.Content = "VMAT TBI";
                }

                thePlans = theCourse.ExternalPlanSetups.OrderBy(x => x.CreationDateTime).ToList();
                if (thePlans.Count > 2)
                {
                    MessageBox.Show($"Error! More than two plans found in course: {theCourse.Id}! Unable to determine which plan(s) should be used for optimization! Exiting!");
                    thePlans = new List<ExternalPlanSetup> { };
                }
                else if (thePlans.Count < 1)
                {
                    MessageBox.Show($"Error! No plans found in course: {theCourse.Id}! Unable to determine which plan(s) should be used for optimization! Exiting!");
                }
                else if (thePlans.Count == 2 && (thePlans.First().StructureSet != thePlans.Last().StructureSet))
                {
                    MessageBox.Show($"Error! Structure set in first plan ({thePlans.First().Id}) is not the same as the structure set in second plan ({thePlans.Last().Id})! Exiting!");
                    thePlans = new List<ExternalPlanSetup> { };
                }
            }
            if (thePlans.Any()) ss = thePlans.First().StructureSet;

            return (thePlans, ss);
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
            initDosePerFxTB.Text = initNumFxTB.Text = initRxTB.Text = numOptLoops.Text = "";
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
            if (normalizationVolumes.Any())
            {
                initNormVolume.Text = normalizationVolumes.First().Item2;
                if(normalizationVolumes.Count > 1) bstNormVolume.Text = normalizationVolumes.Last().Item2;
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
            foreach(ExternalPlanSetup itr in plans) AddListItemsToUI(OptimizationSetupUIHelper.ReadConstraintsFromPlan(itr), itr.Id, theSP);
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

        private void AddListItemsToUI<T>(List<Tuple<string, OptimizationObjectiveType, double, double, T>> defaultList, string planId, StackPanel theSP)
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
            for (int i = 0; i < defaultList.Count; i++)
            {
                counter++;
                theSP.Children.Add(OptimizationSetupUIHelper.AddOptVolume(theSP, 
                                                       selectedSS, 
                                                       defaultList[i], 
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

            (List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>, StringBuilder) parsedOptimizationConstraints = OptimizationSetupUIHelper.ParseOptConstraints(optimizationParamSP);
            if (!parsedOptimizationConstraints.Item1.Any())
            {
                MessageBox.Show(parsedOptimizationConstraints.Item2.ToString());
                return;
            }
            List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> objectives = PlanObjectiveSetupUIHelper.ParsePlanObjectives(planObjectiveParamSP);
            if (!objectives.Any())
            {
                MessageBox.Show("Error! Missing plan objectives! Please add plan objectives and try again!");
                return;
            }
            //determine if flash was used to prep the plan
            if (parsedOptimizationConstraints.Item1.Any(x => x.Item2.Any(y => y.Item1.ToLower().Contains("flash")))) useFlash = true;

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
            OptDataContainer data = new OptDataContainer();
            data.Construct(plans, 
                           prescriptions,
                           normalizationVolumes,
                           objectives, 
                           requestedTSstructures, 
                           planDoseInfo,
                           planType,
                           planNorm, 
                           numOptimizations, 
                           runCoverageCheck, 
                           runOneMoreOpt, 
                           copyAndSavePlanItr, 
                           useFlash, 
                           threshold, 
                           lowDoseLimit, 
                           demo, 
                           logFilePath, 
                           app);

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
        private bool AssignRequestedOptimizationConstraints(List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> constraints)
        {
            foreach (Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>> itr in constraints)
            {
                ExternalPlanSetup thePlan = null;
                //additional check if the plan was not found in the list of VMATplans
                thePlan = plans.FirstOrDefault(x => string.Equals(x.Id, itr.Item1));
                if (thePlan != null)
                {
                    OptimizationSetupUIHelper.RemoveOptimizationConstraintsFromPLan(thePlan);
                    OptimizationSetupUIHelper.AssignOptConstraints(itr.Item2, thePlan, true, 0.0);
                }
                else
                {
                    MessageBox.Show($"Error! Could not find requested plan: {itr.Item1}! Exiting");
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
            if (optimizationParamSP.Children.Count == 0)
            {
                MessageBox.Show("No optimization parameters present to assign to the VMAT plan!");
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
            selectedTemplate = PlanTemplates.FirstOrDefault(x => string.Equals(x.GetTemplateName(), selectedTemplateName));
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
                                    tsTargets.Add(LogHelper.ParseKeyValuePairFromLogFile(line));
                                }
                            }
                            else if (line.Contains("Normalization volumes:"))
                            {
                                while (!string.IsNullOrEmpty((line = reader.ReadLine().Trim())))
                                {
                                    normalizationVolumes.Add(LogHelper.ParseKeyValuePairFromLogFile(line));
                                }
                            }
                            else if (line.Contains("Optimization constraints:"))
                            {
                                string planId = "";
                                List<Tuple<string, OptimizationObjectiveType, double, double, int>> tmpConstraints = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
                                while (!string.IsNullOrEmpty((line = reader.ReadLine().Trim())))
                                {
                                    if (!line.Contains("{"))
                                    {
                                        if(tmpConstraints.Any())
                                        {
                                            optConstraintsFromLogs.Add(new Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>(planId, new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(tmpConstraints)));
                                        }
                                        planId = line;
                                        tmpConstraints = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
                                    }
                                    else
                                    {
                                        tmpConstraints.Add(ConfigurationHelper.ParseOptimizationConstraint(line));
                                    }
                                }
                                if (tmpConstraints.Any())
                                {
                                    optConstraintsFromLogs.Add(new Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>(planId, new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(tmpConstraints)));
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
