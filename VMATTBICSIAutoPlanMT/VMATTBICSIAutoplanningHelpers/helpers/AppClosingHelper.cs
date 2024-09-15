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
        public static void CloseApplication(Application app, bool patientOpen, bool isModified, bool autoSave)
        {
            //be sure to close the patient before closing the application. Not doing so will result in unclosed timestamps in eclipse
            if (isModified)
            {
                if (autoSave)
                {
                    //Save the results without asking the user
                    app.SaveModifications();
                    Logger.GetInstance().AppendLogOutput("Modifications saved to database!");
                    Logger.GetInstance().ChangesSaved = true;
                }
                else
                {
                    //ask the user if they want to save their changes
                    SaveChangesPrompt SCP = new SaveChangesPrompt();
                    SCP.ShowDialog();
                    if (SCP.GetSelection())
                    {
                        app.SaveModifications();
                        Logger.GetInstance().AppendLogOutput("Modifications saved to database!");
                        Logger.GetInstance().ChangesSaved = true;
                    }
                    else
                    {
                        Logger.GetInstance().AppendLogOutput("Modifications NOT saved to database!");
                        Logger.GetInstance().ChangesSaved = false;
                    }
                }
            }
            else
            {
                //no modifications made to database, don't bother saving
                Logger.GetInstance().AppendLogOutput("No modifications made to database objects!");
                Logger.GetInstance().ChangesSaved = false;
            }
            Logger.GetInstance().User = $"{app.CurrentUser.Name} ({app.CurrentUser.Id})";
            if (patientOpen)
            {
                //if a patient was open, close the patient and dump the log file
                app.ClosePatient();
                if (Logger.GetInstance().Dump())
                {
                    MessageBox.Show("Error! Could not save log file!");
                }
            }
            app.Dispose();
        }
    }
}
