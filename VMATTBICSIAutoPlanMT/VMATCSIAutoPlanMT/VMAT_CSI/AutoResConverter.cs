using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public AutoResConverter(string PID, string id)
        {
            mrn = PID;
            SSUID = id;
        }

        public override bool Run()
        {
            try
            {
                PUUD = ProvideUIUpdate;
                if (GenerateAriaInstance()) return true;
                if (PreliminaryChecks()) return true;
                if (ConvertHighToDefaultResolution()) return true;
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
            bool fail = true;
            try
            {
                Application app = Application.CreateApplication();
                Patient pi = app.OpenPatientById(mrn);
                if (pi != null)
                {
                    if (!string.IsNullOrEmpty(SSUID))
                    {
                        if (pi.StructureSets.Any(x => string.Equals(SSUID, x.UID)))
                        {
                            selectedSS = pi.StructureSets.First(x => string.Equals(SSUID, x.UID));
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
            return fail;
        }

        private bool PreliminaryChecks()
        {
            UpdateUILabel("Performing Preliminary Checks: ");
            int calcItems = 1;
            int counter = 0;

            //verify brain and spine structures are present
            if (!StructureTuningHelper.DoesStructureExistInSS("brain", selectedSS, true) || !StructureTuningHelper.DoesStructureExistInSS(new List<string> { "spinal_cord", "spinalcord" }, selectedSS, true))
            {
                ProvideUIUpdate("Missing brain and/or spine structures! Please add and try again!", true);
                return true;
            }
            ProvideUIUpdate(100 * ++counter / calcItems, "Brain and spinal cord structures exist");

            ProvideUIUpdate(100, "Preliminary checks complete!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        private bool ConvertHighToDefaultResolution()
        {
            ProvideUIUpdate(0, $"Converting any critical high res structures to default resolution");
            if (ContourHelper.CheckHighResolutionAndConvert(new List<string> { "brain", "spinal_cord", "spinalcord" }, selectedSS, PUUD)) return true;
            ProvideUIUpdate(100, "Checked and converted any high res base targets");
            return false;
        }
    }
}
