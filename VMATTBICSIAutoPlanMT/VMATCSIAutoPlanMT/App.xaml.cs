using System.Collections.Generic;
using System.Linq;
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
            //string[] dummyArgs = { "-d", "$CSIDryRun_4", "" };
            //List<string> theArguments = new List<string> { };
            //for (int i = 0; i < dummyArgs.Length; i++) theArguments.Add(dummyArgs[i]);

            List<string> theArguments = new List<string> { };
            for (int i = 0; i < e.Args.Length; i++) theArguments.Add(e.Args[i]);

            if (theArguments.Any() && string.Equals(theArguments.First(), "-d"))
            {
                //called from import listener. Need to auto-downsample some important structures
                VMAT_CSI.AutoResConverter ARC = new VMAT_CSI.AutoResConverter(theArguments.ElementAt(1), theArguments.ElementAt(2));
                bool result = ARC.Execute();
                if (result) MessageBox.Show("Unable to convert high resolution structures to default resolution! Try running the script normally and select the 'Generate Prelim Targets' tab");
            }
            else
            {
                VMAT_CSI.CSIAutoPlanMW mw = new VMAT_CSI.CSIAutoPlanMW(theArguments);
                if (!mw.GetCloseOpenPatientWindowStatus()) mw.Show();
            }
        }
    }
}
