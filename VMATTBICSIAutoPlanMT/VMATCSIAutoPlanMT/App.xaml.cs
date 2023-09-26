using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.Logging;

namespace VMATCSIAutoPlanMT
{
    public partial class App : Application
    {
        /// <summary>
        /// Called when application is launched. Copy the starteventargs into a string list and pass to main UI or pass them to the autoconvert high to default
        /// res class (indicated by the first argument in the startup args list)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            List<string> theArguments = new List<string> { };
            for (int i = 0; i < e.Args.Length; i++) theArguments.Add(e.Args[i]);

            if (theArguments.Any() && string.Equals(theArguments.First(), "-d"))
            {
                //called from import listener. Need to auto-downsample some important structures
                string logPath = ConfigurationHelper.ReadLogPathFromConfigurationFile(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\log_configuration.ini");
                //if the log file path in the configuration file is empty, use the default path
                if (string.IsNullOrEmpty(logPath)) logPath = ConfigurationHelper.GetDefaultLogPath();
                Logger log = new Logger(logPath,
                                        VMATTBICSIAutoPlanningHelpers.Enums.PlanType.VMAT_CSI,
                                        theArguments.ElementAt(1));

                log.OpType = VMATTBICSIAutoPlanningHelpers.Enums.ScriptOperationType.AutoConvertHighToDefaultRes;
                VMAT_CSI.AutoResConverter ARC = new VMAT_CSI.AutoResConverter(theArguments.ElementAt(1), theArguments.ElementAt(2));
                bool result = ARC.Execute();
                log.AppendLogOutput(ARC.GetLogOutput().ToString());
                if (result)
                {
                    log.AppendLogOutput(ARC.GetErrorStackTrace());
                    log.LogError("Unable to convert high resolution structures to default resolution! Try running the script normally and select the 'Generate Prelim Targets' tab");
                }
                AppClosingHelper.CloseApplication(ARC.GetAriaApplicationInstance(), ARC.GetIsPatientOpenStatus(), ARC.GetAriaIsModifiedStatus(), true, log);
                Current.Shutdown();
            }
            else
            {
                VMAT_CSI.CSIAutoPlanMW mw = new VMAT_CSI.CSIAutoPlanMW(theArguments);
                if (!mw.GetCloseOpenPatientWindowStatus()) mw.Show();
            }
        }
    }
}
