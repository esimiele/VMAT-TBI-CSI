using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATAutoPlanMT.baseClasses;
using VMATAutoPlanMT.Prompts;

namespace VMATAutoPlanMT
{
    class planPrep_TBI : planPrepBase
    {
        //common variables
        IEnumerable<ExternalPlanSetup> appaPlan;
        //empty vectors to hold the isocenter position of one beam from each isocenter and the names of each isocenter
        List<List<Beam>> appaBeamsPerIso = new List<List<Beam>> { };
        bool legsSeparated = false;

        public planPrep_TBI(ExternalPlanSetup vmat, IEnumerable<ExternalPlanSetup> appa)
        {
            //copy arguments into local variables
            vmatPlan = vmat;
            appaPlan = new List<ExternalPlanSetup>(appa);
            //if there is more than one AP/PA legs plan in the list, this indicates that the user already separated these plans. Don't separate them in this script
            if (appa.Count() > 1) legsSeparated = true;
        }

        public override bool getShiftNote()
        {
            //loop through each beam in the vmat plan, grab the isocenter position of the beam. Compare the z position of each isocenter to the list of z positions in the vector. 
            //If no match is found, this is a new isocenter. Add it to the stack. If it is not unique, this beam belongs to an existing isocenter group --> ignore it
            //also grab instances of each beam in each isocenter and save them (used for separating the plans later)
            List<ExternalPlanSetup> plans = new List<ExternalPlanSetup> (appaPlan);
            plans.Add(vmatPlan);
            if (checkBeamNameFormatting(plans)) return true;

            Tuple<List<List<Beam>>, int> result = extractNumIsoAndBeams(vmatPlan, numVMATIsos);
            vmatBeamsPerIso = new List<List<Beam>>(result.Item1);
            numVMATIsos = result.Item2;

            //copy number of vmat isocenters determined above onto the total number of isos
            numIsos = numVMATIsos;
            //if the ap/pa plan is NOT null, then get the isocenter position(s) of those beams as well. Do the same thing as above
            foreach (ExternalPlanSetup p in appaPlan)
            {
                result = extractNumIsoAndBeams(p, numIsos);
                List<List<Beam>> tmp = new List<List<Beam>>(result.Item1);
                foreach (List<Beam> itr in tmp) appaBeamsPerIso.Add(new List<Beam>(itr));
                numIsos += result.Item2;
            }

            //get the isocenter names using the isoNameHelper class
            names = new List<string>(new isoNameHelper().getIsoNames(numVMATIsos, numIsos));

            //get the user origin in user coordinates
            VVector uOrigin = vmatPlan.StructureSet.Image.UserOrigin;
            uOrigin = vmatPlan.StructureSet.Image.DicomToUser(uOrigin, vmatPlan);
            //vector to hold the isocenter name, the x,y,z shifts from CT ref, and the shifts between each adjacent iso for each axis (LR, AntPost, SupInf)
            List<Tuple<string, Tuple<double, double, double>, Tuple<double, double, double>>> shifts = new List<Tuple<string, Tuple<double, double, double>, Tuple<double, double, double>>>(extractIsoPositions());

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
            if (appaPlan.Any() && appaPlan.Where(x => x.TreatmentOrientation != PatientOrientation.FeetFirstSupine).Any())
            {
                message += "The following AP/PA plans are NOT in the FFS orientation:\r\n";
                foreach (ExternalPlanSetup p in appaPlan) if (p.TreatmentOrientation != PatientOrientation.FeetFirstSupine) message += p.Id + "\r\n";
                message += "WARNING! THE COUCH SHIFTS FOR THESE PLANS WILL NOT BE ACCURATE!\r\n";
            }
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

        public bool separate()
        {
            //check for setup fields in the vmat and AP/PA plans
            if (!vmatPlan.Beams.Where(x => x.IsSetupField).Any() || (appaPlan.Count() > 0 && !legsSeparated && !appaPlan.First().Beams.Where(x => x.IsSetupField).Any()))
            {
                string problemPlan = "";
                if (!vmatPlan.Beams.Where(x => x.IsSetupField).Any()) problemPlan = "VMAT plan";
                else problemPlan = "AP/PA plan(s)";
                confirmUI CUI = new confirmUI();
                CUI.message.Text = String.Format("I didn't find any setup fields in the {0}.", problemPlan) + Environment.NewLine + Environment.NewLine + "Are you sure you want to continue?!";
                CUI.ShowDialog();
                if (!CUI.confirm) return true;
            }

            //check if flash was used in the plan. If so, ask the user if they want to remove these structures as part of cleanup
            if (checkForFlash())
            {
                confirmUI CUI = new confirmUI();
                CUI.message.Text = "I found some structures in the structure set for generating flash." + Environment.NewLine + Environment.NewLine + "Do you want me to remove them?!";
                CUI.cancelBTN.Text = "No";
                CUI.ShowDialog();
                if (CUI.confirm) if (removeFlashStr()) return true;
            }
            //counter for indexing names
            int count = 0;
            //loop through the list of beams in each isocenter
            count = separatePlans(vmatPlan, count);

            //do the same as above, but for the AP/PA legs plan
            if (!legsSeparated)
            {
                foreach (List<Beam> beams in appaBeamsPerIso)
                {
                    //string message = "";
                    //foreach (Beam b in beams) message += b.Id + "\n";
                    //MessageBox.Show(message);
                    ExternalPlanSetup newplan = (ExternalPlanSetup)appaPlan.First().Course.CopyPlanSetup(appaPlan.First());
                    List<Beam> removeMe = new List<Beam> { };
                    newplan.Id = String.Format("{0} {1}", count + 1, (names.ElementAt(count).Contains("upper") ? "Upper Legs" : "Lower Legs"));
                    //newplan.AddReferencePoint(newplan.StructureSet.Structures.First(x => x.Id.ToLower() == "ptv_body"), null, newplan.Id, newplan.Id);
                    separatedPlans.Add(newplan);
                    foreach (Beam b in newplan.Beams)
                    {
                        //if the current beam in newPlan is NOT found in the beams list, then remove it from the current new plan
                        if (!beams.Where(x => x.Id == b.Id).Any() && !b.IsSetupField) removeMe.Add(b);
                    }
                    foreach (Beam b in removeMe) newplan.RemoveBeam(b);
                    count++;
                }
            }
            //inform the user it's done
            string message = "Original plan(s) have been separated! \r\nBe sure to set the target volume and primary reference point!\r\n";
            if (vmatPlan.Beams.Where(x => x.IsSetupField).Any() || (appaPlan.Count() > 0 && !legsSeparated && appaPlan.First().Beams.Where(x => x.IsSetupField).Any()))
                message += "Also reset the isocenter position of the setup fields!";
            MessageBox.Show(message);
            return false;
        }

        private bool removeFlashStr()
        {
            List<ExternalPlanSetup> plans = new List<ExternalPlanSetup>();
            plans.Add(vmatPlan);
            foreach (ExternalPlanSetup p in appaPlan) plans.Add(p);
            if (removeFlash(plans)) return true;

            //from the generateTS class, the human_body structure was a copy of the body structure BEFORE flash was added. Therefore, if this structure still exists, we can just copy it back onto the body
            StructureSet ss = vmatPlan.StructureSet;
            Structure bodyCopy = ss.Structures.FirstOrDefault(x => x.Id.ToLower() == "human_body");
            if (bodyCopy != null && !bodyCopy.IsEmpty)
            {
                Structure body = ss.Structures.First(x => x.Id.ToLower() == "body");
                body.SegmentVolume = bodyCopy.Margin(0.0);
                if (ss.CanRemoveStructure(bodyCopy)) ss.RemoveStructure(bodyCopy);
            }
            else MessageBox.Show("WARNING 'HUMAN_BODY' STRUCTURE NOT FOUND! BE SURE TO RE-CONTOUR THE BODY STRUCTURE!");
            flashRemoved = true;

            return false;
        }
    }
}
