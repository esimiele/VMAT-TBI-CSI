using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using EvilDICOM;
using EvilDICOM.Core;
using EvilDICOM.Core.Helpers;
using EvilDICOM.Core.Element;
using EvilDICOM.Network;
using EvilDICOM.Network.Enums;

[assembly: AssemblyVersion("1.0.0.1")]

namespace VMS.TPS
{
    public class Script
    {
        public Script()
        {
        }
        
        public const string DCMTK_BIN_PATH = @"N:\RadiationTherapy\Public\CancerCTR\RSPD\v36\bin"; // path to DCMTK binaries
        public const string AET = @"DCMTK";                 // local AE title
        public const string AEC = @"VMSDBD";               // AE title of VMS DB Daemon
        public const string AEM = @"VMSFD";                 // AE title of VMS File Daemon
        public const string IP_PORT = @" 10.151.176.60 51402";// IP address of server hosting the DB Daemon, port daemon is listening to
        public const string CMD_FILE_FMT = @"move-{0}({1})-{2}.cmd";

        // holds standard error output collected during run of the DCMTK script
        private static StringBuilder stdErr = new StringBuilder("");
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context /*, System.Windows.Window window, ScriptEnvironment environment*/)
        {
            string temp = System.Environment.GetEnvironmentVariable("TEMP");
            string filename = MakeFilenameValid(
                string.Format(CMD_FILE_FMT, context.Patient.LastName, context.Patient.Id, context.Image.Id)
              );
            filename = temp + @"\" + filename;
            GenerateDicomMoveScript(context.Patient, context.Image, filename);

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

            string sourcePath = @"\\shariatscap105\Dicom\RSDCM\Export\" + context.Patient.Id;
            string targetPath = @"P:\_DEMO\" + context.Patient.Id;
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

            string exeSourcePath = @"P:\_DEMO\" + context.Patient.Id;
            string exeTargetPath = @"P:\_DEMO\Eclipse Scripting API\Plugins\InputData\" + context.Patient.Id;
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

            // IMPORT SECTION
            while (true)
            {
                if (File.Exists(@"P:\_DEMO\Eclipse Scripting API\Plugins\OutputData\ContouredByAI.dcm"))
                {
                    var daemon = new Entity("VMSDBD", "10.151.176.60", 51402);
                    var local = Entity.CreateLocal("DCMTK", 50400);
                    var client = new DICOMSCU(local);
                    var storer = client.GetCStorer(daemon);
                    var outputPath = @"P:\_DEMO\Eclipse Scripting API\Plugins\OutputData";
                    ushort msgId = 1;
                    var dcmFiles = Directory.GetFiles(outputPath);
                    foreach (var path in dcmFiles)
                    {
                        var dcm = DICOMObject.Read(path);
                        var response = storer.SendCStore(dcm, ref msgId);
                        /*
                        MessageBox.Show($"DICOM C-Store from {local.AeTitle} => " +
                            $"{daemon.AeTitle} @{daemon.IpAddress}:{daemon.Port}:" +
                            $"{(Status)response.Status}");
                        */
                    }
                    break;
                }
                Thread.Sleep(10000);
            }
        }
        private static void stdErrHandler(object sendingProcess,
            DataReceivedEventArgs outLine)
        {
            if (!String.IsNullOrEmpty(outLine.Data))
            {
                stdErr.Append(Environment.NewLine + outLine.Data);
            }
        }
        string MakeFilenameValid(string s)
        {
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            foreach (char ch in invalidChars)
            {
                s = s.Replace(ch, '_');
            }
            return s;
        }
        public void GenerateDicomMoveScript(Patient patient, Image image, string filename)
        {
            string move = "movescu -v -aet " + AET + " -aec " + AEC + " -aem " + AEM + " -S -k ";

            StreamWriter sw = new StreamWriter(filename, false, Encoding.ASCII);

            sw.WriteLine(@"@set PATH=%PATH%;" + DCMTK_BIN_PATH);

            // write the command to move the 3D image data set
            if (image != null)
            {
                sw.WriteLine("rem move 3D image " + image.Id);
                string cmd = move + '"' + "0008,0052=SERIES" + '"' + " -k " + '"' + "0020,000E=" + image.Series.UID + '"' + IP_PORT;
                sw.WriteLine(cmd);
            }

            sw.Flush();
            sw.Close();
        }
    }
}