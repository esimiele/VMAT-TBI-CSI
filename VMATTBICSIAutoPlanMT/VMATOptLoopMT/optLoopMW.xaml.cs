using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using Microsoft.Win32;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIOptLoopMT.Helpers;
using VMATTBICSIOptLoopMT.baseClasses;
using VMATTBICSIAutoplanningHelpers.Helpers;
using VMATTBICSIAutoplanningHelpers.UIHelpers;
using VMATTBICSIAutoplanningHelpers.Prompts;
using VMATTBICSIAutoplanningHelpers.TemplateClasses;
using VMATTBICSIOptLoopMT.VMAT_CSI;
using VMATTBICSIOptLoopMT.VMAT_TBI;
using VMATTBICSIOptLoopMT.Prompts;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;

namespace VMATTBICSIOptLoopMT
{
    public partial class OptLoopMW : Window
    {
        //configuration file
        string configFile = "";
        //point this to the directory holding the documentation files
        string documentationPath = @"\\enterprise.stanfordmed.org\depts\RadiationTherapy\Public\Users\ESimiele\Research\VMAT_TBI\documentation\";
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
        string logFilePath = @"\\enterprise.stanfordmed.org\depts\RadiationTherapy\Public\Users\ESimiele\Research\VMAT_TBI\log_files";
        //decision threshold
        double threshold = 0.15;
        //lower dose limit
        double lowDoseLimit = 0.1;

        //structure, constraint type, dose, relative volume, dose value presentation (unless otherwise specified)
        //note, if the constraint type is "mean", the relative volume value is ignored
        public List<Tuple<string, string, double, double, DoseValuePresentation>> planObj = new List<Tuple<string, string, double, double, DoseValuePresentation>> { };

        //ID, lower dose level, upper dose level, volume (%), priority, list of criteria that must be met to add the requested cooler/heater structures
        public List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> requestedTSstructures = new List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>>{ };

        //structure id(or can put '<plan>' to get the plan dose value), metric requested(Dmax, Dmin, D<vol %>, V<dose %>), return value representation(dose or volume as absolute or relative)
        public List<Tuple<string, string, double, string>> planDoseInfo = new List<Tuple<string, string, double, string>> { };

        VMS.TPS.Common.Model.API.Application app = VMS.TPS.Common.Model.API.Application.CreateApplication();
        List<ExternalPlanSetup> plans;
        StructureSet selectedSS;
        Patient pi = null;
        bool runCoverageCheck = false;
        bool runOneMoreOpt = false;
        bool copyAndSavePlanItr = false;
        bool useFlash = false;
        //ATTENTION! THE FOLLOWING LINE HAS TO BE FORMATTED THIS WAY, OTHERWISE THE DATA BINDING WILL NOT WORK!
        public ObservableCollection<CSIAutoPlanTemplate> PlanTemplates { get; set; }
        public CSIAutoPlanTemplate selectedTemplate;
        string selectedTemplateName = "";
        //to be read from the plan prep log files
        VMATTBICSIAutoplanningHelpers.Helpers.PlanType planType;
        List<string> planUIDs = new List<string> { };
        //plan id, target id, num fx, dose per fx, cumulative rx for this target
        List<Tuple<string, string, int, DoseValue, double>> prescriptions = new List<Tuple<string, string, int, DoseValue, double>> { };
        //plan id, volume id
        List<Tuple<string, string>> normalizationVolumes = new List<Tuple<string, string>> { };

        public OptLoopMW(string[] args)
        {
            InitializeComponent();
            PlanTemplates = new ObservableCollection<CSIAutoPlanTemplate>() { new CSIAutoPlanTemplate("--select--") };
            DataContext = this;
            string patmrn = "";
            List<string> configurationFiles = new List<string> { Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\General_configuration.ini" };
            for (int i = 0; i < args.Length; i++)
            {
                if (i == 0) patmrn = args[i];
                if (i == 1) configurationFiles.Add(args[i]);
            }
            
            foreach(string itr in configurationFiles)
            {
                if (File.Exists(itr))
                {
                    loadConfigurationSettings(itr);
                }
            }
            
            if (args.Length > 0) OpenPatient(patmrn);
            else LoadPatient();
            DisplayConfigurationParameters();
        }

        #region help and info buttons
        private void Help_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(documentationPath + "VMAT_TBI_guide.pdf");
        }

        private void QuickStart_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(documentationPath + "TBI_executable_quickStart_guide.pdf");
        }

        private void targetNormInfo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This is used to set the plan normalization. What percentage of the PTV volume should recieve the prescription dose?");
        }
        #endregion

        #region load and open patient
        private void LoadPatient()
        {
            //open the patient with the user-entered MRN number
            SelectPatient sp = new SelectPatient(logFilePath);
            sp.ShowDialog();
            if (sp.selectionMade)
            {
                string currentMRN;
                if (pi != null) currentMRN = pi.Id;
                else currentMRN = "-1";
                (string mrn, string fullLogName) = sp.GetPatientMRN();
                if (!string.IsNullOrEmpty(mrn))
                {
                    if(!string.Equals(mrn,currentMRN))
                    {
                        planUIDs = new List<string> { };
                        if (!string.IsNullOrEmpty(fullLogName))
                        {
                            LoadLogFile(fullLogName);
                        }

                        string planTypeSpecificConfigurationSettings;
                        if (planType == VMATTBICSIAutoplanningHelpers.Helpers.PlanType.VMAT_CSI) planTypeSpecificConfigurationSettings = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\VMAT_CSI_config.ini";
                        else planTypeSpecificConfigurationSettings = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\VMAT_TBI_config.ini";
                        loadConfigurationSettings(planTypeSpecificConfigurationSettings);

                        OpenPatient(mrn);
                        LoadTemplatePlanChoices(planType);
                    }
                }
                else MessageBox.Show(String.Format("Entered MRN: {0} is invalid! Please re-enter and try again", mrn));
                selectPatientBtn.Background = System.Windows.Media.Brushes.DarkGray;
            }
            else if (pi == null) selectPatientBtn.Background = System.Windows.Media.Brushes.PaleVioletRed;
        }

        private void OpenPatient(string pat_mrn)
        {
            try
            {
                clearEverything();
                app.ClosePatient();
                pi = app.OpenPatientById(pat_mrn);
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
                populateRx();

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
            LoadPatient();
        }

        private void getOptFromPlan_Click(object sender, RoutedEventArgs e)
        {
            if (pi != null && plans.Any()) PopulateOptimizationTab(optimizationParamSP);
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
                    selectItem SUI = new selectItem();
                    SUI.title.Text = "Please selct a plan to add a constraint!";
                    foreach (ExternalPlanSetup itr in plans) SUI.itemCombo.Items.Add(itr.Id);
                    //SUI.itemCombo.Items.Add("Both");
                    SUI.itemCombo.SelectedIndex = 0;
                    SUI.ShowDialog();
                    if (SUI.confirm) thePlan = plans.FirstOrDefault(x => x.Id == SUI.itemCombo.SelectedItem.ToString());
                    else return;
                    if (thePlan == null) { MessageBox.Show("Plan not found! Exiting!"); return; }
                }
                else thePlan = plans.First();

                List<Tuple<string, string, double, double, int>> tmp = new List<Tuple<string, string, double, double, int>> { Tuple.Create("--select--", "--select--", 0.0, 0.0, 0) };
                List<List<Tuple<string, string, double, double, int>>> tmpList = new List<List<Tuple<string, string, double, double, int>>> { };
                if (SPAndSV.Item1.Children.Count > 0)
                {
                    OptimizationSetupUIHelper helper = new OptimizationSetupUIHelper();
                    List<Tuple<string, List<Tuple<string, string, double, double, int>>>> optParametersListList = helper.ParseOptConstraints(SPAndSV.Item1, false).Item1;
                    foreach (Tuple<string, List<Tuple<string, string, double, double, int>>> itr in optParametersListList)
                    {
                        if (itr.Item1 == thePlan.Id)
                        {
                            tmp = new List<Tuple<string, string, double, double, int>>(itr.Item2);
                            tmp.Add(Tuple.Create("--select--", "--select--", 0.0, 0.0, 0));
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
                foreach (List<Tuple<string, string, double, double, int>> itr in tmpList) AddListItemsToUI(itr, plans.ElementAt(count++).Id, SPAndSV.Item1);
            }
            else
            {
                List<Tuple<string, string, double, double, DoseValuePresentation>> tmp = new List<Tuple<string, string, double, double, DoseValuePresentation>> 
                { 
                    Tuple.Create("--select--", "--select--", 0.0, 0.0, DoseValuePresentation.Relative) 
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
            if (new GeneralUIhelper().ClearRow(sender, theSP)) ClearAllItemsFromUIList(theSP);
        }
        #endregion

        #region UI manipulation
        private void Templates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pi == null) return;
            selectedTemplate = templateList.SelectedItem as CSIAutoPlanTemplate;
            if (selectedTemplate == null) return;
            UpdateSelectedTemplate();
        }

        private void UpdateSelectedTemplate()
        {
            if (selectedTemplate != null && selectedSS != null)
            {
                ClearAllItemsFromUIList(planObjectiveParamSP);
                //requires a structure set to properly function
                planObj = new List<Tuple<string, string, double, double, DoseValuePresentation>>(ConstructPlanObjectives(selectedTemplate.GetPlanObjectives()));
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
                planObj = new List<Tuple<string, string, double, double, DoseValuePresentation>>();
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
                    ExternalPlanSetup tmp = pi.Courses.SelectMany(x => x.ExternalPlanSetups).FirstOrDefault(x => x.UID == uid);
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
                    selectItem SI = new selectItem();
                    SI.title.Text = "Please a course:";
                    foreach (Course itr in courses) SI.itemCombo.Items.Add(itr.Id);
                    SI.itemCombo.SelectedIndex = 0;
                    SI.ShowDialog();
                    if (!SI.confirm) return (thePlans, ss);
                    theCourse = courses.FirstOrDefault(x => x.Id == SI.itemCombo.SelectedItem.ToString());
                }
                else theCourse = courses.First();
                if (theCourse.Id.ToLower().Contains("csi"))
                {
                    planType = VMATTBICSIAutoplanningHelpers.Helpers.PlanType.VMAT_CSI;
                    planTypeLabel.Content = "VMAT CSI";
                }
                else
                {
                    planType = VMATTBICSIAutoplanningHelpers.Helpers.PlanType.VMAT_TBI;
                    planTypeLabel.Content = "VMAT TBI";
                }

                thePlans = theCourse.ExternalPlanSetups.OrderBy(x => x.CreationDateTime).ToList();
                if (thePlans.Count > 2)
                {
                    MessageBox.Show(String.Format("Error! More than two plans found in course: {0}! Unable to determine which plan(s) should be used for optimization! Exiting!", theCourse.Id));
                    thePlans = new List<ExternalPlanSetup> { };
                }
                else if (thePlans.Count < 1)
                {
                    MessageBox.Show(String.Format("Error! No plans found in course: {0}! Unable to determine which plan(s) should be used for optimization! Exiting!", theCourse.Id));
                }
                else if (thePlans.Count == 2 && (thePlans.First().StructureSet != thePlans.Last().StructureSet))
                {
                    MessageBox.Show(String.Format("Error! Structure set in first plan ({0}) is not the same as the structure set in second plan ({1})! Exiting!", thePlans.First().Id, thePlans.Last().Id));
                    thePlans = new List<ExternalPlanSetup> { };
                }
            }
            if (thePlans.Any()) ss = thePlans.First().StructureSet;

            return (thePlans, ss);
        }

        private void clearEverything()
        {
            //clear all existing content from the main window
            templateList.UnselectAll();
            selectedSS = null;
            plans = new List<ExternalPlanSetup> { };
            initDosePerFxTB.Text = initNumFxTB.Text = initRxTB.Text = numOptLoops.Text = "";
            ClearAllItemsFromUIList(optimizationParamSP);
            ClearAllItemsFromUIList(planObjectiveParamSP);
        }

        private void populateRx()
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
            else
            {
                initNormVolume.Text = GetPlanTargetId();
            }
        }

        private void PopulateOptimizationTab(StackPanel theSP)
        {
            //clear the current list of optimization constraints and ones obtained from the plan to the user
            ClearAllItemsFromUIList(theSP);
            foreach(ExternalPlanSetup itr in plans) AddListItemsToUI(new OptimizationSetupUIHelper().ReadConstraintsFromPlan(itr), itr.Id, theSP);
        }

        private void PopulatePlanObjectivesTab(StackPanel theSP)
        {
            //clear the current list of optimization constraints and ones obtained from the plan to the user
            ClearAllItemsFromUIList(theSP);
            AddListItemsToUI(planObj, "", theSP);
        }

        private void AddOptimizationConstraintsHeader(StackPanel theSP)
        {
            theSP.Children.Add(new OptimizationSetupUIHelper().GetOptHeader(theSP.Width));
        }

        private void AddPlanObjectivesHeader(StackPanel theSP)
        {
            theSP.Children.Add(new PlanObjectiveSetupUIHelper().GetObjHeader(theSP.Width));
        }

        private void AddListItemsToUI<T>(List<Tuple<string, string, double, double, T>> defaultList, string planId, StackPanel theSP)
        {
            int counter = 0;
            string clearBtnNamePrefix;
            OptimizationSetupUIHelper helper = new OptimizationSetupUIHelper();
            if (theSP.Name.ToLower().Contains("optimization"))
            {
                clearBtnNamePrefix = "clearOptimizationConstraintBtn";
                theSP.Children.Add(helper.AddPlanIdtoOptList(theSP, planId));
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
                theSP.Children.Add(helper.AddOptVolume(theSP, 
                                                       selectedSS, 
                                                       defaultList[i], 
                                                       clearBtnNamePrefix, 
                                                       counter, 
                                                       new RoutedEventHandler(this.ClearItem_Click), 
                                                       theSP.Name.Contains("template") ? true : false));
            }
        }

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
        private void startOpt_Click(object sender, RoutedEventArgs e)
        {
            if (!plans.Any()) 
            { 
                MessageBox.Show("No plans found!"); 
                return; 
            }

            if (optimizationParamSP.Children.Count == 0)
            {
                MessageBox.Show("No optimization parameters present to assign to the VMAT plan!");
                return;
            }
            if (!int.TryParse(numOptLoops.Text, out int numOptimizations))
            {
                MessageBox.Show("Error! Invalid input for number of optimization loops! \nFix and try again.");
                return;
            }

            if(!double.TryParse(targetNormTB.Text, out double planNorm))
            {
                MessageBox.Show("Error! Target normalization is NaN \nFix and try again.");
                return;
            }
            if(planNorm < 0.0 || planNorm > 100.0)
            {
                MessageBox.Show("Error! Target normalization is is either < 0% or > 100% \nExiting!");
                return;
            }
            if(numOptimizations < 1)
            {
                MessageBox.Show("Number of requested optimizations needs to be greater than or equal to 1.\nExiting!");
                return;
            }

            (List<Tuple<string, List<Tuple<string, string, double, double, int>>>>, StringBuilder) parsedOptimizationConstraints = new OptimizationSetupUIHelper().ParseOptConstraints(optimizationParamSP);
            if (!parsedOptimizationConstraints.Item1.Any())
            {
                MessageBox.Show(parsedOptimizationConstraints.Item2.ToString());
                return;
            }
            List<Tuple<string, string, double, double, DoseValuePresentation>> objectives = new PlanObjectiveSetupUIHelper().GetPlanObjectives(planObjectiveParamSP);
            if (!objectives.Any())
            {
                MessageBox.Show("Error! Missing plan objectives! Please add plan objectives and try again!");
                return;
            }
            //determine if flash was used to prep the plan
            if (parsedOptimizationConstraints.Item1.Where(x => x.Item2.Where(y => y.Item1.ToLower().Contains("flash")).Any()).Any()) useFlash = true;

            //assign optimization constraints
            pi.BeginModifications();
            OptimizationSetupUIHelper helper = new OptimizationSetupUIHelper();
            foreach (Tuple<string, List<Tuple<string, string, double, double, int>>> itr in parsedOptimizationConstraints.Item1)
            {
                ExternalPlanSetup thePlan = null;
                //additional check if the plan was not found in the list of VMATplans
                thePlan = plans.FirstOrDefault(x => x.Id == itr.Item1);
                if (thePlan != null)
                {
                    helper.RemoveOptimizationConstraintsFromPLan(thePlan);
                    helper.AssignOptConstraints(itr.Item2, thePlan, true, 0.0);
                }
            }

            //does the user want to run the initial dose coverage check?
            runCoverageCheck = runCoverageCk.IsChecked.Value;
            //does the user want to run one additional optimization to reduce hotspots?
            runOneMoreOpt = runAdditionalOpt.IsChecked.Value;
            //does the user want to copy and save each plan after it's optimized (so the user can choose between the various plans)?
            copyAndSavePlanItr = copyAndSave.IsChecked.Value;

            //construct the actual plan objective array
            planDoseInfo = new List<Tuple<string, string, double, string>>(ConstructPlanDoseInfo());

            //create a new instance of the structure dataContainer and assign the optimization loop parameters entered by the user to the various data members
            dataContainer data = new dataContainer();
            data.construct(plans, 
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
            if(planType == VMATTBICSIAutoplanningHelpers.Helpers.PlanType.VMAT_CSI) optLoop = new VMATCSIOptimization(data);
            else optLoop = new VMATTBIOptimization(data);
            optLoop.Execute();
        }

        private List<Tuple<string,string,double,string>> ConstructPlanDoseInfo()
        {
            List<Tuple<string, string, double, string>> tmp = new List<Tuple<string, string, double, string>> { };

            foreach(Tuple<string,string,double,string> itr in planDoseInfo)
            {
                if (itr.Item1 == "<targetId>")
                {
                    tmp.Add(Tuple.Create(GetPlanTargetId(), itr.Item2, itr.Item3, itr.Item4));
                }
                else
                {
                    tmp.Add(Tuple.Create(itr.Item1, itr.Item2, itr.Item3, itr.Item4));
                }
            }
            return tmp;
        }
           

        private List<Tuple<string, string, double, double, DoseValuePresentation>> ConstructPlanObjectives(List<Tuple<string, string, double, double, DoseValuePresentation>> obj)
        {
            List<Tuple<string, string, double, double, DoseValuePresentation>> tmp = new List<Tuple<string, string, double, double, DoseValuePresentation>> { };
            if(selectedSS != null)
            {
                foreach (Tuple<string, string, double, double, DoseValuePresentation> itr in obj)
                {
                    if (itr.Item1 == "<targetId>")
                    {
                        tmp.Add(Tuple.Create(GetPlanTargetId(), itr.Item2, itr.Item3, itr.Item4, itr.Item5));
                    }
                    else
                    {
                        if (selectedSS.Structures.Any(x => x.Id.ToLower() == itr.Item1.ToLower() && !x.IsEmpty)) tmp.Add(Tuple.Create(itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5));
                    }
                }
            }
            return tmp;
        }

        private string GetPlanTargetId()
        {
            //if(useFlash) planObj.Add(Tuple.Create("TS_PTV_FLASH", obj.Item2, obj.Item3, obj.Item4, obj.Item5)); 
            //else planObj.Add(Tuple.Create("TS_PTV_VMAT", obj.Item2, obj.Item3, obj.Item4, obj.Item5)); 
            return new TargetsHelper().GetTargetStructureForPlanType(selectedSS, "", useFlash, planType).Id;
        }
        #endregion

        #region script and configuration
        private void DisplayConfigurationParameters()
        {
            configTB.Text = "";
            configTB.Text = String.Format("{0}", DateTime.Now.ToString()) + Environment.NewLine;
            if (configFile != "") configTB.Text += String.Format("Configuration file: {0}", configFile) + Environment.NewLine + Environment.NewLine;
            else configTB.Text += String.Format("Configuration file: none") + Environment.NewLine + Environment.NewLine;
            configTB.Text += String.Format("Documentation path: {0}", documentationPath) + Environment.NewLine + Environment.NewLine;
            configTB.Text += String.Format("Log file path: {0}", logFilePath) + Environment.NewLine + Environment.NewLine;
            configTB.Text += String.Format("Default run parameters:") + Environment.NewLine;
            configTB.Text += String.Format("Demo mode: {0}", demo) + Environment.NewLine;
            configTB.Text += String.Format("Run coverage check: {0}", runCoverageCheckOption) + Environment.NewLine;
            configTB.Text += String.Format("Run additional optimization: {0}", runAdditionalOptOption) + Environment.NewLine;
            configTB.Text += String.Format("Copy and save each optimized plan: {0}", copyAndSaveOption) + Environment.NewLine;
            configTB.Text += String.Format("Plan normalization: {0}% (i.e., PTV V100% = {0}%)", defaultPlanNorm) + Environment.NewLine;
            configTB.Text += String.Format("Decision threshold: {0}", threshold) + Environment.NewLine;
            configTB.Text += String.Format("Relative lower dose limit: {0}", lowDoseLimit) + Environment.NewLine + Environment.NewLine;

            foreach (CSIAutoPlanTemplate itr in PlanTemplates.Where(x => x.GetTemplateName() != "--select--"))
            {
                configTB.Text += "-----------------------------------------------------------------------------" + Environment.NewLine;

                configTB.Text += String.Format(" Template ID: {0}", itr.GetTemplateName()) + Environment.NewLine;
                configTB.Text += String.Format(" Initial Dose per fraction: {0} cGy", itr.GetInitialRxDosePerFx()) + Environment.NewLine;
                configTB.Text += String.Format(" Initial number of fractions: {0}", itr.GetInitialRxNumFx()) + Environment.NewLine;
                configTB.Text += String.Format(" Boost Dose per fraction: {0} cGy", itr.GetBoostRxDosePerFx()) + Environment.NewLine;
                configTB.Text += String.Format(" Boost number of fractions: {0}", itr.GetBoostRxNumFx()) + Environment.NewLine;

                if (itr.GetTargets().Any())
                {
                    configTB.Text += String.Format(" {0} targets:", itr.GetTemplateName()) + Environment.NewLine;
                    configTB.Text += String.Format("  {0, -15} | {1, -8} | {2, -14} |", "structure Id", "Rx (cGy)", "Plan Id") + Environment.NewLine;
                    foreach (Tuple<string, double, string> tgt in itr.GetTargets()) configTB.Text += String.Format("  {0, -15} | {1, -8} | {2,-14:N1} |" + Environment.NewLine, tgt.Item1, tgt.Item2, tgt.Item3);
                    configTB.Text += Environment.NewLine;
                }
                else configTB.Text += String.Format(" No targets set for template: {0}", itr.GetTemplateName()) + Environment.NewLine + Environment.NewLine;

                if (itr.GetCreateTSStructures().Any())
                {
                    configTB.Text += String.Format(" {0} additional tuning structures:", itr.GetTemplateName()) + Environment.NewLine;
                    configTB.Text += String.Format("  {0, -10} | {1, -15} |", "DICOM type", "Structure Id") + Environment.NewLine;
                    foreach (Tuple<string, string> ts in itr.GetCreateTSStructures()) configTB.Text += String.Format("  {0, -10} | {1, -15} |" + Environment.NewLine, ts.Item1, ts.Item2);
                    configTB.Text += Environment.NewLine;
                }
                else configTB.Text += String.Format(" No additional tuning structures for template: {0}", itr.GetTemplateName()) + Environment.NewLine + Environment.NewLine;

                if (itr.GetTSManipulations().Any())
                {
                    configTB.Text += String.Format(" {0} additional sparing structures:", itr.GetTemplateName()) + Environment.NewLine;
                    configTB.Text += String.Format("  {0, -15} | {1, -26} | {2, -11} |", "structure Id", "sparing type", "margin (cm)") + Environment.NewLine;
                    foreach (Tuple<string, string, double> spare in itr.GetTSManipulations()) configTB.Text += String.Format("  {0, -15} | {1, -26} | {2,-11:N1} |" + Environment.NewLine, spare.Item1, spare.Item2, spare.Item3);
                    configTB.Text += Environment.NewLine;
                }
                else configTB.Text += String.Format(" No additional sparing structures for template: {0}", itr.GetTemplateName()) + Environment.NewLine + Environment.NewLine;

                if (itr.GetInitOptimizationConstraints().Any())
                {
                    configTB.Text += String.Format(" {0} template initial plan optimization parameters:", itr.GetTemplateName()) + Environment.NewLine;
                    configTB.Text += String.Format("  {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority") + Environment.NewLine;
                    foreach (Tuple<string, string, double, double, int> opt in itr.GetInitOptimizationConstraints()) configTB.Text += String.Format("  {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |" + Environment.NewLine, opt.Item1, opt.Item2, opt.Item3, opt.Item4, opt.Item5);
                    configTB.Text += Environment.NewLine;
                }
                else configTB.Text += String.Format(" No iniital plan optimization constraints for template: {0}", itr.GetTemplateName()) + Environment.NewLine + Environment.NewLine;

                if (itr.GetBoostOptimizationConstraints().Any())
                {
                    configTB.Text += String.Format(" {0} template boost plan optimization parameters:", itr.GetTemplateName()) + Environment.NewLine;
                    configTB.Text += String.Format("  {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority") + Environment.NewLine;
                    foreach (Tuple<string, string, double, double, int> opt in itr.GetBoostOptimizationConstraints()) configTB.Text += String.Format("  {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |" + Environment.NewLine, opt.Item1, opt.Item2, opt.Item3, opt.Item4, opt.Item5);
                    configTB.Text += Environment.NewLine;
                }
                else configTB.Text += String.Format(" No boost plan optimization constraints for template: {0}", itr.GetTemplateName()) + Environment.NewLine + Environment.NewLine;

                if (itr.GetRequestedPlanDoseInfo().Any())
                {
                    configTB.Text += String.Format(" {0} template requested dosimetric info after each iteration:", itr.GetTemplateName()) + Environment.NewLine;
                    configTB.Text += String.Format(" {0, -15} | {1, -6} | {2, -9} |", "structure Id", "metric", "dose type") + Environment.NewLine;

                    foreach (Tuple<string, string, double, string> info in itr.GetRequestedPlanDoseInfo())
                    {
                        if (info.Item2.Contains("max") || info.Item2.Contains("min")) configTB.Text += String.Format(" {0, -15} | {1, -6} | {2, -9} |", info.Item1, info.Item2, info.Item4) + Environment.NewLine;
                        else configTB.Text += String.Format(" {0, -15} | {1, -6} | {2, -9} |", info.Item1, String.Format("{0}{1}%", info.Item2, info.Item3), info.Item4) + Environment.NewLine;
                    }
                    configTB.Text += Environment.NewLine;
                    configTB.Text += Environment.NewLine;
                }
                else configTB.Text += String.Format(" No requested dosimetric info for template: {0}", itr.GetTemplateName()) + Environment.NewLine + Environment.NewLine;

                if(itr.GetPlanObjectives().Any())
                {
                    configTB.Text += String.Format(" {0} template plan objectives:", itr.GetTemplateName()) + Environment.NewLine;
                    configTB.Text += String.Format(" {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -9} |", "structure Id", "constraint type", "dose", "volume (%)", "dose type") + Environment.NewLine;
                    foreach (Tuple<string, string, double, double, DoseValuePresentation> obj in itr.GetPlanObjectives())
                    {
                        configTB.Text += String.Format(" {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-9} |" + Environment.NewLine, obj.Item1, obj.Item2, obj.Item3, obj.Item4, obj.Item5);
                    }
                    configTB.Text += Environment.NewLine;
                }
                else configTB.Text += String.Format(" No plan objectives for template: {0}", itr.GetTemplateName()) + Environment.NewLine + Environment.NewLine;

                if(itr.GetRequestedOptTSStructures().Any())
                {
                    configTB.Text += String.Format(" {0} template requested tuning structures:", itr.GetTemplateName()) + Environment.NewLine;
                    configTB.Text += String.Format(" {0, -15} | {1, -9} | {2, -10} | {3, -5} | {4, -8} | {5, -10} |", "structure Id", "low D (%)", "high D (%)", "V (%)", "priority", "constraint") + Environment.NewLine;
                    foreach (Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>> ts in itr.GetRequestedOptTSStructures())
                    {
                        configTB.Text += String.Format(" {0, -15} | {1, -9:N1} | {2,-10:N1} | {3,-5:N1} | {4,-8} |", ts.Item1, ts.Item2, ts.Item3, ts.Item4, ts.Item5);
                        if (!ts.Item6.Any()) configTB.Text += String.Format(" {0,-10} |", "none") + Environment.NewLine;
                        else
                        {
                            int count = 0;
                            foreach (Tuple<string, double, string, double> ts1 in ts.Item6)
                            {
                                if (count == 0)
                                {
                                    if (ts1.Item1.Contains("Dmax")) configTB.Text += String.Format(" {0,-10} |", String.Format("{0}{1}{2}%", ts1.Item1, ts1.Item3, ts1.Item4)) + Environment.NewLine;
                                    else if (ts1.Item1.Contains("V")) configTB.Text += String.Format(" {0,-10} |", String.Format("{0}{1}%{2}{3}%", ts1.Item1, ts1.Item2, ts1.Item3, ts1.Item4)) + Environment.NewLine;
                                    else configTB.Text += String.Format(" {0,-10} |", String.Format("{0}", ts1.Item1)) + Environment.NewLine;
                                }
                                else
                                {
                                    if (ts1.Item1.Contains("Dmax")) configTB.Text += String.Format(" {0,-59} | {1,-10} |", " ", String.Format("{0}{1}{2}%", ts1.Item1, ts1.Item3, ts1.Item4)) + Environment.NewLine;
                                    else if (ts1.Item1.Contains("V")) configTB.Text += String.Format(" {0,-59} | {1,-10} |", " ", String.Format("{0}{1}%{2}{3}%", ts1.Item1, ts1.Item2, ts1.Item3, ts1.Item4)) + Environment.NewLine;
                                    else configTB.Text += String.Format(" {0,-59} | {1,-10} |", " ", String.Format("{0}", ts1.Item1)) + Environment.NewLine;
                                }
                                count++;
                            }
                        }
                    }
                    configTB.Text += Environment.NewLine;
                }
                else configTB.Text += String.Format(" No requested heater/cooler structures for template: {0}", itr.GetTemplateName()) + Environment.NewLine + Environment.NewLine;
            }
            configScroller.ScrollToTop();

            //set the default parameters for the optimization loop
            runCoverageCk.IsChecked = runCoverageCheckOption;
            numOptLoops.Text = defautlNumOpt;
            runAdditionalOpt.IsChecked = runAdditionalOptOption;
            copyAndSave.IsChecked = copyAndSaveOption;
            targetNormTB.Text = defaultPlanNorm;
        }

        private void loadNewConfigFile_Click(object sender, RoutedEventArgs e)
        {
            configFile = "";
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\";
            openFileDialog.Filter = "ini files (*.ini)|*.ini|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog().Value) 
            { 
                if (!loadConfigurationSettings(openFileDialog.FileName)) 
                { 
                    if (pi != null) DisplayConfigurationParameters(); 
                } 
                else MessageBox.Show("Error! Selected file is NOT valid!"); 
            }
        }

        private bool loadConfigurationSettings(string file)
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
                                        documentationPath = value;
                                        if (documentationPath.LastIndexOf("\\") != documentationPath.Length - 1) documentationPath += "\\";
                                    }
                                    else if (parameter == "log file path")
                                    {
                                        logFilePath = value;
                                        if (logFilePath.LastIndexOf("\\") != logFilePath.Length - 1) logFilePath += "\\";
                                    }
                                    else if (parameter == "demo") { if (value != "") demo = bool.Parse(value); }
                                    else if (parameter == "run coverage check") { if (value != "") runCoverageCheckOption = bool.Parse(value); }
                                    else if (parameter == "run additional optimization") { if (value != "") runAdditionalOptOption = bool.Parse(value); }
                                    else if (parameter == "copy and save each plan") { if (value != "") copyAndSaveOption = bool.Parse(value); }
                                }
                        }
                    }
                    reader.Close();
                }
                
                return false;
            }
            catch (Exception e) { MessageBox.Show(String.Format("Error could not load configuration file because: {0}\n\nAssuming default parameters", e.Message)); return true; }
        }

        private void LoadTemplatePlanChoices(VMATTBICSIAutoplanningHelpers.Helpers.PlanType type)
        {
            ConfigurationHelper helper = new ConfigurationHelper();
            int count = 1;
            SearchOption option = SearchOption.AllDirectories;
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\templates\\";
            PlanTemplates.Clear();
            if (type == VMATTBICSIAutoplanningHelpers.Helpers.PlanType.VMAT_CSI) path += "CSI\\";
            else path += "TBI\\";
            try
            {
                foreach (string itr in Directory.GetFiles(path, "*.ini", option).OrderBy(x => x))
                {
                    PlanTemplates.Add(helper.ReadTemplatePlan(itr, count++));
                }
            }
            catch(Exception e)
            {
                MessageBox.Show(String.Format("Error could not load plan template file because: {0}!", e.Message));
            }
            selectedTemplate = PlanTemplates.FirstOrDefault(x => x.GetTemplateName() == selectedTemplateName);
            if (selectedTemplate != null) templateList.SelectedItem = selectedTemplate;
        }

        private void LoadLogFile(string fullLogName)
        {
            try
            {
                using (StreamReader reader = new StreamReader(fullLogName))
                {
                    string line;
                    ConfigurationHelper helper = new ConfigurationHelper();
                    while (!(line = reader.ReadLine()).Equals("Optimization constraints:"))
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            //useful info on this line
                            if (line.Contains("="))
                            {
                                string parameter = line.Substring(0, line.IndexOf("="));
                                string value = line.Substring(line.IndexOf("=") + 1, line.Length - line.IndexOf("=") - 1);
                                if (parameter == "plan type")
                                {
                                    if(value.Contains("CSI")) planType = VMATTBICSIAutoplanningHelpers.Helpers.PlanType.VMAT_CSI;
                                    else planType = VMATTBICSIAutoplanningHelpers.Helpers.PlanType.VMAT_TBI;
                                    planTypeLabel.Content = planType;
                                }
                                else if (parameter == "template")
                                {
                                    //plan objectives will be updated in OpenPatient method
                                    selectedTemplateName = value;
                                }
                            }
                            else if (line.Contains("prescriptions:"))
                            {
                                while (!string.IsNullOrEmpty((line = reader.ReadLine().Trim())))
                                {
                                    prescriptions.Add(helper.ParsePrescriptionsFromLogFile(line));
                                }
                            }
                            else if (line.Contains("plan UIDs:"))
                            {
                                while (!string.IsNullOrEmpty((line = reader.ReadLine().Trim())))
                                {
                                    planUIDs.Add(line);
                                }
                            }
                            else if (line.Contains("normalization volumes:"))
                            {
                                while (!string.IsNullOrEmpty((line = reader.ReadLine().Trim())))
                                {
                                    normalizationVolumes.Add(helper.ParseNormalizationVolumeFromLogFile(line));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e) { MessageBox.Show(String.Format("Error could not load log file because: {0}\n\n", e.Message));}
        }
        #endregion

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //be sure to close the patient before closing the application. Not doing so will result in unclosed timestamps in eclipse
            app.ClosePatient();
            app.Dispose();
        }
    }
}
