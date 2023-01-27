using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATAutoPlanMT.helpers
{
    class OptimizationSetupUIHelper
    {
        public StackPanel AddPlanIdtoOptList(StackPanel theSP, string id)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Margin = new Thickness(15, 0, 5, 5);

            Label strName = new Label();
            strName.Content = String.Format("Plan Id: {0}", id);
            strName.HorizontalAlignment = HorizontalAlignment.Center;
            strName.VerticalAlignment = VerticalAlignment.Top;
            strName.Width = theSP.Width;
            strName.FontSize = 14;
            strName.FontWeight = FontWeights.Bold;

            sp.Children.Add(strName);
            return sp;
        }

        public StackPanel getOptHeader(double theWidth)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theWidth;
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(30, 0, 5, 5);

            Label strName = new Label();
            strName.Content = "Structure";
            strName.HorizontalAlignment = HorizontalAlignment.Center;
            strName.VerticalAlignment = VerticalAlignment.Top;
            strName.Width = 110;
            strName.FontSize = 14;
            strName.Margin = new Thickness(27, 0, 0, 0);

            Label spareType = new Label();
            spareType.Content = "Constraint";
            spareType.HorizontalAlignment = HorizontalAlignment.Center;
            spareType.VerticalAlignment = VerticalAlignment.Top;
            spareType.Width = 90;
            spareType.FontSize = 14;
            spareType.Margin = new Thickness(2, 0, 0, 0);

            Label volLabel = new Label();
            volLabel.Content = "V (%)";
            volLabel.HorizontalAlignment = HorizontalAlignment.Center;
            volLabel.VerticalAlignment = VerticalAlignment.Top;
            volLabel.Width = 60;
            volLabel.FontSize = 14;
            volLabel.Margin = new Thickness(18, 0, 0, 0);

            Label doseLabel = new Label();
            doseLabel.Content = "D (cGy)";
            doseLabel.HorizontalAlignment = HorizontalAlignment.Center;
            doseLabel.VerticalAlignment = VerticalAlignment.Top;
            doseLabel.Width = 60;
            doseLabel.FontSize = 14;
            doseLabel.Margin = new Thickness(3, 0, 0, 0);

            Label priorityLabel = new Label();
            priorityLabel.Content = "Priority";
            priorityLabel.HorizontalAlignment = HorizontalAlignment.Center;
            priorityLabel.VerticalAlignment = VerticalAlignment.Top;
            priorityLabel.Width = 65;
            priorityLabel.FontSize = 14;
            priorityLabel.Margin = new Thickness(13, 0, 0, 0);

            sp.Children.Add(strName);
            sp.Children.Add(spareType);
            sp.Children.Add(volLabel);
            sp.Children.Add(doseLabel);
            sp.Children.Add(priorityLabel);
            return sp;
        }

        public StackPanel addOptVolume(StackPanel theSP, StructureSet selectedSS, Tuple<string, string, double, double, int> listItem, string clearBtnNamePrefix, int clearOptBtnCounter, RoutedEventHandler e, bool addStructureEvenIfNotInSS = false)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(30, 5, 5, 5);

            ComboBox opt_str_cb = new ComboBox();
            opt_str_cb.Name = "opt_str_cb";
            opt_str_cb.Width = 120;
            opt_str_cb.Height = sp.Height - 5;
            opt_str_cb.HorizontalAlignment = HorizontalAlignment.Left;
            opt_str_cb.VerticalAlignment = VerticalAlignment.Top;
            opt_str_cb.Margin = new Thickness(5, 5, 0, 0);

            opt_str_cb.Items.Add("--select--");
            //this code is used to fix the issue where the structure exists in the structure set, but doesn't populate as the default option in the combo box.
            int index = 0;
            //j is initially 1 because we already added "--select--" to the combo box 
            int j = 1;
            foreach (Structure s in selectedSS.Structures)
            {
                opt_str_cb.Items.Add(s.Id);
                if (s.Id.ToLower() == listItem.Item1.ToLower()) index = j;
                j++;
            }
            if (addStructureEvenIfNotInSS && selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == listItem.Item1.ToLower()) == null)
            {
                opt_str_cb.Items.Add(listItem.Item1);
                opt_str_cb.SelectedIndex = opt_str_cb.Items.Count - 1;
            }
            else opt_str_cb.SelectedIndex = index;
            opt_str_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            sp.Children.Add(opt_str_cb);

            ComboBox constraint_cb = new ComboBox();
            constraint_cb.Name = "type_cb";
            constraint_cb.Width = 100;
            constraint_cb.Height = sp.Height - 5;
            constraint_cb.HorizontalAlignment = HorizontalAlignment.Left;
            constraint_cb.VerticalAlignment = VerticalAlignment.Top;
            constraint_cb.Margin = new Thickness(5, 5, 0, 0);
            string[] types = new string[] { "--select--", "Upper", "Lower", "Mean", "Exact" };
            foreach (string s in types) constraint_cb.Items.Add(s);
            constraint_cb.Text = listItem.Item2;
            constraint_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            sp.Children.Add(constraint_cb);

            //the order of the dose and volume values are switched when they are displayed to the user. This way, the optimization objective appears to the user as it would in the optimization workspace.
            //However, due to the way ESAPI assigns optimization objectives via VMATplan.OptimizationSetup.AddPointObjective, they need to be stored in the order listed in the templates above
            TextBox dose_tb = new TextBox();
            dose_tb.Name = "dose_tb";
            dose_tb.Width = 65;
            dose_tb.Height = sp.Height - 5;
            dose_tb.HorizontalAlignment = HorizontalAlignment.Left;
            dose_tb.VerticalAlignment = VerticalAlignment.Top;
            dose_tb.Margin = new Thickness(5, 5, 0, 0);
            dose_tb.Text = String.Format("{0:0.#}", listItem.Item4);
            dose_tb.TextAlignment = TextAlignment.Center;
            sp.Children.Add(dose_tb);

            TextBox vol_tb = new TextBox();
            vol_tb.Name = "vol_tb";
            vol_tb.Width = 70;
            vol_tb.Height = sp.Height - 5;
            vol_tb.HorizontalAlignment = HorizontalAlignment.Left;
            vol_tb.VerticalAlignment = VerticalAlignment.Top;
            vol_tb.Margin = new Thickness(5, 5, 0, 0);
            vol_tb.Text = String.Format("{0:0.#}", listItem.Item3);
            vol_tb.TextAlignment = TextAlignment.Center;
            sp.Children.Add(vol_tb);

            TextBox priority_tb = new TextBox();
            priority_tb.Name = "priority_tb";
            priority_tb.Width = 65;
            priority_tb.Height = sp.Height - 5;
            priority_tb.HorizontalAlignment = HorizontalAlignment.Left;
            priority_tb.VerticalAlignment = VerticalAlignment.Top;
            priority_tb.Margin = new Thickness(5, 5, 0, 0);
            priority_tb.Text = Convert.ToString(listItem.Item5);
            priority_tb.TextAlignment = TextAlignment.Center;
            sp.Children.Add(priority_tb);

            Button clearOptStructBtn = new Button();
            clearOptStructBtn.Name = clearBtnNamePrefix + clearOptBtnCounter;
            clearOptStructBtn.Content = "Clear";
            clearOptStructBtn.Width = 50;
            clearOptStructBtn.Height = sp.Height - 5;
            clearOptStructBtn.HorizontalAlignment = HorizontalAlignment.Left;
            clearOptStructBtn.VerticalAlignment = VerticalAlignment.Top;
            clearOptStructBtn.Margin = new Thickness(10, 5, 0, 0);
            clearOptStructBtn.Click += e;
            sp.Children.Add(clearOptStructBtn);

            return sp;
        }

        public List<Tuple<string, List<Tuple<string, string, double, double, int>>>> parseOptConstraints(StackPanel sp, bool checkInputIntegrity = true)
        {
            if (sp.Children.Count == 0)
            {
                System.Windows.Forms.MessageBox.Show("No optimization parameters present to assign to plans!");
                return new List<Tuple<string, List<Tuple<string, string, double, double, int>>>>();
            }

            //get constraints
            List<Tuple<string, string, double, double, int>> optParametersList = new List<Tuple<string, string, double, double, int>> { };
            List<Tuple<string, List<Tuple<string, string, double, double, int>>>> optParametersListList = new List<Tuple<string, List<Tuple<string, string, double, double, int>>>> { };
            string structure = "";
            string constraintType = "";
            double dose = -1.0;
            double vol = -1.0;
            int priority = -1;
            int txtbxNum = 1;
            bool firstCombo = true;
            //bool headerObj = true;
            int numElementsPerRow = 0;
            string planId = "";
            object copyObj = null;
            foreach (object obj in sp.Children)
            {
                foreach (object obj1 in ((StackPanel)obj).Children)
                {
                    if (obj1.GetType() == typeof(ComboBox))
                    {
                        if (firstCombo)
                        {
                            //first combobox is the structure
                            structure = (obj1 as ComboBox).SelectedItem.ToString();
                            firstCombo = false;
                        }
                        //second combobox is the constraint type
                        else constraintType = (obj1 as ComboBox).SelectedItem.ToString();
                    }
                    else if (obj1.GetType() == typeof(TextBox))
                    {
                        if (!string.IsNullOrWhiteSpace((obj1 as TextBox).Text))
                        {
                            //first text box is the volume percentage
                            if (txtbxNum == 1) double.TryParse((obj1 as TextBox).Text, out vol);
                            //second text box is the dose constraint
                            else if (txtbxNum == 2) double.TryParse((obj1 as TextBox).Text, out dose);
                            //third text box is the priority
                            else int.TryParse((obj1 as TextBox).Text, out priority);
                        }
                        txtbxNum++;
                    }
                    else if (obj1.GetType() == typeof(Label)) copyObj = obj1;
                    numElementsPerRow++;
                }
                if (numElementsPerRow == 1)
                {
                    if (optParametersList.Any())
                    {
                        optParametersListList.Add(new Tuple<string, List<Tuple<string, string, double, double, int>>>(planId, new List<Tuple<string, string, double, double, int>>(optParametersList)));
                        optParametersList = new List<Tuple<string, string, double, double, int>> { };
                    }
                    planId = (copyObj as Label).Content.ToString().Substring((copyObj as Label).Content.ToString().IndexOf(":") + 2, (copyObj as Label).Content.ToString().Length - (copyObj as Label).Content.ToString().IndexOf(":") - 2);
                }
                else if (numElementsPerRow != 5)
                {
                    //do some checks to ensure the integrity of the data
                    if (checkInputIntegrity && (structure == "--select--" || constraintType == "--select--"))
                    {
                        System.Windows.Forms.MessageBox.Show("Error! \nStructure or Sparing Type not selected! \nSelect an option and try again");
                        return new List<Tuple<string, List<Tuple<string, string, double, double, int>>>>();
                    }
                    else if (checkInputIntegrity && (dose == -1.0 || vol == -1.0 || priority == -1.0))
                    {
                        System.Windows.Forms.MessageBox.Show("Error! \nDose, volume, or priority values are invalid! \nEnter new values and try again");
                        return new List<Tuple<string, List<Tuple<string, string, double, double, int>>>>();
                    }
                    //if the row of data passes the above checks, add it the optimization parameter list
                    else optParametersList.Add(Tuple.Create(structure, constraintType, dose, vol, priority));
                    //reset the values of the variables used to parse the data
                    firstCombo = true;
                    txtbxNum = 1;
                    dose = -1.0;
                    vol = -1.0;
                    priority = -1;
                }
                numElementsPerRow = 0;
            }
            optParametersListList.Add(new Tuple<string, List<Tuple<string, string, double, double, int>>>(planId, new List<Tuple<string, string, double, double, int>>(optParametersList)));
            return optParametersListList;
        }

        public bool AssignOptConstraints(List<Tuple<string, string, double, double, int>> parameters, ExternalPlanSetup VMATplan, bool useJawTracking, double NTOpriority)
        {
            bool isError = false;
            foreach (Tuple<string, string, double, double, int> opt in parameters)
            {
                //assign the constraints to the plan. I haven't found a use for the exact constraint yet, so I just wrote the script to throw a warning if the exact constraint was selected (that row of data will NOT be
                //assigned to the VMAT plan)
                if (opt.Item2 == "Upper") VMATplan.OptimizationSetup.AddPointObjective(VMATplan.StructureSet.Structures.First(x => x.Id == opt.Item1), OptimizationObjectiveOperator.Upper, new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), opt.Item4, (double)opt.Item5);
                else if (opt.Item2 == "Lower") VMATplan.OptimizationSetup.AddPointObjective(VMATplan.StructureSet.Structures.First(x => x.Id == opt.Item1), OptimizationObjectiveOperator.Lower, new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), opt.Item4, (double)opt.Item5);
                else if (opt.Item2 == "Mean") VMATplan.OptimizationSetup.AddMeanDoseObjective(VMATplan.StructureSet.Structures.First(x => x.Id == opt.Item1), new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), (double)opt.Item5);
                else if (opt.Item2 == "Exact") System.Windows.Forms.MessageBox.Show("Script not setup to handle exact dose constraints!");
                else { System.Windows.Forms.MessageBox.Show("Constraint type not recognized!"); isError = true; }
            }
            //turn on/turn off jaw tracking
            try { VMATplan.OptimizationSetup.UseJawTracking = useJawTracking; }
            catch (Exception except) { System.Windows.Forms.MessageBox.Show(String.Format("Warning! Could not set jaw tracking for VMAT plan because: {0}\nJaw tacking will have to be set manually!", except.Message)); }
            //set auto NTO priority to zero (i.e., shut it off). It has to be done this way because every plan created in ESAPI has an instance of an automatic NTO, which CAN'T be deleted.
            VMATplan.OptimizationSetup.AddAutomaticNormalTissueObjective(NTOpriority);
            return isError;
        }
    }
}
