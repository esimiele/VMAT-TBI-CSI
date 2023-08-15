using System.Collections.Generic;
using System.Windows;

namespace Launcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Called when application is launched. Copy the starteventargs into a string list and pass to main UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            List<string> theArguments = new List<string> { };
            //only add first two arguments (patient id and structure set). Don't care about 3rd argument
            for (int i = 0; i < e.Args.Length; i++) theArguments.Add(e.Args[i]);

            Window mw = new LauncherMainWindow(theArguments);
            mw.Show();
        }
    }
}
