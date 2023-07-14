using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMS.TPS;

namespace planPrepper
{
    public partial class UI : UserControl
    {
        public Patient pi;
        ScriptContext context;
        ExternalPlanSetup thePlan = null;
        planPrep prep = null;
        string planType = "";
        public UI(ScriptContext c)
        {
            InitializeComponent();
            pi = c.Patient;
            context = c;
        }

        private void scriptSetupInfo_Click(object sender, RoutedEventArgs e)
        {
            string message = String.Format("Hello!\n" +
                                            "To use this script:\n" +
                                            "1. Open the plan with multiple isocenters in Eclipse\n" +
                                            "2. Make sure the beams are numbered (i.e., the first character of the beam Id) in the order you intend. E.g., CSI plans will have the brain beams as 1 and 2 and the lower spine beam as 4. The script can accept a maximum of 9 beams.\n" +
                                            "3. (optional) Ensure setup fields are present in the plan. These can be assigned to any isocenter. The script will copy these setup fields to each plan it creates. Ensure the setup field names are ISO AP, ISO PA, ISO RLAT, ISO LLAT, and ISO CBCT\n" +
                                            "4. Run the script.\n" +
                                            "5. If there are multiple plans in the current treatment course, the script will ask you which one you want to prepare\n" +
                                            "6. The shift note will be generate first. The relative shifts between isocenters and the user origin will be calculated. In addition, the table top shift will also be calculated and copied to the clipboard\n" +
                                            "7. Paste the shift note into the Journal and verify the calculated shifts make sense\n" +
                                            "8. Next, hit the Separate Plan button. The script will automatically choose names for the plans for CSI and TLI cases. If the plan is not a CSI or TLI case, the script will prompt you to enter names for each plan/isocenter\n" +
                                            "9. If PlanDoseCalculation was set to true (in calculation options), dose will need to be recalculated. In this case, another button will appear asking if you want to recalculate dose to the newly created plans.\n" +
                                            "10. Verify the MU for each field in the newly created plans agree with the MUs in the original plan.");
            MessageBox.Show(message);
        }

        private void generateShiftNote_Click(object sender, RoutedEventArgs e)
        {
            thePlan = getPlan();
            if (thePlan == null) return;
            //determine the plan type. If the plan or course Id contain 'TLI' or 'CSI', plan preparation can be done automatically. If not, then the user will have to manually specify the names of each isocenter
            if (thePlan.Id.ToLower().Contains("tli") || thePlan.Course.Id.ToLower().Contains("tli")) planType = "TLI";
            else if (thePlan.Id.ToLower().Contains("csi") || thePlan.Course.Id.ToLower().Contains("csi")) planType = "CSI";
            else planType = "other";

            //create an instance of the planPep class and pass it the vmatPlan and appaPlan objects as arguments. Get the shift note for the plan of interest
            prep = new planPrepper.planPrep(thePlan, planType);
            if (prep.getShiftNote()) return;

            //let the user know this step has been completed (they can now do the other steps like separate plans and calculate dose)
            shiftTB.Background = System.Windows.Media.Brushes.ForestGreen;
            shiftTB.Text = "YES";

            if(prep.isoPositions.Count() > 1)
            {
                separatePlans.Visibility = Visibility.Visible;
                separateTB.Visibility = Visibility.Visible;
            }
        }

        private ExternalPlanSetup getPlan()
        {
            //this method assumes no prior knowledge, so it will have to retrive the number of isocenters (vmat and total) and isocenter names explicitly
            Course c = context.Course;
            ExternalPlanSetup plan = null;
            if (c == null)
            {
                //vmat tbi course not found. Dealbreaker, exit method
                MessageBox.Show("No plan/course open! Open a plan and try again!");
                return plan;
            }
            else
            {
                //get all plans in the course that don't have the ID "_Legs". If more than 1 exists, prompt the user to select the plan they want to prep
                IEnumerable<ExternalPlanSetup> plans = c.ExternalPlanSetups;
                if (plans.Count() > 1)
                {
                    var SUI = new planPrepper.selectSingleItem();
                    SUI.title.Text = "Multiple plans found in this course!" + Environment.NewLine + "Please select a plan to prep!";
                    foreach (ExternalPlanSetup p in plans) SUI.planCombo.Items.Add(p.Id);
                    if(context.ExternalPlanSetup != null) SUI.planCombo.Text = context.ExternalPlanSetup.Id;
                    SUI.ShowDialog();
                    if (!SUI.confirm) return null;
                    //get the plan the user chose from the combobox
                    plan = c.ExternalPlanSetups.FirstOrDefault(x => x.Id == SUI.planCombo.SelectedItem.ToString());
                }
                else
                {
                    //course found and only one or fewer plans inside course with Id != "_Legs", get vmat and ap/pa plans
                    plan = plans.FirstOrDefault();
                }
                if (plan == null)
                {
                    //vmat plan not found. Dealbreaker, exit method
                    MessageBox.Show("No plan found!");
                    return plan;
                }
            }
            return plan;
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

            if (prep.recalcNeeded)
            {
                calcDose.Visibility = Visibility.Visible;
                calcDoseTB.Visibility = Visibility.Visible;
            }
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
            confirmUI CUI = new planPrepper.confirmUI();
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

        private void RunAPC_Click(object sender, RoutedEventArgs e)
        {
            //System.Diagnostics.Process.Start(@"\\shariapfcap103\va_data$\ProgramData\Vision\PublishedScripts\APC_15.6_v3.1.esapi.dll");
            //DllHelper.Script();
            //DllHelper.Execute(context);
        }

        private void exportToMobius_Click(object sender, RoutedEventArgs e)
        {
            //working on it
        }

        private void planSum_Click(object sender, RoutedEventArgs e)
        {
            //do nothing. Eclipse v15.6 doesn't have this capability, but v16 and later does. This method is a placeholder
        }
    }
}
