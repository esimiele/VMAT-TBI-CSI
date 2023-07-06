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

        public override bool GetShiftNote()
        {
            //loop through each beam in the vmat plan, grab the isocenter position of the beam. Compare the z position of each isocenter to the list of z positions in the vector. 
            //If no match is found, this is a new isocenter. Add it to the stack. If it is not unique, this beam belongs to an existing isocenter group --> ignore it
            //also grab instances of each beam in each isocenter and save them (used for separating the plans later)
            List<ExternalPlanSetup> plans = new List<ExternalPlanSetup> { };
            plans.Add(vmatPlan);
            if (CheckBeamNameFormatting(plans)) return true;

            Tuple<List<List<Beam>>, int> result = ExtractNumIsoAndBeams(vmatPlan, numVMATIsos);
            vmatBeamsPerIso = new List<List<Beam>>(result.Item1);
            numVMATIsos = result.Item2;

            //copy number of vmat isocenters determined above onto the total number of isos
            numIsos = numVMATIsos;
            //if the ap/pa plan is NOT null, then get the isocenter position(s) of those beams as well. Do the same thing as above

            //get the isocenter names using the isoNameHelper class
            names = new List<string>(IsoNameHelper.GetCSIIsoNames(numVMATIsos));

            //get the user origin in user coordinates
            VVector uOrigin = vmatPlan.StructureSet.Image.UserOrigin;
            uOrigin = vmatPlan.StructureSet.Image.DicomToUser(uOrigin, vmatPlan);
            //vector to hold the isocenter name, the x,y,z shifts from CT ref, and the shifts between each adjacent iso for each axis (LR, AntPost, SupInf)
            List<Tuple<string, Tuple<double, double, double>, Tuple<double, double, double>>> shifts = new List<Tuple<string, Tuple<double, double, double>, Tuple<double, double, double>>>(ExtractIsoPositions());

            //convert the user origin back to dicom coordinates
            uOrigin = vmatPlan.StructureSet.Image.UserToDicom(uOrigin, vmatPlan);

            //grab the couch surface
            Structure couchSurface = vmatPlan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToLower() == "couchsurface");
            double TT = 0;
            //check if couch is present. Warn if not found, otherwise it is the separation between the the beam isocenter position and the minimum y-position of the couch surface (in dicom coordinates)
            if (couchSurface == null) MessageBox.Show("Warning! No couch surface structure found!");
            else TT = (vmatPlan.Beams.First(x => !x.IsSetupField).IsocenterPosition.y - couchSurface.MeshGeometry.Positions.Min(p => p.Y)) / 10;

            //create the message
            string message = "";
            if (couchSurface != null) message += "***Bars out***\r\n";
            else message += "No couch surface structure found in plan!\r\n";
            //check if AP/PA plans are in FFS orientation
            
            if (numIsos > numVMATIsos) message += "VMAT TBI setup per procedure. Please ensure the matchline on Spinning Manny and the bag matches\r\n";
            else message += "VMAT TBI setup per procedure. No Spinning Manny.\r\r\n";
            message += String.Format("TT = {0:0.0} cm for all plans\r\n", TT);
            message += "Dosimetric shifts SUP to INF:\r\n";

            //write the first set of shifts from CT ref before the loop. 12-23-2020 support added for the case where the lat/vert shifts are non-zero
            if (Math.Abs(shifts.ElementAt(0).Item3.Item1) >= 0.1 || Math.Abs(shifts.ElementAt(0).Item3.Item2) >= 0.1)
            {
                message += String.Format("{0} iso shift from CT REF:", shifts.ElementAt(0).Item1) + System.Environment.NewLine;
                if (Math.Abs(shifts.ElementAt(0).Item3.Item1) >= 0.1) message += String.Format("X = {0:0.0} cm {1}", Math.Abs(shifts.ElementAt(0).Item3.Item1), (shifts.ElementAt(0).Item3.Item1) > 0 ? "LEFT" : "RIGHT") + System.Environment.NewLine;
                if (Math.Abs(shifts.ElementAt(0).Item3.Item2) >= 0.1) message += String.Format("Y = {0:0.0} cm {1}", Math.Abs(shifts.ElementAt(0).Item3.Item2), (shifts.ElementAt(0).Item3.Item2) > 0 ? "POST" : "ANT") + System.Environment.NewLine;
                message += String.Format("Z = {0:0.0} cm {1}", shifts.ElementAt(0).Item3.Item3, Math.Abs(shifts.ElementAt(0).Item3.Item3) > 0 ? "SUP" : "INF") + System.Environment.NewLine;
            }
            else message += String.Format("{0} iso shift from CT ref = {1:0.0} cm {2} ({3:0.0} cm {4} from CT ref)\r\n", shifts.ElementAt(0).Item1, Math.Abs(shifts.ElementAt(0).Item3.Item3), shifts.ElementAt(0).Item3.Item3 > 0 ? "SUP" : "INF", Math.Abs(shifts.ElementAt(0).Item2.Item3), shifts.ElementAt(0).Item2.Item3 > 0 ? "SUP" : "INF");

            for (int i = 1; i < numIsos; i++)
            {
                if (i == numVMATIsos)
                {
                    //if numVMATisos == numIsos this message won't be displayed. Otherwise, we have exhausted the vmat isos and need to add these lines to the shift note
                    message += "Rotate Spinning Manny, shift to opposite Couch Lat\r\n";
                    message += "Upper Leg iso - same Couch Lng as Pelvis iso\r\n";
                    //let the therapists know that they need to shift couch lateral to the opposite side if the initial lat shift was non-zero
                    if (Math.Abs(shifts.ElementAt(0).Item3.Item1) >= 0.1) message += "Shift couch lateral to opposite side!\r\n";
                }
                //shift messages when the current isocenter is NOT the number of vmat isocenters (i.e., the first ap/pa isocenter). First case is for the vmat isocenters, the second case is when the isocenters are ap/pa (but not the first ap/pa isocenter)
                else if (i < numVMATIsos) message += String.Format("{0} iso shift from {1} iso = {2:0.0} cm {3} ({4:0.0} cm {5} from CT ref)\r\n", shifts.ElementAt(i).Item1, shifts.ElementAt(i - 1).Item1, Math.Abs(shifts.ElementAt(i).Item3.Item3), shifts.ElementAt(i).Item3.Item3 > 0 ? "SUP" : "INF", Math.Abs(shifts.ElementAt(i).Item2.Item3), shifts.ElementAt(i).Item2.Item3 > 0 ? "SUP" : "INF");
                else message += String.Format("{0} iso shift from {1} iso = {2:0.0} cm {3} ({4:0.0} cm {5} from CT ref)\r\n", shifts.ElementAt(i).Item1, shifts.ElementAt(i - 1).Item1, Math.Abs(shifts.ElementAt(i).Item3.Item3), shifts.ElementAt(i).Item3.Item3 > 0 ? "INF" : "SUP", Math.Abs(shifts.ElementAt(i).Item2.Item3), shifts.ElementAt(i).Item2.Item3 > 0 ? "INF" : "SUP");
            }

            //copy to clipboard and inform the user it's done
            Clipboard.SetText(message);
            MessageBox.Show("Shifts have been copied to the clipboard! \r\nPaste them into the journal note!");
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
