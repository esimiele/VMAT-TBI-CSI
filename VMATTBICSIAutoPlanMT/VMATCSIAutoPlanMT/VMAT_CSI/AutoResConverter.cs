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
        #region Get methods
        public string GetErrorStackTrace() { return stackTraceError; }
        public Application GetAriaApplicationInstance() { return app; }
        public bool GetIsPatientOpenStatus() { return isPatientOpen; }
        public bool GetAriaIsModifiedStatus() { return isModified; }
        #endregion

        //data memebers
        private string mrn;
        private bool isPatientOpen = false;
        private bool isModified = false;
        private string SSID;
        private StructureSet selectedSS;
        private ProvideUIUpdateDelegate PUUD;
        private string stackTraceError;
        private Application app = null;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="PID"></param>
        /// <param name="id"></param>
        public AutoResConverter(string PID, string id)
        {
            mrn = PID;
            SSID = id;
            SetCloseOnFinish(true, 1000);
        }

        #region Run control
        public override bool Run()
        {
            try
            {
                PUUD = ProvideUIUpdate;
                if (GenerateAriaInstance()) return true;
                if (PreliminaryChecks()) return true;
                if (ConvertHighToDefaultResolution()) return true;
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
        #endregion

        #region Generate Aria instance, prelim checks, and convert
        /// <summary>
        /// Helper method to create an aria instance, open the patient, and grab the structure set
        /// </summary>
        /// <returns></returns>
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
                    isPatientOpen = true;
                    ProvideUIUpdate($"Patient: {mrn} open successfully");
                    if (!string.IsNullOrEmpty(SSID))
                    {
                        ProvideUIUpdate($"Structure set UID: {SSID}");
                        if (pi.StructureSets.Any(x => string.Equals(SSID, x.Id)))
                        {
                            ProvideUIUpdate($"Structure set exists for patient!");
                            selectedSS = pi.StructureSets.First(x => string.Equals(SSID, x.Id));
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
                stackTraceError = e.StackTrace;
            }
            ProvideUIUpdate(100, $"Elapsed time: {GetElapsedTime()}");
            return fail;
        }

        /// <summary>
        /// Preliminary checks to run prior to downsampling the structures (basically make sure they exist and are not empty)
        /// </summary>
        /// <returns></returns>
        private bool PreliminaryChecks()
        {
            UpdateUILabel("Performing Preliminary Checks: ");
            //verify brain and spine structures are present
            if (!selectedSS.Structures.Any(x => x.Id.ToLower().Contains("brain") && !x.Id.ToLower().Contains("brainstem") && !x.IsEmpty))
            {
                ProvideUIUpdate("Missing brain structure! Please add and try again!", true);
                return true;
            }
            if (!selectedSS.Structures.Any(x => x.Id.ToLower().Contains("spinalcord") && !x.IsEmpty))
            {
                ProvideUIUpdate("Missing spinal cord structure! Please add and try again!", true);
                return true;
            }
            ProvideUIUpdate("Brain and spinal cord structures exist");

            ProvideUIUpdate(100, "Preliminary checks complete!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        /// <summary>
        /// Downsample the brain and spinal cord structures
        /// </summary>
        /// <returns></returns>
        private bool ConvertHighToDefaultResolution()
        {
            UpdateUILabel("Converting to default res:");
            ProvideUIUpdate(0, $"Converting any critical high res structures to default resolution");
            List<Structure> theStructures = new List<Structure> { };
            //already checked in preliminary checks if brain and spinal cord exist.
            ProvideUIUpdate($"Adding brain structure to list");
            theStructures.Add(selectedSS.Structures.First(x => x.Id.ToLower().Contains("brain") && !x.Id.ToLower().Contains("brainstem") && !x.IsEmpty));
            ProvideUIUpdate($"Adding spinal cord structure to list");
            theStructures.Add(selectedSS.Structures.First(x => x.Id.ToLower().Contains("spinalcord") && !x.IsEmpty));
            if (ContourHelper.CheckHighResolutionAndConvert(theStructures, selectedSS, PUUD)) return true;
            isModified = true;
            ProvideUIUpdate(100, "Checked and converted any high res base targets");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }
        #endregion
    }
}
