using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Controls;
using System.Windows;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace VMATAutoPlanMT
{
    public class UIhelper
    {
        public bool imageExport(VMS.TPS.Common.Model.API.Image img, string imgWriteLocation, string patientID)
        {
            int[,] pixels = new int[img.XSize,img.YSize];
            //write 16 bit-depth image (still having problems as of 7/20/2022. Geometry and image looks vaguely correct, but the gray levels look off)
            //try
            //{
            //    for (int k = 0; k < img.ZSize; k++)
            //    {
            //        img.GetVoxels(k, pixels);
            //        FromTwoDimIntArrayGray(pixels, k, imgWriteLocation);
            //        //SaveBmp(bmp, imgWriteLocation);
            //    }
            //    return false;
            //}
            //catch (Exception e) { MessageBox.Show(e.StackTrace); return true; }
            
            //the main limitation of this method is the maximum bit depth is limited to 8 (whereas CT data has a bit depth of 12)
            try
            {
                string ct_ID = img.Id;
                string folderLoc = Path.Combine(imgWriteLocation, patientID);
                if (!Directory.Exists(folderLoc)) Directory.CreateDirectory(folderLoc);
                for (int k = 0; k < img.ZSize; k++)
                {
                    Bitmap bmp = new Bitmap(img.XSize, img.YSize, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                    img.GetVoxels(k, pixels);
                    int i, j;
                    for (j = 0; j < img.YSize; j++)
                    {
                        for (i = 0; i < img.XSize; i++)
                        {
                            int r, b, g;
                            int val = (int)((double)pixels[i, j] / Math.Pow(2, 12) * 255);
                            if (val > 255) r = b = g = 255;
                            else r = b = g = val;
                            bmp.SetPixel(i, j, System.Drawing.Color.FromArgb(255, r, g, b));
                        }
                    }
                    bmp.Save(String.Format(@"{0}\{1}_{2}.png",folderLoc,ct_ID,k));
                }
            }
            catch (Exception e) { MessageBox.Show(e.Message); return true; }
            return false;
        }

        public void FromTwoDimIntArrayGray(Int32[,] data, int sliceNum, string writeLocation)
        {
            // Transform 2-dimensional Int32 array to 1-byte-per-pixel byte array
            Int32 width = data.GetLength(0);
            Int32 height = data.GetLength(1);
            //stride must be a multiple of 4
            Int32 stride = 4*((width*2 + 3) / 4);
            Int32 byteIndex = 0;
            byte[] dataBytes = new byte[height * stride];
            for (Int32 y = 0; y < height; y++)
            {
                for (Int32 x = 0; x < width; x++)
                {
                    // read the bytes and shift right. Shifting bits right does NOT alter value.
                    // NOTE: signed int is BIG ENDIAN (unsigned ints are little endian)
                    //dataBytes[byteIndex] = (Byte)(val >> 24);
                    //dataBytes[byteIndex + 1] = (Byte)(val >> 16);
                    dataBytes[byteIndex] = (Byte)(data[x,y] >> 08);
                    dataBytes[byteIndex + 1] = (Byte)data[x,y];

                    // More efficient than multiplying
                    byteIndex += 2;

                }
            }
            //MemoryStream ms = new MemoryStream(dataBytes);
            //ms.Seek(0, SeekOrigin.Begin);
            //create new bitmap of width and hight equal to width and height of axial CT image
            Bitmap bmp = new Bitmap(width, height);
            //lock the bits (pay attention to pixel format argument)
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format16bppRgb565);
            //copy the byte array determined from the loop above to the bitmap
            Marshal.Copy(dataBytes, 0, bmpData.Scan0, dataBytes.Length);

            BitmapSource source = BitmapSource.Create(bmp.Width,
                                                      bmp.Height,
                                                      96,
                                                      96,
                                                      System.Windows.Media.PixelFormats.Gray16,
                                                      null,
                                                      bmpData.Scan0,
                                                      bmpData.Stride * bmp.Height,
                                                      bmpData.Stride);
            bmp.UnlockBits(bmpData);
            FileStream stream = new FileStream(Path.Combine(writeLocation, String.Format("{0}.bmp",sliceNum)), FileMode.Create);

            TiffBitmapEncoder encoder = new TiffBitmapEncoder();

            encoder.Compression = TiffCompressOption.Zip;
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(stream);

            stream.Close();
        }

        //private void SaveBmp(Bitmap bmp, string path)
        //{
        //    Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);

        //    BitmapData bitmapData = bmp.LockBits(rect, ImageLockMode.ReadOnly, bmp.PixelFormat);

        //    System.Windows.Media.PixelFormat pixelFormats = ConvertBmpPixelFormat(bmp.PixelFormat);

        //    BitmapSource source = BitmapSource.Create(bmp.Width,
        //                                              bmp.Height,
        //                                              bmp.HorizontalResolution,
        //                                              bmp.VerticalResolution,
        //                                              pixelFormats,
        //                                              null,
        //                                              bitmapData.Scan0,
        //                                              bitmapData.Stride * bmp.Height,
        //                                              bitmapData.Stride);

        //    bmp.UnlockBits(bitmapData);


        //    FileStream stream = new FileStream(Path.Combine(path,"testing.bmp"), FileMode.Create);

        //    TiffBitmapEncoder encoder = new TiffBitmapEncoder();

        //    encoder.Compression = TiffCompressOption.Zip;
        //    encoder.Frames.Add(BitmapFrame.Create(source));
        //    encoder.Save(stream);

        //    stream.Close();
        //}

        //private static System.Windows.Media.PixelFormat ConvertBmpPixelFormat(System.Drawing.Imaging.PixelFormat pixelformat)
        //{
        //    //System.Windows.Media.PixelFormat pixelFormats = System.Windows.Media.PixelFormats.Default;

        //    //switch (pixelformat)
        //    //{
        //    //    case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
        //    //        pixelFormats = PixelFormats.Bgr32;
        //    //        break;

        //    //    case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
        //    //        pixelFormats = PixelFormats.Gray8;
        //    //        break;

        //    //    case System.Drawing.Imaging.PixelFormat.Format16bppGrayScale:
        //    //        pixelFormats = PixelFormats.Gray16;
        //    //        break;
        //    //}
            
        //    return PixelFormats.Gray32Float;
        //}

        public bool clearRow(object sender, StackPanel sp)
        {
            //same deal as the clear sparing structure button (clearStructBtn_click)
            Button btn = (Button)sender;
            int i = 0;
            int k = 0;
            foreach (object obj in sp.Children)
            {
                foreach (object obj1 in ((StackPanel)obj).Children)
                {
                    if (obj1.Equals(btn)) k = i;
                }
                if (k > 0) break;
                i++;
            }

            //clear entire list if there are only two entries (header + 1 real entry)
            if (sp.Children.Count < 3) { return true; }
            else sp.Children.RemoveAt(k);
            return false;
        }

        public StackPanel getCTImageSets(StackPanel theSP, VMS.TPS.Common.Model.API.Image theImage, bool isFirst)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(25, 0, 5, 5);

            TextBox SID_TB = new TextBox();
            SID_TB.Text = theImage.Series.Id;
            SID_TB.HorizontalAlignment = HorizontalAlignment.Center;
            SID_TB.VerticalAlignment = VerticalAlignment.Center;
            SID_TB.Width = 80;
            SID_TB.FontSize = 14;
            SID_TB.Margin = new Thickness(0);

            TextBox CTID_TB = new TextBox();
            CTID_TB.Text = theImage.Id;
            CTID_TB.Name = "theTB";
            CTID_TB.HorizontalAlignment = HorizontalAlignment.Center;
            CTID_TB.VerticalAlignment = VerticalAlignment.Center;
            CTID_TB.Width = 160;
            CTID_TB.FontSize = 14;
            CTID_TB.Margin = new Thickness(25,0,0,0);

            TextBox numImg_TB = new TextBox();
            numImg_TB.Text = theImage.ZSize.ToString();
            numImg_TB.HorizontalAlignment = HorizontalAlignment.Center;
            numImg_TB.VerticalAlignment = VerticalAlignment.Center;
            numImg_TB.Width = 40;
            numImg_TB.FontSize = 14;
            numImg_TB.HorizontalContentAlignment = HorizontalAlignment.Center;
            numImg_TB.Margin = new Thickness(25,0,0,0);

            TextBox creation_TB = new TextBox();
            creation_TB.Text = theImage.HistoryDateTime.ToString("MM/dd/yyyy");
            creation_TB.HorizontalAlignment = HorizontalAlignment.Center;
            creation_TB.VerticalAlignment = VerticalAlignment.Center;
            creation_TB.Width = 90;
            creation_TB.FontSize = 14;
            creation_TB.HorizontalContentAlignment = HorizontalAlignment.Center;
            creation_TB.Margin = new Thickness(25,0,0,0);

            CheckBox select_CB = new CheckBox();
            select_CB.Margin = new Thickness(30, 5, 0, 0);
            if (isFirst) select_CB.IsChecked = true;

            sp.Children.Add(SID_TB);
            sp.Children.Add(CTID_TB);
            sp.Children.Add(numImg_TB);
            sp.Children.Add(creation_TB);
            sp.Children.Add(select_CB);

            return sp;
        }

        public string parseSelectedCTImage(StackPanel theSP)
        {
            string theImage = "";
            foreach (object obj in theSP.Children)
            {
                //skip over the header row
                foreach (object obj1 in ((StackPanel)obj).Children)
                {
                    if (obj1.GetType() == typeof(TextBox))
                    {
                        if((obj1 as TextBox).Name == "theTB")
                        {
                            theImage = (obj1 as TextBox).Text;
                        }
                    }
                    else if(obj1.GetType() == typeof(CheckBox))
                    {
                        if ((obj1 as CheckBox).IsChecked.Value) return theImage;
                    }
                }
            }

            return theImage;
        }

        public StackPanel getSpareStructHeader(StackPanel theSP)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(5, 0, 5, 5);

            Label strName = new Label();
            strName.Content = "Structure Name";
            strName.HorizontalAlignment = HorizontalAlignment.Center;
            strName.VerticalAlignment = VerticalAlignment.Top;
            strName.Width = 150;
            strName.FontSize = 14;
            strName.Margin = new Thickness(27, 0, 0, 0);

            Label spareType = new Label();
            spareType.Content = "Sparing Type";
            spareType.HorizontalAlignment = HorizontalAlignment.Center;
            spareType.VerticalAlignment = VerticalAlignment.Top;
            spareType.Width = 150;
            spareType.FontSize = 14;
            spareType.Margin = new Thickness(10, 0, 0, 0);

            Label marginLabel = new Label();
            marginLabel.Content = "Margin (cm)";
            marginLabel.HorizontalAlignment = HorizontalAlignment.Center;
            marginLabel.VerticalAlignment = VerticalAlignment.Top;
            marginLabel.Width = 150;
            marginLabel.FontSize = 14;
            marginLabel.Margin = new Thickness(0, 0, 0, 0);

            sp.Children.Add(strName);
            sp.Children.Add(spareType);
            sp.Children.Add(marginLabel);

            return sp;
        }

        public StackPanel addSpareStructVolume(StackPanel theSP, StructureSet selectedSS, Tuple<string, string, double> listItem, int clearSpareBtnCounter, SelectionChangedEventHandler typeChngHndl, RoutedEventHandler clearEvtHndl)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(10, 0, 5, 5);

            ComboBox str_cb = new ComboBox();
            str_cb.Name = "str_cb";
            str_cb.Width = 150;
            str_cb.Height = sp.Height - 5;
            str_cb.HorizontalAlignment = HorizontalAlignment.Left;
            str_cb.VerticalAlignment = VerticalAlignment.Top;
            str_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            str_cb.Margin = new Thickness(5, 5, 0, 0);

            str_cb.Items.Add("--select--");
            //this code is used to fix the issue where the structure exists in the structure set, but doesn't populate as the default option in the combo box.
            int index = 0;
            //j is initially 1 because we already added "--select--" to the combo box
            int j = 1;
            foreach (Structure s in selectedSS.Structures)
            {
                str_cb.Items.Add(s.Id);
                if (s.Id.ToLower() == listItem.Item1.ToLower()) index = j;
                j++;
            }
            str_cb.SelectedIndex = index;
            sp.Children.Add(str_cb);

            ComboBox type_cb = new ComboBox();
            type_cb.Name = "type_cb";
            type_cb.Width = 150;
            type_cb.Height = sp.Height - 5;
            type_cb.HorizontalAlignment = HorizontalAlignment.Left;
            type_cb.VerticalAlignment = VerticalAlignment.Top;
            type_cb.Margin = new Thickness(5, 5, 0, 0);
            type_cb.HorizontalContentAlignment = HorizontalAlignment.Center;
            string[] types = new string[] { "--select--", "Crop from target", "Contour overlap", "Mean Dose < Rx Dose", "Dmax ~ Rx Dose" };
            foreach (string s in types) type_cb.Items.Add(s);
            type_cb.Text = listItem.Item2;
            type_cb.SelectionChanged += typeChngHndl;
            sp.Children.Add(type_cb);

            TextBox addMargin = new TextBox();
            addMargin.Name = "addMargin_tb";
            addMargin.Width = 120;
            addMargin.Height = sp.Height - 5;
            addMargin.HorizontalAlignment = HorizontalAlignment.Left;
            addMargin.VerticalAlignment = VerticalAlignment.Top;
            addMargin.TextAlignment = TextAlignment.Center;
            addMargin.VerticalContentAlignment = VerticalAlignment.Center;
            addMargin.Margin = new Thickness(5, 5, 0, 0);
            addMargin.Text = Convert.ToString(listItem.Item3);
            if (listItem.Item2 != "Mean Dose < Rx Dose" && listItem.Item2 != "Crop from target") addMargin.Visibility = Visibility.Hidden;
            sp.Children.Add(addMargin);

            Button clearStructBtn = new Button();
            clearStructBtn.Name = "clearStructBtn" + clearSpareBtnCounter;
            clearStructBtn.Content = "Clear";
            clearStructBtn.Click += clearEvtHndl;
            clearStructBtn.Width = 50;
            clearStructBtn.Height = sp.Height - 5;
            clearStructBtn.HorizontalAlignment = HorizontalAlignment.Left;
            clearStructBtn.VerticalAlignment = VerticalAlignment.Top;
            clearStructBtn.Margin = new Thickness(10, 5, 0, 0);
            sp.Children.Add(clearStructBtn);

            return sp;
        }

        public List<Tuple<Structure,Structure>> checkStructuresToUnion(StructureSet selectedSS)
        {
            List<Tuple<Structure, Structure>> structuresToUnion = new List<Tuple<Structure, Structure>> { };
            List<Structure> LStructs = selectedSS.Structures.Where(x => x.Id.Substring(x.Id.Length - 2, 2).ToLower() == "_l" || x.Id.Substring(x.Id.Length - 2, 2).ToLower() == " l").ToList();
            List<Structure> RStructs = selectedSS.Structures.Where(x => x.Id.Substring(x.Id.Length - 2, 2).ToLower() == "_r" || x.Id.Substring(x.Id.Length - 2, 2).ToLower() == " r").ToList();
            foreach (Structure itr in LStructs)
            {
                Structure RStruct = RStructs.FirstOrDefault(x => x.Id.Substring(0, x.Id.Length - 2) == itr.Id.Substring(0, itr.Id.Length - 2));
                if (RStruct != null && selectedSS.Structures.FirstOrDefault(x => x.Id == itr.Id.Substring(0, itr.Id.Length - 2) && !x.IsEmpty) == null)
                {
                    string newName = addProperEndingToName(itr.Id.Substring(0, itr.Id.Length - 2).ToLower());
                    if(selectedSS.Structures.FirstOrDefault(x => x.Id == newName) == null) structuresToUnion.Add(new Tuple<Structure, Structure>(itr, RStruct));
                }
            }
            return structuresToUnion;
        }

        private string addProperEndingToName(string initName)
        {
            if (initName.Substring(initName.Length - 1, 1) == "y" && initName.Substring(initName.Length - 2, 2) != "ey") initName = initName.Substring(0, initName.Length - 1) + "ies";
            else if (initName.Substring(initName.Length - 1, 1) == "s") initName += "es";
            else initName += "s";
            return initName;
        }

        public bool unionLRStructures(Tuple<Structure, Structure> itr, StructureSet selectedSS)
        {
            Structure newStructure = null;
            string newName = addProperEndingToName(itr.Item1.Id.Substring(0, itr.Item1.Id.Length - 2).ToLower());
            try
            {
                Structure existStructure = selectedSS.Structures.FirstOrDefault(x => x.Id == newName);
                //a structure already exists in the structure set with the intended name
                if (existStructure != null) newStructure = existStructure;
                else newStructure = selectedSS.AddStructure("CONTROL", newName);
                newStructure.SegmentVolume = itr.Item1.Margin(0.0);
                newStructure.SegmentVolume = newStructure.Or(itr.Item2.Margin(0.0));
            }
            catch (Exception except) { MessageBox.Show(String.Format("Warning! Could not add structure: {0}\nBecause: {1}", newName, except.Message)); return true; }
            return false;
        }

        public List<Tuple<string, string, double>> parseSpareStructList(StackPanel theSP)
        {
            List<Tuple<string, string, double>> structureSpareList = new List<Tuple<string, string, double>> { };
            string structure = "";
            string spareType = "";
            double margin = -1000.0;
            bool firstCombo = true;
            bool headerObj = true;
            foreach (object obj in theSP.Children)
            {
                //skip over the header row
                if (!headerObj)
                {
                    foreach (object obj1 in ((StackPanel)obj).Children)
                    {
                        if (obj1.GetType() == typeof(ComboBox))
                        {
                            //first combo box is the structure and the second is the sparing type
                            if (firstCombo)
                            {
                                structure = (obj1 as ComboBox).SelectedItem.ToString();
                                firstCombo = false;
                            }
                            else spareType = (obj1 as ComboBox).SelectedItem.ToString();
                        }
                        //try to parse the margin value as a double
                        else if (obj1.GetType() == typeof(TextBox)) if (!string.IsNullOrWhiteSpace((obj1 as TextBox).Text)) double.TryParse((obj1 as TextBox).Text, out margin);
                    }
                    if (structure == "--select--" || spareType == "--select--")
                    {
                        MessageBox.Show("Error! \nStructure or Sparing Type not selected! \nSelect an option and try again");
                        return new List<Tuple<string, string, double>> { };
                    }
                    //margin will not be assigned from the default value (-1000) if the input is empty, a whitespace, or NaN
                    else if (margin == -1000.0)
                    {
                        MessageBox.Show("Error! \nEntered margin value is invalid! \nEnter a new margin and try again");
                        return new List<Tuple<string, string, double>> { };
                    }
                    //only add the current row to the structure sparing list if all the parameters were successful parsed
                    else structureSpareList.Add(Tuple.Create(structure, spareType, margin));
                    firstCombo = true;
                    margin = -1000.0;
                }
                else headerObj = false;
            }

            return structureSpareList;
        }

        public List<StackPanel> populateBeamsTabHelper(StackPanel theSP, List<string> linacs, List<string> beamEnergies, List<string> isoNames, int[] beamsPerIso, int numIsos, int numVMATIsos)
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

        public List<StackPanel> populateBeamsTabHelper(StackPanel theSP, List<string> linacs, List<string> beamEnergies, List<Tuple<string,List<string>>> isoNames, int[] beamsPerIso)
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

        public StackPanel getOptHeader(StackPanel theSP)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(5, 0, 5, 5);

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

        public StackPanel addOptVolume(StackPanel theSP, StructureSet selectedSS, Tuple<string, string, double, double, int> listItem, int clearOptBtnCounter, RoutedEventHandler e)
        {
            StackPanel sp = new StackPanel();
            sp.Height = 30;
            sp.Width = theSP.Width;
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(5);

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
            opt_str_cb.SelectedIndex = index;
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
            clearOptStructBtn.Name = "clearOptStructBtn" + clearOptBtnCounter;
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

        public List<Tuple<string, string, double, double, int>> parseOptConstraints(StackPanel sp)
        {
            if (sp.Children.Count == 0)
            {
                System.Windows.Forms.MessageBox.Show("No optimization parameters present to assign to VMAT TBI plan!");
                return new List<Tuple<string, string, double, double, int>>();
            }

            //get constraints
            List<Tuple<string, string, double, double, int>> optParametersList = new List<Tuple<string, string, double, double, int>> { };
            string structure = "";
            string constraintType = "";
            double dose = -1.0;
            double vol = -1.0;
            int priority = -1;
            int txtbxNum = 1;
            bool firstCombo = true;
            bool headerObj = true;
            foreach (object obj in sp.Children)
            {
                //skip over header row
                if (!headerObj)
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
                    }
                    //do some checks to ensure the integrity of the data
                    if (structure == "--select--" || constraintType == "--select--")
                    {
                        System.Windows.Forms.MessageBox.Show("Error! \nStructure or Sparing Type not selected! \nSelect an option and try again");
                        return new List<Tuple<string, string, double, double, int>>();
                    }
                    else if (dose == -1.0 || vol == -1.0 || priority == -1.0)
                    {
                        System.Windows.Forms.MessageBox.Show("Error! \nDose, volume, or priority values are invalid! \nEnter new values and try again");
                        return new List<Tuple<string, string, double, double, int>>();
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
                else headerObj = false;
            }
            return optParametersList;
        }

        public void assignOptConstraints(List<Tuple<string, string, double, double, int>> parameters, ExternalPlanSetup VMATplan, bool useJawTracking, double NTOpriority)
        {
            foreach (Tuple<string, string, double, double, int> opt in parameters)
            {
                //assign the constraints to the plan. I haven't found a use for the exact constraint yet, so I just wrote the script to throw a warning if the exact constraint was selected (that row of data will NOT be
                //assigned to the VMAT plan)
                if (opt.Item2 == "Upper") VMATplan.OptimizationSetup.AddPointObjective(VMATplan.StructureSet.Structures.First(x => x.Id == opt.Item1), OptimizationObjectiveOperator.Upper, new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), opt.Item4, (double)opt.Item5);
                else if (opt.Item2 == "Lower") VMATplan.OptimizationSetup.AddPointObjective(VMATplan.StructureSet.Structures.First(x => x.Id == opt.Item1), OptimizationObjectiveOperator.Lower, new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), opt.Item4, (double)opt.Item5);
                else if (opt.Item2 == "Mean") VMATplan.OptimizationSetup.AddMeanDoseObjective(VMATplan.StructureSet.Structures.First(x => x.Id == opt.Item1), new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), (double)opt.Item5);
                else if (opt.Item2 == "Exact") System.Windows.Forms.MessageBox.Show("Script not setup to handle exact dose constraints!");
                else System.Windows.Forms.MessageBox.Show("Constraint type not recognized!");
            }
            //turn on/turn off jaw tracking
            try { VMATplan.OptimizationSetup.UseJawTracking = useJawTracking; }
            catch (Exception except) { System.Windows.Forms.MessageBox.Show(String.Format("Warning! Could not set jaw tracking for VMAT plan because: {0}\nJaw tacking will have to be set manually!", except.Message)); }
            //set auto NTO priority to zero (i.e., shut it off). It has to be done this way because every plan created in ESAPI has an instance of an automatic NTO, which CAN'T be deleted.
            VMATplan.OptimizationSetup.AddAutomaticNormalTissueObjective(NTOpriority);
        }
    }
}
