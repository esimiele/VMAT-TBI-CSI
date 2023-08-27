using System.Collections.Generic;
using System.Windows;

namespace VMATCSIAutoPlanMT
{
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
            for (int i = 0; i < e.Args.Length; i++) theArguments.Add(e.Args[i]);

            VMAT_CSI.CSIAutoPlanMW mw = new VMAT_CSI.CSIAutoPlanMW(theArguments);
            if(!mw.GetCloseOpenPatientWindowStatus()) mw.Show();
        }
    }
}
