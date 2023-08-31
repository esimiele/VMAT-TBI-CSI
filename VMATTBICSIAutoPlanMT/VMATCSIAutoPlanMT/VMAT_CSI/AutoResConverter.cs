using System;
using System.Collections.Generic;
using System.Linq;
using SimpleProgressWindow;
using VMATTBICSIAutoPlanningHelpers.Delegates;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMS.TPS.Common.Model.API;

namespace VMATCSIAutoPlanMT.VMAT_CSI
{
    public class AutoResConverter : SimpleMTbase
    {
        public string GetErrorStackTrace() { return stackTraceError; }

        private string mrn;
        private string SSUID;
        private StructureSet selectedSS;
        private ProvideUIUpdateDelegate PUUD;
        private string stackTraceError;
        Application app = null;

        public AutoResConverter(string PID, string id)
        {
            mrn = PID;
            SSUID = id;
            SetCloseOnFinish(true, 1000);
        }

        public override bool Run()
        {
            try
            {
                PUUD = ProvideUIUpdate;
                if (GenerateAriaInstance()) return true;
                if (PreliminaryChecks()) return true;
                if (ConvertHighToDefaultResolution()) return true;
                if (CleanUp()) return true;
                UpdateUILabel("Finished!");
                ProvideUIUpdate(100, "Finished converting critical high res structures to default res!");
                ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
                return false;
            }
            catch (Exception e)
            {
                ProvideUIUpdate($"{e.Message}", true);
                stackTraceError = e.StackTrace;
                return true;
            }
        }

        private bool GenerateAriaInstance()
        {
            UpdateUILabel("Generating Aria instance:");
            bool fail = true;
            try
            {
                app = Application.CreateApplication();
                ProvideUIUpdate("Aria instance generated successfully");
                Patient pi = app.OpenPatientById(mrn);
                if (pi != null)
                {
                    ProvideUIUpdate($"Patient: {mrn} open successfully");
                    if (!string.IsNullOrEmpty(SSUID))
                    {
                        ProvideUIUpdate($"Structure set UID: {SSUID}");
                        if (pi.StructureSets.Any(x => string.Equals(SSUID, x.UID)))
                        {
                            ProvideUIUpdate($"Structure set exists for patient!");
                            selectedSS = pi.StructureSets.First(x => string.Equals(SSUID, x.UID));
                            ProvideUIUpdate($"Structure set Id: {selectedSS.Id}");
                            pi.BeginModifications();
                            fail = false;
                        }
                        else ProvideUIUpdate("Structure set not found in Aria!", true);
                    }
                }
                else
                {
                    ProvideUIUpdate($"Error! Could not open patient {mrn}!", true);
                }
            }
            catch (Exception e)
            {
                ProvideUIUpdate($"Error! Unable to connect to aria DB to check if structure set was successfully imported! Check manually!", true);
                ProvideUIUpdate(e.Message);
                ProvideUIUpdate(e.StackTrace);
            }
            ProvideUIUpdate(100, $"Elapsed time: {GetElapsedTime()}");
            return fail;
        }

        private bool PreliminaryChecks()
        {
            UpdateUILabel("Performing Preliminary Checks: ");
            //verify brain and spine structures are present
            if (!StructureTuningHelper.DoesStructureExistInSS("brain", selectedSS, true) || !StructureTuningHelper.DoesStructureExistInSS(new List<string> { "spinal_cord", "spinalcord" }, selectedSS, true))
            {
                ProvideUIUpdate("Missing brain and/or spine structures! Please add and try again!", true);
                return true;
            }
            ProvideUIUpdate("Brain and spinal cord structures exist");

            ProvideUIUpdate(100, "Preliminary checks complete!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        private bool ConvertHighToDefaultResolution()
        {
            UpdateUILabel("Converting to default res:");
            ProvideUIUpdate(0, $"Converting any critical high res structures to default resolution");
            if (ContourHelper.CheckHighResolutionAndConvert(new List<string> { "brain", "spinal_cord", "spinalcord" }, selectedSS, PUUD)) return true;
            ProvideUIUpdate(100, "Checked and converted any high res base targets");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        private bool CleanUp()
        {
            UpdateUILabel("Cleaning up:");
            try
            {
                app.SaveModifications();
                ProvideUIUpdate("Modification saved!");
                app.ClosePatient();
                ProvideUIUpdate($"Patient: {mrn} closed successfully!");
                app.Dispose();
                return false;
            }
            catch (Exception e)
            {
                ProvideUIUpdate($"Error! Unable to clean up because: {e.Message}", true);
                ProvideUIUpdate($"Modifications may or may not be saved to the data based! Check manually!");
                return true;
            }
        }
    }
}
