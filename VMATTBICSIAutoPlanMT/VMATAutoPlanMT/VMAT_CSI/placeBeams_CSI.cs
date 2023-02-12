using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Media.Media3D;
using VMATAutoPlanMT.baseClasses;
using VMATAutoPlanMT.Prompts;
using System.Windows.Threading;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.ExceptionServices;

namespace VMATAutoPlanMT.VMAT_CSI
{
    class placeBeams_CSI : placeBeamsBase
    {
        //plan, list<iso name, number of beams>
        List<Tuple<string, List<Tuple<string, int>>>> planIsoBeamInfo;
        double isoSeparation = 0;
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

        //to handle system access exception violation
        [HandleProcessCorruptedStateExceptions]
        public override bool Run()
        {
            return GeneratePlanList();
        }

        public override List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> GetIsocenterPositions()
        {
            UpdateUILabel("Calculating isocenter positions: ");
            ProvideUIUpdate(0, String.Format("Extracting isocenter positions for all plans"));
            List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> allIsocenters = new List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> { };
            Image image = selectedSS.Image;
            VVector userOrigin = image.UserOrigin;
            int count = 0;
            foreach (ExternalPlanSetup itr in plans)
            {
                string pid = itr.Id;
                ProvideUIUpdate(String.Format("Retrieving number of isocenters for plan: {0}", pid));
                int numIsos = planIsoBeamInfo.FirstOrDefault(x => x.Item1 == itr.Id).Item2.Count;
                int counter = 0;
                int calcItems = numIsos;
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Num isos for plan (from generateTS): {0}", pid));

                ProvideUIUpdate(String.Format("Retrieving prescriptions for plan: {0}", pid));
                //grab the target in this plan with the greatest z-extent (plans can now have multiple targets assigned)
                List<Tuple<string, string, int, DoseValue, double>> tmpList = prescriptions.Where(x => x.Item1 == itr.Id).ToList();
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Retrieved Presciptions"));

                ProvideUIUpdate(String.Format("Determining target with greatest extent"));
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
                string longestTgtId = longestTargetInPlan.Id;
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Longest target in plan {0}: {1}", pid, longestTgtId));

                List<Tuple<VVector, string, int>> tmp = new List<Tuple<VVector, string, int>> { };
                double spineYMin = 0.0;
                double spineZMax = 0.0;
                double spineZMin = 0.0;
                double brainZCenter = 0.0;
                if (longestTargetInPlan.Id.ToLower() == "ptv_csi")
                {
                    calcItems += 7;
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Retrieving PTV_Spine Structure"));
                    Structure ptvSpine = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_spine");
                    if(ptvSpine == null)
                    {
                        calcItems += 1;
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Failed to find PTV_Spine Structure! Retrieving spinal cord structure"));
                        ptvSpine = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "spinalcord" || x.Id.ToLower() == "spinal_cord");
                        if(ptvSpine == null) ProvideUIUpdate(String.Format("Failed to retrieve spinal cord structure! Cannot calculate isocenter positions! Exiting"), true);
                    }
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Retrieving PTV_Brain Structure"));
                    Structure ptvBrain = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_brain");
                    if (ptvBrain == null)
                    {
                        calcItems += 1;
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Failed to find PTV_Brain Structure! Retrieving brain structure"));
                        ptvBrain = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "brain");
                        if (ptvBrain == null) ProvideUIUpdate(String.Format("Failed to retrieve brain structure! Cannot calculate isocenter positions! Exiting"), true);
                    }

                    ProvideUIUpdate(String.Format("Calculating anterior extent of PTV_Spine"));
                    //Place field isocenters in y-direction at 2/3 the max 
                    spineYMin = (ptvSpine.MeshGeometry.Positions.Min(p => p.Y));
                    ProvideUIUpdate(String.Format("Calculating superior and inferior extent of PTV_Spine"));
                    spineZMax = ptvSpine.MeshGeometry.Positions.Max(p => p.Z);
                    spineZMin = ptvSpine.MeshGeometry.Positions.Min(p => p.Z);
                    if (!ptvSpine.Id.ToLower().Contains("ptv"))
                    {
                        ProvideUIUpdate("Adding 5 mm anterior margin to spinal cord structure to mimic anterior extent of PTV_Spine!");
                        spineYMin += 5;
                        ProvideUIUpdate("Adding 10 mm superior margin to spinal cord structure to mimic superior extent of PTV_Spine!");
                        spineZMax += 10.0;
                        ProvideUIUpdate("Adding 15 mm inferior margin to spinal cord structure to mimic inferior extent of PTV_Spine!");
                        spineZMax -= 15.0;
                    }
                    ProvideUIUpdate(String.Format("Anterior extent of PTV_Spine: {0:0.0} mm", spineYMin));
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Superior extent of PTV_Spine: {0:0.0} mm", spineZMax));
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Inferior extent of PTV_Spine: {0:0.0} mm", spineZMin));

                    spineYMin *= 0.8;
                    //absolute value accounts for positive or negative y position in DCM coordinates
                    if (Math.Abs(spineYMin) < Math.Abs(ptvSpine.CenterPoint.y))
                    {
                        ProvideUIUpdate(String.Format("0.8 * PTV_Spine Ymin is more posterior than center of PTV_Spine!: {0:0.0} mm vs {1:0.0} mm", spineYMin, ptvSpine.CenterPoint.y));
                        spineYMin = ptvSpine.CenterPoint.y;
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Assigning Ant-post iso location to center of PTV_Spine: {0:0.0} mm", spineYMin));
                    }
                    else
                    {
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("0.8 * Anterior extent of PTV_spine: {0:0.0} mm", spineYMin));
                    }

                    //since Brain CTV = Brain and PTV = CTV + 5 mm uniform margin, center of brain is unaffected by adding the 5 mm margin if the PTV_Brain structure could not be found
                    ProvideUIUpdate(String.Format("Calculating center of PTV_Brain"));
                    brainZCenter = ptvBrain.CenterPoint.z;
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Center of PTV_Brain: {0:0.0} mm", brainZCenter));

                    ProvideUIUpdate(String.Format("Calculating center of PTV_Brain to inf extent of PTV_Spine"));
                    targetExtent = brainZCenter - spineZMin;
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Extent: {0:0.0} mm", targetExtent));
                }
                //The actual correct equation for calculating the isocenter separation is given below for VMAT TBI:
                //isoSeparation = Math.Round(((targetExtent - 380.0) / (numIsos - 1)) / 10.0f) * 10.0f;

                //It is calculated by setting the most superior and inferior isocenters to be 19.0 cm from the target volume edge in the z-direction. 
                //The isocenter separtion is then calculated as half the distance between these two isocenters (sep = ((max-19cm)-(min+19cm)/2).
                //NOTE THAT THIS EQUATION WILL NOT WORK FOR VMAT CSI AS IT RESULTS IN A POORLY POSITIONED UPPER SPINE ISOCENTER IF APPLICABLE
                isoSeparation = 380.0;
                
                for (int i = 0; i < numIsos; i++)
                {
                    ProvideUIUpdate(String.Format("Determining position for isocenter: {0}", i));
                    VVector v = new VVector();
                    v.x = userOrigin.x;
                    if (longestTargetInPlan.Id.ToLower() == "ptv_csi")
                    {
                        //special case when the main target is ptv_csi
                        //asign y position to spineYmin
                        v.y = spineYMin;
                        //assign the first isocenter to the center of the ptv_brain
                        if (i == 0) v.z = brainZCenter;
                        //else v.z = (brainZCenter - i * isoSeparation);
                        else
                        {
                            //for all other isocenters work your way down towards the inferior extent of ptv_spine
                            v.z = (spineZMin + (numIsos - i - 1) * isoSeparation + 180.0);
                            if(i == 1)
                            {
                                //for the second isocenter, check to see if it is placed TOO CLOSE to the brain isocenter. Do this by adding 20 cm (1/2 field length) to the proposed second isocenter.
                                //if the resulting value is greater than the brain isocenter z position, force the second isocenter position to be equal to the brain isocenter z position - 20 cm.
                                if (v.z + 200.0 > tmp.ElementAt(0).Item1.z) v.z = tmp.ElementAt(0).Item1.z - 200.0;
                            }
                        }
                    }
                    else
                    {
                        //assign y isocenter position to the y position of the user origin. This will likely change
                        v.y = userOrigin.y;
                        //assumes one isocenter if the target is not ptv_csi
                        v.z = longestTargetInPlan.CenterPoint.z;
                    }
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Calculated isocenter position {0}", i + 1));
                    
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Rounding Y- and Z-positions to nearest integer values"));
                    //round z position to the nearest integer
                    v = itr.StructureSet.Image.DicomToUser(v, itr);
                    v.y = Math.Round(v.y / 10.0f) * 10.0f;
                    v.z = Math.Round(v.z / 10.0f) * 10.0f;
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Calculated isocenter position (user coordinates): ({0}, {1}, {2})", v.x, v.y, v.z));
                    v = itr.StructureSet.Image.UserToDicom(v, itr);
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Adding calculated isocenter position to stack!"));
                    tmp.Add(new Tuple<VVector, string, int>(v, planIsoBeamInfo.FirstOrDefault(x => x.Item1 == itr.Id).Item2.ElementAt(i).Item1, planIsoBeamInfo.FirstOrDefault(x => x.Item1 == itr.Id).Item2.ElementAt(i).Item2));
                }

                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Finished retrieving isocenters for plan: {0}", pid));
                allIsocenters.Add(Tuple.Create(itr, new List<Tuple<VVector, string, int>>(tmp)));
                count++;
            }
            return allIsocenters;
        }

        public override bool SetBeams(Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>> iso)
        {
            ProvideUIUpdate(0, String.Format("Preparing to set isocenters for plan: {0}", iso.Item1.Id));
            int calcItems = 3;
            int counter = 0;
            //DRR parameters (dummy parameters to generate DRRs for each field)
            DRRCalculationParameters DRR = new DRRCalculationParameters();
            DRR.DRRSize = 500.0;
            DRR.FieldOutlines = true;
            DRR.StructureOutlines = true;
            DRR.SetLayerParameters(1, 1.0, 100.0, 1000.0);
            ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Created default DRR parameters"));

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

            //grab all prescriptions assigned to this plan
            List<Tuple<string, string, int, DoseValue, double>> tmp = prescriptions.Where(x => x.Item1 == iso.Item1.Id).ToList();
            //if any of the targets for this plan are ptv_csi, then you must use the special beam placement logic for the initial plan
            if (tmp.Where(x => x.Item2.ToLower().Contains("ptv_csi")).Any())
            {
                target = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_brain"));
                if (target == null)
                {
                    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                    /////2-12-2023 Need to finish!!!
                    calcItems += 3;
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Failed to find PTV_Brain Structure! Retrieving brain structure"));
                    target = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "brain");
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Creating PTV_Brain structure!"));
                    selectedSS.AddStructure("CONTROL", "PTV_Brain");
                    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                }
            }
            else target = selectedSS.Structures.FirstOrDefault(x => x.Id.Contains(tmp.First().Item2));
            ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Retrieved target for plan: {0}", target.Id));

            if (target == null || target.IsEmpty) { ProvideUIUpdate(0, String.Format("Error! Target not found or is not contoured in plan {0}! Exiting!", iso.Item1.Id), true); return true; }
            ProvideUIUpdate(100, String.Format("Preparation complete!"));

            //place the beams for the VMAT plan
            int count = 0;
            string beamName;
            VRect<double> jp;
            calcItems = 0;
            counter = 0;
            for (int i = 0; i < iso.Item2.Count; i++)
            {
                calcItems += iso.Item2.ElementAt(i).Item3 * 6;
                ProvideUIUpdate(0, String.Format("Assigning isocenter: {0}", i + 1));

                if (target.Id.ToLower().Contains("ptv_brain") && i > 0) target = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_spine"));
                //add beams for each isocenter
                for (int j = 0; j < iso.Item2.ElementAt(i).Item3; j++)
                {
                    Beam b;
                    beamName = String.Format("{0} ", count + 1);

                    //kind of messy, but used to increment the collimator rotation one element in the array so you don't end up in a situation where the 
                    //single beam in this isocenter has the same collimator rotation as the single beam in the previous isocenter
                    if (i > 0 && iso.Item2.ElementAt(i).Item3 == 1 && iso.Item2.ElementAt(i - 1).Item3 == 1) j++;

                    jp = jawPos.ElementAt(j);
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Retrieved jaw positions (iso: {0}, beam: {1})", i + 1, j + 1));

                    double coll = collRot[j];
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Retrieved collimator positions (iso: {0}, beam: {1})", i + 1, j + 1));

                    //all even beams (e.g., 2, 4, etc.) will be CCW and all odd beams will be CW
                    if (count % 2 == 0)
                    {
                        b = iso.Item1.AddArcBeam(ebmpArc, jp, coll, CCW[0], CCW[1], GantryDirection.CounterClockwise, 0, iso.Item2.ElementAt(i).Item1);
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Added arc beam to iso: {0}", i));

                        if (j >= 2) beamName += String.Format("CCW {0}{1}", iso.Item2.ElementAt(i).Item2, 90);
                        else beamName += String.Format("CCW {0}{1}", iso.Item2.ElementAt(i).Item2, "");
                    }
                    else
                    {
                        b = iso.Item1.AddArcBeam(ebmpArc, jp, coll, CW[0], CW[1], GantryDirection.Clockwise, 0, iso.Item2.ElementAt(i).Item1);
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Added arc beam to iso: {0}", i + 1));
                        
                        if (j >= 2) beamName += String.Format("CW {0}{1}", iso.Item2.ElementAt(i).Item2, 90);
                        else beamName += String.Format("CW {0}{1}", iso.Item2.ElementAt(i).Item2, "");
                    }
                    //auto fit collimator to target structure
                    //circular margin (in mm), target structure, use asymmetric x Jaws, use asymmetric y jaws, optimize collimator rotation
                    if (target.Id.ToLower().Contains("ptv_brain"))
                    {
                        b.FitCollimatorToStructure(new FitToStructureMargins(30.0, 40.0, 30.0, 30.0), target, true, true, false);
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Fit collimator to: {0}", target.Id));
                        ProvideUIUpdate(String.Format("Asymmetric margin: {0} cm Lat, {1} cm Sup, {2} cm Inf", 3.0, 3.0, 4.0));
                    }
                    else
                    {
                        b.FitCollimatorToStructure(new FitToStructureMargins(30.0), target, true, true, false);
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Fit collimator to: {0}", target.Id));
                        ProvideUIUpdate(String.Format("Uniform margin: {0} cm", 3.0));
                    }

                    b.Id = beamName;
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Assigned beam id: {0}", beamName));

                    b.CreateOrReplaceDRR(DRR);
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Assigned DRR to beam: {0}", beamName));

                    count++;
                }
            }
            return false;
        }
    }
}
