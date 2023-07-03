using System;
using System.Windows;
using System.IO;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Linq;
using EvilDICOM;
using EvilDICOM.Core;
using EvilDICOM.Core.Helpers;
using EvilDICOM.Core.Element;
using EvilDICOM.Network;
using EvilDICOM.Network.Enums;
using SimpleProgressWindow;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public class CTImageExport : SimpleMTbase
    {
        //overridden during construction of class
        private string _DCMTK_BIN_PATH; // path to DCMTK binaries
        private string _AET;                 // local AE title
        private string _AEC;               // AE title of VMS DB Daemon
        private string _AEM;                 // AE title of VMS File Daemon
        private string _IP_PORT;// IP address of server hosting the DB Daemon, port daemon is listening to
        private string _CMD_FILE_FMT;
        private StringBuilder stdErr = new StringBuilder("");

        private string _exportFormat;
        private VMS.TPS.Common.Model.API.Image _image;
        private string _writeLocation;
        private string _patID;

        public CTImageExport(VMS.TPS.Common.Model.API.Image img, 
                             string imgWriteLocation, 
                             string patientID, 
                             string exportFormat = "dcm", 
                             string DCMTK_BIN_PATH = @"N:\RadiationTherapy\Public\CancerCTR\RSPD\v36\bin", 
                             string AET = @"DCMTK", 
                             string AEC = @"VMSDBD", 
                             string AEM = @"VMSFD", 
                             string IP_PORT = @" 10.151.176.60 51402", 
                             string CMD_FILE_FMT = @"move-{0}-{1}.cmd") 
        {
            _DCMTK_BIN_PATH = DCMTK_BIN_PATH;
            _AET = AET;
            _AEC = AEC;
            _AEM = AEM;
            _IP_PORT = IP_PORT;
            _CMD_FILE_FMT = CMD_FILE_FMT;
            _exportFormat = exportFormat;
            _image = img;
            _writeLocation = imgWriteLocation;
            _patID = patientID;
        }

        public override bool Run()
        {
            return ExportImage();
        }

        public bool ExportImage()
        {
            bool result = false;
            if (_exportFormat == "png") result = ExportAsPNG();
            else if (_exportFormat == "dcm") result = ExportAsDCM();
            if(!result) ProvideUIUpdate($"{_image.Id} has been exported successfully!");
            return result;
        }

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

            //the main limitation of this method is the maximum bit depth is limited to 8 (whereas CT data has a bit depth of 12)
            try
            {
                string ct_ID = _image.Id;
                string folderLoc = Path.Combine(_writeLocation, _patID);
                if (!Directory.Exists(folderLoc)) Directory.CreateDirectory(folderLoc);
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), "Initializtion complete. Exporting");
                for (int k = 0; k < _image.ZSize; k++)
                {
                    ProvideUIUpdate((int)(100 * ++percentComplete / calcItems));
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

        private bool ExportAsDCM()
        {
            //GenerateDicomMoveScript(filename);
            return ExportCTDataAsDCM();
        }

        private void StdErrHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (!String.IsNullOrEmpty(outLine.Data))
            {
                stdErr.Append(Environment.NewLine + outLine.Data);
            }
        }
        private string MakeFilenameValid(string s)
        {
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            foreach (char ch in invalidChars)
            {
                s = s.Replace(ch, '_');
            }
            return s;
        }

        public bool ExportCTData()
        {
            var daemon = new Entity("VMSDBD", "10.151.176.60", 51402);
            var local = Entity.CreateLocal("DCMTK", 50400);
            var client = new DICOMSCU(local);

            ProvideUIUpdate($"C Echo from {local.AeTitle} => {daemon.AeTitle} @ {daemon.IpAddress} : {daemon.Port}");
            ProvideUIUpdate($"Success: {client.Ping(daemon)}");
            return false;
        }

        public bool ExportCTDataAsDCM()
        {
            var AriaDBDaemon = new Entity("VMSDBD", "10.151.176.60", 51402);
            var VMSFileDaemon = new Entity("VMSFD", "10.151.176.60", 51402);

            //var daemon = new Entity("VMSFD", "10.151.176.60", 51402);
            var local = Entity.CreateLocal("DCMTK", 50400);
            var client = new DICOMSCU(local);

            ProvideUIUpdate($"C Echo from {local.AeTitle} => {AriaDBDaemon.AeTitle} @ {AriaDBDaemon.IpAddress} : {AriaDBDaemon.Port}");
            bool success = client.Ping(AriaDBDaemon, 2000);
            ProvideUIUpdate($"Success: {success}", !success);
            if(!success) return true;

            var receiver = new DICOMSCP(local);
            receiver.SupportedAbstractSyntaxes = AbstractSyntax.ALL_RADIOTHERAPY_STORAGE;
            if(!Directory.Exists(_writeLocation))
            {
                ProvideUIUpdate($"Error! {_writeLocation} does not exist! Exiting!", true);
                return true;
            }

            receiver.DIMSEService.CStoreService.CStorePayloadAction = (dcm, asc) =>
            {
                var path = Path.Combine(_writeLocation, dcm.GetSelector().SOPInstanceUID.Data + ".dcm");
                ProvideUIUpdate($"Writing file {dcm.GetSelector().SOPInstanceUID.Data + ".dcm"}");
                dcm.Write(path);
                return true;
            };
            receiver.ListenForIncomingAssociations(true);

            var finder = client.GetCFinder(AriaDBDaemon);
            var studies = finder.FindStudies(_patID);
            var series = finder.FindSeries(studies);
            ProvideUIUpdate($"Found {series.Count()} series for {_patID}");
            ProvideUIUpdate($"Found {series.Where(x => x.Modality == "CT").Count()} total CT images for {_patID}");

            if(series.Any(x => string.Equals(x.SeriesInstanceUID,_image.Series.UID)))
            {
                ProvideUIUpdate($"Found matching series for {_image.Id} based on UID: {_image.Series.UID}");
                var ctSeries = series.First(x => x.SeriesInstanceUID == _image.Series.UID);
                var imageStack = finder.FindImages(ctSeries);
                ProvideUIUpdate($"Total CT slices for export: {imageStack.Count()}");

                var mover = client.GetCMover(AriaDBDaemon);
                ushort msgId = 1;
                int numImages = imageStack.Count();
                int counter = 0;
                foreach (var img in imageStack)
                {
                    ProvideUIUpdate((int)(100 * ++counter / numImages),$"Exporting image: {img.SOPInstanceUID}");
                    var response = mover.SendCMove(img, "VMSFD", ref msgId);
                    if(response.NumberOfCompletedOps != 1)
                    {
                        ProvideUIUpdate($"Error! C-Move operation failed!");
                        ProvideUIUpdate("DICOM C-Move Results:");
                        ProvideUIUpdate($"Completed moves: {response.NumberOfCompletedOps}");
                        ProvideUIUpdate($"Failed moves: {response.NumberOfFailedOps}");
                        ProvideUIUpdate($"Warning moves: {response.NumberOfWarningOps}");
                        ProvideUIUpdate($"Remaining moves: {response.NumberOfRemainingOps}");
                        ProvideUIUpdate("Exiting!",true);
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

        public void GenerateDicomMoveScript3(string filename)
        {
            MessageBox.Show("1");
            var daemon = new Entity("VMSDBD", "10.151.176.60", 51402);
            MessageBox.Show("2");
            var local = Entity.CreateLocal("DCMTK", 50400);
            MessageBox.Show("3");
            var client = new DICOMSCU(local);
            MessageBox.Show("4");
            var storer = client.GetCStorer(daemon);
            MessageBox.Show("5");
            var outputPath = @"\\shariatscap105\Dicom\RSDCM\Import";
            ushort msgId = 1;
            var dcmFiles = Directory.GetFiles(outputPath);
            foreach (var path in dcmFiles)
            {
                var dcm = DICOMObject.Read(path);
                var response = storer.SendCStore(dcm, ref msgId);
                MessageBox.Show($"DICOM C-Store from {local.AeTitle} => " +
                    $"{daemon.AeTitle} @{daemon.IpAddress}:{daemon.Port}:" +
                    $"{(Status)response.Status}");
            }
        }
    }
}
