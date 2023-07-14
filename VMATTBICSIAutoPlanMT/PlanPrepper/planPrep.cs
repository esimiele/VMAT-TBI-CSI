using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace planPrepper
{
    class planPrep
    {
        //common variables
        ExternalPlanSetup thePlan = null;
        int numIsos = 0;
        //empty vectors to hold the isocenter position of one beam from each isocenter and the names of each isocenter
        public List<Tuple<double,double,double>> isoPositions = new List<Tuple<double, double, double>> { };
        List<string> names = new List<string> { };
        List<List<Beam>> beamsPerIso = new List<List<Beam>> { };
        List<ExternalPlanSetup> separatedPlans = new List<ExternalPlanSetup> { };
        string planType = "";
        public bool recalcNeeded = false;

        public planPrep(ExternalPlanSetup p, string type)
        {
            //copy arguments into local variables
            thePlan = p;
            planType = type;
        }

        public bool getShiftNote()
        {
            //loop through each beam in the vmat plan, grab the isocenter position of the beam. Compare the z position of each isocenter to the list of z positions in the vector. 
            //If no match is found, this is a new isocenter. Add it to the stack. If it is not unique, this beam belongs to an existing isocenter group --> ignore it
            //also grab instances of each beam in each isocenter and save them (used for separating the plans later)
            List<Beam> beams = new List<Beam> { };
            foreach (Beam b in thePlan.Beams.Where(x => !x.IsSetupField).OrderBy(o => int.Parse(o.Id.First().ToString())))
            {
                //Ignore setup fields
                if (!b.IsSetupField)
                {
                    VVector v = b.IsocenterPosition;
                    v = thePlan.StructureSet.Image.DicomToUser(v, thePlan);
                    IEnumerable<Tuple<double,double,double>> d = isoPositions.Where(k => k.Item3 == v.z);
                    if (!d.Any())
                    {
                        isoPositions.Add(Tuple.Create(v.x, v.y, v.z));
                        numIsos++;
                        //do NOT add the first detected isocenter to the number of beams per isocenter list. Start with the second isocenter 
                        //(otherwise there will be no beams in the first isocenter, the beams in the first isocenter will be attached to the second isocenter, etc.)
                        if (numIsos > 1)
                        {
                            //NOTE: it is important to have 'new List<Beam>(beams)' as the argument rather than 'beams'. A list of a list is essentially a pointer to a list, so if you delete the sublists,
                            //the list of lists will have the correct number of elements, but they will all be empty
                            beamsPerIso.Add(new List<Beam>(beams));
                            beams.Clear();
                        }
                    }
                    //add the current beam to the sublist
                    beams.Add(b);
                }
            }
            //add the beams from the last isocenter to the beams per iso list
            beamsPerIso.Add(new List<Beam>(beams));
            beams.Clear();
            if (isoPositions.Count == 1) planType = "single";

            //add names to each isocenter. These can be hard-coded for TLI and CSI since they are fairly standard
            if (planType == "TLI")
            {
                names.Add("Mantle");
                names.Add("Spleen");
                names.Add("Pelvis");
            }
            else if (planType == "CSI")
            {
                names.Add("Brain");
                if (numIsos == 3) names.Add("UpSpine");
                names.Add("LoSpine");

            }
            else if (planType == "other")
            {
                //plan is NOT TLI or CSI, therefore, prompt the user to enter each isocenter name. Can accomodate a maximum of four isocenters
                if (isoPositions.Count() > 4)
                {
                    MessageBox.Show("Error! There's too many isocenters!");
                    return true;
                }
                inputNames input = new planPrepper.inputNames();
                if (isoPositions.Count() > 2)
                {
                    //only make the appropriate number of text boxes visibile (depending on the number of detected isocenters)
                    input.iso3.Visible = true;
                    input.isoName3.Visible = true;
                    if (isoPositions.Count() > 3)
                    {
                        input.iso4.Visible = true;
                        input.isoName4.Visible = true;
                    }
                }
                input.ShowDialog();
                if (!input.confirm) return true;

                //add the entered isocenter names to the name vector
                names.Add(input.isoName1.Text);
                names.Add(input.isoName2.Text);
                if (isoPositions.Count() > 2) names.Add(input.isoName3.Text);
                if (isoPositions.Count() > 3) names.Add(input.isoName4.Text);
                //check to see that entered names are NOT blank
                int index = 1;
                foreach (string s in names)
                {
                    if (s == "" || s.Substring(s.Length - 1, 1) == " " || s.Substring(0, 1) == " " || s.Contains("\\") || s.Count() + 2 > 13)
                    {
                        MessageBox.Show(String.Format("Error! Isocenter {0} is empty, has trailing or leading spaces, contains \\, or is > 13 characters!", index));
                        return true;
                    }
                    index++;
                }
            }
            else if (planType == "single")
            {
                if(thePlan.Id.ToLower().Contains("ccw")) names.Add(thePlan.Id.Substring(4, thePlan.Id.Length - 5));
                else names.Add(thePlan.Id.Substring(3, thePlan.Id.Length - 4));
            }

            //get the user origin in user coordinates
            VVector uOrigin = thePlan.StructureSet.Image.UserOrigin;
            uOrigin = thePlan.StructureSet.Image.DicomToUser(uOrigin, thePlan);
            //vector to hold the isocenter name, the x,y,z shifts from CT ref, and the shifts between each adjacent iso for each axis (LR, AntPost, SupInf)
            List<Tuple<string, Tuple<double,double,double>, Tuple<double, double, double>>> shifts = new List<Tuple<string, Tuple<double, double, double>, Tuple<double, double, double>>> { };
            double SupInfShifts = 0.0;
            double AntPostShifts = 0.0;
            double LRShifts = 0.0;
            int count = 0;
            foreach (Tuple<double,double,double> pos in isoPositions)
            {
                //each isocenter position inherently represents the shift from CT ref in User coordinates
                Tuple<double,double,double> CTrefShifts = Tuple.Create(pos.Item1 / 10, pos.Item2 / 10, pos.Item3 / 10);
                //calculate the relative shifts between isocenters (the first isocenter is the CTrefShift)
                if (count == 0)
                {
                    SupInfShifts = (isoPositions.ElementAt(count).Item3 / 10);
                    AntPostShifts = (isoPositions.ElementAt(count).Item2 / 10);
                    LRShifts = (isoPositions.ElementAt(count).Item1 / 10);
                }
                else
                {
                    SupInfShifts = (isoPositions.ElementAt(count).Item3 - isoPositions.ElementAt(count - 1).Item3) / 10;
                    AntPostShifts = (isoPositions.ElementAt(count).Item2 - isoPositions.ElementAt(count - 1).Item2) / 10;
                    LRShifts = (isoPositions.ElementAt(count).Item1 - isoPositions.ElementAt(count - 1).Item1) / 10;
                }
                //add the iso name, CT ref shifts, and relative iso shifts to the vector
                shifts.Add(Tuple.Create(names.ElementAt(count), CTrefShifts, Tuple.Create(LRShifts, AntPostShifts, SupInfShifts)));
                count++;
            }

            //convert the user origin back to dicom coordinates (needed for tabletop calculation)
            uOrigin = thePlan.StructureSet.Image.UserToDicom(uOrigin, thePlan);

            //grab the couch surface
            Structure couchSurface = thePlan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToLower() == "couchsurface");

            double TT = 0.0;
            //check if couch is present. Warn if not found, otherwise it is the separation between the user origin and the minimum y-position of the couch surface (in dicom coordinates)
            if (couchSurface == null) MessageBox.Show("Warning! No couch surface structure found!");
            //couch shift should be negative for normal patients
            else TT = (thePlan.Beams.FirstOrDefault(x => !x.IsSetupField).IsocenterPosition.y - couchSurface.MeshGeometry.Positions.Min(p => p.Y)) / 10;

            //create the message
            string message = "";
            //what is the plan type
            if (planType == "TLI") message += "TLI iso shift\r\n";
            else if (planType == "CSI") message += "CSI iso shift\r\n";
            else message += String.Format("{0} iso shift\r\n", thePlan.Id);
            //add message to journal note to let the people know the patient orientation
            if (thePlan.TreatmentOrientation == PatientOrientation.HeadFirstSupine) message += "Patient orientation: Head First SUPINE\r\n";
            else if (thePlan.TreatmentOrientation == PatientOrientation.HeadFirstProne) message += "Patient orientation: Head First PRONE\r\n";
            else message += "WARNING! THE PATIENT ORIENTATION IS NOT HFS OR HFP!";
            //insert table top (TT) measurement in the journal note. Also let the user know if they forgot to insert a couch structure.
            if (TT != 0.0) message += String.Format("TT = {0:0.0} cm for all plans\r\n", TT);
            else message += String.Format("NO COUCH STRUCTURE PRESENT IN PLAN!\r\n", TT);
            message += "**Dosimetric Shift**\r\n\r\n";

            double multiplier = 1.0;
            //used to account for the situation where the patient is head first prone (x and y directions are flipped from head first supine)
            if (thePlan.TreatmentOrientation == PatientOrientation.HeadFirstProne) multiplier = -1.0;

            for (int i = 0; i < numIsos; i++)
            {
                if (i == 0)
                {
                    //only report the CT ref shifts for the first isocenter
                    message += String.Format("{0} iso shift from CT REF:", shifts.ElementAt(i).Item1) + System.Environment.NewLine;
                    //LR shifts
                    if (Math.Abs(shifts.ElementAt(i).Item3.Item1) > 0.001) message += String.Format("X = {0:0.0} cm {1}", Math.Abs(shifts.ElementAt(i).Item3.Item1), (shifts.ElementAt(i).Item3.Item1 * multiplier) > 0 ? "LEFT" : "RIGHT") + System.Environment.NewLine;
                    if (Math.Abs(shifts.ElementAt(i).Item3.Item2) > 0.001) message += String.Format("Y = {0:0.0} cm {1}", Math.Abs(shifts.ElementAt(i).Item3.Item2), (shifts.ElementAt(i).Item3.Item2 * multiplier) > 0 ? "POST" : "ANT") + System.Environment.NewLine;
                    message += String.Format("Z = {0:0.0} cm {1}", Math.Abs(shifts.ElementAt(i).Item3.Item3), shifts.ElementAt(i).Item3.Item3 > 0 ? "SUP" : "INF") + System.Environment.NewLine + System.Environment.NewLine;
                }
                else
                {
                    //report relative shifts between each isocenter and the absolute CT ref shift for each isocenter
                    message += String.Format("{0} shift from **{1} ISO**\r\n", shifts.ElementAt(i).Item1, shifts.ElementAt(i - 1).Item1);
                    if (Math.Abs(shifts.ElementAt(i).Item3.Item1) > 0.001)
                        message += String.Format("X = {0:0.0} cm {1} ({2:0.0} cm {3} from CT ref)\r\n", Math.Abs(shifts.ElementAt(i).Item3.Item1), (shifts.ElementAt(i).Item3.Item1 * multiplier) > 0 ? "LEFT" : "RIGHT", Math.Abs(shifts.ElementAt(i).Item2.Item1), (shifts.ElementAt(i).Item2.Item1 * multiplier) > 0 ? "LEFT" : "RIGHT");
                    if (Math.Abs(shifts.ElementAt(i).Item3.Item2) > 0.001)
                        message += String.Format("Y = {0:0.0} cm {1} ({2:0.0} cm {3} from CT ref)\r\n", Math.Abs(shifts.ElementAt(i).Item3.Item2), (shifts.ElementAt(i).Item3.Item2 * multiplier) > 0 ? "POST" : "ANT", Math.Abs(shifts.ElementAt(i).Item2.Item2), (shifts.ElementAt(i).Item2.Item2 * multiplier) > 0 ? "POST" : "ANT");
                    message += String.Format("Z = {0:0.0} cm {1} ({2:0.0} cm {3} from CT ref)\r\n\r\n", Math.Abs(shifts.ElementAt(i).Item3.Item3), shifts.ElementAt(i).Item3.Item3 > 0 ? "SUP" : "INF", Math.Abs(shifts.ElementAt(i).Item2.Item3), shifts.ElementAt(i).Item2.Item3 > 0 ? "SUP" : "INF");
                }
            }

            //copy to clipboard and inform the user it's done
            Clipboard.SetText(message);
            MessageBox.Show("Shifts have been copied to the clipboard! \r\nPaste them into the journal note!");
            return false;
        }

        public bool separate()
        {
            //determine if there are any setup fields in the current plan. If not, ask the user if they want to stop or continue separating the plan
            if (!thePlan.Beams.Where(x => x.IsSetupField).Any())
            {
                confirmUI CUI = new planPrepper.confirmUI();
                CUI.message.Text = "I didn't find any setup fields in the plan!" + Environment.NewLine + Environment.NewLine + "Are you sure you want to continue?!";
                CUI.ShowDialog();
                if (!CUI.confirm) return true;
            }

            //if(thePlan.GetCalculationModel(CalculationType.PhotonVolumeDose).ToLower().Contains("acuros")) recalcNeeded = true;
            if (thePlan.GetCalculationOptions(thePlan.GetCalculationModel(CalculationType.PhotonVolumeDose)).FirstOrDefault(x => x.Key == "PlanDoseCalculation").Value == "ON") recalcNeeded = true;

            //counter for indexing names
            int count = 0;
            //loop through the list of beams in each isocenter
            foreach (List<Beam> beams in beamsPerIso)
            {
                //string message = "";
                //foreach (Beam b in beams) message += b.Id + "\n";
                //MessageBox.Show(message);

                //copy the plan, set the plan id based on the counter, and make a empty list to hold the beams that need to be removed
                ExternalPlanSetup newplan = (ExternalPlanSetup)thePlan.Course.CopyPlanSetup(thePlan);
                newplan.Id = String.Format("{0} {1}", count + 1, names.ElementAt(count));
                List<Beam> removeMe = new List<Beam> { };
                //can't add reference point to plan because it must be open in Eclipse for ESAPI to perform this function. Need to fix in v16
                //newplan.AddReferencePoint(newplan.StructureSet.Structures.First(x => x.Id.ToLower() == "ptv_body"), null, newplan.Id, newplan.Id);
                //add the current plan copy to the separatedPlans list
                separatedPlans.Add(newplan);
                //loop through each beam in the plan copy and compare it to the list of beams in the current isocenter
                foreach (Beam b in newplan.Beams)
                {
                    //if the current beam in newPlan is NOT found in the beams list, add it to the removeMe list. This logic has to be applied. You can't directly remove the beams in this loop as ESAPI will
                    //complain that the enumerable that it is using to index the loop changes on each iteration (i.e., newplan.Beams changes with each iteration). Do NOT add setup beams to the removeMe list. The
                    //idea is to have dosi add one set of setup fields to the original plan and then not remove those for each created plan. Unfortunately, dosi will have to manually adjust the iso position for
                    //the setup fields in each new plan (no way to adjust the existing isocenter of a setup beam, it has to be re-added)
                    if (!beams.Where(x => x.Id == b.Id).Any() && !b.IsSetupField) removeMe.Add(b);
                    if (b.IsSetupField)
                    {
                        //if the current beam is a setup field, verify that the beam Id DOES NOT exceed 16 characters. If so, ask the user to change the beam Id to a shorter value. Also, automatically remove
                        //the space betweem R (or L) and LAT in ISO R LAT (or ISO L LAT). Easy way to save space
                        if (b.Id.ToLower() == "iso r lat") b.Id = "ISO RLAT";
                        else if (b.Id.ToLower() == "iso l lat") b.Id = "ISO LLAT";
                        string name = b.Id + String.Format(" {0}", names.ElementAt(count));
                        while (name.Length > 16)
                        {
                            //changeName c = new planPrepper.changeName();
                            //c.currentId.Text = b.Id + String.Format(" {0}", names.ElementAt(count));
                            //c.ShowDialog();
                            //if (!c.confirm) return true;
                            //name = c.textBox1.Text;
                            name = name.Substring(0, 16);
                        }
                        b.Id = name;
                    }
                }
                //now remove the beams for the current plan copy (no canremovebeam() function, so need to use try-catch statement)
                foreach (Beam b in removeMe) try { newplan.RemoveBeam(b); } catch { MessageBox.Show(String.Format("Unable to remove beam: {0}!\nExiting!\nTry removing the attached DRR (if applicable) and re-running the script.", b.Id)); return true; }
                count++;
            }
            //inform the user it's done
            string message = "Original plan has been separated! \r\nBe sure to set the target volume and primary reference point!\r\n";
            if (thePlan.Beams.Where(x => x.IsSetupField).Any()) message += "Also reset the isocenter position of the setup fields!";
            MessageBox.Show(message);
            return false;
        }

        public void calculateDose()
        {
            //loop through each plan in the separatedPlans list (generated in the separate method above) and calculate dose for each plan
            foreach (ExternalPlanSetup p in separatedPlans)
            {
                p.CalculateDose();
                p.PlanNormalizationValue = thePlan.PlanNormalizationValue;
            }
        }
    }
}
