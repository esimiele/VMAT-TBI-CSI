using System.Collections.Generic;
using System.Linq;
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
            Window mw = new LauncherMainWindow(e.Args.ToList());
            mw.Show();
        }
    }
}
