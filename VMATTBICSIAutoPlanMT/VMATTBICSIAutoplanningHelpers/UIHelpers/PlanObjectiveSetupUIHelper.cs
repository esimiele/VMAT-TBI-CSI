using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoplanningHelpers.UIHelpers
{
    public class PlanObjectiveSetupUIHelper
    {
        public StackPanel GetObjHeader(double theWidth)
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
            doseLabel.Content = "Dose";
            doseLabel.HorizontalAlignment = HorizontalAlignment.Center;
            doseLabel.VerticalAlignment = VerticalAlignment.Top;
            doseLabel.Width = 60;
            doseLabel.FontSize = 14;
            doseLabel.Margin = new Thickness(5, 0, 0, 0);

            Label unitsLabel = new Label();
            unitsLabel.Content = "Units";
            unitsLabel.HorizontalAlignment = HorizontalAlignment.Center;
            unitsLabel.VerticalAlignment = VerticalAlignment.Top;
            unitsLabel.Width = 65;
            unitsLabel.FontSize = 14;
            unitsLabel.Margin = new Thickness(13, 0, 0, 0);

            sp.Children.Add(strName);
            sp.Children.Add(spareType);
            sp.Children.Add(volLabel);
            sp.Children.Add(doseLabel);
            sp.Children.Add(unitsLabel);
            return sp;
        }

        public List<Tuple<string, string, double, double, DoseValuePresentation>> GetPlanObjectives(StackPanel theSP)
        {
            //get constraints
            List<Tuple<string, string, double, double, DoseValuePresentation>> tmp = new List<Tuple<string, string, double, double, DoseValuePresentation>> { };
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
                    DoseValuePresentation presentation = DoseValuePresentation.Relative;
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
                                    if ((obj1 as TextBox).Text.Contains("cGy")) presentation = DoseValuePresentation.Absolute;
                                }
                            }
                            txtbxNum++;
                        }
                    }
                    //do some checks to ensure the integrity of the data
                    if (structure == "--select--" || constraintType == "--select--")
                    {
                        MessageBox.Show("Error! \nStructure or Sparing Type not selected! \nSelect an option and try again");
                        return new List<Tuple<string, string, double, double, DoseValuePresentation>>{};
                    }
                    else if (dose == -1.0 || vol == -1.0)
                    {
                        MessageBox.Show("Error! \nDose, volume, or priority values are invalid! \nEnter new values and try again");
                        return new List<Tuple<string, string, double, double, DoseValuePresentation>> { };
                    }
                    //if the row of data passes the above checks, add it the optimization parameter list
                    else tmp.Add(Tuple.Create(structure, constraintType, Math.Round(dose, 3, MidpointRounding.AwayFromZero), Math.Round(vol, 3, MidpointRounding.AwayFromZero), presentation));
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
