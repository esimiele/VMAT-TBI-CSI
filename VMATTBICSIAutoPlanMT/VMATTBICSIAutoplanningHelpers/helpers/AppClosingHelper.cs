﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.Logging;
using VMATTBICSIAutoPlanningHelpers.Prompts;
using System.Windows;
using Application = VMS.TPS.Common.Model.API.Application;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class AppClosingHelper
    {
        public static void CloseApplication(Application app, bool patientOpen, bool isModified, bool autoSave, Logger log)
        {
            //be sure to close the patient before closing the application. Not doing so will result in unclosed timestamps in eclipse
            if (isModified)
            {
                if (autoSave)
                {
                    app.SaveModifications();
                    log.AppendLogOutput("Modifications saved to database!");
                    log.ChangesSaved = true;
                }
                else
                {
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
                log.AppendLogOutput("No modifications made to database objects!");
                log.ChangesSaved = false;
            }
            log.User = $"{app.CurrentUser.Name} ({app.CurrentUser.Id})";
            if (app != null)
            {
                if (patientOpen)
                {
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
}