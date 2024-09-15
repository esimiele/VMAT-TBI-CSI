using System;
using System.IO;
using System.Drawing.Imaging;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Linq;
using EvilDICOM.Core.Helpers;
using EvilDICOM.Network;
using EvilDICOM.Network.Enums;
using SimpleProgressWindow;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Models;
using System.Threading;

namespace VMATCSIAutoPlanMT.VMAT_CSI
{
    public class CTImageExport : SimpleMTbase
    {
        //data members
        private VMS.TPS.Common.Model.API.Image _image;
        private string _patID;
        private ImportExportDataModel _data;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="img"></param>
        /// <param name="patientID"></param>
        /// <param name="theData"></param>
        /// <param name="closePW"></param>
        public CTImageExport(VMS.TPS.Common.Model.API.Image img, 
                             string patientID,
                             ImportExportDataModel theData,
                             bool closePW) 
        {
            _image = img;
            _patID = patientID;
            _data = theData;
            SetCloseOnFinish(closePW, 3000);
        }

        /// <summary>
        /// Run control
        /// </summary>
        /// <returns></returns>
        public override bool Run()
        {
            try
            {
                if (_data.ExportFormat == ImgExportFormat.PNG)
                {
                    if (PreliminaryChecksPNG()) return true;
                    if (ExportAsPNG()) return true;
                }
                else if (_data.ExportFormat == ImgExportFormat.DICOM)
                {
                    if (PreliminaryChecksDCM()) return true;
                    if (ExportAsDCM())
                    {
                        CheckAndRemoveImagesIfPresent();
                        return true;
                    }
                    if (LaunchListener()) return true;
                }
                UpdateUILabel("Finished:");
                ProvideUIUpdate($"{_image.Id} has been exported successfully!");
                ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            }
            catch(Exception e)
            {
                ProvideUIUpdate($"Error! Exception: {e.Message}");
                ProvideUIUpdate($"{e.StackTrace}", true);
                CheckAndRemoveImagesIfPresent();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Utility method to verify the supplied file path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private bool VerifyPathIntegrity(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                ProvideUIUpdate($"File path {path} is empty! Exiting!", true);
                return true;
            }
            return false;
        }

        #region PNG export
        /// <summary>
        /// Preliminary checks for exporting the data in PNG format
        /// </summary>
        /// <returns></returns>
        private bool PreliminaryChecksPNG()
        {
            UpdateUILabel("Preliminary checks:");
            return VerifyPathIntegrity(_data.WriteLocation);
        }

        /// <summary>
        /// Export the CT image data in PNG format
        /// </summary>
        /// <returns></returns>
        private bool ExportAsPNG()
        {
            UpdateUILabel("Exporting as PNG:");
            int percentComplete = 0;
            int calcItems = 1 + _image.ZSize;
            ProvideUIUpdate("Initializing...");
            int[,] pixels = new int[_image.XSize, _image.YSize];
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

            //the main limitation of this method is the maximum bit depth is limited to 8 (whereas CT data has a bit depth of 12-16)
            try
            {
                string ct_ID = _image.Id;
                string folderLoc = Path.Combine(_data.WriteLocation, _patID);
                if (!Directory.Exists(folderLoc)) Directory.CreateDirectory(folderLoc);
                ProvideUIUpdate(100 * ++percentComplete / calcItems, "Initializtion complete. Exporting");
                for (int k = 0; k < _image.ZSize; k++)
                {
                    ProvideUIUpdate(100 * ++percentComplete / calcItems);
                    Bitmap bmp = new Bitmap(_image.XSize, _image.YSize, PixelFormat.Format32bppRgb);
                    _image.GetVoxels(k, pixels);
                    int i, j;
                    for (j = 0; j < _image.YSize; j++)
                    {
                        for (i = 0; i < _image.XSize; i++)
                        {
                            int r, b, g;
                            int val = (int)((double)pixels[i, j] / Math.Pow(2, 12) * 255);
                            if (val > 255) r = b = g = 255;
                            else r = b = g = val;
                            bmp.SetPixel(i, j, Color.FromArgb(255, r, g, b));
                        }
                    }
                    bmp.Save(String.Format(@"{0}\{1}_{2}.png", folderLoc, ct_ID, k));
                }
            }
            catch (Exception e) 
            { 
                ProvideUIUpdate(e.Message, true); 
                return true; 
            }
            UpdateUILabel("Finished!");
            return false;
        }

        /// <summary>
        /// Utility method to take the int array of pixel values and convert them to an int16 array that will be converted to bmp format
        /// </summary>
        /// <param name="data"></param>
        /// <param name="sliceNum"></param>
        /// <param name="writeLocation"></param>
        private void FromTwoDimIntArrayGray(Int32[,] data, int sliceNum, string writeLocation)
        {
            // Transform 2-dimensional Int32 array to 1-byte-per-pixel byte array
            Int32 width = data.GetLength(0);
            Int32 height = data.GetLength(1);
            //stride must be a multiple of 4
            Int32 stride = 4 * ((width * 2 + 3) / 4);
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
                    dataBytes[byteIndex] = (Byte)(data[x, y] >> 08);
                    dataBytes[byteIndex + 1] = (Byte)data[x, y];

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
            FileStream stream = new FileStream(Path.Combine(writeLocation, String.Format("{0}.bmp", sliceNum)), FileMode.Create);

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
        #endregion

        #region DCM export
        /// <summary>
        /// Preliminary checks prior to exporting the CT series in dicom format. Need to verify the paths of the import/export locations, verify the integrity of the daemons, and check the connection between the local and database daemon
        /// </summary>
        /// <returns></returns>
        private bool PreliminaryChecksDCM()
        {
            UpdateUILabel("Preliminary Checks:");
            int percentComplete = 0;
            int calcItems = 6;

            if (VerifyPathIntegrity(_data.WriteLocation)) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Write location path {_data.WriteLocation} verified");
            if (VerifyPathIntegrity(_data.ImportLocation)) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Import data path {_data.ImportLocation} verified");

            if (VerifyDaemonIntegrity(_data.AriaDBDaemon)) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Aria DB Daemon integrity verified");
            if (VerifyDaemonIntegrity(_data.VMSFileDaemon)) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"VMS File Daemon integrity verified");
            if (VerifyDaemonIntegrity(_data.LocalDaemon)) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Local file Daemon integrity verified");

            if (CheckDaemonConnection()) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Daemon connection verified");
            return false;
        }

        /// <summary>
        /// Helper method to verify the integrity of the daemon parameters that were parsed from the configuration file
        /// </summary>
        /// <param name="daemon"></param>
        /// <returns></returns>
        private bool VerifyDaemonIntegrity(DaemonModel daemon)
        {
            //check if anything was assigned to this daemon (the port, item3, is set to -1 by default)
            if (daemon.Port == -1) return true;
            return false;
        }

        /// <summary>
        /// Helper method to construct the aria database and local daemons and ensure the two are talking to each other
        /// </summary>
        /// <returns></returns>
        private bool CheckDaemonConnection() 
        {
            //check connection between local file daemon and aria database daemon
            Entity ariaDBDaemon = new Entity(_data.AriaDBDaemon.AETitle, _data.AriaDBDaemon.IP, _data.AriaDBDaemon.Port);
            Entity localDaemon = Entity.CreateLocal(_data.LocalDaemon.AETitle, _data.LocalDaemon.Port);
            if (PingDaemon(ariaDBDaemon, localDaemon)) return true;
            return false;
        }

        /// <summary>
        /// Ping the aria database daemon to ensure the local daemon can talk to the database daemon
        /// </summary>
        /// <param name="daemon"></param>
        /// <param name="local"></param>
        /// <returns></returns>
        private bool PingDaemon(Entity daemon, Entity local)
        {
            ProvideUIUpdate($"C Echo from {local.AeTitle} => {daemon.AeTitle} @ {daemon.IpAddress} : {daemon.Port}");
            DICOMSCU client = new DICOMSCU(local);
            //5 sec timeout
            bool success = client.Ping(daemon, 5000);
            ProvideUIUpdate($"Success: {success}", !success);
            return !success;
        }

        /// <summary>
        /// Export the CT series in dicom format using the construct daemons
        /// </summary>
        /// <returns></returns>
        private bool ExportAsDCM()
        {
            UpdateUILabel("Exporting Dicom Images:");
            int percentComplete = 0;
            int calcItems = 3;

            Entity ariaDBDaemon = new Entity(_data.AriaDBDaemon.AETitle, _data.AriaDBDaemon.IP, _data.AriaDBDaemon.Port);
            Entity VMSFileDaemon = new Entity(_data.VMSFileDaemon.AETitle, _data.VMSFileDaemon.IP, _data.VMSFileDaemon.Port);
            Entity localDaemon = Entity.CreateLocal(_data.LocalDaemon.AETitle, _data.LocalDaemon.Port);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Constructed Daemon entities");

            DICOMSCU client = new DICOMSCU(localDaemon);
            DICOMSCP receiver = new DICOMSCP(localDaemon)
            {
                SupportedAbstractSyntaxes = AbstractSyntax.ALL_RADIOTHERAPY_STORAGE,
            };
            receiver.DIMSEService.CStoreService.CStorePayloadAction = (dcm, asc) =>
            {
                string path = Path.Combine(_data.WriteLocation, dcm.GetSelector().SOPInstanceUID.Data + ".dcm");
                ProvideUIUpdate($"Writing file {dcm.GetSelector().SOPInstanceUID.Data + ".dcm"}");
                dcm.Write(path);
                return true;
            };
            receiver.ListenForIncomingAssociations(true);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Configured client and receiver for C-move operations");

            EvilDICOM.Network.SCUOps.CFinder finder = client.GetCFinder(ariaDBDaemon);
            List<EvilDICOM.Network.DIMSE.IOD.CFindStudyIOD> studies = finder.FindStudies(_patID).ToList();
            List<EvilDICOM.Network.DIMSE.IOD.CFindSeriesIOD> series = finder.FindSeries(studies).ToList();
            ProvideUIUpdate(100 * ++percentComplete / calcItems);
            ProvideUIUpdate($"Found {series.Count()} series for {_patID}");
            ProvideUIUpdate($"Found {series.Where(x => x.Modality == "CT").Count()} total CT images for {_patID}");

            if (series.Any(x => string.Equals(x.SeriesInstanceUID, _image.Series.UID)))
            {
                ProvideUIUpdate($"Found matching series for {_image.Id} based on UID: {_image.Series.UID}");
                EvilDICOM.Network.DIMSE.IOD.CFindSeriesIOD ctSeries = series.First(x => x.SeriesInstanceUID == _image.Series.UID);
                List<EvilDICOM.Network.DIMSE.IOD.CFindInstanceIOD> imageStack = finder.FindImages(ctSeries).ToList();
                ProvideUIUpdate($"Total CT slices for export: {imageStack.Count()}");

                if (PingAutoContouringProgram(imageStack.Count)) return true;

                EvilDICOM.Network.SCUOps.CMover mover = client.GetCMover(ariaDBDaemon);
                ushort msgId = 1;
                int numImages = imageStack.Count();
                int counter = 0;
                ProvideUIUpdate("Exporting CT series now");
                ProvideUIUpdate("Exporting image:");
                foreach (var img in imageStack)
                {
                    ProvideUIUpdate(100 * ++counter / numImages, $"{img.SOPInstanceUID}");
                    EvilDICOM.Network.DIMSE.CMoveResponse response = mover.SendCMove(img, VMSFileDaemon.AeTitle, ref msgId);
                    if ((Status)response.Status != Status.SUCCESS)
                    {
                        CMoveFailed(response);
                        CheckAndRemoveImagesIfPresent();
                        return true;
                    }
                }
            }
            else
            {
                ProvideUIUpdate($"Error! Matching image series not found for UID: {_image.Series.UID}! Exiting!", true);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Write a simple file with the number of images in the file name (no contents) so Autoseg listener knows how many images to expect. Also returns the full file path of the written file
        /// </summary>
        /// <param name="numImgs"></param>
        /// <returns></returns>
        private (bool, string) WriteNumImgsFile(int numImgs)
        {
            ProvideUIUpdate($"Writing number of images file for autocontouring program");
            string folderLoc = Path.Combine(_data.WriteLocation, _patID);
            if (!Directory.Exists(folderLoc))
            {
                ProvideUIUpdate($"Creating directory: {folderLoc}");
                Directory.CreateDirectory(folderLoc);
            }
            string fileName = folderLoc + $"\\{numImgs}.txt";
            try
            {
                ProvideUIUpdate($"Writing num images file: {fileName}");
                File.WriteAllText(fileName, "");
            }
            catch (Exception e)
            {
                ProvideUIUpdate($"Could not write num images file because: {e.Message}", true);
                return (true, fileName);
            }
            return (false, fileName);
        }

        /// <summary>
        /// Method for pinging autocontouring program to ensure it is up and running. Specific to our implementation
        /// </summary>
        /// <param name="numImgs"></param>
        /// <returns></returns>
        private bool PingAutoContouringProgram(int numImgs)
        {
            (bool fail, string fileName) = WriteNumImgsFile(numImgs);
            if (fail) return true;

            if (WaitForAutocontouringProgramResponse(fileName)) return true;
            return false;
        }

        /// <summary>
        /// Method to wait for a 'response' from the autocontouring program. Here, response means the autocontouring program recieved and removed the number of images file that was previously written. 
        /// </summary>
        /// <param name="theFile"></param>
        /// <returns></returns>
        private bool WaitForAutocontouringProgramResponse(string theFile)
        {
            ProvideUIUpdate("Waiting for response from AI contouring program");
            ProvideUIUpdate($"Approximate elapsed time:");
            //10 sec timeout
            int timeout = 20000;
            int elapsedTime = 0;
            while (elapsedTime <= timeout)
            {
                ProvideUIUpdate($"{elapsedTime} msec");
                //if file does NOT exist --> was read by AI contouring program and removed --> AI contouring program is up and running
                if (!File.Exists(theFile))
                {
                    ProvideUIUpdate($"Autocontouring program ping: SUCCESS");
                    return false;
                }
                //wait 1 sec and check again. Super janky way to keep track of elapsed time, but it works
                Thread.Sleep(1000);
                elapsedTime += 1000;
            }
            ProvideUIUpdate($"Autocontouring program ping: FAILED", true);
            //num images file still exists after 10 sec period --> something is wrong with autocontouring program, don't bother 
            return true;
        }

        /// <summary>
        /// Helper method to delete the export folder and all its contents if something went wrong with the CT export
        /// </summary>
        /// <returns></returns>
        private bool CheckAndRemoveImagesIfPresent()
        {
            string folderLoc = Path.Combine(_data.WriteLocation, _patID);
            if (Directory.Exists(folderLoc))
            {
                Directory.Delete(folderLoc, true);
            }
            return false;
        }

        /// <summary>
        /// Helper method to report a failed C-move
        /// </summary>
        /// <param name="response"></param>
        private void CMoveFailed(EvilDICOM.Network.DIMSE.CMoveResponse response)
        {
            ProvideUIUpdate($"Error! C-Move operation failed!");
            ProvideUIUpdate("DICOM C-Move Results:");
            ProvideUIUpdate($"Completed moves: {response.NumberOfCompletedOps}");
            ProvideUIUpdate($"Failed moves: {response.NumberOfFailedOps}");
            ProvideUIUpdate($"Warning moves: {response.NumberOfWarningOps}");
            ProvideUIUpdate($"Remaining moves: {response.NumberOfRemainingOps}");
            ProvideUIUpdate("Exiting!", true);
        }

        /// <summary>
        /// Helper method to launch the import listener console application
        /// </summary>
        /// <returns></returns>
        private bool LaunchListener()
        {
            ProvideUIUpdate("Launching import listener...");
            string listener = ImportListenerHelper.GetImportListenerExePath();
            ProvideUIUpdate($"Listener path: {listener}");

            if (ImportListenerHelper.LaunchImportListener(listener, _data, _patID))
            {
                ProvideUIUpdate("Error! Could not find listener executable or could not launch executable! Exiting!");
                return true;
            }
            else ProvideUIUpdate("Import listener launched succesfully!");
            return false;
        }
        #endregion
    }
}
