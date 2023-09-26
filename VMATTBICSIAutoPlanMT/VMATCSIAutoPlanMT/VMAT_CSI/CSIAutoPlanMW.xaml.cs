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
using VMATTBICSIAutoPlanningHelpers.Structs;
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
        string documentationPath;
        //log file path
        string logPath;
        //struct to hold all the import/export info
        ImportExportDataStruct IEData;
        //flag to indicate whether a CT image has been exported (getting connection conflicts because the port is still being used from the first export)
        bool imgExported = false;
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
        //default course ID
        string courseId = "VMAT TBI";
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
        //ts target list
        //plan id, list<original target id, ts target id>
        List<Tuple<string, List<Tuple<string, string>>>> tsTargets = new List<Tuple<string, List<Tuple<string, string>>>> { };
        //planId, lower dose target id, list<manipulation target id, operation>
        List<Tuple<string, string, List<Tuple<string, string>>>> targetCropOverlapManipulations = new List<Tuple<string, string, List<Tuple<string, string>>>> { };
        //target id, ring id, dose (cGy)
        List<Tuple<string, string, double>> addedRings = new List<Tuple<string, string, double>> { };
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
        private VMS.TPS.Common.Model.API.Application app = null;
        bool isModified = false;
        bool autoSave = false;
        bool closePWOnFinish = false;
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

        #region Initialization
        private void LoadDefaultConfigurationFiles()
        {
            //load script configuration and display the settings
            List<string> configurationFiles = new List<string> { };
            configurationFiles.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\VMAT_CSI_config.ini");
            foreach (string itr in configurationFiles) LoadConfigurationSettings(itr);
        }

        private bool InitializeScript(List<string> args)
        {
            try { app = VMS.TPS.Common.Model.API.Application.CreateApplication(); }
            catch (Exception e) { MessageBox.Show($"Warning! Could not generate Aria application instance because: {e.Message}"); }
            string mrn = "";
            string ss = "";
            if (args.Any())
            {
                mrn = args.ElementAt(0);
                ss = args.ElementAt(1);
            }

            IEData = new ImportExportDataStruct();
            documentationPath = ConfigurationHelper.GetDefaultDocumentationPath();
            logPath = ConfigurationHelper.ReadLogPathFromConfigurationFile(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\log_configuration.ini");
            //if the log file path in the configuration file is empty, use the default path
            if (string.IsNullOrEmpty(logPath)) logPath = ConfigurationHelper.GetDefaultLogPath();
            log = new Logger(logPath, PlanType.VMAT_CSI, mrn);
            LoadDefaultConfigurationFiles();
            if (app != null)
            {
                if(OpenPatient(mrn)) return true;
                InitializeStructureSetSelection(ss);

                //check the version information of Eclipse installed on this machine. If it is older than version 15.6, let the user know that this script may not work properly on their system
                if (!double.TryParse(app.ScriptEnvironment.VersionInfo.Substring(0, app.ScriptEnvironment.VersionInfo.LastIndexOf(".")), out double vinfo)) log.LogError("Warning! Could not parse Eclise version number! Proceed with caution!");
                else if (vinfo < 15.6) log.LogError($"Warning! Detected Eclipse version: {vinfo} is older than v15.6! Proceed with caution!");
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
                if (!string.IsNullOrEmpty(ss) && pi.StructureSets.Any(x => string.Equals(x.Id, ss)))
                {
                    selectedSS = pi.StructureSets.First(x => string.Equals(x.Id, ss));
                    SSID.Text = selectedSS.Id;
                }
                else log.LogError("Warning! No structure set in context! Please select a structure set at the top of the GUI!");
                PopulateExportCTTab();
                patientMRNLabel.Content = pi.Id;
            }
            else log.LogError("Could not open patient!");
        }
        #endregion

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(documentationPath + "VMAT-CSI_PrepScript_Guide.pdf")) log.LogError("VMAT-CSI_PrepScript_Guide PDF file does not exist!");
            else Process.Start(documentationPath + "VMAT-CSI_PrepScript_Guide.pdf");
        }

        private void QuickStart_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(documentationPath + "VMAT-CSI_PrepScript_QuickStartGuide.pdf")) log.LogError("VMAT-CSI_PrepScript_QuickStartGuide PDF file does not exist!");
            else Process.Start(documentationPath + "VMAT-CSI_PrepScript_QuickStartGuide.pdf");
        }

        private void StructureSetId_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ClearAllCurrentParameters();

            //update selected structure set
            selectedSS = pi.StructureSets.FirstOrDefault(x => string.Equals(x.Id, SSID.SelectedItem.ToString()));
            log.StructureSet = selectedSS.Id;
            structureIdsPostUnion = CheckLRStructures();
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
            CheckPreliminaryTargets();
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
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region ExportCT
        private void PopulateExportCTTab()
        {
            VMS.TPS.Common.Model.API.Image theImage = null;
            if (selectedSS != null) theImage = selectedSS.Image;
            ExportCTUIHelper.PopulateCTImageSets(ExportCTUIHelper.GetAllCTImagesForPatient(pi).Where(x => x != theImage).ToList(), theImage, CTimageSP);
        }

        //stuff related to Export CT tab
        private void ExportImgInfo_Click(object sender, RoutedEventArgs e)
        {
            ExportCTUIHelper.PrintExportImgInfo();
        }

        private void ExportImg_Click(object sender, RoutedEventArgs e)
        {
            if (app == null || pi == null || imgExported) return;
            //CT image stack panel, patient structure set list, patient id, image export path, image export format
            VMS.TPS.Common.Model.API.Image selectedImage = ExportCTUIHelper.GetSelectedImageForExport(CTimageSP, ExportCTUIHelper.GetAllCTImagesForPatient(pi));
            if(selectedImage != null)
            {
                CTImageExport exporter = new CTImageExport(selectedImage, pi.Id, IEData, closePWOnFinish);
                bool result = exporter.Execute();
                log.AppendLogOutput("Export CT data:", exporter.GetLogOutput());
                log.OpType = ScriptOperationType.ExportCT;
                if (result) return;
                imgExported = true;
                exportCTTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
                this.Close();
            }
            else log.LogError("No image selected for export!");
        }
        #endregion

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Preliminary Targets Generation
        //stuff related to Set Targets tab
        private void CheckPreliminaryTargets()
        {
            if (selectedSS == null)
            {
                return;
            }
            PrelimTargetGenerationSP.Children.Clear();
            List<string> missingPrelimTargets = new List<string> { };
            List<string> approvedTargets = new List<string> { };
            if (prelimTargets.Any())
            {
                foreach (string itr in prelimTargets.Select(x => x.Item2))
                {
                    //needs to be present AND contoured
                    if (!StructureTuningHelper.DoesStructureExistInSS(itr, selectedSS, true))
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
                    if(selectedSS.Structures.Any(x => string.Equals(x.Id.ToLower(), itr.Item1.ToLower()) && 
                                                 !x.IsEmpty &&
                                                 x.ApprovalHistory.First().ApprovalStatus == StructureApprovalStatus.Approved))
                    {
                        approvedTargets.Add(selectedSS.Structures.First(x => string.Equals(x.Id.ToLower(), itr.Item1.ToLower()) && x.ApprovalHistory.First().ApprovalStatus == StructureApprovalStatus.Approved).Id);
                    }
                }
            }

            targetsTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed;
            if (approvedTargets.Any())
            {
                //targets are present and approved
                PrelimTargetsTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
                setTargetsTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed;
            }
            else if (missingPrelimTargets.Any() || CheckIfBrainSpinalCordAreHighRes())
            {
                AddPrelimTargetVolumes(prelimTargets, PrelimTargetGenerationSP);
                PrelimTargetsTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed; 
            }
        }

        private bool CheckIfBrainSpinalCordAreHighRes()
        {
            bool isHighResOrMissing = true;
            if(StructureTuningHelper.DoesStructureExistInSS("Brain", selectedSS, true) && 
               StructureTuningHelper.DoesStructureExistInSS("spinalcord", selectedSS, true))
            {
                if(!StructureTuningHelper.GetStructureFromId("Brain", selectedSS).IsHighResolution &&
                   !StructureTuningHelper.GetStructureFromId("SpinalCord", selectedSS).IsHighResolution)
                {
                    isHighResOrMissing = false;
                }
            }
            return isHighResOrMissing;
        }

        private void CreatePrelimTargetsInfo_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("This tab will prepare the structure set for contouring of the final targets that will be used for planning. Specifically, it will ensure the brain and spinal cord are default resolution.");
            MessageBox.Show(message.ToString());
        }

        private void AddPrelimTargetVolumes(List<Tuple<string, string>> defaultList, StackPanel theSP)
        {
            if (selectedSS == null)
            {
                log.LogError("Error! Please select a Structure Set before adding items to the prepare structure set tab!");
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

            GenerateTargets_CSI generateTargets = new GenerateTargets_CSI(prelimTargets, selectedSS, closePWOnFinish);
            pi.BeginModifications();
            bool result = generateTargets.Execute();
            //grab the log output regardless if it passes or fails
            log.AppendLogOutput("TS Generation and manipulation output:", generateTargets.GetLogOutput());
            log.OpType = ScriptOperationType.GeneratePrelimTargets;
            if (result) return;
            log.AddedPrelimTargetsStructures = generateTargets.GetAddedTargetStructures();
            PrelimTargetsTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
            isModified = true;
            MessageBox.Show("Structure set is prepared and ready for physician to contour targets!");
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
                theScroller = templateTargetsScroller;
                theSP = templateTargetsSP;
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
            if (selectedSS == null)
            {
                log.LogError("Error! The structure set has not been assigned! Choose a structure set and try again!");
                return;
            }
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
            List<Tuple<string, double, string>> targetList = new List<Tuple<string, double, string>>(TargetsUIHelper.AddTargetDefaults((templateList.SelectedItem as CSIAutoPlanTemplate)));
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
            if(btn == null || string.Equals(btn.Name,"clear_target_list") || !btn.Name.Contains("template"))
            {
                targetsSP.Children.Clear();
                clearTargetBtnCounter = 0;
            }
            else
            {
                templateTargetsSP.Children.Clear();
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
                if(theSP.Name.Contains("template") || string.Equals(itr.Item1,"--select--") || StructureTuningHelper.DoesStructureExistInSS(itr.Item1, selectedSS, true))
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
            (List<Tuple<string, double, string>>, StringBuilder) parsedTargets = TargetsUIHelper.ParseTargets(targetsSP);
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
            (List<Tuple<string, string, int, DoseValue, double>>, StringBuilder) parsedPrescriptions = TargetsHelper.BuildPrescriptionList(targets,
                                                                                                                                           initDosePerFxTB.Text,
                                                                                                                                           initNumFxTB.Text,
                                                                                                                                           initRxTB.Text,
                                                                                                                                           boostDosePerFxTB.Text,
                                                                                                                                           boostNumFxTB.Text,
                                                                                                                                           boostRxTB.Text);
            if(!parsedPrescriptions.Item1.Any())
            {
                log.LogError(parsedPrescriptions.Item2);
                return;
            }
            prescriptions = new List<Tuple<string, string, int, DoseValue, double>>(parsedPrescriptions.Item1);
            log.Prescriptions = prescriptions;

            //need targets to be assigned prior to populating the tuning structure tabs
            AddDefaultRings_Click(null, null);
            AddDefaultTuningStructures_Click(null, null);
            AddDefaultCropOverlapOARs_Click(null, null);
            AddDefaultStructureManipulations();

            targetsTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
            setTargetsTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
            structureTuningTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed;
            TSManipulationTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed;
        }

        private (bool, StringBuilder) VerifySelectedTargetsIntegrity(List<Tuple<string, double, string>> parsedTargets)
        {
            //verify selected targets are APPROVED
            bool fail = false;
            StringBuilder sb = new StringBuilder();
            foreach (Tuple<string, double, string> itr in parsedTargets)
            {
                if (!StructureTuningHelper.DoesStructureExistInSS(itr.Item1, selectedSS, true))
                {
                    sb.AppendLine($"Error! {itr.Item1} is either NOT present in structure set or is not contoured!");
                    fail = true;
                }
                else
                {
                    //structure is present and contoured
                    Structure tgt = StructureTuningHelper.GetStructureFromId(itr.Item1, selectedSS);
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
            message += String.Format("TS structure generation involves adding structures to the structure set to shape the dose distribution. These include rings, preliminary targets, etc. E.g.,") + Environment.NewLine;
            message += String.Format("TS_ring900  -->  ring structure around the targets using a nominal dose level of 900 cGy to determine fall-off") + Environment.NewLine;
            message += String.Format("PTV_Spine  -->  preliminary target used to aid physician contouring of the final target that will be approved") + Environment.NewLine;
            message += String.Format("TS structure manipulation involves manipulating/modifying the structure itself or target structures. E.g.,") + Environment.NewLine;
            message += String.Format("(Ovaries, Crop target from structure, 1.5cm)  -->  modify the target structure such that the ovaries structure is cropped from the target with a 1.5 cm margin") + Environment.NewLine;
            message += String.Format("(Brainstem, Contour overlap, 0.0 cm)  -->  Identify the overlapping regions between the brainstem and target structure(s) and contour them as new structures") + Environment.NewLine + Environment.NewLine;
            message += String.Format("Kidneys-1cm  -->  substructure for the Kidneys volume where the Kidneys are contracted by 1 cm") + Environment.NewLine + Environment.NewLine;
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
            if (selectedSS == null)
            {
                log.LogError("Error! The structure set has not been assigned! Choose a structure set and try again!");
                return;
            }
            if (!prescriptions.Any())
            {
                log.LogError("Please set the targets first on the 'Set Targets' tab!");
                return;
            }
            Button theBtn = sender as Button;
            StackPanel theSP = createRingsSP;
            string initRxText = initRxTB.Text;
            string bstRxText = boostRxTB.Text;
            GeneralUIHelper.ClearList(createRingsSP);

            CSIAutoPlanTemplate selectedTemplate = templateList.SelectedItem as CSIAutoPlanTemplate;
            if (selectedTemplate == null)
            {
                //no plan template selected --> copy and scale objectives from an existing template
                string selectedTemplateId = GeneralUIHelper.PromptUserToSelectPlanTemplate(PlanTemplates.Select(x => x.TemplateName).ToList());
                if (string.IsNullOrEmpty(selectedTemplateId))
                {
                    log.LogError("Template not found! Exiting!");
                    return;
                }
                selectedTemplate = PlanTemplates.FirstOrDefault(x => string.Equals(x.GetTemplateName(), selectedTemplateId));
            }
            //selected plan is valid
            //get prescription
            double initRx = 0.1;
            double bstRx = 0.1;

            if (!double.TryParse(initRxText, out initRx))
            {
                log.LogError("Warning! Entered initial plan prescription is not valid! \nCannot scale optimization objectives to requested Rx! Exiting!");
                return;
            }
            if (selectedTemplate.GetBoostRxDosePerFx() != 0.1 && !double.TryParse(bstRxText, out bstRx))
            {
                log.LogError("Warning! Entered boost plan prescription is not valid! \nCannot verify template Rx vs entered Rx! Exiting!");
                return;
            }
                
            //assumes you set all targets and upstream items correctly (as you would have had to place beams prior to this point)
            if (CalculationHelper.AreEqual(selectedTemplate.GetInitialRxDosePerFx() * selectedTemplate.GetInitialRxNumFx(), initRx) && (bstRx == 0.1 || CalculationHelper.AreEqual(selectedTemplate.GetBoostRxDosePerFx() * selectedTemplate.GetBoostRxNumFx(), bstRx)))
            {
                //currently entered prescription is equal to the prescription dose in the selected template. Simply populate the optimization objective list with the objectives from that template
                AddRingStructures(selectedTemplate.GetCreateRings(), theSP);
            }
            else
            {
                //entered prescription differs from prescription in template --> need to rescale all objectives by ratio of prescriptions
                AddRingStructures(RingHelper.RescaleRingDosesToNewRx(selectedTemplate.GetCreateRings(),
                                                                     prescriptions,
                                                                     selectedTemplate.GetInitialRxDosePerFx() * selectedTemplate.GetInitialRxNumFx(),
                                                                     initRx,
                                                                     selectedTemplate.GetBoostRxDosePerFx() * selectedTemplate.GetBoostRxNumFx(),
                                                                     bstRx),
                                                                     theSP);

            }
        }

        private void AddRingStructures(List<Tuple<string, double, double, double>> lists, StackPanel theSP)
        {
            if (selectedSS == null)
            {
                log.LogError("Error! Please select a Structure Set before adding ring structures!");
                return;
            }
            if (!lists.Any()) return;

            if (theSP.Children.Count == 0) theSP.Children.Add(RingUIHelper.GetRingHeader(theSP.Width));
            int counter = 0;
            string clearBtnName = "ClearRingBtn";
            ScrollViewer theScroller;
            if (theSP.Name.Contains("template"))
            {
                clearBtnName = "template" + clearBtnName;
                theScroller = templateCreateRingsScroller;
            }
            else
            {
                theScroller = createRingsScroller;
            }
            for (int i = 0; i < lists.Count; i++)
            {
                if(theSP.Name.Contains("template") || StructureTuningHelper.DoesStructureExistInSS(lists.ElementAt(i).Item1, selectedSS, true))
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
            theScroller.ScrollToBottom();
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

        private void CropContourOverlapInfo_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("What are create/contour overlap structures?");
            message.AppendLine("These structures are OARs which will be used in the TS generation operations to generate new tuning structures for the targets.");
            message.AppendLine("Generated tuning structures include 'crop' structures which are the original targets that have been cropped from all OARs.");
            message.AppendLine("In addition, 'overlap' structures will be created that are the overlapping portions of the original OARs and all targets.");
            message.AppendLine("These target tuning structures are used to carve dose away from high-risk senstive OARs (e.g., brainstem) for sequential boost CSI.");
            message.AppendLine("Once generated, the optimization constraints for the targets will be updated to reflect the generated tuning structures.");
            message.AppendLine("In addition, the normalization volumes for each plan will also be updated.");
            MessageBox.Show(message.ToString());
        }

        private StackPanel GetAppropriateCropOverlapSP(object o)
        {
            Button theBtn = (Button)o;
            StackPanel theSP;
            if (theBtn.Name.Contains("template"))
            {
                theSP = templateCropOverlapOARsSP;
            }
            else
            {
                theSP = cropOverlapOARsSP;
            }
            return theSP;
        }

        private void AddCropOverlapOAR_Click(object sender, RoutedEventArgs e)
        {
            //populate the comboboxes
            AddCropOverlapOARs(new List<string> { "--select--" }, GetAppropriateCropOverlapSP(sender));
        }

        private void AddDefaultCropOverlapOARs_Click(object sender, RoutedEventArgs e)
        {
            if (templateList.SelectedItem != null)
            {
                GeneralUIHelper.ClearList(cropOverlapOARsSP);
                AddCropOverlapOARs((templateList.SelectedItem as CSIAutoPlanTemplate).GetCropAndOverlapStructures(), cropOverlapOARsSP);
                cropOverlapOARsScroller.ScrollToBottom();
            }
        }

        private void AddCropOverlapOARs(List<string> OARs, StackPanel theSP)
        {
            if (selectedSS == null)
            {
                log.LogError("Error! Please select a Structure Set before adding ring structures!");
                return;
            }
            if (!OARs.Any()) return;
            if (theSP.Children.Count == 0) theSP.Children.Add(CropOverlapOARUIHelper.GetCropOverlapHeader());
            int counter = 0;
            string clearBtnName = "ClearCropOverlapOARBtn";
            if (theSP.Name.Contains("template"))
            {
                clearBtnName = "template" + clearBtnName;
            }
            for (int i = 0; i < OARs.Count; i++)
            {
                counter++;
                theSP.Children.Add(CropOverlapOARUIHelper.AddCropOverlapOAR(selectedSS.Structures.Select(x => x.Id).ToList(),
                                                                            OARs[i],
                                                                            clearBtnName,
                                                                            counter,
                                                                            new RoutedEventHandler(this.ClearCropOverlapOARItem_Click),
                                                                            true));
            }
        }

        private void ClearCropOverlapOARItem_Click(object sender, RoutedEventArgs e)
        {
            StackPanel theSP = GetAppropriateCropOverlapSP(sender);
            if (GeneralUIHelper.ClearRow(sender, theSP)) GeneralUIHelper.ClearList(theSP);
        }

        private void ClearCropOverlapOARList_Click(object sender, RoutedEventArgs e)
        {
            GeneralUIHelper.ClearList(GetAppropriateCropOverlapSP(sender));
        }

        private List<string> CheckLRStructures()
        {
            //check if structures need to be unioned before adding defaults
            List<string> ids = selectedSS.Structures.Select(x => x.Id).ToList();
            List<Tuple<Structure, Structure, string>> structuresToUnion = new List<Tuple<Structure, Structure, string>>(StructureTuningHelper.CheckStructuresToUnion(selectedSS));
            foreach (Tuple<Structure, Structure, string> itr in structuresToUnion) ids.Add(itr.Item3);
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
                                                                             new RoutedEventHandler(this.ClearStructureManipulationItem_Click),
                                                                             theSP.Name.Contains("template")));
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
                        if ((TSManipulationType)c.SelectedItem == TSManipulationType.None)
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
            AddDefaultStructureManipulations(true);
        }

        private void AddDefaultStructureManipulations(bool fromButtonClickEvent = false)
        {
            if (selectedSS == null)
            {
                log.LogError("Error! The structure set has not been assigned! Choose a structure set and try again!");
                return;
            }
            //copy the sparing structures in the defaultSpareStruct list to a temporary vector
            List<Tuple<string, TSManipulationType, double>> templateManipulationList = new List<Tuple<string, TSManipulationType, double>>(defaultTSStructureManipulations);
            //add the case-specific sparing structures to the temporary list
            if (templateList.SelectedItem != null)
            {
                templateManipulationList = new List<Tuple<string, TSManipulationType, double>>(StructureTuningHelper.AddTemplateSpecificStructureManipulations((templateList.SelectedItem as CSIAutoPlanTemplate).GetTSManipulations(), templateManipulationList, pi.Sex));
            }
            if (!templateManipulationList.Any())
            {
                if(fromButtonClickEvent) log.LogError("Warning! No default tuning structure manipulations contained in the selected template!");
                return;
            }

            (List<string> missingEmptyStructures, StringBuilder warnings) = StructureTuningUIHelper.VerifyTSManipulationIntputIntegrity(templateManipulationList.Select(x => x.Item1).Distinct().ToList(), structureIdsPostUnion, selectedSS);
            if (missingEmptyStructures.Any()) log.LogError(warnings);

            List<Tuple<string, TSManipulationType, double>> defaultList = new List<Tuple<string, TSManipulationType, double>> { };
            foreach (Tuple<string, TSManipulationType, double> itr in templateManipulationList)
            {
                if (!missingEmptyStructures.Any(x => string.Equals(x, itr.Item1)))
                {
                    defaultList.Add(Tuple.Create(structureIdsPostUnion.First(x => x.ToLower() == itr.Item1.ToLower()), itr.Item2, itr.Item3));
                }
            }

            ClearStructureManipulationsList(ClearStructureManipulationsBtn);
            AddStructureManipulationVolumes(defaultList, structureManipulationSP);
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
            List<string> cropOverlapOARList;
            List<Tuple<string, TSManipulationType, double>> TSManipulationList;
            //get sparing structure and tuning structure lists from the UI
            (List<Tuple<string, string>>, StringBuilder) parseCreateTSList = StructureTuningUIHelper.ParseCreateTSStructureList(TSGenerationSP);
            (List<Tuple<string, double, double, double>>, StringBuilder) parseCreateRingList = RingUIHelper.ParseCreateRingList(createRingsSP);
            (List<string>, StringBuilder) parseCropOverlapOARList = CropOverlapOARUIHelper.ParseCropOverlapOARList(cropOverlapOARsSP);
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
            if (!string.IsNullOrEmpty(parseCropOverlapOARList.Item2.ToString()))
            {
                log.LogError(parseCropOverlapOARList.Item2);
                return;
            }
            if (!string.IsNullOrEmpty(parseTSManipulationList.Item2.ToString()))
            {
                log.LogError(parseTSManipulationList.Item2);
                return;
            }

            createTSStructureList = new List<Tuple<string, string>>(parseCreateTSList.Item1);
            createRingList = new List<Tuple<string, double, double, double>>(parseCreateRingList.Item1);
            cropOverlapOARList = new List<string>(parseCropOverlapOARList.Item1);
            TSManipulationList = new List<Tuple<string, TSManipulationType, double>>(parseTSManipulationList.Item1);

            //create an instance of the generateTS_CSI class, passing the tuning structure list, structure sparing list, targets, prescriptions, and the selected structure set
            GenerateTS_CSI generate = new GenerateTS_CSI(createTSStructureList, parseTSManipulationList.Item1, createRingList, prescriptions, selectedSS, cropOverlapOARList, closePWOnFinish);
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
            if (generate.GetTsTargets().Any()) tsTargets = generate.GetTsTargets();
            if (generate.GetTargetCropOverlapManipulations().Any()) targetCropOverlapManipulations = generate.GetTargetCropOverlapManipulations();
            if (generate.GetAddedRings().Any()) addedRings = generate.GetAddedRings();

            isModified = true;
            structureTuningTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
            TSManipulationTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
            beamPlacementTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed;
            log.AddedStructures = generate.GetAddedStructures();
            log.TSTargets = generate.GetTsTargets().SelectMany(x => x.Item2).ToList();
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
            List<StackPanel> SPList = BeamPlacementUIHelper.PopulateBeamsTabHelper(structureManipulationSP.Width, linacs, beamEnergies, isoNames, beamsPerIso);
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
            bool contourOverlap = contourOverlap_chkbox.IsChecked.Value;
            double contourOverlapMargin = 0.0;
            if (contourOverlap)
            {
                //contour overlap in cm (conversion performed in contourfieldoverlap method in placebeamsbase)
                //ensure the value entered in the added margin text box for contouring field overlap is a valid double
                if (!double.TryParse(contourOverlapTB.Text, out contourOverlapMargin))
                {
                    log.LogError("Error! The entered added margin for the contour overlap text box is NaN! Please enter a valid number and try again!");
                    return;
                }
            }
            PlaceBeams_CSI place = new PlaceBeams_CSI(selectedSS, 
                                                      planIsoBeamInfo, 
                                                      collRot, 
                                                      chosenLinac, 
                                                      chosenEnergy, 
                                                      calculationModel, 
                                                      optimizationModel, 
                                                      useGPUdose, 
                                                      useGPUoptimization, 
                                                      MRrestartLevel, 
                                                      contourOverlap,
                                                      contourOverlapMargin,
                                                      closePWOnFinish);

            place.Initialize(courseId, prescriptions);
            bool result = place.Execute();
            log.AppendLogOutput("Plan generation and beam placement output:", place.GetLogOutput());
            if (result) return;
            VMATplans = new List<ExternalPlanSetup>(place.GetGeneratedVMATPlans());
            if (!VMATplans.Any()) return;

            //if the user elected to contour the overlap between fields in adjacent isocenters, get this list of structures from the placeBeams class and copy them to the jnxs vector
            //also repopulate the optimization tab (will include the newly added field junction structures)!
            if (contourOverlap_chkbox.IsChecked.Value)
            {
                jnxs = place.GetFieldJunctionStructures();
                ClearOptimizationConstraintsList(optParametersSP);
                AddDefaultOptimizationConstraints_Click(null, null);
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
        private void PopulateOptimizationTab(StackPanel theSP, List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> tmpList = null, bool checkIfStructurePresentInSS = true, bool updateTsStructureJnxObjectives = false)
        {
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> defaultListList = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };
            if(tmpList == null)
            {
                //tmplist is empty indicating that no optimization constraints were present on the UI when this method was called
                updateTsStructureJnxObjectives = true;
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
                        else if (StructureTuningHelper.DoesStructureExistInSS(opt.Item1 + "_lowRes", selectedSS, true))
                        {
                            string lowResStructure = opt.Item1 + "_lowRes";
                            defaultList.Add(Tuple.Create(StructureTuningHelper.GetStructureFromId(lowResStructure, selectedSS).Id, opt.Item2, opt.Item3, opt.Item4, opt.Item5));
                        }
                        else if (StructureTuningHelper.DoesStructureExistInSS(opt.Item1, selectedSS, true)) defaultList.Add(opt);
                    }
                    defaultListList.Add(Tuple.Create(itr.Item1, new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(defaultList)));
                }
            }
            else
            {
                //do NOT check to ensure structures in optimization constraint list are present in structure set before adding them to the UI list
                defaultListList = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> (tmpList);
            }

            if (updateTsStructureJnxObjectives)
            {
                defaultListList = OptimizationSetupUIHelper.UpdateOptObjectivesWithTsStructuresAndJnxs(defaultListList,
                                                                                                       prescriptions,
                                                                                                       templateList.SelectedItem,
                                                                                                       tsTargets,
                                                                                                       jnxs,
                                                                                                       targetCropOverlapManipulations,
                                                                                                       addedRings);
            }

            foreach (Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>> itr in defaultListList) AddOptimizationConstraintItems(itr.Item2, itr.Item1, theSP);
        }

        private void AddDefaultOptimizationConstraints_Click(object sender, RoutedEventArgs e)
        {
            Button theBtn = sender as Button;
            StackPanel theSP;
            string initRxText = "";
            string bstRxText = "";
            bool checkIfStructIsInSS = true;
            if (theBtn != null && theBtn.Name.Contains("template"))
            {
                theSP = templateOptParamsSP;
                initRxText = templateInitPlanRxTB.Text;
                bstRxText = templateBstPlanRxTB.Text;
                checkIfStructIsInSS = false;
            }
            else
            {
                theSP = optParametersSP;
                initRxText = initRxTB.Text;
                bstRxText = boostRxTB.Text;
            }
            ClearOptimizationConstraintsList(theSP);
            CSIAutoPlanTemplate selectedTemplate = templateList.SelectedItem as CSIAutoPlanTemplate;
            if (selectedTemplate == null)
            {
                //no plan template selected --> copy and scale objectives from an existing template
                string selectedTemplateId = GeneralUIHelper.PromptUserToSelectPlanTemplate(PlanTemplates.Select(x => x.TemplateName).ToList());
                if (string.IsNullOrEmpty(selectedTemplateId))
                {
                    log.LogError("Template not found! Exiting!");
                    return;
                }
                selectedTemplate = PlanTemplates.FirstOrDefault(x => string.Equals(x.GetTemplateName(), selectedTemplateId));
            }
            //selected plan is valid
            //get prescription
            double initRx = 0.1;
            double bstRx = 0.1;

            if (!double.TryParse(initRxText, out initRx))
            {
                log.LogError("Warning! Entered initial plan prescription is not valid! \nCannot scale optimization objectives to requested Rx! Exiting!");
                return;
            }
            if (selectedTemplate.GetBoostRxDosePerFx() != 0.1 && !double.TryParse(bstRxText, out bstRx))
            {
                log.LogError("Warning! Entered boost plan prescription is not valid! \nCannot verify template Rx vs entered Rx! Exiting!");
                return;
            }
            (List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> constraints, StringBuilder errorMessage) parsedConstraints = OptimizationSetupHelper.RetrieveOptConstraintsFromTemplate(selectedTemplate, prescriptions);
            if (!parsedConstraints.constraints.Any())
            {
                log.LogError(parsedConstraints.errorMessage);
                return;
            }
            //assumes you set all targets and upstream items correctly (as you would have had to place beams prior to this point)
            if (CalculationHelper.AreEqual(selectedTemplate.GetInitialRxDosePerFx() * selectedTemplate.GetInitialRxNumFx(), initRx) && (bstRx == 0.1 || CalculationHelper.AreEqual(selectedTemplate.GetBoostRxDosePerFx() * selectedTemplate.GetBoostRxNumFx(), bstRx)))
            {
                //currently entered prescription is equal to the prescription dose in the selected template. Simply populate the optimization objective list with the objectives from that template
                PopulateOptimizationTab(theSP, parsedConstraints.constraints, checkIfStructIsInSS, true);
            }
            else
            {
                //entered prescription differs from prescription in template --> need to rescale all objectives by ratio of prescriptions
                List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> scaledConstraints = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>
                {
                    Tuple.Create(parsedConstraints.constraints.First().Item1, OptimizationSetupUIHelper.RescalePlanObjectivesToNewRx(parsedConstraints.constraints.First().Item2, selectedTemplate.GetInitialRxDosePerFx() * selectedTemplate.GetInitialRxNumFx(), initRx))
                };
                if(bstRx != 0.1)
                {
                    scaledConstraints.Add(Tuple.Create(parsedConstraints.constraints.Last().Item1, OptimizationSetupUIHelper.RescalePlanObjectivesToNewRx(parsedConstraints.constraints.Last().Item2, selectedTemplate.GetBoostRxDosePerFx() * selectedTemplate.GetBoostRxNumFx(), bstRx)));
                }
                PopulateOptimizationTab(theSP, scaledConstraints, checkIfStructIsInSS, true);
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
                if(VMATplans.Any()) plan = VMATplans.FirstOrDefault(x => x.Id == itr.Item1);
                else plan = theCourse.ExternalPlanSetups.FirstOrDefault(x => x.Id == itr.Item1);
                if (plan != null)
                {
                    if (plan.OptimizationSetup.Objectives.Count() > 0)
                    {
                        foreach (OptimizationObjective o in plan.OptimizationSetup.Objectives) plan.OptimizationSetup.RemoveObjective(o);
                    }
                    OptimizationSetupUIHelper.AssignOptConstraints(itr.Item2, plan, true, 0.0);
                    constraintsAssigned = true;
                }
                else log.LogError($"_{itr.Item1} not found!");
            }
            if(constraintsAssigned)
            {
                string message = "Optimization objectives have been successfully set!" + Environment.NewLine + Environment.NewLine + "Please review the generated structures, placed isocenters, placed beams, and optimization parameters!";
                MessageBox.Show(message);
                isModified = true;
                optimizationSetupTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
                log.OptimizationConstraints = parsedOptimizationConstraints.Item1;
            }

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
        }

        private void AddOptimizationConstraint_Click(object sender, RoutedEventArgs e)
        {
            Button theBtn = sender as Button;
            StackPanel theSP;
            if (theBtn.Name.Contains("template")) theSP = templateOptParamsSP;
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
                if (theSP.Children.Count > 0)
                {
                    //read list of current objectives
                    (List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>, StringBuilder) parsedOptimizationConstraints = OptimizationSetupUIHelper.ParseOptConstraints(theSP, false);
                    if(!parsedOptimizationConstraints.Item1.Any())
                    {
                        log.LogError(parsedOptimizationConstraints.Item2);
                        return;
                    }
                    foreach(Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>> itr in parsedOptimizationConstraints.Item1)
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
                theSP.Children.Add(OptimizationSetupUIHelper.AddOptVolume(theSP, 
                                                                          selectedSS, 
                                                                          defaultList[i], 
                                                                          clearBtnNamePrefix, 
                                                                          counter, 
                                                                          new RoutedEventHandler(this.ClearOptimizationConstraint_Click), 
                                                                          theSP.Name.Contains("template")));
            }
        }

        private void ClearOptimizationConstraintsList_Click(object sender, RoutedEventArgs e)
        {
            StackPanel theSP;
            if ((sender as Button).Name.Contains("template")) theSP = templateOptParamsSP;
            else theSP = optParametersSP;
            ClearOptimizationConstraintsList(theSP);
        }

        private void ClearOptimizationConstraint_Click(object sender, EventArgs e)
        {
            StackPanel theSP;
            if ((sender as Button).Name.Contains("template")) theSP = templateOptParamsSP;
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
            (ExternalPlanSetup VMATPlan, StringBuilder errorMessage) = PlanPrepUIHelper.RetrieveVMATPlan(pi, logPath, "VMAT CSI");
            if (VMATPlan == null)
            {
                log.LogError(errorMessage);
                return;
            }

            VMATplans.Add(VMATPlan);
            Clipboard.SetText(PlanPrepHelper.GetCSIShiftNote(VMATPlan).ToString());
            MessageBox.Show("Shifts have been copied to the clipboard! \r\nPaste them into the journal note!");

            //let the user know this step has been completed (they can now do the other steps like separate plans and calculate dose)
            shiftTB.Background = System.Windows.Media.Brushes.ForestGreen;
            shiftTB.Text = "YES";
        }

        private void SeparatePlans_Click(object sender, RoutedEventArgs e)
        {
            //The shift note has to be retrieved first! Otherwise, we don't have instances of the plan objects
            if (!VMATplans.Any())
            {
                log.LogError("Please generate the shift note before separating the plans!");
                return;
            }

            ExternalPlanSetup vmatPlan = VMATplans.First();
            if (!vmatPlan.Beams.Any(x => x.IsSetupField))
            {
                ConfirmPrompt CUI = new ConfirmPrompt($"I didn't find any setup fields in the {vmatPlan.Id}." + Environment.NewLine + Environment.NewLine + "Are you sure you want to continue?!");
                CUI.ShowDialog();
                if (!CUI.GetSelection()) return;
            }

            //separate the plans
            pi.BeginModifications();
            PlanPrep_CSI planPrep = new PlanPrep_CSI(vmatPlan, closePWOnFinish);
            bool result = planPrep.Execute();
            log.AppendLogOutput("Plan preparation:", planPrep.GetLogOutput());
            log.OpType = ScriptOperationType.PlanPrep;
            if (result) return;

            //inform the user it's done
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Original plan(s) have been separated!");
            sb.AppendLine("Be sure to set the target volume and primary reference point!");
            if (vmatPlan.Beams.Any(x => x.IsSetupField))
            {
                sb.AppendLine("Also reset the isocenter position of the setup fields!");
            }
            MessageBox.Show(sb.ToString());

            //let the user know this step has been completed
            separateTB.Background = System.Windows.Media.Brushes.ForestGreen;
            separateTB.Text = "YES";

            isModified = true;
            planPreparationTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
        }

        private void CalculateDose_Click(object sender, RoutedEventArgs e)
        {
            ////the shift note must be retireved and the plans must be separated before calculating dose
            //if (shiftTB.Text == "NO" || separateTB.Text == "NO")
            //{
            //    log.LogError("Error! \nYou must generate the shift note AND separate the plan before calculating dose to each plan!");
            //    return;
            //}

            ////ask the user if they are sure they want to do this. Each plan will calculate dose sequentially, which will take time
            //ConfirmPrompt CUI = new ConfirmPrompt("Warning!" + Environment.NewLine + "This will take some time as each plan needs to be calculated sequentionally!" + Environment.NewLine + "Continue?!");
            //CUI.ShowDialog();
            //if (!CUI.GetSelection()) return;

            ////let the user know the script is working
            //calcDoseTB.Background = System.Windows.Media.Brushes.Yellow;
            //calcDoseTB.Text = "WORKING";

            //prep.CalculateDose();

            ////let the user know this step has been completed
            //calcDoseTB.Background = System.Windows.Media.Brushes.ForestGreen;
            //calcDoseTB.Text = "YES";
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
            if (app == null)
            {
                log.LogError("Error! No connection to ESAPI! Exiting");
                return;
            }
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
                AddTargetVolumes(targetList, templateTargetsSP);

                //add create TS structures
                GeneralUIHelper.ClearList(templateTSSP);
                if (theTemplate.GetCreateTSStructures().Any()) AddTuningStructureVolumes(theTemplate.GetCreateTSStructures(), templateTSSP);

                GeneralUIHelper.ClearList(templateCreateRingsSP);
                if (theTemplate.GetCreateRings().Any()) AddRingStructures(theTemplate.GetCreateRings(), templateCreateRingsSP);

                GeneralUIHelper.ClearList(templateCropOverlapOARsSP);
                if (theTemplate.GetCropAndOverlapStructures().Any()) AddCropOverlapOARs(theTemplate.GetCropAndOverlapStructures(), templateCropOverlapOARsSP);

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
                PopulateOptimizationTab(templateOptParamsSP, parsedConstraints.Item1, false);
            }
            else if(templateBuildOptionCB.SelectedItem.ToString().ToLower() == "current parameters")
            {
                //add targets (checked first to ensure the user has actually input some parameters into the UI before trying to make a template based on the current settings)
                (List<Tuple<string, double, string>> targetList, StringBuilder) parsedTargetList = TargetsUIHelper.ParseTargets(targetsSP);
                if (!parsedTargetList.targetList.Any())
                {
                    log.LogError("Error! Enter parameters into the UI before trying to use them to make a new plan template!");
                    return;
                }
                ClearAllTargetItems(templateClearTargetList);
                AddTargetVolumes(parsedTargetList.targetList.OrderBy(x => x.Item2).ToList(), templateTargetsSP);

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

                (List<string>, StringBuilder) parseCropOverlapOARList = CropOverlapOARUIHelper.ParseCropOverlapOARList(cropOverlapOARsSP);
                if (!string.IsNullOrEmpty(parseCropOverlapOARList.Item2.ToString()))
                {
                    log.LogError(parseCropOverlapOARList.Item2);
                    return;
                }
                AddCropOverlapOARs(parseCropOverlapOARList.Item1, templateCropOverlapOARsSP);

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
                ClearOptimizationConstraintsList(templateOptParamsSP);
                PopulateOptimizationTab(templateOptParamsSP, parsedOptimizationConstraints.Item1, false);
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
            prospectiveTemplate.SetTargets(TargetsUIHelper.ParseTargets(templateTargetsSP).Item1.OrderBy(x => x.Item2).ToList());
            prospectiveTemplate.SetCreateTSStructures(StructureTuningUIHelper.ParseCreateTSStructureList(templateTSSP).Item1);
            prospectiveTemplate.SetCreateRings(RingUIHelper.ParseCreateRingList(templateCreateRingsSP).Item1);
            prospectiveTemplate.SetCropAndOverlapStructures(CropOverlapOARUIHelper.ParseCropOverlapOARList(templateCropOverlapOARsSP).Item1);
            prospectiveTemplate.SetTSManipulations(StructureTuningUIHelper.ParseTSManipulationList(templateStructuresSP).Item1);
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> templateOptParametersListList = OptimizationSetupUIHelper.ParseOptConstraints(templateOptParamsSP).Item1;
            prospectiveTemplate.SetInitOptimizationConstraints(templateOptParametersListList.First().Item2);
            prospectiveTemplate.SetBoostOptimizationConstraints(templateOptParametersListList.Last().Item2);

            templatePreviewTB.Text = TemplateBuilder.GenerateTemplatePreviewText(prospectiveTemplate).ToString();
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

            File.WriteAllText(fileName, TemplateBuilder.GenerateSerializedTemplate(prospectiveTemplate).ToString());
            PlanTemplates.Add(prospectiveTemplate);
            DisplayConfigurationParameters();
            templateList.ScrollIntoView(prospectiveTemplate);

            templatePreviewTB.Text += $"New template written to: {fileName}" + Environment.NewLine;
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
            configTB.Text += $"Log file path: {logPath}" + Environment.NewLine + Environment.NewLine;
            configTB.Text += $"Close progress windows on finish: {closePWOnFinish}" + Environment.NewLine + Environment.NewLine;
            configTB.Text += $"Import/export settings:" + Environment.NewLine;
            configTB.Text += $"Image export path: {IEData.WriteLocation}" + Environment.NewLine;
            configTB.Text += $"RT structure set import path: {IEData.ImportLocation}" + Environment.NewLine;
            configTB.Text += $"Image export format: {IEData.ExportFormat}" + Environment.NewLine;

            if (!string.IsNullOrEmpty(IEData.AriaDBDaemon.Item1))
            {
                configTB.Text += "Aria database daemon:" + Environment.NewLine;
                configTB.Text += $"AE Title: {IEData.AriaDBDaemon.Item1}" + Environment.NewLine;
                configTB.Text += $"IP: {IEData.AriaDBDaemon.Item2}" + Environment.NewLine;
                configTB.Text += $"Port: {IEData.AriaDBDaemon.Item3}" + Environment.NewLine;
            }
            if (!string.IsNullOrEmpty(IEData.VMSFileDaemon.Item1))
            {
                configTB.Text += "Aria VMS File daemon:" + Environment.NewLine;
                configTB.Text += $"AE Title: {IEData.VMSFileDaemon.Item1}" + Environment.NewLine;
                configTB.Text += $"IP: {IEData.VMSFileDaemon.Item2}" + Environment.NewLine;
                configTB.Text += $"Port: {IEData.VMSFileDaemon.Item3}" + Environment.NewLine;
            }
            if (!string.IsNullOrEmpty(IEData.LocalDaemon.Item1))
            {
                configTB.Text += "Local daemon:" + Environment.NewLine;
                configTB.Text += $"AE Title: {IEData.LocalDaemon.Item1}" + Environment.NewLine;
                configTB.Text += $"Port: {IEData.LocalDaemon.Item3}" + Environment.NewLine;
            }
            configTB.Text += Environment.NewLine;

            configTB.Text += "Default parameters:" + Environment.NewLine;
            configTB.Text += $"Course Id: {courseId}" + Environment.NewLine;
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
                configTB.Text += "Requested preliminary target structures:" + Environment.NewLine;
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

            if(PlanTemplates.Any()) configTB.Text += ConfigurationUIHelper.PrintCSIPlanTemplateConfigurationParameters(PlanTemplates.ToList()).ToString();

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
                                    if (!string.IsNullOrEmpty(value))
                                    {
                                        string path = ConfigurationHelper.VerifyPathIntegrity(value);
                                        if (!string.IsNullOrEmpty(path)) documentationPath = path;
                                        else log.LogError($"Warning! {value} does NOT exist!");
                                    }
                                }
                                else if (parameter == "close progress windows on finish")
                                {
                                    if (!string.IsNullOrEmpty(value)) closePWOnFinish = bool.Parse(value);
                                }
                                else if (parameter == "img export location")
                                {
                                    string result = ConfigurationHelper.VerifyPathIntegrity(value);
                                    if (!string.IsNullOrEmpty(result)) IEData.WriteLocation = result;
                                    else log.LogError($"Warning! {value} does NOT exist!");
                                }
                                else if (parameter == "RTStruct import location")
                                {
                                    string result = ConfigurationHelper.VerifyPathIntegrity(value);
                                    if (!string.IsNullOrEmpty(result)) IEData.ImportLocation = result;
                                    else log.LogError($"Warning! {value} does NOT exist!");
                                }
                                else if (parameter == "img export format")
                                {
                                    if (string.Equals(value, "dcm") || string.Equals(value, "png")) IEData.ExportFormat = ExportFormatTypeHelper.GetExportFormatType(value);
                                    else log.LogError("Only png and dcm image formats are supported for export!");
                                }
                                else if (parameter.Contains("daemon"))
                                {
                                    //CONTINUE HERE 070523!
                                    Tuple<string, string, int> result = ConfigurationHelper.ParseDaemonSettings(line);
                                    if (result.Item3 != -1)
                                    {
                                        if (parameter.ToLower().Contains("aria")) IEData.AriaDBDaemon = result;
                                        else if (parameter.ToLower().Contains("vms file")) IEData.VMSFileDaemon = result;
                                        else if (parameter.ToLower().Contains("local")) IEData.LocalDaemon = result;
                                        else
                                        {
                                            log.LogError($"Error! Daemon type {parameter} not recognized! Skipping!");
                                        }
                                    }
                                    else log.LogError($"Error! Daemon configuration settings for {line} not parsed successfully! Skipping!");
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
                                    for (int i = 0; i < c.Count(); i++) 
                                    { 
                                        if (i < 5) collRot[i] = c.ElementAt(i); 
                                    }
                                }
                                else if (parameter == "course Id") courseId = value;
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
                log.LogError($"Error could not load configuration file because: {e.Message}\n\nAssuming default parameters");
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
                    PlanTemplates.Add(ConfigurationHelper.ReadCSITemplatePlan(itr, count++));
                }

            }
            catch(Exception e)
            {
                log.LogError($"Error could not load plan template file because: {e.Message}");
                log.LogError(e.StackTrace, true);
                return true;
            }
            return false;
        }
        #endregion
        
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(app != null) AppClosingHelper.CloseApplication(app, pi != null, isModified, autoSave, log);
        }

        private void MainWindow_SizeChanged(object sender, RoutedEventArgs e)
        {
            SizeToContent = SizeToContent.WidthAndHeight;
        }
    }
}