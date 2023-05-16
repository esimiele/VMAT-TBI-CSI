using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using VMATTBICSIAutoPlanningHelpers.Prompts;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class BeamPlacementUIHelper
    {
        public static List<StackPanel> PopulateBeamsTabHelper(StackPanel theSP, List<string> linacs, List<string> beamEnergies, List<string> isoNames, int[] beamsPerIso, int numIsos, int numVMATIsos)
        {
            List<StackPanel> theSPList = new List<StackPanel> { };
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = theSP.Width,
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
                Width = theSP.Width,
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
            for (int i = 0; i < numIsos; i++)
            {
                sp = new StackPanel
                {
                    Height = 30,
                    Width = theSP.Width,
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(2)
                };

                Label iso = new Label
                {
                    Content = String.Format("Isocenter {0} <{1}>:", (i + 1).ToString(), isoNames.ElementAt(i)),
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
                    Margin = new Thickness(0, 0, 80, 0)
                };

                if (i >= numVMATIsos) beams_tb.IsReadOnly = true;
                beams_tb.Text = beamsPerIso[i].ToString();
                beams_tb.TextAlignment = TextAlignment.Center;
                sp.Children.Add(beams_tb);

                theSPList.Add(sp);
            }
            return theSPList;
        }

        public static List<StackPanel> PopulateBeamsTabHelper(StackPanel theSP, List<string> linacs, List<string> beamEnergies, List<Tuple<string, List<string>>> isoNames, int[] beamsPerIso)
        {
            List<StackPanel> theSPList = new List<StackPanel> { };
            StackPanel sp = new StackPanel
            {
                Height = 30,
                Width = theSP.Width,
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
                Width = theSP.Width,
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
            for (int i = 0; i < isoNames.Count; i++)
            {
                sp = new StackPanel
                {
                    Height = 30,
                    Width = theSP.Width,
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(2)
                };

                Label planID = new Label
                {
                    Content = "Plan Id: " + isoNames.ElementAt(i).Item1,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Width = 230,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold
                };
                sp.Children.Add(planID);
                theSPList.Add(sp);

                for (int j = 0; j < isoNames.ElementAt(i).Item2.Count; j++)
                {
                    sp = new StackPanel
                    {
                        Height = 30,
                        Width = theSP.Width,
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(2)
                    };

                    Label iso = new Label
                    {
                        //11/1/22
                        //interesting issue where the first character of the first plan Id is ignored and not printed on the place beams tab
                        Content = String.Format("Isocenter {0} <{1}>:", (j + 1).ToString(), isoNames.ElementAt(i).Item2.ElementAt(j)),
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

                        Text = beamsPerIso[j].ToString(),
                        TextAlignment = TextAlignment.Center
                    };
                    sp.Children.Add(beams_tb);
                    theSPList.Add(sp);
                }
            }
            return theSPList;
        }

        public static (string, string, List<List<int>>, StringBuilder) GetBeamSelections(StackPanel theSP, List<Tuple<string,List<string>>> isos)
        {
            StringBuilder sb = new StringBuilder();
            int count = 0;
            bool firstCombo = true;
            string chosenLinac = "";
            string chosenEnergy = "";
            //int[,] numBeams = new int[numVMATIsos];
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
                            sb.AppendLine(String.Format("Error! \nNumber of beams entered in iso {0} is NaN!", isos.ElementAt(count)));
                            return ("","",new List<List<int>>(), sb);
                        }
                        else if (beamTMP < 1)
                        {
                            sb.AppendLine(String.Format("Error! \nNumber of beams entered in iso {0} is < 1!", isos.ElementAt(count)));
                            return ("", "", new List<List<int>>(), sb);
                        }
                        else if (beamTMP > 4)
                        {
                            sb.AppendLine(String.Format("Error! \nNumber of beams entered in iso {0} is > 4!", isos.ElementAt(count)));
                            return ("", "", new List<List<int>>(), sb);
                        }
                        else numBeams_temp.Add(beamTMP);
                        count++;
                    }
                    numElementsPerRow++;
                }
                if (numElementsPerRow == 1 && numBeams_temp.Any())
                {
                    //indicates only one item was in this stack panel indicating it was only a label indicating the code has finished reading the number of isos and beams per isos for this plan
                    numBeams.Add(new List<int>(numBeams_temp));
                    numBeams_temp = new List<int> { };
                }
                numElementsPerRow = 0;
            }
            numBeams.Add(new List<int>(numBeams_temp));

            return (chosenLinac, chosenEnergy, numBeams, sb);
        }

        public static void GeneratePlanIsoBeamList(List<Tuple<string, List<string>>> isoNames, List<List<int>> numBeams)
        {
            List<Tuple<string, List<Tuple<string, int>>>> planIsoBeamInfo = new List<Tuple<string, List<Tuple<string, int>>>> { };
            int count = 0;
            foreach (Tuple<string, List<string>> itr in isoNames)
            {
                List<Tuple<string, int>> isoNameBeams = new List<Tuple<string, int>> { };
                for (int i = 0; i < itr.Item2.Count; i++) isoNameBeams.Add(new Tuple<string, int>(itr.Item2.ElementAt(i), numBeams.ElementAt(count).ElementAt(i)));
                planIsoBeamInfo.Add(new Tuple<string, List<Tuple<string, int>>>(itr.Item1, new List<Tuple<string, int>>(isoNameBeams)));
                count++;
            }
        }
    }
}
