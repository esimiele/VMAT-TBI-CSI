using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;
using System.Threading;
using System.Timers;
using System.IO;
using System.Collections.Generic;
using EvilDICOM.Core;
using EvilDICOM.Core.Helpers;
using EvilDICOM.Network;
using EvilDICOM.Network.Enums;

namespace ImportListener
{
    class Program
    {
        static string path;
        static string mrn;
        static string ariaDBAET;
        static string ariaDBIP;
        static int ariaDBPort;
        static string localAET;
        static int localPort;
        //timeout in seconds (20 mins by default)
        static double timeout = 20 * 60.0;

        static bool filePresent = false;
        static string theFile;
        static double elapsedMS = 0.0;
        static int barCount = 0;
        private static System.Timers.Timer aTimer;

        static void Main(string[] args)
        {
            try
            {
                SetTimer();
                if (!ParseInputArguments(args.ToList()))
                {
                    aTimer.Start();
                    PrintConfiguration();
                    ListenForRTStruct();
                    if (filePresent) ImportRTStructureSet();
                }
                else
                {
                    Console.WriteLine("Error! Unable to parse command line arguments!");
                }
            }
            catch (Exception e)
            {
                aTimer.Stop();
                Console.Error.WriteLine(e.ToString());
                Console.Error.WriteLine(e.StackTrace);
            }
            aTimer.Dispose();
            Console.WriteLine("Press any key to exit");
            Console.ReadLine();
        }

        private static bool ParseInputArguments(List<string> args)
        {
            if (args.Any())
            {
                path = args.ElementAt(0);
                mrn = args.ElementAt(1);
                ariaDBAET = args.ElementAt(2);
                ariaDBIP = args.ElementAt(3);
                ariaDBPort = int.Parse(args.ElementAt(4));
                localAET = args.ElementAt(5);
                localPort = int.Parse(args.ElementAt(6));
                if (args.Count() == 8) timeout = double.Parse(args.ElementAt(7));
                return false;
            }
            else return true;
        }

        private static void PrintConfiguration()
        {
            Console.WriteLine(DateTime.Now);
            Console.WriteLine("Configuration:");
            Console.WriteLine($"Import path: {path}");
            Console.WriteLine($"Patient Id: {mrn}");
            Console.WriteLine($"Aria DB Daemon AE Title: {ariaDBAET}");
            Console.WriteLine($"Aria DB Daemon IP: {ariaDBIP}");
            Console.WriteLine($"Aria DB Daemon Port: {ariaDBPort}");
            Console.WriteLine($"Local Daemon AE Title: {localAET}");
            Console.WriteLine($"Local Daemon Port: {localPort}");
            Console.WriteLine($"Requested timeout: {timeout} seconds");
            Console.WriteLine("");
            Console.WriteLine("Listening for RT structure set...");

        }

        private static void SetTimer()
        {
            // Create a timer with a two second interval.
            aTimer = new System.Timers.Timer(500);
            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
        }

        private static void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            //increment the time on the progress window for each "tick", which is set to intervals of 1 second
            if (barCount == 20)
            {
                Console.Clear();
                barCount = 0;
                PrintConfiguration();
            }
            Console.Write("=");
            barCount++;
            elapsedMS += 500;
        }

        private static (Entity, Entity) ConstructDaemons()
        {
            Entity ariaDBDaemon = new Entity(ariaDBAET, ariaDBIP, ariaDBPort);
            Entity localDaemon = Entity.CreateLocal(localAET, localPort);
            return (ariaDBDaemon, localDaemon);
        }

        private static void ListenForRTStruct()
        {
            while(!filePresent && elapsedMS / 1000 < timeout)
            {
                if (CheckDirectoryForRTStruct())
                {
                    filePresent = true;
                    aTimer.Stop();
                    Console.WriteLine("");
                    Console.WriteLine($"Auto contours for patient {mrn} found");
                }
                Thread.Sleep(1000);
            }
            Console.WriteLine($"Elapsed time: {elapsedMS}");
        }

        private static bool CheckDirectoryForRTStruct()
        {
            foreach (string file in Directory.GetFiles(path))
            {
                //get the names of each patient whose CT data is in the CT DICOM dump directory
                DICOMObject dcmObj = DICOMObject.Read(file);
                if (string.Equals(dcmObj.FindFirst(TagHelper.PatientID).DData as string, mrn))
                {
                    theFile = file;
                    return true;
                }
            }
            return false;
        }

        private static bool ImportRTStructureSet()
        {
            Console.WriteLine("Importing structure set now...");
            (Entity ariaDBDaemon, Entity localDaemon) = ConstructDaemons();
            if (PingDaemon(ariaDBDaemon, localDaemon)) return true;

            DICOMSCU client = new DICOMSCU(localDaemon);
            EvilDICOM.Network.SCUOps.CStorer storer = client.GetCStorer(ariaDBDaemon);
            ushort msgId = 1;
            DICOMObject dcm = DICOMObject.Read(theFile);

            Console.WriteLine("Executing C-store operation now...");
            EvilDICOM.Network.DIMSE.CStoreResponse response = storer.SendCStore(dcm, ref msgId);
            if ((Status)response.Status != Status.SUCCESS)
            {
                Console.WriteLine($"CStore failed");
            }
            else
            {
                Console.WriteLine($"DICOM C-Store from {localDaemon.AeTitle} => {ariaDBDaemon.AeTitle} @{ariaDBDaemon.IpAddress}:{ariaDBDaemon.Port}: {(Status)response.Status}");
            }
            return false;
        }

        private static bool PingDaemon(Entity daemon, Entity local)
        {
            Console.WriteLine($"C Echo from {local.AeTitle} => {daemon.AeTitle} @ {daemon.IpAddress} : {daemon.Port}");
            DICOMSCU client = new DICOMSCU(local);
            //5 sec timeout
            bool success = client.Ping(daemon, 5000);
            Console.WriteLine($"Success: {success}", !success);
            return !success;
        }
    }
}
