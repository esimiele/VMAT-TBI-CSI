using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Helpers;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class OptimizationSetupUIHelper
    {
        /// <summary>
        /// Helper method to take the supplied plan and read the optimization constraints attached to the plan. Returns the list of 
        /// optimization constraints
        /// </summary>
        /// <param name="plan"></param>
        /// <returns></returns>
        public static List<Tuple<string, OptimizationObjectiveType, double, double, int>> ReadConstraintsFromPlan(ExternalPlanSetup plan)
        {
            //grab the optimization constraints in the existing VMAT TBI plan and display them to the user
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> defaultList = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
            foreach (OptimizationObjective itr in plan.OptimizationSetup.Objectives)
            {
                //do NOT include any cooler or heater tuning structures in the list
                if (!itr.StructureId.ToLower().Contains("ts_cooler") && !itr.StructureId.ToLower().Contains("ts_heater"))
                {
                    if (itr.GetType() == typeof(OptimizationPointObjective))
                    {
                        OptimizationPointObjective pt = (itr as OptimizationPointObjective);
                        defaultList.Add(Tuple.Create(pt.StructureId, OptimizationTypeHelper.GetObjectiveType(pt), pt.Dose.Dose, pt.Volume, (int)pt.Priority));
                    }
                    else if (itr.GetType() == typeof(OptimizationMeanDoseObjective))
                    {
                        OptimizationMeanDoseObjective mean = (itr as OptimizationMeanDoseObjective);
                        defaultList.Add(Tuple.Create(mean.StructureId, OptimizationObjectiveType.Mean, mean.Dose.Dose, 0.0, (int)mean.Priority));
                    }
                }
            }
            return defaultList;
        }

        /// <summary>
        /// Helper method to take the supplied optimization constraints and rescale the dose objectives by the ratio of the supplied prescription doses (new Rx/ old Rx)
        /// </summary>
        /// <param name="currentList"></param>
        /// <param name="oldRx"></param>
        /// <param name="newRx"></param>
        /// <returns></returns>
        public static List<Tuple<string, OptimizationObjectiveType, double, double, int>> RescalePlanObjectivesToNewRx(List<Tuple<string, OptimizationObjectiveType, double, double, int>> currentList,
                                                                                                                       double oldRx,
                                                                                                                       double newRx)
        {
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> tmpList = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
            foreach(Tuple<string, OptimizationObjectiveType, double, double, int> itr in currentList)
            {
                tmpList.Add(new Tuple<string, OptimizationObjectiveType, double, double, int>(itr.Item1, itr.Item2, itr.Item3 * newRx / oldRx, itr.Item4, itr.Item5));
            }
            return tmpList;
        }

        /// <summary>
        /// Helper method to control the flow of adding additional optimization constraints to the supplied list. Additional constraints are added for TS targets, TS manipulations, adding rings, and adding overlap junctions
        /// </summary>
        /// <param name="defaultListList"></param>
        /// <param name="prescriptions"></param>
        /// <param name="selectedTemplate"></param>
        /// <param name="tsTargets"></param>
        /// <param name="jnxs"></param>
        /// <param name="targetManipulations"></param>
        /// <param name="addedRings"></param>
        /// <returns></returns>
        public static List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> UpdateOptObjectivesWithTsStructuresAndJnxs(List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> defaultListList,
                                                                                                                                                          List<Tuple<string, string, int, DoseValue, double>> prescriptions,
                                                                                                                                                          object selectedTemplate,
                                                                                                                                                          List<Tuple<string, List<Tuple<string, string>>>> tsTargets,
                                                                                                                                                          List<Tuple<ExternalPlanSetup, List<Structure>>> jnxs,
                                                                                                                                                          List<Tuple<string, string, List<Tuple<string, string>>>> targetManipulations = null,
                                                                                                                                                          List<Tuple<string, string, double>> addedRings = null)
        {
            if (tsTargets.Any())
            {
                //handles if crop/overlap operations were performed for all targets and the optimization constraints need to be updated
                defaultListList = OptimizationSetupHelper.UpdateOptimizationConstraints(tsTargets, prescriptions, selectedTemplate, defaultListList);
            }
            if (targetManipulations != null && targetManipulations.Any())
            {
                //handles if crop/overlap operations were performed for all targets and the optimization constraints need to be updated
                defaultListList = OptimizationSetupHelper.UpdateOptimizationConstraints(targetManipulations, prescriptions, selectedTemplate, defaultListList);
            }
            if (addedRings != null && addedRings.Any())
            {
                defaultListList = OptimizationSetupHelper.UpdateOptimizationConstraints(addedRings, prescriptions, selectedTemplate, defaultListList);
            }
            if (jnxs.Any())
            {
                defaultListList = OptimizationSetupHelper.InsertTSJnxOptConstraints(defaultListList, jnxs, prescriptions);
            }
            return defaultListList;
        }

        /// <summary>
        /// Simple helper method to remove all optimization constraints from the supplied plan
        /// </summary>
        /// <param name="plan"></param>
        /// <returns></returns>
        public static bool RemoveOptimizationConstraintsFromPLan(ExternalPlanSetup plan)
        {
            if (plan.OptimizationSetup.Objectives.Count() > 0)
            {
                foreach (OptimizationObjective o in plan.OptimizationSetup.Objectives) plan.OptimizationSetup.RemoveObjective(o);
            }
            return false;
        }

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
        /// <typeparam name="T"></typeparam>
        /// <param name="theSP"></param>
        /// <param name="selectedSS"></param>
        /// <param name="listItem"></param>
        /// <param name="clearBtnNamePrefix"></param>
        /// <param name="clearOptBtnCounter"></param>
        /// <param name="e"></param>
        /// <param name="addStructureEvenIfNotInSS"></param>
        /// <returns></returns>
        public static StackPanel AddOptVolume<T>(StackPanel theSP, 
                                                 StructureSet selectedSS, 
                                                 Tuple<string, OptimizationObjectiveType, double, double, T> listItem, 
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
                if (s.Id.ToLower() == listItem.Item1.ToLower()) index = j;
                j++;
            }
            if (addStructureEvenIfNotInSS && !selectedSS.Structures.Any(x => string.Equals(x.Id.ToLower(), listItem.Item1.ToLower())))
            {
                opt_str_cb.Items.Add(listItem.Item1);
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
            if ((int)listItem.Item2 <= constraint_cb.Items.Count && listItem.Item2 != OptimizationObjectiveType.None) constraint_cb.SelectedIndex = (int)listItem.Item2;
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
                Text = String.Format("{0:0.#}", listItem.Item4),
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
                Text = String.Format("{0:0.#}", listItem.Item3),
                TextAlignment = TextAlignment.Center
            };
            sp.Children.Add(dose_tb);

            if(listItem.Item5.GetType() == typeof(int))
            {
                TextBox priority_tb = new TextBox
                {
                    Name = "priority_tb",
                    Width = 65,
                    Height = sp.Height - 5,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(5, 5, 0, 0),
                    Text = Convert.ToString(listItem.Item5),
                    TextAlignment = TextAlignment.Center
                };
                sp.Children.Add(priority_tb);
            }
            else
            {
                TextBox dvPresentation_tb = new TextBox
                {
                    Name = "dvPresentation_tb",
                    Width = 65,
                    Height = sp.Height - 5,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(5, 5, 0, 0),
                    Text = Convert.ToString(listItem.Item5) == "Absolute" ? "cGy" : "%",
                    TextAlignment = TextAlignment.Center
                };
                sp.Children.Add(dvPresentation_tb);
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
        public static (List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>, StringBuilder) ParseOptConstraints(StackPanel sp, bool checkInputIntegrity = true)
        {
            StringBuilder sb = new StringBuilder();
            if (sp.Children.Count == 0)
            {
                sb.AppendLine("No optimization parameters present to assign to plans!");
                return (new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>(), sb);
            }

            //get constraints
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> optParametersList = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> optParametersListList = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };
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
                        optParametersListList.Add(new Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>(planId, new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(optParametersList)));
                        optParametersList = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
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
                        return (new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>(), sb);
                    }
                    else if (checkInputIntegrity && (dose == -1.0 || vol == -1.0 || priority == -1.0))
                    {
                        sb.AppendLine("Error! \nDose, volume, or priority values are invalid! \nEnter new values and try again");
                        return (new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>(), sb);
                    }
                    //if the row of data passes the above checks, add it the optimization parameter list
                    else optParametersList.Add(Tuple.Create(structure, OptimizationTypeHelper.GetObjectiveType(constraintType), Math.Round(dose, 3, MidpointRounding.AwayFromZero), Math.Round(vol, 3, MidpointRounding.AwayFromZero), priority));
                    //reset the values of the variables used to parse the data
                    firstCombo = true;
                    txtBxNum = 1;
                    dose = -1.0;
                    vol = -1.0;
                    priority = -1;
                }
                numElementsPerRow = 0;
            }
            optParametersListList.Add(new Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>(planId, new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(optParametersList)));
            return (optParametersListList, sb);
        }

        /// <summary>
        /// Helper method to take the supplied optimization constaints and assign them to the supplied plan. Jaw tracking and NTO priority are also assigned
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="VMATplan"></param>
        /// <param name="useJawTracking"></param>
        /// <param name="NTOpriority"></param>
        /// <returns></returns>
        public static (bool, StringBuilder) AssignOptConstraints(List<Tuple<string, OptimizationObjectiveType, double, double, int>> parameters, 
                                                                 ExternalPlanSetup VMATplan, 
                                                                 bool useJawTracking, 
                                                                 double NTOpriority)
        {
            bool isError = false;
            StringBuilder sb = new StringBuilder();
            foreach (Tuple<string, OptimizationObjectiveType, double, double, int> opt in parameters)
            {
                Structure s =StructureTuningHelper.GetStructureFromId(opt.Item1, VMATplan.StructureSet);
                if (opt.Item2 != OptimizationObjectiveType.Mean) VMATplan.OptimizationSetup.AddPointObjective(s, OptimizationTypeHelper.GetObjectiveOperator(opt.Item2), new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), opt.Item4, (double)opt.Item5);
                else VMATplan.OptimizationSetup.AddMeanDoseObjective(s, new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), (double)opt.Item5);
            }
            //turn on/turn off jaw tracking
            try { VMATplan.OptimizationSetup.UseJawTracking = useJawTracking; }
            catch (Exception except) 
            { 
                sb.AppendLine($"Warning! Could not set jaw tracking for VMAT plan because: {except.Message}"); 
                sb.AppendLine("Jaw tacking will have to be set manually!"); 
            }
            //set auto NTO priority to zero (i.e., shut it off). It has to be done this way because every plan created in ESAPI has an instance of an automatic NTO, which CAN'T be deleted.
            VMATplan.OptimizationSetup.AddAutomaticNormalTissueObjective(NTOpriority);
            return (isError, sb);
        }
    }
}
