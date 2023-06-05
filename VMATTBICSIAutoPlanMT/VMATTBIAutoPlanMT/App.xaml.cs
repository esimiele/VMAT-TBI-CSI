using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Windows;

namespace VMATTBIAutoPlanMT
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            List<string> theArguments = new List<string> { };
            if (e.Args.Length > 1)
            {
                //only add first two arguments (patient id and structure set). Don't care about 3rd argument
                for (int i = 0; i < 2; i++) theArguments.Add(e.Args[i]);
            }
            Window mw = new VMAT_TBI.TBIAutoPlanMW(theArguments);
            if (!mw.GetCloseOpenPatientWindowStatus()) mw.Show();
            //string binDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            //string optLoopExe = Directory.GetFiles(binDir, "*.exe").FirstOrDefault(x => x.Contains("VMATTBICSIOptLoopMT"));
            //ProcessStartInfo optLoopProcess = new ProcessStartInfo(optLoopExe);
            //Process.Start(optLoopProcess);
            //this.Shutdown();
        }
    }
}
