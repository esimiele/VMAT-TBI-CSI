using VMATTBICSIAutoPlanningHelpers.Logging;
using VMATTBICSIAutoPlanningHelpers.Prompts;
using System.Windows;
using Application = VMS.TPS.Common.Model.API.Application;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class AppClosingHelper
    {
        /// <summary>
        /// Simple helper method for closing down the main prep script UIs
        /// </summary>
        /// <param name="app"></param>
        /// <param name="patientOpen"></param>
        /// <param name="isModified"></param>
        /// <param name="autoSave"></param>
        /// <param name="log"></param>
        public static void CloseApplication(Application app, bool patientOpen, bool isModified, bool autoSave, Logger log)
        {
            //be sure to close the patient before closing the application. Not doing so will result in unclosed timestamps in eclipse
            if (isModified)
            {
                if (autoSave)
                {
                    //Save the results without asking the user
                    app.SaveModifications();
                    log.AppendLogOutput("Modifications saved to database!");
                    log.ChangesSaved = true;
                }
                else
                {
                    //ask the user if they want to save their changes
                    SaveChangesPrompt SCP = new SaveChangesPrompt();
                    SCP.ShowDialog();
                    if (SCP.GetSelection())
                    {
                        app.SaveModifications();
                        log.AppendLogOutput("Modifications saved to database!");
                        log.ChangesSaved = true;
                    }
                    else
                    {
                        log.AppendLogOutput("Modifications NOT saved to database!");
                        log.ChangesSaved = false;
                    }
                }
            }
            else
            {
                //no modifications made to database, don't bother saving
                log.AppendLogOutput("No modifications made to database objects!");
                log.ChangesSaved = false;
            }
            log.User = $"{app.CurrentUser.Name} ({app.CurrentUser.Id})";
            if (patientOpen)
            {
                //if a patient was open, close the patient and dump the log file
                app.ClosePatient();
                if (log.Dump())
                {
                    MessageBox.Show("Error! Could not save log file!");
                }
            }
            app.Dispose();
        }
    }
}
