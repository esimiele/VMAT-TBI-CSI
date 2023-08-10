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

        public PlaceBeams_CSI(StructureSet ss, List<Tuple<string, List<Tuple<string, int>>>> planInfo, double[] coll, string linac, string energy, string calcModel, string optModel, string gpuDose, string gpuOpt, string mr, bool overlap, double overlapMargin, bool closePW)
        {
            selectedSS = ss;
            planIsoBeamInfo = new List<Tuple<string, List<Tuple<string, int>>>>(planInfo);
            collRot = coll;
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
            SetCloseOnFinish(closePW, 3000);
        }

        //to handle system access exception violation
        [HandleProcessCorruptedStateExceptions]
        public override bool Run()
        {
            try
            {
                if (CheckExistingCourse()) return true;
                if (CheckExistingPlans()) return true;
                if(CheckTSArmsAvoid())
                {
                    if (ExtendBodyContour()) return true;
                }
                if (CreateVMATPlans()) return true;
                //plan, List<isocenter position, isocenter name, number of beams per isocenter>
                List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> isoLocations = GetIsocenterPositions();
                UpdateUILabel("Assigning isocenters and beams: ");
                int isoCount = 0;
                foreach (Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>> itr in isoLocations)
                {
                    if (SetVMATBeams(itr)) return true;
                    //ensure contour overlap is requested AND there are more than two isocenters for this plan
                    if (contourOverlap && itr.Item2.Count > 1) if (ContourFieldOverlap(itr, isoCount)) return true;
                    isoCount += itr.Item2.Count;
                }
                if(CheckTSArmsAvoid())
                {
                    if (CropBodyFromArmsAvoid()) return true;
                }
                UpdateUILabel("Finished!");
                return false;
            }
            catch(Exception e)
            {
                ProvideUIUpdate($"{e.Message}", true);
                stackTraceError = e.StackTrace;
                return true;
            }
        }

        private bool CheckTSArmsAvoid()
        {
            return StructureTuningHelper.DoesStructureExistInSS("TS_ArmsAvoid", selectedSS, true);
        }

        private bool ExtendBodyContour()
        {
            int percentComplete = 0;
            int calcItems = 4;

            Structure body = StructureTuningHelper.GetStructureFromId("body", selectedSS);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved body structure: {body.Id}");
            if (selectedSS.CanAddStructure("CONTROL", "human_body"))
            {
                Structure bodyCopy = selectedSS.AddStructure("CONTROL", "human_body");
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Created structure: {bodyCopy.Id}");
                bodyCopy.SegmentVolume = body.Margin(0.0);
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Copied body structure onto {bodyCopy.Id}");
            }
            else
            {
                ProvideUIUpdate($"Error! Could not add human_body structure to compensate for TS_ArmsAvoid! Exiting!", true);
                return true;
            }

            (bool unionFail, StringBuilder unionMessage) = ContourHelper.ContourUnion(StructureTuningHelper.GetStructureFromId("TS_ArmsAvoid", selectedSS), body, 0.0);
            if (unionFail)
            {
                ProvideUIUpdate(unionMessage.ToString(), true);
                return true;
            }
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Contour union betwen between TS_ArmsAvoid and body onto body");
            return false;
        }

        private bool CropBodyFromArmsAvoid()
        {
            int percentComplete = 0;
            int calcItems = 4;
            Structure bodyCopy = StructureTuningHelper.GetStructureFromId("human_body", selectedSS);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved body copy structure: {bodyCopy.Id}");
            Structure body = StructureTuningHelper.GetStructureFromId("body", selectedSS);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved body structure: {body.Id}");

            (bool failCopyTarget, StringBuilder copyErrorMessage) = ContourHelper.CopyStructureOntoStructure(bodyCopy, body);
            if (failCopyTarget)
            {
                ProvideUIUpdate(copyErrorMessage.ToString(), true);
                return true;
            }
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Copied structure {bodyCopy.Id} onto {body.Id}");

            if(selectedSS.CanRemoveStructure(bodyCopy))
            {
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Removed {bodyCopy.Id} from structure set");
                selectedSS.RemoveStructure(bodyCopy);
            }
            else
            {
                ProvideUIUpdate($"Error! Could not remove {bodyCopy.Id} structure! Exiting!", true);
                return true;
            }
            return false;
        }

        private (bool, double) GetBrainZCenter(ref int counter, ref int calcItems)
        {
            bool fail = false;
            double brainZCenter = 0.0;
            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Retrieving Brain Structure");
            Structure ptvBrain = StructureTuningHelper.GetStructureFromId("Brain", selectedSS);
            if (ptvBrain == null || ptvBrain.IsEmpty)
            {
                calcItems += 1;
                ProvideUIUpdate((int)(100 * ++counter / calcItems), "Failed to find Brain Structure! Retrieving PTV_Brain structure");
                ptvBrain = StructureTuningHelper.GetStructureFromId("PTV_Brain", selectedSS, true);
                if (ptvBrain == null || ptvBrain.IsEmpty)
                {
                    ProvideUIUpdate("Failed to retrieve PTV_Brain structure! Cannot calculate isocenter positions! Exiting", true);
                    fail = true;
                    return (fail, brainZCenter);
                }
            }

            ProvideUIUpdate($"Calculating center of Brain");
            brainZCenter = ptvBrain.CenterPoint.z;
            ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Center of Brain: {brainZCenter:0.0} mm");
            return (fail, brainZCenter);
        }

        private double ScaleSpineYPosition(double spineYMin, double spineYCenter, double scaleFactor)
        {
            spineYMin *= scaleFactor;
            //absolute value accounts for positive or negative y position in DCM coordinates
            if (Math.Abs(spineYMin) < Math.Abs(spineYCenter))
            {
                ProvideUIUpdate($"0.8 * PTV_Spine Ymin is more posterior than center of PTV_Spine!: {spineYMin:0.0} mm vs {spineYCenter:0.0} mm");
                spineYMin = spineYCenter;
                ProvideUIUpdate($"Assigning Ant-post iso location to center of PTV_Spine: {spineYMin:0.0} mm");
            }
            else
            {
                ProvideUIUpdate($"0.8 * Anterior extent of PTV_spine: {spineYMin:0.0} mm");
            }
            return spineYMin;
        }

        private (bool, double, double, double) GetSpineYminZminZMax(ref int counter, ref int calcItems)
        {
            bool fail = false;
            double spineYMin = 0.0;
            double spineZMax = 0.0;
            double spineZMin = 0.0;
            calcItems += 5;
            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Retrieving PTV_Spine Structure");
            Structure ptvSpine = StructureTuningHelper.GetStructureFromId("PTV_Spine", selectedSS);
            if (ptvSpine == null)
            {
                calcItems += 1;
                ProvideUIUpdate((int)(100 * ++counter / calcItems), "Failed to find PTV_Spine Structure! Retrieving spinal cord structure");
                ptvSpine = StructureTuningHelper.GetStructureFromId("spinalcord", selectedSS);
                if (ptvSpine == null) ptvSpine = StructureTuningHelper.GetStructureFromId("spinal_cord", selectedSS);
                if (ptvSpine == null)
                {
                    ProvideUIUpdate("Failed to retrieve spinal cord structure! Cannot calculate isocenter positions! Exiting", true);
                    fail = true;
                    return (fail, spineYMin, spineZMin, spineZMax);
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
                spineZMin -= 15.0;
            }
            ProvideUIUpdate($"Anterior extent of PTV_Spine: {spineYMin:0.0} mm");
            ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Superior extent of PTV_Spine: {spineZMax:0.0} mm");
            ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Inferior extent of PTV_Spine: {spineZMin:0.0} mm");

            spineYMin = ScaleSpineYPosition(spineYMin, ptvSpine.CenterPoint.y, 0.8);
            ProvideUIUpdate((int)(100 * ++counter / calcItems));
            return (fail, spineYMin, spineZMin, spineZMax);
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
            foreach (ExternalPlanSetup itr in vmatPlans)
            {
                ProvideUIUpdate($"Retrieving number of isocenters for plan: {itr.Id}");
                int numIsos = planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)).Item2.Count;
                int percentComplete = 0;
                int calcItems = numIsos;
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Num isos for plan (from generateTS): {itr.Id}");

                ProvideUIUpdate($"Retrieving prescriptions for plan: {itr.Id}");
                //grab the target in this plan with the greatest z-extent (plans can now have multiple targets assigned)
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), "Retrieved Presciptions");

                ProvideUIUpdate("Determining target with greatest extent");
                (bool fail, Structure longestTargetInPlan, double maxTargetLength, StringBuilder errorMessage) = TargetsHelper.GetLongestTargetInPlan(planIdTargets.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)), selectedSS);
                if (fail)
                {
                    ProvideUIUpdate($"Error! No structure named: {errorMessage} found or contoured!", true);
                    return new List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> { };
                }
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Longest target in plan {itr.Id}: {longestTargetInPlan.Id}");

                List<Tuple<VVector, string, int>> tmp = new List<Tuple<VVector, string, int>> { };
                isoSeparation = 380.0;
                if (string.Equals(longestTargetInPlan.Id.ToLower(), "ptv_csi"))
                {
                    (bool failSpineRetrival, double spineYMin, double spineZMin, double spineZMax) = GetSpineYminZminZMax(ref percentComplete, ref calcItems);
                    if (failSpineRetrival) return allIsocenters;

                    (bool failBrainRetrival, double brainZCenter) = GetBrainZCenter(ref percentComplete, ref calcItems);
                    if (failBrainRetrival) return allIsocenters;
                    //since Brain CTV = Brain and PTV = CTV + 5 mm uniform margin, center of brain is unaffected by adding the 5 mm margin if the PTV_Brain structure could not be found
                    ProvideUIUpdate($"Calculating distance between center of PTV_Brain and inf extent of PTV_Spine");
                    maxTargetLength = brainZCenter - spineZMin;
                    ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Extent: {maxTargetLength:0.0} mm");

                    for (int i = 0; i < numIsos; i++)
                    {
                        VVector v = new VVector();
                        ProvideUIUpdate($"Determining position for isocenter: {i + 1}");
                        //special case when the main target is ptv_csi
                        //asign y position to spineYmin
                        v.y = spineYMin;
                        //assign the first isocenter to the center of the ptv_brain
                        if (i == 0) v.z = brainZCenter;
                        else
                        {
                            v.z = (spineZMin + (numIsos - i - 1) * isoSeparation + 180.0);
                            if(i == 1)
                            {
                                if (v.z + 200.0 > tmp.ElementAt(0).Item1.z) v.z = tmp.ElementAt(0).Item1.z - 200.0;
                            }
                        }
                        
                        ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Calculated isocenter position {i + 1}");
                        tmp.Add(new Tuple<VVector, string, int>(RoundIsocenterPositions(v, itr, ref percentComplete, ref calcItems),
                                                                planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)).Item2.ElementAt(i).Item1,
                                                                planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)).Item2.ElementAt(i).Item2));
                    }
                }
                else
                {
                    //assumes only one isocenter position for the plan (assuming it's the boost plan)
                    ProvideUIUpdate($"Determining position for isocenter: {1}");
                    VVector v = new VVector
                    {
                        x = userOrigin.x,
                        //assign y isocenter position to the center of the target
                        y = longestTargetInPlan.CenterPoint.y,
                        //assumes one isocenter if the target is not ptv_csi
                        z = longestTargetInPlan.CenterPoint.z
                    };

                    ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Calculated isocenter position {1}");
                    tmp.Add(new Tuple<VVector, string, int>(RoundIsocenterPositions(v, itr, ref percentComplete, ref calcItems),
                                                            planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)).Item2.ElementAt(0).Item1,
                                                            planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)).Item2.ElementAt(0).Item2));
                }

                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Finished retrieving isocenters for plan: {itr.Id}");
                allIsocenters.Add(Tuple.Create(itr, new List<Tuple<VVector, string, int>>(tmp)));
                count++;
            }
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return allIsocenters;
        }

        protected override bool SetVMATBeams(Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>> iso)
        {
            ProvideUIUpdate(0, $"Preparing to set isocenters for plan: {iso.Item1.Id}");
            int percentComplete = 0;
            int calcItems = 3;
            bool initCSIPlan = false;
            //if the plan id is equal to the plan Id in the first entry in the prescriptions, then this is the initial plan --> use special rules to fit fields
            if (string.Equals(iso.Item1.Id, prescriptions.First().Item1)) initCSIPlan = true;
            //DRR parameters (dummy parameters to generate DRRs for each field)
            DRRCalculationParameters DRR = GenerateDRRParameters();
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), "Created default DRR parameters");

            List<Tuple<string, List<string>>> planIdTargets = new List<Tuple<string, List<string>>>(TargetsHelper.GetTargetListForEachPlan(prescriptions));
            ProvideUIUpdate("Determining target with greatest extent");
            (bool fail, Structure longestTargetInPlan, double maxTargetLength, StringBuilder errorMessage) = TargetsHelper.GetLongestTargetInPlan(planIdTargets.FirstOrDefault(x => string.Equals(x.Item1, iso.Item1.Id)), selectedSS);
            if (fail)
            {
                ProvideUIUpdate($"Error! No structure named: {errorMessage} found or contoured!", true);
            }
            Structure target = longestTargetInPlan;

            if (target == null || target.IsEmpty) 
            { 
                ProvideUIUpdate(0, $"Error! Target not found or is not contoured in plan {iso.Item1.Id}! Exiting!", true); 
                return true; 
            }
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Longest target in plan {iso.Item1.Id}: {longestTargetInPlan.Id}");
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved target for plan: {iso.Item1.Id}");
            ProvideUIUpdate(100, "Preparation complete!");

            //place the beams for the VMAT plan
            int count = 0;
            string beamName;
            calcItems = 0;
            percentComplete = 0;
            //iso counter
            for (int i = 0; i < iso.Item2.Count; i++)
            {
                calcItems += iso.Item2.ElementAt(i).Item3 * 5;
                ProvideUIUpdate(0, $"Assigning isocenter: {i + 1}");

                //beam counter
                for (int j = 0; j < iso.Item2.ElementAt(i).Item3; j++)
                {
                    Beam b;
                    beamName = $"{count + 1} ";

                    //kind of messy, but used to increment the collimator rotation one element in the array so you don't end up in a situation where the 
                    //single beam in this isocenter has the same collimator rotation as the single beam in the previous isocenter
                    if (i > 0 && iso.Item2.ElementAt(i).Item3 == 1 && iso.Item2.ElementAt(i - 1).Item3 == 1) j++;

                    (bool result, VRect<double> jaws) = GetXYJawPositionsForStructure(initCSIPlan, i == 0, iso.Item2.ElementAt(i).Item1, new FitToStructureMargins(30.0, 40.0, 30.0, 30.0), target);
                    ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Jaw positions fit to target: {target.Id} (iso: {i + 1}, beam: {j + 1})");
                    if(!result)
                    {
                        ProvideUIUpdate($"Calculated jaw positions:");
                        ProvideUIUpdate($"x1: {jaws.X1:0.0}");
                        ProvideUIUpdate($"x2: {jaws.X2:0.0}");
                        ProvideUIUpdate($"y1: {jaws.Y1:0.0}");
                        ProvideUIUpdate($"y2: {jaws.Y2:0.0}");
                    }

                    double coll = collRot[j];
                    ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved collimator positions (iso: {i + 1}, beam: {j + 1})");

                    //all even beams (e.g., 2, 4, etc.) will be CCW and all odd beams will be CW
                    if (count % 2 == 0)
                    {
                        b = iso.Item1.AddArcBeam(ebmpArc, jaws, coll, CCW[0], CCW[1], GantryDirection.CounterClockwise, 0, iso.Item2.ElementAt(i).Item1);
                        ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Added arc beam to iso: {i + 1}");

                        if (j >= 2) beamName += $"CCW {iso.Item2.ElementAt(i).Item2}{90}";
                        else beamName += $"CCW {iso.Item2.ElementAt(i).Item2}";
                    }
                    else
                    {
                        b = iso.Item1.AddArcBeam(ebmpArc, jaws, coll, CW[0], CW[1], GantryDirection.Clockwise, 0, iso.Item2.ElementAt(i).Item1);
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

        private (bool, VRect<double>) GetXYJawPositionsForStructure(bool isInitCSIPlan, bool isFirstIso, VVector iso, FitToStructureMargins margins, Structure target = null)
        {
            ProvideUIUpdate("Fitting jaws to target");
            double x1, y1, x2, y2;
            x1 = x2 = y1 = y2 = 0.0;
            if (isInitCSIPlan)
            {
                double startZ, stopZ;
                ProvideUIUpdate("Initial CSI plan!");
                if (isFirstIso)
                {
                    ProvideUIUpdate("First isocenter in initial CSI plan!");
                    //first isocenter in brain
                    Structure brain = StructureTuningHelper.GetStructureFromId("PTV_Brain", selectedSS);
                    if (brain == null || brain.IsEmpty) return (true, new VRect<double>());
                    y1 = brain.MeshGeometry.Positions.Min(p => p.Z) - iso.z - margins.Y1;
                    y2 = brain.MeshGeometry.Positions.Max(p => p.Z) - iso.z + margins.Y2;
                    startZ = brain.MeshGeometry.Positions.Min(p => p.Z);
                    stopZ = brain.MeshGeometry.Positions.Max(p => p.Z);
                    ProvideUIUpdate($"Start position: {startZ} mm");
                    ProvideUIUpdate($"Stop position: {stopZ} mm");
                }
                else
                {
                    ProvideUIUpdate("Spine isocenter(s) in initial CSI plan!");
                    Structure spine = StructureTuningHelper.GetStructureFromId("PTV_Spine", selectedSS);
                    if (spine == null || spine.IsEmpty) return (true, new VRect<double>());
                    y2 = spine.MeshGeometry.Positions.Max(p => p.Z) - iso.z + margins.Y2;
                    if (y2 > 200.0) y2 = 200.0;
                    y1 = spine.MeshGeometry.Positions.Min(p => p.Z) - iso.z - margins.Y1;
                    if (y1 < -200.0) y1 = -200.0;
                    startZ = iso.z - Math.Abs(y1);
                    //need this min comparison to ensure the max spine position isn't always used for stopZ
                    stopZ = Math.Min(iso.z + y2, spine.MeshGeometry.Positions.Max(p => p.Z));
                    ProvideUIUpdate($"Start position: {startZ} mm");
                    ProvideUIUpdate($"Stop position: {stopZ} mm");
                }
                Structure ptv_csi = StructureTuningHelper.GetStructureFromId("PTV_CSI", selectedSS);
                if (ptv_csi == null || ptv_csi.IsEmpty) return (true, new VRect<double>());
                (double latProjection, StringBuilder message) = ContourHelper.GetMaxLatProjectionDistance(GetLateralStructureBoundingBox(ptv_csi, startZ, stopZ), iso);
                ProvideUIUpdate(message.ToString());
                x2 = latProjection + margins.X2;
                x1 = -x2;
            }
            else
            {
                if(target == null || target.IsEmpty) return (true, new VRect<double>());
                (double latProjection, StringBuilder message) = ContourHelper.GetMaxLatProjectionDistance(target, iso);
                ProvideUIUpdate(message.ToString());
                x2 = latProjection + margins.X2;
                x1 = -x2;
                y2 = target.MeshGeometry.Positions.Max(p => p.Z) - iso.z + margins.Y2;
                if (y2 > 200.0) y2 = 200.0;
                y1 = target.MeshGeometry.Positions.Min(p => p.Z) - iso.z - margins.Y1;
                if (y1 < -200.0) y1 = -200.0;
            }
            return (false, new VRect<double> (x1, y1, x2, y2));
        }

        private VVector[] GetLateralStructureBoundingBox(Structure target, double zMin, double zMax) 
        {
            MeshGeometry3D mesh = target.MeshGeometry;
            //get most inferior slice of ptv_csi (mesgeometry.bounds.z indicates the most inferior part of a structure)
            int startSlice = CalculationHelper.ComputeSlice(zMin, selectedSS);
            //only go to the most superior part of the lungs for contouring the arms
            int stopSlice = CalculationHelper.ComputeSlice(zMax, selectedSS);
            int percentComplete = 0;
            int calcItems = stopSlice - startSlice + 1; 
            ProvideUIUpdate($"Start slice: {startSlice}");
            ProvideUIUpdate($"Stop slice: {stopSlice}");
            VVector[][] pts;
            double xMax, xMin, yMax, yMin;
            xMax = -500000000000.0;
            xMin = 500000000000.0;
            yMax = -500000000000.0;
            yMin = 500000000000.0; 
            for (int slice = startSlice; slice <= stopSlice; slice++)
            {
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems));
                //get body contour points
                pts = target.GetContoursOnImagePlane(slice);
                
                //find min and max x positions for the body on this slice (so we can adapt the box positions for each slice)
                for (int i = 0; i < pts.GetLength(0); i++)
                {
                    if (pts[i].Max(p => p.x) > xMax) xMax = pts[i].Max(p => p.x);
                    if (pts[i].Min(p => p.x) < xMin) xMin = pts[i].Min(p => p.x);
                    if (pts[i].Max(p => p.y) > yMax) yMax = pts[i].Max(p => p.y);
                    if (pts[i].Min(p => p.y) < yMin) yMin = pts[i].Min(p => p.y);
                }
            }
            VVector[] boundinBox = new[] {
                                           new VVector(xMax, yMax, 0),
                                           new VVector(xMax, yMin, 0),
                                           new VVector(xMin, yMax, 0),
                                           new VVector(xMin, yMin, 0)};

            ProvideUIUpdate($"xMax: {xMax:0.0} mm, xMin: {xMin:0.0} mm, yMax: {yMax:0.0} mm, yMin: {yMin:0.0} mm");
            return boundinBox;
        }
    }
}
