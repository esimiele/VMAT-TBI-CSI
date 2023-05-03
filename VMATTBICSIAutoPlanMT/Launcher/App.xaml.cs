using System.Collections.Generic;
using System.Windows;

namespace Launcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
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
            //only add first two arguments (patient id and structure set). Don't care about 3rd argument
            for (int i = 0; i < e.Args.Length; i++) theArguments.Add(e.Args[i]);

            Window mw = new LauncherMainWindow(theArguments);
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
