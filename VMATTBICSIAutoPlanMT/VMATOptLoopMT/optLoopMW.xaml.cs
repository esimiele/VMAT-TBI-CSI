using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using Microsoft.Win32;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIOptLoopMT.helpers;
using VMATTBICSIOptLoopMT.baseClasses;
using VMATTBICSIAutoplanningHelpers.helpers;
using VMATTBICSIOptLoopMT.VMAT_CSI;

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
        //default plan normalization (i.e., PTV100% = 90%) 
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

        //changed PTV_BODY to targetId for the cases where the patient has an appa plan and needs to ts_PTV_VMAT or ts_PTV_FLASH (if flash was used) structure
        public List<Tuple<string, string, double, double, DoseValuePresentation>> planObjSclero = new List<Tuple<string, string, double, double, DoseValuePresentation>>
            {
                //structure, constraint type, dose, relative volume
                //"<targetId>" will be overwritten with the actual Id of the target (depends if flash was used) 
                new Tuple<string, string, double, double, DoseValuePresentation>("<targetId>", "Lower", 800.0, 90.0, DoseValuePresentation.Absolute),
                new Tuple<string, string, double, double, DoseValuePresentation>("<targetId>", "Upper", 810.0, 0.0, DoseValuePresentation.Absolute),
                new Tuple<string, string, double, double, DoseValuePresentation>("Lungs_Eval", "Mean", 200.0, 0.0, DoseValuePresentation.Absolute),
                new Tuple<string, string, double, double, DoseValuePresentation>("Kidneys", "Mean", 200.0, 0.0, DoseValuePresentation.Absolute)
            };
        //generic plan objectives for all treatment regiments
        public List<Tuple<string, string, double, double, DoseValuePresentation>> planObjGeneral = new List<Tuple<string, string, double, double, DoseValuePresentation>>
            {
                //structure, constraint type, relative dose, relative volume (unless otherwise specified)
                //note, if the constraint type is "mean", the relative volume value is ignored
                new Tuple<string, string, double, double, DoseValuePresentation>("<targetId>", "Lower", 100.0, 90.0, DoseValuePresentation.Relative),
                new Tuple<string, string, double, double, DoseValuePresentation>("<targetId>", "Upper", 120.0, 0.0, DoseValuePresentation.Relative),
                new Tuple<string, string, double, double, DoseValuePresentation>("<targetId>", "Upper", 110.0, 5.0, DoseValuePresentation.Relative),
                new Tuple<string, string, double, double, DoseValuePresentation>("Lungs", "Mean", 60.0, 0.0, DoseValuePresentation.Relative),
                new Tuple<string, string, double, double, DoseValuePresentation>("Lungs-1cm", "Mean", 45.0, 0.0, DoseValuePresentation.Relative),
                new Tuple<string, string, double, double, DoseValuePresentation>("Kidneys", "Upper", 105.0, 0.0, DoseValuePresentation.Relative),
                new Tuple<string, string, double, double, DoseValuePresentation>("Kidneys", "Mean", 60.0, 0.0, DoseValuePresentation.Relative),
                new Tuple<string, string, double, double, DoseValuePresentation>("Bowel", "Upper", 105.0, 0.0, DoseValuePresentation.Relative),
                new Tuple<string, string, double, double, DoseValuePresentation>("Testes", "Upper", 100.0, 0.0, DoseValuePresentation.Absolute), //these doses are in cGy, not percentage!
                new Tuple<string, string, double, double, DoseValuePresentation>("Testes", "Mean", 25.0, 0.0, DoseValuePresentation.Relative), 
                new Tuple<string, string, double, double, DoseValuePresentation>("Ovaries", "Upper", 100.0, 0.0, DoseValuePresentation.Absolute), //these doses are in cGy, not percentage!
                new Tuple<string, string, double, double, DoseValuePresentation>("Ovaries", "Mean", 25.0, 0.0, DoseValuePresentation.Relative), 
                new Tuple<string, string, double, double, DoseValuePresentation>("Brain-1cm", "Mean", 75.0, 0.0, DoseValuePresentation.Relative),
                new Tuple<string, string, double, double, DoseValuePresentation>("Thyroid", "Mean", 75.0, 0.0, DoseValuePresentation.Relative)
            };

        public List<Tuple<string, string, double, double, DoseValuePresentation>> planObj = new List<Tuple<string, string, double, double, DoseValuePresentation>> { };
        public List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> requestedTSstructures = new List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>>
            {
                new Tuple<string,double,double,double,int,List<Tuple<string, double, string, double>>>("TS_cooler110",110.0,108.0,0.0,80,new List<Tuple<string, double, string, double>>{ }),
                new Tuple<string,double,double,double,int,List<Tuple<string, double, string, double>>>("TS_heater90",90.0,100.0,0.0,60,new List<Tuple<string, double, string, double>>{ }),
                new Tuple<string,double,double,double,int,List<Tuple<string, double, string, double>>>("TS_cooler70",70.0,90.0,0.0,80,new List<Tuple<string, double, string, double>>{new Tuple<string, double, string, double>("Dmax",0.0,">",140), new Tuple<string, double, string, double>("V",110.0,">",10)}),
            };

        public List<Tuple<string, string, double, string>> planDoseInfo = new List<Tuple<string, string, double, string>> { };

        VMS.TPS.Common.Model.API.Application app = VMS.TPS.Common.Model.API.Application.CreateApplication();
        ExternalPlanSetup plan;
        StructureSet selectedSS;
        Patient pi = null;
        int clearOptBtnCounter = 0;
        bool scleroTrial = false;
        bool runCoverageCheck = false;
        bool runOneMoreOpt = false;
        bool copyAndSavePlanItr = false;
        bool useFlash = false;

        public OptLoopMW(string[] args)
        {
            InitializeComponent();
            string patmrn = "";
            string configurationFile = "";
            for (int i = 0; i < args.Length; i++)
            {
                if (i == 0) patmrn = args[i];
                if (i == 1) configurationFile = args[i];
            }
            //if (args.Length == 0) configurationFile = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\configuration\\VMAT_TBI_config.ini";
            if (args.Length == 0) configurationFile = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\configuration\\VMAT_CSI_config.ini";

            if (File.Exists(configurationFile)) 
            { 
                if (!loadConfigurationSettings(configurationFile)) displayConfigurationParameters(); 
            }
            else MessageBox.Show("No configuration file found! Loading default settings!");
            if (args.Length > 0) 
            { 
                MRN.Text = patmrn; 
                OpenPatient_Click(new object(), new RoutedEventArgs()); 
            }
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

        #region button events
        private void OpenPatient_Click(object sender, RoutedEventArgs e)
        {
            //open the patient with the user-entered MRN number
            string pat_mrn = MRN.Text;
            clearEverything();
            try
            {
                app.ClosePatient();
                pi = app.OpenPatientById(pat_mrn);
                //grab instances of the course and VMAT tbi plans that were created using the binary plug in script. This is explicitly here to let the user know if there is a problem with the course OR plan
                //Course c = pi.Courses.FirstOrDefault(x => x.Id.ToLower() == "vmat tbi");
                (plan, selectedSS) = GetPlanAndStructureSet();
                if (plan == null)
                {
                    MessageBox.Show("No plan named _VMAT TBI!");
                    return;
                }

                //populate the optimization stackpanel with the optimization parameters that were stored in the VMAT TBI plan
                populateOptimizationTab();
                //populate the prescription text boxes with the prescription stored in the VMAT TBI plan
                populateRx();
                //set the default parameters for the optimization loop
                runCoverageCk.IsChecked = runCoverageCheckOption;
                numOptLoops.Text = defautlNumOpt;
                runAdditionalOpt.IsChecked = runAdditionalOptOption;
                copyAndSave.IsChecked = copyAndSaveOption;
                targetNormTB.Text = defaultPlanNorm;
            }
            catch 
            { 
                MessageBox.Show("No such patient exists!"); 
            }
        }

        private void getOptFromPlan_Click(object sender, RoutedEventArgs e)
        {
            if (pi != null && plan != null) populateOptimizationTab();
        }

        private void AddOptimizationConstraint_Click(object sender, RoutedEventArgs e)
        {
            //add a blank contraint to the list
            if (plan != null)
            {
                List<Tuple<string, string, double, double, int>> tmp = new List<Tuple<string, string, double, double, int>> { Tuple.Create("--select--", "--select--", 0.0, 0.0, 0) };
                StackPanel theSP = opt_parameters;
                if (opt_parameters.Children.Count > 0)
                {
                    OptimizationSetupUIHelper helper = new OptimizationSetupUIHelper();
                    List<Tuple<string, List<Tuple<string, string, double, double, int>>>> optParametersListList = helper.parseOptConstraints(theSP, false);
                    tmp = new List<Tuple<string, string, double, double, int>>(optParametersListList.First().Item2);
                    tmp.Add(Tuple.Create("--select--", "--select--", 0.0, 0.0, 0));
                }
                clearAllOptimizationStructs();
                AddOptimizationConstraintItems(tmp, plan.Id, opt_parameters);
                optParamScroller.ScrollToBottom();
            }
        }

        private void clear_optParams_Click(object sender, RoutedEventArgs e) { clearAllOptimizationStructs(); }

        private void ClearOptimizationConstraint_Click(object sender, EventArgs e)
        {
            //same code as in binary plug in
            Button btn = (Button)sender;
            int i = 0;
            int k = 0;
            foreach (object obj in opt_parameters.Children)
            {
                foreach (object obj1 in ((StackPanel)obj).Children) if ((obj1.Equals(btn))) k = i;
                if (k > 0) break;
                i++;
            }

            //clear entire list if there are only two entries (header + 1 real entry)
            if (opt_parameters.Children.Count < 3) clearAllOptimizationStructs();
            else opt_parameters.Children.RemoveAt(k);
        }
        #endregion

        #region UI manipulation
        private (ExternalPlanSetup, StructureSet) GetPlanAndStructureSet()
        {
            //grab an instance of the VMAT TBI plan. Return null if it isn't found
            if (pi == null) return (null, null);
            //Course c = pi.Courses.FirstOrDefault(x => x.Id.ToLower() == "vmat tbi");
            Course c = pi.Courses.FirstOrDefault(x => x.Id.ToLower() == "vmat csi");
            if (c == null) return (null, null);

            ExternalPlanSetup thePlan = c.ExternalPlanSetups.FirstOrDefault(x => x.Id.ToLower() == "csi-init");
            return (thePlan, thePlan.StructureSet);
        }

        private void clearEverything()
        {
            //clear all existing content from the main window
            dosePerFx.Text = numFx.Text = Rx.Text = numOptLoops.Text = "";
            opt_parameters.Children.Clear();
            clearOptBtnCounter = 0;
            scleroTrial = false;
        }

        private void populateRx()
        {
            //populate the prescription text boxes
            dosePerFx.Text = plan.DosePerFraction.Dose.ToString();
            numFx.Text = plan.NumberOfFractions.ToString();
            Rx.Text = plan.TotalDose.Dose.ToString();
            //if the dose per fraction and number of fractions equal 200 cGy and 4, respectively, then this is a scleroderma trial patient. This information will be passed to the optimization loop
            if (plan.DosePerFraction.Dose == 200.0 && plan.NumberOfFractions == 4) scleroTrial = true;
        }

        private void populateOptimizationTab()
        {
            //clear the current list of optimization constraints and ones obtained from the plan to the user
            clearAllOptimizationStructs();
            AddOptimizationConstraintItems(new OptimizationSetupUIHelper().ReadConstraintsFromPlan(plan), plan.Id, opt_parameters);
        }

        private void AddOptimizationConstraintsHeader(StackPanel theSP)
        {
            theSP.Children.Add(new OptimizationSetupUIHelper().getOptHeader(theSP.Width));
        }

        private void AddOptimizationConstraintItems(List<Tuple<string, string, double, double, int>> defaultList, string planId, StackPanel theSP)
        {
            int counter = clearOptBtnCounter;
            string clearBtnNamePrefix = "clearOptConstraintBtn";
            OptimizationSetupUIHelper helper = new OptimizationSetupUIHelper();
            theSP.Children.Add(helper.AddPlanIdtoOptList(theSP, planId));
            AddOptimizationConstraintsHeader(theSP);
            for (int i = 0; i < defaultList.Count; i++)
            {
                counter++;
                theSP.Children.Add(helper.addOptVolume(theSP, selectedSS, defaultList[i], clearBtnNamePrefix, counter, new RoutedEventHandler(this.ClearOptimizationConstraint_Click), theSP.Name.Contains("template") ? true : false));
            }
        }

        private void clearAllOptimizationStructs()
        {
            //same code as in binary plug in
            clearOptBtnCounter = 0;
            opt_parameters.Children.Clear();
        }
        #endregion

        #region start optimization
        private void startOpt_Click(object sender, RoutedEventArgs e)
        {
            if (plan == null) 
            { 
                MessageBox.Show("No plan or course found!"); 
                return; 
            }

            if (opt_parameters.Children.Count == 0)
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

            OptimizationSetupUIHelper helper = new OptimizationSetupUIHelper();
            List<Tuple<string, List<Tuple<string, string, double, double, int>>>> optParametersListList = helper.parseOptConstraints(opt_parameters);
            if (!optParametersListList.Any()) return;
            //determine if flash was used to prep the plan
            if (optParametersListList.Where(x => x.Item2.Where(y => y.Item1.ToLower().Contains("flash")).Any()).Any()) useFlash = true;

            //does the user want to run the initial dose coverage check?
            runCoverageCheck = runCoverageCk.IsChecked.Value;
            //does the user want to run one additional optimization to reduce hotspots?
            runOneMoreOpt = runAdditionalOpt.IsChecked.Value;
            //does the user want to copy and save each plan after it's optimized (so the user can choose between the various plans)?
            copyAndSavePlanItr = copyAndSave.IsChecked.Value;

            //construct the actual plan objective array
            planObj = new List<Tuple<string, string, double, double, DoseValuePresentation>>(ConstructPlanObjectives());
            planDoseInfo = new List<Tuple<string, string, double, string>>(ConstructPlanDoseInfo());

            //create a new instance of the structure dataContainer and assign the optimization loop parameters entered by the user to the various data members
            dataContainer data = new dataContainer();
            data.construct(plan, 
                           optParametersListList.First().Item2, 
                           planObj, 
                           requestedTSstructures, 
                           planDoseInfo,
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
            pi.BeginModifications();
            //use a bit of polymorphism
            optimizationLoopBase optLoop;
            optLoop = new VMATCSIOptimization(data);
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
                else tmp.Add(Tuple.Create(itr.Item1, itr.Item2, itr.Item3, itr.Item4));
            }
            return tmp;
        }

        private List<Tuple<string, string, double, double, DoseValuePresentation>> ConstructPlanObjectives()
        {
            List<Tuple<string, string, double, double, DoseValuePresentation>> tmp = new List<Tuple<string, string, double, double, DoseValuePresentation>> { };
            foreach(Tuple<string,string,double,double,DoseValuePresentation> obj in (scleroTrial ? planObjSclero : planObjGeneral))
            {
                if(obj.Item1 == "<targetId>")
                {
                    tmp.Add(Tuple.Create(GetPlanTargetId(), obj.Item2, obj.Item3, obj.Item4, obj.Item5)); 
                }
                else tmp.Add(Tuple.Create(obj.Item1, obj.Item2, obj.Item3, obj.Item4, obj.Item5));
            }
            return tmp;
        }

        private string GetPlanTargetId()
        {
            //if(useFlash) planObj.Add(Tuple.Create("TS_PTV_FLASH", obj.Item2, obj.Item3, obj.Item4, obj.Item5)); 
            //else planObj.Add(Tuple.Create("TS_PTV_VMAT", obj.Item2, obj.Item3, obj.Item4, obj.Item5)); 
            if (useFlash) return "TS_PTV_FLASH";
            else return "TS_PTV_CSI";
        }
        #endregion

        #region script configuration
        private void displayConfigurationParameters()
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

            if (planDoseInfo.Any())
            {
                configTB.Text += "---------------------------------------------------------------------------" + Environment.NewLine;
                configTB.Text += String.Format("Requested dosimetric info from plan after each iteration:") + Environment.NewLine;
                configTB.Text += String.Format(" {0, -15} | {1, -6} | {2, -9} |", "structure Id", "metric", "dose type") + Environment.NewLine;

                foreach (Tuple<string,string,double,string> itr in planDoseInfo)
                {
                    if(itr.Item2.Contains("max") || itr.Item2.Contains("min")) configTB.Text += String.Format(" {0, -15} | {1, -6} | {2, -9} |", itr.Item1, itr.Item2, itr.Item4) + Environment.NewLine;
                    else configTB.Text += String.Format(" {0, -15} | {1, -6} | {2, -9} |", itr.Item1, String.Format("{0}{1}%", itr.Item2, itr.Item3), itr.Item4) + Environment.NewLine;
                }
                configTB.Text += Environment.NewLine;
                configTB.Text += Environment.NewLine;
            }
            
            configTB.Text += "---------------------------------------------------------------------------" + Environment.NewLine;
            configTB.Text += String.Format("Scleroderma trial plan objectives:") + Environment.NewLine;
            configTB.Text += String.Format(" {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -9} |", "structure Id", "constraint type", "dose", "volume (%)", "dose type") + Environment.NewLine;
            foreach (Tuple<string, string, double, double, DoseValuePresentation> itr in planObjSclero) configTB.Text += String.Format(" {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-9} |" + Environment.NewLine, itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5);
            configTB.Text += Environment.NewLine;
            configTB.Text += Environment.NewLine;

            configTB.Text += "---------------------------------------------------------------------------" + Environment.NewLine;
            configTB.Text += String.Format("General plan objectives:") + Environment.NewLine;
            configTB.Text += String.Format(" {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -9} |", "structure Id", "constraint type", "dose", "volume (%)", "dose type") + Environment.NewLine;
            foreach (Tuple<string, string, double, double, DoseValuePresentation> itr in planObjGeneral) configTB.Text += String.Format(" {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-9} |" + Environment.NewLine, itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5);
            configTB.Text += Environment.NewLine;
            
            configTB.Text += "---------------------------------------------------------------------------" + Environment.NewLine;
            configTB.Text += String.Format("Requested tuning structures:") + Environment.NewLine;
            configTB.Text += String.Format(" {0, -15} | {1, -9} | {2, -10} | {3, -5} | {4, -8} | {5, -10} |", "structure Id", "low D (%)", "high D (%)", "V (%)", "priority", "constraint") + Environment.NewLine;
            foreach (Tuple<string, double,double,double,int,List<Tuple<string,double,string,double>>> itr in requestedTSstructures)
            {
                configTB.Text += String.Format(" {0, -15} | {1, -9:N1} | {2,-10:N1} | {3,-5:N1} | {4,-8} |", itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5);
                if (!itr.Item6.Any()) configTB.Text += String.Format(" {0,-10} |", "none") + Environment.NewLine;
                else 
                {
                    int count = 0;
                    foreach (Tuple<string, double, string, double> itr1 in itr.Item6)
                    {
                        if (count == 0)
                        {
                            if (itr1.Item1.Contains("Dmax")) configTB.Text += String.Format(" {0,-10} |", String.Format("{0}{1}{2}%", itr1.Item1, itr1.Item3, itr1.Item4)) + Environment.NewLine;
                            else if(itr1.Item1.Contains("V")) configTB.Text += String.Format(" {0,-10} |", String.Format("{0}{1}%{2}{3}%", itr1.Item1, itr1.Item2, itr1.Item3, itr1.Item4)) + Environment.NewLine;
                            else configTB.Text += String.Format(" {0,-10} |", String.Format("{0}", itr1.Item1)) + Environment.NewLine;
                        }
                        else
                        {
                            if (itr1.Item1.Contains("Dmax")) configTB.Text += String.Format(" {0,-59} | {1,-10} |", " ", String.Format("{0}{1}{2}%", itr1.Item1, itr1.Item3, itr1.Item4)) + Environment.NewLine;
                            else if (itr1.Item1.Contains("V")) configTB.Text += String.Format(" {0,-59} | {1,-10} |", " ", String.Format("{0}{1}%{2}{3}%", itr1.Item1, itr1.Item2, itr1.Item3, itr1.Item4)) + Environment.NewLine;
                            else configTB.Text += String.Format(" {0,-59} | {1,-10} |", " ", String.Format("{0}", itr1.Item1)) + Environment.NewLine;
                        }
                        count++;
                    }
                }
            }
            configScroller.ScrollToTop();
        }

        private void loadNewConfigFile_Click(object sender, RoutedEventArgs e)
        {
            configFile = "";
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\configuration\\";
            openFileDialog.Filter = "ini files (*.ini)|*.ini|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog().Value) { if (!loadConfigurationSettings(openFileDialog.FileName)) { if (pi != null) displayConfigurationParameters(); } else MessageBox.Show("Error! Selected file is NOT valid!"); }
        }

        private bool loadConfigurationSettings(string file)
        {
            configFile = file;
            try
            {
                using (StreamReader reader = new StreamReader(configFile))
                {
                    string line;
                    List<Tuple<string, string, double, double, DoseValuePresentation>> planObjSclero_temp = new List<Tuple<string, string, double, double, DoseValuePresentation>> { };
                    List<Tuple<string, string, double, double, DoseValuePresentation>> planObjGeneral_temp = new List<Tuple<string, string, double, double, DoseValuePresentation>> { };
                    List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> requestedTSstructures_temp = new List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> { };
                    List<Tuple<string, string, double, string>> planDoseInfo_temp = new List<Tuple<string, string, double, string>> { };
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrEmpty(line) && line.Substring(0, 1) != "%")
                        {
                            //start actually reading data when you find the begin executable configuration tab
                            if (line.Equals(":begin executable configuration:"))
                            {
                                while (!(line = reader.ReadLine()).Equals(":end executable configuration:"))
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
                                        else if (line.Contains("add scleroderma plan objective")) planObjSclero_temp.Add(parsePlanObjective(line));
                                        else if (line.Contains("add plan objective")) planObjGeneral_temp.Add(parsePlanObjective(line));
                                        else if (line.Contains("add TS structure")) requestedTSstructures_temp.Add(parseTSstructure(line));
                                        else if (line.Contains("add plan dose info")) planDoseInfo_temp.Add(ParseRequestedPlanDoseInfo(line));
                                    }
                                }
                            }
                        }
                    }
                    if (planObjSclero_temp.Any()) planObjSclero = planObjSclero_temp;
                    if (planObjGeneral_temp.Any()) planObjGeneral = planObjGeneral_temp;
                    if (requestedTSstructures_temp.Any()) requestedTSstructures = requestedTSstructures_temp;
                    if (planDoseInfo_temp.Any()) planDoseInfo = planDoseInfo_temp;
                }
                return false;
            }
            catch (Exception e) { MessageBox.Show(String.Format("Error could not load configuration file because: {0}\n\nAssuming default parameters", e.Message)); return true; }
        }

        private Tuple<string, string, double, double, DoseValuePresentation> parsePlanObjective(string line)
        {
            string structure = "";
            string constraintType = "";
            double doseVal = 0.0;
            double volumeVal = 0.0;
            DoseValuePresentation dvp;
            line = cropLine(line, "{");
            structure = line.Substring(0, line.IndexOf(","));
            line = cropLine(line, ",");
            constraintType = line.Substring(0, line.IndexOf(","));
            line = cropLine(line, ",");
            doseVal = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = cropLine(line, ",");
            volumeVal = double.Parse(line.Substring(0, line.IndexOf(",")));
            line = cropLine(line, ",");
            if (line.Contains("Relative")) dvp = DoseValuePresentation.Relative;
            else dvp = DoseValuePresentation.Absolute;
            return Tuple.Create(structure, constraintType, doseVal, volumeVal, dvp);
        }

        private Tuple<string,double,double,double,int,List<Tuple<string,double,string,double>>> parseTSstructure(string line)
        {
            //type (Dmax or V), dose value for volume constraint (N/A for Dmax), equality or inequality, volume (%) or dose (%)
            List<Tuple<string, double, string, double>> constraints = new List<Tuple<string, double, string, double>> { };
            string structure = "";
            double lowDoseLevel = 0.0;
            double upperDoseLevel = 0.0;
            double volumeVal = 0.0;
            int priority = 0;
            try
            {
                line = cropLine(line, "{");
                structure = line.Substring(0, line.IndexOf(","));
                line = cropLine(line, ",");
                lowDoseLevel = double.Parse(line.Substring(0, line.IndexOf(",")));
                line = cropLine(line, ",");
                upperDoseLevel = double.Parse(line.Substring(0, line.IndexOf(",")));
                line = cropLine(line, ",");
                volumeVal = double.Parse(line.Substring(0, line.IndexOf(",")));
                line = cropLine(line, ",");
                priority = int.Parse(line.Substring(0, line.IndexOf(",")));
                line = cropLine(line, "{");

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
                        if (!line.Contains(",")) line = cropLine(line, "}");
                        else line = cropLine(line, ",");
                    }
                    else
                    {
                        if (line.Substring(0, 1) == "V")
                        {
                            constraintType = "V";
                            line = cropLine(line, "V");
                            int index = 0;
                            while (line.ElementAt(index).ToString() != ">" && line.ElementAt(index).ToString() != "<") index++;
                            doseVal = double.Parse(line.Substring(0, index));
                            line = line.Substring(index, line.Length - index);
                        }
                        else
                        {
                            constraintType = "Dmax";
                            line = cropLine(line, "x");
                        }
                        inequality = line.Substring(0, 1);

                        if (!line.Contains(",")) { queryVal = double.Parse(line.Substring(1, line.IndexOf("}") - 1)); line = cropLine(line, "}"); }
                        else
                        {
                            queryVal = double.Parse(line.Substring(1, line.IndexOf(",") - 1));
                            line = cropLine(line, ",");
                        }
                    }
                    constraints.Add(Tuple.Create(constraintType, doseVal, inequality, queryVal));
                }

                return Tuple.Create(structure, lowDoseLevel, upperDoseLevel, volumeVal, priority, new List<Tuple<string, double, string, double>>(constraints));
            }
            catch (Exception e) { MessageBox.Show(String.Format("Error could not parse TS structure: {0}\nBecause: {1}", line, e.Message)); return Tuple.Create("", 0.0, 0.0, 0.0, 0, new List<Tuple<string, double, string, double>> { }); }
        }

        private Tuple<string,string,double,string> ParseRequestedPlanDoseInfo(string line)
        {
            line = cropLine(line, "{");
            string structure = line.Substring(0, line.IndexOf(","));
            line = cropLine(line, ",");
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
                    constraintTypeTmp = cropLine(constraintTypeTmp, "D");
                    doseVal = double.Parse(constraintTypeTmp);
                }
            }
            else
            {
                constraintType = "V";
                constraintTypeTmp = cropLine(constraintTypeTmp, "V");
                doseVal = double.Parse(constraintTypeTmp);
            }
            line = cropLine(line, ",");
            if (line.Contains("Relative")) representation = "Relative";
            else representation = "Absolute";

            return Tuple.Create(structure, constraintType, doseVal, representation);
        }

        private string cropLine(string line, string cropChar) { return line.Substring(line.IndexOf(cropChar) + 1, line.Length - line.IndexOf(cropChar) - 1); }
        #endregion

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //be sure to close the patient before closing the application. Not doing so will result in unclosed timestamps in eclipse
            app.ClosePatient();
            app.Dispose();
        }
    }
}
