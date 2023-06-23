using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Prompts;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System.Runtime.ExceptionServices;

namespace VMATTBIAutoPlanMT.VMAT_TBI
{
    public class PlaceBeams_TBI : PlaceBeamsBase
    {
        //get methods
        public bool GetCheckIsoPlacementStatus() { return checkIsoPlacement; }
        public double GetCheckIsoPlacementLimit() { return checkIsoPlacementLimit; }

        //list plan, list<iso name, num beams for iso>
        private List<Tuple<string, List<Tuple<string, int>>>> planIsoBeamInfo;
        private ExternalPlanSetup vmatPlan = null;
        private ExternalPlanSetup legsPlan = null;

        //5-5-2020 ask nataliya about importance of matching collimator angles to CW and CCW rotations...
        private double[] collRot;
        private double[] CW = { 181.0, 179.0 };
        private double[] CCW = { 179.0, 181.0 };
        private ExternalBeamMachineParameters ebmpArc;
        private ExternalBeamMachineParameters ebmpStatic;
        private List<VRect<double>> jawPos;
        private double targetMargin;
        private int numVMATIsos;
        private int totalNumIsos;
        protected double checkIsoPlacementLimit = 5.0;
        protected bool checkIsoPlacement = false;

        public PlaceBeams_TBI(StructureSet ss, List<Tuple<string, List<Tuple<string, int>>>> planInfo, double[] coll, List<VRect<double>> jp, string linac, string energy, string calcModel, string optModel, string gpuDose, string gpuOpt, string mr, double tgtMargin, bool overlap, double overlapMargin)
        {
            selectedSS = ss;
            planIsoBeamInfo = new List<Tuple<string, List<Tuple<string, int>>>>(planInfo);
            numVMATIsos = planIsoBeamInfo.First().Item2.Count;
            if (planIsoBeamInfo.Count > 1) totalNumIsos = numVMATIsos + planIsoBeamInfo.Last().Item2.Count;
            else totalNumIsos = numVMATIsos;
            collRot = coll;
            jawPos = new List<VRect<double>>(jp);
            ebmpArc = new ExternalBeamMachineParameters(linac, energy, 600, "ARC", null);
            //AP/PA beams always use 6X
            ebmpStatic = new ExternalBeamMachineParameters(linac, "6X", 600, "STATIC", null);
            //copy the calculation model
            calculationModel = calcModel;
            optimizationModel = optModel;
            useGPUdose = gpuDose;
            useGPUoptimization = gpuOpt;
            MRrestart = mr;
            //convert from cm to mm
            targetMargin = tgtMargin * 10.0;
            //user wants to contour the overlap between fields in adjacent VMAT isocenters
            contourOverlap = overlap;
            contourOverlapMargin = overlapMargin;
            SetCloseOnFinish(true, 500);
        }

        [HandleProcessCorruptedStateExceptions]
        public override bool Run()
        {
            try
            {
                if (CheckExistingCourse()) return true;
                if (CheckExistingPlans()) return true;
                if (CreateVMATPlans()) return true;
                vmatPlan = vmatPlans.First();
                if (planIsoBeamInfo.Count > 1 && CreateAPPAPlan()) return true;
                //plan, List<isocenter position, isocenter name, number of beams per isocenter>
                List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> isoLocations = GetIsocenterPositions();
                UpdateUILabel("Assigning isocenters and beams: ");
                if (SetVMATBeams(isoLocations.First())) return true;
                //ensure contour overlap is requested AND there are more than two isocenters for this plan
                if (contourOverlap && isoLocations.First().Item2.Count > 1) if (ContourFieldOverlap(isoLocations.First(), 0)) return true;
                if (isoLocations.Count > 1)
                {
                    if (SetAPPABeams(isoLocations.Last())) return true;
                }
                UpdateUILabel("Finished!");
                return false;
            }
            catch (Exception e)
            {
                ProvideUIUpdate($"{e.Message}", true);
                stackTraceError = e.StackTrace;
                return true;
            }
        }

        protected override bool CheckExistingPlans()
        {
            //check for vmat plans (contained in prescriptions vector)
            if (base.CheckExistingPlans()) return true;

            //check for any plans containing 'legs' if the total number of isocenters is greater than the number of vmat isocenters
            if (planIsoBeamInfo.Count > 1 && theCourse.ExternalPlanSetups.Any(x => x.Id.ToLower().Contains("legs")))
            {
                ProvideUIUpdate(0, $"One or more legs plans exist in course {theCourse.Id}");
                ProvideUIUpdate("ESAPI can't remove plans in the clinical environment!");
                ProvideUIUpdate("Please manually remove this plan and try again.", true);
                return true;
            }
            return false;
        }

        private bool CreateAPPAPlan()
        {
            UpdateUILabel("Creating AP/PA plan: ");
            int percentComplete = 0;
            int calcItems = 4;
            legsPlan = theCourse.AddExternalPlanSetup(selectedSS);
            ProvideUIUpdate((int)(100* ++percentComplete / calcItems), $"Creating AP/PA plan");

            legsPlan.Id = String.Format("_Legs");
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Set plan Id for {legsPlan.Id}");

            //100% dose prescribed in plan
            //FIX FOR SIB PLANS
            legsPlan.SetPrescription(prescriptions.First().Item3, prescriptions.First().Item4, 1.0);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Set prescription for plan {legsPlan.Id}");
            legsPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, calculationModel);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Set calculation model to {calculationModel}");
            return false;
        }

        private List<Tuple<VVector, string, int>> CalculateVMATIsoPositions(double targetSupExtent, double targetInfExtent, double supInfTargetMargin, double maxFieldYExtent, double minOverlap)
        {
            int percentComplete = 0;
            int calcItems = 10;
            List<Tuple<VVector, string, int>> tmp = new List<Tuple<VVector, string, int>> { };
            Image _image = selectedSS.Image;
            VVector userOrigin = _image.UserOrigin;
            double isoSeparation = Math.Round(((targetSupExtent - targetInfExtent - (maxFieldYExtent - minOverlap)) / (numVMATIsos - 1)) / 10.0f) * 10.0f;
            //5-11-2020 update EAS. isoSeparationSup is the isocenter separation for the VMAT isos and isoSeparationInf is the iso separation for the AP/PA isocenters
            if (isoSeparation > 380.0)
            {
                ConfirmPrompt CP = new ConfirmPrompt("Calculated isocenter separation > 38.0 cm, which reduces the overlap between adjacent fields!" + Environment.NewLine + Environment.NewLine + "Truncate isocenter separation to 38.0 cm?!");
                CP.ShowDialog();
                if (CP.GetSelection())
                {
                    isoSeparation = 380.0;
                }
            }

            for (int i = 0; i < numVMATIsos; i++)
            {
                VVector v = new VVector();
                v.x = userOrigin.x;
                v.y = userOrigin.y;
                //6-10-2020 EAS, want to count up from matchplane to ensure distance from matchplane is fixed at 190 mm
                v.z = targetInfExtent + (numVMATIsos - i - 1) * isoSeparation + (maxFieldYExtent / 2 - supInfTargetMargin);
                //round z position to the nearest integer
                v = _image.DicomToUser(v, vmatPlan);
                v.z = Math.Round(v.z / 10.0f) * 10.0f;
                v = _image.UserToDicom(v, vmatPlan);
                //iso.Add(v);
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Calculated isocenter position {i + 1}");
                tmp.Add(new Tuple<VVector, string, int>(RoundIsocenterPositions(v, vmatPlan, ref percentComplete, ref calcItems),
                                                        planIsoBeamInfo.First().Item2.ElementAt(i).Item1,
                                                        planIsoBeamInfo.First().Item2.ElementAt(i).Item2));
            }

            return tmp;
        }

        private List<Tuple<VVector, string, int>> CalculateAPPAIsoPositions(double targetSupExtent, double targetInfExtent, double maxFieldYExtent, double minOverlap, double lastVMATIsoZPosition)
        {
            int percentComplete = 0;
            int calcItems = 10;
            List<Tuple<VVector, string, int>> tmp = new List<Tuple<VVector, string, int>> { };
            Image _image = selectedSS.Image;
            VVector userOrigin = _image.UserOrigin;
            double isoSeparation = 0;
            if(totalNumIsos - numVMATIsos > 1) isoSeparation = Math.Round(((targetSupExtent - targetInfExtent - (maxFieldYExtent - minOverlap)) / (numVMATIsos - 1)) / 10.0f) * 10.0f;
            //5-11-2020 update EAS. isoSeparationSup is the isocenter separation for the VMAT isos and isoSeparationInf is the iso separation for the AP/PA isocenters
            if (isoSeparation > 380.0)
            {
                ConfirmPrompt CP = new ConfirmPrompt("Calculated isocenter separation > 38.0 cm, which reduces the overlap between adjacent fields!" + Environment.NewLine + Environment.NewLine + "Truncate isocenter separation to 38.0 cm?!");
                CP.ShowDialog();
                if (CP.GetSelection())
                {
                    isoSeparation = 380.0;
                }
            }

            double offset = lastVMATIsoZPosition - targetSupExtent;
            for (int i = 0; i < (totalNumIsos - numVMATIsos); i++)
            {
                VVector v = new VVector();
                v.x = userOrigin.x;
                v.y = userOrigin.y;
                //5-11-2020 update EAS (the first isocenter immediately inferior to the matchline is now a distance = offset away). This ensures the isocenters immediately inferior and superior to the 
                //matchline are equidistant from the matchline
                v.z = targetSupExtent - i * isoSeparation - offset;
                //round z position to the nearest integer
                v = _image.DicomToUser(v, legsPlan);
                v.z = Math.Round(v.z / 10.0f) * 10.0f;
                v = _image.UserToDicom(v, legsPlan);
                //iso.Add(v);
                tmp.Add(new Tuple<VVector, string, int>(RoundIsocenterPositions(v, legsPlan, ref percentComplete, ref calcItems),
                                                        planIsoBeamInfo.Last().Item2.ElementAt(i).Item1,
                                                        planIsoBeamInfo.Last().Item2.ElementAt(i).Item2));
            }
            return tmp;
        }

        protected override List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> GetIsocenterPositions()
        {
            List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> allIsocenters = new List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> { };
            List<Tuple<VVector, string, int>> tmp = new List<Tuple<VVector, string, int>> { };

            Image image = selectedSS.Image;
            VVector userOrigin = image.UserOrigin;
            //if the user requested to add flash to the plan, be sure to grab the ptv_body_flash structure (i.e., the ptv_body structure created from the body with added flash). 
            //This structure is named 'TS_FLASH_TARGET'
            Structure target = StructureTuningHelper.GetStructureFromId("body", selectedSS);
            double targetSupExtent = target.MeshGeometry.Positions.Max(p => p.Z) - targetMargin;
            double targetInfExtent = target.MeshGeometry.Positions.Min(p => p.Z) + targetMargin;

            //matchline is present and not empty
            if (StructureTuningHelper.DoesStructureExistInSS("matchline",selectedSS,true))
            {
                Structure matchline = StructureTuningHelper.GetStructureFromId("matchline", selectedSS);
                tmp = CalculateVMATIsoPositions(targetSupExtent, matchline.CenterPoint.z, 10.0, 400.0, 20.0);
                allIsocenters.Add(Tuple.Create(vmatPlan, new List<Tuple<VVector, string, int>>(tmp)));
                allIsocenters.Add(Tuple.Create(legsPlan, new List<Tuple<VVector, string, int>>(CalculateAPPAIsoPositions(matchline.CenterPoint.z, targetInfExtent, 400.0, 20.0, tmp.Last().Item1.z))));
            }
            else
            {
                tmp = CalculateVMATIsoPositions(targetSupExtent, targetInfExtent, 10.0, 400.0, 20.0);
                allIsocenters.Add(Tuple.Create(vmatPlan, new List<Tuple<VVector, string, int>>(tmp)));
            }

            //evaluate the distance between the edge of the beam and the max/min of the PTV_body contour. If it is < checkIsoPlacementLimit, then warn the user that they might be fully covering the ptv_body structure.
            //7-17-2020, checkIsoPlacementLimit = 5 mm
            VVector firstIso = tmp.First().Item1;
            VVector lastIso = tmp.Last().Item1;
            if (!((firstIso.z + 200.0) - targetSupExtent >= checkIsoPlacementLimit) ||
                !(targetInfExtent - (lastIso.z - 200.0) >= checkIsoPlacementLimit)) checkIsoPlacement = true;

            return allIsocenters;
        }

        protected override bool SetVMATBeams(Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>> iso)
        {
            ProvideUIUpdate(0, $"Preparing to set isocenters for plan: {iso.Item1.Id}");
            int percentComplete = 0;
            int calcItems = 3;
            //DRR parameters (dummy parameters to generate DRRs for each field)
            DRRCalculationParameters DRR = new DRRCalculationParameters
            {
                DRRSize = 500.0,
                FieldOutlines = true,
                StructureOutlines = true
            };
            DRR.SetLayerParameters(1, 1.0, 100.0, 1000.0);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), "Created default DRR parameters");

            //place the beams for the VMAT plan
            //unfortunately, all of Nataliya's requirements for beam placement meant that this process couldn't simply draw from beam placement templates. Some of the beam placements for specific isocenters
            //and under certain conditions needed to be hard-coded into the script. I'm not really a fan of this, but it was the only way to satisify Nataliya's requirements.
            ProvideUIUpdate(100, "Preparation complete!");

            //place the beams for the VMAT plan
            int count = 0;
            string beamName;
            VRect<double> jp;
            calcItems = 0;
            percentComplete = 0;
            for (int i = 0; i < iso.Item2.Count; i++)
            {
                calcItems += iso.Item2.ElementAt(i).Item3 * 5;
                ProvideUIUpdate(0, $"Assigning isocenter: {i + 1}");

                //beam counter
                for (int j = 0; j < iso.Item2.ElementAt(i).Item3; j++)
                {
                    //second isocenter and third beam requires the x-jaw positions to be mirrored about the y-axis (these jaw positions are in the fourth element of the jawPos list)
                    //this is generally the isocenter located in the pelvis and we want the beam aimed at the kidneys-area
                    if (i == 1 && j == 2) jp = jawPos.ElementAt(j + 1);
                    else if (i == 1 && j == 3) jp = jawPos.ElementAt(j - 1);
                    else jp = jawPos.ElementAt(j);
                    Beam b;
                    beamName = "";
                    beamName += String.Format("{0} ", count + 1);
                    //zero collimator rotations of two main fields for beams in isocenter immediately superior to matchline. 
                    //Adjust the third beam such that collimator rotation is 90 degrees. Do not adjust 4th beam
                    double coll = collRot[j];
                    if ((totalNumIsos > numVMATIsos) && (i == (numVMATIsos - 1)))
                    {
                        if (j < 2) coll = 0.0;
                        else if (j == 2) coll = 90.0;
                    }
                    //all even beams (e.g., 2, 4, etc.) will be CCW and all odd beams will be CW
                    if (count % 2 == 0)
                    {
                        b = vmatPlan.AddArcBeam(ebmpArc, jp, coll, CCW[0], CCW[1], GantryDirection.CounterClockwise, 0, iso.Item2.ElementAt(i).Item1);
                        ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Added arc beam to iso: {i + 1}");

                        if (j >= 2) beamName += $"CCW {iso.Item2.ElementAt(i).Item2}{90}";
                        else beamName += $"CCW {iso.Item2.ElementAt(i).Item2}";
                    }
                    else
                    {
                        b = vmatPlan.AddArcBeam(ebmpArc, jp, coll, CW[0], CW[1], GantryDirection.Clockwise, 0, iso.Item2.ElementAt(i).Item1);
                        ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Added arc beam to iso: {i + 1}");

                        if (j >= 2) beamName += $"CW {iso.Item2.ElementAt(i).Item2}{90}";
                        else beamName += $"CW {iso.Item2.ElementAt(i).Item2}";
                    }
                    b.Id = beamName;
                    ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Assigned beam id: {beamName}");

                    b.CreateOrReplaceDRR(DRR);
                    ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Assigned DRR to beam: {beamName}");

                    count++;
                }
            }
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        private bool SetAPPABeams(Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>> iso)
        {
            ProvideUIUpdate(0, $"Preparing to set isocenters for plan: {iso.Item1.Id}");
            int percentComplete = 0;
            int calcItems = 3 + iso.Item2.First().Item3 * 5;

            Structure target = StructureTuningHelper.GetStructureFromId("body", selectedSS);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), "Retrieved body structure");
            double targetInfExtent = target.MeshGeometry.Positions.Min(p => p.Z) + targetMargin;
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Calculated target inferior extent: {targetInfExtent}");

            //place the beams for the VMAT plan
            //unfortunately, all of Nataliya's requirements for beam placement meant that this process couldn't simply draw from beam placement templates. Some of the beam placements for specific isocenters
            //and under certain conditions needed to be hard-coded into the script. I'm not really a fan of this, but it was the only way to satisify Nataliya's requirements.
            ProvideUIUpdate("Preparation complete!");

            //place the beams for the VMAT plan
            int count = 0;
            ProvideUIUpdate($"Assigning isocenter: {1}");

            double x1, y1, x2, y2;
            x1 = y1 = -200.0;
            y2 = 200;
            //adjust x2 jaw (furthest from matchline) so that it covers edge of target volume
            x2 = CalculateX2Jaws(iso.Item2.First().Item1.z, targetInfExtent, 20.0);
            VRect<double> jaws = GenerateJawsPositions(x1, y1, x2, y2, iso.Item2.First().Item2);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems));

            //AP field
            //set MLC positions. First row is bank number 0 (X1 leaves) and second row is bank number 1 (X2).
            float[,] MLCpos = BuildMLCArray(x1, x2);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Generated MLC positions for iso: {iso.Item2.First().Item2}");
            
            CreateAPBeam(++count, "Upper", MLCpos, jaws, iso.Item2.First().Item1);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Added AP beam to iso: {iso.Item2.First().Item2}");

            //PA field
            CreatePABeam(++count, "Upper", MLCpos, jaws, iso.Item2.First().Item1);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Added PA beam to iso: {iso.Item2.First().Item2}");

            //lower legs field if applicable
            if (iso.Item2.Count > 1)
            {
                ProvideUIUpdate($"Added beams to lower leg isocenter: {iso.Item2.Last().Item2}");
                calcItems += iso.Item2.Last().Item3 * 5;
                VVector infIso = new VVector
                {
                    //the element at numVMATIsos in isoLocations vector is the first AP/PA isocenter
                    x = iso.Item2.Last().Item1.x,
                    y = iso.Item2.Last().Item1.y
                };

                //if the distance between the matchline and the inferior edge of the target is < 600 mm, set the beams in the second isocenter (inferior-most) to be half-beam blocks
                double legsTargetExtent = StructureTuningHelper.GetStructureFromId("matchline", selectedSS).CenterPoint.z - targetInfExtent;
                if (legsTargetExtent < 600.0)
                {
                    ProvideUIUpdate($"Separation between matchline center z and target inferior extent: {legsTargetExtent:0.0} mm");
                    infIso.z = iso.Item2.First().Item1.z - 200.0;
                    ProvideUIUpdate($"legs target extent is < 60 cm! Adjusting isocenter z position from {iso.Item2.First().Item1.z:0.0} mm to {infIso.z:0.0} mm");
                    ProvideUIUpdate($"Setting X1 jaw position to 0.0 --> Half beam block");
                    x1 = 0.0;
                }
                else infIso.z = iso.Item2.First().Item1.z - 390.0;
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Calculated lower leg isocenter: ({infIso.x:0.0}, {infIso.y:0.0}, {infIso.z:0.0}) mm");

                //fit x1 jaw to extent of patient
                x2 = CalculateX2Jaws(infIso.z, targetInfExtent, 20.0);
                jaws = GenerateJawsPositions(x1, y1, x2, y2, iso.Item2.Last().Item2);
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems));

                //set MLC positions
                MLCpos = BuildMLCArray(x1, x2);
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Generated MLC positions for iso: {iso.Item2.Last().Item2}");
                
                //AP field
                CreateAPBeam(++count, "Lower", MLCpos, jaws, infIso);
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Added AP beam to iso: {iso.Item2.Last().Item2}");

                //PA field
                CreatePABeam(++count, "Lower", MLCpos, jaws, infIso);
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Added PA beam to iso: {iso.Item2.Last().Item2}");
            }
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        private void CreateAPBeam(int beamCount, string beamPositionId, float[,] MLCs, VRect<double> jaws, VVector iso)
        {
            Beam b = CreateStaticMLCBeam(0.0, beamCount, MLCs, jaws, iso);
            b.Id = $"{beamCount} AP {beamPositionId} Legs";
        }

        private void CreatePABeam(int beamCount, string beamPositionId, float[,] MLCs, VRect<double> jaws, VVector iso)
        {
            Beam b = CreateStaticMLCBeam(180.0, beamCount, MLCs, jaws, iso);
            b.Id = $"{beamCount} PA {beamPositionId} Legs";
        }

        private Beam CreateStaticMLCBeam(double gantryAngle, int beamCount, float[,] MLCs, VRect<double> jaws, VVector iso)
        {
            Beam b = legsPlan.AddMLCBeam(ebmpStatic, MLCs, jaws, 90.0, gantryAngle, 0.0, iso);
            b.CreateOrReplaceDRR(GenerateDRRParameters());
            return b;
        }

        private VRect<double> GenerateJawsPositions(double x1, double y1, double x2, double y2, string isoId)
        {
            VRect<double> jaws = new VRect<double>(x1, y1, x2, y2);
            ProvideUIUpdate($"Calculated jaw positions for lower leg isocenter: {isoId}");
            ProvideUIUpdate($"x1: {jaws.X1:0.0}");
            ProvideUIUpdate($"x2: {jaws.X2:0.0}");
            ProvideUIUpdate($"y1: {jaws.Y1:0.0}");
            ProvideUIUpdate($"y2: {jaws.Y2:0.0}");
            return jaws;
        }

        private double CalculateX2Jaws(double isoZPosition, double targetInfExtent, double minFieldOverlap)
        {
            //adjust x2 jaw (furthest from matchline) so that it covers edge of target volume
            double x2 = isoZPosition - (targetInfExtent - minFieldOverlap);
            if (x2 > 200.0) x2 = 200.0;
            else if (x2 < 10.0) x2 = 10.0;
            return x2;
        }

        private float[,] BuildMLCArray(double x1, double x2)
        {
            float[,] MLCpos = new float[2, 60];
            for (int j = 0; j < 60; j++)
            {
                MLCpos[0, j] = (float)(x1);
                MLCpos[1, j] = (float)(x2);
            }
            return MLCpos;
        }
    }
}
