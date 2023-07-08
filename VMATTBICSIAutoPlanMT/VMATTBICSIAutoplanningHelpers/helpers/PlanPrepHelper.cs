using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class PlanPrepHelper
    {
        public static Stringbuilder GetShiftNote(StructureSet ss, List<string> isoNames, List<Tuple<double, double, double>> isoPositions)
        {
            Stringbuilder sb = new Stringbuilder();
            //vector to hold the isocenter name, the x,y,z shifts from CT ref, and the shifts between each adjacent iso for each axis (LR, AntPost, SupInf)
            (List<Tuple<double, double, double>> shiftsfromBBs, List<Tuple<double, double, double>> shiftsBetweenIsos) = ExtractIsoPositions(isoPositions);

            //create the message
            double TT = 0;
            //grab the couch surface
            if (StructureTuningHelper.DoesStructureExistInSS("couchsurface", ss, true))
            {
                Structure couchSurface = StructureTuningHelper.GetStructureFromId("couchsurface", ss);
                TT = shiftsfromBBs.First().Item2 - (couchSurface.MeshGeometry.Positions.Min(p => p.Y)) / 10;
                sb.AppendLine("***Bars out***");
            }
            else
            {
                sb.AppendLine("No couch surface structure found in plan!");
            }

            sb.AppendLine("VMAT CSI setup per procedure.");
            sb.AppendLine($"TT = {TT:0.0} cm for all plans");
            sb.AppendLine("Dosimetric shifts SUP to INF:");

            int count = 0;
            foreach (Tuple<double, double, double> itr in shiftsBetweenIsos)
            {
                if (itr == shiftsBetweenIsos.First())
                {
                    //write the first set of shifts from CT ref before the loop. 12-23-2020 support added for the case where the lat/vert shifts are non-zero
                    if (Math.Abs(itr.Item1) >= 0.1 || Math.Abs(itr.Item2) >= 0.1)
                    {
                        sb.AppendLine($"{isoNames.ElementAt(count)} iso shift from CT REF:");
                        if (Math.Abs(itr.Item1) >= 0.1) sb.AppendLine(String.Format("X = {0:0.0} cm {1}", Math.Abs(itr.Item1), (itr.Item1) > 0 ? "LEFT" : "RIGHT"));
                        if (Math.Abs(itr.Item2) >= 0.1) sb.AppendLine(String.Format("Y = {0:0.0} cm {1}", Math.Abs(itr.Item2), (itr.Item2) > 0 ? "POST" : "ANT"));
                        sb.AppendLine(String.Format("Z = {0:0.0} cm {1}", itr.Item3, Math.Abs(itr.Item3) > 0 ? "SUP" : "INF"));
                    }
                    else sb.AppendLine(String.Format("{0} iso shift from CT ref = {1:0.0} cm {2} ({3:0.0} cm {4} from CT ref)", itr.Item1, Math.Abs(itr.Item3), itr.Item3 > 0 ? "SUP" : "INF", Math.Abs(itr.Item3), itr.Item3 > 0 ? "SUP" : "INF"));
                }
                else
                {
                    sb.AppendLine(String.Format("{0} iso shift from {1} iso = {2:0.0} cm {3} ({4:0.0} cm {5} from CT ref)", itr.Item1, isoNames.ElementAt(count - 1), Math.Abs(itr.Item3), itr.Item3 > 0 ? "SUP" : "INF", Math.Abs(itr.Item3), itr.Item3 > 0 ? "SUP" : "INF"));
                }
                count++;
            }

            //copy to clipboard and inform the user it's done
            return sb;
        }

        public static (List<List<Beam>>, List<Tuple<double,double,double>>) ExtractBeamsPerIsoAndIsoPositions(ExternalPlanSetup plan)
        {
            List<List<Beam>> beamsPerIso = new List<List<Beam>> { };
            List<Beam> beams = new List<Beam> { };
            List<Tuple<double, double, double>> isoPositions = new List<Tuple<double, double, double>> { };
            foreach (Beam b in plan.Beams.Where(x => !x.IsSetupField).OrderByDescending(o => o.IsocenterPosition.z))
            {
                VVector v = b.IsocenterPosition;
                v = plan.StructureSet.Image.DicomToUser(v, plan);
                if (!isoPositions.Any(k => k.Item3 == v.z))
                {
                    isoPositions.Add(Tuple.Create(v.x, v.y, v.z));
                    //do NOT add the first detected isocenter to the number of beams per isocenter list. Start with the second isocenter 
                    //(otherwise there will be no beams in the first isocenter, the beams in the first isocenter will be attached to the second isocenter, etc.)
                    if (isoPositions.Count > 1)
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
            return (beamsPerIso, isoPositions);
        }

        public static (List<Tuple<double, double, double>>, List<Tuple<double, double, double>>) ExtractIsoPositions(List<Tuple<double, double, double>> isoPositions)
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
            return (shiftsFromBBs, shiftsBetweenIsos);
        }
    }
}
