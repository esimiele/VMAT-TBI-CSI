using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using VMATTBICSIAutoPlanningHelpers.Prompts;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class BeamPlacementUIHelper
    {
        /// <summary>
        /// Helper method to pupulate the place beams tab
        /// </summary>
        /// <param name="theSP"></param>
        /// <param name="linacs"></param>
        /// <param name="beamEnergies"></param>
        /// <param name="isoNames"></param>
        /// <param name="beamsPerIso"></param>
        /// <param name="numIsos"></param>
        /// <param name="numVMATIsos"></param>
        /// <returns></returns>
        public static List<StackPanel> PopulateBeamsTabHelper(double width, 
                                                              List<string> linacs, 
                                                              List<string> beamEnergies, 
                                                              List<PlanIsocenterModel> isoNames, 
                                                              int[] beamsPerIso)
        {
            List<StackPanel> theSPList = new List<StackPanel> { };
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = width,
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5)
            };

            //select linac (LA-16 or LA-17)
            Label linac = new Label
            {
                Content = "Linac:",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 208,
                FontSize = 14,
                Margin = new Thickness(0, 0, 10, 0)
            };
            sp.Children.Add(linac);

            ComboBox linac_cb = new ComboBox
            {
                Name = "linac_cb",
                Width = 80,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 65, 0)
            };
            if (linacs.Count() > 0) foreach (string s in linacs) linac_cb.Items.Add(s);
            else
            {
                EnterMissingInfoPrompt linacName = new EnterMissingInfoPrompt("Enter the name of the linac you want to use", "Linac:");
                linacName.ShowDialog();
                if (!linacName.GetSelection()) return new List<StackPanel> { };
                linac_cb.Items.Add(linacName.GetEnteredValue());
            }
            linac_cb.SelectedIndex = 0;
            linac_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            sp.Children.Add(linac_cb);

            theSPList.Add(sp);

            //select energy (6X or 10X)
            sp = new StackPanel
            {
                Height = 30,
                Width = width,
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5)
            };

            Label energy = new Label
            {
                Content = "Beam energy:",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 215,
                FontSize = 14,
                Margin = new Thickness(0, 0, 10, 0)
            };
            sp.Children.Add(energy);

            ComboBox energy_cb = new ComboBox
            {
                Name = "energy_cb",
                Width = 70,
                Height = sp.Height - 5,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 65, 0)
            };
            if (beamEnergies.Count() > 0) foreach (string s in beamEnergies) energy_cb.Items.Add(s);
            else
            {
                EnterMissingInfoPrompt energyName = new EnterMissingInfoPrompt("Enter the photon beam energy you want to use", "Energy:");
                energyName.ShowDialog();
                if (!energyName.GetSelection()) return new List<StackPanel> { };
                energy_cb.Items.Add(energyName.GetEnteredValue());
            }
            energy_cb.SelectedIndex = 0;
            energy_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            sp.Children.Add(energy_cb);

            theSPList.Add(sp);

            //add iso names and suggested number of beams
            foreach(PlanIsocenterModel itr in isoNames)
            {
                sp = new StackPanel
                {
                    Height = 30,
                    Width = width,
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(2)
                };

                Label planID = new Label
                {
                    Content = "Plan Id: " + itr.PlanId,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Width = 230,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold
                };
                sp.Children.Add(planID);
                theSPList.Add(sp);

                int isoCount = 0;
                foreach(string isoId in itr.Isocenters.Select(x => x.IsocenterId))
                {
                    sp = new StackPanel
                    {
                        Height = 30,
                        Width = width,
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(2)
                    };

                    Label iso = new Label
                    {
                        //11/1/22
                        //interesting issue where the first character of the first plan Id is ignored and not printed on the place beams tab
                        Content = String.Format("Isocenter {0} <{1}>:", (isoCount + 1).ToString(), isoId),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        Width = 230,
                        FontSize = 14,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    sp.Children.Add(iso);

                    TextBox beams_tb = new TextBox
                    {
                        Name = "beams_tb",
                        Width = 40,
                        Height = sp.Height - 5,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 80, 0),

                        TextAlignment = TextAlignment.Center
                    };
                    //ugly provision to override suggested number of beams per iso to 2 for leg AP/PA isocenters for TBI plans
                    if (!itr.PlanId.Contains("legs")) beams_tb.Text = beamsPerIso[isoCount].ToString();
                    else beams_tb.Text = "2";

                    sp.Children.Add(beams_tb);
                    theSPList.Add(sp);
                    isoCount++;
                }
            }
            return theSPList;
        }

        /// <summary>
        /// Helper method to evaluate the entered number of beams in the place beams tab and return a list of isocenters each containing a list of beams
        /// </summary>
        /// <param name="theSP"></param>
        /// <param name="isos"></param>
        /// <returns></returns>
        public static (string, string, List<List<int>>, StringBuilder) GetBeamSelections(StackPanel theSP, List<PlanIsocenterModel> isos)
        {
            StringBuilder sb = new StringBuilder();
            int isocount = 0;
            int plancount = 0;
            bool firstCombo = true;
            string chosenLinac = "";
            string chosenEnergy = "";
            List<List<int>> numBeams = new List<List<int>> { };
            List<int> numBeams_temp = new List<int> { };
            int numElementsPerRow = 0;
            foreach (object obj in theSP.Children)
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
                            sb.AppendLine(String.Format("Error! \nNumber of beams entered in iso {0} is NaN!", isos.ElementAt(plancount).Isocenters.ElementAt(isocount++).IsocenterId));
                            return ("","",new List<List<int>>(), sb);
                        }
                        else if (beamTMP < 1)
                        {
                            sb.AppendLine(String.Format("Error! \nNumber of beams entered in iso {0} is < 1!", isos.ElementAt(plancount).Isocenters.ElementAt(isocount++).IsocenterId));
                            return ("", "", new List<List<int>>(), sb);
                        }
                        else if (beamTMP > 4)
                        {
                            sb.AppendLine(String.Format("Error! \nNumber of beams entered in iso {0} is > 4!", isos.ElementAt(plancount).Isocenters.ElementAt(isocount++).IsocenterId));
                            return ("", "", new List<List<int>>(), sb);
                        }
                        else numBeams_temp.Add(beamTMP);
                    }
                    numElementsPerRow++;
                }
                if (numElementsPerRow == 1 && numBeams_temp.Any())
                {
                    //indicates only one item was in this stack panel indicating it was only a label indicating the code has finished reading the number of isos and beams per isos for this plan
                    numBeams.Add(new List<int>(numBeams_temp));
                    numBeams_temp = new List<int> { };
                    plancount++;
                    isocount = 0;
                }
                numElementsPerRow = 0;
            }
            numBeams.Add(new List<int>(numBeams_temp));

            return (chosenLinac, chosenEnergy, numBeams, sb);
        }
    }
}
