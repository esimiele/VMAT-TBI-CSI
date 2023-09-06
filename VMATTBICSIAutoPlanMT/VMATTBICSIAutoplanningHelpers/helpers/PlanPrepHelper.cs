using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Media3D;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class PlanPrepHelper
    {
        /// <summary>
        /// Helper method to gather the relevant information that should be put in the TBI shift note
        /// </summary>
        /// <param name="vmatPlan"></param>
        /// <param name="appaPlan"></param>
        /// <returns></returns>
        public static StringBuilder GetTBIShiftNote(ExternalPlanSetup vmatPlan, ExternalPlanSetup appaPlan)
        {
            StringBuilder sb = new StringBuilder();
            List<Tuple<double, double, double>> isoPositions = ExtractIsoPositions(vmatPlan);
            int numVMATIsos = isoPositions.Count;
            int numIsos = numVMATIsos;

            if (appaPlan != null)
            {
                isoPositions.AddRange(ExtractIsoPositions(appaPlan));
                numIsos = isoPositions.Count;
            }
            List<string> isoNames = new List<string>(IsoNameHelper.GetTBIVMATIsoNames(numVMATIsos, numIsos));
            if (appaPlan != null) isoNames.AddRange(IsoNameHelper.GetTBIAPPAIsoNames(numVMATIsos, numIsos));

            //vector to hold the x,y,z shifts from CT ref and the shifts between each adjacent iso for each axis (LR, AntPost, SupInf)
            (List<Tuple<double, double, double>> shiftsfromBBs, List<Tuple<double, double, double>> shiftsBetweenIsos) = CalculateShifts(isoPositions);

            //create the message
            double TT = -1;
            //grab the couch surface
            if (StructureTuningHelper.DoesStructureExistInSS("couchsurface", vmatPlan.StructureSet, true))
            {
                Structure couchSurface = StructureTuningHelper.GetStructureFromId("couchsurface", vmatPlan.StructureSet);
                TT = (vmatPlan.Beams.First(x => !x.IsSetupField).IsocenterPosition.y- couchSurface.MeshGeometry.Positions.Min(p => p.Y)) / 10;
            }

            sb.Append(BuildTBIShiftNote(TT, isoNames, shiftsBetweenIsos, numVMATIsos, numIsos));
            return sb;
        }

        /// <summary>
        /// Helper method to take the relevant shift information and build the shift note for TBI plans
        /// </summary>
        /// <param name="TT"></param>
        /// <param name="isoNames"></param>
        /// <param name="shiftsBetweenIsos"></param>
        /// <param name="numVMATIsos"></param>
        /// <param name="numIsos"></param>
        /// <returns></returns>
        private static StringBuilder BuildTBIShiftNote(double TT, List<string> isoNames, List<Tuple<double, double, double>> shiftsBetweenIsos, int numVMATIsos, int numIsos)
        {
            StringBuilder sb = new StringBuilder();
            if (TT != -1)
            {
                sb.AppendLine("***Bars out***");
            }
            else sb.AppendLine("No couch surface structure found in plan!");

            if (numIsos > numVMATIsos) sb.AppendLine("VMAT TBI setup per procedure. Please ensure the matchline on Spinning Manny and the bag matches");
            else sb.AppendLine("VMAT TBI setup per procedure. No Spinning Manny.");
            if(TT != -1) sb.AppendLine($"TT = {TT:0.0} cm for all plans");
            sb.AppendLine("Dosimetric shifts SUP to INF:");

            int count = 0;
            foreach (Tuple<double, double, double> itr in shiftsBetweenIsos)
            {
                if (count == numVMATIsos)
                {
                    //if numVMATisos == numIsos this message won't be displayed. Otherwise, we have exhausted the vmat isos and need to add these lines to the shift note
                    sb.AppendLine("Rotate Spinning Manny, shift to opposite Couch Lat");
                    sb.AppendLine("Upper Leg iso - same Couch Lng as Pelvis iso");
                    //let the therapists know that they need to shift couch lateral to the opposite side if the initial lat shift was non-zero
                    if (!CalculationHelper.AreEqual(shiftsBetweenIsos.First().Item1, 0.0)) sb.AppendLine("Shift couch lateral to opposite side!");
                }
                else
                {
                    if (itr == shiftsBetweenIsos.First())
                    {
                        sb.AppendLine($"{isoNames.ElementAt(count)} iso shift from CT REF:");
                    }
                    else
                    {
                        sb.AppendLine($"{isoNames.ElementAt(count)} shift from **{isoNames.ElementAt(count - 1)} ISO**");
                    }
                    if (!CalculationHelper.AreEqual(itr.Item1, 0.0)) sb.AppendLine($"X = {Math.Abs(itr.Item1):0.0} cm {(itr.Item1 > 0 ? "LEFT" : "RIGHT")}");
                    if (!CalculationHelper.AreEqual(itr.Item2, 0.0)) sb.AppendLine($"Y = {Math.Abs(itr.Item2):0.0} cm {(itr.Item2 > 0 ? "POST" : "ANT")}");
                    sb.AppendLine($"Z = {Math.Abs(itr.Item3):0.0} cm {(itr.Item3 > 0 ? "SUP" : "INF")}");
                }
                count++;
            }
            return sb;
        }

        /// <summary>
        /// Helper method to gather the relevant information that should be put in the CSI shift note
        /// </summary>
        /// <param name="vmatPlan"></param>
        /// <param name="appaPlan"></param>
        /// <returns></returns>
        public static StringBuilder GetCSIShiftNote(ExternalPlanSetup vmatPlan)
        {
            StringBuilder sb = new StringBuilder();
            List<Tuple<double, double, double>> isoPositions = ExtractIsoPositions(vmatPlan);

            //vector to hold the isocenter name, the x,y,z shifts from CT ref, and the shifts between each adjacent iso for each axis (LR, AntPost, SupInf)
            (List<Tuple<double, double, double>> shiftsfromBBs, List<Tuple<double, double, double>> shiftsBetweenIsos) = CalculateShifts(isoPositions);

            //create the message
            double TT = -1;
            //grab the couch surface
            if (StructureTuningHelper.DoesStructureExistInSS("couchsurface", vmatPlan.StructureSet, true))
            {
                Structure couchSurface = StructureTuningHelper.GetStructureFromId("couchsurface", vmatPlan.StructureSet);
                TT = (vmatPlan.Beams.First(x => !x.IsSetupField).IsocenterPosition.y - couchSurface.MeshGeometry.Positions.Min(p => p.Y)) / 10;
            }

            sb.Append(BuildCSIShiftNote(TT, IsoNameHelper.GetCSIIsoNames(isoPositions.Count), shiftsBetweenIsos));
            return sb;
        }

        /// <summary>
        /// Helper method to take the relevant shift information and build the shift note for CSI plan
        /// </summary>
        /// <param name="TT"></param>
        /// <param name="isoNames"></param>
        /// <param name="shiftsBetweenIsos"></param>
        /// <param name="numVMATIsos"></param>
        /// <param name="numIsos"></param>
        /// <returns></returns>
        private static StringBuilder BuildCSIShiftNote(double TT, List<string> isoNames, List<Tuple<double, double, double>> shiftsBetweenIsos)
        {
            StringBuilder sb = new StringBuilder();
            if(TT != -1)
            {
                sb.AppendLine("***Bars out***");
            }
            else sb.AppendLine("No couch surface structure found in plan!");

            sb.AppendLine("VMAT CSI setup per procedure.");
            if(TT != -1) sb.AppendLine($"TT = {TT:0.0} cm for all plans");
            sb.AppendLine("Dosimetric shifts SUP to INF:");

            int count = 0;
            foreach (Tuple<double, double, double> itr in shiftsBetweenIsos)
            {
                if (itr == shiftsBetweenIsos.First())
                {
                    sb.AppendLine($"{isoNames.ElementAt(count)} iso shift from CT REF:");
                }
                else
                {
                    sb.AppendLine($"{isoNames.ElementAt(count)} shift from **{isoNames.ElementAt(count - 1)} ISO**");
                }
                if (!CalculationHelper.AreEqual(itr.Item1, 0.0)) sb.AppendLine($"X = {Math.Abs(itr.Item1):0.0} cm {(itr.Item1 > 0 ? "LEFT" : "RIGHT")}");
                if (!CalculationHelper.AreEqual(itr.Item2, 0.0)) sb.AppendLine($"Y = {Math.Abs(itr.Item2):0.0} cm {(itr.Item2 > 0 ? "POST" : "ANT")}");
                sb.AppendLine($"Z = {Math.Abs(itr.Item3):0.0} cm {(itr.Item3 > 0 ? "SUP" : "INF")}");
                count++;
            }
            return sb;
        }

        /// <summary>
        /// Helper method to extract the isocenter positions for the supplied plan
        /// </summary>
        /// <param name="plan"></param>
        /// <returns></returns>
        public static List<Tuple<double,double,double>> ExtractIsoPositions(ExternalPlanSetup plan)
        {
            List<Tuple<double, double, double>> isoPositions = new List<Tuple<double, double, double>> { };
            //order from sup to inf for HFS
            foreach (Beam b in plan.Beams.Where(x => !x.IsSetupField).OrderByDescending(o => o.IsocenterPosition.z))
            {
                VVector v = b.IsocenterPosition;
                v = plan.StructureSet.Image.DicomToUser(v, plan);
                if (!isoPositions.Any(k => CalculationHelper.AreEqual(k.Item3, v.z)))
                {
                    isoPositions.Add(Tuple.Create(v.x, v.y, v.z));
                }
            }
            return isoPositions;
        }

        /// <summary>
        /// Helper method to take the supplied plan and extract a list of the list of beams for each isocenter
        /// </summary>
        /// <param name="plan"></param>
        /// <returns></returns>
        public static List<List<Beam>> ExtractBeamsPerIso(ExternalPlanSetup plan)
        {
            List<List<Beam>> beamsPerIso = new List<List<Beam>> { };
            List<Beam> beams = new List<Beam> { };
            double PreviosIsoZ = -10000.0;
            foreach (Beam b in plan.Beams.Where(x => !x.IsSetupField).OrderByDescending(o => o.IsocenterPosition.z))
            {
                VVector v = b.IsocenterPosition;
                if (!CalculationHelper.AreEqual(v.z, PreviosIsoZ))
                {
                    //do NOT add the first detected isocenter to the number of beams per isocenter list. Start with the second isocenter 
                    //(otherwise there will be no beams in the first isocenter, the beams in the first isocenter will be attached to the second isocenter, etc.)
                    if (beams.Any())
                    {
                        //NOTE: it is important to have 'new List<Beam>(beams)' as the argument rather than 'beams'. A list of a list is essentially a pointer to a list, so if you delete the sublists,
                        //the list of lists will have the correct number of elements, but they will all be empty
                        beamsPerIso.Add(new List<Beam>(beams));
                        beams.Clear();
                    }
                }
                //add the current beam to the sublist
                beams.Add(b);
                PreviosIsoZ = v.z;
            }
            //add the beams from the last isocenter to the vmat beams per iso list
            beamsPerIso.Add(new List<Beam>(beams));
            return beamsPerIso;
        }

        /// <summary>
        /// Helper method to calculate the shifts from the bbs and the shifts between isocenters for the supplied list of isocenters
        /// </summary>
        /// <param name="isoPositions"></param>
        /// <returns></returns>
        public static (List<Tuple<double, double, double>>, List<Tuple<double, double, double>>) CalculateShifts(List<Tuple<double, double, double>> isoPositions)
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
