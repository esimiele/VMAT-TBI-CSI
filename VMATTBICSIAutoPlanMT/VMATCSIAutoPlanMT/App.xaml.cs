using System.Collections.Generic;
using System.Windows;
using System.IO;
using System.Reflection;

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
            if (e.Args.Length > 1)
            {
                //only add first two arguments (patient id and structure set). Don't care about 3rd argument
                for (int i = 0; i < 2; i++) theArguments.Add(e.Args[i]);
            }

            VMAT_CSI.CSIAutoPlanMW mw = new VMAT_CSI.CSIAutoPlanMW(theArguments);
            if(!mw.GetCloseOpenPatientWindowStatus()) mw.Show();
        }
    }
}
