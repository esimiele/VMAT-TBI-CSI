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
using EvilDICOM;
using EvilDICOM.Core;
using EvilDICOM.Core.Helpers;
using EvilDICOM.Core.Element;
using EvilDICOM.Network;
using EvilDICOM.Network.Enums;

namespace VMATTBICSIAutoplanningHelpers.Helpers
{
    public class CTImageExport
    {
        //overridden during construction of class
        private string _DCMTK_BIN_PATH; // path to DCMTK binaries
        private string _AET;                 // local AE title
        private string _AEC;               // AE title of VMS DB Daemon
        private string _AEM;                 // AE title of VMS File Daemon
        private string _IP_PORT;// IP address of server hosting the DB Daemon, port daemon is listening to
        string _CMD_FILE_FMT;
        StringBuilder stdErr = new StringBuilder("");

        private string _exportFormat;
        private VMS.TPS.Common.Model.API.Image _image;
        string _writeLocation;
        string _patID;

        public CTImageExport(VMS.TPS.Common.Model.API.Image img, string imgWriteLocation, string patientID, string exportFormat = "png", string DCMTK_BIN_PATH = @"N:\RadiationTherapy\Public\CancerCTR\RSPD\v36\bin", string AET = @"DCMTK", string AEC = @"VMSDBD", string AEM = @"VMSFD", string IP_PORT = @" 10.151.176.60 51402", string CMD_FILE_FMT = @"move-{1}-{2}.cmd") 
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

        public bool exportImage()
        {
            bool result = false;
            if (_exportFormat == "png") exportAsPNG();
            else if (_exportFormat == "dcm") exportAsDCM();
            return result;
        }

        private bool exportAsPNG()
        {
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
                for (int k = 0; k < _image.ZSize; k++)
                {
                    Bitmap bmp = new Bitmap(_image.XSize, _image.YSize, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
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
                            bmp.SetPixel(i, j, System.Drawing.Color.FromArgb(255, r, g, b));
                        }
                    }
                    bmp.Save(String.Format(@"{0}\{1}_{2}.png", folderLoc, ct_ID, k));
                }
            }
            catch (Exception e) { MessageBox.Show(e.Message); return true; }
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

        private void exportAsDCM()
        {
            string temp = System.Environment.GetEnvironmentVariable("TEMP");
            string filename = MakeFilenameValid(String.Format(_CMD_FILE_FMT, _patID, _image.Id));
            filename = temp + @"\" + filename;
            GenerateDicomMoveScript(filename);

            string stdErr1;
            string logFile = filename + "-log.txt";
            using (Process process = new Process())
            {

                // this powershell command allows us to see the standard output and also log it.
                string command = string.Format(@"&'{0}' | tee-object -filepath '{1}'", filename, logFile);
                // Configure the process using the StartInfo properties.
                process.StartInfo.FileName = "PowerShell.exe";
                process.StartInfo.Arguments = command;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardError = true;

                // Set our event handler to asynchronously accumulate std err
                process.ErrorDataReceived += new DataReceivedEventHandler(stdErrHandler);

                process.Start();

                // Read the error stream first and then wait.
                stdErr1 = process.StandardError.ReadToEnd();
                //        process.BeginErrorReadLine();
                //process.WaitForExit();
                process.Close();
            }

            // dump out the standard error file, show them to user if they exist, and exit with a nice message.
            string stdErrFile = "";

            if (stdErr1.Length > 0)
            {
                stdErrFile = filename + "-err.txt";
                System.IO.File.WriteAllText(stdErrFile, stdErr1);
            }

            /*
            string message = string.Format("Done processing. \n\nCommand File = {0}\n\nLog file: {1}\nStandard error log: {2}", filename, logFile, stdErrFile);
            MessageBox.Show(message, "Varian Developer");

            // 'Start' generated log file to launch Notepad
            System.Diagnostics.Process.Start(logFile);
            // 'Start' generated text file to launch Notepad
            if (stdErr1.Length > 0)
                System.Diagnostics.Process.Start(stdErrFile);
            // Sleep for a few seconds to let notepad start
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(2));
            */

            string sourcePath = @"\\shariatscap105\Dicom\RSDCM\Export\" + _patID;
            string targetPath = @"P:\_DEMO\" + _patID;
            if (!System.IO.Directory.Exists(targetPath))
            {
                System.IO.Directory.CreateDirectory(targetPath);
            }
            string fileName;
            string destFile;
            if (System.IO.Directory.Exists(sourcePath))
            {
                string[] files = System.IO.Directory.GetFiles(sourcePath);
                // Copy the files and overwrite destination files if they already exist.
                foreach (string s in files)
                {
                    // Use static Path methods to extract only the file name from the path.
                    fileName = System.IO.Path.GetFileName(s);
                    destFile = System.IO.Path.Combine(targetPath, fileName);
                    System.IO.File.Copy(s, destFile, true);
                }
            }
            else
            {
                Console.WriteLine("Source path does not exist!");
            }

            ProcessStartInfo processStartInfo = new ProcessStartInfo();
            Process exeProcess;
            processStartInfo.FileName = @"P:\_DEMO\Eclipse Scripting API\Plugins\main.exe";
            processStartInfo.CreateNoWindow = false;
            processStartInfo.UseShellExecute = true;
            exeProcess = Process.Start(processStartInfo);
            Thread.Sleep(30000);

            string exeSourcePath = @"P:\_DEMO\" + _patID;
            string exeTargetPath = @"P:\_DEMO\Eclipse Scripting API\Plugins\InputData\" + _patID;
            if (!System.IO.Directory.Exists(exeTargetPath))
            {
                System.IO.Directory.CreateDirectory(exeTargetPath);
            }
            string exeFileName;
            string exeDestFile;
            if (System.IO.Directory.Exists(exeSourcePath))
            {
                string[] exeFiles = System.IO.Directory.GetFiles(exeSourcePath);
                // Copy the files and overwrite destination files if they already exist.
                foreach (string s in exeFiles)
                {
                    // Use static Path methods to extract only the file name from the path.
                    exeFileName = System.IO.Path.GetFileName(s);
                    exeDestFile = System.IO.Path.Combine(exeTargetPath, exeFileName);
                    System.IO.File.Copy(s, exeDestFile, true);
                }
            }
            else
            {
                Console.WriteLine("Source path does not exist!");
            }

            //// IMPORT SECTION
            //while (true)
            //{
            //    if (File.Exists(@"P:\_DEMO\Eclipse Scripting API\Plugins\OutputData\ContouredByAI.dcm"))
            //    {
            //        var daemon = new Entity("VMSDBD", "10.151.176.60", 51402);
            //        var local = Entity.CreateLocal("DCMTK", 50400);
            //        var client = new DICOMSCU(local);
            //        var storer = client.GetCStorer(daemon);
            //        var outputPath = @"P:\_DEMO\Eclipse Scripting API\Plugins\OutputData";
            //        ushort msgId = 1;
            //        var dcmFiles = Directory.GetFiles(outputPath);
            //        foreach (var path in dcmFiles)
            //        {
            //            var dcm = DICOMObject.Read(path);
            //            var response = storer.SendCStore(dcm, ref msgId);
            //            /*
            //            MessageBox.Show($"DICOM C-Store from {local.AeTitle} => " +
            //                $"{daemon.AeTitle} @{daemon.IpAddress}:{daemon.Port}:" +
            //                $"{(Status)response.Status}");
            //            */
            //        }
            //        break;
            //    }
            //    Thread.Sleep(10000);
            //}
        }

        private void stdErrHandler(object sendingProcess, DataReceivedEventArgs outLine)
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
        private void GenerateDicomMoveScript(string filename)
        {
            string move = "movescu -v -aet " + _AET + " -aec " + _AEC + " -aem " + _AEM + " -S -k ";
            StreamWriter sw = new StreamWriter(filename, false, Encoding.ASCII);

            sw.WriteLine(@"@set PATH=%PATH%;" + _DCMTK_BIN_PATH);

            // write the command to move the 3D image data set
            if (_image != null)
            {
                sw.WriteLine("rem move 3D image " + _image.Id);
                string cmd = move + '"' + "0008,0052=SERIES" + '"' + " -k " + '"' + "0020,000E=" + _image.Series.UID + '"' + _IP_PORT;
                sw.WriteLine(cmd);
            }

            sw.Flush();
            sw.Close();
        }
    }
}
