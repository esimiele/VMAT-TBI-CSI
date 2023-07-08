using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Prompts;
using VMATTBICSIAutoPlanningHelpers.Helpers;

namespace VMATCSIAutoPlanMT.VMAT_CSI
{
    class PlanPrep_CSI : PlanPrepBase
    {
        public PlanPrep_CSI(ExternalPlanSetup vmat)
        {
            //copy arguments into local variables
            vmatPlan = vmat;
        }

        public override bool Run()
        {
            if (PreliminaryChecks()) return true;
            if (SeparatePlans()) return true;
            return false;
        }

        private bool PreliminaryChecks()
        {
            if (CheckBeamNameFormatting(vmatPlan)) return true;
            return false;
        }

        public bool SeparatePlans()
        {
            //check for setup fields in the vmat and AP/PA plans
            if (!vmatPlan.Beams.Where(x => x.IsSetupField).Any())
            {
                string problemPlan = "";
                if (!vmatPlan.Beams.Where(x => x.IsSetupField).Any()) problemPlan = "VMAT plan";
                ConfirmPrompt CUI = new ConfirmPrompt(String.Format("I didn't find any setup fields in the {0}.", problemPlan) + Environment.NewLine + Environment.NewLine + "Are you sure you want to continue?!");
                CUI.ShowDialog();
                if (!CUI.GetSelection()) return true;
            }

            //counter for indexing names
            int count = 0;
            //loop through the list of beams in each isocenter
            count = SeparatePlan(vmatPlan, count);
            //inform the user it's done
            string message = "Original plan(s) have been separated! \r\nBe sure to set the target volume and primary reference point!\r\n";
            if (vmatPlan.Beams.Where(x => x.IsSetupField).Any())
                message += "Also reset the isocenter position of the setup fields!";
            MessageBox.Show(message);
            return false;
        }
    }
}
