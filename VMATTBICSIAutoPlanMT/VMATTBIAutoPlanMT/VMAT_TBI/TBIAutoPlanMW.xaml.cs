using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Reflection;
using System.Windows.Controls;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.UIHelpers;
using VMATTBICSIAutoPlanningHelpers.Prompts;
using VMATTBICSIAutoPlanningHelpers.Logging;
using VMATTBICSIAutoPlanningHelpers.PlanTemplateClasses;
using PlanType = VMATTBICSIAutoPlanningHelpers.Enums.PlanType;
using System.Collections.ObjectModel;
using System.Text;
using VMATTBICSIAutoPlanningHelpers.Models;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;

namespace VMATTBIAutoPlanMT.VMAT_TBI
{
    public partial class TBIAutoPlanMW : Window
    {
        public bool GetCloseOpenPatientWindowStatus() { return closeOpenPatientWindow; }
        /// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// HARD-CODED MAIN PARAMETERS FOR THIS CLASS AND ALL OTHER CLASSES IN THIS DLL APPLICATION.
        /// ADJUST THESE PARAMETERS TO YOUR TASTE. THESE PARAMETERS WILL BE OVERWRITTEN BY THE CONFIG.INI FILE IF IT IS SUPPLIED.
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        //flash option
        bool useFlashByDefault = true;
        //default flash type is global
        string defaultFlashType = "Global";
        //default flash margin of 0.5 cm
        string defaultFlashMargin = "0.5";
        //default inner PTV margin from body of 0.3 cm
        string defaultTargetMargin = "0.3";
        //option to contour overlap between VMAT fields in adjacent isocenters and default margin for contouring the overlap
        bool contourOverlap = true;
        string contourFieldOverlapMargin = "1.0";
        //point this to the directory holding the documentation files
        string documentationPath;
        //log file path
        string logPath;
        //default course ID
        string courseId = "VMAT TBI";
        //flag to see if user wants to check for potential couch collision (based on stanford experience)
        bool checkTTCollision = false;
        //treatment units and associated photon beam energies
        List<string> linacs = new List<string> { "LA16", "LA17" };
        List<string> beamEnergies = new List<string> { "6X", "10X" };
        //default number of beams per isocenter from head to toe
        int[] beamsPerIso = { 4, 3, 2, 2, 2, 2 };
        //collimator rotations for how to orient the beams (placeBeams class)
        double[] collRot = { 3.0, 357.0, 87.0, 93.0 };
        //jaw positions of the placed VMAT beams
        List<VRect<double>> jawPos = new List<VRect<double>> {
            new VRect<double>(-20.0, -200.0, 200.0, 200.0),
            new VRect<double>(-200.0, -200.0, 20.0, 200.0),
            new VRect<double>(-200.0, -200.0, 0.0, 200.0),
            new VRect<double>(0.0, -200.0, 200.0, 200.0) };
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
        string configFile = "";
        public Patient pi = null;
        StructureSet selectedSS = null;
        public int clearTargetBtnCounter = 0;
        public int clearTargetTemplateBtnCounter = 0;
        public int clearSpareBtnCounter = 0;
        private int clearTemplateSpareBtnCounter = 0;
        public int clearOptBtnCounter = 0;
        public int clearTemplateOptBtnCounter = 0;
        //structure id, Rx dose, plan Id
        //ts target list
        //plan id, list<original target id, ts target id>
        List<PlanTargetsModel> tsTargets = new List<PlanTargetsModel> { };
        //general tuning structures to be added (if selected for sparing) to all case types
        //default general tuning structures to be added (specified in CSI_plugin_config.ini file)
        List<RequestedTSStructureModel> defaultTSStructures = new List<RequestedTSStructureModel> { };
        //default general tuning structure manipulations to be added (specified in CSI_plugin_config.ini file)
        List<RequestedTSManipulationModel> defaultTSStructureManipulations = new List<RequestedTSManipulationModel> { };
        //list to hold the current structure ids in the structure set in addition to the prospective ids after unioning the left and right structures together
        List<string> structureIdsPostUnion = new List<string> { };
        //list of junction structures (i.e., overlap regions between adjacent isocenters)
        List<PlanFieldJunctionModel> jnxs = new List<PlanFieldJunctionModel> { };
        ExternalPlanSetup VMATplan = null;
        int numIsos = 0;
        int numVMATIsos = 0;
        //plan Id, list of isocenter names for this plan
        public List<PlanIsocenterModel> planIsocenters = new List<PlanIsocenterModel> { };
        //plan ID, target Id, numFx, dosePerFx, cumulative dose
        List<PrescriptionModel> prescriptions = new List<PrescriptionModel> { };
        bool useFlash = false;
        FlashType flashType = FlashType.Global;
        Structure flashStructure = null;
        public VMS.TPS.Common.Model.API.Application app = null;
        bool isModified = false;
        bool autoSave = false;
        bool closePWOnFinish = false;
        bool autoDoseRecalc = false;
        //ATTENTION! THE FOLLOWING LINE HAS TO BE FORMATTED THIS WAY, OTHERWISE THE DATA BINDING WILL NOT WORK!
        public ObservableCollection<TBIAutoPlanTemplate> PlanTemplates { get; set; }
        //temporary variable to add new templates to the list
        TBIAutoPlanTemplate prospectiveTemplate = null;
        //ProcessStartInfo optLoopProcess;
        private bool closeOpenPatientWindow = false;
        private PlanPrep_TBI planPrep = null;

        public TBIAutoPlanMW(List<string> args)
        {
            InitializeComponent();
            if (InitializeScript(args)) this.Close();
        }

        #region initialization
        private bool InitializeScript(List<string> args)
        {
            try { app = VMS.TPS.Common.Model.API.Application.CreateApplication(); }
            catch (Exception e) { MessageBox.Show(String.Format("Warning! Could not generate Aria application instance because: {0}", e.Message)); }
            string mrn = "";
            string ss = "";

            if (args.Any(x => string.Equals("-m", x)))
            {
                int index = args.IndexOf("-m");
                mrn = args.ElementAt(index + 1);
            }
            if (args.Any(x => string.Equals("-s", x)))
            {
                int index = args.IndexOf("-s");
                ss = args.ElementAt(index + 1);
            }

            documentationPath = ConfigurationHelper.GetDefaultDocumentationPath();
            logPath = ConfigurationHelper.ReadLogPathFromConfigurationFile(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\log_configuration.ini");
            //if the log file path in the configuration file is empty, use the default path
            if (string.IsNullOrEmpty(logPath)) logPath = ConfigurationHelper.GetDefaultLogPath();
            Logger.GetInstance().LogPath = logPath;
            Logger.GetInstance().PlanType = PlanType.VMAT_TBI;
            Logger.GetInstance().MRN = mrn;
            LoadDefaultConfigurationFiles();
            if (app != null)
            {
                if (OpenPatient(mrn)) return true;
                InitializeStructureSetSelection(ss);

                //check the version information of Eclipse installed on this machine. If it is older than version 15.6, let the user know that this script may not work properly on their system
                if (!double.TryParse(app.ScriptEnvironment.VersionInfo.Substring(0, app.ScriptEnvironment.VersionInfo.LastIndexOf(".")), out double vinfo)) Logger.GetInstance().LogError("Warning! Could not parse Eclise version number! Proceed with caution!");
                else if (vinfo < 15.6) Logger.GetInstance().LogError(String.Format("Warning! Detected Eclipse version: {0:0.0} is older than v15.6! Proceed with caution!", vinfo));
            }

            PlanTemplates = new ObservableCollection<TBIAutoPlanTemplate>() { new TBIAutoPlanTemplate("--select--") };
            DataContext = this;
            templateBuildOptionCB.Items.Add("Existing template");
            templateBuildOptionCB.Items.Add("Current parameters");

            LoadPlanTemplates();

            //pre-populate the flash comboxes (set global flash as default)
            flashOption.Items.Add(FlashType.Global.ToString());
            flashOption.Items.Add(FlashType.Local.ToString());
            flashOption.Text = defaultFlashType;
            flashMarginTB.Text = defaultFlashMargin;

            //set default PTV inner margin from body
            targetMarginTB.Text = defaultTargetMargin;

            DisplayConfigurationParameters();
            targetsTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed;
            return false;
        }

        private void LoadDefaultConfigurationFiles()
        {
            //load script configuration and display the settings
            List<string> configurationFiles = new List<string> { };
            configurationFiles.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\VMAT_TBI_config.ini");
            foreach (string itr in configurationFiles) LoadConfigurationSettings(itr);
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
                        Logger.GetInstance().MRN = mrn;
                    }
                    catch (Exception except)
                    {
                        Logger.GetInstance().LogError(string.Format("Error! Could not open patient because: {0}! Please try again!", except.Message));
                        Logger.GetInstance().LogError(except.StackTrace, true);
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
                else Logger.GetInstance().LogError("Warning! No structure set in context! Please select a structure set at the top of the GUI!");
                patientMRNLabel.Content = pi.Id;
            }
            else Logger.GetInstance().LogError("Could not open patient!");
        }
        #endregion

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(documentationPath + "VMAT-TBI_PrepScript_Guide.pdf")) MessageBox.Show("VMAT-TBI_PrepScript_Guide PDF file does not exist!");
            else Process.Start(documentationPath + "VMAT-TBI_PrepScript_Guide.pdf");
        }

        private void QuickStart_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(documentationPath + "VMAT-TBI_PrepScript_QuickStartGuide.pdf")) MessageBox.Show("VMAT-TBI_PrepScript_QuickStartGuide PDF file does not exist!");
            else Process.Start(documentationPath + "VMAT-TBI_PrepScript_QuickStartGuide.pdf");
        }

        private void TargetMarginInfo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Specify the inner body margin (in cm) that should be used to create the PTV. Typical values range from 0.0 to 0.5 cm. Default value at Stanford University is 0.3 cm.");
        }

        #region selection changed events
        private void StructureSetId_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //clear sparing structure list
            ClearAllCurrentParameters();

            //update selected structure set
            selectedSS = pi.StructureSets.FirstOrDefault(x => string.Equals(x.Id, SSID.SelectedItem.ToString()));
            Logger.GetInstance().StructureSet = selectedSS.Id;

            //update volumes in flash volume combobox with the structures from the current structure set
            flashVolume.Items.Clear();
            foreach (Structure s in selectedSS.Structures) flashVolume.Items.Add(s.Id);
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
            AddDefaultTuningStructures_Click(null, null);
            AddDefaultStructureManipulations();
        }

        private void Templates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TBIAutoPlanTemplate selectedTemplate = templateList.SelectedItem as TBIAutoPlanTemplate;
            if (selectedTemplate == null) return;
            dosePerFxTB.Text = "";
            numFxTB.Text = "";
            if (selectedTemplate.TemplateName != "--select--")
            {
                SetPresciptionInfo(selectedTemplate.InitialRxDosePerFx, selectedTemplate.InitialRxNumberOfFractions);
                ClearAllCurrentParameters();
                LoadTemplateDefaults();
                Logger.GetInstance().Template = selectedTemplate.TemplateName;
            }
            else
            {
                templateList.UnselectAll();
            }
        }

        private void FlashOption_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //update the flash type whenever the user changes the option in the combo box. If the flash type is local, show the flash volume combo box and label. If not, hide them
            flashType = FlashTypeHelper.GetFlashType(flashOption.SelectedItem.ToString());
            if (flashType == FlashType.Global)
            {
                flashVolumeLabel.Visibility = Visibility.Hidden;
                flashVolume.Visibility = Visibility.Hidden;
                flashStructure = null;
            }
            else
            {
                flashVolumeLabel.Visibility = Visibility.Visible;
                flashVolume.Visibility = Visibility.Visible;
            }
        }

        private void FlashVolume_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //update the flash type whenever the user changes the option in the combo box. If the flash type is local, show the flash volume combo box and label. If not, hide them
            if (StructureTuningHelper.DoesStructureExistInSS(flashVolume.SelectedItem.ToString(), selectedSS, true))
            {
                flashStructure = StructureTuningHelper.GetStructureFromId(flashVolume.SelectedItem.ToString(), selectedSS);
            }
            else Logger.GetInstance().LogError($"Error! Could not set flash structure because {flashVolume.SelectedItem.ToString()} does not exist or is empty! Please try again");
        }
        #endregion

        #region flash
        //simple method to either show or hide the relevant flash parameters depending on if the user wants to use flash (i.e., if the 'add flash' checkbox is checked)
        private void Flash_chkbox_Click(object sender, RoutedEventArgs e) { UpdateUseFlash(); }

        private void UpdateUseFlash()
        {
            //logic to hide or show the flash option in GUI
            if (flash_chkbox.IsChecked.Value)
            {
                flashOption.Visibility = Visibility.Visible;
                flashMarginLabel.Visibility = Visibility.Visible;
                flashMarginTB.Visibility = Visibility.Visible;
                if (flashType == FlashType.Local)
                {
                    flashVolumeLabel.Visibility = Visibility.Visible;
                    flashVolume.Visibility = Visibility.Visible;
                }
            }
            else
            {
                flashOption.Visibility = Visibility.Hidden;
                flashMarginLabel.Visibility = Visibility.Hidden;
                flashMarginTB.Visibility = Visibility.Hidden;
                if (flashType == FlashType.Local)
                {
                    flashVolumeLabel.Visibility = Visibility.Hidden;
                    flashVolume.Visibility = Visibility.Hidden;
                }
                flashStructure = null;
            }
            //update whether the user wants to user flash or not
            useFlash = flash_chkbox.IsChecked.Value;
        }
        #endregion

        bool waitToUpdate = false;
        private void SetPresciptionInfo(double dose_perFx, int num_Fx)
        {
            if (dosePerFxTB.Text != dose_perFx.ToString() && numFxTB.Text != num_Fx.ToString()) waitToUpdate = true;
            dosePerFxTB.Text = dose_perFx.ToString();
            numFxTB.Text = num_Fx.ToString();
        }

        private void NumFx_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!int.TryParse(numFxTB.Text, out int newNumFx)) RxTB.Text = "";
            else if (newNumFx < 1)
            {
                Logger.GetInstance().LogError("Error! The number of fractions must be non-negative integer and greater than zero!");
                RxTB.Text = "";
            }
            else ResetInitRxDose();
        }

        private void DosePerFx_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!double.TryParse(dosePerFxTB.Text, out double newDoseFx)) RxTB.Text = "";
            else if (newDoseFx <= 0)
            {
                Logger.GetInstance().LogError("Error! The dose per fraction must be a number and non-negative!");
                RxTB.Text = "";
            }
            else ResetInitRxDose();
        }

        private void ResetInitRxDose()
        {
            if (waitToUpdate) waitToUpdate = false;
            else if (int.TryParse(numFxTB.Text, out int newNumFx) && double.TryParse(dosePerFxTB.Text, out double newDoseFx))
            {
                RxTB.Text = (newNumFx * newDoseFx).ToString();
                if (useFlashByDefault) flash_chkbox.IsChecked = true;
                UpdateUseFlash();
            }
        }

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
            (ScrollViewer, StackPanel) SVSP = GetSVAndSPTargetsTab(sender);
            AddTargetVolumes(new List<PlanTargetsModel> { new PlanTargetsModel("--select--", 0.0, "--select--") }, SVSP.Item2);
            SVSP.Item1.ScrollToBottom();
        }

        private void AddTargetDefaults_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSS == null)
            {
                Logger.GetInstance().LogError("Error! The structure set has not been assigned! Choose a structure set and try again!");
                return;
            }
            List<PlanTargetsModel> targetList = new List<PlanTargetsModel>(TargetsUIHelper.AddTargetDefaults((templateList.SelectedItem as TBIAutoPlanTemplate)));
            ClearAllTargetItems();
            AddTargetVolumes(targetList, targetsSP);
            targetsScroller.ScrollToBottom();
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
            if (btn == null || btn.Name == "clear_target_list" || !btn.Name.Contains("template"))
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

        private void AddTargetVolumes(List<PlanTargetsModel> defaultList, StackPanel theSP)
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
            //assumes each target has a unique planID 
            List<string> planIDs = new List<string>(defaultList.Select(x => x.PlanId))
            {
                "--Add New--"
            };
            foreach (PlanTargetsModel itr in defaultList)
            {
                foreach(TargetModel tgt in itr.Targets)
                {
                    counter++;
                    theSP.Children.Add(TargetsUIHelper.AddTargetVolumes(theSP.Width,
                                                                        itr.PlanId,
                                                                        tgt,
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
            if (selectedSS == null)
            {
                Logger.GetInstance().LogError("Please select a structure set before setting the targets!");
                return;
            }
            if (targetsSP.Children.Count == 0)
            {
                Logger.GetInstance().LogError("No targets present in list! Please add some targets to the list before setting the target structures!");
                return;
            }

            //target id, target Rx, plan id
            List<PlanTargetsModel> parsedTargets = TargetsUIHelper.ParseTargets(targetsSP);
            if (VerifySelectedTargetsIntegrity(parsedTargets)) return;

            (List<PrescriptionModel>, StringBuilder) parsedPrescriptions = TargetsHelper.BuildPrescriptionList(parsedTargets,
                                                                                                          dosePerFxTB.Text,
                                                                                                          numFxTB.Text,
                                                                                                          RxTB.Text);
            if (!parsedPrescriptions.Item1.Any())
            {
                Logger.GetInstance().LogError(parsedPrescriptions.Item2);
                return;
            }
            prescriptions = new List<PrescriptionModel>(parsedPrescriptions.Item1);
            targetsTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
            structureTuningTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed;
            TSManipulationTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed;
            Logger.GetInstance().Prescriptions = prescriptions;
        }

        private bool VerifySelectedTargetsIntegrity(List<PlanTargetsModel> parsedTargets)
        {
            //verify selected targets are APPROVED
            //for tbi, we only want to make there is one plan (not configured for sequential boosts)
            if(!parsedTargets.Any()) return true;
            if(parsedTargets.Select(x => x.PlanId).Distinct().Count() > 1)
            {
                Logger.GetInstance().LogError($"Error! Multiple plan Ids entered! This script is only configured to auto-plan one TBI plan!");
                return true;
            }
            return false;
        }
        #endregion

        #region TS generation and manipulation
        private List<string> CheckLRStructures()
        {
            //check if structures need to be unioned before adding defaults
            List<string> ids = selectedSS.Structures.Select(x => x.Id).ToList();
            List<UnionStructureModel> structuresToUnion = new List<UnionStructureModel>(StructureTuningHelper.CheckStructuresToUnion(selectedSS));
            foreach (UnionStructureModel itr in structuresToUnion) ids.Add(itr.ProposedUnionStructureId);
            return ids;
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
            AddTuningStructureVolumes(new List<RequestedTSStructureModel> { new RequestedTSStructureModel("--select--", "--select--") }, theSP);
            theScroller.ScrollToBottom();
        }

        private void AddDefaultTuningStructures_Click(object sender, RoutedEventArgs e)
        {
            List<RequestedTSStructureModel> tmp = new List<RequestedTSStructureModel>(defaultTSStructures);
            if (templateList.SelectedItem != null)
            {
                foreach (RequestedTSStructureModel itr in ((TBIAutoPlanTemplate)templateList.SelectedItem).CreateTSStructures) tmp.Add(itr);
            }
            GeneralUIHelper.ClearList(TSGenerationSP);
            //populate the comboboxes
            AddTuningStructureVolumes(tmp, TSGenerationSP);
            TSGenerationScroller.ScrollToBottom();
        }

        private void AddTuningStructureVolumes(List<RequestedTSStructureModel> defaultList, StackPanel theSP)
        {
            if (selectedSS == null)
            {
                Logger.GetInstance().LogError("Error! Please select a Structure Set before adding tuning structure manipulations!");
                return;
            }
            if (!defaultList.Any()) return;
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

        //add structure to spare to the list
        private void AddStructureManipulationItem_Click(object sender, RoutedEventArgs e)
        {
            Button theBtn = sender as Button;
            ScrollViewer theScroller;
            StackPanel theSP;
            if (theBtn.Name.Contains("template"))
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
            AddStructureManipulationVolumes(new List<RequestedTSManipulationModel> { new RequestedTSManipulationModel("--select--", TSManipulationType.None, 0.0) }, theSP);
            theScroller.ScrollToBottom();
        }

        private void AddDefaultStructureManipulations_Click(object sender, RoutedEventArgs e)
        {
            AddDefaultStructureManipulations(true);
        }

        private void AddDefaultStructureManipulations(bool fromButtonClickEvent = false)
        {
            if (selectedSS == null)
            {
                Logger.GetInstance().LogError("Error! The structure set has not been assigned! Choose a structure set and try again!");
                return;
            }
            //copy the sparing structures in the defaultSpareStruct list to a temporary vector
            List<RequestedTSManipulationModel> templateManipulationList = new List<RequestedTSManipulationModel>(defaultTSStructureManipulations);
            //add the case-specific sparing structures to the temporary list
            if (templateList.SelectedItem != null)
            {
                templateManipulationList = new List<RequestedTSManipulationModel>(StructureTuningHelper.AddTemplateSpecificStructureManipulations((templateList.SelectedItem as TBIAutoPlanTemplate).TSManipulations, templateManipulationList, pi.Sex));
            }
            if (!templateManipulationList.Any())
            {
                if (fromButtonClickEvent) Logger.GetInstance().LogError("Warning! No default tuning structure manipulations contained in the selected template!");
                return;
            }

            (List<string> missingEmptyStructures, StringBuilder warnings) = StructureTuningUIHelper.VerifyTSManipulationIntputIntegrity(templateManipulationList.Select(x => x.StructureId).Distinct().ToList(), structureIdsPostUnion, selectedSS);
            if (missingEmptyStructures.Any()) Logger.GetInstance().LogError(warnings);
            
            List<RequestedTSManipulationModel> defaultList = new List<RequestedTSManipulationModel> { };
            foreach (RequestedTSManipulationModel itr in templateManipulationList)
            {
                if (!missingEmptyStructures.Any(x => string.Equals(x, itr.StructureId)))
                {
                    defaultList.Add(new RequestedTSManipulationModel(structureIdsPostUnion.First(x => x.ToLower() == itr.StructureId.ToLower()), itr.ManipulationType, itr.MarginInCM));
                }
            }

            ClearStructureManipulationsList(ClearStructureManipulationsBtn);
            AddStructureManipulationVolumes(defaultList, structureManipulationSP);
        }

        //populate the structure sparing list. This method is called whether the add structure or add defaults buttons are hit (because a vector containing the list of structures is passed as an argument to this method)
        private void AddStructureManipulationVolumes(List<RequestedTSManipulationModel> defaultList, StackPanel theSP)
        {
            if (selectedSS == null)
            {
                Logger.GetInstance().LogError("Error! Please select a Structure Set before add tuning structure manipulations!");
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
            if (theSP.Children.Count == 0) theSP.Children.Add(StructureTuningUIHelper.GetTSManipulationHeader(theSP));
            foreach (RequestedTSManipulationModel itr in defaultList)
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

        //method to clear and individual row in the structure sparing list (i.e., remove a single structure)
        private void ClearStructureManipulationItem_Click(object sender, EventArgs e)
        {
            if (GeneralUIHelper.ClearRow(sender, (sender as Button).Name.Contains("template") ? templateStructuresSP : structureManipulationSP))
            {
                ClearStructureManipulationsList((sender as Button).Name.Contains("template") ? templateClearSpareStructuresBtn : ClearStructureManipulationsBtn);
            }
        }

        //wipe the displayed list of sparing structures
        private void ClearStructureManipulations_Click(object sender, RoutedEventArgs e)
        {
            ClearStructureManipulationsList((sender as Button));
        }

        private void ClearStructureManipulationsList(Button theBtn)
        {
            if (theBtn.Name.Contains("template"))
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
                        if ((TSManipulationType)c.SelectedItem == TSManipulationType.None &&
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

        private (bool, double) ParseFlashMargin()
        {
            //get the relevant flash parameters if the user requested to add flash to the target volume(s)
            bool fail = false;
            double flashMargin = 0.0;
            if (useFlash)
            {
                if (!double.TryParse(flashMarginTB.Text, out flashMargin))
                {
                    Logger.GetInstance().LogError("Error! Added flash margin is NaN! \nExiting!");
                    fail = true;
                    return (fail, flashMargin);

                }
                //ESAPI has a limit on the margin for structure of 5.0 cm. The margin should always be positive (i.e., an outer margin)
                if (flashMargin < 0.0 || flashMargin > 5.0)
                {
                    Logger.GetInstance().LogError("Error! Added flash margin is either < 0.0 or > 5.0 cm \nExiting!");
                    fail = true;
                    return (fail, flashMargin);

                }
                if (flashType == FlashType.Local)
                {
                    //if flash type is local, grab an instance of the structure class associated with the selected structure 
                    if (!StructureTuningHelper.DoesStructureExistInSS(flashVolume.SelectedItem.ToString(), selectedSS, true))
                    {
                        Logger.GetInstance().LogError("Error! Selected local flash structure is either null or empty! \nExiting!");
                        fail = true;
                        return (fail, flashMargin);

                    }
                }
            }
            return (fail, flashMargin);
        }

        private (bool, double) ParseTargetMargin()
        {
            //get the relevant flash parameters if the user requested to add flash to the target volume(s)
            bool fail = false;
            double targetMargin;
            if (!double.TryParse(targetMarginTB.Text, out targetMargin))
            {
                Logger.GetInstance().LogError("Error! PTV margin from body is NaN! \nExiting!");
                fail = true;
                return (fail, targetMargin);
            }
            if (targetMargin < 0.0 || targetMargin > 5.0)
            {
                Logger.GetInstance().LogError("Error! PTV margin from body is either < 0.0 or > 5.0 cm \nExiting!");
                fail = true;
                return (fail, targetMargin);

            }
            return (fail, targetMargin);
        }

        private void PerformTSStructureGenerationManipulation_Click(object sender, RoutedEventArgs e)
        {
            //ensure the targets have been specified prior to generating and manipulating the tuning structures
            if (!prescriptions.Any())
            {
                Logger.GetInstance().LogError("Please set the targets first on the 'Set Targets' tab!");
                return;
            }

            //check that there are actually structures to spare in the sparing list
            if (structureManipulationSP.Children.Count == 0 && TSGenerationSP.Children.Count == 0)
            {
                Logger.GetInstance().LogError("No structures present to generate tuning structures!");
                return;
            }

            //margins in cm (conversion to mm handled in generateTS_TBI)
            (bool flashParseFail, double flashMargin) = ParseFlashMargin();
            (bool targetParseFail, double targetMargin) = ParseTargetMargin();
            if (flashParseFail || targetParseFail) return;

            List<RequestedTSStructureModel> createTSStructureList;
            List<RequestedTSManipulationModel> TSManipulationList;
            //get sparing structure and tuning structure lists from the UI
            (List<RequestedTSStructureModel>, StringBuilder) parseCreateTSList = StructureTuningUIHelper.ParseCreateTSStructureList(TSGenerationSP);
            (List<RequestedTSManipulationModel>, StringBuilder) parseTSManipulationList = StructureTuningUIHelper.ParseTSManipulationList(structureManipulationSP);
            if (!string.IsNullOrEmpty(parseCreateTSList.Item2.ToString()))
            {
                Logger.GetInstance().LogError(parseCreateTSList.Item2);
                return;
            }
            if (!string.IsNullOrEmpty(parseTSManipulationList.Item2.ToString()))
            {
                Logger.GetInstance().LogError(parseTSManipulationList.Item2);
                return;
            }
            createTSStructureList = new List<RequestedTSStructureModel>(parseCreateTSList.Item1);
            TSManipulationList = new List<RequestedTSManipulationModel>(parseTSManipulationList.Item1);

            //create an instance of the generateTS class, passing the structure sparing list vector, the selected structure set, and if this is the scleroderma trial treatment regiment
            //The scleroderma trial contouring/margins are specific to the trial, so this trial needs to be handled separately from the generic VMAT treatment type

            //GenerateTS_TBI generate = new GenerateTS_TBI(TS_structures, scleroStructures, structureSpareList, selectedSS, targetMargin, sclero_chkbox.IsChecked.Value, useFlash, flashStructure, flashMargin);
            GenerateTS_TBI generate = new GenerateTS_TBI(createTSStructureList, TSManipulationList, prescriptions, selectedSS, targetMargin, useFlash, flashStructure, flashMargin, closePWOnFinish);
            //overloaded constructor depending on if the user requested to use flash or not. If so, pass the relevant flash parameters to the generateTS class
            pi.BeginModifications();
            bool result = generate.Execute();
            //grab the log output regardless if it passes or fails
            Logger.GetInstance().AppendLogOutput("TS Generation and manipulation output:", generate.GetLogOutput());
            if (result) return;

            //does the structure sparing list need to be updated? This occurs when structures the user elected to spare with option of 'Mean Dose < Rx Dose' are high resolution. Since Eclipse can't perform
            //boolean operations on structures of two different resolutions, code was added to the generateTS class to automatically convert these structures to low resolution with the name of
            // '<original structure Id>_lowRes'. When these structures are converted to low resolution, the updateSparingList flag in the generateTS class is set to true to tell this class that the 
            //structure sparing list needs to be updated with the new low resolution structures.
            if (generate.DoesTSManipulationListRequireUpdating)
            {
                structureIdsPostUnion = new List<string>(selectedSS.Structures.Select(x => x.Id));
                ClearStructureManipulationsList(ClearStructureManipulationsBtn);
                //update the structure sparing list in this class and update the structure sparing list displayed to the user in TS Generation tab
                AddStructureManipulationVolumes(generate.TSManipulationList, structureManipulationSP);
            }
            planIsocenters = generate.PlanIsocentersList;
            numIsos = generate.NumberofIsocenters;
            numVMATIsos = generate.NumberofVMATIsocenters;
           
            PopulateBeamsTab();
            if (generate.PlanTargets.Any()) tsTargets = generate.PlanTargets;

            isModified = true;
            structureTuningTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
            TSManipulationTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
            beamPlacementTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed;
            Logger.GetInstance().AddedStructures = generate.AddedStructureIds;
            Logger.GetInstance().StructureManipulations = TSManipulationList;
            Logger.GetInstance().TSTargets = generate.PlanTargets.SelectMany(x => x.Targets).ToDictionary(x => x.TargetId, x => x.TsTargetId);
            Logger.GetInstance().NormalizationVolumes = generate.NormalizationVolumes;
            Logger.GetInstance().PlanIsocenters = planIsocenters;
        }
        #endregion

        #region Beam placement
        private void ContourOverlapInfo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Selecting this option will contour the overlap between fields in adjacent isocenters in the VMAT plan and assign the resulting structures as targets in the optimization.");
        }

        private void ContourOverlapChecked(object sender, RoutedEventArgs e)
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
            ContourOverlapChecked(null, null);
            contourOverlapTB.Text = contourFieldOverlapMargin;

            beamPlacementSP.Children.Clear();
            numVMATisosTB.Text = numVMATIsos.ToString();

            ////subtract a beam from the first isocenter (head) if the user is NOT interested in sparing the brain
            //if (!optParameters.Where(x => x.Item1.ToLower().Contains("brain")).Any()) beamsPerIso[0]--;
            List<StackPanel> SPList = BeamPlacementUIHelper.PopulateBeamsTabHelper(structureManipulationSP.Width, linacs, beamEnergies, planIsocenters, beamsPerIso);
            if (!SPList.Any()) return;
            foreach (StackPanel s in SPList) beamPlacementSP.Children.Add(s);
            ////subtract a beam from the second isocenter (chest/abdomen area) if the user is NOT interested in sparing the kidneys
            ////if (!optParameters.Where(x => x.Item1.ToLower().Contains("kidneys")).Any()) beamsPerIso[1]--;
        }

        private void UpdateVMATisos_Click(object sender, RoutedEventArgs e)
        {
            if (!planIsocenters.Any()) Logger.GetInstance().LogError("Error! Please generate the tuning structures before updating the requested number of VMAT isocenters!");
            else if (VMATplan != null) Logger.GetInstance().LogError("Error! VMAT plan has already been generated! Cannot place beams again!");
            else if (!int.TryParse(numVMATisosTB.Text, out int tmp)) Logger.GetInstance().LogError("Error! Requested number of VMAT isocenters is NaN! Please try again!");
            else if (tmp == numVMATIsos) Logger.GetInstance().LogError("Warning! Requested number of VMAT isocenters = current number of VMAT isocenters!");
            else if (tmp < 2 || tmp > 4) Logger.GetInstance().LogError("Error! Requested number of VMAT isocenters is less than 2 or greater than 4! Please try again!");
            else
            {
                //if (!optParameters.Where(x => x.Item1.ToLower().Contains("brain")).Any()) beamsPerIso[0]++;
                numIsos += tmp - numVMATIsos;
                numVMATIsos = tmp;
                planIsocenters.Clear();
                planIsocenters = new List<PlanIsocenterModel> { new PlanIsocenterModel(prescriptions.First().PlanId, IsoNameHelper.GetTBIVMATIsoNames(numVMATIsos, numIsos))};
                if (numIsos > numVMATIsos)
                {
                    if (numIsos == numVMATIsos + 2)
                    {
                        planIsocenters.Add(new PlanIsocenterModel("_upper legs", new IsocenterModel("upper legs")));
                        planIsocenters.Add(new PlanIsocenterModel("_lower legs", new IsocenterModel("lower legs")));
                    }
                    else
                    {
                        planIsocenters.Add(new PlanIsocenterModel("_legs", new IsocenterModel("legs")));
                    }
                }
                Logger.GetInstance().PlanIsocenters = planIsocenters;
                PopulateBeamsTab();
            }
        }

        private void CreatePlanAndSetBeams_Click(object sender, RoutedEventArgs e)
        {
            if (beamPlacementSP.Children.Count == 0)
            {
                Logger.GetInstance().LogError("No isocenters present to place beams!");
                return;
            }

            (string, string, List<List<int>>, StringBuilder) parseSelections = BeamPlacementUIHelper.GetBeamSelections(beamPlacementSP, planIsocenters);
            if (string.IsNullOrEmpty(parseSelections.Item1))
            {
                Logger.GetInstance().LogError(parseSelections.Item4);
                return;
            }

            string chosenLinac = parseSelections.Item1;
            string chosenEnergy = parseSelections.Item2;
            List<List<int>> numBeams = parseSelections.Item3;

            //now that we have a list of plans each with a list of isocenter names, we want to make a new list of plans each with a list of tuples of isocenter names and beams per isocenter
            int planCount = 0;
            int isoCount = 0;
            foreach (PlanIsocenterModel itr in planIsocenters)
            {
                foreach(IsocenterModel iso in itr.Isocenters)
                {
                    iso.NumberOfBeams = numBeams.ElementAt(planCount).ElementAt(isoCount++);
                }
                planCount++;
                isoCount = 0;
            }

            //Added code to account for the scenario where the user either requested or did not request to contour the overlap between fields in adjacent isocenters
            bool contourOverlap = contourOverlap_chkbox.IsChecked.Value;
            double contourOverlapMargin = 0.0;
            if (contourOverlap)
            {
                //contour overlap in cm (conversion performed in contourfieldoverlap method in placebeamsbase)
                //ensure the value entered in the added margin text box for contouring field overlap is a valid double
                if (!double.TryParse(contourOverlapTB.Text, out contourOverlapMargin))
                {
                    Logger.GetInstance().LogError("Error! The entered added margin for the contour overlap text box is NaN! Please enter a valid number and try again!");
                    return;
                }
            }

            (bool targetParseFail, double targetMargin) = ParseTargetMargin();
            if (targetParseFail) return;

            PlaceBeams_TBI place = new PlaceBeams_TBI(selectedSS,
                                                      planIsocenters,
                                                      collRot, 
                                                      jawPos, 
                                                      chosenLinac, 
                                                      chosenEnergy, 
                                                      calculationModel, 
                                                      optimizationModel, 
                                                      useGPUdose, 
                                                      useGPUoptimization, 
                                                      MRrestartLevel, 
                                                      targetMargin, 
                                                      contourOverlap,
                                                      contourOverlapMargin,
                                                      checkTTCollision,
                                                      closePWOnFinish);

            place.Initialize(courseId, prescriptions);
            bool result = place.Execute();
            Logger.GetInstance().AppendLogOutput("Plan generation and beam placement output:", place.GetLogOutput());
            if (result) return;
            VMATplan = place.VMATPlans.First();
            if (VMATplan == null) return;
            if(place.GetCheckIsoPlacementStatus())
            {
               Logger.GetInstance().LogError($"WARNING: < {place.GetCheckIsoPlacementLimit() / 10:0.00} cm margin at most superior and inferior locations of body! Verify isocenter placement!");
            }

            //if the user elected to contour the overlap between fields in adjacent isocenters, get this list of structures from the placeBeams class and copy them to the jnxs vector
            //also repopulate the optimization tab (will include the newly added field junction structures)!
            if (contourOverlap_chkbox.IsChecked.Value)
            {
                jnxs = place.FieldJunctions;
                ClearOptimizationConstraintsList(optParametersSP);
                AddDefaultOptimizationConstraints_Click(null, null);
            }

            beamPlacementTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
            optimizationSetupTabItem.Background = System.Windows.Media.Brushes.PaleVioletRed;
            //list the plan UIDs by creation date (initial always gets created first, then boost)
            Logger.GetInstance().PlanUIDs = new List<string> { VMATplan.UID };
            isModified = true;
        }
        #endregion

        #region optimization setup
        private void PopulateOptimizationTab(StackPanel theSP, List<PlanOptimizationSetupModel> tmpList = null, bool checkIfStructurePresentInSS = true, bool updateTsStructureJnxObjectives = false)
        {
            List<PlanOptimizationSetupModel> defaultListList = new List<PlanOptimizationSetupModel> { };
            if (tmpList == null)
            {
                //tmplist is empty indicating that no optimization constraints were present on the UI when this method was called
                updateTsStructureJnxObjectives = true;
                //retrieve constraints from template
                (List<PlanOptimizationSetupModel> constraints, StringBuilder errorMessage) parsedConstraints = OptimizationSetupHelper.RetrieveOptConstraintsFromTemplate(templateList.SelectedItem as TBIAutoPlanTemplate, prescriptions);
                if (!parsedConstraints.constraints.Any())
                {
                    Logger.GetInstance().LogError(parsedConstraints.errorMessage);
                    return;
                }
                tmpList = parsedConstraints.constraints;
            }

            if (checkIfStructurePresentInSS)
            {
                foreach (PlanOptimizationSetupModel itr in tmpList)
                {
                    List<OptimizationConstraintModel> defaultList = new List<OptimizationConstraintModel> { };
                    foreach (OptimizationConstraintModel opt in itr.OptimizationConstraints)
                    {
                        //always add PTV objectives to optimization objectives list
                        if (opt.StructureId.Contains("--select--") || opt.StructureId.Contains("PTV")) defaultList.Add(opt);
                        //only add template optimization objectives for each structure to default list if that structure is present in the selected structure set and contoured
                        //12-22-2020 coded added to account for the situation where the structure selected for sparing had to be converted to a low resolution structure
                        else if (StructureTuningHelper.DoesStructureExistInSS(opt.StructureId + "_lowRes", selectedSS, true))
                        {
                            string lowResStructure = opt.StructureId + "_lowRes";
                            defaultList.Add(new OptimizationConstraintModel(StructureTuningHelper.GetStructureFromId(lowResStructure, selectedSS).Id, opt.ConstraintType, opt.QueryDose, opt.QueryDoseUnits, opt.QueryVolume, opt.Priority));
                        }
                        else if (StructureTuningHelper.DoesStructureExistInSS(opt.StructureId, selectedSS, true)) defaultList.Add(opt);
                    }
                    defaultListList.Add(new PlanOptimizationSetupModel(itr.PlanId, new List<OptimizationConstraintModel>(defaultList)));
                }
            }
            else
            {
                //do NOT check to ensure structures in optimization constraint list are present in structure set before adding them to the UI list
                defaultListList = new List<PlanOptimizationSetupModel>(tmpList);
            }

            if (updateTsStructureJnxObjectives)
            {
                defaultListList = OptimizationSetupHelper.UpdateOptObjectivesWithTsStructuresAndJnxs(defaultListList,
                                                                                                       prescriptions,
                                                                                                       templateList.SelectedItem as AutoPlanTemplateBase,
                                                                                                       tsTargets,
                                                                                                       jnxs);
            }

            foreach (PlanOptimizationSetupModel itr in defaultListList) AddOptimizationConstraintItems(itr.OptimizationConstraints, itr.PlanId, theSP);
        }

        private void AddOptimizationConstraintsHeader(StackPanel theSP)
        {
            theSP.Children.Add(OptimizationSetupUIHelper.GetOptHeader(theSP.Width));
        }

        private void AddOptimizationConstraintItems(List<OptimizationConstraintModel> defaultList, string planId, StackPanel theSP)
        {
            if (selectedSS == null)
            {
                Logger.GetInstance().LogError("Error! The structure set has not been assigned! Choose a structure set and try again!");
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
            foreach(OptimizationConstraintModel itr in defaultList)
            {
                counter++;
                theSP.Children.Add(OptimizationSetupUIHelper.AddOptVolume(theSP, 
                                                                          selectedSS, 
                                                                          itr, 
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

        private void AddDefaultOptimizationConstraints_Click(object sender, RoutedEventArgs e)
        {
            Button theBtn = sender as Button;
            StackPanel theSP;
            string RxText = "";
            bool checkIfStructIsInSS = true;
            if (theBtn != null && theBtn.Name.Contains("template"))
            {
                theSP = templateOptParamsSP;
                RxText = templateInitPlanRxTB.Text;
                checkIfStructIsInSS = false;
            }
            else
            {
                theSP = optParametersSP;
                RxText = RxTB.Text;
            }
            ClearOptimizationConstraintsList(theSP);
            TBIAutoPlanTemplate selectedTemplate = templateList.SelectedItem as TBIAutoPlanTemplate;
            if (selectedTemplate == null)
            {
                //no plan template selected --> copy and scale objectives from an existing template
                string selectedTemplateId = GeneralUIHelper.PromptUserToSelectPlanTemplate(PlanTemplates.Select(x => x.TemplateName).ToList());
                if (string.IsNullOrEmpty(selectedTemplateId))
                {
                    Logger.GetInstance().LogError("Template not found! Exiting!");
                    return;
                }
                selectedTemplate = PlanTemplates.FirstOrDefault(x => string.Equals(x.TemplateName, selectedTemplateId));
            }
            //get prescription
            double Rx = 0.1;
            if (!double.TryParse(RxText, out Rx))
            {
                Logger.GetInstance().LogError("Warning! Entered initial plan prescription is not valid! \nCannot scale optimization objectives to requested Rx! Exiting!");
                return;
            }
            (List<PlanOptimizationSetupModel> constraints, StringBuilder errorMessage) parsedConstraints = OptimizationSetupHelper.RetrieveOptConstraintsFromTemplate(selectedTemplate, prescriptions);
            if (!parsedConstraints.constraints.Any())
            {
                Logger.GetInstance().LogError(parsedConstraints.errorMessage);
                return;
            }
            //assumes you set all targets and upstream items correctly (as you would have had to place beams prior to this point)
            if (CalculationHelper.AreEqual(selectedTemplate.InitialRxDosePerFx * selectedTemplate.InitialRxNumberOfFractions, Rx))
            {
                //currently entered prescription is equal to the prescription dose in the selected template. Simply populate the optimization objective list with the objectives from that template
                PopulateOptimizationTab(theSP, parsedConstraints.constraints, checkIfStructIsInSS, true);
            }
            else
            {
                //entered prescription differs from prescription in template --> need to rescale all objectives by ratio of prescriptions
                string planId = parsedConstraints.constraints.First().PlanId;
                List<PlanOptimizationSetupModel> scaledConstraints = new List<PlanOptimizationSetupModel>
                {
                    new PlanOptimizationSetupModel(planId, OptimizationSetupHelper.RescalePlanObjectivesToNewRx(parsedConstraints.constraints.First().OptimizationConstraints, selectedTemplate.InitialRxDosePerFx * selectedTemplate.InitialRxNumberOfFractions, Rx))
                };
                PopulateOptimizationTab(theSP, scaledConstraints, checkIfStructIsInSS, true);
            }
        }

        private void SetOptConst_Click(object sender, RoutedEventArgs e)
        {
            (List<PlanOptimizationSetupModel>, StringBuilder) parsedOptimizationConstraints = OptimizationSetupUIHelper.ParseOptConstraints(optParametersSP);
            if (!parsedOptimizationConstraints.Item1.Any())
            {
                Logger.GetInstance().LogError(parsedOptimizationConstraints.Item2);
                return;
            }
            bool constraintsAssigned = false;
            Course theCourse = null;
            if (VMATplan == null)
            {
                //if both are found, grab an instance of that plan
                theCourse = pi.Courses.First(x => x.Id == "VMAT TBI");
                pi.BeginModifications();
                //additional check if the plan was not found in the list of VMATplans
                VMATplan = theCourse.ExternalPlanSetups.FirstOrDefault(x => x.Id == parsedOptimizationConstraints.Item1.First().PlanId);
            }

            List<OptimizationConstraintModel> constraints = parsedOptimizationConstraints.Item1.First().OptimizationConstraints;
            if (VMATplan != null)
            {
                if (VMATplan.OptimizationSetup.Objectives.Count() > 0)
                {
                    foreach (OptimizationObjective o in VMATplan.OptimizationSetup.Objectives) VMATplan.OptimizationSetup.RemoveObjective(o);
                }
                OptimizationSetupHelper.AssignOptConstraints(constraints, VMATplan, true, 0.0);
                constraintsAssigned = true;
            }
            else
            {
                Logger.GetInstance().LogError($"VMAT TBI plan not found! Exiting!");
                return;
            }

            if (constraintsAssigned)
            {
                string message = "Optimization objectives have been successfully set!" + Environment.NewLine + Environment.NewLine + "Please review the generated structures, placed isocenters, placed beams, and optimization parameters!";
                if (constraints.Any(x => x.StructureId.ToLower().Contains("_lowres"))) message += "\n\nBE SURE TO VERIFY THE ACCURACY OF THE GENERATED LOW-RESOLUTION CONTOURS!";
                if (numIsos != 0 && numIsos != numVMATIsos)
                {
                    //VMAT only TBI plan was created with the script in this instance info or the user wants to only set the optimization constraints
                    message += "\n\nFor the AP/PA Legs plan, be sure to change the orientation from head-first supine to feet-first supine!";
                }
                MessageBox.Show(message);
                isModified = true;
                optimizationSetupTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
                Logger.GetInstance().OptimizationConstraints = parsedOptimizationConstraints.Item1;
            }

            //confirmUI CUI = new confirmUI();
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

            //}
        }

        private void AddOptimizationConstraint_Click(object sender, RoutedEventArgs e)
        {
            Button theBtn = sender as Button;
            StackPanel theSP;
            if (theBtn.Name.Contains("template")) theSP = templateOptParamsSP;
            else theSP = optParametersSP;
            if (!prescriptions.Any()) return;
            List<PlanOptimizationSetupModel> tmpListList = new List<PlanOptimizationSetupModel> { };
            List<OptimizationConstraintModel> tmp = new List<OptimizationConstraintModel> { };
            if (theSP.Children.Count > 0)
            {
                //stuff in the optimization UI. Parse it, and add a blank optimization constraint at the end
                //read list of current objectives
                (List<PlanOptimizationSetupModel>, StringBuilder) parsedOptimizationConstraints = OptimizationSetupUIHelper.ParseOptConstraints(theSP, false);
                foreach (PlanOptimizationSetupModel itr in parsedOptimizationConstraints.Item1)
                {
                    tmp = new List<OptimizationConstraintModel>(itr.OptimizationConstraints)
                    {
                        new OptimizationConstraintModel("--select--", OptimizationObjectiveType.None, 0.0, Units.cGy, 0.0, 0)
                    };
                    tmpListList.Add(new PlanOptimizationSetupModel(itr.PlanId, tmp));
                }
            }
            else
            {
                //nothing in the optimization setup UI. Populate constraints with constraints from selected template
                TBIAutoPlanTemplate selectedTemplate = templateList.SelectedItem as TBIAutoPlanTemplate;
                if (selectedTemplate != null)
                {
                    if (selectedTemplate.InitialOptimizationConstraints.Any())
                    {
                        tmp = new List<OptimizationConstraintModel>(selectedTemplate.InitialOptimizationConstraints);
                    }
                    else tmp.Add(new OptimizationConstraintModel("--select--", OptimizationObjectiveType.None, 0.0, Units.cGy, 0.0, 0));
                    tmpListList.Add(new PlanOptimizationSetupModel(prescriptions.First().PlanId, tmp));
                }
                else
                {
                    if (VMATplan != null)
                    {
                        tmpListList.Add(new PlanOptimizationSetupModel(VMATplan.Id, new List<OptimizationConstraintModel> { new OptimizationConstraintModel("--select--", OptimizationObjectiveType.None, 0.0, Units.cGy, 0.0, 0) }));
                    }
                    else Logger.GetInstance().LogError("Error! Please generate a VMAT plan and place beams prior to adding optimization constraints!");
                }
            }
            ClearOptimizationConstraintsList(theSP);
            PopulateOptimizationTab(theSP, tmpListList);
        }

        private void ClearOptStructBtn_click(object sender, EventArgs e)
        {
            if (GeneralUIHelper.ClearRow(sender, optParametersSP)) Clear_optimization_parameter_list();
        }

        private void Clear_optimization_parameter_list()
        {
            optParametersSP.Children.Clear();
            clearOptBtnCounter = 0;
        }
        #endregion

        #region plan preparation
        private void GenerateShiftNote_Click(object sender, RoutedEventArgs e)
        {
            (ExternalPlanSetup thePlan, StringBuilder errorMessage) = PlanPrepUIHelper.RetrieveVMATPlan(pi, logPath, !string.IsNullOrEmpty(courseId) ? courseId : "VMAT TBI");
            if (thePlan == null)
            {
                Logger.GetInstance().LogError(errorMessage);
                return;
            }
            VMATplan = thePlan;

            List<ExternalPlanSetup> appaPlans = new List<ExternalPlanSetup> { };
            if (VMATplan.Course.ExternalPlanSetups.Any(x => x.Id.ToLower().Contains("legs")))
            {
                appaPlans = VMATplan.Course.ExternalPlanSetups.Where(x => x.Id.ToLower().Contains("legs")).ToList();
                if (appaPlans.Any(x => x.TreatmentOrientation != PatientOrientation.FeetFirstSupine))
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"The AP/PA plan {appaPlans.First(x => x.TreatmentOrientation != PatientOrientation.FeetFirstSupine).Id} is NOT in the FFS orientation!");
                    sb.AppendLine("THE COUCH SHIFTS FOR THESE PLANS WILL NOT BE ACCURATE! Please fix and try again!");
                    Logger.GetInstance().LogError(sb.ToString());
                    return;
                }
            }

            Clipboard.SetText(PlanPrepHelper.GetTBIShiftNote(VMATplan, appaPlans).ToString());
            MessageBox.Show("Shifts have been copied to the clipboard! \r\nPaste them into the journal note!");

            //let the user know this step has been completed (they can now do the other steps like separate plans and calculate dose)
            shiftTB.Background = System.Windows.Media.Brushes.ForestGreen;
            shiftTB.Text = "YES";
            Logger.GetInstance().OpType = ScriptOperationType.PlanPrep;

            //let the user know this step has been completed (they can now do the other steps like separate plans and calculate dose)
            shiftTB.Background = System.Windows.Media.Brushes.ForestGreen;
            shiftTB.Text = "YES";
        }

        private void SeparatePlans_Click(object sender, RoutedEventArgs e)
        {
            //The shift note has to be retrieved first! Otherwise, we don't have instances of the plan objects
            if (VMATplan == null)
            {
                Logger.GetInstance().LogError("Please generate the shift note before separating the plans!");
                return;
            }

            if (!VMATplan.Beams.Any(x => x.IsSetupField))
            {
                ConfirmPrompt CUI = new ConfirmPrompt($"I didn't find any setup fields in the {VMATplan.Id}." + Environment.NewLine + Environment.NewLine + "Are you sure you want to continue?!");
                CUI.ShowDialog();
                if (!CUI.GetSelection()) return;
            }

            List<ExternalPlanSetup> appaPlan = new List<ExternalPlanSetup> { };
            if (VMATplan.Course.ExternalPlanSetups.Any(x => x.Id.ToLower().Contains("legs")))
            {
                appaPlan = VMATplan.Course.ExternalPlanSetups.Where(x => x.Id.ToLower().Contains("legs")).ToList();
            }

            bool removeFlash = false;
            StringBuilder sb = new StringBuilder();
            //check if flash was used in the plan. If so, ask the user if they want to remove these structures as part of cleanup
            if (PlanPrepUIHelper.CheckForFlash(VMATplan.StructureSet))
            {
                sb.AppendLine("I found some structures in the structure set for generating flash.");
                sb.AppendLine("Should I remove them?");
                sb.AppendLine("(NOTE: this will require dose recalculation for all plans using this structure set!)");
                ConfirmPrompt CP = new ConfirmPrompt(sb.ToString(), "YES", "NO");
                CP.ShowDialog();
                if (CP.GetSelection()) removeFlash = true;
            }

            //separate the plans
            pi.BeginModifications();
            planPrep = new PlanPrep_TBI(VMATplan, appaPlan, autoDoseRecalc, removeFlash, closePWOnFinish);
            bool result = planPrep.Execute();
            Logger.GetInstance().AppendLogOutput("Plan preparation:", planPrep.GetLogOutput());
            Logger.GetInstance().OpType = ScriptOperationType.PlanPrep;
            if (result) return;

            //inform the user it's done
            sb.Clear();
            sb.AppendLine("Original plan(s) have been separated!");
            sb.AppendLine("Be sure to set the target volume and primary reference point!");
            if (VMATplan.Beams.Any(x => x.IsSetupField))
            {
                sb.AppendLine("Also reset the isocenter position of the setup fields!");
            }
            MessageBox.Show(sb.ToString());

            //let the user know this step has been completed
            separateTB.Background = System.Windows.Media.Brushes.ForestGreen;
            separateTB.Text = "YES";

            isModified = true;
            
            if (planPrep.recalcNeeded && !autoDoseRecalc)
            {
                calcDose.Visibility = Visibility.Visible;
                calcDoseTB.Visibility = Visibility.Visible;
            }
            else planPreparationTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
        }

        private void CalcDose_Click(object sender, RoutedEventArgs e)
        {
            //the shift note must be retireved and the plans must be separated before calculating dose
            if (shiftTB.Text == "NO" || separateTB.Text == "NO")
            {
                Logger.GetInstance().LogError("Error! \nYou must generate the shift note AND separate the plan before calculating dose to each plan!");
                return;
            }

            //ask the user if they are sure they want to do this. Each plan will calculate dose sequentially, which will take time
            ConfirmPrompt CUI = new ConfirmPrompt("Warning!" + Environment.NewLine + "This will take some time as each plan needs to be calculated sequentionally!" + Environment.NewLine + "Continue?!");
            CUI.ShowDialog();
            if (!CUI.GetSelection()) return;

            planPrep.RecalculateDoseOnly = true;
            bool result = planPrep.Execute();
            Logger.GetInstance().AppendLogOutput("Plan prep dose recalculation:", planPrep.GetLogOutput());
            Logger.GetInstance().OpType = ScriptOperationType.PlanPrep;
            if (result) return;

            //let the user know this step has been completed
            calcDoseTB.Background = System.Windows.Media.Brushes.ForestGreen;
            calcDoseTB.Text = "YES";
            planPreparationTabItem.Background = System.Windows.Media.Brushes.ForestGreen;
        }

        private void PlanSum_Click(object sender, RoutedEventArgs e)
        {
            //do nothing. Eclipse v15.6 doesn't have this capability, but v16 and later does. This method is a placeholder (the planSum button exists in the UI.xaml file, but its visibility is set to 'hidden')
        }
        #endregion

        #region TemplateBuilder
        private void TemplateDosePerFx_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox dosePerFxTB = sender as TextBox;
            TextBox numFxTB, planRxTB;
            numFxTB = templateInitPlanNumFxTB;
            planRxTB = templateInitPlanRxTB;
            if (!double.TryParse(dosePerFxTB.Text, out double newDoseFx)) planRxTB.Text = "";
            else if (newDoseFx <= 0)
            {
                Logger.GetInstance().LogError("Error! The dose per fraction must be a number and non-negative!");
                planRxTB.Text = "";
            }
            else ResetTemplateRxDose(dosePerFxTB, numFxTB, planRxTB);
        }

        private void TemplateNumFx_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox numFxTB = sender as TextBox;
            TextBox dosePerFxTB, planRxTB;
            dosePerFxTB = templateInitPlanDosePerFxTB;
            planRxTB = templateInitPlanRxTB;
            if (!int.TryParse(numFxTB.Text, out int newNumFx)) planRxTB.Text = "";
            else if (newNumFx < 1)
            {
                Logger.GetInstance().LogError("Error! The number of fractions must be an integer and greater than 0!");
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
                Logger.GetInstance().LogError("Error! The structure set has not been assigned! Choose a structure set and try again!");
                return;
            }
            if (templateBuildOptionCB.SelectedItem.ToString().ToLower() == "existing template")
            {
                TBIAutoPlanTemplate theTemplate = null;
                string selectedTemplateId = GeneralUIHelper.PromptUserToSelectPlanTemplate(PlanTemplates.Select(x => x.TemplateName).ToList());
                if (string.IsNullOrEmpty(selectedTemplateId))
                {
                    Logger.GetInstance().LogError("Template not found! Exiting!");
                    return;
                }
                theTemplate = PlanTemplates.FirstOrDefault(x => string.Equals(x.TemplateName, selectedTemplateId));
                //set name
                templateNameTB.Text = theTemplate.TemplateName + "_1";

                //setRx
                templateInitPlanDosePerFxTB.Text = theTemplate.InitialRxDosePerFx.ToString();
                templateInitPlanNumFxTB.Text = theTemplate.InitialRxNumberOfFractions.ToString();

                //add targets
                List<PlanTargetsModel> targetList = new List<PlanTargetsModel>(theTemplate.PlanTargets);
                ClearAllTargetItems(templateClearTargetList);
                AddTargetVolumes(targetList, templateTargetsSP);

                //add create TS structures
                GeneralUIHelper.ClearList(templateTSSP);
                if (theTemplate.CreateTSStructures.Any()) AddTuningStructureVolumes(theTemplate.CreateTSStructures, templateTSSP);

                //add tuning structure manipulations sparing structures
                ClearStructureManipulationsList(templateClearSpareStructuresBtn);
                if (theTemplate.TSManipulations.Any()) AddStructureManipulationVolumes(theTemplate.TSManipulations, templateStructuresSP);

                //add optimization constraints
                (List<PlanOptimizationSetupModel>, StringBuilder) parsedConstraints = OptimizationSetupHelper.RetrieveOptConstraintsFromTemplate(theTemplate, targetList);
                if (!parsedConstraints.Item1.Any())
                {
                    Logger.GetInstance().LogError(parsedConstraints.Item2);
                    return;
                }
                PopulateOptimizationTab(templateOptParamsSP, parsedConstraints.Item1, false);
            }
            else if (templateBuildOptionCB.SelectedItem.ToString().ToLower() == "current parameters")
            {
                //add targets (checked first to ensure the user has actually input some parameters into the UI before trying to make a template based on the current settings)
                List<PlanTargetsModel> parsedTargetList = TargetsUIHelper.ParseTargets(targetsSP);
                if (!parsedTargetList.Any())
                {
                    return;
                }
                ClearAllTargetItems(templateClearTargetList);
                AddTargetVolumes(parsedTargetList, templateTargetsSP);

                //set name
                templateNameTB.Text = "--new template--";

                //setRx
                templateInitPlanDosePerFxTB.Text = dosePerFxTB.Text;
                templateInitPlanNumFxTB.Text = numFxTB.Text;

                //add create tuning structures structures
                (List<RequestedTSStructureModel>, StringBuilder) parsedCreateTSList = StructureTuningUIHelper.ParseCreateTSStructureList(TSGenerationSP);
                if (!string.IsNullOrEmpty(parsedCreateTSList.Item2.ToString()))
                {
                    Logger.GetInstance().LogError(parsedCreateTSList.Item2);
                    return;
                }
                GeneralUIHelper.ClearList(templateTSSP);
                AddTuningStructureVolumes(parsedCreateTSList.Item1, templateTSSP);

                //add tuning structure manipulations
                (List<RequestedTSManipulationModel>, StringBuilder) parsedTSManipulationList = StructureTuningUIHelper.ParseTSManipulationList(structureManipulationSP);
                if (!string.IsNullOrEmpty(parsedTSManipulationList.Item2.ToString()))
                {
                    Logger.GetInstance().LogError(parsedTSManipulationList.Item2);
                    return;
                }
                ClearStructureManipulationsList(templateClearSpareStructuresBtn);
                AddStructureManipulationVolumes(parsedTSManipulationList.Item1, templateStructuresSP);

                //add optimization constraints
                (List<PlanOptimizationSetupModel>, StringBuilder) parsedOptimizationConstraints = OptimizationSetupUIHelper.ParseOptConstraints(optParametersSP);
                if (parsedOptimizationConstraints.Item1.Any())
                {
                    Logger.GetInstance().LogError(parsedOptimizationConstraints.Item2);
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
                Logger.GetInstance().LogError("Error! Please select a Structure Set before add sparing volumes!");
                return;
            }
            prospectiveTemplate = new TBIAutoPlanTemplate();
            prospectiveTemplate.TemplateName = templateNameTB.Text;

            if (double.TryParse(templateInitPlanDosePerFxTB.Text, out double initDosePerFx))
            {
                prospectiveTemplate.InitialRxDosePerFx = initDosePerFx;
            }
            else
            {
                Logger.GetInstance().LogError("Error! Initial plan dose per fx not parsed successfully! Fix and try again!");
                return;
            }
            if (int.TryParse(templateInitPlanNumFxTB.Text, out int initNumFx))
            {
                prospectiveTemplate.InitialRxNumberOfFractions = initNumFx;
            }
            else
            {
                Logger.GetInstance().LogError("Error! Initial plan dose per fx not parsed successfully! Fix and try again!");
                return;
            }

            //sort targets by prescription dose (ascending order)
            prospectiveTemplate.PlanTargets = TargetsUIHelper.ParseTargets(templateTargetsSP);
            prospectiveTemplate.CreateTSStructures = StructureTuningUIHelper.ParseCreateTSStructureList(templateTSSP).Item1;
            prospectiveTemplate.TSManipulations = StructureTuningUIHelper.ParseTSManipulationList(templateStructuresSP).Item1;
            List<PlanOptimizationSetupModel> templateOptParametersListList = OptimizationSetupUIHelper.ParseOptConstraints(templateOptParamsSP).Item1;
            prospectiveTemplate.InitialOptimizationConstraints = new List<OptimizationConstraintModel>(templateOptParametersListList.First().OptimizationConstraints);

            templatePreviewTB.Text = TemplateBuilder.GenerateTemplatePreviewText(prospectiveTemplate).ToString();
            templatePreviewScroller.ScrollToTop();
        }

        private void SerializeNewTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSS == null)
            {
                Logger.GetInstance().LogError("Error! Please select a Structure Set before add sparing volumes!");
                return;
            }
            if (prospectiveTemplate == null)
            {
                Logger.GetInstance().LogError("Error! Please preview the requested template before building!");
                return;
            }
            string fileName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\templates\\TBI\\TBI_" + prospectiveTemplate.TemplateName + ".ini";
            if (File.Exists(fileName))
            {
                ConfirmPrompt CUI = new ConfirmPrompt("Warning! The requested template file already exists! Overwrite?");
                CUI.ShowDialog();
                if (!CUI.GetSelection()) return;
                if (PlanTemplates.Any(x => string.Equals(x.TemplateName, prospectiveTemplate.TemplateName)))
                {
                    int index = PlanTemplates.IndexOf(PlanTemplates.FirstOrDefault(x => string.Equals(x.TemplateName, prospectiveTemplate.TemplateName)));
                    PlanTemplates.RemoveAt(index);
                }
            }

            File.WriteAllText(fileName, TemplateBuilder.GenerateSerializedTemplate(prospectiveTemplate).ToString());
            PlanTemplates.Add(prospectiveTemplate);
            DisplayConfigurationParameters();
            templateList.ScrollIntoView(prospectiveTemplate);

            templatePreviewTB.Text += String.Format("New template written to: {0}", fileName) + Environment.NewLine;
            templatePreviewScroller.ScrollToBottom();
        }
        #endregion

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Script Configuration
        //stuff related to script configuration
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
                else Logger.GetInstance().LogError("Error! Selected file is NOT valid!"); 
            }
        }

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
            configTB.Text += "Default parameters:" + Environment.NewLine;
            configTB.Text += $"Course Id: {courseId}" + Environment.NewLine;
            configTB.Text += $"Check for potential couch collision: {checkTTCollision}" + Environment.NewLine;
            configTB.Text += $"Contour field ovelap: {contourOverlap}" + Environment.NewLine;
            configTB.Text += $"Contour field overlap margin: {contourFieldOverlapMargin} cm" + Environment.NewLine;
            configTB.Text += $"Automatic dose recalculation during plan preparation: {autoDoseRecalc}" + Environment.NewLine;
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
            configTB.Text += $"Include flash by default: {useFlashByDefault}" + Environment.NewLine;
            configTB.Text += $"Flash type: {defaultFlashType}"  + Environment.NewLine;
            configTB.Text += $"Flash margin: {defaultFlashMargin} cm"  + Environment.NewLine;
            configTB.Text += $"Target inner margin: {defaultTargetMargin} cm"  + Environment.NewLine;
            
            configTB.Text += Environment.NewLine;
            configTB.Text += "Field jaw position (cm) order: " + Environment.NewLine;
            configTB.Text += " (x1,y1,x2,y2)" + Environment.NewLine;
            foreach (VRect<double> j in jawPos) configTB.Text += $"({j.X1 / 10:0.0},{j.Y1 / 10:0.0},{j.X2 / 10:0.0},{j.Y2 / 10:0.0})" + Environment.NewLine;
            configTB.Text += $"Photon dose calculation model: {calculationModel}" + Environment.NewLine;
            configTB.Text += $"Use GPU for dose calculation: {useGPUdose}" + Environment.NewLine;
            configTB.Text += $"Photon optimization model: {optimizationModel}" + Environment.NewLine;
            configTB.Text += $"Use GPU for optimization: {useGPUoptimization}" + Environment.NewLine;
            configTB.Text += $"MR level restart at: {MRrestartLevel}" + Environment.NewLine + Environment.NewLine;

            if (defaultTSStructures.Any())
            {
                configTB.Text += "Requested general tuning structures:" + Environment.NewLine;
                configTB.Text += String.Format(" {0, -10} | {1, -15} |", "DICOM type", "Structure Id") + Environment.NewLine;
                foreach (RequestedTSStructureModel ts in defaultTSStructures) configTB.Text += String.Format(" {0, -10} | {1, -15} |" + Environment.NewLine, ts.DICOMType, ts.StructureId);
                configTB.Text += Environment.NewLine;
            }
            else configTB.Text += "No general TS manipulations requested!" + Environment.NewLine + Environment.NewLine;

            if (defaultTSStructureManipulations.Any())
            {
                configTB.Text += "Default TS manipulations:" + Environment.NewLine;
                configTB.Text += String.Format(" {0, -15} | {1, -26} | {2, -11} |", "structure Id", "sparing type", "margin (cm)") + Environment.NewLine;
                foreach (RequestedTSManipulationModel itr in defaultTSStructureManipulations) configTB.Text += String.Format(" {0, -15} | {1, -26} | {2,-11:N1} |" + Environment.NewLine, itr.StructureId, itr.ManipulationType, itr.MarginInCM);
                configTB.Text += Environment.NewLine;
            }
            else configTB.Text += "No default TS manipulations to list" + Environment.NewLine + Environment.NewLine;

            if (PlanTemplates.Any()) configTB.Text += ConfigurationUIHelper.PrintTBIPlanTemplateConfigurationParameters(PlanTemplates.ToList()).ToString();
            configScroller.ScrollToTop();
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
                    List<VRect<double>> jawPos_temp = new List<VRect<double>> { };
                    List<RequestedTSManipulationModel> defaultTSManipulations_temp = new List<RequestedTSManipulationModel> { };
                    List<RequestedTSStructureModel> defaultTSstructures_temp = new List<RequestedTSStructureModel> { };

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
                                //check if it's a double value
                                if (double.TryParse(value, out double result))
                                {
                                    if (parameter == "default flash margin") defaultFlashMargin = result.ToString();
                                    else if (parameter == "default target margin") defaultTargetMargin = result.ToString();
                                }
                                else if (parameter == "documentation path")
                                {
                                    if(!string.IsNullOrEmpty(value))
                                    {
                                        string path = ConfigurationHelper.VerifyPathIntegrity(value);
                                        if (!string.IsNullOrEmpty(path)) documentationPath = path;
                                        else Logger.GetInstance().LogError($"Warning! {value} does NOT exist!");
                                    }
                                }
                                else if (parameter == "close progress windows on finish")
                                {
                                    if (!string.IsNullOrEmpty(value)) closePWOnFinish = bool.Parse(value);
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
                                else if (parameter == "check couch collision") 
                                { 
                                    if (!string.IsNullOrEmpty(value)) checkTTCollision = bool.Parse(value); 
                                }
                                else if (parameter == "course Id") courseId = value;
                                else if (parameter == "use GPU for dose calculation") useGPUdose = value;
                                else if (parameter == "use GPU for optimization") useGPUoptimization = value;
                                else if (parameter == "MR level restart") MRrestartLevel = value;
                                //other parameters that should be updated
                                else if (parameter == "use flash by default") useFlashByDefault = bool.Parse(value);
                                else if (parameter == "auto dose recalculation") { if (value != "") autoDoseRecalc = bool.Parse(value); }
                                else if (parameter == "default flash type") { if (value != "") defaultFlashType = value; }
                                else if (parameter == "calculation model") { if (value != "") calculationModel = value; }
                                else if (parameter == "optimization model") { if (value != "") optimizationModel = value; }
                                else if (parameter == "contour field overlap") { if (value != "") contourOverlap = bool.Parse(value); }
                                else if (parameter == "contour field overlap margin") { if (value != "") contourFieldOverlapMargin = value; }
                            }
                            else if (line.Contains("add default TS manipulation")) defaultTSManipulations_temp.Add(ConfigurationHelper.ParseTSManipulation(line));
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
                            else if (line.Contains("add jaw position"))
                            {
                                //parse the default requested number of beams per isocenter
                                (bool fail, VRect<double> parsedPositions) = ConfigurationHelper.ParseJawPositions(line);
                                if (fail)
                                {
                                    Logger.GetInstance().LogError("Error! Jaw positions not defined correctly!");
                                    Logger.GetInstance().LogError(line);
                                }
                                else jawPos_temp.Add(parsedPositions);
                            }
                        }
                    }
                    //anything that is an array needs to be updated AFTER the while loop.
                    if (linac_temp.Any()) linacs = new List<string>(linac_temp);
                    if (energy_temp.Any()) beamEnergies = new List<string>(energy_temp);
                    if (jawPos_temp.Any() && jawPos_temp.Count == 4) jawPos = new List<VRect<double>>(jawPos_temp);
                    if (defaultTSManipulations_temp.Any()) defaultTSStructureManipulations = new List<RequestedTSManipulationModel>(defaultTSManipulations_temp);
                    if (defaultTSstructures_temp.Any()) defaultTSStructures = new List<RequestedTSStructureModel>(defaultTSstructures_temp);
                }
                return false;
            }
            //let the user know if the data parsing failed
            catch (Exception e)
            {
                Logger.GetInstance().LogError($"Error could not load configuration file because: {e.Message}\n\nAssuming default parameters");
                Logger.GetInstance().LogError(e.StackTrace, true);
                return true;
            }
        }

        private bool LoadPlanTemplates()
        {
            int count = 1;
            try
            {
                foreach (string itr in Directory.GetFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\templates\\TBI\\", "*.ini").OrderBy(x => x))
                {
                    PlanTemplates.Add(ConfigurationHelper.ReadTBITemplatePlan(itr, count++));
                }

            }
            catch (Exception e)
            {
                Logger.GetInstance().LogError($"Error could not load plan template file because: {e.Message}");
                Logger.GetInstance().LogError(e.StackTrace, true);
                return true;
            }
            return false;
        }
        #endregion

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(app != null) AppClosingHelper.CloseApplication(app, pi != null, isModified, autoSave);
        }

        private void MainWindow_SizeChanged(object sender, RoutedEventArgs e)
        {
            SizeToContent = SizeToContent.WidthAndHeight;
        }
    }
}
