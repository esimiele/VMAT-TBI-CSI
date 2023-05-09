using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Media.Media3D;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System.Runtime.ExceptionServices;
using System.Text;

namespace VMATCSIAutoPlanMT.VMAT_CSI
{
    public class PlaceBeams_CSI : PlaceBeamsBase
    {
        //plan, list<iso name, number of beams>
        private List<Tuple<string, List<Tuple<string, int>>>> planIsoBeamInfo;
        private double isoSeparation = 0;
        private double[] collRot;
        private double[] CW = { 181.0, 179.0 };
        private double[] CCW = { 179.0, 181.0 };
        private ExternalBeamMachineParameters ebmpArc;
        private List<VRect<double>> jawPos;

        public PlaceBeams_CSI(StructureSet ss, List<Tuple<string, List<Tuple<string, int>>>> planInfo, double[] coll, List<VRect<double>> jp, string linac, string energy, string calcModel, string optModel, string gpuDose, string gpuOpt, string mr, bool overlap, double overlapMargin)
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
            contourOverlap = overlap;
            contourOverlapMargin = overlapMargin;
        }

        //to handle system access exception violation
        [HandleProcessCorruptedStateExceptions]
        public override bool Run()
        {
            return GeneratePlanList();
        }

        protected override List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> GetIsocenterPositions()
        {
            UpdateUILabel("Calculating isocenter positions: ");
            ProvideUIUpdate(0, "Extracting isocenter positions for all plans");
            List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> allIsocenters = new List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> { };
            Image image = selectedSS.Image;
            VVector userOrigin = image.UserOrigin;
            int count = 0;
            List<Tuple<string, List<string>>> planIdTargets = new List<Tuple<string, List<string>>>(TargetsHelper.GetTargetListForEachPlan(prescriptions));
            foreach (ExternalPlanSetup itr in plans)
            {
                ProvideUIUpdate($"Retrieving number of isocenters for plan: {itr.Id}");
                int numIsos = planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)).Item2.Count;
                int counter = 0;
                int calcItems = numIsos;
                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Num isos for plan (from generateTS): {itr.Id}");

                ProvideUIUpdate($"Retrieving prescriptions for plan: {itr.Id}");
                //grab the target in this plan with the greatest z-extent (plans can now have multiple targets assigned)
                ProvideUIUpdate((int)(100 * ++counter / calcItems), "Retrieved Presciptions");

                ProvideUIUpdate("Determining target with greatest extent");
                (bool fail, Structure longestTargetInPlan, double maxTargetLength, StringBuilder errorMessage) = TargetsHelper.GetLongestTargetInPlan(planIdTargets.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)), selectedSS);
                if (fail)
                {
                    ProvideUIUpdate($"Error! No structure named: {errorMessage} found or contoured!", true);
                    return new List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> { };
                }
                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Longest target in plan {itr.Id}: {longestTargetInPlan.Id}");

                List<Tuple<VVector, string, int>> tmp = new List<Tuple<VVector, string, int>> { };
                double spineYMin = 0.0;
                double spineZMax = 0.0;
                double spineZMin = 0.0;
                double brainZCenter = 0.0;
                if (string.Equals(longestTargetInPlan.Id.ToLower(), "ptv_csi"))
                {
                    calcItems += 7;
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), "Retrieving PTV_Spine Structure");
                    Structure ptvSpine = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "ptv_spine"));
                    if(ptvSpine == null)
                    {
                        calcItems += 1;
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), "Failed to find PTV_Spine Structure! Retrieving spinal cord structure");
                        ptvSpine = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "spinalcord") || string.Equals(x.Id.ToLower(), "spinal_cord"));
                        if (ptvSpine == null)
                        {
                            ProvideUIUpdate("Failed to retrieve spinal cord structure! Cannot calculate isocenter positions! Exiting", true);
                            return allIsocenters;
                        }
                    }
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), "Retrieving PTV_Brain Structure");
                    Structure ptvBrain = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "ptv_brain"));
                    if (ptvBrain == null)
                    {
                        calcItems += 1;
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), "Failed to find PTV_Brain Structure! Retrieving brain structure");
                        ptvBrain = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "brain"));
                        if (ptvBrain == null)
                        {
                            ProvideUIUpdate("Failed to retrieve brain structure! Cannot calculate isocenter positions! Exiting", true);
                            return allIsocenters;
                        }
                    }

                    ProvideUIUpdate("Calculating anterior extent of PTV_Spine");
                    //Place field isocenters in y-direction at 2/3 the max 
                    spineYMin = (ptvSpine.MeshGeometry.Positions.Min(p => p.Y));
                    ProvideUIUpdate("Calculating superior and inferior extent of PTV_Spine");
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
                    ProvideUIUpdate($"Anterior extent of PTV_Spine: {spineYMin:0.0} mm");
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Superior extent of PTV_Spine: {spineZMax:0.0} mm");
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Inferior extent of PTV_Spine: {spineZMin:0.0} mm");

                    spineYMin *= 0.8;
                    //absolute value accounts for positive or negative y position in DCM coordinates
                    if (Math.Abs(spineYMin) < Math.Abs(ptvSpine.CenterPoint.y))
                    {
                        ProvideUIUpdate($"0.8 * PTV_Spine Ymin is more posterior than center of PTV_Spine!: {spineYMin:0.0} mm vs {ptvSpine.CenterPoint.y:0.0} mm");
                        spineYMin = ptvSpine.CenterPoint.y;
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Assigning Ant-post iso location to center of PTV_Spine: {spineYMin:0.0} mm");
                    }
                    else
                    {
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), $"0.8 * Anterior extent of PTV_spine: {spineYMin:0.0} mm");
                    }

                    //since Brain CTV = Brain and PTV = CTV + 5 mm uniform margin, center of brain is unaffected by adding the 5 mm margin if the PTV_Brain structure could not be found
                    ProvideUIUpdate($"Calculating center of PTV_Brain");
                    brainZCenter = ptvBrain.CenterPoint.z;
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Center of PTV_Brain: {brainZCenter:0.0} mm");

                    ProvideUIUpdate($"Calculating center of PTV_Brain to inf extent of PTV_Spine");
                    maxTargetLength = brainZCenter - spineZMin;
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Extent: {maxTargetLength:0.0} mm");
                }
                //The actual correct equation for calculating the isocenter separation is given below for VMAT TBI:
                //isoSeparation = Math.Round(((targetExtent - 380.0) / (numIsos - 1)) / 10.0f) * 10.0f;

                //It is calculated by setting the most superior and inferior isocenters to be 19.0 cm from the target volume edge in the z-direction. 
                //The isocenter separtion is then calculated as half the distance between these two isocenters (sep = ((max-19cm)-(min+19cm)/2).
                //NOTE THAT THIS EQUATION WILL NOT WORK FOR VMAT CSI AS IT RESULTS IN A POORLY POSITIONED UPPER SPINE ISOCENTER IF APPLICABLE
                isoSeparation = 380.0;
                
                for (int i = 0; i < numIsos; i++)
                {
                    ProvideUIUpdate($"Determining position for isocenter: {i}");
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
                        v.y = longestTargetInPlan.CenterPoint.y;
                        //assumes one isocenter if the target is not ptv_csi
                        v.z = longestTargetInPlan.CenterPoint.z;
                    }
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Calculated isocenter position {i + 1}");
                    
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), "Rounding Y- and Z-positions to nearest integer values");
                    //round z position to the nearest integer
                    v = itr.StructureSet.Image.DicomToUser(v, itr);
                    v.y = Math.Round(v.y / 10.0f) * 10.0f;
                    v.z = Math.Round(v.z / 10.0f) * 10.0f;
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Calculated isocenter position (user coordinates): ({v.x}, {v.y}, {v.z})");
                    v = itr.StructureSet.Image.UserToDicom(v, itr);
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), "Adding calculated isocenter position to stack!");
                    tmp.Add(new Tuple<VVector, string, int>(v, 
                                                            planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)).Item2.ElementAt(i).Item1, 
                                                            planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)).Item2.ElementAt(i).Item2));
                }

                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Finished retrieving isocenters for plan: {itr.Id}");
                allIsocenters.Add(Tuple.Create(itr, new List<Tuple<VVector, string, int>>(tmp)));
                count++;
            }
            return allIsocenters;
        }

        protected override bool SetBeams(Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>> iso)
        {
            ProvideUIUpdate(0, $"Preparing to set isocenters for plan: {iso.Item1.Id}");
            int calcItems = 3;
            int counter = 0;
            //DRR parameters (dummy parameters to generate DRRs for each field)
            DRRCalculationParameters DRR = new DRRCalculationParameters
            {
                DRRSize = 500.0,
                FieldOutlines = true,
                StructureOutlines = true
            };
            DRR.SetLayerParameters(1, 1.0, 100.0, 1000.0);
            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Created default DRR parameters");

            //grab all prescriptions assigned to this plan
            List<Tuple<string, string, int, DoseValue, double>> tmp = prescriptions.Where(x => string.Equals(x.Item1, iso.Item1.Id)).ToList();
            //if any of the targets for this plan are ptv_csi, then you must use the special beam placement logic for the initial plan
            if (tmp.Where(x => x.Item2.ToLower().Contains("ptv_csi")).Any())
            {
                //verify that BOTH PTV spine and PTV brain exist in the current structure set! If not, create them (used to fit the field jaws to the target
                if (selectedSS.Structures.Any(x => string.Equals(x.Id.ToLower(), "ptv_brain")))
                {
                    //uniform 5mm outer margin to create brain ptv from brain ctv/brain structure
                    if (CreateTargetStructure("PTV_Brain", "brain", new AxisAlignedMargins(StructureMarginGeometry.Outer, 5.0, 5.0, 5.0, 5.0, 5.0, 5.0))) return true;
                }
                if (selectedSS.Structures.Any(x => string.Equals(x.Id.ToLower(), "ptv_spine")))
                {
                    //ctv_spine = spinal_cord+0.5cm ANT, +1.5cm Inf, and +1.0 cm in all other directions
                    //ptv_spine = ctv_spine + 5 mm outer margin --> add 5 mm to the asymmetric margins used to create the ctv
                    if (CreateTargetStructure("PTV_Spine", "spinalcord", new AxisAlignedMargins(StructureMarginGeometry.Outer, 15.0, 10.0, 20.0, 15.0, 15.0, 15.0), "spinal_cord")) return true;
                }
                target = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "ptv_brain"));
            }
            else target = selectedSS.Structures.FirstOrDefault(x => x.Id.Contains(tmp.First().Item2));
            ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Retrieved target for plan: {target.Id}");

            if (target == null || target.IsEmpty) 
            { 
                ProvideUIUpdate(0, $"Error! Target not found or is not contoured in plan {iso.Item1.Id}! Exiting!", true); 
                return true; 
            }
            ProvideUIUpdate(100, "Preparation complete!");

            //place the beams for the VMAT plan
            int count = 0;
            string beamName;
            VRect<double> jp;
            calcItems = 0;
            counter = 0;
            for (int i = 0; i < iso.Item2.Count; i++)
            {
                calcItems += iso.Item2.ElementAt(i).Item3 * 6;
                ProvideUIUpdate(0, $"Assigning isocenter: {i + 1}");

                if (target.Id.ToLower().Contains("ptv_brain") && i > 0) target = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_spine"));
                //add beams for each isocenter
                for (int j = 0; j < iso.Item2.ElementAt(i).Item3; j++)
                {
                    Beam b;
                    beamName = $"{count + 1} ";

                    //kind of messy, but used to increment the collimator rotation one element in the array so you don't end up in a situation where the 
                    //single beam in this isocenter has the same collimator rotation as the single beam in the previous isocenter
                    if (i > 0 && iso.Item2.ElementAt(i).Item3 == 1 && iso.Item2.ElementAt(i - 1).Item3 == 1) j++;

                    jp = jawPos.ElementAt(j);
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Retrieved jaw positions (iso: {i + 1}, beam: {j + 1})");

                    double coll = collRot[j];
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Retrieved collimator positions (iso: {i + 1}, beam: {j + 1})");

                    //all even beams (e.g., 2, 4, etc.) will be CCW and all odd beams will be CW
                    if (count % 2 == 0)
                    {
                        b = iso.Item1.AddArcBeam(ebmpArc, jp, coll, CCW[0], CCW[1], GantryDirection.CounterClockwise, 0, iso.Item2.ElementAt(i).Item1);
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Added arc beam to iso: {i}");

                        if (j >= 2) beamName += $"CCW {iso.Item2.ElementAt(i).Item2}{90}";
                        else beamName += $"CCW {iso.Item2.ElementAt(i).Item2}";
                    }
                    else
                    {
                        b = iso.Item1.AddArcBeam(ebmpArc, jp, coll, CW[0], CW[1], GantryDirection.Clockwise, 0, iso.Item2.ElementAt(i).Item1);
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Added arc beam to iso: {i + 1}");

                        if (j >= 2) beamName += $"CW {iso.Item2.ElementAt(i).Item2}{90}";
                        else beamName += $"CW {iso.Item2.ElementAt(i).Item2}";
                    }
                    //auto fit collimator to target structure
                    //circular margin (in mm), target structure, use asymmetric x Jaws, use asymmetric y jaws, optimize collimator rotation
                    if (target.Id.ToLower().Contains("ptv_brain"))
                    {
                        //original (3/28/23) 30.0,40.0,30.0,30.0
                        b.FitCollimatorToStructure(new FitToStructureMargins(45.0, 40.0, 45.0, 30.0), target, true, true, false);
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Fit collimator to: {target.Id}");
                        ProvideUIUpdate($"Asymmetric margin: {4.5} cm Lat, {3.0} cm Sup, {4.0} cm Inf");
                    }
                    else
                    {
                        //original (3/28/23) 30.0
                        b.FitCollimatorToStructure(new FitToStructureMargins(45.0,30.0,45.0,30.0), target, true, true, false);
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Fit collimator to: {target.Id}");
                        ProvideUIUpdate($"Asymmetric margin: {4.5} cm Lat, {3.0} cm Sup-Inf");
                    }

                    b.Id = beamName;
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Assigned beam id: {beamName}");

                    b.CreateOrReplaceDRR(DRR);
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Assigned DRR to beam: {beamName}");

                    count++;
                }
            }
            return false;
        }

        private bool CreateTargetStructure(string targetStructureId, string baseStructureId, AxisAlignedMargins margin, string alternateBasStructureId = "")
        {
            int calcItems = 3;
            int counter = 0;
            ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Failed to find {targetStructureId} Structure! Retrieving {baseStructureId} structure");
            Structure baseStructure = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), baseStructureId.ToLower()));
            if(baseStructure == null && !string.IsNullOrEmpty(alternateBasStructureId)) baseStructure = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), alternateBasStructureId.ToLower()));
            if(baseStructure == null)
            {
                ProvideUIUpdate($"Could not retrieve base structure {baseStructureId}. Exiting!", true);
                return true;
            }
            ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Creating {targetStructureId} structure!");
            if (selectedSS.CanAddStructure("CONTROL", $"{targetStructureId}"))
            {
                Structure target = selectedSS.AddStructure("CONTROL", $"{targetStructureId}");
                target.SegmentVolume = baseStructure.AsymmetricMargin(margin);
                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Created {targetStructureId} structure!");
            }
            else
            {
                ProvideUIUpdate($"Failed to add {targetStructureId} to the structure set! Exiting!", true);
                return true;
            }
            return false;
        }
    }
}
