using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Reflection;
using System.IO;

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0")]

[assembly: ESAPIScript(IsWriteable = true)]

namespace VMATCSIAutoPlanMT
{
    public partial class MainWindow : Window
    {
        string configFile = "";
        /// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// HARD-CODED MAIN PARAMETERS FOR THIS CLASS AND ALL OTHER CLASSES IN THIS DLL APPLICATION.
        /// ADJUST THESE PARAMETERS TO YOUR TASTE. THESE PARAMETERS WILL BE OVERWRITTEN BY THE CONFIG.INI FILE IF IT IS SUPPLIED.
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        double nonmyeloDosePerFx = 200;
        int nonmyeloNumFx = 1;
        List<Tuple<string, string, double, double, int>> optConstDefaultNonMyelo = new List<Tuple<string, string, double, double, int>>
        {
            new Tuple<string, string, double, double, int>("TS_PTV_VMAT", "Lower", 200.0, 100.0, 100),
            new Tuple<string, string, double, double, int>("TS_PTV_VMAT", "Upper", 202.0, 0.0, 100),
            new Tuple<string, string, double, double, int>("TS_PTV_VMAT", "Lower", 201.0, 98.0, 100),
            new Tuple<string, string, double, double, int>("Kidneys", "Mean", 120.0, 0.0, 80),
            new Tuple<string, string, double, double, int>("Kidneys-1cm", "Mean", 75.0, 0.0, 50),
            new Tuple<string, string, double, double, int>("Lungs", "Mean", 75.0, 0.0, 90),
            new Tuple<string, string, double, double, int>("Lungs-1cm", "Mean", 50.0, 0.0, 80),
            new Tuple<string, string, double, double, int>("Lungs-2cm", "Mean", 25.0, 0.0, 70),
            new Tuple<string, string, double, double, int>("Ovaries", "Mean", 50.0, 0.0, 50),
            new Tuple<string, string, double, double, int>("Ovaries", "Upper", 75.0, 0.0, 70),
            new Tuple<string, string, double, double, int>("Testes", "Mean", 50.0, 0.0, 50),
            new Tuple<string, string, double, double, int>("Testes", "Upper", 75.0, 0.0, 70),
            new Tuple<string, string, double, double, int>("Lenses", "Upper", 190.0, 0.0, 50),
            new Tuple<string, string, double, double, int>("Brain", "Mean", 150.0, 0.0, 60),
            new Tuple<string, string, double, double, int>("Brain-1cm", "Mean", 100.0, 0.0, 50),
            new Tuple<string, string, double, double, int>("Brain-2cm", "Mean", 75.0, 0.0, 50),
            new Tuple<string, string, double, double, int>("Brain-3cm", "Mean", 50.0, 0.0, 50),
            new Tuple<string, string, double, double, int>("Bowel", "Upper", 201.0, 0.0, 50),
            new Tuple<string, string, double, double, int>("Thyroid", "Mean", 100.0, 0.0, 50)
        };

        //general tuning structures to be added (if selected for sparing) to all case types
        List<Tuple<string, string>> TS_structures = new List<Tuple<string, string>>
        { Tuple.Create("CONTROL","Human_Body"),
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

        List<Tuple<string, string, double>> defaultSpareStruct = new List<Tuple<string, string, double>>
        {
            new Tuple<string, string, double>("Lungs", "Mean Dose < Rx Dose", 0.3),
            new Tuple<string, string, double>("Kidneys", "Mean Dose < Rx Dose", 0.0),
            new Tuple<string, string, double>("Bowel", "Dmax ~ Rx Dose", 0.0)
        };

        List<Tuple<string, string, double>> nonmyeloSpareStruct = new List<Tuple<string, string, double>>
        {
            new Tuple<string, string, double>("Ovaries", "Mean Dose < Rx Dose", 1.5),
            new Tuple<string, string, double>("Testes", "Mean Dose < Rx Dose", 2.0),
            new Tuple<string, string, double>("Brain", "Mean Dose < Rx Dose", -0.5),
            new Tuple<string, string, double>("Lenses", "Dmax ~ Rx Dose", 0.0),
            new Tuple<string, string, double>("Thyroid", "Mean Dose < Rx Dose", 0.0)
        };

        //flash option
        bool useFlashByDefault = true;
        //default flash margin of 0.5 cm
        string defaultFlashMargin = "0.5";
        //option to contour overlap between VMAT fields in adjacent isocenters and default margin for contouring the overlap
        bool contourOverlap = true;
        string contourFieldOverlapMargin = "1.0";
        //point this to the directory holding the documentation files
        string documentationPath = @"\\enterprise.stanfordmed.org\depts\RadiationTherapy\Public\Users\ESimiele\Research\VMAT_TBI\documentation\";
        //treatment units and associated photon beam energies
        List<string> linacs = new List<string> { "LA16", "LA17" };
        List<string> beamEnergies = new List<string> { "6X", "10X" };
        //default number of beams per isocenter from head to toe
        int[] beamsPerIso = { 2, 1, 1};
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
        public Patient pi = null;
        StructureSet selectedSS = null;
        private bool firstSpareStruct = true;
        private bool firstOptStruct = true;
        public int clearSpareBtnCounter = 0;
        public int clearOptBtnCounter = 0;
        List<Tuple<string, string>> optParameters = new List<Tuple<string, string>> { };
        ExternalPlanSetup VMATplan = null;
        int numIsos = 0;
        int numVMATIsos = 0;
        public List<string> isoNames = new List<string> { };
        Tuple<int, DoseValue> prescription = null;
        bool useFlash = false;
        string flashType = "";
        List<Structure> jnxs = new List<Structure> { };
        Structure flashStructure = null;
        //planPrep prep = null;
        public VMS.TPS.Common.Model.API.Application app = VMS.TPS.Common.Model.API.Application.CreateApplication();
        bool isModified = false;
        bool autoSave = false;
        //ProcessStartInfo optLoopProcess;
        public MainWindow(string[] args)
        {
            InitializeComponent();
            string mrn = "";
            string ss = "";
            string configurationFile = "";
            for (int i = 0; i < args.Length; i++)
            {
                if (i == 0) mrn = args[i];
                if (i == 1) ss = args[i];
                if (i == 2) configurationFile = args[i];
            }
            if (string.IsNullOrEmpty(mrn) || string.IsNullOrWhiteSpace(mrn))
            {
                //missing patient MRN. Need to ask user for it
                enterMissingInfo e = new enterMissingInfo("Missing patient Id!\nPlease enter it below and hit Confirm!", "MRN:");
                e.ShowDialog();
                if (!e.confirm) { this.Close(); return; }
                try { pi = app.OpenPatientById(e.value.Text); }
                catch (Exception) { MessageBox.Show(string.Format("Error! Could not open patient: {0}! Please try again!", e.value.Text)); }
            }
            else pi = app.OpenPatientById(mrn);

            //check the version information of Eclipse installed on this machine. If it is older than version 15.6, let the user know that this script may not work properly on their system
            if (!double.TryParse(app.ScriptEnvironment.VersionInfo.Substring(0, app.ScriptEnvironment.VersionInfo.LastIndexOf(".")), out double vinfo)) MessageBox.Show("Warning! Could not parse Eclise version number! Proceed with caution!");
            else if (vinfo < 15.6) MessageBox.Show(String.Format("Warning! Detected Eclipse version: {0:0.0} is older than v15.6! Proceed with caution!", vinfo));

            //SSID is combobox defined in UI.xaml
            foreach (StructureSet s in pi.StructureSets) SSID.Items.Add(s.Id);
            //SSID default is the current structure set in the context
            if (!string.IsNullOrEmpty(ss)) { selectedSS = pi.StructureSets.FirstOrDefault(x => x.Id == ss); SSID.Text = selectedSS.Id; }
            else MessageBox.Show("Warning! No structure set in context! Please select a structure set at the top of the GUI!");

            //load script configuration and display the settings
            if (configurationFile != "") loadConfigurationSettings(configurationFile);
            displayConfigurationParameters();

            //pre-populate the flash comboxes (set global flash as default)
            flashMarginTB.Text = defaultFlashMargin;
        }

        //flash stuff
        //simple method to either show or hide the relevant flash parameters depending on if the user wants to use flash (i.e., if the 'add flash' checkbox is checked)
        private void flash_chkbox_Click(object sender, RoutedEventArgs e) { updateUseFlash(); }

        private void updateUseFlash()
        {
            //logic to hide or show the flash option in GUI
            if (flash_chkbox.IsChecked.Value)
            {
                flashMarginLabel.Visibility = Visibility.Visible;
                flashMarginTB.Visibility = Visibility.Visible;
            }
            else
            {
                flashMarginLabel.Visibility = Visibility.Hidden;
                flashMarginTB.Visibility = Visibility.Hidden;
            }
            //update whether the user wants to user flash or not
            useFlash = flash_chkbox.IsChecked.Value;
        }

        //method to display the loaded configuration settings
        private void displayConfigurationParameters()
        {
            configTB.Text = "";
            configTB.Text = String.Format("{0}", DateTime.Now.ToString()) + System.Environment.NewLine;
            if (configFile != "") configTB.Text += String.Format("Configuration file: {0}", configFile) + System.Environment.NewLine + System.Environment.NewLine;
            else configTB.Text += String.Format("Configuration file: none") + System.Environment.NewLine + System.Environment.NewLine;
            configTB.Text += String.Format("Documentation path: {0}", documentationPath) + System.Environment.NewLine + System.Environment.NewLine;
            configTB.Text += String.Format("Default parameters:") + System.Environment.NewLine;
            configTB.Text += String.Format("Include flash by default: {0}", useFlashByDefault) + System.Environment.NewLine;
            configTB.Text += String.Format("Flash margin: {0} cm", defaultFlashMargin) + System.Environment.NewLine;
            configTB.Text += String.Format("Contour field ovelap: {0}", contourOverlap) + System.Environment.NewLine;
            configTB.Text += String.Format("Contour field overlap margin: {0} cm", contourFieldOverlapMargin) + System.Environment.NewLine;
            configTB.Text += String.Format("Available linacs:") + System.Environment.NewLine;
            foreach (string l in linacs) configTB.Text += l + System.Environment.NewLine;
            configTB.Text += String.Format("Available photon energies:") + System.Environment.NewLine;
            foreach (string e in beamEnergies) configTB.Text += e + System.Environment.NewLine;
            configTB.Text += String.Format("Beams per isocenter: ");
            for (int i = 0; i < beamsPerIso.Length; i++)
            {
                configTB.Text += String.Format("{0}", beamsPerIso.ElementAt(i));
                if (i != beamsPerIso.Length - 1) configTB.Text += String.Format(", ");
            }
            configTB.Text += System.Environment.NewLine;
            configTB.Text += String.Format("Collimator rotation (deg) order: ");
            for (int i = 0; i < collRot.Length; i++)
            {
                configTB.Text += String.Format("{0:0.0}", collRot.ElementAt(i));
                if (i != collRot.Length - 1) configTB.Text += String.Format(", ");
            }
            configTB.Text += System.Environment.NewLine;
            configTB.Text += String.Format("Field jaw position (cm) order: ") + System.Environment.NewLine;
            configTB.Text += String.Format("(x1,y1,x2,y2)") + System.Environment.NewLine;
            foreach (VRect<double> j in jawPos) configTB.Text += String.Format("({0:0.0},{1:0.0},{2:0.0},{3:0.0})", j.X1 / 10, j.Y1 / 10, j.X2 / 10, j.Y2 / 10) + System.Environment.NewLine;
            configTB.Text += String.Format("Photon dose calculation model: {0}", calculationModel) + System.Environment.NewLine;
            configTB.Text += String.Format("Use GPU for dose calculation: {0}", useGPUdose) + System.Environment.NewLine;
            configTB.Text += String.Format("Photon optimization model: {0}", optimizationModel) + System.Environment.NewLine;
            configTB.Text += String.Format("Use GPU for optimization: {0}", useGPUoptimization) + System.Environment.NewLine;
            configTB.Text += String.Format("MR level restart at: {0}", MRrestartLevel) + System.Environment.NewLine + System.Environment.NewLine;

            configTB.Text += String.Format("Requested general tuning structures:") + System.Environment.NewLine;
            configTB.Text += String.Format(" {0, -10} | {1, -15} |", "DICOM type", "Structure Id") + System.Environment.NewLine;
            foreach (Tuple<string, string> ts in TS_structures) configTB.Text += String.Format(" {0, -10} | {1, -15} |" + System.Environment.NewLine, ts.Item1, ts.Item2);
            configTB.Text += System.Environment.NewLine;

            configTB.Text += String.Format("Default sparing structures:") + System.Environment.NewLine;
            configTB.Text += String.Format(" {0, -15} | {1, -19} | {2, -11} |", "structure Id", "sparing type", "margin (cm)") + System.Environment.NewLine;
            foreach (Tuple<string, string, double> spare in defaultSpareStruct) configTB.Text += String.Format(" {0, -15} | {1, -19} | {2,-11:N1} |" + System.Environment.NewLine, spare.Item1, spare.Item2, spare.Item3);
            configTB.Text += System.Environment.NewLine;

            configTB.Text += "-----------------------------------------------------------------------------" + System.Environment.NewLine;
            configTB.Text += String.Format("Non-Myeloablative case parameters:") + System.Environment.NewLine;
            configTB.Text += String.Format("Dose per fraction: {0} cGy", nonmyeloDosePerFx) + System.Environment.NewLine;
            configTB.Text += String.Format("Number of fractions: {0}", nonmyeloNumFx) + System.Environment.NewLine;
            if (nonmyeloSpareStruct.Any())
            {
                configTB.Text += String.Format("Non-Myeloablative case additional sparing structures:") + System.Environment.NewLine;
                configTB.Text += String.Format(" {0, -15} | {1, -19} | {2, -11} |", "structure Id", "sparing type", "margin (cm)") + System.Environment.NewLine;
                foreach (Tuple<string, string, double> spare in nonmyeloSpareStruct) configTB.Text += String.Format(" {0, -15} | {1, -19} | {2,-11:N1} |" + System.Environment.NewLine, spare.Item1, spare.Item2, spare.Item3);
                configTB.Text += System.Environment.NewLine;
            }
            else configTB.Text += String.Format("No additional sparing structures for Non-Myeloablative case") + System.Environment.NewLine + System.Environment.NewLine;
            configTB.Text += String.Format("Optimization parameters:") + System.Environment.NewLine;
            configTB.Text += String.Format(" {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |", "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority") + System.Environment.NewLine;
            foreach (Tuple<string, string, double, double, int> opt in optConstDefaultNonMyelo) configTB.Text += String.Format(" {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |" + System.Environment.NewLine, opt.Item1, opt.Item2, opt.Item3, opt.Item4, opt.Item5);
            configTB.Text += "-----------------------------------------------------------------------------" + System.Environment.NewLine;
            configScroller.ScrollToTop();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //be sure to close the patient before closing the application. Not doing so will result in unclosed timestamps in eclipse
            //if (autoSave) { app.SaveModifications(); Process.Start(optLoopProcess); }
            if (isModified)
            {
                confirmUI CUI = new confirmUI();
                CUI.message.Text = "Save work to database?";
                CUI.ShowDialog();
                CUI.confirmBTN.Text = "YES";
                CUI.cancelBTN.Text = "No";
                if (CUI.confirm) app.SaveModifications();
            }
            if (pi != null) app.ClosePatient();
            app.Dispose();
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
                    List<Tuple<string, string>> TSstructures_temp = new List<Tuple<string, string>> { };
                    List<Tuple<string, string>> scleroTSstructures_temp = new List<Tuple<string, string>> { };
                    List<Tuple<string, string, double, double, int>> optConstDefaultSclero_temp = new List<Tuple<string, string, double, double, int>> { };
                    List<Tuple<string, string, double, double, int>> optConstDefaultMyelo_temp = new List<Tuple<string, string, double, double, int>> { };
                    List<Tuple<string, string, double, double, int>> optConstDefaultNonMyelo_temp = new List<Tuple<string, string, double, double, int>> { };
                    List<Tuple<string, string, double>> scleroSpareStruct_temp = new List<Tuple<string, string, double>> { };
                    List<Tuple<string, string, double>> myeloSpareStruct_temp = new List<Tuple<string, string, double>> { };
                    List<Tuple<string, string, double>> nonmyeloSpareStruct_temp = new List<Tuple<string, string, double>> { };

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
                                        //check if it's a double value
                                        if (double.TryParse(value, out double result))
                                        {
                                            if (parameter == "default flash margin") defaultFlashMargin = result.ToString();
                                        }
                                        else if (parameter == "documentation path")
                                        {
                                            documentationPath = value;
                                            if (documentationPath.LastIndexOf("\\") != documentationPath.Length - 1) documentationPath += "\\";
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
                                        else if (parameter == "use GPU for dose calculation") useGPUdose = value;
                                        else if (parameter == "use GPU for optimization") useGPUoptimization = value;
                                        else if (parameter == "MR level restart") MRrestartLevel = value;
                                        //other parameters that should be updated
                                        else if (parameter == "use flash by default") useFlashByDefault = bool.Parse(value);
                                        else if (parameter == "calculation model") { if (value != "") calculationModel = value; }
                                        else if (parameter == "optimization model") { if (value != "") optimizationModel = value; }
                                        else if (parameter == "contour field overlap") { if (value != "") contourOverlap = bool.Parse(value); }
                                        else if (parameter == "contour field overlap margin") { if (value != "") contourFieldOverlapMargin = value; }
                                    }
                                    else if (line.Contains("add default sparing structure")) defaultSpareStruct_temp.Add(parseSparingStructure(line));
                                    else if (line.Contains("add TS")) TSstructures_temp.Add(parseTS(line));
                                    else if (line.Contains("add sclero TS")) scleroTSstructures_temp.Add(parseTS(line));
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
                                    else if (line.Equals(":begin nonmyeloablative case configuration:"))
                                    {
                                        //parse the data specific to the non-myeloablative case setup
                                        while (!(line = reader.ReadLine()).Equals(":end nonmyeloablative case configuration:"))
                                        {
                                            if (line.Substring(0, 1) != "%")
                                            {
                                                if (line.Contains("="))
                                                {
                                                    string parameter = line.Substring(0, line.IndexOf("="));
                                                    string value = line.Substring(line.IndexOf("=") + 1, line.Length - line.IndexOf("=") - 1);
                                                    if (parameter == "dose per fraction") { if (double.TryParse(value, out double result)) nonmyeloDosePerFx = result; }
                                                    else if (parameter == "num fx") { if (int.TryParse(value, out int fxResult)) nonmyeloNumFx = fxResult; }
                                                }
                                                else if (line.Contains("add sparing structure")) nonmyeloSpareStruct_temp.Add(parseSparingStructure(line));
                                                else if (line.Contains("add opt constraint")) optConstDefaultNonMyelo_temp.Add(parseOptimizationConstraint(line));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    //anything that is an array needs to be updated AFTER the while loop.
                    if (linac_temp.Any()) linacs = new List<string>(linac_temp);
                    if (energy_temp.Any()) beamEnergies = new List<string>(energy_temp);
                    if (jawPos_temp.Any() && jawPos_temp.Count == 4) jawPos = new List<VRect<double>>(jawPos_temp);
                    if (defaultSpareStruct_temp.Any()) defaultSpareStruct = new List<Tuple<string, string, double>>(defaultSpareStruct_temp);
                    if (TSstructures_temp.Any()) TS_structures = new List<Tuple<string, string>>(TSstructures_temp);
                    if (nonmyeloSpareStruct_temp.Any()) nonmyeloSpareStruct = new List<Tuple<string, string, double>>(nonmyeloSpareStruct_temp);
                    if (optConstDefaultNonMyelo_temp.Any()) optConstDefaultNonMyelo = new List<Tuple<string, string, double, double, int>>(optConstDefaultNonMyelo_temp);
                }
                return false;
            }
            //let the user know if the data parsing failed
            catch (Exception e) { MessageBox.Show(String.Format("Error could not load configuration file because: {0}\n\nAssuming default parameters", e.Message)); return true; }
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
    }
}
