using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.IO;
using System.Text;
using System.Diagnostics;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Reflection;
using VMATTBICSIAutoPlanningHelpers.Logging;
using VMATTBICSIAutoPlanningHelpers.PlanTemplateClasses;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.UIHelpers;
using VMATTBICSIAutoPlanningHelpers.Prompts;
using PlanType = VMATTBICSIAutoPlanningHelpers.Enums.PlanType;

namespace VMATCSIAutoPlanMT.VMAT_CSI
{
    public partial class CSIAutoPlanMW : Window
    {
        public bool GetCloseOpenPatientWindowStatus() { return closeOpenPatientWindow; }
        /// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// HARD-CODED MAIN PARAMETERS FOR THIS CLASS AND ALL OTHER CLASSES IN THIS PROGRAM
        /// ADJUST THESE PARAMETERS TO YOUR TASTE. THESE PARAMETERS WILL BE OVERWRITTEN BY THE CONFIG.INI FILE IF IT IS SUPPLIED.
        /// OTHERWISE, THE USER CANNOT ADJUST THESE ITEMS IN THE UI!
        /// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //point this to the directory holding the documentation files
        string documentationPath = @"\\vfs0006\RadData\oncology\ESimiele\Research\VMAT_TBI_CSI\documentation\";
        //log file path
        string logPath = @"\\enterprise.stanfordmed.org\depts\RadiationTherapy\Public\Users\ESimiele\Research\VMAT-TBI-CSI\log_files\";
        //location where CT images should be exported
        string imgExportPath = @"\\vfs0006\RadData\oncology\ESimiele\Research\VMAT_TBI_CSI\exportedImages\";
        //image export format
        string imgExportFormat = "png";
        //treatment units and associated photon beam energies
        List<string> linacs = new List<string> { "LA16", "LA17" };
        List<string> beamEnergies = new List<string> { "6X"};
        //default number of beams per isocenter from head to toe
        int[] beamsPerIso = { 2, 1, 1 };
        //collimator rotations for how to orient the beams (placeBeams class)
        double[] collRot = {10.0, 350.0, 3.0, 357.0};
        //flag whether to use algorithm to auto fit jaws to targets
        bool autoFitJaws = true;
        //photon beam calculation model
        string calculationModel = "AAA_15605";
        //photon optimization algorithm
        string optimizationModel = "PO_15605";
        //use GPU for dose calculation (not optimization)
        string useGPUdose = "false";
        //use GPU for optimization
        string useGPUoptimization = "false";
        //what MR level should the optimizer restart at following intermediate dose calculation
        string MRrestartLevel = "MR3";
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        //data members
        Logger log = null;
        string configFile = "";
        public Patient pi = null;
        StructureSet selectedSS = null;
        public int clearTargetBtnCounter = 0;
        public int clearTargetTemplateBtnCounter = 0;
        public int clearSpareBtnCounter = 0;
        public int clearTemplateSpareBtnCounter = 0;
        public int clearOptBtnCounter = 0;
        public int clearTemplateOptBtnCounter = 0;
        //option to contour overlap between VMAT fields in adjacent isocenters and default margin for contouring the overlap
        bool contourOverlap = true;
        string contourFieldOverlapMargin = "1.0";
        //structure id, Rx dose, plan Id
        List<Tuple<string, double, string>> targets = new List<Tuple<string, double, string>> { };
        //requested preliminary targets from configuration file (i.e., starting point for MD when contouring targets). Same structure as TSStructures below
        List<Tuple<string, string>> prelimTargets = new List<Tuple<string, string>> { };
        //list to hold the current structure ids in the structure set in addition to the prospective ids after unioning the left and right structures together
        List<string> structureIdsPostUnion = new List<string> { };
        //default general tuning structures to be added (specified in CSI_plugin_config.ini file)
        List<Tuple<string, string>> defaultTSStructures = new List<Tuple<string, string>> { };
        //default general tuning structure manipulations to be added (specified in CSI_plugin_config.ini file)
        List<Tuple<string, TSManipulationType, double>> defaultTSStructureManipulations = new List<Tuple<string, TSManipulationType, double>> { };
        //list of plans generated by this script (PlanSetup objects)
        List<ExternalPlanSetup> VMATplans = new List<ExternalPlanSetup> { };
        //plan Id, list of isocenter names for this plan
        public List<Tuple<string,List<string>>> isoNames = new List<Tuple<string, List<string>>> { };
        //plan ID, target Id, numFx, dosePerFx, cumulative dose
        List<Tuple<string, string, int, DoseValue, double>> prescriptions = new List<Tuple<string, string, int, DoseValue, double>> { };
        //list of junction structures (i.e., overlap regions between adjacent isocenters)
        List<Tuple<ExternalPlanSetup, List<Structure>>> jnxs = new List<Tuple<ExternalPlanSetup, List<Structure>>> { };
        PlanPrep_CSI prep = null;
        private VMS.TPS.Common.Model.API.Application app = null;
        bool isModified = false;
        bool autoSave = false;
        bool checkStructuresToUnion = true;
        //ATTENTION! THE FOLLOWING LINE HAS TO BE FORMATTED THIS WAY, OTHERWISE THE DATA BINDING WILL NOT WORK!
        public ObservableCollection<CSIAutoPlanTemplate> PlanTemplates { get; set; }
        //temporary variable to add new templates to the list
        CSIAutoPlanTemplate prospectiveTemplate = null;
        //ProcessStartInfo optLoopProcess;
        private bool closeOpenPatientWindow = false;

        public CSIAutoPlanMW(List<string> args)
        {
            InitializeComponent();
            if(InitializeScript(args)) this.Close();
        }

        private void LoadDefaultConfigurationFiles()
        {
            //load script configuration and display the settings
            List<string> configurationFiles = new List<string> { };
            configurationFiles.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\log_configuration.ini");
            configurationFiles.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\VMAT_CSI_config.ini");
            foreach (string itr in configurationFiles) LoadConfigurationSettings(itr);
        }

        private bool InitializeScript(List<string> args)
        {
            try { app = VMS.TPS.Common.Model.API.Application.CreateApplication(); }
            catch (Exception e) { MessageBox.Show(String.Format("Warning! Could not generate Aria application instance because: {0}", e.Message)); }
            string mrn = "";
            string ss = "";
            if (args.Any())
            {
                mrn = args.ElementAt(0);
                ss = args.ElementAt(1);
            }

            LoadDefaultConfigurationFiles();
            log = new Logger(logPath, PlanType.VMAT_CSI, mrn);
            if (app != null)
            {
                if(OpenPatient(mrn)) return true;
                InitializeStructureSetSelection(ss);
                CheckPreliminaryTargets();

                //check the version information of Eclipse installed on this machine. If it is older than version 15.6, let the user know that this script may not work properly on their system
                if (!double.TryParse(app.ScriptEnvironment.VersionInfo.Substring(0, app.ScriptEnvironment.VersionInfo.LastIndexOf(".")), out double vinfo)) log.LogError("Warning! Could not parse Eclise version number! Proceed with caution!");
                else if (vinfo < 15.6) log.LogError(String.Format("Warning! Detected Eclipse version: {0:0.0} is older than v15.6! Proceed with caution!", vinfo));
            }

            PlanTemplates = new ObservableCollection<CSIAutoPlanTemplate>() { new CSIAutoPlanTemplate("--select--") };
            DataContext = this;
            templateBuildOptionCB.Items.Add("Existing template");
            templateBuildOptionCB.Items.Add("Current parameters");

            LoadPlanTemplates();
            DisplayConfigurationParameters();
            return false;
        }

        private bool OpenPatient(string mrn)
        {
            if (string.IsNullOrEmpty(mrn))
            {
                //missing patient MRN. Need to ask user for it
                EnterMissingInfoPrompt EMIP = new EnterMissingInfoPrompt("Missing patient Id!\nPlease enter it below and hit Confirm!", "MRN:");
                EMIP.ShowDialog();
                if (EMIP.GetSelection())
                {
                    try
                    {
                        if (app != null) pi = app.OpenPatientById(EMIP.GetEnteredValue());
                        mrn = EMIP.GetEnteredValue();
                        log.MRN = mrn;
                    }
                    catch (Exception except)
                    {
                        log.LogError(string.Format("Error! Could not open patient because: {0}! Please try again!", except.Message));
                        log.LogError(except.StackTrace, true);
                        pi = null;
                    }
                }
                else
                {
                    closeOpenPatientWindow = true;
                    return true;
                }
            }
            else pi = app.OpenPatientById(mrn);
            return false;
        }

        private void InitializeStructureSetSelection(string ss)
        {
            if (pi != null)
            {
                foreach (StructureSet s in pi.StructureSets.OrderByDescending(x => x.HistoryDateTime)) SSID.Items.Add(s.Id);
                //SSID default is the current structure set in the context
                if (!string.IsNullOrEmpty(ss))
                {
                    selectedSS = pi.StructureSets.FirstOrDefault(x => string.Equals(x.Id, ss));
                    SSID.Text = selectedSS.Id;
                }
                else log.LogError("Warning! No structure set in context! Please select a structure set at the top of the GUI!");
                ExportCTUIHelper.PopulateCTImageSets(pi.StructureSets.Where(x => x != selectedSS).ToList(), selectedSS, CTimageSP);
                patientMRNLabel.Content = pi.Id;
            }
            else log.LogError("Could not open patient!");
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(documentationPath + "VMAT_CSI_guide.pdf")) log.LogError("VMAT_CSI_guide PDF file does not exist!");
            else Process.Start(documentationPath + "VMAT_CSI_guide.pdf");
        }

        private void QuickStart_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(documentationPath + "CSI_plugIn_quickStart_guide.pdf")) log.LogError("CSI_plugIn_quickStart_guide PDF file does not exist!");
            else Process.Start(documentationPath + "CSI_plugIn_quickStart_guide.pdf");
        }

        private void StructureSetId_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ClearAllCurrentParameters();

            //update selected structure set
            selectedSS = pi.StructureSets.FirstOrDefault(x => string.Equals(x.Id, SSID.SelectedItem.ToString()));
            log.StructureSet = selectedSS.Id;

            CheckPreliminaryTargets();
        }

        private void ClearAllCurrentParameters()
        {
            //targets and tuning structures are automatically handled in their respectful AddDefaults event click method
            //clear isocenter and beams information
            beamPlacementSP.Children.Clear();

            //clear optimization structure list
            ClearOptimizationConstraintsList(optParametersSP);
        }

        private void LoadTemplateDefaults()
        {
            AddTargetDefaults_Click(null, null);
            AddDefaultTuningStructures_Click(null, null);
            AddDefaultStructureManipulations_Click(null, null);
        }

        private void Templates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CSIAutoPlanTemplate selectedTemplate = templateList.SelectedItem as CSIAutoPlanTemplate;
            if (selectedTemplate == null) return;
            initDosePerFxTB.Text = "";
            initNumFxTB.Text = "";
            boostDosePerFxTB.Text = "";
            boostNumFxTB.Text = "";
            if (selectedTemplate.GetTemplateName() != "--select--")
            {
                SetInitPresciptionInfo(selectedTemplate.GetInitialRxDosePerFx(), selectedTemplate.GetInitialRxNumFx());
                if (selectedTemplate.GetBoostRxDosePerFx() != 0.1 && selectedTemplate.GetBoostRxNumFx() != 1) SetBoostPrescriptionInfo(selectedTemplate.GetBoostRxDosePerFx(), selectedTemplate.GetBoostRxNumFx());
                ClearAllCurrentParameters();
                LoadTemplateDefaults();
                log.Template = selectedTemplate.GetTemplateName();
            }
            else
            {
                templateList.UnselectAll();
            }
        }

        bool waitToUpdate = false;
        private void SetInitPresciptionInfo(double dose_perFx, int num_Fx)
        {
            if (initDosePerFxTB.Text != dose_perFx.ToString() && initNumFxTB.Text != num_Fx.ToString()) waitToUpdate = true;
            initDosePerFxTB.Text = dose_perFx.ToString();
            initNumFxTB.Text = num_Fx.ToString();
        }

        bool boostWaitToUpdate = false;
        private void SetBoostPrescriptionInfo(double dose_perFx, int num_Fx)
        {
            if (boostDosePerFxTB.Text != dose_perFx.ToString() && boostNumFxTB.Text != num_Fx.ToString()) boostWaitToUpdate = true;
            boostDosePerFxTB.Text = dose_perFx.ToString();
            boostNumFxTB.Text = num_Fx.ToString();
        }

        private void InitNumFx_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!int.TryParse(initNumFxTB.Text, out int newNumFx)) initRxTB.Text = "";
            else if (newNumFx < 1)
            {
                log.LogError("Error! The number of fractions must be non-negative integer and greater than zero!");
                initRxTB.Text = "";
            }
            else ResetInitRxDose();
        }

        private void InitDosePerFx_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!double.TryParse(initDosePerFxTB.Text, out double newDoseFx)) initRxTB.Text = "";
            else if (newDoseFx <= 0)
            {
                log.LogError("Error! The dose per fraction must be a number and non-negative!");
                initRxTB.Text = "";
            }
            else ResetInitRxDose();
        }

        private void ResetInitRxDose()
        {
            if (waitToUpdate) waitToUpdate = false;
            else if (int.TryParse(initNumFxTB.Text, out int newNumFx) && double.TryParse(initDosePerFxTB.Text, out double newDoseFx))
            {
                initRxTB.Text = (newNumFx * newDoseFx).ToString();
                CSIAutoPlanTemplate selectedTemplate = templateList.SelectedItem as CSIAutoPlanTemplate;
                if (selectedTemplate != null)
                {
                    //verify that the entered dose/fx and num fx agree with those stored in the template, otherwise unselect the template
                    if (newNumFx != selectedTemplate.GetInitialRxNumFx() || newDoseFx != selectedTemplate.GetInitialRxDosePerFx()) templateList.UnselectAll();
                }
            }
        }

        private void BoostDosePerFx_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!double.TryParse(boostDosePerFxTB.Text, out double newDoseFx)) boostRxTB.Text = "";
            else if (newDoseFx <= 0)
            {
                log.LogError("Error! The dose per fraction must be a number and non-negative!");
                initRxTB.Text = "";
            }
            else ResetBoostRxDose();
        }

        private void BoostNumFx_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!int.TryParse(boostNumFxTB.Text, out int newNumFx)) boostRxTB.Text = "";
            else if (newNumFx < 1)
            {
                log.LogError("Error! The number of fractions must be non-negative integer and greater than zero!");
                initRxTB.Text = "";
            }
            else ResetBoostRxDose();
        }

        private void ResetBoostRxDose()
        {
            if (boostWaitToUpdate) boostWaitToUpdate = false;
            else if (int.TryParse(boostNumFxTB.Text, out int newNumFx) && double.TryParse(boostDosePerFxTB.Text, out double newDoseFx))
            {
                boostRxTB.Text = (newNumFx * newDoseFx).ToString();
                CSIAutoPlanTemplate selectedTemplate = templateList.SelectedItem as CSIAutoPlanTemplate;
                if (selectedTemplate != null)
                {
                    //verify that the entered dose/fx and num fx agree with those stored in the template, otherwise unselect the template
                    if (newNumFx != selectedTemplate.GetBoostRxNumFx() || newDoseFx != selectedTemplate.GetBoostRxDosePerFx()) templateList.UnselectAll();
                }
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region ExportCT
        //stuff related to Export CT tab
        private void ExportImgInfo_Click(object sender, RoutedEventArgs e)
        {
            ExportCTUIHelper.PrintExportImgInfo();
        }

        private void ExportImg_Click(object sender, RoutedEventArgs e)
        {
            if (app == null || pi == null) return;
            //CT image stack panel, patient structure set list, patient id, image export path, image export format
            VMS.TPS.Common.Model.API.Image selectedImage = ExportCTUIHelper.GetSelectedImageForExport(CTimageSP, pi.StructureSets.ToList());
            if(selectedImage != null)
            {
                CTImageExport exporter = new CTImageExport(selectedImage, imgExportPath, pi.Id, imgExportFormat);
                //don't care about bool result
                exporter.Execute();
                log.AppendLogOutput("Export CT data:", exporter.GetLogOutput());
            }
            else log.LogError("No image selected for export!");
        }
        #endregion

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Preliminary Targets Generation
        //stuff related to Set Targets tab
        private void CheckPreliminaryTargets()
        {
            if (selectedSS == null) return;
            PrelimTargetGenerationSP.Children.Clear();
            List<string> missingPrelimTargets = new List<string> { };
            List<String> approvedTargets = new List<string> { };
            if (prelimTargets.Any())
            {
                foreach (string itr in prelimTargets.Select(x => x.Item2))
                {
                    //needs to be present AND contoured
                    if (!selectedSS.Structures.Any(x => string.Equals(x.Id, itr) && !x.IsEmpty))
                    {
                        missingPrelimTargets.Add(itr);
                    }
                }
            }

            CSIAutoPlanTemplate selectedTemplate = templateList.SelectedItem as CSIAutoPlanTemplate;
            if (selectedTemplate != null)
            {
                foreach(Tuple<string,double,string> itr in selectedTemplate.GetTargets())
                {
                    if(selectedSS.Structures.Any(x => string.Equals(x.Id.ToLower(), itr.Item1.ToLower()) && x.ApprovalHistory.First().ApprovalStatus == StructureApprovalStatus.Approved))
                    {
                        approvedTargets.Add(selectedSS.Structures.First(x => string.Equals(x.Id.ToLower(), itr.Item1.ToLower()) && x.ApprovalHistory.First().ApprovalStatus == StructureApprovalStatus.Approved).Id);
                    }
                }
            }

            targetsTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed;
            if (!approvedTargets.Any() && missingPrelimTargets.Any())
            {
                AddPrelimTargetVolumes(prelimTargets, PrelimTargetGenerationSP);
                PrelimTargetsTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed;
            }
            else
            {
                PrelimTargetsTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
                setTargetsTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed;
            }
        }

        private void CreatePrelimTargetsInfo_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("This will generate the preliminary targets that will be sent to the physician for review and modification. Included targets:");
            foreach(string itr in prelimTargets.Select(x => x.Item2)) message.AppendLine(itr);
            MessageBox.Show(message.ToString());
        }

        private void AddPrelimTargetVolumes(List<Tuple<string, string>> defaultList, StackPanel theSP)
        {
            if (selectedSS == null)
            {
                log.LogError("Error! Please select a Structure Set before adding tuning structure manipulations!");
                return;
            }
            if (theSP.Children.Count == 0) theSP.Children.Add(StructureTuningUIHelper.AddTemplateTSHeader(theSP));
            string clearBtnName = "ClearPrelimTargetsBtn";
            int counter = 0;
            for(int i = 0; i < defaultList.Count; i++)
            {
                counter++;
                theSP.Children.Add(StructureTuningUIHelper.AddTSVolume(theSP,
                                                       selectedSS,
                                                       defaultList[i],
                                                       clearBtnName,
                                                       counter,
                                                       new RoutedEventHandler(this.ClearPrelimTargetItem_Click)));
            }
        }

        private void ClearPrelimTargetItem_Click(object sender, RoutedEventArgs e)
        {
            StackPanel theSP;
            theSP = PrelimTargetGenerationSP;
            if (GeneralUIHelper.ClearRow(sender, theSP)) GeneralUIHelper.ClearList(theSP);
        }

        private void CreatePrelimTargets_Click(object sender, RoutedEventArgs e)
        {
            //get sparing structure and tuning structure lists from the UI
            (List<Tuple<string, string>> createTSStructureList, StringBuilder errorMessage) parseCreatePrelimTargetList = StructureTuningUIHelper.ParseCreateTSStructureList(PrelimTargetGenerationSP);
            if (!string.IsNullOrEmpty(parseCreatePrelimTargetList.errorMessage.ToString()))
            {
                log.LogError(parseCreatePrelimTargetList.errorMessage);
                return;
            }

            GenerateTargets_CSI generateTargets = new GenerateTargets_CSI(prelimTargets, selectedSS);
            pi.BeginModifications();
            bool result = generateTargets.Execute();
            //grab the log output regardless if it passes or fails
            log.AppendLogOutput("TS Generation and manipulation output:", generateTargets.GetLogOutput());
            if (result) return;
            log.AddedPrelimTargetsStructures = generateTargets.GetAddedTargetStructures();
            PrelimTargetsTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
            MessageBox.Show("Preliminary targets ready for physician review and modification!");
            isModified = true;
        }
        #endregion

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Set targets
        private (ScrollViewer, StackPanel) GetSVAndSPTargetsTab(object sender)
        {
            Button btn = (Button)sender;
            ScrollViewer theScroller;
            StackPanel theSP;
            if (btn.Name.Contains("template"))
            {
                theScroller = targetTemplateScroller;
                theSP = targetTemplate_sp;
            }
            else
            {
                theScroller = targetsScroller;
                theSP = targetsSP;
            }
            return (theScroller, theSP);
        }

        private void AddTarget_Click(object sender, RoutedEventArgs e)
        {
            (ScrollViewer, StackPanel) SVSP = GetSVAndSPTargetsTab(sender);
            AddTargetVolumes(new List<Tuple<string, double, string>> { Tuple.Create("--select--", 0.0, "--select--") }, SVSP.Item2);
            SVSP.Item1.ScrollToBottom();
        }

        private void AddTargetDefaults_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSS == null) 
            { 
                log.LogError("Error! The structure set has not been assigned! Choose a structure set and try again!"); 
                return; 
            }
            List<Tuple<string, double, string>> targetList = new List<Tuple<string, double, string>>(TargetsUIHelper.AddTargetDefaults((templateList.SelectedItem as CSIAutoPlanTemplate), selectedSS));
            ClearAllTargetItems();
            AddTargetVolumes(targetList, targetsSP);
            targetsScroller.ScrollToBottom();
        }

        private void ScanSSAndAddTargets_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSS == null) 
            { 
                log.LogError("Error! The structure set has not been assigned! Choose a structure set and try again!"); 
                return; 
            }
            
            List<Tuple<string, double, string>> targetList = new List<Tuple<string, double, string>>(TargetsUIHelper.ScanSSAndAddTargets(selectedSS));
            if (!targetList.Any()) return;

            (ScrollViewer, StackPanel) SVSP = GetSVAndSPTargetsTab(sender);
            ClearAllTargetItems();
            AddTargetVolumes(targetList, SVSP.Item2);
            SVSP.Item1.ScrollToBottom();
        }

        private void ClearTargetItem_click(object sender, RoutedEventArgs e)
        {
            //same deal as the clear sparing structure button (clearStructBtn_click)
            StackPanel theSP = GetSVAndSPTargetsTab(sender).Item2;
            //clear entire list if there are only two entries (header + 1 real entry)
            if (GeneralUIHelper.ClearRow(sender, theSP)) ClearAllTargetItems((Button)sender);
        }

        private void ClearTargetList_Click(object sender, RoutedEventArgs e) { ClearAllTargetItems((Button)sender); }

        private void ClearAllTargetItems(Button btn = null)
        {
            if(btn == null || btn.Name == "clear_target_list" || !btn.Name.Contains("template"))
            {
                targetsSP.Children.Clear();
                clearTargetBtnCounter = 0;
            }
            else
            {
                targetTemplate_sp.Children.Clear();
                clearTargetTemplateBtnCounter = 0;
            }
        }

        private void AddTargetVolumes(List<Tuple<string,double,string>> defaultList, StackPanel theSP)
        {
            int counter;
            string clearBtnNamePrefix;
            if (theSP.Name == "targetsSP")
            {
                counter = clearTargetBtnCounter;
                clearBtnNamePrefix = "clearTargetBtn";
            }
            else
            {
                counter = clearTargetTemplateBtnCounter;
                clearBtnNamePrefix = "templateClearTargetBtn";
            }
            if (theSP.Children.Count == 0) AddTargetHeader(theSP);
            List<string> planIDs = new List<string> { };
            //assumes each target has a unique planID 
            //TODO: add function to return unique list of planIDs sorted by Rx ascending
            foreach (Tuple<string, double, string> itr in defaultList) planIDs.Add(itr.Item3);
            planIDs.Add("--Add New--");
            foreach(Tuple<string,double,string> itr in defaultList)
            {
                counter++;
                theSP.Children.Add(TargetsUIHelper.AddTargetVolumes(theSP.Width, 
                                                           itr, 
                                                           clearBtnNamePrefix, 
                                                           counter, 
                                                           planIDs, 
                                                           (delegate (object sender, SelectionChangedEventArgs e) { TargetPlanId_SelectionChanged(theSP, sender, e); }), 
                                                           new RoutedEventHandler(this.ClearTargetItem_click)));
            }
        }

        private void TargetPlanId_SelectionChanged(StackPanel theSP, object sender, EventArgs e)
        {
            //not the most elegent code, but it works. Basically, it finds the combobox where the selection was changed and asks the user to enter the id of the plan or the target id
            ComboBox c = (ComboBox)sender;
            if (c.SelectedItem.ToString() != "--Add New--") return;
            bool isTargetStructure = true;
            if (c.Name != "str_cb") isTargetStructure = false;
            foreach (object obj in theSP.Children)
            {
                UIElementCollection row = ((StackPanel)obj).Children;
                foreach (object obj1 in row)
                {
                    //the btn has a unique tag to it, so we can just loop through all children in the structureManipulationSP children list and find which button is equivalent to our button
                    if (obj1.Equals(c))
                    {
                        string msg = "Enter the Id of the target structure!";
                        if (!isTargetStructure) msg = "Enter the requested plan Id!";
                        EnterMissingInfoPrompt EMIP = new EnterMissingInfoPrompt(msg, "Id:");
                        EMIP.ShowDialog();
                        if (EMIP.GetSelection())
                        {
                            c.Items.Insert(c.Items.Count - 1, EMIP.GetEnteredValue());
                            //c.Items.Add(emi.value.Text);
                            c.Text = EMIP.GetEnteredValue();
                        }
                        else c.SelectedIndex = 0;
                        return;
                    }
                }
            }
        }

        private void AddTargetHeader(StackPanel theSP)
        {
            theSP.Children.Add(TargetsUIHelper.GetTargetHeader(theSP.Width));
        }

        private void SetTargets_Click(object sender, RoutedEventArgs e)
        {
            if(selectedSS == null)
            {
                log.LogError("Please select a structure set before setting the targets!");
                return;
            }
            if (targetsSP.Children.Count == 0)
            {
                log.LogError("No targets present in list! Please add some targets to the list before setting the target structures!");
                return;
            }

            //target id, target Rx, plan id
            (List<Tuple<string, double, string>>, StringBuilder) parsedTargets = TargetsUIHelper.ParseTargets(targetsSP, selectedSS);
            if (!parsedTargets.Item1.Any())
            {
                log.LogError(parsedTargets.Item2);
                return;
            }
            (bool isTargetError, StringBuilder errorMessage) = VerifySelectedTargetsIntegrity(parsedTargets.Item1);
            if(isTargetError)
            {
                log.LogError(errorMessage);
                return;
            }

            targets = new List<Tuple<string, double, string>>(parsedTargets.Item1);
            (List<Tuple<string, string, int, DoseValue, double>>, StringBuilder) parsedPrescriptions = TargetsHelper.GetPrescriptions(targets,
                                                                                                                                      initDosePerFxTB.Text,
                                                                                                                                      initNumFxTB.Text,
                                                                                                                                      initRxTB.Text,
                                                                                                                                      boostDosePerFxTB.Text,
                                                                                                                                      boostNumFxTB.Text);
            if(!parsedPrescriptions.Item1.Any())
            {
                log.LogError(parsedPrescriptions.Item2);
                return;
            }
            prescriptions = new List<Tuple<string, string, int, DoseValue, double>>(parsedPrescriptions.Item1);
            targetsTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
            structureTuningTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed;
            TSManipulationTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed;
            //need targets to be assigned prior to populating the ring defaults
            AddDefaultRings_Click(null, null);
            log.targets = targets;
            log.Prescriptions = prescriptions;
        }

        private (bool, StringBuilder) VerifySelectedTargetsIntegrity(List<Tuple<string, double, string>> parsedTargets)
        {
            //verify selected targets are APPROVED
            bool fail = false;
            StringBuilder sb = new StringBuilder();
            foreach (Tuple<string, double, string> itr in parsedTargets)
            {
                if (!selectedSS.Structures.Any(x => string.Equals(x.Id, itr.Item1) && !x.IsEmpty))
                {
                    sb.AppendLine($"Error! {itr.Item1} is either NOT present in structure set or is not contoured!");
                    fail = true;
                }
                else
                {
                    //structure is present and contoured
                    Structure tgt = selectedSS.Structures.First(x => string.Equals(x.Id, itr.Item1));
                    if (tgt.ApprovalHistory.First().ApprovalStatus != StructureApprovalStatus.Approved)
                    {
                        sb.AppendLine($"Error! {tgt.Id} is NOT approved!");
                        sb.AppendLine($"{tgt.Id} approval status: {tgt.ApprovalHistory.First().ApprovalStatus}");
                        fail = true;
                    }
                }
            }
            return (fail, sb);
        }
        #endregion

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region TSGenerationManipulation
        private void TsGenerateVsManipulateInfo_Click(object sender, RoutedEventArgs e)
        {
            string message = "What's the difference between TS structure generation vs manipulation?" + Environment.NewLine;
            message += String.Format("TS structure generation involves adding structures to the structure set to shape the dose distribution. These include rings and substructures. E.g.,") + Environment.NewLine;
            message += String.Format("TS_ring900  -->  ring structure around the targets using a nominal dose level of 900 cGy to determine fall-off") + Environment.NewLine;
            message += String.Format("Kidneys-1cm  -->  substructure for the Kidneys volume where the Kidneys are contracted by 1 cm") + Environment.NewLine + Environment.NewLine;
            message += String.Format("TS structure manipulation involves manipulating/modifying the structure itself or target structures. E.g.,") + Environment.NewLine;
            message += String.Format("(Ovaries, Crop target from structure, 1.5cm)  -->  modify the target structure such that the ovaries structure is cropped from the target with a 1.5 cm margin") + Environment.NewLine;
            message += String.Format("(Brainstem, Contour overlap, 0.0 cm)  -->  Identify the overlapping regions between the brainstem and target structure(s) and contour them as new structures") + Environment.NewLine + Environment.NewLine;
            MessageBox.Show(message);
        }

        private void AddTuningStructure_Click(object sender, RoutedEventArgs e)
        {
            //populate the comboboxes
            Button theBtn = (Button)sender;
            ScrollViewer theScroller;
            StackPanel theSP;
            if (theBtn.Name.Contains("template"))
            {
                theScroller = templateTSScroller;
                theSP = templateTSSP;
            }
            else
            {
                theScroller = TSGenerationScroller;
                theSP = TSGenerationSP;
            }
            AddTuningStructureVolumes(new List<Tuple<string, string>> { Tuple.Create("--select--", "--select--") }, theSP);
            theScroller.ScrollToBottom();
        }

        private void AddDefaultTuningStructures_Click(object sender, RoutedEventArgs e)
        {
            List<Tuple<string, string>> tmp = new List<Tuple<string, string>>(defaultTSStructures);
            if (templateList.SelectedItem != null)
            {
                foreach (Tuple<string, string> itr in ((CSIAutoPlanTemplate)templateList.SelectedItem).GetCreateTSStructures()) tmp.Add(itr);
            }
            GeneralUIHelper.ClearList(TSGenerationSP);
            //populate the comboboxes
            AddTuningStructureVolumes(tmp, TSGenerationSP);
            TSGenerationScroller.ScrollToBottom();
        }

        private void AddTuningStructureVolumes(List<Tuple<string, string>> defaultList, StackPanel theSP)
        {
            if (selectedSS == null) 
            { 
                log.LogError("Error! Please select a Structure Set before adding tuning structure manipulations!"); 
                return; 
            }
            if (theSP.Children.Count == 0) theSP.Children.Add(StructureTuningUIHelper.AddTemplateTSHeader(theSP));
            int counter = 0;
            string clearBtnName = "ClearTSStructuresBtn";
            if (theSP.Name.Contains("template"))
            {
                clearBtnName = "template" + clearBtnName;
            }
            for (int i = 0; i < defaultList.Count; i++)
            {
                counter++;
                theSP.Children.Add(StructureTuningUIHelper.AddTSVolume(theSP, 
                                                       selectedSS, 
                                                       defaultList[i], 
                                                       clearBtnName, 
                                                       counter, 
                                                       new RoutedEventHandler(this.ClearTuningStructureItem_Click)));
            }
        }

        private void ClearTuningStructureItem_Click(object sender, RoutedEventArgs e)
        {
            Button theBtn = (Button)sender;
            StackPanel theSP;
            if (theBtn.Name.Contains("template"))
            {
                theSP = templateTSSP;
            }
            else
            {
                theSP = TSGenerationSP;
            }
            if (GeneralUIHelper.ClearRow(sender, theSP)) GeneralUIHelper.ClearList(theSP);
        }

        private void ClearTuningStructureList_Click(object sender, RoutedEventArgs e)
        {
            Button theBtn = (Button)sender;
            StackPanel theSP;
            if (theBtn.Name.Contains("template"))
            {
                theSP = templateTSSP;
            }
            else
            {
                theSP = TSGenerationSP;
            }
            GeneralUIHelper.ClearList(theSP);
        }

        private StackPanel GetAppropriateRingSP(object o)
        {
            Button theBtn = (Button)o;
            StackPanel theSP;
            if (theBtn.Name.Contains("template"))
            {
                theSP = templateCreateRingsSP;
            }
            else
            {
                theSP = createRingsSP;
            }
            return theSP;
        }

        private void AddRing_Click(object sender, RoutedEventArgs e)
        {
            //populate the comboboxes
            AddRingStructures(new List<Tuple<string, double, double, double>> { Tuple.Create("--select--", 0.0, 0.0, 0.0) }, GetAppropriateRingSP(sender));
        }

        private void AddDefaultRings_Click(object sender, RoutedEventArgs e)
        {
            if (templateList.SelectedItem != null) 
            {
                AddRingStructures((templateList.SelectedItem as CSIAutoPlanTemplate).GetCreateRings(), createRingsSP);
            }
        }

        private void AddRingStructures(List<Tuple<string, double, double, double>> lists, StackPanel theSP)
        {
            if (selectedSS == null)
            {
                log.LogError("Error! Please select a Structure Set before adding ring structures!");
                return;
            }
            if (theSP.Children.Count == 0) theSP.Children.Add(RingUIHelper.GetRingHeader(theSP.Width));
            int counter = 0;
            string clearBtnName = "ClearRingBtn";
            if (theSP.Name.Contains("template"))
            {
                clearBtnName = "template" + clearBtnName;
            }
            for (int i = 0; i < lists.Count; i++)
            {
                counter++;
                theSP.Children.Add(RingUIHelper.AddRing(theSP, 
                                                  targets.Select(x => x.Item1).ToList(), 
                                                  lists[i], 
                                                  clearBtnName, 
                                                  counter, 
                                                  new RoutedEventHandler(this.ClearRingItem_Click), 
                                                  theSP.Name.Contains("template")));
            }
        }

        private void ClearRingItem_Click(object sender, RoutedEventArgs e)
        {
            StackPanel theSP = GetAppropriateRingSP(sender);
            if (GeneralUIHelper.ClearRow(sender, theSP)) GeneralUIHelper.ClearList(theSP);
        }

        private void ClearRings_Click(object sender, RoutedEventArgs e)
        {
            GeneralUIHelper.ClearList(GetAppropriateRingSP(sender));
        }

        private List<string> CheckLRStructures()
        {
            //check if structures need to be unioned before adding defaults
            List<string> ids = selectedSS.Structures.Select(x => x.Id).ToList();
            List<Tuple<Structure, Structure, string>> structuresToUnion = new List<Tuple<Structure, Structure, string>>(StructureTuningHelper.CheckStructuresToUnion(selectedSS));
            foreach (Tuple<Structure, Structure, string> itr in structuresToUnion) ids.Add(itr.Item3);
            checkStructuresToUnion = false;
            return ids;
        }

        //add structure to spare to the list
        private void AddStructureManipulationItem_Click(object sender, RoutedEventArgs e)
        {
            Button theBtn = sender as Button;
            ScrollViewer theScroller;
            StackPanel theSP;
            if(theBtn.Name.Contains("template"))
            {
                theScroller = templateSpareStructScroller;
                theSP = templateStructuresSP;
            }
            else
            {
                if (checkStructuresToUnion) structureIdsPostUnion = CheckLRStructures();
                theScroller = spareStructScroller;
                theSP = structureManipulationSP;
            }
            //populate the comboboxes
            AddStructureManipulationVolumes(new List<Tuple<string, TSManipulationType, double>> { Tuple.Create("--select--", TSManipulationType.None, 0.0) }, theSP);
            theScroller.ScrollToBottom();
        }

        //add the header to the structure manipulation list (basically just add some labels to make it look nice)
        private void AddStructureManipulationHeader(StackPanel theSP)
        {
            theSP.Children.Add(StructureTuningUIHelper.GetTSManipulationHeader(theSP));
        }

        //populate the structure sparing list. This method is called whether the add structure or add defaults buttons are hit (because a vector containing the list of structures is passed as an argument to this method)
        private void AddStructureManipulationVolumes(List<Tuple<string, TSManipulationType, double>> defaultList, StackPanel theSP)
        {
            if (selectedSS == null) 
            { 
                log.LogError("Error! Please select a Structure Set before add tuning structure manipulations!"); 
                return; 
            }
            int counter;
            string clearBtnNamePrefix;
            if (theSP.Name.Contains("template"))
            {
                counter = clearTemplateSpareBtnCounter;
                clearBtnNamePrefix = "templateClearSpareStructBtn";
            }
            else
            {
                counter = clearSpareBtnCounter;
                clearBtnNamePrefix = "clearSpareStructBtn";
            }
            if (theSP.Children.Count == 0) AddStructureManipulationHeader(theSP);
            foreach (Tuple<string, TSManipulationType, double> itr in defaultList)
            {
                counter++;
                theSP.Children.Add(StructureTuningUIHelper.AddTSManipulation(theSP, 
                                                                             structureIdsPostUnion, 
                                                                             itr, 
                                                                             clearBtnNamePrefix, 
                                                                             counter, 
                                                                             (delegate (object sender, SelectionChangedEventArgs e) { StructureManipulationType_SelectionChanged(theSP, sender, e); }), 
                                                                             new RoutedEventHandler(this.ClearStructureManipulationItem_Click)));
            }
        }

        private void StructureManipulationType_SelectionChanged(StackPanel theSP, object sender, EventArgs e)
        {
            //not the most elegent code, but it works. Basically, it finds the combobox where the selection was changed and increments one additional child to get the add margin text box. Then it can change
            //the visibility of this textbox based on the sparing type selected for this structure
            ComboBox c = (ComboBox)sender;
            bool row = false;
            foreach (object obj in theSP.Children)
            {
                foreach (object obj1 in ((StackPanel)obj).Children)
                {
                    //the btn has a unique tag to it, so we can just loop through all children in the structureManipulationSP children list and find which button is equivalent to our button
                    if (row)
                    {
                        if (c.SelectedItem.ToString() != "Mean Dose < Rx Dose" &&
                            c.SelectedItem.ToString() != "Crop target from structure" &&
                            c.SelectedItem.ToString() != "Crop from Body")
                        {
                            (obj1 as TextBox).Visibility = Visibility.Hidden;
                        }
                        else (obj1 as TextBox).Visibility = Visibility.Visible;
                        return;
                    }
                    if (obj1.Equals(c)) row = true;
                }
            }
        }

        //method to clear and individual row in the structure sparing list (i.e., remove a single structure)
        private void ClearStructureManipulationItem_Click(object sender, EventArgs e)
        {
            if (GeneralUIHelper.ClearRow(sender, (sender as Button).Name.Contains("template") ? templateStructuresSP : structureManipulationSP))
            {
                ClearStructureManipulationsList((sender as Button).Name.Contains("template") ? templateClearSpareStructuresBtn : ClearStructureManipulationsBtn);
            }
        }

        private void AddDefaultStructureManipulations_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSS == null) 
            { 
                log.LogError("Error! The structure set has not been assigned! Choose a structure set and try again!"); 
                return; 
            }
            if (checkStructuresToUnion) structureIdsPostUnion = CheckLRStructures();
            //copy the sparing structures in the defaultSpareStruct list to a temporary vector
            List<Tuple<string, TSManipulationType, double>> templateSpareList = new List<Tuple<string, TSManipulationType, double>>(defaultTSStructureManipulations);
            //add the case-specific sparing structures to the temporary list
            if (templateList.SelectedItem != null)
            {
                templateSpareList = new List<Tuple<string, TSManipulationType, double>>(StructureTuningHelper.AddTemplateSpecificStructureManipulations((templateList.SelectedItem as CSIAutoPlanTemplate).GetTSManipulations(), templateSpareList, pi.Sex));
            }
            if (!templateSpareList.Any()) 
            { 
                log.LogError("Warning! No default tuning structure manipulations contained in the selected template!"); 
                return; 
            }

            string missOutput = "";
            string emptyOutput = "";
            int missCount = 0;
            int emptyCount = 0;
            List<Tuple<string, TSManipulationType, double>> defaultList = new List<Tuple<string, TSManipulationType, double>> { };
            foreach (Tuple<string, TSManipulationType, double> itr in templateSpareList)
            {
                //check to ensure the structures in the templateSpareList vector are actually present in the selected structure set and are actually contoured. If they are, add them to the defaultList vector, which will be passed 
                //to the add_sp_volumes method
                if (!structureIdsPostUnion.Where(x => x.ToLower() == itr.Item1.ToLower()).Any())
                {
                    if (missCount == 0) missOutput = String.Format("Warning! The following default structures are missing from the selected structure list:\n");
                    missOutput += String.Format("{0}\n", itr.Item1);
                    missCount++;
                }
                else if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == itr.Item1.ToLower()) != null && selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == itr.Item1.ToLower()).IsEmpty)
                {
                    if (emptyCount == 0) emptyOutput = String.Format("Warning! The following default structures are present but empty:\n");
                    emptyOutput += String.Format("{0}\n", itr.Item1);
                    emptyCount++;
                }
                else defaultList.Add(Tuple.Create(structureIdsPostUnion.First(x => x.ToLower() == itr.Item1.ToLower()), itr.Item2, itr.Item3));
            }

            ClearStructureManipulationsList(ClearStructureManipulationsBtn);
            AddStructureManipulationVolumes(defaultList, structureManipulationSP);
            if (missCount > 0) log.LogError(missOutput);
            if (emptyCount > 0) log.LogError(emptyOutput);
        }

        //wipe the displayed list of sparing structures
        private void ClearStructureManipulations_Click(object sender, RoutedEventArgs e) 
        { 
            ClearStructureManipulationsList((sender as Button)); 
        }

        private void ClearStructureManipulationsList(Button theBtn)
        {
            if(theBtn.Name.Contains("template"))
            {
                templateStructuresSP.Children.Clear();
                clearTemplateSpareBtnCounter = 0;
            }
            else
            {
                structureManipulationSP.Children.Clear();
                clearSpareBtnCounter = 0;
            }
        }

        private void PerformTSStructureGenerationManipulation_Click(object sender, RoutedEventArgs e)
        {
            //ensure the targets have been specified prior to generating and manipulating the tuning structures
            if(!prescriptions.Any())
            {
                log.LogError("Please set the targets first on the 'Set Targets' tab!");
                return;
            }

            List<Tuple<string, string>> createTSStructureList;
            List<Tuple<string, double, double, double>> createRingList;
            List<Tuple<string, TSManipulationType, double>> TSManipulationList;
            //get sparing structure and tuning structure lists from the UI
            (List<Tuple<string, string>>, StringBuilder) parseCreateTSList = StructureTuningUIHelper.ParseCreateTSStructureList(TSGenerationSP);
            (List<Tuple<string, double, double, double>>, StringBuilder) parseCreateRingList = RingUIHelper.ParseCreateRingList(createRingsSP);
            (List<Tuple<string, TSManipulationType, double>>, StringBuilder) parseTSManipulationList = StructureTuningUIHelper.ParseTSManipulationList(structureManipulationSP);
            if (!string.IsNullOrEmpty(parseCreateTSList.Item2.ToString()))
            {
                log.LogError(parseCreateTSList.Item2);
                return;
            }
            if (!string.IsNullOrEmpty(parseCreateRingList.Item2.ToString()))
            {
                log.LogError(parseCreateRingList.Item2);
                return;
            }
            if (!string.IsNullOrEmpty(parseTSManipulationList.Item2.ToString()))
            {
                log.LogError(parseTSManipulationList.Item2);
                return;
            }
            
            createTSStructureList = new List<Tuple<string, string>>(parseCreateTSList.Item1);
            createRingList = new List<Tuple<string, double, double, double>>(parseCreateRingList.Item1);
            TSManipulationList = new List<Tuple<string, TSManipulationType, double>>(parseTSManipulationList.Item1);
            List<string> cropAndOverlapStructs = new List<string>((templateList.SelectedItem as CSIAutoPlanTemplate).GetCropAndOverlapStructures());

            //create an instance of the generateTS_CSI class, passing the tuning structure list, structure sparing list, targets, prescriptions, and the selected structure set
            GenerateTS_CSI generate = new GenerateTS_CSI(createTSStructureList, parseTSManipulationList.Item1, createRingList, prescriptions, selectedSS, cropAndOverlapStructs);
            pi.BeginModifications();
            bool result = generate.Execute();
            //grab the log output regardless if it passes or fails
            log.AppendLogOutput("TS Generation and manipulation output:", generate.GetLogOutput());
            if (result) return;

            //does the structure sparing list need to be updated? This occurs when structures the user elected to perform manipulations on are high resolution. Since Eclipse can't perform
            //boolean operations on structures of two different resolutions, code was added to the generateTS class to automatically convert these structures to low resolution with the name of
            // '<original structure Id>_lowRes'. When these structures are converted to low resolution, the updateSparingList flag in the generateTS class is set to true to tell this class that the 
            //structure sparing list needs to be updated with the new low resolution structures.
            if (generate.GetUpdateSparingListStatus())
            {
                ClearStructureManipulationsList(ClearStructureManipulationsBtn);
                //update the structure sparing list in this class and update the structure sparing list displayed to the user in TS Generation tab
                TSManipulationList = generate.GetSparingList();
                AddStructureManipulationVolumes(TSManipulationList, structureManipulationSP);
            }
            //the number of isocenters will always be equal to the number of vmat isocenters for vmat csi
            isoNames = generate.GetIsoNames();

            //populate the beams and optimization tabs
            PopulateBeamsTab();
            if (generate.GetTargetCropOverlapManipulations().Any() || generate.GetAddedRings().Any())
            {
                List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> tmpList = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>{ };
                if(generate.GetTargetCropOverlapManipulations().Any()) tmpList = OptimizationSetupHelper.UpdateOptimizationConstraints(generate.GetTargetCropOverlapManipulations(), prescriptions, templateList.SelectedItem, tmpList);
                if(generate.GetAddedRings().Any()) tmpList = OptimizationSetupHelper.UpdateOptimizationConstraints(generate.GetAddedRings(), prescriptions, templateList.SelectedItem, tmpList);
                //handles if crop/overlap operations were performed for all targets and the optimization constraints need to be updated
                PopulateOptimizationTab(optParametersSP, tmpList);
            }
            else PopulateOptimizationTab(optParametersSP);
            isModified = true;
            structureTuningTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
            TSManipulationTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
            beamPlacementTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed;
            log.AddedStructures = generate.GetAddedStructures();
            log.StructureManipulations = TSManipulationList;
            log.NormalizationVolumes = generate.GetNormalizationVolumes();
            log.IsoNames = isoNames;
        }
        #endregion

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region BeamPlacement
        //stuff related to beam placement tab
        private void ContourOverlapInfo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Selecting this option will contour the (approximate) overlap between fields in adjacent isocenters in the VMAT plan and assign the resulting structures as targets in the optimization.");
        }

        private void ContourOverlap_Checked(object sender, RoutedEventArgs e)
        {
            if (contourOverlap_chkbox.IsChecked.Value)
            {
                contourOverlapLabel.Visibility = Visibility.Visible;
                contourOverlapTB.Visibility = Visibility.Visible;
            }
            else
            {
                contourOverlapLabel.Visibility = Visibility.Hidden;
                contourOverlapTB.Visibility = Visibility.Hidden;
            }
        }

        private void PopulateBeamsTab()
        {
            //default option to contour overlap between fields in adjacent isocenters and assign the resulting structures as targets
            contourOverlap_chkbox.IsChecked = contourOverlap;
            ContourOverlap_Checked(null, null);
            contourOverlapTB.Text = contourFieldOverlapMargin;

            beamPlacementSP.Children.Clear();

            //number of isocenters = number of vmat isocenters
            List<StackPanel> SPList = BeamPlacementUIHelper.PopulateBeamsTabHelper(structureManipulationSP, linacs, beamEnergies, isoNames, beamsPerIso);
            if (!SPList.Any()) return;
            foreach (StackPanel s in SPList) beamPlacementSP.Children.Add(s);
        }

        private void CreatePlanAndSetBeams_Click(object sender, RoutedEventArgs e)
        {
            if (beamPlacementSP.Children.Count == 0)
            {
                log.LogError("No isocenters present to place beams!");
                return;
            }

            (string, string, List<List<int>>, StringBuilder) parseSelections = BeamPlacementUIHelper.GetBeamSelections(beamPlacementSP, isoNames);
            if (string.IsNullOrEmpty(parseSelections.Item1))
            {
                log.LogError(parseSelections.Item4);
                return;
            }

            string chosenLinac = parseSelections.Item1;
            string chosenEnergy = parseSelections.Item2;
            List<List<int>> numBeams = parseSelections.Item3;

            //now that we have a list of plans each with a list of isocenter names, we want to make a new list of plans each with a list of tuples of isocenter names and beams per isocenter
            List<Tuple<string, List<Tuple<string, int>>>> planIsoBeamInfo = new List<Tuple<string, List<Tuple<string, int>>>> { };
            int count = 0;
            foreach(Tuple<string,List<string>> itr in isoNames)
            {
                List<Tuple<string, int>> isoNameBeams = new List<Tuple<string, int>> { };
                for(int i = 0; i < itr.Item2.Count; i++) isoNameBeams.Add(new Tuple<string, int>(itr.Item2.ElementAt(i), numBeams.ElementAt(count).ElementAt(i)));
                planIsoBeamInfo.Add(new Tuple<string, List<Tuple<string, int>>>(itr.Item1, new List<Tuple<string, int>>(isoNameBeams)));
                count++;
            }

            //Added code to account for the scenario where the user either requested or did not request to contour the overlap between fields in adjacent isocenew OptimizationSetupUIHelper()nters
            bool contourOverlap = false;
            double contourOverlapMargin = 0.0;
            if (contourOverlap_chkbox.IsChecked.Value)
            {
                //ensure the value entered in the added margin text box for contouring field overlap is a valid double
                if (!double.TryParse(contourOverlapTB.Text, out contourOverlapMargin))
                {
                    log.LogError("Error! The entered added margin for the contour overlap text box is NaN! Please enter a valid number and try again!");
                    return;
                }
                contourOverlap = true;
                //convert from cm to mm
                contourOverlapMargin *= 10.0;
            }
            PlaceBeams_CSI place = new PlaceBeams_CSI(selectedSS, 
                                                      planIsoBeamInfo, 
                                                      collRot, 
                                                      autoFitJaws,
                                                      chosenLinac, 
                                                      chosenEnergy, 
                                                      calculationModel, 
                                                      optimizationModel, 
                                                      useGPUdose, 
                                                      useGPUoptimization, 
                                                      MRrestartLevel, 
                                                      contourOverlap,
                                                      contourOverlapMargin);

            place.Initialize("VMAT CSI", prescriptions);
            place.Execute();
            log.AppendLogOutput("Plan generation and beam placement output:", place.GetLogOutput());
            VMATplans = new List<ExternalPlanSetup>(place.GetGeneratedPlans());
            if (!VMATplans.Any()) return;

            //if the user elected to contour the overlap between fields in adjacent isocenters, get this list of structures from the placeBeams class and copy them to the jnxs vector
            //also repopulate the optimization tab (will include the newly added field junction structures)!
            if (contourOverlap_chkbox.IsChecked.Value)
            {
                jnxs = place.GetFieldJunctionStructures();
                (List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>, StringBuilder) parsedOptimizationConstraits = OptimizationSetupUIHelper.ParseOptConstraints(optParametersSP);
                if (parsedOptimizationConstraits.Item1.Any())
                {
                    ClearOptimizationConstraintsList(optParametersSP);
                    PopulateOptimizationTab(optParametersSP, parsedOptimizationConstraits.Item1);
                }
                else log.LogError(parsedOptimizationConstraits.Item2);
            }

            beamPlacementTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
            optimizationSetupTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed;
            //list the plan UIDs by creation date (initial always gets created first, then boost)
            log.PlanUIDs = VMATplans.OrderBy(x => x.CreationDateTime).Select(y => y.UID).ToList();
            isModified = true;
        }
        #endregion

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region OptimizationSetup
        //stuff related to optimization setup tab
        private void PopulateOptimizationTab(StackPanel theSP, List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> tmpList = null, bool checkIfStructurePresentInSS = true)
        {
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> defaultListList = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };
            if(tmpList == null)
            {
                //tmplist is empty indicating that no optimization constraints were present on the UI when this method was called
                //retrieve constraints from template
                (List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> constraints, StringBuilder errorMessage) parsedConstraints = OptimizationSetupHelper.RetrieveOptConstraintsFromTemplate(templateList.SelectedItem as CSIAutoPlanTemplate, prescriptions);
                if(!parsedConstraints.constraints.Any())
                {
                    log.LogError(parsedConstraints.errorMessage);
                    return;
                }
                tmpList = parsedConstraints.constraints;
            }

            if(checkIfStructurePresentInSS)
            {
                foreach (Tuple<string,List<Tuple<string, OptimizationObjectiveType, double, double, int>>> itr in tmpList)
                {
                    List<Tuple<string, OptimizationObjectiveType, double, double, int>> defaultList = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
                    foreach (Tuple<string, OptimizationObjectiveType, double, double, int> opt in itr.Item2)
                    {
                        //always add PTV objectives to optimization objectives list
                        if (opt.Item1.Contains("--select--") || opt.Item1.Contains("PTV")) defaultList.Add(opt);
                        //only add template optimization objectives for each structure to default list if that structure is present in the selected structure set and contoured
                        //12-22-2020 coded added to account for the situation where the structure selected for sparing had to be converted to a low resolution structure
                        else if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == (opt.Item1 + "_lowRes").ToLower()) != null && 
                                 !selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == (opt.Item1 + "_lowRes").ToLower()).IsEmpty) defaultList.Add(Tuple.Create(selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == (opt.Item1 + "_lowRes").ToLower()).Id, opt.Item2, opt.Item3, opt.Item4, opt.Item5));
                        else if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == opt.Item1.ToLower()) != null && !selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == opt.Item1.ToLower()).IsEmpty) defaultList.Add(opt);
                    }
                    defaultListList.Add(Tuple.Create(itr.Item1, new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(defaultList)));
                }
            }
            else
            {
                //do NOT check to ensure structures in optimization constraint list are present in structure set before adding them to the UI list
                defaultListList = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> (tmpList);
            }

            if(jnxs.Any())
            {
                defaultListList = InsertTSJnxOptConstraints(defaultListList);
            }

            //12/27/2022 this line needs to be fixed as it assumes prescriptions is arranged such that each entry in the list contains a unique plan ID
            //1/18/2023 super ugly, but it works. A simple check is performed to ensure that we won't exceed the number of prescriptions in the loop
            //an issue for the following line is that boost constraints might end up being added to the initial plan (if there are two prescriptions for the initial plan)
            //need to implement function to get unique plan Id's sorted by Rx
            foreach (Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>> itr in defaultListList) AddOptimizationConstraintItems(itr.Item2, itr.Item1, theSP);
        }

        private List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> InsertTSJnxOptConstraints(List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> list)
        {
            //we want to insert the optimization constraints for these junction structure right after the ptv constraints, so find the last index of the target ptv structure and insert
            //the junction structure constraints directly after the target structure constraints
            foreach(Tuple < string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>> itr in list)
            {
                if(jnxs.Any(x => string.Equals(x.Item1.Id.ToLower(), itr.Item1.ToLower())))
                {
                    int index = itr.Item2.FindLastIndex(x => x.Item1.ToLower().Contains("ptv") || x.Item1.ToLower().Contains("ts_overlap"));
                    double rxDose = TargetsHelper.GetHighestRxForPlan(prescriptions, itr.Item1);
                    foreach (Structure itr1 in jnxs.First(x => string.Equals(x.Item1.Id.ToLower(), itr.Item1.ToLower())).Item2)
                    {
                        //per Nataliya's instructions, add both a lower and upper constraint to the junction volumes. Make the constraints match those of the ptv target
                        itr.Item2.Insert(++index, new Tuple<string, OptimizationObjectiveType, double, double, int>(itr1.Id, OptimizationObjectiveType.Lower, rxDose, 100.0, 100));
                        itr.Item2.Insert(++index, new Tuple<string, OptimizationObjectiveType, double, double, int>(itr1.Id, OptimizationObjectiveType.Upper, rxDose * 1.01, 0.0, 100));
                    }
                }
            }
            return list;
        }

        private void ScanSSAndAddOptimizationConstraints_Click(object sender, RoutedEventArgs e)
        {
            if (prescriptions.Any())
            {
                ClearOptimizationConstraintsList(optParametersSP);
                PopulateOptimizationTab(optParametersSP);
            }
            else
            {
                Course course = pi.Courses.FirstOrDefault(x => x.Id.ToLower() == "vmat csi");
                if (course == null)
                {
                    log.LogError("Error: No VMAT CSI course found! \nYou must create a plan before scanning the structure set to add optimization constraints!");
                    return;
                }
                List<ExternalPlanSetup> thePlans = course.ExternalPlanSetups.OrderByDescending(x => x.TotalDose).ToList();
                PopulateOptimizationTab(optParametersSP, null, true);
                //list the plan UIDs in ascending order
                log.PlanUIDs = thePlans.OrderBy(x => x.CreationDateTime).Select(y => y.UID).ToList();
            }
        }

        private void AssignOptimizationConstraints_Click(object sender, RoutedEventArgs e)
        {
            (List<Tuple<string,List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>, StringBuilder) parsedOptimizationConstraints = OptimizationSetupUIHelper.ParseOptConstraints(optParametersSP);
            if (!parsedOptimizationConstraints.Item1.Any())
            {
                log.LogError(parsedOptimizationConstraints.Item2);
                return;
            }
            bool constraintsAssigned = false;
            Course theCourse = null;
            if(!VMATplans.Any())
            {
                theCourse = pi.Courses.FirstOrDefault(x => x.Id.ToLower() == "vmat csi");
                pi.BeginModifications();
            }
            foreach (Tuple<string,List<Tuple<string, OptimizationObjectiveType, double,double,int>>> itr in parsedOptimizationConstraints.Item1)
            {
                ExternalPlanSetup plan = null;
                
                //additional check if the plan was not found in the list of VMATplans
                if(theCourse != null) plan = theCourse.ExternalPlanSetups.FirstOrDefault(x => x.Id == itr.Item1);
                else plan = VMATplans.FirstOrDefault(x => x.Id == itr.Item1);
                if (plan != null)
                {
                    if (plan.OptimizationSetup.Objectives.Count() > 0)
                    {
                        foreach (OptimizationObjective o in plan.OptimizationSetup.Objectives) plan.OptimizationSetup.RemoveObjective(o);
                    }
                    OptimizationSetupUIHelper.AssignOptConstraints(itr.Item2, plan, true, 0.0);
                    constraintsAssigned = true;
                }
                else log.LogError(String.Format("_{0} not found!", itr.Item1));
            }
            if(constraintsAssigned)
            {
                string message = "Optimization objectives have been successfully set!" + Environment.NewLine + Environment.NewLine + "Please review the generated structures, placed isocenters, placed beams, and optimization parameters!";
                MessageBox.Show(message);
                isModified = true;
            }
            optimizationSetupTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
            /*
            if (VMATplan == null)
            {
                //search for a course named VMAT TBI. If it is found, search for a plan named _VMAT TBI inside the VMAT TBI course. If neither are found, throw an error and return
                if (!pi.Courses.Where(x => x.Id == "VMAT TBI").Any() || !pi.Courses.First(x => x.Id == "VMAT TBI").PlanSetups.Where(x => x.Id == "_VMAT TBI").Any())
                {
                    MessageBox.Show("No course or plan named 'VMAT TBI' and '_VMAT TBI' found! Exiting...");
                    return;
                }
                //if both are found, grab an instance of that plan
                VMATplan = pi.Courses.First(x => x.Id == "VMAT TBI").PlanSetups.First(x => x.Id == "_VMAT TBI") as ExternalPlanSetup;
                pi.BeginModifications();
            }
            if (VMATplan.OptimizationSetup.Objectives.Count() > 0)
            {
                //the plan has existing objectives, which need to be removed be assigning the new objectives
                foreach (OptimizationObjective o in VMATplan.OptimizationSetup.Objectives) VMATplan.OptimizationSetup.RemoveObjective(o);
            }
            //optimization parameter list, the plan object, enable jaw tracking?, Auto NTO priority
            helper.assignOptConstraints(optParametersList, VMATplan, true, 0.0);
            */

            //ConfirmPrompt CUI = new ConfirmPrompt();
            //CUI.message.Text = "Optimization objectives have been successfully set!" + Environment.NewLine + Environment.NewLine + "Save changes and launch optimization loop?";
            //CUI.confirmBTN.Text = "Yes";
            //CUI.cancelBTN.Text = "No";
            //CUI.ShowDialog();
            //if (CUI.confirm)
            //{
            //    string binDir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            //    string optLoopExe = Directory.GetFiles(binDir, "*.exe").FirstOrDefault(x => x.Contains("VMATTBI_optLoopMT"));
            //    optLoopProcess = new ProcessStartInfo(optLoopExe);
            //    optLoopProcess.Arguments = String.Format("{0} {1}", pi.Id, configFile);
            //    autoSave = true;
            //    this.Close();
            //}
            //else
            //{
            //if (optParametersList.Where(x => x.Item1.ToLower().Contains("_lowres")).Any()) message += "\n\nBE SURE TO VERIFY THE ACCURACY OF THE GENERATED LOW-RESOLUTION CONTOURS!";
            //if (numIsos != 0 && numIsos != numVMATIsos)
            //{
            //    //VMAT only TBI plan was created with the script in this instance info or the user wants to only set the optimization constraints
            //    message += "\n\nFor the AP/PA Legs plan, be sure to change the orientation from head-first supine to feet-first supine!";
            //}
            //}
            //autoSave = true;
            log.OptimizationConstraints = parsedOptimizationConstraints.Item1;
        }

        private void AddOptimizationConstraint_Click(object sender, RoutedEventArgs e)
        {
            Button theBtn = sender as Button;
            StackPanel theSP;
            if (theBtn.Name.Contains("template")) theSP = templateOptParams_sp;
            else theSP = optParametersSP;
            if (!prescriptions.Any()) return;
            ExternalPlanSetup thePlan = null;
            if (!VMATplans.Any()) return;
            if (VMATplans.Count > 1)
            {
                SelectItemPrompt SIP = new SelectItemPrompt("Please selct a plan to add a constraint!", new List<string>(VMATplans.Select(x => x.Id))); ;
                //SIP.itemCombo.Items.Add("Both");
                SIP.ShowDialog();
                if (SIP.GetSelection()) thePlan = VMATplans.FirstOrDefault(x => string.Equals(x.Id, SIP.GetSelectedItem()));
                else return;
                if (thePlan == null) 
                { 
                    log.LogError("Plan not found! Exiting!"); 
                    return; 
                }
            }
            else thePlan = VMATplans.First();
            int index = prescriptions.IndexOf(prescriptions.FirstOrDefault(x => x.Item1 == thePlan.Id));
            if(index != -1)
            {
                List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> tmpListList = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };
                List<Tuple<string, OptimizationObjectiveType, double, double, int>> tmp = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
                if (optParametersSP.Children.Count > 0)
                {
                    //read list of current objectives
                    (List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>, StringBuilder) parsedOptimizationConstraints = OptimizationSetupUIHelper.ParseOptConstraints(theSP, false);
                    if(!parsedOptimizationConstraints.Item1.Any())
                    {
                        log.LogError(parsedOptimizationConstraints.Item2);
                        return;
                    }
                    foreach (Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>> itr in parsedOptimizationConstraints.Item1)
                    {
                        if (itr.Item1 == thePlan.Id)
                        {
                            tmp = new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(itr.Item2)
                            {
                                Tuple.Create("--select--", OptimizationObjectiveType.None, 0.0, 0.0, 0)
                            };
                            tmpListList.Add(Tuple.Create(itr.Item1,tmp));
                        }
                        else tmpListList.Add(itr);
                    }
                }
                else
                {
                    //nothing in the optimization setup UI. Populate constraints with constraints from selected template
                    CSIAutoPlanTemplate selectedTemplate = templateList.SelectedItem as CSIAutoPlanTemplate;
                    if(selectedTemplate != null)
                    {
                        //this is a bit of a mess
                        if(index == 0)
                        {
                            if (selectedTemplate.GetInitOptimizationConstraints().Any()) tmp = new List<Tuple<string, OptimizationObjectiveType, double,double,int>>(selectedTemplate.GetInitOptimizationConstraints());
                            tmp.Add(Tuple.Create("--select--", OptimizationObjectiveType.None, 0.0, 0.0, 0));
                            tmpListList.Add(Tuple.Create(thePlan.Id,tmp));
                            if (selectedTemplate.GetBoostOptimizationConstraints().Any()) tmpListList.Add(Tuple.Create(prescriptions.FirstOrDefault(x => x.Item1 != thePlan.Id).Item1, selectedTemplate.GetBoostOptimizationConstraints()));
                        }
                        else
                        {
                            if (selectedTemplate.GetInitOptimizationConstraints().Any()) tmpListList.Add(Tuple.Create(prescriptions.FirstOrDefault(x => x.Item1 != thePlan.Id).Item1, selectedTemplate.GetInitOptimizationConstraints()));
                            else
                            {
                                log.LogError("Error! There should not be a boost plan with no initial plan!");
                                return;
                            }

                            if (selectedTemplate.GetBoostOptimizationConstraints().Any()) tmp = new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(selectedTemplate.GetBoostOptimizationConstraints());
                            tmp.Add(Tuple.Create("--select--", OptimizationObjectiveType.None, 0.0, 0.0, 0));
                            tmpListList.Add(Tuple.Create(thePlan.Id, tmp));
                        }
                    }
                }
                ClearOptimizationConstraintsList(theSP);
                PopulateOptimizationTab(theSP, tmpListList);
            }
        }

        private void AddOptimizationConstraintsHeader(StackPanel theSP)
        {
            theSP.Children.Add(OptimizationSetupUIHelper.GetOptHeader(theSP.Width));
        }

        private void AddOptimizationConstraintItems(List<Tuple<string, OptimizationObjectiveType, double, double, int>> defaultList, string planId, StackPanel theSP)
        {
            if (selectedSS == null) 
            { 
                log.LogError("Error! The structure set has not been assigned! Choose a structure set and try again!");
                return; 
            }
            int counter;
            string clearBtnNamePrefix;
            if (theSP.Name.Contains("template"))
            {
                counter = clearTemplateOptBtnCounter;
                clearBtnNamePrefix = "templateClearOptConstraintBtn";
            }
            else
            {
                counter = clearOptBtnCounter;
                clearBtnNamePrefix = "clearOptConstraintBtn";
            }
            theSP.Children.Add(OptimizationSetupUIHelper.AddPlanIdtoOptList(theSP, planId));
            AddOptimizationConstraintsHeader(theSP);
            for (int i = 0; i < defaultList.Count; i++)
            {
                counter++;
                theSP.Children.Add(OptimizationSetupUIHelper.AddOptVolume(theSP, selectedSS, defaultList[i], clearBtnNamePrefix, counter, new RoutedEventHandler(this.ClearOptimizationConstraint_Click), theSP.Name.Contains("template")));
            }
        }

        private void ClearOptimizationConstraintsList_Click(object sender, RoutedEventArgs e)
        {
            StackPanel theSP;
            if ((sender as Button).Name.Contains("template")) theSP = templateOptParams_sp;
            else theSP = optParametersSP;
            ClearOptimizationConstraintsList(theSP);
        }

        private void ClearOptimizationConstraint_Click(object sender, EventArgs e)
        {
            StackPanel theSP;
            if ((sender as Button).Name.Contains("template")) theSP = templateOptParams_sp;
            else theSP = optParametersSP;
            if (GeneralUIHelper.ClearRow(sender, theSP)) ClearOptimizationConstraintsList(theSP);
        }

        private void ClearOptimizationConstraintsList(StackPanel theSP)
        {
            theSP.Children.Clear();
            if (theSP.Name.Contains("template")) clearTemplateOptBtnCounter = 0;
            else clearOptBtnCounter = 0;
        }
        #endregion

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region PlanPreparation
        //methods related to plan preparation
        private void GenerateShiftNote_Click(object sender, RoutedEventArgs e)
        {
            if (prep == null)
            {
                //this method assumes no prior knowledge, so it will have to retrive the number of isocenters (vmat and total) and isocenter names explicitly
                Course c = pi.Courses.FirstOrDefault(x => x.Id.ToLower() == "vmat tbi");
                ExternalPlanSetup vmatPlan = null;
                IEnumerable<ExternalPlanSetup> appaPlan = new List<ExternalPlanSetup> { };
                if (c == null)
                {
                    //vmat tbi course not found. Dealbreaker, exit method
                    log.LogError("VMAT TBI course not found! Exiting!");
                    return;
                }
                else
                {
                    //always try and get the AP/PA plans (it's ok if it returns null). NOTE: Nataliya sometimes separates the _legs plan into two separate plans for planning PRIOR to running the optimization loop
                    //therefore, look for all external beam plans that contain the string 'legs'. If 3 plans are found, one of them is the original '_Legs' plan, so we can exculde that from the list
                    appaPlan = c.ExternalPlanSetups.Where(x => x.Id.ToLower().Contains("legs"));
                    if (appaPlan.Count() > 2) appaPlan = c.ExternalPlanSetups.Where(x => x.Id.ToLower().Contains("legs")).Where(x => x.Id.ToLower() != "_legs").OrderBy(o => int.Parse(o.Id.Substring(0, 2).ToString()));
                    //get all plans in the course that don't contain the string 'legs' in the plan ID. If more than 1 exists, prompt the user to select the plan they want to prep
                    IEnumerable<ExternalPlanSetup> plans = c.ExternalPlanSetups.Where(x => !x.Id.ToLower().Contains("legs"));
                    if (plans.Count() > 1)
                    {
                        SelectItemPrompt SIP = new SelectItemPrompt("Multiple plans found in VMAT TBI course!" + Environment.NewLine + "Please select a plan to prep!", plans.Select(x => x.Id).ToList());
                        SIP.ShowDialog();
                        if (!SIP.GetSelection()) return;
                        //get the plan the user chose from the combobox
                        vmatPlan = c.ExternalPlanSetups.FirstOrDefault(x => string.Equals(x.Id, SIP.GetSelectedItem()));
                    }
                    else
                    {
                        //course found and only one or fewer plans inside course with Id != "_Legs", get vmat and ap/pa plans
                        vmatPlan = c.ExternalPlanSetups.FirstOrDefault(x => x.Id.ToLower() == "_vmat tbi");
                    }
                    if (vmatPlan == null)
                    {
                        //vmat plan not found. Dealbreaker, exit method
                        log.LogError("VMAT plan not found! Exiting!");
                        return;
                    }
                }

                //create an instance of the planPep class and pass it the vmatPlan and appaPlan objects as arguments. Get the shift note for the plan of interest
                prep = new PlanPrep_CSI(vmatPlan, appaPlan);
            }
            if (prep.GetShiftNote()) return;

            //let the user know this step has been completed (they can now do the other steps like separate plans and calculate dose)
            shiftTB.Background = System.Windows.Media.Brushes.ForestGreen;
            shiftTB.Text = "YES";
        }

        private void SeparatePlans_Click(object sender, RoutedEventArgs e)
        {
            //The shift note has to be retrieved first! Otherwise, we don't have instances of the plan objects
            if (shiftTB.Text != "YES")
            {
                log.LogError("Please generate the shift note before separating the plans!");
                return;
            }

            //separate the plans
            pi.BeginModifications();
            if (prep.SeparatePlans()) return;

            //let the user know this step has been completed
            separateTB.Background = System.Windows.Media.Brushes.ForestGreen;
            separateTB.Text = "YES";

            //if flash was removed, display the calculate dose button (to remove flash, the script had to wipe the dose in the original plan)
            if (prep.flashRemoved)
            {
                calcDose.Visibility = Visibility.Visible;
                calcDoseTB.Visibility = Visibility.Visible;
            }
            isModified = true;
            planPreparationTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
        }

        private void CalculateDose_Click(object sender, RoutedEventArgs e)
        {
            //the shift note must be retireved and the plans must be separated before calculating dose
            if (shiftTB.Text == "NO" || separateTB.Text == "NO")
            {
                log.LogError("Error! \nYou must generate the shift note AND separate the plan before calculating dose to each plan!");
                return;
            }

            //ask the user if they are sure they want to do this. Each plan will calculate dose sequentially, which will take time
            ConfirmPrompt CUI = new ConfirmPrompt("Warning!" + Environment.NewLine + "This will take some time as each plan needs to be calculated sequentionally!" + Environment.NewLine + "Continue?!");
            CUI.ShowDialog();
            if (!CUI.GetSelection()) return;

            //let the user know the script is working
            calcDoseTB.Background = System.Windows.Media.Brushes.Yellow;
            calcDoseTB.Text = "WORKING";

            prep.CalculateDose();

            //let the user know this step has been completed
            calcDoseTB.Background = System.Windows.Media.Brushes.ForestGreen;
            calcDoseTB.Text = "YES";
        }

        private void GeneratePlanSum_Click(object sender, RoutedEventArgs e)
        {
            //do nothing. Eclipse v15.6 doesn't have this capability, but v16 and later does. This method is a placeholder (the planSum button exists in the UI.xaml file, but its visibility is set to 'hidden')
        }
        #endregion

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region TemplateBuilder
        private void TemplateDosePerFx_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox dosePerFxTB = sender as TextBox;
            TextBox numFxTB, planRxTB;
            if (dosePerFxTB.Name.Contains("Bst"))
            {
                numFxTB = templateBstPlanNumFxTB;
                planRxTB = templateBstPlanRxTB;
            }
            else
            {
                numFxTB = templateInitPlanNumFxTB;
                planRxTB = templateInitPlanRxTB;
            }
            if (!double.TryParse(dosePerFxTB.Text, out double newDoseFx)) planRxTB.Text = "";
            else if (newDoseFx <= 0)
            {
                log.LogError("Error! The dose per fraction must be a number and non-negative!");
                planRxTB.Text = "";
            }
            else ResetTemplateRxDose(dosePerFxTB, numFxTB, planRxTB);
        }

        private void TemplateNumFx_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox numFxTB = sender as TextBox;
            TextBox dosePerFxTB, planRxTB;
            if (numFxTB.Name.Contains("Bst"))
            {
                dosePerFxTB = templateBstPlanDosePerFxTB;
                planRxTB = templateBstPlanRxTB;
            }
            else
            {
                dosePerFxTB = templateInitPlanDosePerFxTB;
                planRxTB = templateInitPlanRxTB;
            }
            if (!int.TryParse(numFxTB.Text, out int newNumFx)) planRxTB.Text = "";
            else if (newNumFx < 1)
            {
                log.LogError("Error! The number of fractions must be an integer and greater than 0!");
                planRxTB.Text = "";
            }
            else ResetTemplateRxDose(dosePerFxTB, numFxTB, planRxTB);
        }

        private void ResetTemplateRxDose(TextBox dosePerFxTB, TextBox numFxTB, TextBox RxTB)
        {
            if (int.TryParse(numFxTB.Text, out int newNumFx) && double.TryParse(dosePerFxTB.Text, out double newDoseFx)) RxTB.Text = (newNumFx * newDoseFx).ToString();
        }

        private void TemplateBuildOptionCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (selectedSS == null) 
            { 
                log.LogError("Error! The structure set has not been assigned! Choose a structure set and try again!"); 
                return; 
            }
            if (templateBuildOptionCB.SelectedItem.ToString().ToLower() == "existing template")
            {
                CSIAutoPlanTemplate theTemplate = null;
                SelectItemPrompt SIP = new SelectItemPrompt("Please select an existing template!", PlanTemplates.Select(x => x.TemplateName).ToList());
                SIP.ShowDialog();
                if (SIP.GetSelection()) theTemplate = PlanTemplates.FirstOrDefault(x => string.Equals(x.GetTemplateName(), SIP.GetSelectedItem()));
                else return;
                if (theTemplate == null) 
                { 
                    log.LogError("Template not found! Exiting!"); 
                    return; 
                }

                //set name
                templateNameTB.Text = theTemplate.GetTemplateName() + "_1";

                //setRx
                templateInitPlanDosePerFxTB.Text = theTemplate.GetInitialRxDosePerFx().ToString();
                templateInitPlanNumFxTB.Text = theTemplate.GetInitialRxNumFx().ToString();
                if (theTemplate.GetBoostRxDosePerFx() > 0.1)
                {
                    templateBstPlanDosePerFxTB.Text = theTemplate.GetBoostRxDosePerFx().ToString();
                    templateBstPlanNumFxTB.Text = theTemplate.GetBoostRxNumFx().ToString();
                }

                //add targets
                List<Tuple<string, double, string>> targetList = new List<Tuple<string, double, string>>(theTemplate.GetTargets());
                ClearAllTargetItems(templateClearTargetList);
                AddTargetVolumes(targetList, targetTemplate_sp);

                //add create TS structures
                GeneralUIHelper.ClearList(templateTSSP);
                if (theTemplate.GetCreateTSStructures().Any()) AddTuningStructureVolumes(theTemplate.GetCreateTSStructures(), templateTSSP);

                GeneralUIHelper.ClearList(templateCreateRingsSP);
                if (theTemplate.GetCreateRings().Any()) AddRingStructures(theTemplate.GetCreateRings(), templateCreateRingsSP);

                //add tuning structure manipulations sparing structures
                ClearStructureManipulationsList(templateClearSpareStructuresBtn);
                if (theTemplate.GetTSManipulations().Any()) AddStructureManipulationVolumes(theTemplate.GetTSManipulations(), templateStructuresSP);

                //add optimization constraints
                (List<Tuple<string,List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>, StringBuilder) parsedConstraints = OptimizationSetupHelper.RetrieveOptConstraintsFromTemplate(theTemplate, targetList);
                if(!parsedConstraints.Item1.Any())
                {
                    log.LogError(parsedConstraints.Item2);
                    return;
                }
                PopulateOptimizationTab(templateOptParams_sp, parsedConstraints.Item1, false);
            }
            else if(templateBuildOptionCB.SelectedItem.ToString().ToLower() == "current parameters")
            {
                //add targets (checked first to ensure the user has actually input some parameters into the UI before trying to make a template based on the current settings)
                (List<Tuple<string, double, string>> targetList, StringBuilder) parsedTargetList = TargetsUIHelper.ParseTargets(targetsSP, selectedSS);
                if (!parsedTargetList.targetList.Any())
                {
                    log.LogError("Error! Enter parameters into the UI before trying to use them to make a new plan template!");
                    return;
                }
                ClearAllTargetItems(templateClearTargetList);
                AddTargetVolumes(parsedTargetList.targetList.OrderBy(x => x.Item2).ToList(), targetTemplate_sp);

                //set name
                templateNameTB.Text = "--new template--";

                //setRx
                templateInitPlanDosePerFxTB.Text = initDosePerFxTB.Text;
                templateInitPlanNumFxTB.Text = initNumFxTB.Text;
                if (!string.IsNullOrEmpty(boostDosePerFxTB.Text))
                {
                    templateBstPlanDosePerFxTB.Text = boostDosePerFxTB.Text;
                    templateBstPlanNumFxTB.Text = boostNumFxTB.Text;
                }


                //add create tuning structures structures
                (List<Tuple<string, string>>, StringBuilder) parsedCreateTSList = StructureTuningUIHelper.ParseCreateTSStructureList(TSGenerationSP);
                if(!string.IsNullOrEmpty(parsedCreateTSList.Item2.ToString()))
                {
                    log.LogError(parsedCreateTSList.Item2);
                    return;
                }
                GeneralUIHelper.ClearList(templateTSSP);
                AddTuningStructureVolumes(parsedCreateTSList.Item1, templateTSSP);

                GeneralUIHelper.ClearList(templateCreateRingsSP);
                (List<Tuple<string, double, double, double>>, StringBuilder) parsedCreateRingList = RingUIHelper.ParseCreateRingList(createRingsSP);
                if(!string.IsNullOrEmpty(parsedCreateRingList.Item2.ToString()))
                {
                    log.LogError(parsedCreateRingList.Item2);
                    return;
                }
                AddRingStructures(parsedCreateRingList.Item1, templateCreateRingsSP);

                //add tuning structure manipulations
                (List<Tuple<string, TSManipulationType, double>>, StringBuilder) parsedTSManipulationList = StructureTuningUIHelper.ParseTSManipulationList(structureManipulationSP);
                if(!string.IsNullOrEmpty(parsedTSManipulationList.Item2.ToString()))
                {
                    log.LogError(parsedTSManipulationList.Item2);
                    return;
                }
                ClearStructureManipulationsList(templateClearSpareStructuresBtn);
                AddStructureManipulationVolumes(parsedTSManipulationList.Item1, templateStructuresSP);

                //add optimization constraints
                (List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>, StringBuilder) parsedOptimizationConstraints = OptimizationSetupUIHelper.ParseOptConstraints(optParametersSP);
                if(parsedOptimizationConstraints.Item1.Any())
                {
                    log.LogError(parsedOptimizationConstraints.Item2);
                    return;
                }
                ClearOptimizationConstraintsList(templateOptParams_sp);
                PopulateOptimizationTab(templateOptParams_sp, parsedOptimizationConstraints.Item1, false);
            }
        }

        private void GenerateTemplatePreview_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSS == null) 
            { 
                log.LogError("Error! Please select a Structure Set before add sparing volumes!"); 
                return; 
            }
            prospectiveTemplate = new CSIAutoPlanTemplate();
            prospectiveTemplate.SetTemplateName(templateNameTB.Text);

            if (double.TryParse(templateInitPlanDosePerFxTB.Text, out double initDosePerFx))
            {
                prospectiveTemplate.SetInitRxDosePerFx(initDosePerFx);
            }
            else
            {
                log.LogError("Error! Initial plan dose per fx not parsed successfully! Fix and try again!"); 
                return; 
            }
            if (int.TryParse(templateInitPlanNumFxTB.Text, out int initNumFx))
            {
                prospectiveTemplate.SetInitialRxNumFx(initNumFx);
            }
            else 
            {
                log.LogError("Error! Initial plan dose per fx not parsed successfully! Fix and try again!");
                return; 
            }
            if (double.TryParse(templateBstPlanDosePerFxTB.Text, out double bstDosePerFx))
            {
                prospectiveTemplate.SetBoostRxDosePerFx(bstDosePerFx);
            }
            else
            { 
                log.LogError("Error! Boost plan dose per fx not parsed successfully! Fix and try again!"); 
                return; 
            }
            if (int.TryParse(templateBstPlanNumFxTB.Text, out int bstNumFx))
            {
                prospectiveTemplate.SetBoostRxNumFx(bstNumFx);
            }
            else
            {
                log.LogError("Error! Boost plan dose per fx not parsed successfully! Fix and try again!"); 
                return; 
            }

            //sort targets by prescription dose (ascending order)
            prospectiveTemplate.SetTargets(TargetsUIHelper.ParseTargets(targetTemplate_sp, selectedSS).Item1.OrderBy(x => x.Item2).ToList());
            prospectiveTemplate.SetCreateTSStructures(StructureTuningUIHelper.ParseCreateTSStructureList(templateTSSP).Item1);
            prospectiveTemplate.SetCreateRings(RingUIHelper.ParseCreateRingList(templateCreateRingsSP).Item1);
            prospectiveTemplate.SetTSManipulations(StructureTuningUIHelper.ParseTSManipulationList(templateStructuresSP).Item1);
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> templateOptParametersListList = OptimizationSetupUIHelper.ParseOptConstraints(templateOptParams_sp).Item1;
            prospectiveTemplate.SetInitOptimizationConstraints(templateOptParametersListList.First().Item2);
            prospectiveTemplate.SetBoostOptimizationConstraints(templateOptParametersListList.Last().Item2);

            templatePreviewTB.Text = TemplateBuilder.GenerateTemplatePreviewText(prospectiveTemplate);
            templatePreviewScroller.ScrollToTop();
        }

        private void SerializeNewTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSS == null) 
            { 
                log.LogError("Error! Please select a Structure Set before add sparing volumes!"); 
                return; 
            }
            if (prospectiveTemplate == null) 
            { 
                log.LogError("Error! Please preview the requested template before building!"); 
                return; 
            }
            string fileName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\templates\\CSI\\CSI_" + prospectiveTemplate.GetTemplateName() + ".ini";
            if (File.Exists(fileName))
            {
                ConfirmPrompt CUI = new ConfirmPrompt("Warning! The requested template file already exists! Overwrite?");
                CUI.ShowDialog();
                if (!CUI.GetSelection()) return;
                if(PlanTemplates.Any(x => string.Equals(x.GetTemplateName(), prospectiveTemplate.GetTemplateName())))
                {
                    int index = PlanTemplates.IndexOf(PlanTemplates.FirstOrDefault(x => string.Equals(x.GetTemplateName(), prospectiveTemplate.GetTemplateName())));
                    PlanTemplates.RemoveAt(index);
                }
            }

            File.WriteAllText(fileName, TemplateBuilder.GenerateSerializedTemplate(prospectiveTemplate));
            PlanTemplates.Add(prospectiveTemplate);
            DisplayConfigurationParameters();
            templateList.ScrollIntoView(prospectiveTemplate);

            templatePreviewTB.Text += String.Format("New template written to: {0}", fileName) + Environment.NewLine;
            templatePreviewScroller.ScrollToBottom();
        }
        #endregion

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region ScriptConfiguration
        //stuff related to script configuration
        //method to display the loaded configuration settings
        private void DisplayConfigurationParameters()
        {
            configTB.Text = "";
            configTB.Text = $"{DateTime.Now}" + Environment.NewLine;
            if (configFile != "") configTB.Text += $"Configuration file: {configFile}" + Environment.NewLine + Environment.NewLine;
            else configTB.Text += "Configuration file: none" + Environment.NewLine + Environment.NewLine;
            configTB.Text += $"Documentation path: {documentationPath}" + Environment.NewLine + Environment.NewLine;
            configTB.Text += $"Image export path: {imgExportPath}" + Environment.NewLine + Environment.NewLine;
            configTB.Text += "Default parameters:" + Environment.NewLine;
            configTB.Text += $"Image export format: {imgExportFormat}" + Environment.NewLine;
            configTB.Text += $"Contour field ovelap: {contourOverlap}" + Environment.NewLine;
            configTB.Text += $"Contour field overlap margin: {contourFieldOverlapMargin} cm" + Environment.NewLine;
            configTB.Text += "Available linacs:" + Environment.NewLine;
            foreach (string l in linacs) configTB.Text += $"    {l}" + Environment.NewLine;
            configTB.Text += "Available photon energies:" + Environment.NewLine;
            foreach (string e in beamEnergies) configTB.Text += $"    {e}" + Environment.NewLine;
            configTB.Text += $"Beams per isocenter: ";
            for (int i = 0; i < beamsPerIso.Length; i++)
            {
                configTB.Text += $"{beamsPerIso.ElementAt(i)}";
                if (i != beamsPerIso.Length - 1) configTB.Text += ", ";
            }
            configTB.Text += Environment.NewLine;
            configTB.Text += "Collimator rotation (deg) order: ";
            for (int i = 0; i < collRot.Length; i++)
            {
                configTB.Text += $"{collRot.ElementAt(i):0.0}";
                if (i != collRot.Length - 1) configTB.Text += ", ";
            }
            configTB.Text += Environment.NewLine;
            configTB.Text += $"AutoFit Jaws to targets: {autoFitJaws} " + Environment.NewLine;
            configTB.Text += $"Photon dose calculation model: {calculationModel}" + Environment.NewLine;
            configTB.Text += $"Use GPU for dose calculation: {useGPUdose}" + Environment.NewLine;
            configTB.Text += $"Photon optimization model: {optimizationModel}" + Environment.NewLine;
            configTB.Text += $"Use GPU for optimization: {useGPUoptimization}" + Environment.NewLine;
            configTB.Text += $"MR level restart at: {MRrestartLevel}" + Environment.NewLine + Environment.NewLine;

            if (prelimTargets.Any())
            {
                configTB.Text += "Requested preliminary taret structures:" + Environment.NewLine;
                configTB.Text += String.Format(" {0, -10} | {1, -15} |", "DICOM type", "Structure Id") + Environment.NewLine;
                foreach (Tuple<string, string> ts in prelimTargets) configTB.Text += String.Format(" {0, -10} | {1, -15} |" + Environment.NewLine, ts.Item1, ts.Item2);
                configTB.Text += Environment.NewLine;
            }
            else configTB.Text += "No general TS manipulations requested!" + Environment.NewLine + Environment.NewLine;

            if (defaultTSStructures.Any())
            {
                configTB.Text += "Requested general tuning structures:" + Environment.NewLine;
                configTB.Text += String.Format(" {0, -10} | {1, -15} |", "DICOM type", "Structure Id") + Environment.NewLine;
                foreach (Tuple<string, string> ts in defaultTSStructures) configTB.Text += String.Format(" {0, -10} | {1, -15} |" + Environment.NewLine, ts.Item1, ts.Item2);
                configTB.Text += Environment.NewLine;
            }
            else configTB.Text += "No general TS manipulations requested!" + Environment.NewLine + Environment.NewLine;

            if (defaultTSStructureManipulations.Any())
            {
                configTB.Text += "Default TS manipulations:" + Environment.NewLine;
                configTB.Text += String.Format(" {0, -15} | {1, -26} | {2, -11} |", "structure Id", "sparing type", "margin (cm)") + Environment.NewLine;
                foreach (Tuple<string, TSManipulationType, double> itr in defaultTSStructureManipulations) configTB.Text += String.Format(" {0, -15} | {1, -26} | {2,-11:N1} |" + Environment.NewLine, itr.Item1, itr.Item2.ToString(), itr.Item3);
                configTB.Text += Environment.NewLine;
            }
            else configTB.Text += "No default TS manipulations to list" + Environment.NewLine + Environment.NewLine;

            if(PlanTemplates.Any()) configTB.Text += ConfigurationUIHelper.PrintPlanTemplateConfigurationParameters(PlanTemplates.ToList()).ToString();

            configScroller.ScrollToTop();
        }

        private void LoadNewConfigFile_Click(object sender, RoutedEventArgs e)
        {
            //load a configuration file different from the default in the executing assembly folder
            configFile = "";
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                InitialDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\",
                Filter = "ini files (*.ini)|*.ini|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog().Value) 
            { 
                if (!LoadConfigurationSettings(openFileDialog.FileName)) DisplayConfigurationParameters(); 
                else log.LogError("Error! Selected file is NOT valid!"); 
            }
        }

        //parse the relevant data in the configuration file
        private bool LoadConfigurationSettings(string file)
        {
            configFile = file;
            //encapsulate everything in a try-catch statment so I can be a bit lazier about data checking of the configuration settings (i.e., if a parameter or value is bad the script won't crash)
            try
            {
                using (StreamReader reader = new StreamReader(configFile))
                {
                    //setup temporary vectors to hold the parsed data
                    string line;
                    List<string> linac_temp = new List<string> { };
                    List<string> energy_temp = new List<string> { };
                    List<Tuple<string, TSManipulationType, double>> defaultTSManipulations_temp = new List<Tuple<string, TSManipulationType, double>> { };
                    List<Tuple<string, string>> prelimTargets_temp = new List<Tuple<string, string>> { };
                    List<Tuple<string, string>> defaultTSstructures_temp = new List<Tuple<string, string>> { };

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
                                if (parameter == "documentation path")
                                {
                                    if (Directory.Exists(value))
                                    {
                                        documentationPath = value;
                                        if (documentationPath.LastIndexOf("\\") != documentationPath.Length - 1) documentationPath += "\\";
                                    }
                                }
                                else if (parameter == "log file path")
                                {
                                    if (Directory.Exists(value))
                                    {
                                        logPath = value;
                                        if (logPath.LastIndexOf("\\") != logPath.Length - 1) logPath += "\\";
                                    }
                                }
                                else if (parameter == "img export location")
                                {
                                    if (Directory.Exists(value))
                                    {
                                        imgExportPath = value;
                                        if (imgExportPath.LastIndexOf("\\") != imgExportPath.Length - 1) imgExportPath += "\\";
                                    }
                                    else log.LogError(String.Format("Warning! {0} does NOT exist!", value));
                                }
                                else if (parameter == "beams per iso")
                                {
                                    //parse the default requested number of beams per isocenter
                                    line = ConfigurationHelper.CropLine(line, "{");
                                    List<int> b = new List<int> { };
                                    //second character should not be the end brace (indicates the last element in the array)
                                    while (line.Substring(1, 1) != "}")
                                    {
                                        b.Add(int.Parse(line.Substring(0, line.IndexOf(","))));
                                        line = ConfigurationHelper.CropLine(line, ",");
                                    }
                                    b.Add(int.Parse(line.Substring(0, line.IndexOf("}"))));
                                    //only override the requested number of beams in the beamsPerIso array  
                                    for (int i = 0; i < b.Count(); i++) { if (i < beamsPerIso.Count()) beamsPerIso[i] = b.ElementAt(i); }
                                }
                                else if (parameter == "collimator rotations")
                                {
                                    //parse the default requested number of beams per isocenter
                                    line = ConfigurationHelper.CropLine(line, "{");
                                    List<double> c = new List<double> { };
                                    //second character should not be the end brace (indicates the last element in the array)
                                    while (line.Contains(","))
                                    {
                                        c.Add(double.Parse(line.Substring(0, line.IndexOf(","))));
                                        line = ConfigurationHelper.CropLine(line, ",");
                                    }
                                    c.Add(double.Parse(line.Substring(0, line.IndexOf("}"))));
                                    for (int i = 0; i < c.Count(); i++) { if (i < 5) collRot[i] = c.ElementAt(i); }
                                }
                                else if (parameter == "img export format") { if (value == "dcm" || value == "png") imgExportFormat = value; else log.LogError("Only png and dcm image formats are supported for export!"); }
                                else if (parameter == "use GPU for dose calculation") useGPUdose = value;
                                else if (parameter == "use GPU for optimization") useGPUoptimization = value;
                                else if (parameter == "MR level restart") MRrestartLevel = value;
                                //other parameters that should be updated
                                else if (parameter == "calculation model") { if (value != "") calculationModel = value; }
                                else if (parameter == "optimization model") { if (value != "") optimizationModel = value; }
                                else if (parameter == "contour field overlap") { if (value != "") contourOverlap = bool.Parse(value); }
                                else if (parameter == "contour field overlap margin") { if (value != "") contourFieldOverlapMargin = value; }
                            }
                            else if (line.Contains("add default TS manipulation")) defaultTSManipulations_temp.Add(ConfigurationHelper.ParseTSManipulation(line));
                            else if (line.Contains("create preliminary target")) prelimTargets_temp.Add(ConfigurationHelper.ParseCreateTS(line));
                            else if (line.Contains("create default TS")) defaultTSstructures_temp.Add(ConfigurationHelper.ParseCreateTS(line));
                            else if (line.Contains("add linac"))
                            {
                                //parse the linacs that should be added. One entry per line
                                line = ConfigurationHelper.CropLine(line, "{");
                                linac_temp.Add(line.Substring(0, line.IndexOf("}")));
                            }
                            else if (line.Contains("add beam energy"))
                            {
                                //parse the photon energies that should be added. One entry per line
                                line = ConfigurationHelper.CropLine(line, "{");
                                energy_temp.Add(line.Substring(0, line.IndexOf("}")));
                            }
                            //else if (line.Contains("add jaw position"))
                            //{
                            //    //parse the default requested number of beams per isocenter
                            //    line = ConfigurationHelper.CropLine(line, "{");
                            //    (bool fail, VRect<double> parsedPositions) = ConfigurationHelper.ParseJawPositions(line);
                            //    if (fail)
                            //    {
                            //        log.LogError("Error! Jaw positions not defined correctly!");
                            //        log.LogError(line);
                            //    }
                            //    else jawPos_temp.Add(parsedPositions);
                            //}
                            else if (line.Contains("auto fit jaws to targets"))
                            {
                                autoFitJaws = true;
                            }
                        }
                    }
                    reader.Close();
                    //anything that is an array needs to be updated AFTER the while loop.
                    if (linac_temp.Any()) linacs = new List<string>(linac_temp);
                    if (energy_temp.Any()) beamEnergies = new List<string>(energy_temp);
                    if (defaultTSManipulations_temp.Any()) defaultTSStructureManipulations = new List<Tuple<string, TSManipulationType, double>>(defaultTSManipulations_temp);
                    if (defaultTSstructures_temp.Any()) defaultTSStructures = new List<Tuple<string, string>>(defaultTSstructures_temp);
                    if (prelimTargets_temp.Any()) prelimTargets = new List<Tuple<string, string>>(prelimTargets_temp);
                }
                
                return false;
            }
            //let the user know if the data parsing failed
            catch (Exception e) 
            { 
                log.LogError(String.Format("Error could not load configuration file because: {0}\n\nAssuming default parameters", e.Message));
                log.LogError(e.StackTrace, true);
                return true; 
            }
        }

        private bool LoadPlanTemplates()
        {
            int count = 1;
            try
            {
                foreach (string itr in Directory.GetFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\templates\\CSI\\", "*.ini").OrderBy(x => x))
                {
                    PlanTemplates.Add(ConfigurationHelper.ReadTemplatePlan(itr, count++));
                }

            }
            catch(Exception e)
            {
                log.LogError(String.Format("Error could not load plan template file because: {0}", e.Message));
                log.LogError(e.StackTrace, true);
                return true;
            }
            return false;
        }
        #endregion
        
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //be sure to close the patient before closing the application. Not doing so will result in unclosed timestamps in eclipse
            //if (autoSave) { app.SaveModifications(); Process.Start(optLoopProcess); }
            if (isModified)
            {
                if (autoSave)
                {
                    app.SaveModifications();
                    log.AppendLogOutput("Modifications saved to database!");
                    log.ChangesSaved = true;
                }
                else
                {
                    SaveChangesPrompt SCP = new SaveChangesPrompt();
                    SCP.ShowDialog();
                    if (SCP.GetSelection())
                    {
                        app.SaveModifications();
                        log.AppendLogOutput("Modifications saved to database!");
                        log.ChangesSaved = true;
                    }
                    else
                    {
                        log.AppendLogOutput("Modifications NOT saved to database!");
                        log.ChangesSaved = false;
                    }
                }
            }
            else
            {
                log.AppendLogOutput("No modifications made to database objects!");
                log.ChangesSaved = false;
            }
            log.User = String.Format("{0} ({1})", app.CurrentUser.Name, app.CurrentUser.Id);
            if (app != null)
            {
                if (pi != null)
                {
                    app.ClosePatient();
                    if(log.Dump())
                    {
                        MessageBox.Show("Error! Could not save log file!");
                    }
                }
                app.Dispose();
            }
        }

        private void MainWindow_SizeChanged(object sender, RoutedEventArgs e)
        {
            SizeToContent = SizeToContent.WidthAndHeight;
        }
    }
}