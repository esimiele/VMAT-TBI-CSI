using System.Collections.Generic;
using System.Windows;
using System.IO;
using System.Reflection;

namespace VMATCSIAutoPlanMT
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            //selectOption SO;
            //if (e.Args.Length == 3) SO = new selectOption(true);
            //else SO = new selectOption(false);
            //SO.ShowDialog();
            //if (!SO.isVMATCSI && !SO.isVMATTBI && !SO.launchOptimization) Current.Shutdown();

            List<string> theArguments = new List<string> { };
            if (e.Args.Length > 1)
            {
                //only add first two arguments (patient id and structure set). Don't care about 3rd argument
                for (int i = 0; i < 2; i++) theArguments.Add(e.Args[i]);
            }
                
            Window mw = new VMAT_CSI.CSIAutoPlanMW(theArguments);
            mw.Show();
            //else
            //{
            //    string binDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            //    string optLoopExe = Directory.GetFiles(binDir, "*.exe").FirstOrDefault(x => x.Contains("VMATTBICSIOptLoopMT"));
            //    ProcessStartInfo optLoopProcess = new ProcessStartInfo(optLoopExe);
            //    Process.Start(optLoopProcess);
            //    this.Shutdown();
            //}
        }
    }
}
