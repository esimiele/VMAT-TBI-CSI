using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using System.Windows.Threading;
using System.Threading;
using System.Collections.ObjectModel;
using System.Reflection;
using VMATAutoPlanMT.VMAT_CSI;

namespace VMATAutoPlanMT
{
    //8/1/2022
    //idea to migrate checkboxes and hard-coded constraints to a template class that will be populated using the configuration file (saves me the trouble of hard-coding everything)
    //replace labels and checkboxes with listbox that will be populated with template prescriptions
    public partial class CSIAutoPlanMW : Window
    {
        string configFile = "";
        /// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// HARD-CODED MAIN PARAMETERS FOR THIS CLASS AND ALL OTHER CLASSES IN THIS DLL APPLICATION.
        /// ADJUST THESE PARAMETERS TO YOUR TASTE. THESE PARAMETERS WILL BE OVERWRITTEN BY THE CONFIG.INI FILE IF IT IS SUPPLIED.
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //structure id, Rx dose, plan Id
        List<Tuple<string, double, string>> targets = new List<Tuple<string, double, string>> { };

        //general tuning structures to be added (if selected for sparing) to all case types
        List<Tuple<string, string>> defaultTS_structures = new List<Tuple<string, string>>
        {
          Tuple.Create("CONTROL","Lungs-1cm"),
          Tuple.Create("CONTROL","Lungs-2cm"),
          Tuple.Create("CONTROL","Liver-1cm"),
          Tuple.Create("CONTROL","Liver-2cm"),
          Tuple.Create("CONTROL","Kidneys-1cm"),
          Tuple.Create("CONTROL","Brain-0.5cm"),
          Tuple.Create("CONTROL","Brain-1cm"),
          Tuple.Create("CONTROL","Brain-2cm"),
          Tuple.Create("CONTROL","Brain-3cm"),
          Tuple.Create("PTV","PTV_Body"),
          Tuple.Create("CONTROL","TS_PTV_VMAT")
        };

        List<Tuple<string, string>> TS_structures = new List<Tuple<string, string>> { };

        List<Tuple<string, string, double>> defaultSpareStruct = new List<Tuple<string, string, double>>
        {
            new Tuple<string, string, double>("Lungs", "Mean Dose < Rx Dose", 0.3),
            new Tuple<string, string, double>("Kidneys", "Mean Dose < Rx Dose", 0.0),
            new Tuple<string, string, double>("Bowel", "Dmax ~ Rx Dose", 0.0)
        };

        //option to contour overlap between VMAT fields in adjacent isocenters and default margin for contouring the overlap
        bool contourOverlap = true;
        string contourFieldOverlapMargin = "1.0";
        //point this to the directory holding the documentation files
        string documentationPath = @"\\vfs0006\RadData\oncology\ESimiele\Research\VMAT_TBI_CSI\documentation\";
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
        //jaw positions of the placed VMAT beams
        List<VRect<double>> jawPos = new List<VRect<double>> {
            new VRect<double>(-100.0, -100.0, 100.0, 100.0),
            new VRect<double>(-100.0, -100.0, 100.0, 100.0),
            new VRect<double>(-25.0, -200.0, 25.0, 200.0),
            new VRect<double>(-25.0, -200.0, 25.0, 200.0) };
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
        public Patient pi = null;
        StructureSet selectedSS = null;
        private bool firstTargetStruct = true;
        private bool firstTargetTemplateStruct = true;
        private bool firstSpareStruct = true;
        private bool firstTemplateSpareStruct = true;
        public int clearTargetBtnCounter = 0;
        public int clearTargetTemplateBtnCounter = 0;
        public int clearSpareBtnCounter = 0;
        public int clearTemplateSpareBtnCounter = 0;
        public int clearOptBtnCounter = 0;
        public int clearTemplateOptBtnCounter = 0;
        List<ExternalPlanSetup> VMATplans = new List<ExternalPlanSetup> { };
        //plan Id, list of isocenter names for this plan
        public List<Tuple<string,List<string>>> isoNames = new List<Tuple<string, List<string>>> { };
        //plan ID, target Id, numFx, dosePerFx, cumulative dose
        List<Tuple<string, string,int, DoseValue, double>> prescriptions = new List<Tuple<string, string, int, DoseValue, double>> { };
        //Tuple<int, DoseValue> prescriptions;
        List<Structure> jnxs = new List<Structure> { };
        planPrep_CSI prep = null;
        public VMS.TPS.Common.Model.API.Application app = null;
        bool isModified = false;
        bool autoSave = false;
        bool checkStructuresToUnion = true;
        //ATTENTION! THE FOLLOWING LINE HAS TO BE FORMATTED THIS WAY, OTHERWISE THE DATA BINDING WILL NOT WORK!
        public ObservableCollection<autoPlanTemplate> PlanTemplates { get; set; }
        //temporary variable to add new templates to the list
        autoPlanTemplate prospectiveTemplate = null;
        //ProcessStartInfo optLoopProcess;

        public CSIAutoPlanMW(List<string> args)
        {
            InitializeComponent();
            try { app = VMS.TPS.Common.Model.API.Application.CreateApplication(); }
            catch (Exception e) { MessageBox.Show(String.Format("Warning! Could not generate Aria application instance because: {0}", e.Message)); }
            string mrn = "";
            string ss = "";
            string configurationFile = "";
            if (args.Count == 1) configurationFile = args.ElementAt(0);
            else
            {
                mrn = args.ElementAt(0);
                ss = args.ElementAt(1);
                configurationFile = args.ElementAt(2);
            }
            if (app != null)
            {
                if (string.IsNullOrEmpty(mrn) || string.IsNullOrWhiteSpace(mrn))
                {
                    //missing patient MRN. Need to ask user for it
                    enterMissingInfo e = new enterMissingInfo("Missing patient Id!\nPlease enter it below and hit Confirm!", "MRN:");
                    e.ShowDialog();
                    if (!e.confirm) { this.Close(); return; }
                    try { if (app != null) pi = app.OpenPatientById(e.value.Text); }
                    catch (Exception except) { MessageBox.Show(string.Format("Error! Could not open patient because: {0}! Please try again!", except.Message)); pi = null; }
                }
                else pi = app.OpenPatientById(mrn);

                //check the version information of Eclipse installed on this machine. If it is older than version 15.6, let the user know that this script may not work properly on their system
                if (!double.TryParse(app.ScriptEnvironment.VersionInfo.Substring(0, app.ScriptEnvironment.VersionInfo.LastIndexOf(".")), out double vinfo)) MessageBox.Show("Warning! Could not parse Eclise version number! Proceed with caution!");
                else if (vinfo < 15.6) MessageBox.Show(String.Format("Warning! Detected Eclipse version: {0:0.0} is older than v15.6! Proceed with caution!", vinfo));

                if (pi != null)
                {
                    //SSID is combobox defined in UI.xaml
                    foreach (StructureSet s in pi.StructureSets.OrderByDescending(x => x.HistoryDateTime)) SSID.Items.Add(s.Id);
                    //SSID default is the current structure set in the context
                    if (!string.IsNullOrEmpty(ss)) { selectedSS = pi.StructureSets.FirstOrDefault(x => x.Id == ss); SSID.Text = selectedSS.Id; }
                    else MessageBox.Show("Warning! No structure set in context! Please select a structure set at the top of the GUI!");
                    populateCTImageSets();
                }
                else MessageBox.Show("Could not open patient!");
            }

            PlanTemplates = new ObservableCollection<autoPlanTemplate>() { new autoPlanTemplate("--select--") };
            DataContext = this;
            //templateBuildOptionCB.Items.Add("");
            templateBuildOptionCB.Items.Add("Existing template");
            templateBuildOptionCB.Items.Add("Current parameters");
            //load script configuration and display the settings
            if (configurationFile != "") loadConfigurationSettings(configurationFile);
            displayConfigurationParameters();
        }

        private void Help_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(documentationPath + "VMAT_CSI_guide.pdf")) MessageBox.Show("VMAT_CSI_guide PDF file does not exist!");
            else Process.Start(documentationPath + "VMAT_CSI_guide.pdf");
        }

        private void QuickStart_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(documentationPath + "CSI_plugIn_quickStart_guide.pdf")) MessageBox.Show("CSI_plugIn_quickStart_guide PDF file does not exist!");
            else Process.Start(documentationPath + "CSI_plugIn_quickStart_guide.pdf");
        }

        //method to display the loaded configuration settings
        private void displayConfigurationParameters()
        {
            configTB.Text = "";
            configTB.Text = String.Format(" {0}", DateTime.Now.ToString()) + System.Environment.NewLine;
            if (configFile != "") configTB.Text += String.Format(" Configuration file: {0}", configFile) + System.Environment.NewLine + System.Environment.NewLine;
            else configTB.Text += String.Format(" Configuration file: none") + System.Environment.NewLine + System.Environment.NewLine;
            configTB.Text += String.Format(" Documentation path: {0}", documentationPath) + System.Environment.NewLine + System.Environment.NewLine;
            configTB.Text += String.Format(" Image export path: {0}", imgExportPath) + System.Environment.NewLine + System.Environment.NewLine;
            configTB.Text += String.Format(" Default parameters:") + System.Environment.NewLine;
            configTB.Text += String.Format(" Image export format: {0}", imgExportFormat) + System.Environment.NewLine;
            configTB.Text += String.Format(" Contour field ovelap: {0}", contourOverlap) + System.Environment.NewLine;
            configTB.Text += String.Format(" Contour field overlap margin: {0} cm", contourFieldOverlapMargin) + System.Environment.NewLine;
            configTB.Text += String.Format(" Available linacs:") + System.Environment.NewLine;
            foreach (string l in linacs) configTB.Text += string.Format(" {0}",l) + System.Environment.NewLine;
            configTB.Text += String.Format(" Available photon energies:") + System.Environment.NewLine;
            foreach (string e in beamEnergies) configTB.Text += string.Format(" {0}", e) + System.Environment.NewLine;
            configTB.Text += String.Format(" Beams per isocenter: ");
            for (int i = 0; i < beamsPerIso.Length; i++)
            {
                configTB.Text += String.Format(" {0}", beamsPerIso.ElementAt(i));
                if (i != beamsPerIso.Length - 1) configTB.Text += String.Format(", ");
            }
            configTB.Text += System.Environment.NewLine;
            configTB.Text += String.Format(" Collimator rotation (deg) order: ");
            for (int i = 0; i < collRot.Length; i++)
            {
                configTB.Text += String.Format(" {0:0.0}", collRot.ElementAt(i));
                if (i != collRot.Length - 1) configTB.Text += String.Format(", ");
            }
            configTB.Text += System.Environment.NewLine;
            configTB.Text += String.Format(" Field jaw position (cm) order: ") + System.Environment.NewLine;
            configTB.Text += String.Format(" (x1,y1,x2,y2)") + System.Environment.NewLine;
            foreach (VRect<double> j in jawPos) configTB.Text += String.Format(" ({0:0.0},{1:0.0},{2:0.0},{3:0.0})", j.X1 / 10, j.Y1 / 10, j.X2 / 10, j.Y2 / 10) + System.Environment.NewLine;
            configTB.Text += String.Format(" Photon dose calculation model: {0}", calculationModel) + System.Environment.NewLine;
            configTB.Text += String.Format(" Use GPU for dose calculation: {0}", useGPUdose) + System.Environment.NewLine;
            configTB.Text += String.Format(" Photon optimization model: {0}", optimizationModel) + System.Environment.NewLine;
            configTB.Text += String.Format(" Use GPU for optimization: {0}", useGPUoptimization) + System.Environment.NewLine;
            configTB.Text += String.Format(" MR level restart at: {0}", MRrestartLevel) + System.Environment.NewLine + System.Environment.NewLine;

            configTB.Text += String.Format(" Requested general tuning structures:") + System.Environment.NewLine;
            configTB.Text += String.Format("  {0, -10} | {1, -15} |", "DICOM type", "Structure Id") + System.Environment.NewLine;
            foreach (Tuple<string, string> ts in defaultTS_structures) configTB.Text += String.Format("  {0, -10} | {1, -15} |" + System.Environment.NewLine, ts.Item1, ts.Item2);
            configTB.Text += System.Environment.NewLine;

            configTB.Text += String.Format(" Default sparing structures:") + System.Environment.NewLine;
            configTB.Text += String.Format("  {0, -15} | {1, -19} | {2, -11} |", "structure Id", "sparing type", "margin (cm)") + System.Environment.NewLine;
            foreach (Tuple<string, string, double> spare in defaultSpareStruct) configTB.Text += String.Format("  {0, -15} | {1, -19} | {2,-11:N1} |" + System.Environment.NewLine, spare.Item1, spare.Item2, spare.Item3);
            configTB.Text += System.Environment.NewLine;

            foreach(autoPlanTemplate itr in PlanTemplates.Where(x => x.templateName != "--select--"))
            {
                configTB.Text += "-----------------------------------------------------------------------------" + System.Environment.NewLine;

                configTB.Text += String.Format(" Template ID: {0}", itr.templateName) + System.Environment.NewLine;
                configTB.Text += String.Format(" Initial Dose per fraction: {0} cGy", itr.initialRxDosePerFx) + System.Environment.NewLine;
                configTB.Text += String.Format(" Initial number of fractions: {0}", itr.initialRxNumFx) + System.Environment.NewLine;
                configTB.Text += String.Format(" Boost Dose per fraction: {0} cGy", itr.boostRxDosePerFx) + System.Environment.NewLine;
                configTB.Text += String.Format(" Boost number of fractions: {0}", itr.boostRxNumFx) + System.Environment.NewLine;

                if (itr.targets.Any())
                {
                    configTB.Text += String.Format(" {0} targets:", itr.templateName) + System.Environment.NewLine;
                    configTB.Text += String.Format("  {0, -15} | {1, -8} | {2, -14} |", "structure Id", "Rx (cGy)", "Plan Id") + System.Environment.NewLine;
                    foreach (Tuple<string, double, string> tgt in itr.targets) configTB.Text += String.Format("  {0, -15} | {1, -8} | {2,-14:N1} |" + System.Environment.NewLine, tgt.Item1, tgt.Item2, tgt.Item3);
                    configTB.Text += System.Environment.NewLine;
                }
                else configTB.Text += String.Format(" No targets set for template: {0}", itr.templateName) + System.Environment.NewLine + System.Environment.NewLine;

                if(itr.TS_structures.Any())
                {
                    configTB.Text += String.Format(" {0} additional tuning structures:", itr.templateName) + System.Environment.NewLine;
                    configTB.Text += String.Format("  {0, -10} | {1, -15} |", "DICOM type", "Structure Id") + System.Environment.NewLine;
                    foreach (Tuple<string, string> ts in itr.TS_structures) configTB.Text += String.Format("  {0, -10} | {1, -15} |" + System.Environment.NewLine, ts.Item1, ts.Item2);
                    configTB.Text += System.Environment.NewLine;
                }
                else configTB.Text += String.Format(" No additional tuning structures for template: {0}", itr.templateName) + System.Environment.NewLine + System.Environment.NewLine;
                
                if (itr.spareStructures.Any())
                {
                    configTB.Text += String.Format(" {0} additional sparing structures:", itr.templateName) + System.Environment.NewLine;
                    configTB.Text += String.Format("  {0, -15} | {1, -19} | {2, -11} |", "structure Id", "sparing type", "margin (cm)") + System.Environment.NewLine;
                    foreach (Tuple<string, string, double> spare in itr.spareStructures) configTB.Text += String.Format("  {0, -15} | {1, -19} | {2,-11:N1} |" + System.Environment.NewLine, spare.Item1, spare.Item2, spare.Item3);
                    configTB.Text += System.Environment.NewLine;
                }
                else configTB.Text += String.Format(" No additional sparing structures for template: {0}", itr.templateName) + System.Environment.NewLine + System.Environment.NewLine;

                if(itr.init_constraints.Any())
                {
                    configTB.Text += String.Format(" {0} template initial plan optimization parameters:", itr.templateName) + System.Environment.NewLine;
                    configTB.Text += String.Format("  {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority") + System.Environment.NewLine;
                    foreach (Tuple<string, string, double, double, int> opt in itr.init_constraints) configTB.Text += String.Format("  {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |" + System.Environment.NewLine, opt.Item1, opt.Item2, opt.Item3, opt.Item4, opt.Item5);
                    configTB.Text += System.Environment.NewLine;
                }
                else configTB.Text += String.Format(" No iniital plan optimization constraints for template: {0}", itr.templateName) + System.Environment.NewLine + System.Environment.NewLine;

                if (itr.bst_constraints.Any())
                {
                    configTB.Text += String.Format(" {0} template boost plan optimization parameters:", itr.templateName) + System.Environment.NewLine;
                    configTB.Text += String.Format("  {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority") + System.Environment.NewLine;
                    foreach (Tuple<string, string, double, double, int> opt in itr.bst_constraints) configTB.Text += String.Format("  {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |" + System.Environment.NewLine, opt.Item1, opt.Item2, opt.Item3, opt.Item4, opt.Item5);
                }
                else configTB.Text += String.Format(" No boost plan optimization constraints for template: {0}", itr.templateName) + System.Environment.NewLine + System.Environment.NewLine;

                configTB.Text += System.Environment.NewLine;
            }
            configTB.Text += "-----------------------------------------------------------------------------" + System.Environment.NewLine;
            configScroller.ScrollToTop();
        }

        //stuff related to Export CT tab
        private void populateCTImageSets()
        {
            UIhelper helper = new UIhelper();
            //needed to allow automatic selection of CT image for selected CT structure set (nothing will be selected if no structure set is selected)
            List<StructureSet> structureSets = new List<StructureSet>(pi.StructureSets.Where(x => x != selectedSS));
            if (selectedSS != null) { structureSets.Insert(0, selectedSS); }
            foreach (StructureSet itr in structureSets) CTimage_sp.Children.Add(helper.getCTImageSets(CTimage_sp, itr.Image, itr == selectedSS ? true : false));
        }

        private void exportImgInfo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Select a CT image to export to the deep learning model (for autocontouring)");
        }

        private void exportImg_Click(object sender, RoutedEventArgs e)
        {
            UIhelper helper = new UIhelper();
            string selectedCTID = helper.parseSelectedCTImage(CTimage_sp);
            if (!string.IsNullOrWhiteSpace(selectedCTID))
            {
                VMS.TPS.Common.Model.API.Image theImage = pi.StructureSets.FirstOrDefault(x => x.Image.Id == selectedCTID).Image;
                helpers.CTImageExport exporter = new helpers.CTImageExport(theImage, imgExportPath, pi.Id, imgExportFormat);
                if (exporter.exportImage()) return;
                MessageBox.Show(String.Format("{0} has been exported successfully!", theImage.Id));
            }
            else MessageBox.Show("No imaged selected for export!");
        }

        //stuff related to Set Targets tab
        private void addTarget_Click(object sender, RoutedEventArgs e)
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
                theSP = targets_sp;
            }
            add_target_volumes(new List<Tuple<string, double, string>> { Tuple.Create("--select--", 0.0, "--select--") }, theSP);
            theScroller.ScrollToBottom();
        }

        private void addTargetDefaults_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSS == null) { MessageBox.Show("Error! The structure set has not been assigned! Choose a structure set and try again!"); return; }
            List<Tuple<string, double, string>> tmpList = new List<Tuple<string, double, string>> { Tuple.Create("--select--", 0.0, "--select--") };
            List<Tuple<string, double, string>> targetList = new List<Tuple<string, double, string>> { };
            if ((templateList.SelectedItem as autoPlanTemplate) != null)
            {
                tmpList = new List<Tuple<string, double, string>>((templateList.SelectedItem as autoPlanTemplate).targets);
                foreach (Tuple<string, double, string> itr in tmpList) if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == itr.Item1.ToLower()) != null || itr.Item1.ToLower() == "ptv_csi") targetList.Add(itr);
            }
            else targetList = new List<Tuple<string, double, string>>(tmpList);
            clear_targets_list();
            add_target_volumes(targetList, targets_sp);
            targetsScroller.ScrollToBottom();
        }

        private void scanSSAndAddTargets_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSS == null) { MessageBox.Show("Error! The structure set has not been assigned! Choose a structure set and try again!"); return; }
            List<Structure> tgt = selectedSS.Structures.Where(x => x.Id.Contains("PTV") && !x.Id.ToLower().Contains("ts_")).ToList();
            if (!tgt.Any()) return;
            List<Tuple<string, double, string>> targetList = new List<Tuple<string, double, string>> { };
            string structureID;
            double tgtRx;
            foreach(Structure itr in tgt)
            {
                structureID = itr.Id;
                if (!double.TryParse(itr.Id.Substring(itr.Id.IndexOf("_") + 1, itr.Id.Length - (itr.Id.IndexOf("_") + 1)), out tgtRx)) tgtRx = 0.1;
                targetList.Add(new Tuple<string, double, string>(structureID, tgtRx, "_VMAT CSI"));
            }
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
                theSP = targets_sp;
            }
            clear_targets_list();
            add_target_volumes(targetList, theSP);
            theScroller.ScrollToBottom();
        }

        private void clearTargetBtn_click(object sender, RoutedEventArgs e)
        {
            //same deal as the clear sparing structure button (clearStructBtn_click)
            Button btn = (Button)sender;
            int i = 0;
            int k = 0;
            StackPanel theSP;
            if (btn.Name.Contains("template")) theSP = targetTemplate_sp;
            else theSP = targets_sp;
            foreach (object obj in theSP.Children)
            {
                foreach (object obj1 in ((StackPanel)obj).Children)
                {
                    if (obj1.Equals(btn)) k = i;
                }
                if (k > 0) break;
                i++;
            }

            //clear entire list if there are only two entries (header + 1 real entry)
            if (theSP.Children.Count < 3) clear_targets_list(btn);
            else theSP.Children.RemoveAt(k);
        }

        private void clear_targetList_click(object sender, RoutedEventArgs e) { clear_targets_list((Button)sender); }

        private void clear_targets_list(Button btn = null)
        {
            if(btn == null || btn.Name == "clear_target_list" || !btn.Name.Contains("template"))
            {
                firstTargetStruct = true;
                targets_sp.Children.Clear();
                clearTargetBtnCounter = 0;
            }
            else
            {
                firstTargetTemplateStruct = true;
                targetTemplate_sp.Children.Clear();
                clearTargetTemplateBtnCounter = 0;
            }
        }

        private void targetUI_cb_change(StackPanel theSP, object sender, EventArgs e, bool isTargetStructure)
        {
            //not the most elegent code, but it works. Basically, it finds the combobox where the selection was changed and asks the user to enter the id of the plan or the target id
            ComboBox c = (ComboBox)sender;
            if (c.SelectedItem.ToString() != "--Add New--") return;
            foreach (object obj in theSP.Children)
            {
                UIElementCollection row = ((StackPanel)obj).Children;
                foreach (object obj1 in row)
                {
                    //the btn has a unique tag to it, so we can just loop through all children in the structures_sp children list and find which button is equivalent to our button
                    if (obj1.Equals(c))
                    {
                        string msg = "Enter the Id of the target structure!";
                        if(!isTargetStructure) msg = "Enter the requested plan Id!";
                        enterMissingInfo emi = new enterMissingInfo(msg, "Id:");
                        emi.ShowDialog();
                        if (emi.confirm)
                        {
                            c.Items.Insert(c.Items.Count - 1, emi.value.Text);
                            //c.Items.Add(emi.value.Text);
                            c.Text = emi.value.Text;
                        }
                        else c.SelectedIndex = 0;
                        return;
                    }
                }
            }
        }

        private void add_target_volumes(List<Tuple<string,double,string>> defaultList, StackPanel theSP)
        {
            bool firstStruct;
            int counter;
            string clearBtnNamePrefix;
            if (theSP.Name == "targets_sp")
            {
                firstStruct = firstTargetStruct;
                counter = clearTargetBtnCounter;
                clearBtnNamePrefix = "clearTargetBtn";
            }
            else
            {
                firstStruct = firstTargetTemplateStruct;
                counter = clearTargetTemplateBtnCounter;
                clearBtnNamePrefix = "templateClearTargetBtn";
            }
            if (firstStruct) add_target_header(theSP);
            List<string> planIDs = new List<string> { };
            foreach (Tuple<string, double, string> itr in defaultList) planIDs.Add(itr.Item3);
            planIDs.Add("--Add New--");
            foreach(Tuple<string, double, string> itr in defaultList)
            {
                counter++;

                StackPanel sp = new StackPanel();
                sp.Height = 30;
                sp.Width = targets_sp.Width;
                sp.Orientation = Orientation.Horizontal;
                sp.Margin = new Thickness(25, 0, 5, 5);

                ComboBox str_cb = new ComboBox();
                str_cb.Name = "str_cb";
                str_cb.Width = 150;
                str_cb.Height = sp.Height - 5;
                str_cb.HorizontalAlignment = HorizontalAlignment.Left;
                str_cb.VerticalAlignment = VerticalAlignment.Top;
                str_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
                str_cb.Margin = new Thickness(5, 5, 0, 0);

                str_cb.Items.Add(itr.Item1);
                str_cb.Items.Add("--Add New--");
                str_cb.SelectedIndex = 0;
                str_cb.SelectionChanged += delegate(object sender, SelectionChangedEventArgs e) { targetUI_cb_change(theSP, sender, e, true); };
                sp.Children.Add(str_cb);

                TextBox RxDose_tb = new TextBox();
                RxDose_tb.Name = "RxDose_tb";
                RxDose_tb.Width = 120;
                RxDose_tb.Height = sp.Height - 5;
                RxDose_tb.HorizontalAlignment = HorizontalAlignment.Left;
                RxDose_tb.VerticalAlignment = VerticalAlignment.Top;
                RxDose_tb.TextAlignment = TextAlignment.Center;
                RxDose_tb.VerticalContentAlignment = VerticalAlignment.Center;
                RxDose_tb.Margin = new Thickness(5, 5, 0, 0);
                RxDose_tb.Text = itr.Item2.ToString();
                sp.Children.Add(RxDose_tb);

                ComboBox planId_cb = new ComboBox();
                planId_cb.Name = "planId_cb";
                planId_cb.Width = 150;
                planId_cb.Height = sp.Height - 5;
                planId_cb.HorizontalAlignment = HorizontalAlignment.Left;
                planId_cb.VerticalAlignment = VerticalAlignment.Top;
                planId_cb.Margin = new Thickness(5, 5, 0, 0);
                planId_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
                //string[] types = new string[] { itr.Item3, "--Add New--" };
                foreach (string p in planIDs) planId_cb.Items.Add(p);
                planId_cb.Text = itr.Item3;
                planId_cb.SelectionChanged += delegate (object sender, SelectionChangedEventArgs e) { targetUI_cb_change(theSP, sender, e, false); };
                //planId_cb.SelectionChanged += new SelectionChangedEventHandler(type_cb_change);
                sp.Children.Add(planId_cb);

                Button clearStructBtn = new Button();
                clearStructBtn.Name = clearBtnNamePrefix + counter;
                clearStructBtn.Content = "Clear";
                clearStructBtn.Click += new RoutedEventHandler(this.clearTargetBtn_click);
                clearStructBtn.Width = 50;
                clearStructBtn.Height = sp.Height - 5;
                clearStructBtn.HorizontalAlignment = HorizontalAlignment.Left;
                clearStructBtn.VerticalAlignment = VerticalAlignment.Top;
                clearStructBtn.Margin = new Thickness(10, 5, 0, 0);
                sp.Children.Add(clearStructBtn);

                theSP.Children.Add(sp);
            }
        }

        private void add_target_header(StackPanel theSP)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = targets_sp.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(25, 0, 5, 5);

            Label strName = new Label();
            strName.Content = "Target Id";
            strName.HorizontalAlignment = HorizontalAlignment.Center;
            strName.VerticalAlignment = VerticalAlignment.Top;
            strName.Width = 100;
            strName.FontSize = 14;
            strName.Margin = new Thickness(45, 0, 0, 0);

            Label spareType = new Label();
            spareType.Content = "Prescription (cGy)";
            spareType.HorizontalAlignment = HorizontalAlignment.Center;
            spareType.VerticalAlignment = VerticalAlignment.Top;
            spareType.Width = 130;
            spareType.FontSize = 14;
            spareType.Margin = new Thickness(20, 0, 0, 0);

            Label marginLabel = new Label();
            marginLabel.Content = "Plan Id";
            marginLabel.HorizontalAlignment = HorizontalAlignment.Center;
            marginLabel.VerticalAlignment = VerticalAlignment.Top;
            marginLabel.Width = 150;
            marginLabel.FontSize = 14;
            marginLabel.Margin = new Thickness(30, 0, 0, 0);

            sp.Children.Add(strName);
            sp.Children.Add(spareType);
            sp.Children.Add(marginLabel);
            theSP.Children.Add(sp);

            if (theSP.Name == "targets_sp") firstTargetStruct = false;
            else firstTargetTemplateStruct = false;
        }

        private void set_targets_Click(object sender, RoutedEventArgs e)
        {
            if(selectedSS == null)
            {
                MessageBox.Show("Please select a structure set before setting the targets!");
                return;
            }
            if (targets_sp.Children.Count == 0)
            {
                MessageBox.Show("No targets present in list! Please add some targets to the list before setting the target structures!");
                return;
            }

            targets = new List<Tuple<string, double, string>>(parseTargets(targets_sp));
            prescriptions = new List<Tuple<string, string, int, DoseValue, double>> { };
            string targetid = "";
            double rx = 0.0;
            string pid = "";
            int numPlans = 0;
            double dose_perFx = 0.0;
            int numFractions = 0;

            foreach (Tuple<string, double, string> itr in targets)
            {
                if (itr.Item3 != pid) numPlans++;
                pid = itr.Item3;
                rx = itr.Item2;
                targetid = itr.Item1;
                if (rx == double.Parse(initRxTB.Text))
                {
                    if (!double.TryParse(initDosePerFxTB.Text, out dose_perFx) || !int.TryParse(initNumFxTB.Text, out numFractions))
                    {
                        MessageBox.Show("Error! Could not parse dose per fx or number of fractions for initial plan! Exiting");
                        targets = new List<Tuple<string, double, string>> { };
                        prescriptions = new List<Tuple<string, string, int, DoseValue, double>> { };
                        return;
                    }
                }
                else
                {
                    if (!double.TryParse(boostDosePerFxTB.Text, out dose_perFx) || !int.TryParse(boostNumFxTB.Text, out numFractions))
                    {
                        MessageBox.Show("Error! Could not parse dose per fx or number of fractions for boost plan! Exiting");
                        targets = new List<Tuple<string, double, string>> { };
                        prescriptions = new List<Tuple<string, string, int, DoseValue, double>> { };
                        return;
                    }
                }
                prescriptions.Add(Tuple.Create(pid, targetid, numFractions, new DoseValue(dose_perFx, DoseValue.DoseUnit.cGy), rx));
                if (numPlans > 2) { MessageBox.Show("Error! Number of request plans is > 2! Exiting!"); targets = new List<Tuple<string, double, string>> { };  prescriptions = new List<Tuple<string, string, int, DoseValue, double>> { }; return; }
            }
            //sort the prescription list by the cumulative rx dose
            prescriptions.Sort(delegate (Tuple<string, string, int, DoseValue, double> x, Tuple<string, string, int, DoseValue, double> y) { return x.Item5.CompareTo(y.Item5); });

            MessageBox.Show("Targets set successfully!");
            string msg = "Prescriptions:" + Environment.NewLine;
            foreach(Tuple<string,string, int, DoseValue, double> itr in prescriptions) msg += String.Format("{0}, {1}, {2}, {3}, {4}", itr.Item1, itr.Item2, itr.Item3, itr.Item4.Dose, itr.Item5) + Environment.NewLine;
            MessageBox.Show(msg);
        }

        private List<Tuple<string,double,string>> parseTargets(StackPanel theSP)
        {
            List<Tuple<string, double, string>> listTargets = new List<Tuple<string, double, string>> { };
            string structure = "";
            double tgtRx = -1000.0;
            string planID = "";
            bool firstCombo = true;
            bool headerObj = true;
            foreach (object obj in theSP.Children)
            {
                //skip over the header row
                if (!headerObj)
                {
                    foreach (object obj1 in ((StackPanel)obj).Children)
                    {
                        if (obj1.GetType() == typeof(ComboBox))
                        {
                            //first combo box is the structure and the second is the sparing type
                            if (firstCombo)
                            {
                                structure = (obj1 as ComboBox).SelectedItem.ToString();
                                firstCombo = false;
                            }
                            else planID = (obj1 as ComboBox).SelectedItem.ToString();
                        }
                        //try to parse the target Rx as a double value
                        else if (obj1.GetType() == typeof(TextBox)) if (!string.IsNullOrWhiteSpace((obj1 as TextBox).Text)) double.TryParse((obj1 as TextBox).Text, out tgtRx);
                    }
                    if (structure == "--select--" || planID == "--select--")
                    {
                        MessageBox.Show("Error! \nStructure or plan not selected! \nSelect an option and try again");
                        return listTargets;
                    }
                    //margin will not be assigned from the default value (-1000) if the input is empty, a whitespace, or NaN
                    else if (tgtRx == -1000.0)
                    {
                        MessageBox.Show("Error! \nEntered Rx value is invalid! \nEnter a new Rx and try again");
                        return listTargets;
                    }
                    else
                    {
                        if (planID.Length > 13)
                        {
                            //MessageBox.Show(String.Format("Error! Plan Id '{0}' is greater than maximum length allowed by Eclipse (13)! Exiting!", planID));
                            planID = planID.Substring(0, 13);
                        }
                        //only add the current row to the structure sparing list if all the parameters were successful parsed
                        if (!structure.ToLower().Contains("ctv_spine") && !structure.ToLower().Contains("ctv_brain") && !structure.ToLower().Contains("ptv_spine") && !structure.ToLower().Contains("ptv_brain") && !structure.ToLower().Contains("ptv_csi"))
                        {
                            //if the requested target does not have an id that contains ctv, ptv, brain, spine, or ptv_csi, check to make sure it actually exists in the structure set before proceeding
                            Structure unknownStructure = selectedSS.Structures.FirstOrDefault(x => x.Id == structure);
                            if (unknownStructure == null || unknownStructure.IsEmpty)
                            {
                                MessageBox.Show(String.Format("Error! Structure: {0} not found or is empty! Please remove and try again!", structure));
                                return listTargets;
                            }
                        }
                        listTargets.Add(Tuple.Create(structure, tgtRx, planID));
                    }
                    firstCombo = true;
                    tgtRx = -1000.0;
                }
                else headerObj = false;
            }

            //sort the targets based on requested plan Id (alphabetically)
            listTargets.Sort(delegate (Tuple<string, double, string> x, Tuple<string, double, string> y) { return x.Item3.CompareTo(y.Item3); });
            return listTargets;
        }

        //stuff related to TS Generation tab
        private void TargetMarginInfo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Specify the inner body margin (in cm) that should be used to create the PTV. Typical values range from 0.0 to 0.5 cm. Default value at Stanford University is 0.3 cm.");
        }

        private void contourOverlapInfo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Selecting this option will contour the (approximate) overlap between fields in adjacent isocenters in the VMAT plan and assign the resulting structures as targets in the optimization.");
        }

        private void checkLRStructures()
        {
            //check if structures need to be unioned before adding defaults
            UIhelper helper = new UIhelper();
            List<Tuple<Structure, Structure>> structuresToUnion = helper.checkStructuresToUnion(selectedSS);
            if (structuresToUnion.Any())
            {
                string msg = "Structures to union:" + Environment.NewLine;
                foreach (Tuple<Structure, Structure> itr in structuresToUnion) msg += String.Format("{0}, {1}", itr.Item1.Id, itr.Item2.Id) + Environment.NewLine;
                msg += Environment.NewLine + "Continue?";
                confirmUI CUI = new confirmUI();
                CUI.message.Text = msg;
                CUI.message.Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
                CUI.ShowDialog();
                if (CUI.confirm)
                {
                    int numUnioned = 0;
                    pi.BeginModifications();
                    foreach (Tuple<Structure, Structure> itr in structuresToUnion) if (!helper.unionLRStructures(itr, selectedSS)) numUnioned++;
                    if (numUnioned > 0)
                    {
                        MessageBox.Show("L and R structures have been unioned! Please review the contours after saving!");
                        isModified = true;
                    }
                }
            }
            checkStructuresToUnion = false;
        }

        //add structure to spare to the list
        private void add_str_click(object sender, RoutedEventArgs e)
        {
            Button theBtn = sender as Button;
            ScrollViewer theScroller;
            StackPanel theSP;
            if(theBtn.Name.Contains("template"))
            {
                theScroller = templateSpareStructScroller;
                theSP = templateStructures_sp;
            }
            else
            {
                if (checkStructuresToUnion) checkLRStructures();
                theScroller = spareStructScroller;
                theSP = structures_sp;
            }
            //populate the comboboxes
            add_sp_volumes(new List<Tuple<string, string, double>> { Tuple.Create("--select--", "--select--", 0.0) }, theSP);
            theScroller.ScrollToBottom();
        }

        //add the header to the structure sparing list (basically just add some labels to make it look nice)
        private void add_sp_header(StackPanel theSP)
        {
            theSP.Children.Add(new UIhelper().getSpareStructHeader(theSP));

            //bool to indicate that the header has been added
            if (theSP.Name.Contains("template")) firstTemplateSpareStruct = false;
            else firstSpareStruct = false;
        }

        //populate the structure sparing list. This method is called whether the add structure or add defaults buttons are hit (because a vector containing the list of structures is passed as an argument to this method)
        private void add_sp_volumes(List<Tuple<string, string, double>> defaultList, StackPanel theSP)
        {
            if (selectedSS == null) { MessageBox.Show("Error! Please select a Structure Set before add sparing volumes!"); return; }
            bool firstStruct;
            int counter;
            string clearBtnNamePrefix;
            if (theSP.Name.Contains("template"))
            {
                firstStruct = firstTemplateSpareStruct;
                counter = clearTemplateSpareBtnCounter;
                clearBtnNamePrefix = "templateClearSpareStructBtn";
            }
            else
            {
                firstStruct = firstSpareStruct;
                counter = clearSpareBtnCounter;
                clearBtnNamePrefix = "clearSpareStructBtn";
            }
            if (firstStruct) add_sp_header(theSP);
            UIhelper helper = new UIhelper();
            for (int i = 0; i < defaultList.Count; i++)
            {
                counter++;
                theSP.Children.Add(helper.addSpareStructVolume(theSP, selectedSS, defaultList[i], clearBtnNamePrefix, counter, (delegate (object sender, SelectionChangedEventArgs e) { type_cb_change(theSP, sender, e); }), new RoutedEventHandler(this.clearStructBtn_click)));
            }
        }

        private void type_cb_change(StackPanel theSP, object sender, EventArgs e)
        {
            //not the most elegent code, but it works. Basically, it finds the combobox where the selection was changed and increments one additional child to get the add margin text box. Then it can change
            //the visibility of this textbox based on the sparing type selected for this structure
            ComboBox c = (ComboBox)sender;
            bool row = false;
            foreach (object obj in theSP.Children)
            {
                foreach (object obj1 in ((StackPanel)obj).Children)
                {
                    //the btn has a unique tag to it, so we can just loop through all children in the structures_sp children list and find which button is equivalent to our button
                    if (row)
                    {
                        if (c.SelectedItem.ToString() != "Mean Dose < Rx Dose" && c.SelectedItem.ToString() != "Crop from target" && c.SelectedItem.ToString() != "Crop from Body") (obj1 as TextBox).Visibility = Visibility.Hidden;
                        else (obj1 as TextBox).Visibility = Visibility.Visible;
                        return;
                    }
                    if (obj1.Equals(c)) row = true;
                }
            }
        }

        //method to clear and individual row in the structure sparing list (i.e., remove a single structure)
        private void clearStructBtn_click(object sender, EventArgs e) { if (new UIhelper().clearRow(sender, (sender as Button).Name.Contains("template") ? templateStructures_sp : structures_sp)) clear_spare_list((sender as Button).Name.Contains("template") ? templateClearSpareStructuresBtn : clearSpareStructuresBtn); }

        private void SSID_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //clear sparing structure list
            clear_spare_list(clearSpareStructuresBtn);

            //clear optimization structure list
            clear_optimization_parameter_list(opt_parameters);

            //update selected structure set
            selectedSS = pi.StructureSets.FirstOrDefault(x => x.Id == SSID.SelectedItem.ToString());
        }

        private void add_spareDefaults_click(object sender, RoutedEventArgs e)
        {
            if (selectedSS == null) { MessageBox.Show("Error! The structure set has not been assigned! Choose a structure set and try again!"); return; }
            if (checkStructuresToUnion) checkLRStructures();
            //copy the sparing structures in the defaultSpareStruct list to a temporary vector
            List<Tuple<string, string, double>> templateSpareList = new List<Tuple<string, string, double>>(defaultSpareStruct);
            //add the case-specific sparing structures to the temporary list
            if (templateList.SelectedItem != null) templateSpareList = new List<Tuple<string, string, double>>(addTemplateSpecificSpareStructures((templateList.SelectedItem as autoPlanTemplate).spareStructures, templateSpareList));

            string missOutput = "";
            string emptyOutput = "";
            int missCount = 0;
            int emptyCount = 0;
            List<Tuple<string, string, double>> defaultList = new List<Tuple<string, string, double>> { };
            foreach (Tuple<string, string, double> itr in templateSpareList)
            {
                //check to ensure the structures in the templateSpareList vector are actually present in the selected structure set and are actually contoured. If they are, add them to the defaultList vector, which will be passed 
                //to the add_sp_volumes method
                if (!selectedSS.Structures.Where(x => x.Id.ToLower() == itr.Item1.ToLower()).Any())
                {
                    if (missCount == 0) missOutput = String.Format("Warning! The following default structures are missing from the selected structure list:\n");
                    missOutput += String.Format("{0}\n", itr.Item1);
                    missCount++;
                }
                else if (selectedSS.Structures.First(x => x.Id.ToLower() == itr.Item1.ToLower()).IsEmpty)
                {
                    if (emptyCount == 0) emptyOutput = String.Format("Warning! The following default structures are present but empty:\n");
                    emptyOutput += String.Format("{0}\n", itr.Item1);
                    emptyCount++;
                }
                else defaultList.Add(Tuple.Create(selectedSS.Structures.First(x => x.Id.ToLower() == itr.Item1.ToLower()).Id, itr.Item2, itr.Item3));
            }

            clear_spare_list(clearSpareStructuresBtn);
            add_sp_volumes(defaultList, structures_sp);
            if (missCount > 0) MessageBox.Show(missOutput);
            if (emptyCount > 0) MessageBox.Show(emptyOutput);
        }

        //helper method to easily add sparing structures to a sparing structure list. The reason this is its own method is because of the logic used to include/remove sex-specific organs
        private List<Tuple<string, string, double>> addTemplateSpecificSpareStructures(List<Tuple<string, string, double>> caseSpareStruct, List<Tuple<string, string, double>> template)
        {
            foreach (Tuple<string, string, double> s in caseSpareStruct)
            {
                if (s.Item1.ToLower() == "ovaries" || s.Item1.ToLower() == "testes") { if ((pi.Sex == "Female" && s.Item1.ToLower() == "ovaries") || (pi.Sex == "Male" && s.Item1.ToLower() == "testes")) template.Add(s); }
                else template.Add(s);
            }
            return template;
        }

        //wipe the displayed list of sparing structures
        private void clear_spareList_click(object sender, RoutedEventArgs e) { clear_spare_list((sender as Button)); }

        private void clear_spare_list(Button theBtn)
        {
            if(theBtn.Name.Contains("template"))
            {
                firstTemplateSpareStruct = true;
                templateStructures_sp.Children.Clear();
                clearTemplateSpareBtnCounter = 0;
            }
            else
            {
                firstSpareStruct = true;
                structures_sp.Children.Clear();
                clearSpareBtnCounter = 0;
            }
        }

        private void generateStruct(object sender, RoutedEventArgs e)
        {
            //check that there are actually structures to spare in the sparing list
            if (structures_sp.Children.Count == 0)
            {
                MessageBox.Show("No structures present to generate tuning structures!");
                return;
            }

            if(!targets.Any())
            {
                MessageBox.Show("Please set the targets first on the 'Set Targets' tab!");
                return;
            }

            List<Tuple<string, string, double>> structureSpareList = new UIhelper().parseSpareStructList(structures_sp);
            if (!structureSpareList.Any()) return;

            TS_structures = new List<Tuple<string, string>> (defaultTS_structures);
            if(templateList.SelectedItem != null) foreach (Tuple<string, string> itr in ((autoPlanTemplate)templateList.SelectedItem).TS_structures) TS_structures.Add(itr);

            //create an instance of the generateTS class, passing the structure sparing list vector, the selected structure set, and if this is the scleroderma trial treatment regiment
            //The scleroderma trial contouring/margins are specific to the trial, so this trial needs to be handled separately from the generic VMAT treatment type
            generateTS_CSI generate = new generateTS_CSI(TS_structures, structureSpareList, targets, prescriptions, selectedSS);
            pi.BeginModifications();
            if (generate.generateStructures()) return;
            //does the structure sparing list need to be updated? This occurs when structures the user elected to spare with option of 'Mean Dose < Rx Dose' are high resolution. Since Eclipse can't perform
            //boolean operations on structures of two different resolutions, code was added to the generateTS class to automatically convert these structures to low resolution with the name of
            // '<original structure Id>_lowRes'. When these structures are converted to low resolution, the updateSparingList flag in the generateTS class is set to true to tell this class that the 
            //structure sparing list needs to be updated with the new low resolution structures.
            if (generate.updateSparingList)
            {
                clear_spare_list(clearSpareStructuresBtn);
                //update the structure sparing list in this class and update the structure sparing list displayed to the user in TS Generation tab
                structureSpareList = generate.spareStructList;
                add_sp_volumes(structureSpareList, structures_sp);
            }
            //the number of isocenters will always be equal to the number of vmat isocenters for vmat csi
            isoNames = generate.isoNames;

            //populate the beams and optimization tabs
            populateBeamsTab();
            populateOptimizationTab(opt_parameters);
            isModified = true;
        }

        //stuff related to beam placement tab
        private void contourOverlapChecked(object sender, RoutedEventArgs e)
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

        private void populateBeamsTab()
        {
            //default option to contour overlap between fields in adjacent isocenters and assign the resulting structures as targets
            contourOverlap_chkbox.IsChecked = contourOverlap;
            contourOverlapTB.Text = contourFieldOverlapMargin;

            BEAMS_SP.Children.Clear();

            //number of isocenters = number of vmat isocenters
            List<StackPanel> SPList = new UIhelper().populateBeamsTabHelper(structures_sp, linacs, beamEnergies, isoNames, beamsPerIso);
            if (!SPList.Any()) return;
            foreach (StackPanel s in SPList) BEAMS_SP.Children.Add(s);
        }

        private void place_beams_Click(object sender, RoutedEventArgs e)
        {
            if (BEAMS_SP.Children.Count == 0)
            {
                MessageBox.Show("No isocenters present to place beams!");
                return;
            }

            int count = 0;
            bool firstCombo = true;
            string chosenLinac = "";
            string chosenEnergy = "";
            //int[,] numBeams = new int[numVMATIsos];
            List<List<int>> numBeams = new List<List<int>> { };
            List<int> numBeams_temp = new List<int> { };
            int numElementsPerRow = 0;
            foreach (object obj in BEAMS_SP.Children)
            {
                foreach (object obj1 in ((StackPanel)obj).Children)
                {
                    if (obj1.GetType() == typeof(ComboBox))
                    {
                        //similar code to parsing the structure sparing list
                        if (firstCombo)
                        {
                            chosenLinac = (obj1 as ComboBox).SelectedItem.ToString();
                            firstCombo = false;
                        }
                        else chosenEnergy = (obj1 as ComboBox).SelectedItem.ToString();
                    }
                    if (obj1.GetType() == typeof(TextBox))
                    {
                        // MessageBox.Show(count.ToString());
                        if (!int.TryParse((obj1 as TextBox).Text, out int beamTMP))
                        {
                            MessageBox.Show(String.Format("Error! \nNumber of beams entered in iso {0} is NaN!", isoNames.ElementAt(count)));
                            return;
                        }
                        else if (beamTMP < 1)
                        {
                            MessageBox.Show(String.Format("Error! \nNumber of beams entered in iso {0} is < 1!", isoNames.ElementAt(count)));
                            return;
                        }
                        else if (beamTMP > 4)
                        {
                            MessageBox.Show(String.Format("Error! \nNumber of beams entered in iso {0} is > 4!", isoNames.ElementAt(count)));
                            return;
                        }
                        else numBeams_temp.Add(beamTMP);
                        count++;
                    }
                    numElementsPerRow++;
                }
                if(numElementsPerRow == 1 && numBeams_temp.Any())
                {
                    //indicates only one item was in this stack panel indicating it was only a label indicating the code has finished reading the number of isos and beams per isos for this plan
                    numBeams.Add(new List<int>(numBeams_temp));
                    numBeams_temp = new List<int> { };
                }
                numElementsPerRow = 0;
            }
            numBeams.Add(new List<int>(numBeams_temp));

            List<Tuple<string, List<Tuple<string, int>>>> planIsoBeamInfo = new List<Tuple<string, List<Tuple<string, int>>>> { };
            count = 0;
            foreach(Tuple<string,List<string>> itr in isoNames)
            {
                List<Tuple<string, int>> isoNameBeams = new List<Tuple<string, int>> { };
                for(int i = 0; i < itr.Item2.Count; i++) isoNameBeams.Add(new Tuple<string, int>(itr.Item2.ElementAt(i), numBeams.ElementAt(count).ElementAt(i)));
                planIsoBeamInfo.Add(new Tuple<string, List<Tuple<string, int>>>(itr.Item1, new List<Tuple<string, int>>(isoNameBeams)));
                count++;
            }

            /*
            //AP/PA stuff (THIS NEEDS TO GO AFTER THE ABOVE CHECKS!). Ask the user if they want to split the AP/PA isocenters into two plans if there are two AP/PA isocenters
            bool singleAPPAplan = true;
            if (numIsos - numVMATIsos == 2)
            {
                selectItem SUI = new selectItem();
                SUI.title.Text = "What should I do with the AP/PA isocenters?" + Environment.NewLine + Environment.NewLine + Environment.NewLine + "Put them in:";
                SUI.title.TextAlign = System.Drawing.ContentAlignment.TopCenter;
                SUI.itemCombo.Items.Add("One plan");
                SUI.itemCombo.Items.Add("Separate plans");
                SUI.itemCombo.Text = "One plan";
                SUI.ShowDialog();
                if (!SUI.confirm) return;
                //get the option the user chose from the combobox
                if (SUI.itemCombo.SelectedItem.ToString() == "Separate plans") singleAPPAplan = false;
            }
            */

            //Added code to account for the scenario where the user either requested or did not request to contour the overlap between fields in adjacent isocenters
            placeBeams_CSI place;
            if (contourOverlap_chkbox.IsChecked.Value)
            {
                //ensure the value entered in the added margin text box for contouring field overlap is a valid double
                if (!double.TryParse(contourOverlapTB.Text, out double contourOverlapMargin))
                {
                    MessageBox.Show("Error! The entered added margin for the contour overlap text box is NaN! Please enter a valid number and try again!");
                    return;
                }
                //convert from mm to cm
                contourOverlapMargin *= 10.0;
                //overloaded constructor for the placeBeams class
                place = new placeBeams_CSI(selectedSS, planIsoBeamInfo, collRot, jawPos, chosenLinac, chosenEnergy, calculationModel, optimizationModel, useGPUdose, useGPUoptimization, MRrestartLevel, contourOverlapMargin);
            }
            else place = new placeBeams_CSI(selectedSS, planIsoBeamInfo, collRot, jawPos, chosenLinac, chosenEnergy, calculationModel, optimizationModel, useGPUdose, useGPUoptimization, MRrestartLevel);

            VMATplans = new List<ExternalPlanSetup>(place.generatePlans("VMAT CSI", prescriptions));
            if (!VMATplans.Any()) return;

            //if the user elected to contour the overlap between fields in adjacent isocenters, get this list of structures from the placeBeams class and copy them to the jnxs vector
           if (contourOverlap_chkbox.IsChecked.Value) jnxs = place.jnxs;

            //if the user requested to contour the overlap between fields in adjacent VMAT isocenters, repopulate the optimization tab (will include the newly added field junction structures)!
            if (contourOverlap_chkbox.IsChecked.Value) populateOptimizationTab(opt_parameters);
        }

        //stuff related to optimization setup tab
        private void populateOptimizationTab(StackPanel theSP, List<List<Tuple<string, string, double, double, int>>> tmpList = null, bool checkIfStructurePresentInSS = true, List<string> planIds = null)
        {
            List<List<Tuple<string, string, double, double, int>>> defaultListList = new List<List<Tuple<string, string, double, double, int>>> { };
            if(tmpList == null)
            {
                //tmplist is empty indicating that no optimization constraints were present on the UI when this method was called
                tmpList = new List<List<Tuple<string, string, double, double, int>>> { };
                //no treatment template selected => scale optimization objectives by ratio of entered Rx dose to closest template treatment Rx dose
                autoPlanTemplate selectedTemplate = templateList.SelectedItem as autoPlanTemplate;
                if (selectedTemplate == null) { MessageBox.Show("No template selected!"); return; }
                if (prescriptions != null)
                {
                    if (selectedTemplate.init_constraints.Any()) tmpList.Add(selectedTemplate.init_constraints);
                    if (selectedTemplate.bst_constraints.Any()) tmpList.Add(selectedTemplate.bst_constraints);

                    // double RxDose = prescriptions.Item2.Dose * prescriptions.Item1;
                    //double baseDose = 1.0;
                    //List<Tuple<string, string, double, double, int>> dummy = new List<Tuple<string, string, double, double, int>> { };
                    //use optimization objects of the closer of the two default regiments (6-18-2021)
                    //if (Math.Pow(RxDose - (nonmyeloNumFx * nonmyeloDosePerFx), 2) <= Math.Pow(RxDose - (reduceDoseNumFx * reduceDoseDosePerFx), 2))
                    //{
                    //    dummy = optConstBoost;
                    //    baseDose = nonmyeloDosePerFx * nonmyeloNumFx;
                    //}
                    //else
                    //{
                    //    dummy = optConstNoBoost;
                    //    baseDose = reduceDoseDosePerFx * reduceDoseNumFx;
                    //}
                    //  foreach (Tuple<string, string, double, double, int> opt in dummy) tmp.Add(Tuple.Create(opt.Item1, opt.Item2, opt.Item3 * (RxDose / baseDose), opt.Item4, opt.Item5));
                }
                else
                {
                    MessageBox.Show("Error: Prescription(s) are NOT valid! \nYou must specify the prescription(s) and place beams before adding optimization constraints!");
                    return;
                }
            }

            if(checkIfStructurePresentInSS)
            {
                foreach (List<Tuple<string, string, double, double, int>> itr in tmpList)
                {
                    List<Tuple<string, string, double, double, int>> defaultList = new List<Tuple<string, string, double, double, int>> { };
                    foreach (Tuple<string, string, double, double, int> opt in itr)
                    {
                        if (opt.Item1.Contains("--select--")) defaultList.Add(opt);
                        //always add PTV objectives to optimization objectives list
                        if (opt.Item1.Contains("PTV")) defaultList.Add(opt);
                        //only add template optimization objectives for each structure to default list if that structure is present in the selected structure set and contoured
                        //12-22-2020 coded added to account for the situation where the structure selected for sparing had to be converted to a low resolution structure
                        else if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == (opt.Item1 + "_lowRes").ToLower()) != null && !selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == (opt.Item1 + "_lowRes").ToLower()).IsEmpty) defaultList.Add(Tuple.Create(selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == (opt.Item1 + "_lowRes").ToLower()).Id, opt.Item2, opt.Item3, opt.Item4, opt.Item5));
                        else if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == opt.Item1.ToLower()) != null && !selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == opt.Item1.ToLower()).IsEmpty) defaultList.Add(opt);
                    }
                    defaultListList.Add(new List<Tuple<string, string, double, double, int>>(defaultList));
                }
            }
            else
            {
                foreach (List<Tuple<string, string, double, double, int>> itr in tmpList) defaultListList.Add(new List<Tuple<string, string, double, double, int>>(itr));
            }

            int count = 0;

            if (planIds == null)
            {
                //12/27/2022 this line needs to be fixed as it assumes prescriptions is arranged such that each entry in the list contains a unique plan ID
                foreach (List<Tuple<string, string, double, double, int>> itr in defaultListList) add_opt_volumes(itr, prescriptions.ElementAt(count++).Item1, theSP);
            }
            else
            {
                foreach (List<Tuple<string, string, double, double, int>> itr in defaultListList) add_opt_volumes(itr, planIds.ElementAt(count++), theSP);
            }

            //else
            //{
            //    //No items in the optParameters vector, indicating the user just wants to set/reset the optimization parameters. 
            //    //In this case, just search through the structure set to see if any of the contoured structure IDs match the structures in the optimization parameter templates
            //    if (selectedSS.Structures.Where(x => x.Id.ToLower().Contains("ptv")).Any())
            //    {
            //        foreach (Tuple<string, string, double, double, int> opt in tmp)
            //        {
            //            if (opt.Item1.Contains("PTV")) defaultList.Add(opt);
            //            else if (selectedSS.Structures.Where(x => x.Id.ToLower().Contains(opt.Item1.ToLower())).Any())
            //            {
            //                //12-22-2020 coded added to account for the situation where the structure selected for sparing had to be previously converted to a low resolution structure using this script
            //                if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == (opt.Item1 + "_lowRes").ToLower()) != null && !selectedSS.Structures.First(x => x.Id.ToLower() == (opt.Item1 + "_lowRes").ToLower()).IsEmpty) defaultList.Add(Tuple.Create(selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == (opt.Item1 + "_lowRes").ToLower()).Id, opt.Item2, opt.Item3, opt.Item4, opt.Item5));
            //                else if (!selectedSS.Structures.First(x => x.Id.ToLower() == opt.Item1.ToLower()).IsEmpty) defaultList.Add(Tuple.Create(selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == opt.Item1.ToLower()).Id, opt.Item2, opt.Item3, opt.Item4, opt.Item5));
            //            }
            //        }
            //    }
            //    else
            //    {
            //        MessageBox.Show("Warning! No PTV structures in the selected structure set! Add a PTV structure and try again!");
            //        return;
            //    }
            //}

            //check if the user requested to contour the overlap between fields in adjacent isocenters and also check if there are any structures in the junction structure stack (jnxs)
            //if (contourOverlap_chkbox.IsChecked.Value || jnxs.Any())
            //{
            //    //we want to insert the optimization constraints for these junction structure right after the ptv constraints, so find the last index of the target ptv structure and insert
            //    //the junction structure constraints directly after the target structure constraints
            //    int index = defaultList.FindLastIndex(x => x.Item1.ToLower().Contains("ptv"));
            //    foreach (Structure s in jnxs)
            //    {
            //        //per Nataliya's instructions, add both a lower and upper constraint to the junction volumes. Make the constraints match those of the ptv target
            //        defaultList.Insert(++index, new Tuple<string, string, double, double, int>(s.Id, "Lower", prescriptions.Item2.Dose * prescriptions.Item1, 100.0, 100));
            //        defaultList.Insert(++index, new Tuple<string, string, double, double, int>(s.Id, "Upper", prescriptions.Item2.Dose * prescriptions.Item1 * 1.01, 0.0, 100));
            //    }
            //}

            //clear the current list of optimization objectives
            // clear_optimization_parameter_list();
            //add the default list of optimization objectives to the displayed list of optimization objectives
            // add_opt_volumes(defaultList);
        }

        private void scanSS_Click(object sender, RoutedEventArgs e)
        {
            //get prescription
            //if (double.TryParse(initDosePerFxTB.Text, out double dose_perFx) && int.TryParse(initNumFxTB.Text, out int numFractions)) prescriptions = Tuple.Create(numFractions, new DoseValue(dose_perFx, DoseValue.DoseUnit.cGy));
            //else
            //{
            //    MessageBox.Show("Warning! Entered prescription is not valid! \nSetting number of fractions to 1 and dose per fraction to 0.1 cGy/fraction!");
            //  //  prescriptions = Tuple.Create(1, new DoseValue(0.1, DoseValue.DoseUnit.cGy));
            //}
            //if (selectedSS.Structures.Where(x => x.Id.ToLower().Contains("ts_jnx")).Any()) jnxs = selectedSS.Structures.Where(x => x.Id.ToLower().Contains("ts_jnx")).ToList();
            if(prescriptions.Any())
            {
                clear_optimization_parameter_list(opt_parameters);
                populateOptimizationTab(opt_parameters);
            }
            else MessageBox.Show("Error: Prescription(s) are NOT valid! \nYou must specify the prescription(s) and place beams before adding optimization constraints!");
        }

        private void setOptConst_Click(object sender, RoutedEventArgs e)
        {
            UIhelper helper = new UIhelper();
            List<Tuple<string,List<Tuple<string, string, double, double, int>>>> optParametersListList = helper.parseOptConstraints(opt_parameters);
            if (!optParametersListList.Any()) return;
            bool constraintsAssigned = false;
            foreach(Tuple<string,List<Tuple<string,string,double,double,int>>> itr in optParametersListList)
            {
                ExternalPlanSetup plan = VMATplans.FirstOrDefault(x => x.Id == itr.Item1);
                if(plan != null)
                {
                    if(plan.OptimizationSetup.Objectives.Count() > 0)
                    {
                        foreach (OptimizationObjective o in plan.OptimizationSetup.Objectives) plan.OptimizationSetup.RemoveObjective(o);
                    }
                    helper.assignOptConstraints(itr.Item2, plan, true, 0.0);
                    constraintsAssigned = true;
                }
            }
            if(constraintsAssigned)
            {
                string message = "Optimization objectives have been successfully set!" + Environment.NewLine + Environment.NewLine + "Please review the generated structures, placed isocenters, placed beams, and optimization parameters!";
                MessageBox.Show(message);
                isModified = true;
            }
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
            //if (optParametersList.Where(x => x.Item1.ToLower().Contains("_lowres")).Any()) message += "\n\nBE SURE TO VERIFY THE ACCURACY OF THE GENERATED LOW-RESOLUTION CONTOURS!";
            //if (numIsos != 0 && numIsos != numVMATIsos)
            //{
            //    //VMAT only TBI plan was created with the script in this instance info or the user wants to only set the optimization constraints
            //    message += "\n\nFor the AP/PA Legs plan, be sure to change the orientation from head-first supine to feet-first supine!";
            //}
            //}
            //autoSave = true;
        }

        private void add_constraint_Click(object sender, RoutedEventArgs e)
        {
            Button theBtn = sender as Button;
            StackPanel theSP;
            if (theBtn.Name.Contains("template")) theSP = templateOptParams_sp;
            else theSP = opt_parameters;
            if (!prescriptions.Any()) return;
            ExternalPlanSetup thePlan = null;
            if (!VMATplans.Any()) return;
            if (VMATplans.Count > 0)
            {
                selectItem SUI = new selectItem();
                SUI.title.Text = "Please selct a plan to add a constraint!";
                foreach (ExternalPlanSetup itr in VMATplans) SUI.itemCombo.Items.Add(itr.Id);
                SUI.itemCombo.Items.Add("Both");
                SUI.itemCombo.SelectedIndex = 0;
                SUI.ShowDialog();
                if (SUI.confirm) thePlan = VMATplans.FirstOrDefault(x => x.Id == SUI.itemCombo.SelectedItem.ToString());
                else return;
                if (thePlan == null) { MessageBox.Show("Plan not found! Exiting!"); return; }
            }
            else thePlan = VMATplans.First();
            int index = prescriptions.IndexOf(prescriptions.FirstOrDefault(x => x.Item1 == thePlan.Id));
            if(index != -1)
            {
                List<List<Tuple<string, string, double, double, int>>> tmpList = new List<List<Tuple<string, string, double, double, int>>> { };
                List<Tuple<string, string, double, double, int>> tmp = new List<Tuple<string, string, double, double, int>> { };
                if (opt_parameters.Children.Count > 0)
                {
                    //read list of current objectives
                    UIhelper helper = new UIhelper();
                    List<Tuple<string, List<Tuple<string, string, double, double, int>>>> optParametersListList = helper.parseOptConstraints(theSP, false);
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
                    autoPlanTemplate selectedTemplate = templateList.SelectedItem as autoPlanTemplate;
                    if(selectedTemplate != null)
                    {
                        if(index == 0)
                        {
                            if (selectedTemplate.init_constraints.Any()) tmp = new List<Tuple<string,string,double,double,int>>(selectedTemplate.init_constraints);
                            tmp.Add(Tuple.Create("--select--", "--select--", 0.0, 0.0, 0));
                            tmpList.Add(tmp);
                            if (selectedTemplate.bst_constraints.Any()) tmpList.Add(selectedTemplate.bst_constraints);
                        }
                        else
                        {
                            if (selectedTemplate.init_constraints.Any()) tmpList.Add(selectedTemplate.init_constraints);
                            else tmpList.Add(new List<Tuple<string, string, double, double, int>> { Tuple.Create("--select--", "--select--", 0.0, 0.0, 0) });

                            if (selectedTemplate.bst_constraints.Any()) tmp = new List<Tuple<string, string, double, double, int>>(selectedTemplate.bst_constraints);
                            tmp.Add(Tuple.Create("--select--", "--select--", 0.0, 0.0, 0));
                            tmpList.Add(tmp);
                        }
                    }
                }
                clear_optimization_parameter_list(theSP);
                populateOptimizationTab(theSP, tmpList);
            }
        }

        private void add_opt_header(StackPanel theSP)
        {
            theSP.Children.Add(new UIhelper().getOptHeader(structures_sp.Width));
        }

        private void add_opt_volumes(List<Tuple<string, string, double, double, int>> defaultList, string planId, StackPanel theSP)
        {
            if (selectedSS == null) { MessageBox.Show("Error! The structure set has not been assigned! Choose a structure set and try again!"); return; }
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
            UIhelper helper = new UIhelper();
            theSP.Children.Add(helper.AddPlanIdtoOptList(theSP, planId));
            add_opt_header(theSP);
            for (int i = 0; i < defaultList.Count; i++)
            {
                counter++;
                theSP.Children.Add(helper.addOptVolume(theSP, selectedSS, defaultList[i], clearBtnNamePrefix, counter, new RoutedEventHandler(this.clearOptStructBtn_click), theSP.Name.Contains("template") ? true : false));
            }
        }

        private void clear_optParams_Click(object sender, RoutedEventArgs e)
        {
            StackPanel theSP;
            if ((sender as Button).Name.Contains("template")) theSP = templateOptParams_sp;
            else theSP = opt_parameters;
            clear_optimization_parameter_list(theSP);
        }

        private void clearOptStructBtn_click(object sender, EventArgs e)
        {
            StackPanel theSP;
            if ((sender as Button).Name.Contains("template")) theSP = templateOptParams_sp;
            else theSP = opt_parameters;
            if (new UIhelper().clearRow(sender, theSP)) clear_optimization_parameter_list(theSP);
        }

        private void clear_optimization_parameter_list(StackPanel theSP)
        {
            theSP.Children.Clear();
            if (theSP.Name.Contains("template")) clearTemplateOptBtnCounter = 0;
            else clearOptBtnCounter = 0;
        }

        //stuff related to template builder
        private void templates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            autoPlanTemplate selectedTemplate = templateList.SelectedItem as autoPlanTemplate;
            if (selectedTemplate == null) return;
            initDosePerFxTB.Text = "";
            initNumFxTB.Text = "";
            boostDosePerFxTB.Text = "";
            boostNumFxTB.Text = "";
            if (selectedTemplate.templateName != "--select--")
            {
                setInitPresciptionInfo(selectedTemplate.initialRxDosePerFx, selectedTemplate.initialRxNumFx);
                if (selectedTemplate.boostRxDosePerFx != 0.1 && selectedTemplate.boostRxNumFx != 1) setBoostPrescriptionInfo(selectedTemplate.boostRxDosePerFx, selectedTemplate.boostRxNumFx);
            }
            else
            {
                templateList.UnselectAll();
            }
        }

        bool waitToUpdate = false;
        private void setInitPresciptionInfo(double dose_perFx, int num_Fx)
        {
            if (initDosePerFxTB.Text != dose_perFx.ToString() && initNumFxTB.Text != num_Fx.ToString()) waitToUpdate = true;
            initDosePerFxTB.Text = dose_perFx.ToString();
            initNumFxTB.Text = num_Fx.ToString();
        }

        bool boostWaitToUpdate = false;
        private void setBoostPrescriptionInfo(double dose_perFx, int num_Fx)
        {
            if (boostDosePerFxTB.Text != dose_perFx.ToString() && boostNumFxTB.Text != num_Fx.ToString()) boostWaitToUpdate = true;
            boostDosePerFxTB.Text = dose_perFx.ToString();
            boostNumFxTB.Text = num_Fx.ToString();
        }

        private void initNumFx_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!int.TryParse(initNumFxTB.Text, out int newNumFx)) initRxTB.Text = "";
            else if (newNumFx < 1)
            {
                MessageBox.Show("Error! The number of fractions must be non-negative integer and greater than zero!");
                initRxTB.Text = "";
            }
            else resetInitRxDose();
        }

        private void initDosePerFx_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!double.TryParse(initDosePerFxTB.Text, out double newDoseFx)) initRxTB.Text = "";
            else if (newDoseFx <= 0)
            {
                MessageBox.Show("Error! The dose per fraction must be a number and non-negative!");
                initRxTB.Text = "";
            }
            else resetInitRxDose();
        }

        private void resetInitRxDose()
        {
            if (waitToUpdate) waitToUpdate = false;
            else if (int.TryParse(initNumFxTB.Text, out int newNumFx) && double.TryParse(initDosePerFxTB.Text, out double newDoseFx))
            {
                initRxTB.Text = (newNumFx * newDoseFx).ToString();
                autoPlanTemplate selectedTemplate = templateList.SelectedItem as autoPlanTemplate;
                if (selectedTemplate != null)
                {
                    //verify that the entered dose/fx and num fx agree with those stored in the template, otherwise unselect the template
                    if (newNumFx != selectedTemplate.initialRxNumFx || newDoseFx != selectedTemplate.initialRxDosePerFx) templateList.UnselectAll();
                }
            }
        }

        private void BoostDosePerFx_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!double.TryParse(boostDosePerFxTB.Text, out double newDoseFx)) boostRxTB.Text = "";
            else if (newDoseFx <= 0)
            {
                MessageBox.Show("Error! The dose per fraction must be a number and non-negative!");
                initRxTB.Text = "";
            }
            else resetBoostRxDose();
        }

        private void BoostNumFx_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!int.TryParse(boostNumFxTB.Text, out int newNumFx)) boostRxTB.Text = "";
            else if (newNumFx < 1)
            {
                MessageBox.Show("Error! The number of fractions must be non-negative integer and greater than zero!");
                initRxTB.Text = "";
            }
            else resetBoostRxDose();
        }

        private void resetBoostRxDose()
        {
            if (boostWaitToUpdate) boostWaitToUpdate = false;
            else if (int.TryParse(boostNumFxTB.Text, out int newNumFx) && double.TryParse(boostDosePerFxTB.Text, out double newDoseFx))
            {
                boostRxTB.Text = (newNumFx * newDoseFx).ToString();
                autoPlanTemplate selectedTemplate = templateList.SelectedItem as autoPlanTemplate;
                if (selectedTemplate != null)
                {
                    //verify that the entered dose/fx and num fx agree with those stored in the template, otherwise unselect the template
                    if (newNumFx != selectedTemplate.boostRxNumFx || newDoseFx != selectedTemplate.boostRxDosePerFx) templateList.UnselectAll();
                }
            }
        }

        private void templateDosePerFx_TextChanged(object sender, TextChangedEventArgs e)
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
                MessageBox.Show("Error! The dose per fraction must be a number and non-negative!");
                planRxTB.Text = "";
            }
            else resetTemplateRxDose(dosePerFxTB, numFxTB, planRxTB);
        }

        private void templateNumFx_TextChanged(object sender, TextChangedEventArgs e)
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
                MessageBox.Show("Error! The number of fractions must be an integer and greater than 0!");
                planRxTB.Text = "";
            }
            else resetTemplateRxDose(dosePerFxTB, numFxTB, planRxTB);
        }

        private void resetTemplateRxDose(TextBox dosePerFxTB, TextBox numFxTB, TextBox RxTB)
        {
            if (int.TryParse(numFxTB.Text, out int newNumFx) && double.TryParse(dosePerFxTB.Text, out double newDoseFx)) RxTB.Text = (newNumFx * newDoseFx).ToString();
        }

        private void templateBuildOptionCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //12/26/2022
            //need to implement Optimization Constraints, functionality
            if (selectedSS == null) { MessageBox.Show("Error! The structure set has not been assigned! Choose a structure set and try again!"); return; }
            if (templateBuildOptionCB.SelectedItem.ToString().ToLower() == "existing template")
            {
                autoPlanTemplate theTemplate = null;
                selectItem SUI = new selectItem();
                SUI.title.Text = "Please select and existing template!";
                foreach (autoPlanTemplate itr in PlanTemplates) SUI.itemCombo.Items.Add(itr.templateName);
                SUI.itemCombo.SelectedIndex = 0;
                SUI.ShowDialog();
                if (SUI.confirm) theTemplate = PlanTemplates.FirstOrDefault(x => x.templateName == SUI.itemCombo.SelectedItem.ToString());
                else return;
                if (theTemplate == null) { MessageBox.Show("Template not found! Exiting!"); return; }

                //set name
                templateNameTB.Text = theTemplate.templateName + "_1";

                //setRx
                templateInitPlanDosePerFxTB.Text = theTemplate.initialRxDosePerFx.ToString();
                templateInitPlanNumFxTB.Text = theTemplate.initialRxNumFx.ToString();
                if (theTemplate.boostRxDosePerFx > 0.1)
                {
                    templateBstPlanDosePerFxTB.Text = theTemplate.boostRxDosePerFx.ToString();
                    templateBstPlanNumFxTB.Text = theTemplate.boostRxNumFx.ToString();
                }

                //add targets
                List<Tuple<string, double, string>> targetList = new List<Tuple<string, double, string>>(theTemplate.targets);
                clear_targets_list(templateClearTargetList);
                add_target_volumes(targetList, targetTemplate_sp);

                //add default TS structures
                clearTemplateTSList();
                if (theTemplate.TS_structures.Any()) add_templateTS_volumes(theTemplate.TS_structures);

                //add default sparing structures
                clear_spare_list(templateClearSpareStructuresBtn);
                if (theTemplate.spareStructures.Any()) add_sp_volumes(theTemplate.spareStructures, templateStructures_sp);

                //add optimization constraints
                List<List<Tuple<string, string, double, double, int>>> tmpList = new List<List<Tuple<string, string, double, double, int>>> { };
                //no treatment template selected => scale optimization objectives by ratio of entered Rx dose to closest template treatment Rx dose
                if (theTemplate.init_constraints.Any()) tmpList.Add(theTemplate.init_constraints);
                if (theTemplate.bst_constraints.Any()) tmpList.Add(theTemplate.bst_constraints);

                //sort targets based on cumulative Rx
                targetList.Sort(delegate (Tuple<string, double, string> x, Tuple<string, double, string> y) { return x.Item2.CompareTo(y.Item2); });
                List<string> planIds = new List<string> { };
                //assumes only two plan ids in template targets
                planIds.Add(targetList.First().Item3);
                planIds.Add(targetList.Last().Item3);
                populateOptimizationTab(templateOptParams_sp, tmpList, false, planIds);
            }
            else if(templateBuildOptionCB.SelectedItem.ToString().ToLower() == "current parameters")
            {
                //add targets (checked first to ensure the user has actually input some parameters into the UI before trying to make a template based on the current settings)
                List<Tuple<string, double, string>> targetList = new List<Tuple<string, double, string>>(parseTargets(targets_sp).OrderBy(x => x.Item2));
                if (!targetList.Any()) { MessageBox.Show("Error! Enter parameters into the UI before trying to use them to make a new plan template!"); return; }
                clear_targets_list(templateClearTargetList);
                add_target_volumes(targetList, targetTemplate_sp);

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

                autoPlanTemplate selectedTemplate = templateList.SelectedItem as autoPlanTemplate;
                //add default TS structures
                clearTemplateTSList();
                if(selectedTemplate != null) add_templateTS_volumes(selectedTemplate.TS_structures);

                //add default sparing structures
                UIhelper helper = new UIhelper();
                clear_spare_list(templateClearSpareStructuresBtn);
                List<Tuple<string, string, double>> spareStructs = new List<Tuple<string, string, double>>(helper.parseSpareStructList(structures_sp));
                if (spareStructs.Any()) add_sp_volumes(spareStructs, templateStructures_sp);

                //add optimization constraints
                List<List<Tuple<string, string, double, double, int>>> tmpList = new List<List<Tuple<string, string, double, double, int>>> { };
                List<Tuple<string, List<Tuple<string, string, double, double, int>>>> optParametersListList = helper.parseOptConstraints(opt_parameters);
                foreach (Tuple<string, List<Tuple<string, string, double, double, int>>> itr in optParametersListList) tmpList.Add(itr.Item2);

                //sort targets based on cumulative Rx
                targetList.Sort(delegate (Tuple<string, double, string> x, Tuple<string, double, string> y) { return x.Item2.CompareTo(y.Item2); });
                List<string> planIds = new List<string> { };
                //assumes only two plan ids in template targets
                planIds.Add(targetList.First().Item3);
                planIds.Add(targetList.Last().Item3);

                populateOptimizationTab(templateOptParams_sp, tmpList, false, planIds);
            }
        }

        private void TsGenerateVsManipulateInfo_Click(object sender, RoutedEventArgs e)
        {
            string message = "What's the difference between TS structure generation vs manipulation?" + Environment.NewLine;
            message += String.Format("TS structure generation involves adding structures to the structure set to shape the dose distribution. These include rings and substructures. E.g.,") + Environment.NewLine;
            message += String.Format("TS_ring900  -->  ring structure around the targets using a nominal dose level of 900 cGy to determine fallofff") + Environment.NewLine;
            message += String.Format("Kidneys-1cm  -->  substructure for the Kidneys volume where the Kidneys are contracted by 1 cm") + Environment.NewLine + Environment.NewLine;
            message += String.Format("TS structure manipulation involves manipulating/modifying the structure itself or target structures. E.g.,") + Environment.NewLine;
            message += String.Format("(Ovaries, Crop from target, 1.5cm)  -->  modify the target structure such that the ovaries structure is cropped from the target with a 1.5 cm margin") + Environment.NewLine;
            message += String.Format("(Brainstem, Contour overlap, 0.0 cm)  -->  Identify the overlapping regions between the brainstem and target structure(s) and contour them as new structures") + Environment.NewLine + Environment.NewLine;
            MessageBox.Show(message);
        }

        private void add_templateTS_Click(object sender, RoutedEventArgs e)
        {
            //populate the comboboxes
            add_templateTS_volumes(new List<Tuple<string, string>> { Tuple.Create("--select--", "--select--") });
            templateTSScroller.ScrollToBottom();
        }

        private void add_templateTS_volumes(List<Tuple<string, string>> defaultList)
        {
            if (selectedSS == null) { MessageBox.Show("Error! Please select a Structure Set before add sparing volumes!"); return; }
            helpers.templateBuilder builder = new helpers.templateBuilder();
            if (templateTS_sp.Children.Count == 0) templateTS_sp.Children.Add(builder.addTemplateTSHeader(templateTS_sp));
            int counter = 0;
            UIhelper helper = new UIhelper();
            for (int i = 0; i < defaultList.Count; i++)
            {
                counter++;
                templateTS_sp.Children.Add(builder.addTemplateTSVolume(templateTS_sp, selectedSS, defaultList[i], "templateClearTSStructuresBtn", counter, new RoutedEventHandler(this.clearTSStructureBtn))) ;
            }
        }
        
        private void clearTSStructureBtn(object sender, RoutedEventArgs e)
        {
            if (new UIhelper().clearRow(sender, templateTS_sp)) clearTemplateTSList();
        }

        private void clear_templateTS_Click(object sender, RoutedEventArgs e)
        {
            clearTemplateTSList();
        }

        private void clearTemplateTSList()
        {
            templateTS_sp.Children.Clear();
        }

        private void generateTemplatePreview_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSS == null) { MessageBox.Show("Error! Please select a Structure Set before add sparing volumes!"); return; }
            prospectiveTemplate = new autoPlanTemplate();
            prospectiveTemplate.templateName = templateNameTB.Text;

            if (!double.TryParse(templateInitPlanDosePerFxTB.Text, out prospectiveTemplate.initialRxDosePerFx)) {MessageBox.Show("Error! Initial plan dose per fx not parsed successfully! Fix and try again!"); return; }
            if (!int.TryParse(templateInitPlanNumFxTB.Text, out prospectiveTemplate.initialRxNumFx)) {MessageBox.Show("Error! Initial plan dose per fx not parsed successfully! Fix and try again!"); return; }
            if (!double.TryParse(templateBstPlanDosePerFxTB.Text, out prospectiveTemplate.boostRxDosePerFx)) { MessageBox.Show("Error! Initial plan dose per fx not parsed successfully! Fix and try again!"); return; }
            if (!int.TryParse(templateBstPlanNumFxTB.Text, out prospectiveTemplate.boostRxNumFx)) {MessageBox.Show("Error! Initial plan dose per fx not parsed successfully! Fix and try again!"); return; }

            UIhelper helper = new UIhelper();
            helpers.templateBuilder builder = new helpers.templateBuilder();
            prospectiveTemplate.targets = new List<Tuple<string, double, string>>(parseTargets(targetTemplate_sp).OrderBy(x => x.Item2));
            prospectiveTemplate.TS_structures = new List<Tuple<string, string>>(builder.parseTSStructureList(templateTS_sp));
            prospectiveTemplate.spareStructures = new List<Tuple<string,string,double>>(helper.parseSpareStructList(templateStructures_sp));
            List<Tuple<string, List<Tuple<string, string, double, double, int>>>> templateOptParametersListList = helper.parseOptConstraints(templateOptParams_sp);
            prospectiveTemplate.init_constraints = new List<Tuple<string, string, double, double, int>>(templateOptParametersListList.First().Item2);
            prospectiveTemplate.bst_constraints = new List<Tuple<string, string, double, double, int>>(templateOptParametersListList.Last().Item2);

            templatePreviewTB.Text = builder.generateTemplatePreviewText(prospectiveTemplate);
            templatePreviewScroller.ScrollToTop();
        }

        private void serializeNewTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSS == null) { MessageBox.Show("Error! Please select a Structure Set before add sparing volumes!"); return; }
            if (prospectiveTemplate == null) { MessageBox.Show("Error! Please preview the requested template before building!"); return; }
            string fileName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\templates\\CSI_" + prospectiveTemplate.templateName + ".ini";
            if (File.Exists(fileName))
            {
                confirmUI CUI = new confirmUI();
                CUI.message.Text = "Warning! The requested template file already exists! Overwrite?";
                CUI.confirmBTN.Text = "Yes";
                CUI.cancelBTN.Text = "No";
                CUI.ShowDialog();
                if (!CUI.confirm) return;
                if(PlanTemplates.FirstOrDefault(x => x.templateName == prospectiveTemplate.templateName) != null)
                {
                    int index = PlanTemplates.IndexOf(PlanTemplates.FirstOrDefault(x => x.templateName == prospectiveTemplate.templateName));
                    PlanTemplates.RemoveAt(index);
                }
            }

            helpers.templateBuilder builder = new helpers.templateBuilder();
            File.WriteAllText(fileName, builder.generateSerializedTemplate(prospectiveTemplate));
            PlanTemplates.Add(prospectiveTemplate);
            displayConfigurationParameters();
            templateList.ScrollIntoView(prospectiveTemplate);

            templatePreviewTB.Text += String.Format("New template written to: {0}", fileName) + Environment.NewLine;
            templatePreviewScroller.ScrollToBottom();
        }

        //methods related to plan preparation
        private void generateShiftNote_Click(object sender, RoutedEventArgs e)
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
                    MessageBox.Show("VMAT TBI course not found! Exiting!");
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
                        selectItem SUI = new selectItem();
                        SUI.title.Text = "Multiple plans found in VMAT TBI course!" + Environment.NewLine + "Please select a plan to prep!";
                        foreach (ExternalPlanSetup p in plans) SUI.itemCombo.Items.Add(p.Id);
                        SUI.itemCombo.Text = c.ExternalPlanSetups.First().Id;
                        SUI.ShowDialog();
                        if (!SUI.confirm) return;
                        //get the plan the user chose from the combobox
                        vmatPlan = c.ExternalPlanSetups.FirstOrDefault(x => x.Id == SUI.itemCombo.SelectedItem.ToString());
                    }
                    else
                    {
                        //course found and only one or fewer plans inside course with Id != "_Legs", get vmat and ap/pa plans
                        vmatPlan = c.ExternalPlanSetups.FirstOrDefault(x => x.Id.ToLower() == "_vmat tbi");
                    }
                    if (vmatPlan == null)
                    {
                        //vmat plan not found. Dealbreaker, exit method
                        MessageBox.Show("VMAT plan not found! Exiting!");
                        return;
                    }
                }

                //create an instance of the planPep class and pass it the vmatPlan and appaPlan objects as arguments. Get the shift note for the plan of interest
                prep = new planPrep_CSI(vmatPlan, appaPlan);
            }
            if (prep.getShiftNote()) return;

            //let the user know this step has been completed (they can now do the other steps like separate plans and calculate dose)
            shiftTB.Background = System.Windows.Media.Brushes.ForestGreen;
            shiftTB.Text = "YES";
        }

        private void separatePlans_Click(object sender, RoutedEventArgs e)
        {
            //The shift note has to be retrieved first! Otherwise, we don't have instances of the plan objects
            if (shiftTB.Text != "YES")
            {
                MessageBox.Show("Please generate the shift note before separating the plans!");
                return;
            }

            //separate the plans
            pi.BeginModifications();
            if (prep.separate()) return;

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
        }

        private void calcDose_Click(object sender, RoutedEventArgs e)
        {
            //the shift note must be retireved and the plans must be separated before calculating dose
            if (shiftTB.Text == "NO" || separateTB.Text == "NO")
            {
                MessageBox.Show("Error! \nYou must generate the shift note AND separate the plan before calculating dose to each plan!");
                return;
            }

            //ask the user if they are sure they want to do this. Each plan will calculate dose sequentially, which will take time
            confirmUI CUI = new confirmUI();
            CUI.message.Text = "Warning!" + Environment.NewLine + "This will take some time as each plan needs to be calculated sequentionally!" + Environment.NewLine + "Continue?!";
            CUI.ShowDialog();
            if (!CUI.confirm) return;

            //let the user know the script is working
            calcDoseTB.Background = System.Windows.Media.Brushes.Yellow;
            calcDoseTB.Text = "WORKING";

            prep.calculateDose();

            //let the user know this step has been completed
            calcDoseTB.Background = System.Windows.Media.Brushes.ForestGreen;
            calcDoseTB.Text = "YES";
        }

        private void planSum_Click(object sender, RoutedEventArgs e)
        {
            //do nothing. Eclipse v15.6 doesn't have this capability, but v16 and later does. This method is a placeholder (the planSum button exists in the UI.xaml file, but its visibility is set to 'hidden')
        }

        //stuff related to script configuration
        private void loadNewConfigFile_Click(object sender, RoutedEventArgs e)
        {
            //load a configuration file different from the default in the executing assembly folder
            configFile = "";
            //PlanTemplates.Clear();
            //PlanTemplates = new ObservableCollection<autoPlanTemplate> { new autoPlanTemplate("--select--") };
            //PlanTemplates.Add(new autoPlanTemplate("--select--"));
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\configuration\\";
            openFileDialog.Filter = "ini files (*.ini)|*.ini|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog().Value) { if (!loadConfigurationSettings(openFileDialog.FileName)) displayConfigurationParameters(); else MessageBox.Show("Error! Selected file is NOT valid!"); }
        }

        //parse the relevant data in the configuration file
        private bool loadConfigurationSettings(string file)
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
                    List<Tuple<string, string, double>> defaultSpareStruct_temp = new List<Tuple<string, string, double>> { };
                    List<Tuple<string, string>> defaultTSstructures_temp = new List<Tuple<string, string>> { };

                    while ((line = reader.ReadLine()) != null)
                    {
                        //start actually reading the data when you find the begin plugin configuration tag
                        if (line.Equals(":begin plugin configuration:"))
                        {
                            while (!(line = reader.ReadLine()).Equals(":end plugin configuration:"))
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
                                        else if (parameter == "img export location")
                                        {
                                            if (Directory.Exists(value))
                                            {
                                                imgExportPath = value;
                                                if (imgExportPath.LastIndexOf("\\") != imgExportPath.Length - 1) imgExportPath += "\\";
                                            }
                                            else MessageBox.Show(String.Format("Warning! {0} does NOT exist!", value));
                                        }
                                        else if (parameter == "beams per iso")
                                        {
                                            //parse the default requested number of beams per isocenter
                                            line = cropLine(line, "{");
                                            List<int> b = new List<int> { };
                                            //second character should not be the end brace (indicates the last element in the array)
                                            while (line.Substring(1, 1) != "}")
                                            {
                                                b.Add(int.Parse(line.Substring(0, line.IndexOf(","))));
                                                line = cropLine(line, ",");
                                            }
                                            b.Add(int.Parse(line.Substring(0, line.IndexOf("}"))));
                                            //only override the requested number of beams in the beamsPerIso array  
                                            for (int i = 0; i < b.Count(); i++) { if (i < beamsPerIso.Count()) beamsPerIso[i] = b.ElementAt(i); }
                                        }
                                        else if (parameter == "collimator rotations")
                                        {
                                            //parse the default requested number of beams per isocenter
                                            line = cropLine(line, "{");
                                            List<double> c = new List<double> { };
                                            //second character should not be the end brace (indicates the last element in the array)
                                            while (line.Contains(","))
                                            {
                                                c.Add(double.Parse(line.Substring(0, line.IndexOf(","))));
                                                line = cropLine(line, ",");
                                            }
                                            c.Add(double.Parse(line.Substring(0, line.IndexOf("}"))));
                                            for (int i = 0; i < c.Count(); i++) { if (i < 5) collRot[i] = c.ElementAt(i); }
                                        }
                                        else if (parameter == "img export format") { if (value == "dcm" || value == "png") imgExportFormat = value; else MessageBox.Show("Only png and dcm image formats are supported for export!"); }
                                        else if (parameter == "use GPU for dose calculation") useGPUdose = value;
                                        else if (parameter == "use GPU for optimization") useGPUoptimization = value;
                                        else if (parameter == "MR level restart") MRrestartLevel = value;
                                        //other parameters that should be updated
                                        else if (parameter == "calculation model") { if (value != "") calculationModel = value; }
                                        else if (parameter == "optimization model") { if (value != "") optimizationModel = value; }
                                        else if (parameter == "contour field overlap") { if (value != "") contourOverlap = bool.Parse(value); }
                                        else if (parameter == "contour field overlap margin") { if (value != "") contourFieldOverlapMargin = value; }
                                    }
                                    else if (line.Contains("add default sparing structure")) defaultSpareStruct_temp.Add(parseSparingStructure(line));
                                    else if (line.Contains("add default TS")) defaultTSstructures_temp.Add(parseTS(line));
                                    else if (line.Contains("add linac"))
                                    {
                                        //parse the linacs that should be added. One entry per line
                                        line = cropLine(line, "{");
                                        linac_temp.Add(line.Substring(0, line.IndexOf("}")));
                                    }
                                    else if (line.Contains("add beam energy"))
                                    {
                                        //parse the photon energies that should be added. One entry per line
                                        line = cropLine(line, "{");
                                        energy_temp.Add(line.Substring(0, line.IndexOf("}")));
                                    }
                                    else if (line.Contains("add jaw position"))
                                    {
                                        //parse the default requested number of beams per isocenter
                                        line = cropLine(line, "{");
                                        List<double> tmp = new List<double> { };
                                        //second character should not be the end brace (indicates the last element in the array)
                                        while (line.Contains(","))
                                        {
                                            tmp.Add(double.Parse(line.Substring(0, line.IndexOf(","))));
                                            line = cropLine(line, ",");
                                        }
                                        tmp.Add(double.Parse(line.Substring(0, line.IndexOf("}"))));
                                        if (tmp.Count != 4) MessageBox.Show("Error! Jaw positions not defined correctly!");
                                        else jawPos_temp.Add(new VRect<double>(tmp.ElementAt(0), tmp.ElementAt(1), tmp.ElementAt(2), tmp.ElementAt(3)));
                                    }
                                }
                            }
                        }
                    }
                    reader.Close();
                    //anything that is an array needs to be updated AFTER the while loop.
                    if (linac_temp.Any()) linacs = new List<string>(linac_temp);
                    if (energy_temp.Any()) beamEnergies = new List<string>(energy_temp);
                    if (jawPos_temp.Any() && jawPos_temp.Count == 4) jawPos = new List<VRect<double>>(jawPos_temp);
                    if (defaultSpareStruct_temp.Any()) defaultSpareStruct = new List<Tuple<string, string, double>>(defaultSpareStruct_temp);
                    if (defaultTSstructures_temp.Any()) defaultTS_structures = new List<Tuple<string, string>>(defaultTSstructures_temp);
                }
                foreach (string itr in Directory.GetFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\templates\\", "*.ini").OrderBy(x => x)) readTemplatePlan(itr);
                return false;
            }
            //let the user know if the data parsing failed
            catch (Exception e) { MessageBox.Show(String.Format("Error could not load configuration file because: {0}\n\nAssuming default parameters", e.Message)); return true; }
        }

        private void readTemplatePlan(string file)
        {
            using (StreamReader reader = new StreamReader(file))
            {
                int templateCount = 1;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrEmpty(line) && line.Substring(0, 1) != "%")
                    {
                        if (line.Equals(":begin template case configuration:"))
                        {
                            autoPlanTemplate tempTemplate = new autoPlanTemplate(templateCount);
                            List<Tuple<string, string, double>> spareStruct_temp = new List<Tuple<string, string, double>> { };
                            List<Tuple<string, string>> TSstructures_temp = new List<Tuple<string, string>> { };
                            List<Tuple<string, string, double, double, int>> initOptConst_temp = new List<Tuple<string, string, double, double, int>> { };
                            List<Tuple<string, string, double, double, int>> bstOptConst_temp = new List<Tuple<string, string, double, double, int>> { };
                            List<Tuple<string, double, string>> targets_temp = new List<Tuple<string, double, string>> { };
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
                                }
                            }

                            tempTemplate.spareStructures = new List<Tuple<string, string, double>>(spareStruct_temp);
                            tempTemplate.TS_structures = new List<Tuple<string, string>>(TSstructures_temp);
                            tempTemplate.init_constraints = new List<Tuple<string, string, double, double, int>>(initOptConst_temp);
                            tempTemplate.bst_constraints = new List<Tuple<string, string, double, double, int>>(bstOptConst_temp);
                            tempTemplate.targets = new List<Tuple<string, double, string>>(targets_temp);
                            PlanTemplates.Add(tempTemplate);
                            templateCount++;
                        }
                    }
                }
                reader.Close();
            }
        }

        //very useful helper method to remove everything in the input string 'line' up to a given character 'cropChar'
        private string cropLine(string line, string cropChar) { return line.Substring(line.IndexOf(cropChar) + 1, line.Length - line.IndexOf(cropChar) - 1); }

        private Tuple<string, string> parseTS(string line)
        {
            //known array format --> can take shortcuts in parsing the data
            //structure id, sparing type, added margin in cm (ignored if sparing type is Dmax ~ Rx Dose)
            string dicomType = "";
            string TSstructure = "";
            line = cropLine(line, "{");
            dicomType = line.Substring(0, line.IndexOf(","));
            line = cropLine(line, ",");
            TSstructure = line.Substring(0, line.IndexOf("}"));
            return Tuple.Create(dicomType, TSstructure);
        }

        private Tuple<string, double, string> parseTargets(string line)
        {
            //known array format --> can take shortcuts in parsing the data
            //structure id, sparing type, added margin in cm (ignored if sparing type is Dmax ~ Rx Dose)
            string structure = "";
            string planId = "";
            double val = 0.0;
            line = cropLine(line, "{");
            structure = line.Substring(0, line.IndexOf(","));
            line = cropLine(line, ",");
            val = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = cropLine(line, ",");
            planId = line.Substring(0, line.IndexOf("}"));
            return Tuple.Create(structure, val, planId);
        }

        private Tuple<string, string, double> parseSparingStructure(string line)
        {
            //known array format --> can take shortcuts in parsing the data
            //structure id, sparing type, added margin in cm (ignored if sparing type is Dmax ~ Rx Dose)
            string structure = "";
            string spareType = "";
            double val = 0.0;
            line = cropLine(line, "{");
            structure = line.Substring(0, line.IndexOf(","));
            line = cropLine(line, ",");
            spareType = line.Substring(0, line.IndexOf(","));
            line = cropLine(line, ",");
            val = double.Parse(line.Substring(0, line.IndexOf("}")));
            return Tuple.Create(structure, spareType, val);
        }

        private Tuple<string, string, double, double, int> parseOptimizationConstraint(string line)
        {
            //known array format --> can take shortcuts in parsing the data
            //structure id, constraint type, dose (cGy), volume (%), priority
            string structure = "";
            string constraintType = "";
            double doseVal = 0.0;
            double volumeVal = 0.0;
            int priorityVal = 0;
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //be sure to close the patient before closing the application. Not doing so will result in unclosed timestamps in eclipse
            //if (autoSave) { app.SaveModifications(); Process.Start(optLoopProcess); }
            if (autoSave) app.SaveModifications();
            else if (isModified)
            {
                helpers.SaveDialog SD = new helpers.SaveDialog();
                SD.ShowDialog();
                if (SD.save) app.SaveModifications();
            }
            if (app != null)
            {
                if (pi != null) app.ClosePatient();
                app.Dispose();
            }
        }

        private void autorun_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(initRxTB.Text)) { MessageBox.Show("Error! Please select a template regimen or enter the dose per fx and number of fx!"); return; }

            //copy the sparing structures in the defaultSpareStruct list to a temporary vector
            //List<Tuple<string, string, double>> templateList = new List<Tuple<string, string, double>>(defaultSpareStruct);
            //add the case-specific sparing structures to the temporary list
            //if (noBoost_chkbox.IsChecked.Value) templateList = new List<Tuple<string, string, double>>(addCaseSpecificSpareStructures(nonmyeloSpareStruct, templateList));
            //else if (noBoost_chkbox.IsChecked.Value) templateList = new List<Tuple<string, string, double>>(addCaseSpecificSpareStructures(myeloSpareStruct, templateList));

            autoRunData a = new autoRunData();
           // a.construct(TS_structures, scleroStructures, templateList, selectedSS, 0.0, boost_chkbox.IsChecked.Value, useFlash, flashStructure, 0.0, app);
            //create a new thread and pass it the data structure created above (it will copy this information to its local thread memory)
            ESAPIworker slave = new ESAPIworker(a);
            //create a new frame (multithreading jargon)
            DispatcherFrame frame = new DispatcherFrame();
            //start the optimization
            //open a new window to run on the newly created thread called "slave"
            //for definition of the syntax used below, google "statement lambda c#"
            RunOnNewThread(() =>
            {
                //pass the progress window the newly created thread and this instance of the optimizationLoop class.
                AutorunProgress arpw = new AutorunProgress(slave);
                arpw.ShowDialog();

                //tell the code to hold until the progress window closes.
                frame.Continue = false;
            });

            Dispatcher.PushFrame(frame);
            //addDefaultsBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        //method to create the new thread, set the apartment state, set the new thread to be a background thread, and execute the action supplied to this method
        private void RunOnNewThread(Action a)
        {
            Thread t = new Thread(() => a());
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }

        private void MainWindow_SizeChanged(object sender, RoutedEventArgs e)
        {
            SizeToContent = SizeToContent.WidthAndHeight;
        }


    }
}