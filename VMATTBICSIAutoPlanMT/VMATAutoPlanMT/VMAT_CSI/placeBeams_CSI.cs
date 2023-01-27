using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Media.Media3D;
using VMATAutoPlanMT.baseClasses;
using VMATAutoPlanMT.Prompts;

namespace VMATAutoPlanMT.VMAT_CSI
{
    class placeBeams_CSI : placeBeamsBase
    {
        //List<List<int>> numBeams;
        List<Tuple<string, List<Tuple<string, int>>>> planIsoBeamInfo;
        //public List<Tuple<string,List<string>>> isoNames;
        double isoSeparation = 0;
        //public ExternalPlanSetup plan = null;
        double[] collRot;
        double[] CW = { 181.0, 179.0 };
        double[] CCW = { 179.0, 181.0 };
        ExternalBeamMachineParameters ebmpArc;
        List<VRect<double>> jawPos;

        public placeBeams_CSI(StructureSet ss, List<Tuple<string, List<Tuple<string, int>>>> planInfo, double[] coll, List<VRect<double>> jp, string linac, string energy, string calcModel, string optModel, string gpuDose, string gpuOpt, string mr)
        {
            selectedSS = ss;
            planIsoBeamInfo = new List<Tuple<string, List<Tuple<string, int>>>>(planInfo);
            collRot = coll;
            jawPos = new List<VRect<double>>(jp);
            ebmpArc = new ExternalBeamMachineParameters(linac, energy, 600, "ARC", null);
            //copy the calculation model
            calculationModel = calcModel;
            optimizationModel = optModel;
            useGPUdose = gpuDose;
            useGPUoptimization = gpuOpt;
            MRrestart = mr;
        }

        public placeBeams_CSI(StructureSet ss, List<Tuple<string, List<Tuple<string, int>>>> planInfo, double[] coll, List<VRect<double>> jp, string linac, string energy, string calcModel, string optModel, string gpuDose, string gpuOpt, string mr, double overlapMargin)
        {
            selectedSS = ss;
            planIsoBeamInfo = new List<Tuple<string, List<Tuple<string, int>>>>(planInfo);
            collRot = coll;
            jawPos = new List<VRect<double>>(jp);
            ebmpArc = new ExternalBeamMachineParameters(linac, energy, 600, "ARC", null);
            //copy the calculation model
            calculationModel = calcModel;
            optimizationModel = optModel;
            useGPUdose = gpuDose;
            useGPUoptimization = gpuOpt;
            MRrestart = mr;
            //user wants to contour the overlap between fields in adjacent VMAT isocenters
            contourOverlap = true;
            contourOverlapMargin = overlapMargin;
        }

        public override List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> getIsocenterPositions()
        {
            List <Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> allIsocenters = new List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> { };
            Image image = selectedSS.Image;
            VVector userOrigin = image.UserOrigin;
            int count = 0;
            foreach (ExternalPlanSetup itr in plans)
            {
                //grab the target in this plan with the greatest z-extent (plans can now have multiple targets assigned)
                List<Tuple<string, string, int, DoseValue, double>> tmpList = prescriptions.Where(x => x.Item1 == itr.Id).ToList();
                double targetExtent = 0.0;
                Structure longestTargetInPlan = null;
                foreach (Tuple<string, string, int, DoseValue, double> itr1 in tmpList)
                {
                    Structure tmpTargStruct = selectedSS.Structures.FirstOrDefault(x => x.Id == itr1.Item2);
                    Point3DCollection pts = tmpTargStruct.MeshGeometry.Positions;
                    double tmpDiff = pts.Max(p => p.Z) - pts.Min(p => p.Z);
                    if (tmpDiff > targetExtent) { longestTargetInPlan = tmpTargStruct; targetExtent = tmpDiff; }
                }
                if (longestTargetInPlan == null) return new List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> { };
                List<Tuple<VVector, string, int>> tmp = new List<Tuple<VVector, string, int>> { };
                int numIsos = planIsoBeamInfo.FirstOrDefault(x => x.Item1 == itr.Id).Item2.Count;

                double spineYMin = 0.0;
                double spineZMax = 0.0;
                double spineZMin = 0.0;
                double brainZCenter = 0.0;
                if (longestTargetInPlan.Id.ToLower() == "ptv_csi")
                {
                    Structure ptvSpine = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_spine");
                    Structure ptvBrain = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_brain");
                    //Place field isocenters in y-direction at 2/3 the max 
                    spineYMin = (ptvSpine.MeshGeometry.Positions.Min(p => p.Y) * 0.8);
                    spineZMax = ptvSpine.MeshGeometry.Positions.Max(p => p.Z);
                    spineZMin = ptvSpine.MeshGeometry.Positions.Min(p => p.Z);
                    brainZCenter = ptvBrain.CenterPoint.z;
                    targetExtent = brainZCenter - ptvSpine.MeshGeometry.Positions.Min(p => p.Z);
                }

                //All VMAT portions of the plans will ONLY have 3 isocenters
                //double isoSeparation = Math.Round(((target.MeshGeometry.Positions.Max(p => p.Z) - target.MeshGeometry.Positions.Min(p => p.Z) - 10.0*numIsos) / numIsos) / 10.0f) * 10.0f;
                //5-7-202 The equation below was determined assuming each VMAT plan would always use 3 isos. In addition, the -30.0 was empirically determined by comparing the calculated isocenter separations to those that were used in the clinical plans
                //isoSeparation = Math.Round(((target.MeshGeometry.Positions.Max(p => p.Z) - target.MeshGeometry.Positions.Min(p => p.Z) - 30.0) / 3) / 10.0f) * 10.0f;

                //however, the actual correct equation is given below (VMAT TBI):
                //isoSeparation = Math.Round(((targetExtent - 380.0) / (numIsos - 1)) / 10.0f) * 10.0f;
                isoSeparation = 190.0;

                //It is calculated by setting the most superior and inferior isocenters to be 19.0 cm from the target volume edge in the z-direction. The isocenter separtion is then calculated as half the distance between these two isocenters (sep = ((max-19cm)-(min+19cm)/2).
                //Tested on 5-7-2020. When the correct equation is rounded, it gives the same answer as the original empirical equation above, however, the isocenters are better positioned in the target volume (i.e., more symmetric about the target volume). 
                //The ratio of the actual to empirical iso separation equations can be expressed as r=(3/(numVMATIsos-1))((x-380)/(x-30)) where x = (max-min). The ratio is within +/-5% for max-min values (i.e., patient heights) between 99.0 cm (i.e., 3.25 feet) and 116.0 cm

                //if (isoSeparation > 380.0)
                //{
                //    var CUI = new confirmUI();
                //    CUI.message.Text = "Calculated isocenter separation > 38.0 cm, which reduces the overlap between adjacent fields!" + Environment.NewLine + Environment.NewLine + "Truncate isocenter separation to 38.0 cm?!";
                //    CUI.ShowDialog();
                //    if (CUI.confirm) isoSeparation = 380.0;
                //}
                
                for (int i = 0; i < numIsos; i++)
                {
                    VVector v = new VVector();
                    v.x = userOrigin.x;
                    if (longestTargetInPlan.Id.ToLower() == "ptv_csi")
                    {
                        v.y = spineYMin;
                        if (i == 0) v.z = brainZCenter;
                        //else v.z = (brainZCenter - i * isoSeparation);
                        else v.z = (spineZMin + (numIsos - i) * isoSeparation);
                    }
                    else
                    {
                        v.y = userOrigin.y;
                        //assumes one isocenter if the target is not ptv_csi
                        v.z = longestTargetInPlan.CenterPoint.z;
                    }
                    //round z position to the nearest integer
                    v = itr.StructureSet.Image.DicomToUser(v, itr);
                    v.y = Math.Round(v.y / 10.0f) * 10.0f;
                    v.z = Math.Round(v.z / 10.0f) * 10.0f;
                    v = itr.StructureSet.Image.UserToDicom(v, itr);
                    tmp.Add(new Tuple<VVector, string, int>(v, planIsoBeamInfo.FirstOrDefault(x => x.Item1 == itr.Id).Item2.ElementAt(i).Item1, planIsoBeamInfo.FirstOrDefault(x => x.Item1 == itr.Id).Item2.ElementAt(i).Item2));
                }

                //evaluate the distance between the edge of the beam and the max/min of the PTV_body contour. If it is < checkIsoPlacementLimit, then warn the user that they might be fully covering the ptv_body structure.
                //7-17-2020, checkIsoPlacementLimit = 5 mm
                VVector firstIso = tmp.First().Item1;
                VVector lastIso = tmp.Last().Item1;
                if (!((firstIso.z + 200.0) - longestTargetInPlan.MeshGeometry.Positions.Max(p => p.Z) >= checkIsoPlacementLimit) ||
                    !(longestTargetInPlan.MeshGeometry.Positions.Min(p => p.Z) - (lastIso.z - 200.0) >= checkIsoPlacementLimit)) checkIsoPlacement = true;

                allIsocenters.Add(Tuple.Create(itr, new List<Tuple<VVector, string, int>>(tmp)));
                count++;
            }
            return allIsocenters;

            ////matchline is present and not empty
            //if (selectedSS.Structures.Where(x => x.Id.ToLower() == "matchline").Any() && !selectedSS.Structures.First(x => x.Id.ToLower() == "matchline").IsEmpty)
            //{

            //    //5-11-2020 update EAS. isoSeparationSup is the isocenter separation for the VMAT isos and isoSeparationInf is the iso separation for the AP/PA isocenters
            //    double isoSeparationSup = Math.Round(((target.MeshGeometry.Positions.Max(p => p.Z) - selectedSS.Structures.First(x => x.Id.ToLower() == "matchline").CenterPoint.z - 380.0) / (numVMATIsos - 1)) / 10.0f) * 10.0f;
            //    double isoSeparationInf = Math.Round((selectedSS.Structures.First(x => x.Id.ToLower() == "matchline").CenterPoint.z - target.MeshGeometry.Positions.Min(p => p.Z) - 380.0) / 10.0f) * 10.0f;
            //    if (isoSeparationSup > 380.0 || isoSeparationInf > 380.0)
            //    {
            //        var CUI = new confirmUI();
            //        CUI.message.Text = "Calculated isocenter separation > 38.0 cm, which reduces the overlap between adjacent fields!" + Environment.NewLine + Environment.NewLine + "Truncate isocenter separation to 38.0 cm?!";
            //        CUI.cancelBTN.Text = "No";
            //        CUI.ShowDialog();
            //        if (CUI.confirm)
            //        {
            //            if (isoSeparationSup > 380.0 && isoSeparationInf > 380.0) isoSeparationSup = isoSeparationInf = 380.0;
            //            else if (isoSeparationSup > 380.0) isoSeparationSup = 380.0;
            //            else isoSeparationInf = 380.0;
            //        }
            //    }

            //    double matchlineZ = selectedSS.Structures.First(x => x.Id.ToLower() == "matchline").CenterPoint.z;
            //    for (int i = 0; i < numVMATIsos; i++)
            //    {
            //        VVector v = new VVector();
            //        v.x = userOrigin.x;
            //        v.y = userOrigin.y;
            //        //6-10-2020 EAS, want to count up from matchplane to ensure distance from matchplane is fixed at 190 mm
            //        v.z = matchlineZ + i * isoSeparationSup + 190.0;
            //        //round z position to the nearest integer
            //        v = plan.StructureSet.Image.DicomToUser(v, plan);
            //        v.z = Math.Round(v.z / 10.0f) * 10.0f;
            //        v = plan.StructureSet.Image.UserToDicom(v, plan);
            //        iso.Add(v);
            //    }

            //    //6-10-2020 EAS, need to reverse order of list because it has to be descending from z location (i.e., sup to inf) for beam placement to work correctly
            //    iso.Reverse();
            //    //6-11-2020 EAS, this is used to account for any rounding of the isocenter position immediately superior to the matchline
            //    double offset = iso.LastOrDefault().z - matchlineZ;

            //    //for (int i = 0; i < (numIsos - numVMATIsos); i++)
            //    //{
            //    //    VVector v = new VVector();
            //    //    v.x = userOrigin.x;
            //    //    v.y = userOrigin.y;
            //    //    //5-11-2020 update EAS (the first isocenter immediately inferior to the matchline is now a distance = offset away). This ensures the isocenters immediately inferior and superior to the 
            //    //    //matchline are equidistant from the matchline
            //    //    v.z = matchlineZ - i * isoSeparationInf - offset;
            //    //    //round z position to the nearest integer
            //    //    v = plan.StructureSet.Image.DicomToUser(v, plan);
            //    //    v.z = Math.Round(v.z / 10.0f) * 10.0f;
            //    //    v = plan.StructureSet.Image.UserToDicom(v, plan);
            //    //    iso.Add(v);
            //    //}
            //}
            //else
            //{
            //    //All VMAT portions of the plans will ONLY have 3 isocenters
            //    //double isoSeparation = Math.Round(((target.MeshGeometry.Positions.Max(p => p.Z) - target.MeshGeometry.Positions.Min(p => p.Z) - 10.0*numIsos) / numIsos) / 10.0f) * 10.0f;
            //    //5-7-202 The equation below was determined assuming each VMAT plan would always use 3 isos. In addition, the -30.0 was empirically determined by comparing the calculated isocenter separations to those that were used in the clinical plans
            //    //isoSeparation = Math.Round(((target.MeshGeometry.Positions.Max(p => p.Z) - target.MeshGeometry.Positions.Min(p => p.Z) - 30.0) / 3) / 10.0f) * 10.0f;

            //    //however, the actual correct equation is given below:
            //    isoSeparation = Math.Round(((target.MeshGeometry.Positions.Max(p => p.Z) - target.MeshGeometry.Positions.Min(p => p.Z) - 380.0) / (numVMATIsos - 1)) / 10.0f) * 10.0f;

            //    //It is calculated by setting the most superior and inferior isocenters to be 19.0 cm from the target volume edge in the z-direction. The isocenter separtion is then calculated as half the distance between these two isocenters (sep = ((max-19cm)-(min+19cm)/2).
            //    //Tested on 5-7-2020. When the correct equation is rounded, it gives the same answer as the original empirical equation above, however, the isocenters are better positioned in the target volume (i.e., more symmetric about the target volume). 
            //    //The ratio of the actual to empirical iso separation equations can be expressed as r=(3/(numVMATIsos-1))((x-380)/(x-30)) where x = (max-min). The ratio is within +/-5% for max-min values (i.e., patient heights) between 99.0 cm (i.e., 3.25 feet) and 116.0 cm

            //    if (isoSeparation > 380.0)
            //    {
            //        var CUI = new confirmUI();
            //        CUI.message.Text = "Calculated isocenter separation > 38.0 cm, which reduces the overlap between adjacent fields!" + Environment.NewLine + Environment.NewLine + "Truncate isocenter separation to 38.0 cm?!";
            //        CUI.ShowDialog();
            //        if (CUI.confirm) isoSeparation = 380.0;
            //    }

            //    for (int i = 0; i < numVMATIsos; i++)
            //    {
            //        VVector v = new VVector();
            //        v.x = userOrigin.x;
            //        v.y = userOrigin.y;
            //        //5-7-2020 isocenter positions for actual isocenter separation equation described above
            //        v.z = (target.MeshGeometry.Positions.Max(p => p.Z) - i * isoSeparation - 190.0);
            //        //round z position to the nearest integer
            //        v = plan.StructureSet.Image.DicomToUser(v, plan);
            //        v.z = Math.Round(v.z / 10.0f) * 10.0f;
            //        v = plan.StructureSet.Image.UserToDicom(v, plan);
            //        iso.Add(v);
            //    }
            //}

            //MessageBox.Show(String.Format("{0}, {1}, {2}, {3}, {4}, {5}",
            //    firstIso.z,
            //    lastIso.z,
            //    target.MeshGeometry.Positions.Max(p => p.Z),
            //    target.MeshGeometry.Positions.Min(p => p.Z),
            //    (firstIso.z + 200.0 - target.MeshGeometry.Positions.Max(p => p.Z)),
            //    (target.MeshGeometry.Positions.Min(p => p.Z) - (lastIso.z - 200.0))));

            //return iso;
        }

        public override bool setBeams(Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>> iso)
        {
            //DRR parameters (dummy parameters to generate DRRs for each field)
            DRRCalculationParameters DRR = new DRRCalculationParameters();
            DRR.DRRSize = 500.0;
            DRR.FieldOutlines = true;
            DRR.StructureOutlines = true;
            DRR.SetLayerParameters(1, 1.0, 100.0, 1000.0);
            //int count = 0;
            //List<string> isoNameList = new List<string> { }; 
            //List<int> beamList = new List<int> { };
            //foreach (Tuple<string,List<string>> itr in isoNames)
            //{
            //    //determine isocenter name list, number of beams per isocenter list, and target structure from matching the plan Ids in the isoNames list and iso list
            //    if(itr.Item1 == iso.Item1.Id)
            //    {
            //        isoNameList = new List<string>(itr.Item2);
            //        beamList = new List<int>(numBeams.ElementAt(count));
            //        //item 2 is target Id
            //        target = selectedSS.Structures.FirstOrDefault(x => x.Id == prescriptions.ElementAt(count).Item2);
            //        if (target.Id.ToLower().Contains("ptv_csi")) target = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_brain"));
            //        break;
            //    }
            //    count++;
            //}
            List<Tuple<string, string, int, DoseValue, double>> tmp = prescriptions.Where(x => x.Item1 == iso.Item1.Id).ToList();
            if(tmp.Where(x => x.Item2.ToLower().Contains("ptv_csi")).Any()) target = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_brain"));
            else target = selectedSS.Structures.FirstOrDefault(x => x.Id.Contains(tmp.First().Item2));

            if (target == null || target.IsEmpty) { MessageBox.Show(String.Format("Error! Target not found or is not contoured in plan {0}! Exiting!", iso.Item1.Id)); return true; }

            //if (isoNameList == null) { MessageBox.Show(String.Format("Failed to set beams for plan: {0}! Exiting!", iso.Item1.Id)); return; }

            //place the beams for the VMAT plan
            int count = 0;
            string beamName;
            VRect<double> jp;
            for (int i = 0; i < iso.Item2.Count; i++)
            {
                if (target.Id.ToLower().Contains("ptv_brain") && i > 0) target = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_spine"));
                //add beams for each isocenter
                for (int j = 0; j < iso.Item2.ElementAt(i).Item3; j++)
                {
                    //kind of messy, but used to increment the collimator rotation one element in the array so you don't end up in a situation where the single beam in this isocenter has the same collimator rotation as
                    //the single beam in the previous isocenter
                    if (i > 0 && iso.Item2.ElementAt(i).Item3 == 1 && iso.Item2.ElementAt(i - 1).Item3 == 1) j++;
                    jp = jawPos.ElementAt(j);
                    Beam b;
                    beamName = "";
                    beamName += String.Format("{0} ", count + 1);
                    double coll = collRot[j];
                    //all even beams (e.g., 2, 4, etc.) will be CCW and all odd beams will be CW
                    if (count % 2 == 0)
                    {
                        b = iso.Item1.AddArcBeam(ebmpArc, jp, coll, CCW[0], CCW[1], GantryDirection.CounterClockwise, 0, iso.Item2.ElementAt(i).Item1);
                        if (j >= 2) beamName += String.Format("CCW {0}{1}", iso.Item2.ElementAt(i).Item2, 90);
                        else beamName += String.Format("CCW {0}{1}", iso.Item2.ElementAt(i).Item2, "");
                    }
                    else
                    {
                        b = iso.Item1.AddArcBeam(ebmpArc, jp, coll, CW[0], CW[1], GantryDirection.Clockwise, 0, iso.Item2.ElementAt(i).Item1);
                        if (j >= 2) beamName += String.Format("CW {0}{1}", iso.Item2.ElementAt(i).Item2, 90);
                        else beamName += String.Format("CW {0}{1}", iso.Item2.ElementAt(i).Item2, "");
                    }
                    //auto fit collimator to target structure
                    //circular margin (in mm), target structure, use asymmetric x Jaws, use asymmetric y jaws, optimize collimator rotation
                    b.FitCollimatorToStructure(new FitToStructureMargins(30.0), target, true, true, false);
                    b.Id = beamName;
                    b.CreateOrReplaceDRR(DRR);
                    count++;
                }
            }
            return false;

            /*
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
            */
        }
    }
}
