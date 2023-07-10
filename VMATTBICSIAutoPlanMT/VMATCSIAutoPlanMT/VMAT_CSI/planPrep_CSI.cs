using System.Collections.Generic;
using VMS.TPS.Common.Model.API;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Helpers;

namespace VMATCSIAutoPlanMT.VMAT_CSI
{
    class PlanPrep_CSI : PlanPrepBase
    {
        public PlanPrep_CSI(ExternalPlanSetup vmat)
        {
            VMATPlan = vmat;
        }

        #region Run Control
        public override bool Run()
        {
            UpdateUILabel("Running:");
            if (PreliminaryChecks()) return true;
            if (SeparatePlans()) return true;
            if (recalcNeeded && ReCalculateDose()) return true;
            UpdateUILabel("Finished!");
            ProvideUIUpdate(100, "Finished separating plans!");
            ProvideUIUpdate($"Run time: {GetElapsedTime()} (mm:ss)");
            return false;
        }
        #endregion

        #region Preliminary Checks
        private bool PreliminaryChecks()
        {
            UpdateUILabel("Preliminary Checks:");
            ProvideUIUpdate($"Checking {VMATPlan.Id} ({VMATPlan.UID}) is valid for preparation");
            if (CheckBeamNameFormatting(VMATPlan)) return true;
            if (CheckIfDoseRecalcNeeded(VMATPlan)) recalcNeeded = true;
            ProvideUIUpdate(100, "Preliminary checks complete");
            return false;
        }
        #endregion

        #region Separate the vmat plan
        public bool SeparatePlans()
        {
            List<List<Beam>> beamsPerIso = PlanPrepHelper.ExtractBeamsPerIso(VMATPlan);
            List<string> isoNames = IsoNameHelper.GetCSIIsoNames(beamsPerIso.Count);
            if (SeparateVMATPlan(VMATPlan, beamsPerIso, isoNames)) return true;
            return false;
        }
        #endregion
    }
}
