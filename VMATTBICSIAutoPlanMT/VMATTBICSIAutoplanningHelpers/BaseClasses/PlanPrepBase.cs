using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Forms;
using VMATTBICSIAutoPlanningHelpers.Prompts;
using SimpleProgressWindow;

namespace VMATTBICSIAutoPlanningHelpers.BaseClasses
{
    public class PlanPrepBase
    {
        public ExternalPlanSetup vmatPlan = null;
        public int numVMATIsos = 0;
        public int numIsos;
        public List<Tuple<double, double, double>> isoPositions = new List<Tuple<double, double, double>> { };
        public List<string> names = new List<string> { };
        public List<List<Beam>> vmatBeamsPerIso = new List<List<Beam>> { };
        public List<ExternalPlanSetup> separatedPlans = new List<ExternalPlanSetup> { };

        public virtual bool GetShiftNote()
        {
            return false;
        }

        public bool CheckBeamNameFormatting(List<ExternalPlanSetup> plans)
        {
            string beamFormatMessage = "The following beams are not in the correct format:" + Environment.NewLine;
            bool beamFormatError = false;
            foreach (ExternalPlanSetup p in plans)
            {
                foreach (Beam b in p.Beams.Where(x => !x.IsSetupField))
                {
                    if (b.Id.Length < 2 || !int.TryParse(b.Id.Substring(0, 2).ToString(), out int dummy))
                    {
                        beamFormatMessage += b.Id + Environment.NewLine;
                        if (!beamFormatError) beamFormatError = true;
                    }
                }
            }
            if (beamFormatError)
            {
                beamFormatMessage += Environment.NewLine + "Make sure there is a space after the beam number! Please fix and try again!";
                MessageBox.Show(beamFormatMessage);
                return true;
            }
            return false;
        }

        public Tuple<List<List<Beam>>,int> ExtractNumIsoAndBeams(ExternalPlanSetup plan)
        {
            List<List<Beam>> beamsPerIso = new List<List<Beam>> { };
            List<Beam> beams = new List<Beam> { };
            foreach (Beam b in plan.Beams.Where(x => !x.IsSetupField).OrderByDescending(o => o.IsocenterPosition.z))
            {
                VVector v = b.IsocenterPosition;
                v = plan.StructureSet.Image.DicomToUser(v, plan);
                if (!isoPositions.Any(k => k.Item3 == v.z))
                {
                    isoPositions.Add(Tuple.Create(v.x, v.y, v.z));
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
            //add the beams from the last isocenter to the vmat beams per iso list
            beamsPerIso.Add(new List<Beam>(beams));
            return new Tuple<List<List<Beam>>,int>(beamsPerIso, isoPositions.Count());
        }

        public (List<Tuple<double, double, double>>, List<Tuple<double, double, double>>) ExtractIsoPositions()
        {
            List<Tuple<double, double, double>> shiftsFromBBs = new List<Tuple<double, double, double>> { };
            List<Tuple<double, double, double>> shiftsBetweenIsos = new List<Tuple<double, double, double>> { };
            
            double SupInfShifts;
            double AntPostShifts;
            double LRShifts;
            double priorSupInfPos = 0.0;
            double priorAntPostPos = 0.0;
            double priorLRPos = 0.0;
            int count = 0;
            foreach (Tuple<double, double, double> pos in isoPositions)
            {
                //copy shift from CT ref to sup-inf shifts for first element, otherwise calculate the separation between the current and previous iso (from sup to inf direction)
                //calculate the relative shifts between isocenters (the first isocenter is the CTrefShift)
                SupInfShifts = (pos.Item3 - priorSupInfPos) / 10;
                AntPostShifts = (pos.Item2 - priorAntPostPos) / 10;
                LRShifts = (pos.Item1 - priorLRPos) / 10;
                priorSupInfPos = pos.Item3;
                priorAntPostPos = pos.Item2;
                priorLRPos = pos.Item1;

                shiftsFromBBs.Add(Tuple.Create(pos.Item1 / 10, pos.Item2 / 10, pos.Item3 / 10));
                shiftsBetweenIsos.Add(Tuple.Create(LRShifts, AntPostShifts, SupInfShifts));
                count++;
            }
            return (shiftsFromBBs,shiftsBetweenIsos);
        }

        public int SeparatePlan(ExternalPlanSetup plan, int count)
        {
            foreach (List<Beam> beams in vmatBeamsPerIso)
            {
                //string message = "";
                //foreach (Beam b in beams) message += b.Id + "\n";
                //MessageBox.Show(message);

                //copy the plan, set the plan id based on the counter, and make a empty list to hold the beams that need to be removed
                ExternalPlanSetup newplan = (ExternalPlanSetup)plan.Course.CopyPlanSetup(plan);
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
                    //the setup fields in each new plan (no way to adjust the existing isocenter of an existing beam, it has to be re-added)
                    if (!beams.Where(x => x.Id == b.Id).Any() && !b.IsSetupField) removeMe.Add(b);
                }
                //now remove the beams for the current plan copy
                try { foreach (Beam b in removeMe) newplan.RemoveBeam(b); }
                catch (Exception e) { MessageBox.Show(String.Format("Failed to remove beams in plan {0} because:\n{1}", newplan.Id, e.Message)); }
                count++;
            }
            return count;
        }

        

        public void CalculateDose()
        {
            //loop through each plan in the separatedPlans list (generated in the separate method above) and calculate dose for each plan
            foreach (ExternalPlanSetup p in separatedPlans) p.CalculateDose();
        }
    }
}
