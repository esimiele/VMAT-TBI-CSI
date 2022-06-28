using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Media.Media3D;

namespace VMATAutoPlanMT
{
    class placeBeams_TBI : placeBeamsBase
    {
        int numIsos;
        int[] numBeams;
        public List<string> isoNames;
        double isoSeparation = 0;
        ExternalPlanSetup legs_planUpper = null;
        bool singleAPPAplan;

        //5-5-2020 ask nataliya about importance of matching collimator angles to CW and CCW rotations...
        double[] collRot;
        double[] CW = { 181.0, 179.0 };
        double[] CCW = { 179.0, 181.0 };
        ExternalBeamMachineParameters ebmpArc;
        ExternalBeamMachineParameters ebmpStatic;
        List<VRect<double>> jawPos;
        private bool useFlash;

        public placeBeams_TBI(StructureSet ss, Tuple<int, DoseValue> presc, List<string> i, int iso, int vmatIso, bool appaPlan, int[] beams, double[] coll, List<VRect<double>> jp, string linac, string energy, string calcModel, string optModel, string gpuDose, string gpuOpt, string mr, bool flash)
        {
            selectedSS = ss;
            prescription = presc;
            isoNames = new List<string>(i);
            numIsos = iso;
            numVMATIsos = vmatIso;
            singleAPPAplan = appaPlan;
            numBeams = beams;
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
            useFlash = flash;
        }

        public placeBeams_TBI(StructureSet ss, Tuple<int, DoseValue> presc, List<string> i, int iso, int vmatIso, bool appaPlan, int[] beams, double[] coll, List<VRect<double>> jp, string linac, string energy, string calcModel, string optModel, string gpuDose, string gpuOpt, string mr, bool flash, double overlapMargin)
        {
            selectedSS = ss;
            prescription = presc;
            isoNames = new List<string>(i);
            numIsos = iso;
            numVMATIsos = vmatIso;
            singleAPPAplan = appaPlan;
            numBeams = beams;
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
            useFlash = flash;
            //user wants to contour the overlap between fields in adjacent VMAT isocenters
            contourOverlap = true;
            contourOverlapMargin = overlapMargin;
        }

        public override bool checkExistingPlans(string planID)
        {
            //6-10-2020 EAS, research system only!
            //if (tbi.ExternalPlanSetups.Where(x => x.Id == "_VMAT TBI").Any()) if (tbi.CanRemovePlanSetup((tbi.ExternalPlanSetups.First(x => x.Id == "_VMAT TBI")))) tbi.RemovePlanSetup(tbi.ExternalPlanSetups.First(x => x.Id == "_VMAT TBI"));
            if (theCourse.ExternalPlanSetups.Where(x => x.Id == String.Format("_{0}", planID)).Any())
            {
                MessageBox.Show(String.Format("A plan named '_{0}' Already exists! \nESAPI can't remove plans in the clinical environment! \nPlease manually remove this plan and try again.", planID));
                return true;
            }

            if ((numIsos > numVMATIsos) && theCourse.ExternalPlanSetups.Where(x => x.Id.ToLower().Contains("legs")).Any())
            {
                MessageBox.Show("Plan(s) with the string 'legs' already exists! \nESAPI can't remove plans in the clinical environment! \nPlease manually remove this plan and try again.");
                return true;
            }
            return false;
        }

        public override List<VVector> getIsocenterPositions()
        {
            List<VVector> iso = new List<VVector> { };
            Image image = selectedSS.Image;
            VVector userOrigin = image.UserOrigin;
            //if the user requested to add flash to the plan, be sure to grab the ptv_body_flash structure (i.e., the ptv_body structure created from the body with added flash). 
            //This structure is named 'TS_FLASH_TARGET'
            if (useFlash) target = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ts_flash_target");
            else target = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_body");

            //matchline is present and not empty
            if (selectedSS.Structures.Where(x => x.Id.ToLower() == "matchline").Any() && !selectedSS.Structures.First(x => x.Id.ToLower() == "matchline").IsEmpty)
            {

                //5-11-2020 update EAS. isoSeparationSup is the isocenter separation for the VMAT isos and isoSeparationInf is the iso separation for the AP/PA isocenters
                double isoSeparationSup = Math.Round(((target.MeshGeometry.Positions.Max(p => p.Z) - selectedSS.Structures.First(x => x.Id.ToLower() == "matchline").CenterPoint.z - 380.0) / (numVMATIsos - 1)) / 10.0f) * 10.0f;
                double isoSeparationInf = Math.Round((selectedSS.Structures.First(x => x.Id.ToLower() == "matchline").CenterPoint.z - target.MeshGeometry.Positions.Min(p => p.Z) - 380.0) / 10.0f) * 10.0f;
                if (isoSeparationSup > 380.0 || isoSeparationInf > 380.0)
                {
                    var CUI = new confirmUI();
                    CUI.message.Text = "Calculated isocenter separation > 38.0 cm, which reduces the overlap between adjacent fields!" + Environment.NewLine + Environment.NewLine + "Truncate isocenter separation to 38.0 cm?!";
                    CUI.cancelBTN.Text = "No";
                    CUI.ShowDialog();
                    if (CUI.confirm)
                    {
                        if (isoSeparationSup > 380.0 && isoSeparationInf > 380.0) isoSeparationSup = isoSeparationInf = 380.0;
                        else if (isoSeparationSup > 380.0) isoSeparationSup = 380.0;
                        else isoSeparationInf = 380.0;
                    }
                }

                double matchlineZ = selectedSS.Structures.First(x => x.Id.ToLower() == "matchline").CenterPoint.z;
                for (int i = 0; i < numVMATIsos; i++)
                {
                    VVector v = new VVector();
                    v.x = userOrigin.x;
                    v.y = userOrigin.y;
                    //6-10-2020 EAS, want to count up from matchplane to ensure distance from matchplane is fixed at 190 mm
                    v.z = matchlineZ + i * isoSeparationSup + 190.0;
                    //round z position to the nearest integer
                    v = plan.StructureSet.Image.DicomToUser(v, plan);
                    v.z = Math.Round(v.z / 10.0f) * 10.0f;
                    v = plan.StructureSet.Image.UserToDicom(v, plan);
                    iso.Add(v);
                }

                //6-10-2020 EAS, need to reverse order of list because it has to be descending from z location (i.e., sup to inf) for beam placement to work correctly
                iso.Reverse();
                //6-11-2020 EAS, this is used to account for any rounding of the isocenter position immediately superior to the matchline
                double offset = iso.LastOrDefault().z - matchlineZ;

                for (int i = 0; i < (numIsos - numVMATIsos); i++)
                {
                    VVector v = new VVector();
                    v.x = userOrigin.x;
                    v.y = userOrigin.y;
                    //5-11-2020 update EAS (the first isocenter immediately inferior to the matchline is now a distance = offset away). This ensures the isocenters immediately inferior and superior to the 
                    //matchline are equidistant from the matchline
                    v.z = matchlineZ - i * isoSeparationInf - offset;
                    //round z position to the nearest integer
                    v = plan.StructureSet.Image.DicomToUser(v, plan);
                    v.z = Math.Round(v.z / 10.0f) * 10.0f;
                    v = plan.StructureSet.Image.UserToDicom(v, plan);
                    iso.Add(v);
                }
            }
            else
            {
                //All VMAT portions of the plans will ONLY have 3 isocenters
                //double isoSeparation = Math.Round(((target.MeshGeometry.Positions.Max(p => p.Z) - target.MeshGeometry.Positions.Min(p => p.Z) - 10.0*numIsos) / numIsos) / 10.0f) * 10.0f;
                //5-7-202 The equation below was determined assuming each VMAT plan would always use 3 isos. In addition, the -30.0 was empirically determined by comparing the calculated isocenter separations to those that were used in the clinical plans
                //isoSeparation = Math.Round(((target.MeshGeometry.Positions.Max(p => p.Z) - target.MeshGeometry.Positions.Min(p => p.Z) - 30.0) / 3) / 10.0f) * 10.0f;

                //however, the actual correct equation is given below:
                isoSeparation = Math.Round(((target.MeshGeometry.Positions.Max(p => p.Z) - target.MeshGeometry.Positions.Min(p => p.Z) - 380.0) / (numVMATIsos - 1)) / 10.0f) * 10.0f;

                //It is calculated by setting the most superior and inferior isocenters to be 19.0 cm from the target volume edge in the z-direction. The isocenter separtion is then calculated as half the distance between these two isocenters (sep = ((max-19cm)-(min+19cm)/2).
                //Tested on 5-7-2020. When the correct equation is rounded, it gives the same answer as the original empirical equation above, however, the isocenters are better positioned in the target volume (i.e., more symmetric about the target volume). 
                //The ratio of the actual to empirical iso separation equations can be expressed as r=(3/(numVMATIsos-1))((x-380)/(x-30)) where x = (max-min). The ratio is within +/-5% for max-min values (i.e., patient heights) between 99.0 cm (i.e., 3.25 feet) and 116.0 cm

                if (isoSeparation > 380.0)
                {
                    var CUI = new confirmUI();
                    CUI.message.Text = "Calculated isocenter separation > 38.0 cm, which reduces the overlap between adjacent fields!" + Environment.NewLine + Environment.NewLine + "Truncate isocenter separation to 38.0 cm?!";
                    CUI.ShowDialog();
                    if (CUI.confirm) isoSeparation = 380.0;
                }

                for (int i = 0; i < numIsos; i++)
                {
                    VVector v = new VVector();
                    v.x = userOrigin.x;
                    v.y = userOrigin.y;
                    //5-7-2020 isocenter positions for actual isocenter separation equation described above
                    v.z = (target.MeshGeometry.Positions.Max(p => p.Z) - i * isoSeparation - 190.0);
                    //round z position to the nearest integer
                    v = plan.StructureSet.Image.DicomToUser(v, plan);
                    v.z = Math.Round(v.z / 10.0f) * 10.0f;
                    v = plan.StructureSet.Image.UserToDicom(v, plan);
                    iso.Add(v);
                }
            }

            //evaluate the distance between the edge of the beam and the max/min of the PTV_body contour. If it is < checkIsoPlacementLimit, then warn the user that they might be fully covering the ptv_body structure.
            //7-17-2020, checkIsoPlacementLimit = 5 mm
            VVector firstIso = iso.First();
            VVector lastIso = iso.Last();
            if (!((firstIso.z + 200.0) - target.MeshGeometry.Positions.Max(p => p.Z) >= checkIsoPlacementLimit) ||
                !(target.MeshGeometry.Positions.Min(p => p.Z) - (lastIso.z - 200.0) >= checkIsoPlacementLimit)) checkIsoPlacement = true;

            //MessageBox.Show(String.Format("{0}, {1}, {2}, {3}, {4}, {5}",
            //    firstIso.z,
            //    lastIso.z,
            //    target.MeshGeometry.Positions.Max(p => p.Z),
            //    target.MeshGeometry.Positions.Min(p => p.Z),
            //    (firstIso.z + 200.0 - target.MeshGeometry.Positions.Max(p => p.Z)),
            //    (target.MeshGeometry.Positions.Min(p => p.Z) - (lastIso.z - 200.0))));

            return iso;
        }

        public override void setBeams(List<VVector> isoLocations)
        {
            //DRR parameters (dummy parameters to generate DRRs for each field)
            DRRCalculationParameters DRR = new DRRCalculationParameters();
            DRR.DRRSize = 500.0;
            DRR.FieldOutlines = true;
            DRR.StructureOutlines = true;
            DRR.SetLayerParameters(1, 1.0, 100.0, 1000.0);

            //place the beams for the VMAT plan
            //unfortunately, all of Nataliya's requirements for beam placement meant that this process couldn't simply draw from beam placement templates. Some of the beam placements for specific isocenters
            //and under certain conditions needed to be hard-coded into the script. I'm not really a fan of this, but it was the only way to satisify Nataliya's requirements.
            int count = 0;
            string beamName;
            VRect<double> jp;
            for (int i = 0; i < numVMATIsos; i++)
            {
                for (int j = 0; j < numBeams[i]; j++)
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
                    if ((numIsos > numVMATIsos) && (i == (numVMATIsos - 1)))
                    {
                        if (j < 2) coll = 0.0;
                        else if (j == 2) coll = 90.0;
                    }
                    //all even beams (e.g., 2, 4, etc.) will be CCW and all odd beams will be CW
                    if (count % 2 == 0)
                    {
                        b = plan.AddArcBeam(ebmpArc, jp, coll, CCW[0], CCW[1], GantryDirection.CounterClockwise, 0, isoLocations.ElementAt(i));
                        if (j >= 2) beamName += String.Format("CCW {0}{1}", isoNames.ElementAt(i), 90);
                        else beamName += String.Format("CCW {0}{1}", isoNames.ElementAt(i), "");
                    }
                    else
                    {
                        b = plan.AddArcBeam(ebmpArc, jp, coll, CW[0], CW[1], GantryDirection.Clockwise, 0, isoLocations.ElementAt(i));
                        if (j >= 2) beamName += String.Format("CW {0}{1}", isoNames.ElementAt(i), 90);
                        else beamName += String.Format("CW {0}{1}", isoNames.ElementAt(i), "");
                    }
                    b.Id = beamName;
                    b.CreateOrReplaceDRR(DRR);
                    count++;
                }
            }

            //add additional plan for ap/pa legs fields (all ap/pa isocenter fields will be contained within this plan)
            if (numIsos > numVMATIsos)
            {
                //6-10-2020 EAS, checked if exisiting _Legs plan is present in createPlan method
                legs_planUpper = theCourse.AddExternalPlanSetup(selectedSS);
                if (singleAPPAplan) legs_planUpper.Id = String.Format("_Legs");
                else legs_planUpper.Id = String.Format("{0} Upper Legs", numVMATIsos + 1);
                //100% dose prescribed in plan
                legs_planUpper.SetPrescription(prescription.Item1, prescription.Item2, 1.0);
                legs_planUpper.SetCalculationModel(CalculationType.PhotonVolumeDose, calculationModel);

                Structure target;
                if (useFlash) target = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ts_flash_target");
                else target = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_body");

                //adjust x2 jaw (furthest from matchline) so that it covers edge of target volume
                double x2 = isoLocations.ElementAt(numVMATIsos).z - (target.MeshGeometry.Positions.Min(p => p.Z) - 20.0);
                if (x2 > 200.0) x2 = 200.0;
                else if (x2 < 10.0) x2 = 10.0;

                //AP field
                //set MLC positions. First row is bank number 0 (X1 leaves) and second row is bank number 1 (X2).
                float[,] MLCpos = new float[2, 60];
                for (int i = 0; i < 60; i++)
                {
                    MLCpos[0, i] = (float)-200.0;
                    MLCpos[1, i] = (float)(x2);
                }
                Beam b = legs_planUpper.AddMLCBeam(ebmpStatic, MLCpos, new VRect<double>(-200.0, -200.0, x2, 200.0), 90.0, 0.0, 0.0, isoLocations.ElementAt(numVMATIsos));
                b.Id = String.Format("{0} AP Upper Legs", ++count);
                b.CreateOrReplaceDRR(DRR);

                //PA field
                b = legs_planUpper.AddMLCBeam(ebmpStatic, MLCpos, new VRect<double>(-200.0, -200.0, x2, 200.0), 90.0, 180.0, 0.0, isoLocations.ElementAt(numVMATIsos));
                b.Id = String.Format("{0} PA Upper Legs", ++count);
                b.CreateOrReplaceDRR(DRR);

                if ((numIsos - numVMATIsos) == 2)
                {
                    VVector infIso = new VVector();
                    //the element at numVMATIsos in isoLocations vector is the first AP/PA isocenter
                    infIso.x = isoLocations.ElementAt(numVMATIsos).x;
                    infIso.y = isoLocations.ElementAt(numVMATIsos).y;

                    double x1 = -200.0;
                    //if the distance between the matchline and the inferior edge of the target is < 600 mm, set the beams in the second isocenter (inferior-most) to be half-beam blocks
                    if (selectedSS.Structures.First(x => x.Id.ToLower() == "matchline").CenterPoint.z - target.MeshGeometry.Positions.Min(p => p.Z) < 600.0)
                    {
                        infIso.z = isoLocations.ElementAt(numVMATIsos).z - 200.0;
                        x1 = 0.0;
                    }
                    else infIso.z = isoLocations.ElementAt(numVMATIsos).z - 390.0;
                    //fit x1 jaw to extend of patient
                    x2 = infIso.z - (target.MeshGeometry.Positions.Min(p => p.Z) - 20.0);
                    if (x2 > 200.0) x2 = 200.0;
                    else if (x2 < 10.0) x2 = 10.0;

                    //set MLC positions
                    MLCpos = new float[2, 60];
                    for (int i = 0; i < 60; i++)
                    {
                        MLCpos[0, i] = (float)(x1);
                        MLCpos[1, i] = (float)(x2);
                    }
                    //AP field
                    if (singleAPPAplan)
                    {
                        b = legs_planUpper.AddMLCBeam(ebmpStatic, MLCpos, new VRect<double>(x1, -200.0, x2, 200.0), 90.0, 0.0, 0.0, infIso);
                        b.Id = String.Format("{0} AP Lower Legs", ++count);
                        b.CreateOrReplaceDRR(DRR);

                        //PA field
                        b = legs_planUpper.AddMLCBeam(ebmpStatic, MLCpos, new VRect<double>(x1, -200.0, x2, 200.0), 90.0, 180.0, 0.0, infIso);
                        b.Id = String.Format("{0} PA Lower Legs", ++count);
                        b.CreateOrReplaceDRR(DRR);
                    }
                    else
                    {
                        //create a new legs plan if the user wants to separate the two APPA isocenters into separate plans
                        ExternalPlanSetup legs_planLower = theCourse.AddExternalPlanSetup(selectedSS);
                        legs_planLower.Id = String.Format("{0} Lower Legs", numIsos);
                        legs_planLower.SetPrescription(prescription.Item1, prescription.Item2, 1.0);
                        legs_planLower.SetCalculationModel(CalculationType.PhotonVolumeDose, calculationModel);

                        b = legs_planLower.AddMLCBeam(ebmpStatic, MLCpos, new VRect<double>(x1, -200.0, x2, 200.0), 90.0, 0.0, 0.0, infIso);
                        b.Id = String.Format("{0} AP Lower Legs", ++count);
                        b.CreateOrReplaceDRR(DRR);

                        //PA field
                        b = legs_planLower.AddMLCBeam(ebmpStatic, MLCpos, new VRect<double>(x1, -200.0, x2, 200.0), 90.0, 180.0, 0.0, infIso);
                        b.Id = String.Format("{0} PA Lower Legs", ++count);
                        b.CreateOrReplaceDRR(DRR);
                    }
                }
            }
            MessageBox.Show("Beams placed successfully!\nPlease proceed to the optimization setup tab!");
        }
    }
}
