using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using VMATTBICSIAutoplanningHelpers.Prompts;

namespace VMATTBICSIAutoplanningHelpers.UIHelpers
{
    public class BeamPlacementUIHelper
    {
        public List<StackPanel> PopulateBeamsTabHelper(StackPanel theSP, List<string> linacs, List<string> beamEnergies, List<string> isoNames, int[] beamsPerIso, int numIsos, int numVMATIsos)
        {
            List<StackPanel> theSPList = new List<StackPanel> { };
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Margin = new Thickness(5);

            //select linac (LA-16 or LA-17)
            Label linac = new Label();
            linac.Content = "Linac:";
            linac.HorizontalAlignment = HorizontalAlignment.Right;
            linac.VerticalAlignment = VerticalAlignment.Top;
            linac.Width = 208;
            linac.FontSize = 14;
            linac.Margin = new Thickness(0, 0, 10, 0);
            sp.Children.Add(linac);

            ComboBox linac_cb = new ComboBox();
            linac_cb.Name = "linac_cb";
            linac_cb.Width = 80;
            linac_cb.Height = sp.Height - 5;
            linac_cb.HorizontalAlignment = HorizontalAlignment.Right;
            linac_cb.VerticalAlignment = VerticalAlignment.Center;
            linac_cb.Margin = new Thickness(0, 0, 65, 0);
            if (linacs.Count() > 0) foreach (string s in linacs) linac_cb.Items.Add(s);
            else
            {
                enterMissingInfo linacName = new enterMissingInfo("Enter the name of the linac you want to use", "Linac:");
                linacName.ShowDialog();
                if (!linacName.confirm) return new List<StackPanel> { };
                linac_cb.Items.Add(linacName.value.Text);
            }
            linac_cb.SelectedIndex = 0;
            linac_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            sp.Children.Add(linac_cb);

            theSPList.Add(sp);

            //select energy (6X or 10X)
            sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Margin = new Thickness(5);

            Label energy = new Label();
            energy.Content = "Beam energy:";
            energy.HorizontalAlignment = HorizontalAlignment.Right;
            energy.VerticalAlignment = VerticalAlignment.Top;
            energy.Width = 215;
            energy.FontSize = 14;
            energy.Margin = new Thickness(0, 0, 10, 0);
            sp.Children.Add(energy);

            ComboBox energy_cb = new ComboBox();
            energy_cb.Name = "energy_cb";
            energy_cb.Width = 70;
            energy_cb.Height = sp.Height - 5;
            energy_cb.HorizontalAlignment = HorizontalAlignment.Right;
            energy_cb.VerticalAlignment = VerticalAlignment.Center;
            energy_cb.Margin = new Thickness(0, 0, 65, 0);
            if (beamEnergies.Count() > 0) foreach (string s in beamEnergies) energy_cb.Items.Add(s);
            else
            {
                enterMissingInfo energyName = new enterMissingInfo("Enter the photon beam energy you want to use", "Energy:");
                energyName.ShowDialog();
                if (!energyName.confirm) return new List<StackPanel> { };
                energy_cb.Items.Add(energyName.value.Text);
            }
            energy_cb.SelectedIndex = 0;
            energy_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            sp.Children.Add(energy_cb);

            theSPList.Add(sp);

            //add iso names and suggested number of beams
            for (int i = 0; i < numIsos; i++)
            {
                sp = new StackPanel();
                sp.Height = 30;
                sp.Width = theSP.Width;
                sp.Orientation = Orientation.Horizontal;
                sp.HorizontalAlignment = HorizontalAlignment.Center;
                sp.Margin = new Thickness(2);

                Label iso = new Label();
                iso.Content = String.Format("Isocenter {0} <{1}>:", (i + 1).ToString(), isoNames.ElementAt(i));
                iso.HorizontalAlignment = HorizontalAlignment.Right;
                iso.VerticalAlignment = VerticalAlignment.Top;
                iso.Width = 230;
                iso.FontSize = 14;
                iso.Margin = new Thickness(0, 0, 10, 0);
                sp.Children.Add(iso);

                TextBox beams_tb = new TextBox();
                beams_tb.Name = "beams_tb";
                beams_tb.Width = 40;
                beams_tb.Height = sp.Height - 5;
                beams_tb.HorizontalAlignment = HorizontalAlignment.Right;
                beams_tb.VerticalAlignment = VerticalAlignment.Center;
                beams_tb.Margin = new Thickness(0, 0, 80, 0);

                if (i >= numVMATIsos) beams_tb.IsReadOnly = true;
                beams_tb.Text = beamsPerIso[i].ToString();
                beams_tb.TextAlignment = TextAlignment.Center;
                sp.Children.Add(beams_tb);

                theSPList.Add(sp);
            }
            return theSPList;
        }

        public List<StackPanel> PopulateBeamsTabHelper(StackPanel theSP, List<string> linacs, List<string> beamEnergies, List<Tuple<string, List<string>>> isoNames, int[] beamsPerIso)
        {
            List<StackPanel> theSPList = new List<StackPanel> { };
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Margin = new Thickness(5);

            //select linac (LA-16 or LA-17)
            Label linac = new Label();
            linac.Content = "Linac:";
            linac.HorizontalAlignment = HorizontalAlignment.Right;
            linac.VerticalAlignment = VerticalAlignment.Top;
            linac.Width = 208;
            linac.FontSize = 14;
            linac.Margin = new Thickness(0, 0, 10, 0);
            sp.Children.Add(linac);

            ComboBox linac_cb = new ComboBox();
            linac_cb.Name = "linac_cb";
            linac_cb.Width = 80;
            linac_cb.Height = sp.Height - 5;
            linac_cb.HorizontalAlignment = HorizontalAlignment.Right;
            linac_cb.VerticalAlignment = VerticalAlignment.Center;
            linac_cb.Margin = new Thickness(0, 0, 65, 0);
            if (linacs.Count() > 0) foreach (string s in linacs) linac_cb.Items.Add(s);
            else
            {
                enterMissingInfo linacName = new enterMissingInfo("Enter the name of the linac you want to use", "Linac:");
                linacName.ShowDialog();
                if (!linacName.confirm) return new List<StackPanel> { };
                linac_cb.Items.Add(linacName.value.Text);
            }
            linac_cb.SelectedIndex = 0;
            linac_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            sp.Children.Add(linac_cb);

            theSPList.Add(sp);

            //select energy (6X or 10X)
            sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Margin = new Thickness(5);

            Label energy = new Label();
            energy.Content = "Beam energy:";
            energy.HorizontalAlignment = HorizontalAlignment.Right;
            energy.VerticalAlignment = VerticalAlignment.Top;
            energy.Width = 215;
            energy.FontSize = 14;
            energy.Margin = new Thickness(0, 0, 10, 0);
            sp.Children.Add(energy);

            ComboBox energy_cb = new ComboBox();
            energy_cb.Name = "energy_cb";
            energy_cb.Width = 70;
            energy_cb.Height = sp.Height - 5;
            energy_cb.HorizontalAlignment = HorizontalAlignment.Right;
            energy_cb.VerticalAlignment = VerticalAlignment.Center;
            energy_cb.Margin = new Thickness(0, 0, 65, 0);
            if (beamEnergies.Count() > 0) foreach (string s in beamEnergies) energy_cb.Items.Add(s);
            else
            {
                enterMissingInfo energyName = new enterMissingInfo("Enter the photon beam energy you want to use", "Energy:");
                energyName.ShowDialog();
                if (!energyName.confirm) return new List<StackPanel> { };
                energy_cb.Items.Add(energyName.value.Text);
            }
            energy_cb.SelectedIndex = 0;
            energy_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            sp.Children.Add(energy_cb);

            theSPList.Add(sp);

            //add iso names and suggested number of beams
            for (int i = 0; i < isoNames.Count; i++)
            {
                sp = new StackPanel();
                sp.Height = 30;
                sp.Width = theSP.Width;
                sp.Orientation = Orientation.Horizontal;
                sp.HorizontalAlignment = HorizontalAlignment.Center;
                sp.Margin = new Thickness(2);

                Label planID = new Label();
                planID.Content = "Plan Id: " + isoNames.ElementAt(i).Item1;
                planID.HorizontalAlignment = HorizontalAlignment.Center;
                planID.VerticalAlignment = VerticalAlignment.Top;
                planID.Width = 230;
                planID.FontSize = 14;
                planID.FontWeight = FontWeights.Bold;
                sp.Children.Add(planID);
                theSPList.Add(sp);

                for (int j = 0; j < isoNames.ElementAt(i).Item2.Count; j++)
                {
                    sp = new StackPanel();
                    sp.Height = 30;
                    sp.Width = theSP.Width;
                    sp.Orientation = Orientation.Horizontal;
                    sp.HorizontalAlignment = HorizontalAlignment.Center;
                    sp.Margin = new Thickness(2);

                    Label iso = new Label();
                    //11/1/22
                    //interesting issue where the first character of the first plan Id is ignored and not printed on the place beams tab
                    iso.Content = String.Format("Isocenter {0} <{1}>:", (j + 1).ToString(), isoNames.ElementAt(i).Item2.ElementAt(j));
                    iso.HorizontalAlignment = HorizontalAlignment.Right;
                    iso.VerticalAlignment = VerticalAlignment.Top;
                    iso.Width = 230;
                    iso.FontSize = 14;
                    iso.Margin = new Thickness(0, 0, 10, 0);
                    sp.Children.Add(iso);

                    TextBox beams_tb = new TextBox();
                    beams_tb.Name = "beams_tb";
                    beams_tb.Width = 40;
                    beams_tb.Height = sp.Height - 5;
                    beams_tb.HorizontalAlignment = HorizontalAlignment.Right;
                    beams_tb.VerticalAlignment = VerticalAlignment.Center;
                    beams_tb.Margin = new Thickness(0, 0, 80, 0);

                    beams_tb.Text = beamsPerIso[j].ToString();
                    beams_tb.TextAlignment = TextAlignment.Center;
                    sp.Children.Add(beams_tb);
                    theSPList.Add(sp);
                }
            }
            return theSPList;
        }

        public (string, string, List<List<int>>, StringBuilder) GetBeamSelections(StackPanel theSP, List<Tuple<string,List<string>>> isos)
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

        public void GeneratePlanIsoBeamList(List<Tuple<string, List<string>>> isoNames, List<List<int>> numBeams)
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
