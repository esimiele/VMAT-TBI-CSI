using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
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
        private bool singleAPPAplan;
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

        public PlaceBeams_TBI(StructureSet ss, List<Tuple<string, List<Tuple<string, int>>>> planInfo, bool appaPlan, double[] coll, List<VRect<double>> jp, string linac, string energy, string calcModel, string optModel, string gpuDose, string gpuOpt, string mr, double tgtMargin, bool overlap, double overlapMargin)
        {
            selectedSS = ss;
            planIsoBeamInfo = new List<Tuple<string, List<Tuple<string, int>>>>(planInfo);
            numVMATIsos = planIsoBeamInfo.First().Item2.Count;
            if (planIsoBeamInfo.Count > 1) totalNumIsos = numVMATIsos + planIsoBeamInfo.Last().Item2.Count;
            else totalNumIsos = numVMATIsos;
            singleAPPAplan = appaPlan;
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
                ProvideUIUpdate(100,$"Elapsed time: {GetElapsedTime()}");
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
            //6-10-2020 EAS, checked if exisiting _Legs plan is present in createPlan method
            int percentComplete = 0;
            int calcItems = 4;
            legsPlan = theCourse.AddExternalPlanSetup(selectedSS);
            ProvideUIUpdate((int)(100* ++percentComplete / calcItems), $"Creating AP/PA plan");

            if (singleAPPAplan) legsPlan.Id = String.Format("_Legs");
            else legsPlan.Id = String.Format("{0} Upper Legs", numVMATIsos + 1);
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
                    //if (isoSeparationSup > 380.0 && isoSeparationInf > 380.0) isoSeparationSup = isoSeparationInf = 380.0;
                    //else if (isoSeparationSup > 380.0) isoSeparationSup = 380.0;
                    //else isoSeparationInf = 380.0;
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

            //6-10-2020 EAS, need to reverse order of list because it has to be descending from z location (i.e., sup to inf) for beam placement to work correctly
           // tmp.Reverse();
            
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
                    //if (isoSeparationSup > 380.0 && isoSeparationInf > 380.0) isoSeparationSup = isoSeparationInf = 380.0;
                    //else if (isoSeparationSup > 380.0) isoSeparationSup = 380.0;
                    //else isoSeparationInf = 380.0;
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
                //tmp.Add(new Tuple<VVector, string, int>(v, isoNames.ElementAt(numVMATIsos + i), numBeams.ElementAt(numVMATIsos + i)));
            }
            return tmp;
        }

        protected override List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> GetIsocenterPositions()
        {
            //List<Tuple<ExternalPlanSetup, List<VVector>>> allIsocenters = new List<Tuple<ExternalPlanSetup, List<VVector>>> { };
            List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> allIsocenters = new List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> { };
            //List<VVector> iso = new List<VVector> { };
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

                ////5-11-2020 update EAS. isoSeparationSup is the isocenter separation for the VMAT isos and isoSeparationInf is the iso separation for the AP/PA isocenters
                //double isoSeparationSup = Math.Round(((targetSupExtent - matchline.CenterPoint.z - 380.0) / (numVMATIsos - 1)) / 10.0f) * 10.0f;
                //double isoSeparationInf = Math.Round((matchline.CenterPoint.z - targetInfExtent - 380.0) / 10.0f) * 10.0f;
                //if (isoSeparationSup > 380.0 || isoSeparationInf > 380.0)
                //{
                //    ConfirmPrompt CP = new ConfirmPrompt("Calculated isocenter separation > 38.0 cm, which reduces the overlap between adjacent fields!" + Environment.NewLine + Environment.NewLine + "Truncate isocenter separation to 38.0 cm?!");
                //    CP.ShowDialog();
                //    if (CP.GetSelection())
                //    {
                //        if (isoSeparationSup > 380.0 && isoSeparationInf > 380.0) isoSeparationSup = isoSeparationInf = 380.0;
                //        else if (isoSeparationSup > 380.0) isoSeparationSup = 380.0;
                //        else isoSeparationInf = 380.0;
                //    }
                //}

                //for (int i = 0; i < numVMATIsos; i++)
                //{
                //    VVector v = new VVector();
                //    v.x = userOrigin.x;
                //    v.y = userOrigin.y;
                //    //6-10-2020 EAS, want to count up from matchplane to ensure distance from matchplane is fixed at 190 mm
                //    v.z = matchline.CenterPoint.z + i * isoSeparationSup + 190.0;
                //    //round z position to the nearest integer
                //    v = plan.StructureSet.Image.DicomToUser(v, plan);
                //    v.z = Math.Round(v.z / 10.0f) * 10.0f;
                //    v = plan.StructureSet.Image.UserToDicom(v, plan);
                //    //iso.Add(v);
                //    tmp.Add(new Tuple<VVector, string, int>(v, isoNames.ElementAt(i), numBeams.ElementAt(i)));
                //}

                ////6-10-2020 EAS, need to reverse order of list because it has to be descending from z location (i.e., sup to inf) for beam placement to work correctly
                ////iso.Reverse();
                //tmp.Reverse();
                //6-11-2020 EAS, this is used to account for any rounding of the isocenter position immediately superior to the matchline
                //double offset = iso.LastOrDefault().z - matchlineZ;
                //double offset = tmp.LastOrDefault().Item1.z - matchline.CenterPoint.z;

                //for (int i = 0; i < (numIsos - numVMATIsos); i++)
                //{
                //    VVector v = new VVector();
                //    v.x = userOrigin.x;
                //    v.y = userOrigin.y;
                //    //5-11-2020 update EAS (the first isocenter immediately inferior to the matchline is now a distance = offset away). This ensures the isocenters immediately inferior and superior to the 
                //    //matchline are equidistant from the matchline
                //    v.z = matchline.CenterPoint.z - i * isoSeparationInf - offset;
                //    //round z position to the nearest integer
                //    v = plan.StructureSet.Image.DicomToUser(v, plan);
                //    v.z = Math.Round(v.z / 10.0f) * 10.0f;
                //    v = plan.StructureSet.Image.UserToDicom(v, plan);
                //    //iso.Add(v);
                //    tmp.Add(new Tuple<VVector, string, int>(v, isoNames.ElementAt(numVMATIsos + i), numBeams.ElementAt(numVMATIsos + i)));
                //}
                //allIsocenters.Add(Tuple.Create(plan, new List<VVector>(iso)));
                allIsocenters.Add(Tuple.Create(legsPlan, new List<Tuple<VVector, string, int>>(CalculateAPPAIsoPositions(matchline.CenterPoint.z, targetInfExtent, 400.0, 20.0, tmp.Last().Item1.z))));
            }
            else
            {
                tmp = CalculateVMATIsoPositions(targetSupExtent, targetInfExtent, 10.0, 400.0, 20.0);
                //All VMAT portions of the plans will ONLY have 3 isocenters
                //double isoSeparation = Math.Round(((target.MeshGeometry.Positions.Max(p => p.Z) - target.MeshGeometry.Positions.Min(p => p.Z) - 10.0*numIsos) / numIsos) / 10.0f) * 10.0f;
                //5-7-202 The equation below was determined assuming each VMAT plan would always use 3 isos. In addition, the -30.0 was empirically determined by comparing the calculated isocenter separations to those that were used in the clinical plans
                //isoSeparation = Math.Round(((target.MeshGeometry.Positions.Max(p => p.Z) - target.MeshGeometry.Positions.Min(p => p.Z) - 30.0) / 3) / 10.0f) * 10.0f;

                ////however, the actual correct equation is given below:
                //isoSeparation = Math.Round(((targetSupExtent - targetInfExtent - 380.0) / (numVMATIsos - 1)) / 10.0f) * 10.0f;

                ////It is calculated by setting the most superior and inferior isocenters to be 19.0 cm from the target volume edge in the z-direction. The isocenter separtion is then calculated as half the distance between these two isocenters (sep = ((max-19cm)-(min+19cm)/2).
                ////Tested on 5-7-2020. When the correct equation is rounded, it gives the same answer as the original empirical equation above, however, the isocenters are better positioned in the target volume (i.e., more symmetric about the target volume). 
                ////The ratio of the actual to empirical iso separation equations can be expressed as r=(3/(numVMATIsos-1))((x-380)/(x-30)) where x = (max-min). The ratio is within +/-5% for max-min values (i.e., patient heights) between 99.0 cm (i.e., 3.25 feet) and 116.0 cm

                //if (isoSeparation > 380.0)
                //{
                //    ConfirmPrompt CP = new ConfirmPrompt("Calculated isocenter separation > 38.0 cm, which reduces the overlap between adjacent fields!" + Environment.NewLine + Environment.NewLine + "Truncate isocenter separation to 38.0 cm?!");
                //    CP.ShowDialog();
                //    if (CP.GetSelection()) isoSeparation = 380.0;
                //}

                //for (int i = 0; i < numIsos; i++)
                //{
                //    VVector v = new VVector();
                //    v.x = userOrigin.x;
                //    v.y = userOrigin.y;
                //    //5-7-2020 isocenter positions for actual isocenter separation equation described above
                //    v.z = (targetSupExtent - i * isoSeparation - 190.0);
                //    //round z position to the nearest integer
                //    v = plan.StructureSet.Image.DicomToUser(v, plan);
                //    v.z = Math.Round(v.z / 10.0f) * 10.0f;
                //    v = plan.StructureSet.Image.UserToDicom(v, plan);
                //    //iso.Add(v);
                //    tmp.Add(new Tuple<VVector, string, int>(v, isoNames.ElementAt(i), numBeams.ElementAt(i)));
                //}
                //allIsocenters.Add(Tuple.Create(plan, new List<VVector>(iso)));
                allIsocenters.Add(Tuple.Create(vmatPlan, new List<Tuple<VVector, string, int>>(tmp)));
            }

            //evaluate the distance between the edge of the beam and the max/min of the PTV_body contour. If it is < checkIsoPlacementLimit, then warn the user that they might be fully covering the ptv_body structure.
            //7-17-2020, checkIsoPlacementLimit = 5 mm
            VVector firstIso = tmp.First().Item1;
            VVector lastIso = tmp.Last().Item1;
            if (!((firstIso.z + 200.0) - targetSupExtent >= checkIsoPlacementLimit) ||
                !(targetInfExtent - (lastIso.z - 200.0) >= checkIsoPlacementLimit)) checkIsoPlacement = true;

            //MessageBox.Show(String.Format("{0}, {1}, {2}, {3}, {4}, {5}",
            //    firstIso.z,
            //    lastIso.z,
            //    target.MeshGeometry.Positions.Max(p => p.Z),
            //    target.MeshGeometry.Positions.Min(p => p.Z),
            //    (firstIso.z + 200.0 - target.MeshGeometry.Positions.Max(p => p.Z)),
            //    (target.MeshGeometry.Positions.Min(p => p.Z) - (lastIso.z - 200.0))));

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
                    //zero collimator rotations of two main fields for beams in isocenter immediately superior to matchline. Adjust the third beam such that collimator rotation is 90 degrees. Do not adjust 4th beam
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

            Structure target = StructureTuningHelper.GetStructureFromId("body", selectedSS);
            double targetInfExtent = target.MeshGeometry.Positions.Min(p => p.Z) + targetMargin;

            //place the beams for the VMAT plan
            //unfortunately, all of Nataliya's requirements for beam placement meant that this process couldn't simply draw from beam placement templates. Some of the beam placements for specific isocenters
            //and under certain conditions needed to be hard-coded into the script. I'm not really a fan of this, but it was the only way to satisify Nataliya's requirements.
            ProvideUIUpdate(100, "Preparation complete!");

            //place the beams for the VMAT plan
            int count = 0;
            calcItems = 0;
            percentComplete = 0;
            calcItems += iso.Item2.First().Item3 * 5;
            ProvideUIUpdate(0, $"Assigning isocenter: {1}");

            //adjust x2 jaw (furthest from matchline) so that it covers edge of target volume
            double x2 = iso.Item2.First().Item1.z - (targetInfExtent - 20.0);
            if (x2 > 200.0) x2 = 200.0;
            else if (x2 < 10.0) x2 = 10.0;
            double x1 = -200.0;

            //AP field
            //set MLC positions. First row is bank number 0 (X1 leaves) and second row is bank number 1 (X2).
            float[,] MLCpos = BuildMLCArray(x1, x2);
            Beam b = legsPlan.AddMLCBeam(ebmpStatic, MLCpos, new VRect<double>(x1, -200.0, x2, 200.0), 90.0, 0.0, 0.0, iso.Item2.First().Item1);
            b.Id = String.Format("{0} AP Upper Legs", ++count);
            b.CreateOrReplaceDRR(DRR);

            //PA field
            b = legsPlan.AddMLCBeam(ebmpStatic, MLCpos, new VRect<double>(x1, -200.0, x2, 200.0), 90.0, 180.0, 0.0, iso.Item2.First().Item1);
            b.Id = String.Format("{0} PA Upper Legs", ++count);
            b.CreateOrReplaceDRR(DRR);

            if ((totalNumIsos - numVMATIsos) == 2)
            {
                calcItems += iso.Item2.Last().Item3 * 5;
                VVector infIso = new VVector();
                //the element at numVMATIsos in isoLocations vector is the first AP/PA isocenter
                infIso.x = iso.Item2.Last().Item1.x;
                infIso.y = iso.Item2.Last().Item1.y;

                x1 = -200.0;
                //if the distance between the matchline and the inferior edge of the target is < 600 mm, set the beams in the second isocenter (inferior-most) to be half-beam blocks
                if (StructureTuningHelper.GetStructureFromId("matchline", selectedSS).CenterPoint.z - targetInfExtent < 600.0)
                {
                    infIso.z = iso.Item2.First().Item1.z - 200.0;
                    x1 = 0.0;
                }
                else infIso.z = iso.Item2.First().Item1.z - 390.0;
                //fit x1 jaw to extent of patient
                x2 = infIso.z - (targetInfExtent - 20.0);
                if (x2 > 200.0) x2 = 200.0;
                else if (x2 < 10.0) x2 = 10.0;

                //set MLC positions
                MLCpos = BuildMLCArray(x1, x2);
                //AP field
                b = legsPlan.AddMLCBeam(ebmpStatic, MLCpos, new VRect<double>(x1, -200.0, x2, 200.0), 90.0, 0.0, 0.0, infIso);
                b.Id = String.Format("{0} AP Lower Legs", ++count);
                b.CreateOrReplaceDRR(DRR);

                //PA field
                b = legsPlan.AddMLCBeam(ebmpStatic, MLCpos, new VRect<double>(x1, -200.0, x2, 200.0), 90.0, 180.0, 0.0, infIso);
                b.Id = String.Format("{0} PA Lower Legs", ++count);
                b.CreateOrReplaceDRR(DRR);
            }

            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
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
