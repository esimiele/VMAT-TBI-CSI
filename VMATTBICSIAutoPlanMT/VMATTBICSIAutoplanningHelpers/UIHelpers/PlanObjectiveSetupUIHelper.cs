using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class PlanObjectiveSetupUIHelper
    {
        /// <summary>
        /// Helper method to generate the header information for the plan objectives tab on the main optimization loop UI
        /// </summary>
        /// <param name="theWidth"></param>
        /// <returns></returns>
        public static StackPanel GetObjHeader(double theWidth)
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

            Label volLabel = new Label
            {
                Content = "V (%)",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 60,
                FontSize = 14,
                Margin = new Thickness(18, 0, 0, 0)
            };

            Label doseLabel = new Label
            {
                Content = "Dose",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 60,
                FontSize = 14,
                Margin = new Thickness(5, 0, 0, 0)
            };

            Label unitsLabel = new Label
            {
                Content = "Units",
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
            sp.Children.Add(unitsLabel);
            return sp;
        }

        /// <summary>
        /// Helper method to parse the plan objective list from the main optimization loop UI
        /// </summary>
        /// <param name="theSP"></param>
        /// <returns></returns>
        public static List<PlanObjectiveModel> ParsePlanObjectives(StackPanel theSP)
        {
            //get constraints
            List<PlanObjectiveModel> tmp = new List<PlanObjectiveModel> { };
            string structure = "";
            string constraintType = "";
            double dose = -1.0;
            double vol = -1.0;
            int txtbxNum = 1;
            bool firstCombo = true;
            bool headerObj = true;
            foreach (object obj in theSP.Children)
            {
                //skip over header row
                if (!headerObj)
                {
                    Units presentation = Units.Percent;
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
                                //third text box is the dose value presentation
                                else
                                {
                                    if ((obj1 as TextBox).Text.Contains("cGy")) presentation = Units.cGy;
                                }
                            }
                            txtbxNum++;
                        }
                    }
                    //do some checks to ensure the integrity of the data
                    if (structure == "--select--" || constraintType == "--select--")
                    {
                        MessageBox.Show("Error! \nStructure or Sparing Type not selected! \nSelect an option and try again");
                        return new List<PlanObjectiveModel>{};
                    }
                    else if (dose == -1.0 || vol == -1.0)
                    {
                        MessageBox.Show("Error! \nDose, volume, or priority values are invalid! \nEnter new values and try again");
                        return new List<PlanObjectiveModel> { };
                    }
                    //if the row of data passes the above checks, add it the optimization parameter list
                    else tmp.Add(new PlanObjectiveModel(structure, OptimizationTypeHelper.GetObjectiveType(constraintType), Math.Round(dose, 3, MidpointRounding.AwayFromZero), presentation, Math.Round(vol, 3, MidpointRounding.AwayFromZero)));
                    //reset the values of the variables used to parse the data
                    firstCombo = true;
                    txtbxNum = 1;
                    dose = -1.0;
                    vol = -1.0;
                }
                else headerObj = false;
            }
            return tmp;
        }
    }
}
