using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.Models;
using VMATTBICSIAutoPlanningHelpers.Interfaces;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class OptimizationSetupUIHelper
    {
        

        /// <summary>
        /// Helper method to add the supplied plan Id to preceed the header information in the Optimization Setup tab (useful for sequential boost CSI plans)
        /// </summary>
        /// <param name="theSP"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static StackPanel AddPlanIdtoOptList(StackPanel theSP, string id)
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = theSP.Width,
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(15, 0, 5, 5)
            };

            Label strName = new Label
            {
                Content = $"Plan Id: {id}",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = theSP.Width,
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };

            sp.Children.Add(strName);
            return sp;
        }

        /// <summary>
        /// Helper method to build the header information for the Optimization Setup tab
        /// </summary>
        /// <param name="theWidth"></param>
        /// <returns></returns>
        public static StackPanel GetOptHeader(double theWidth)
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = theWidth,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(30, 0, 5, 5)
            };

            Label strName = new Label
            {
                Content = "Structure",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 110,
                FontSize = 14,
                Margin = new Thickness(27, 0, 0, 0)
            };

            Label spareType = new Label
            {
                Content = "Constraint",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 90,
                FontSize = 14,
                Margin = new Thickness(2, 0, 0, 0)
            };

            Label volLabel = new Label();
            volLabel.Content = "V (%)";
            volLabel.HorizontalAlignment = HorizontalAlignment.Center;
            volLabel.VerticalAlignment = VerticalAlignment.Top;
            volLabel.Width = 60;
            volLabel.FontSize = 14;
            volLabel.Margin = new Thickness(18, 0, 0, 0);

            Label doseLabel = new Label
            {
                Content = "D (cGy)",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 60,
                FontSize = 14,
                Margin = new Thickness(3, 0, 0, 0)
            };

            Label priorityLabel = new Label
            {
                Content = "Priority",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 65,
                FontSize = 14,
                Margin = new Thickness(13, 0, 0, 0)
            };

            sp.Children.Add(strName);
            sp.Children.Add(spareType);
            sp.Children.Add(volLabel);
            sp.Children.Add(doseLabel);
            sp.Children.Add(priorityLabel);
            return sp;
        }

        /// <summary>
        /// Helper method to add the optimization constraint item to the list on the Optimization Setup tab
        /// </summary>
        /// <param name="theSP"></param>
        /// <param name="selectedSS"></param>
        /// <param name="listItem"></param>
        /// <param name="clearBtnNamePrefix"></param>
        /// <param name="clearOptBtnCounter"></param>
        /// <param name="e"></param>
        /// <param name="addStructureEvenIfNotInSS"></param>
        /// <returns></returns>
        public static StackPanel AddOptVolume(StackPanel theSP, 
                                                 StructureSet selectedSS, 
                                                 IPlanConstraint listItem, 
                                                 string clearBtnNamePrefix, 
                                                 int clearOptBtnCounter, 
                                                 RoutedEventHandler e, 
                                                 bool addStructureEvenIfNotInSS = false)
        {
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = theSP.Width,
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(30, 5, 5, 5)
            };

            ComboBox opt_str_cb = new ComboBox
            {
                Name = "opt_str_cb",
                Width = 120,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(5, 5, 0, 0)
            };

            opt_str_cb.Items.Add("--select--");
            //this code is used to fix the issue where the structure exists in the structure set, but doesn't populate as the default option in the combo box.
            int index = 0;
            //j is initially 1 because we already added "--select--" to the combo box 
            int j = 1;
            foreach (Structure s in selectedSS.Structures)
            {
                opt_str_cb.Items.Add(s.Id);
                if (s.Id.ToLower() == listItem.StructureId.ToLower()) index = j;
                j++;
            }
            if (addStructureEvenIfNotInSS && !selectedSS.Structures.Any(x => string.Equals(x.Id.ToLower(), listItem.StructureId.ToLower())))
            {
                opt_str_cb.Items.Add(listItem.StructureId);
                opt_str_cb.SelectedIndex = opt_str_cb.Items.Count - 1;
            }
            else opt_str_cb.SelectedIndex = index;
            opt_str_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            sp.Children.Add(opt_str_cb);

            ComboBox constraint_cb = new ComboBox
            {
                Name = "type_cb",
                Width = 100,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(5, 5, 0, 0)
            };
            //add the possible optimization objective types to the combo box
            foreach (OptimizationObjectiveType s in Enum.GetValues(typeof(OptimizationObjectiveType))) constraint_cb.Items.Add(s);
            if ((int)listItem.ConstraintType <= constraint_cb.Items.Count && listItem.ConstraintType != OptimizationObjectiveType.None) constraint_cb.SelectedIndex = (int)listItem.ConstraintType;
            else constraint_cb.SelectedIndex = 0;
            constraint_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            sp.Children.Add(constraint_cb);

            //the order of the dose and volume values are switched when they are displayed to the user. This way, the optimization objective appears to the user as it would in the optimization workspace.
            //However, due to the way ESAPI assigns optimization objectives via VMATplan.OptimizationSetup.AddPointObjective, they need to be stored in the order listed in the templates above
            TextBox vol_tb = new TextBox
            {
                Name = "vol_tb",
                Width = 65,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(5, 5, 0, 0),
                Text = String.Format("{0:0.#}", listItem.QueryVolume),
                TextAlignment = TextAlignment.Center
            };
            sp.Children.Add(vol_tb);

            TextBox dose_tb = new TextBox
            {
                Name = "dose_tb",
                Width = 70,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(5, 5, 0, 0),
                Text = String.Format("{0:0.#}", listItem.QueryDose),
                TextAlignment = TextAlignment.Center
            };
            sp.Children.Add(dose_tb);

            if(listItem.GetType() == typeof(PlanObjectiveModel))
            {
                TextBox dvPresentation_tb = new TextBox
                {
                    Name = "dvPresentation_tb",
                    Width = 65,
                    Height = sp.Height - 5,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(5, 5, 0, 0),
                    Text = (listItem as PlanObjectiveModel).QueryDoseUnits.ToString(),
                    TextAlignment = TextAlignment.Center
                };
                sp.Children.Add(dvPresentation_tb); 
            }
            else
            {
                TextBox priority_tb = new TextBox
                {
                    Name = "priority_tb",
                    Width = 65,
                    Height = sp.Height - 5,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(5, 5, 0, 0),
                    Text = Convert.ToString((listItem as OptimizationConstraintModel).Priority),
                    TextAlignment = TextAlignment.Center
                };
                sp.Children.Add(priority_tb);
            }

            Button clearOptStructBtn = new Button
            {
                Name = clearBtnNamePrefix + clearOptBtnCounter,
                Content = "Clear",
                Width = 50,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 5, 0, 0)
            };
            clearOptStructBtn.Click += e;
            sp.Children.Add(clearOptStructBtn);

            return sp;
        }

        /// <summary>
        /// Helper method to parse the optimization constraints from the Optimization Setup tab. Works for both initial-only and sequential boost plans
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="checkInputIntegrity"></param>
        /// <returns></returns>
        public static (List<PlanOptimizationSetupModel>, StringBuilder) ParseOptConstraints(StackPanel sp, bool checkInputIntegrity = true)
        {
            StringBuilder sb = new StringBuilder();
            if (sp.Children.Count == 0)
            {
                sb.AppendLine("No optimization parameters present to assign to plans!");
                return (new List<PlanOptimizationSetupModel>(), sb);
            }

            //get constraints
            List<OptimizationConstraintModel> optParametersList = new List<OptimizationConstraintModel> { };
            List<PlanOptimizationSetupModel> optParametersListList = new List<PlanOptimizationSetupModel> { };
            string structure = "";
            string constraintType = "";
            double dose = -1.0;
            double vol = -1.0;
            int priority = -1;
            int txtBxNum = 1;
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
                            if (txtBxNum == 1) double.TryParse((obj1 as TextBox).Text, out vol);
                            //second text box is the dose constraint
                            else if (txtBxNum == 2) double.TryParse((obj1 as TextBox).Text, out dose);
                            //third text box is the priority
                            else int.TryParse((obj1 as TextBox).Text, out priority);
                        }
                        txtBxNum++;
                    }
                    else if (obj1.GetType() == typeof(Label)) copyObj = obj1;
                    numElementsPerRow++;
                }
                if (numElementsPerRow == 1)
                {
                    if (optParametersList.Any())
                    {
                        optParametersListList.Add(new PlanOptimizationSetupModel(planId, new List<OptimizationConstraintModel>(optParametersList)));
                        optParametersList = new List<OptimizationConstraintModel> { };
                    }
                    string planIdHeader = (copyObj as Label).Content.ToString();
                    planId = planIdHeader.Substring(planIdHeader.IndexOf(":") + 2, planIdHeader.Length - planIdHeader.IndexOf(":") - 2);
                }
                else if (numElementsPerRow != 5)
                {
                    //do some checks to ensure the integrity of the data
                    if (checkInputIntegrity && (structure == "--select--" || constraintType == "--select--"))
                    {
                        sb.AppendLine("Error! \nStructure or Sparing Type not selected! \nSelect an option and try again");
                        return (new List<PlanOptimizationSetupModel>(), sb);
                    }
                    else if (checkInputIntegrity && (dose == -1.0 || vol == -1.0 || priority == -1.0))
                    {
                        sb.AppendLine("Error! \nDose, volume, or priority values are invalid! \nEnter new values and try again");
                        return (new List<PlanOptimizationSetupModel>(), sb);
                    }
                    //if the row of data passes the above checks, add it the optimization parameter list
                    else optParametersList.Add(new OptimizationConstraintModel(structure, OptimizationTypeHelper.GetObjectiveType(constraintType), Math.Round(dose, 3, MidpointRounding.AwayFromZero), Units.cGy, Math.Round(vol, 3, MidpointRounding.AwayFromZero), priority));
                    //reset the values of the variables used to parse the data
                    firstCombo = true;
                    txtBxNum = 1;
                    dose = -1.0;
                    vol = -1.0;
                    priority = -1;
                }
                numElementsPerRow = 0;
            }
            optParametersListList.Add(new PlanOptimizationSetupModel(planId, new List<OptimizationConstraintModel>(optParametersList)));
            return (optParametersListList, sb);
        }

        
    }
}
