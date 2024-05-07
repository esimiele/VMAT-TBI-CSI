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

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="ss"></param>
        /// <param name="planInfo"></param>
        /// <param name="coll"></param>
        /// <param name="linac"></param>
        /// <param name="energy"></param>
        /// <param name="calcModel"></param>
        /// <param name="optModel"></param>
        /// <param name="gpuDose"></param>
        /// <param name="gpuOpt"></param>
        /// <param name="mr"></param>
        /// <param name="overlap"></param>
        /// <param name="overlapMargin"></param>
        /// <param name="closePW"></param>
        public PlaceBeams_CSI(StructureSet ss, List<Tuple<string, List<Tuple<string, int>>>> planInfo, double[] coll, 
                              string linac, string energy, string calcModel, string optModel, string gpuDose, string gpuOpt, 
                              string mr, bool overlap, double overlapMargin, bool closePW)
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

        /// <summary>
        /// Run control
        /// </summary>
        /// <returns></returns>
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

        #region Manage TS_ArmsAvoid
        /// <summary>
        /// Simple helper method to check if TS_ArmsAvoid is present and contoured in the structure set. Looks cleaner to use this method
        /// rather than put the return method in the run control
        /// </summary>
        /// <returns></returns>
        private bool CheckTSArmsAvoid()
        {
            return StructureTuningHelper.DoesStructureExistInSS("TS_ArmsAvoid", selectedSS, true);
        }

        /// <summary>
        /// Method to retrieve and extend the body contour to encompass TS_ArmsAvoid. Necessary to ensure the dose calculation grid encompasses
        /// TS_ArmsAvoid otherwise the optimization loop will run into faults
        /// </summary>
        /// <returns></returns>
        private bool ExtendBodyContour()
        {
            int percentComplete = 0;
            int calcItems = 4;

            Structure body = StructureTuningHelper.GetStructureFromId("body", selectedSS);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved body structure: {body.Id}");
            if (selectedSS.CanAddStructure("CONTROL", "human_body"))
            {
                //Create a copy of the current body structure for later
                Structure bodyCopy = selectedSS.AddStructure("CONTROL", "human_body");
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Created structure: {bodyCopy.Id}");
                bodyCopy.SegmentVolume = body.Margin(0.0);
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Copied body structure onto {bodyCopy.Id}");
            }
            else
            {
                ProvideUIUpdate($"Error! Could not add human_body structure to compensate for TS_ArmsAvoid! Exiting!", true);
                return true;
            }

            //union TS_ArmsAvoid and body onto the body structure
            (bool unionFail, StringBuilder unionMessage) = ContourHelper.ContourUnion(StructureTuningHelper.GetStructureFromId("TS_ArmsAvoid", selectedSS), body, 0.0);
            if (unionFail)
            {
                ProvideUIUpdate(unionMessage.ToString(), true);
                return true;
            }
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Contour union betwen between TS_ArmsAvoid and body onto body");
            return false;
        }

        /// <summary>
        /// The reverse of the ExtendBodyContour method. Retrieve the created body copy structure and copy it onto the body, then remove the body
        /// copy structure
        /// </summary>
        /// <returns></returns>
        private bool CropBodyFromArmsAvoid()
        {
            int percentComplete = 0;
            int calcItems = 4;
            Structure bodyCopy = StructureTuningHelper.GetStructureFromId("human_body", selectedSS);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved body copy structure: {bodyCopy.Id}");
            Structure body = StructureTuningHelper.GetStructureFromId("body", selectedSS);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved body structure: {body.Id}");

            (bool failCopyTarget, StringBuilder copyErrorMessage) = ContourHelper.CopyStructureOntoStructure(bodyCopy, body);
            if (failCopyTarget)
            {
                ProvideUIUpdate(copyErrorMessage.ToString(), true);
                return true;
            }
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Copied structure {bodyCopy.Id} onto {body.Id}");

            if(selectedSS.CanRemoveStructure(bodyCopy))
            {
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Removed {bodyCopy.Id} from structure set");
                selectedSS.RemoveStructure(bodyCopy);
            }
            else
            {
                ProvideUIUpdate($"Error! Could not remove {bodyCopy.Id} structure! Exiting!", true);
                return true;
            }
            return false;
        }
        #endregion

        #region Isocenter position calculation
        /// <summary>
        /// Helper method to Retrieve the center of the brain/PTV_Brain structure
        /// </summary>
        /// <param name="counter"></param>
        /// <param name="calcItems"></param>
        /// <returns></returns>
        private (bool, double) GetBrainZCenter(ref int counter, ref int calcItems)
        {
            bool fail = false;
            double brainZCenter = 0.0;
            ProvideUIUpdate(100 * ++counter / calcItems, "Retrieving Brain Structure");
            //shouldn't matter if its brain or PTV_brain (ideally would be the same, but the center points should be within 5mm of each other)
            Structure ptvBrain = StructureTuningHelper.GetStructureFromId("Brain", selectedSS);
            if (ptvBrain == null || ptvBrain.IsEmpty)
            {
                calcItems += 1;
                ProvideUIUpdate(100 * ++counter / calcItems, "Failed to find Brain Structure! Retrieving PTV_Brain structure");
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
            ProvideUIUpdate(100 * ++counter / calcItems, $"Center of Brain: {brainZCenter:0.0} mm");
            return (fail, brainZCenter);
        }


        /// <summary>
        /// Helper method to scale the Y isocenter position to be 80% of the anterior-most position of the spince
        /// </summary>
        /// <param name="spineYMin"></param>
        /// <param name="spineYCenter"></param>
        /// <param name="minYBound"></param>
        /// <param name="scaleFactor"></param>
        /// <returns></returns>
        private double ScaleSpineYPosition(double spineYMin, double spineYCenter, double minYBound, double scaleFactor)
        {
            spineYMin *= scaleFactor;
            //absolute value accounts for positive or negative y position in DCM coordinates
            if (Math.Abs(spineYMin) < Math.Abs(spineYCenter))
            {
                ProvideUIUpdate($"{scaleFactor} * PTV_Spine Ymin is more posterior than center of PTV_Spine!: {spineYMin:0.0} mm vs {spineYCenter:0.0} mm");
                spineYMin = spineYCenter;
                ProvideUIUpdate($"Assigning Ant-post iso location to center of PTV_Spine: {spineYMin:0.0} mm");
            }
            else if(Math.Abs(spineYMin) > Math.Abs(minYBound))
            {
                ProvideUIUpdate($"{scaleFactor} * PTV_Spine Ymin is more anterior than spinal cord Ymin with 20 mm margin!: {spineYMin:0.0} mm vs {minYBound:0.0} mm");
                spineYMin = scaleFactor * minYBound;
                ProvideUIUpdate($"Assigning Ant-post iso location to {scaleFactor} * {minYBound: 0.0}: {spineYMin:0.0} mm");
            }
            else
            {
                ProvideUIUpdate($"{scaleFactor} * Anterior extent of PTV_spine: {spineYMin:0.0} mm");
            }
            return spineYMin;
        }

        /// <summary>
        /// Helper method to retrieve the min Y, min Z, and max Z positions from PTV_Spine or the spinalcord
        /// </summary>
        /// <param name="counter"></param>
        /// <param name="calcItems"></param>
        /// <returns></returns>
        private (bool, double, double, double) GetSpineYminZminZMax(ref int counter, ref int calcItems)
        {
            double spineYMin = 0.0;
            double spineZMax = 0.0;
            double spineZMin = 0.0;
            calcItems += 5;

            if (!StructureTuningHelper.DoesStructureExistInSS("PTV_Spine", selectedSS, true) || !StructureTuningHelper.DoesStructureExistInSS("SpinalCord", selectedSS, true))
            {
                ProvideUIUpdate("Error! Either PTV_Spine or SpinalCord structure are missing or empty! Correct and try again!", true);
                return (true, spineYMin, spineZMin, spineZMax);
            }
            ProvideUIUpdate(100 * ++counter / calcItems, "Retrieving PTV_Spine Structure");
            Structure ptvSpine = StructureTuningHelper.GetStructureFromId("PTV_Spine", selectedSS);
            Structure spine = StructureTuningHelper.GetStructureFromId("SpinalCord", selectedSS);

            ProvideUIUpdate("Calculating anterior extent of PTV_Spine");
            spineYMin = ptvSpine.MeshGeometry.Positions.Min(p => p.Y);
            ProvideUIUpdate("Calculating superior and inferior extent of PTV_Spine");
            spineZMax = ptvSpine.MeshGeometry.Positions.Max(p => p.Z);
            spineZMin = ptvSpine.MeshGeometry.Positions.Min(p => p.Z);
            if (!ptvSpine.Id.ToLower().Contains("ptv"))
            {
                ProvideUIUpdate("Adding 5 mm anterior margin to spinal cord structure to mimic anterior extent of PTV_Spine!");
                spineYMin += 10;
                ProvideUIUpdate("Adding 10 mm superior margin to spinal cord structure to mimic superior extent of PTV_Spine!");
                spineZMax += 15.0;
                ProvideUIUpdate("Adding 15 mm inferior margin to spinal cord structure to mimic inferior extent of PTV_Spine!");
                spineZMin -= 20.0;
            }
            ProvideUIUpdate($"Anterior extent of PTV_Spine: {spineYMin:0.0} mm");
            ProvideUIUpdate(100 * ++counter / calcItems, $"Superior extent of PTV_Spine: {spineZMax:0.0} mm");
            ProvideUIUpdate(100 * ++counter / calcItems, $"Inferior extent of PTV_Spine: {spineZMin:0.0} mm");

            double minYBound = spine.MeshGeometry.Positions.Min(p => p.Y) - 20.0;
            ProvideUIUpdate($"Minimum Y bound for isocenter placement set to anterior extent of spinal cord + 20 mm margin: {minYBound:0.0} mm");

            //Rescale Ymin (anterior-most position) to 80% of it's value for iso placement
            spineYMin = ScaleSpineYPosition(spineYMin, ptvSpine.CenterPoint.y, minYBound,  0.8);
            ProvideUIUpdate(100 * ++counter / calcItems);
            return (false, spineYMin, spineZMin, spineZMax);
        }

        /// <summary>
        /// Calculate the necessary isocenter positions for all VMAT plans
        /// </summary>
        /// <returns></returns>
        protected override List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> GetIsocenterPositions()
        {
            UpdateUILabel("Calculating isocenter positions: ");
            ProvideUIUpdate(0, "Extracting isocenter positions for all plans");
            //external beam plan, list<iso position, iso name, number of beams for iso>
            List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> allIsocenters = new List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> { };
            Image image = selectedSS.Image;
            VVector userOrigin = image.UserOrigin;
            ProvideUIUpdate($"Retrieved user origin position: ({userOrigin.x:0.0}, {userOrigin.y:0.0}, {userOrigin.z:0.0}) mm");
            List<Tuple<string, List<string>>> planIdTargets = new List<Tuple<string, List<string>>>(TargetsHelper.GetTargetListForEachPlan(prescriptions));
            foreach (ExternalPlanSetup itr in vmatPlans)
            {
                ProvideUIUpdate($"Retrieving number of isocenters for plan: {itr.Id}");
                int numIsos = planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)).Item2.Count;
                int percentComplete = 0;
                int calcItems = numIsos;
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Num isos for plan (from generateTS): {itr.Id}");

                ProvideUIUpdate($"Retrieving prescriptions for plan: {itr.Id}");
                //grab the target in this plan with the greatest z-extent (plans can now have multiple targets assigned)
                ProvideUIUpdate(100 * ++percentComplete / calcItems, "Retrieved Presciptions");

                ProvideUIUpdate("Determining target with greatest extent");
                (bool fail, Structure longestTargetInPlan, double maxTargetLength, StringBuilder errorMessage) = TargetsHelper.GetLongestTargetInPlan(planIdTargets.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)), selectedSS);
                if (fail)
                {
                    ProvideUIUpdate($"Error! No structure named: {errorMessage} found or contoured!", true);
                    return new List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> { };
                }
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Longest target in plan {itr.Id}: {longestTargetInPlan.Id}");

                //iso position, iso name, number of beams for iso
                List<Tuple<VVector, string, int>> tmp = new List<Tuple<VVector, string, int>> { };
                if (string.Equals(longestTargetInPlan.Id.ToLower(), "ptv_csi"))
                {
                    (bool failPTVCSIIso, List<Tuple<VVector, string, int>> isos) = CalculateIsoPositionsForCSIInit(itr, numIsos, userOrigin.x);
                    if(failPTVCSIIso)
                    {
                        return new List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> { };
                    }
                    tmp.AddRange(isos);
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

                    ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Calculated isocenter position {1}");
                    tmp.Add(new Tuple<VVector, string, int>(RoundIsocenterPositions(v, itr),
                                                            planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)).Item2.ElementAt(0).Item1,
                                                            planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, itr.Id)).Item2.ElementAt(0).Item2));
                }

                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Finished retrieving isocenters for plan: {itr.Id}");
                allIsocenters.Add(Tuple.Create(itr, new List<Tuple<VVector, string, int>>(tmp)));
            }
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return allIsocenters;
        }

        /// <summary>
        /// Helper method to calculate the isocenter positions for the initial CSI plan (PTV_CSI target)
        /// </summary>
        /// <param name="thePlan"></param>
        /// <param name="numIsos"></param>
        /// <param name="xUserOrigin"></param>
        /// <returns></returns>
        private (bool, List<Tuple<VVector, string, int>>) CalculateIsoPositionsForCSIInit(ExternalPlanSetup thePlan, int numIsos, double xUserOrigin)
        {
            int percentComplete = 0;
            int calcItems = numIsos;
            List<Tuple<VVector, string, int>> tmp = new List<Tuple<VVector, string, int>> { };
            //special case when the main target is ptv_csi
            (bool failSpineRetrival, double spineYMin, double spineZMin, double spineZMax) = GetSpineYminZminZMax(ref percentComplete, ref calcItems);
            if (failSpineRetrival) return (true, tmp);

            (bool failBrainRetrival, double brainZCenter) = GetBrainZCenter(ref percentComplete, ref calcItems);
            if (failBrainRetrival) return (true, tmp);

            //maximum separation between isocenters (35 cm for 5 cm overlap). May remove this since it's not used with the updated
            //iso placement algorithm
            isoSeparation = 350.0;
            for (int i = 0; i < numIsos; i++)
            {
                VVector v = new VVector();
                ProvideUIUpdate($"Determining position for isocenter: {i + 1}");
                //asign x position to user origin x position
                v.x = xUserOrigin;
                //asign y position to spineYmin
                v.y = spineYMin;
                //assign the first isocenter to the center of the ptv_brain
                if (i == 0) v.z = brainZCenter;
                else
                {
                    v.z = spineZMin + (numIsos - i - 1) * isoSeparation + 180.0;
                    if (i == 1 && numIsos > 2)
                    {
                        //if this is iso 2 and the total number of isos is 3, apply this special logic to balance field
                        //overlap between brain iso and lower spine iso
                        //inf field superior edge.ptv spine + 18 cm = iso position + 20 cm Y field extent)
                        double infIsoFieldSupEdge = spineZMin + 180.0 + 200.0;
                        Structure brainTarget = StructureTuningHelper.GetStructureFromId("PTV_Brain", selectedSS);
                        //brain field inferior extent (ptv brain inf extent - 5 cm margin)
                        double supFieldInfExtent = brainTarget.MeshGeometry.Positions.Min(p => p.Z) - 50.0;
                        //place the iso at the midpoint between the brain field inf extent and low spine file sup extent
                        v.z = CalculationHelper.ComputeAverage(infIsoFieldSupEdge, supFieldInfExtent);
                        // Check to ensure calculated iso position is not too close to brain iso, If so, push it inf
                        if (v.z + 200.0 > tmp.ElementAt(0).Item1.z) v.z = tmp.ElementAt(0).Item1.z - 200.0;
                    }
                }

                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Calculated isocenter position {i + 1}");
                ProvideUIUpdate($"Isocenter position: ({v.x:0.0}, {v.y:0.0}, {v.z:0.0}) mm");
                tmp.Add(new Tuple<VVector, string, int>(RoundIsocenterPositions(v, thePlan),
                                                        planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, thePlan.Id)).Item2.ElementAt(i).Item1,
                                                        planIsoBeamInfo.FirstOrDefault(x => string.Equals(x.Item1, thePlan.Id)).Item2.ElementAt(i).Item2));
            }
            return (false, tmp);
        }
        #endregion

        #region Beam placement
        /// <summary>
        /// Utility method to generate and place the beams for each vmat iso for this plan
        /// </summary>
        /// <param name="iso"></param>
        /// <returns></returns>
        protected override bool SetVMATBeams(Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>> iso)
        {
            ProvideUIUpdate(0, $"Preparing to set isocenters for plan: {iso.Item1.Id}");
            int percentComplete = 0;
            int calcItems = 3;
            bool initCSIPlan = false;
            //if the plan id is equal to the plan Id in the first entry in the prescriptions, then this is the initial plan
            //--> use special rules to fit fields
            if (string.Equals(iso.Item1.Id, prescriptions.First().PlanId)) initCSIPlan = true;
            //DRR parameters (dummy parameters to generate DRRs for each field)
            DRRCalculationParameters DRR = GenerateDRRParameters();
            ProvideUIUpdate(100 * ++percentComplete / calcItems, "Created default DRR parameters");

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
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Longest target in plan {iso.Item1.Id}: {longestTargetInPlan.Id}");
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved target for plan: {iso.Item1.Id}");
            ProvideUIUpdate(100, "Preparation complete!");

            //place the beams for the VMAT plan
            int numIsos = iso.Item2.Count;
            int count = 0;
            string beamName;
            calcItems = 0;
            percentComplete = 0;
            //iso counter
            for (int i = 0; i < numIsos; i++)
            {
                calcItems += iso.Item2.ElementAt(i).Item3 * 5;
                ProvideUIUpdate(0, $"Assigning isocenter: {i + 1}");

                //beam counter
                for (int j = 0; j < iso.Item2.ElementAt(i).Item3; j++)
                {
                    Beam b;
                    beamName = $"{count + 1} ";
                    VRect<double> jaws;
                    //kind of messy, but used to increment the collimator rotation one element in the array so you don't end up in a situation where the 
                    //single beam in this isocenter has the same collimator rotation as the single beam in the previous isocenter
                    if (i > 0 && iso.Item2.ElementAt(i).Item3 == 1 && iso.Item2.ElementAt(i - 1).Item3 == 1) j++;

                    if(initCSIPlan)
                    {
                        (bool, VRect<double>) proposedJaws = GetXYJawPositionsForPTVCSI(i == 0, iso.Item2.ElementAt(i).Item1, new FitToStructureMargins(30.0, 30.0, 30.0, 30.0), numIsos);
                        if (proposedJaws.Item1) return true;
                        jaws = proposedJaws.Item2;
                    }
                    else
                    {
                        (bool, VRect<double>) proposedJaws = GetXYJawPositionsForStructure(iso.Item2.ElementAt(i).Item1, new FitToStructureMargins(30.0, 30.0, 30.0, 30.0), target);
                        if (proposedJaws.Item1) return true;
                        jaws = proposedJaws.Item2;
                    }
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Jaw positions fit to target: {target.Id} (iso: {i + 1}, beam: {j + 1})");
                    ProvideUIUpdate($"Calculated jaw positions:");
                    ProvideUIUpdate($"x1: {jaws.X1:0.0}");
                    ProvideUIUpdate($"x2: {jaws.X2:0.0}");
                    ProvideUIUpdate($"y1: {jaws.Y1:0.0}");
                    ProvideUIUpdate($"y2: {jaws.Y2:0.0}");

                    double coll = collRot[j];
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved collimator positions (iso: {i + 1}, beam: {j + 1})");

                    //all even beams (e.g., 2, 4, etc.) will be CCW and all odd beams will be CW
                    if (count % 2 == 0)
                    {
                        b = iso.Item1.AddArcBeam(ebmpArc, jaws, coll, CCW[0], CCW[1], GantryDirection.CounterClockwise, 0, iso.Item2.ElementAt(i).Item1);
                        ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Added arc beam to iso: {i + 1}");

                        if (j >= 2) beamName += $"CCW {iso.Item2.ElementAt(i).Item2}{90}";
                        else beamName += $"CCW {iso.Item2.ElementAt(i).Item2}";
                    }
                    else
                    {
                        b = iso.Item1.AddArcBeam(ebmpArc, jaws, coll, CW[0], CW[1], GantryDirection.Clockwise, 0, iso.Item2.ElementAt(i).Item1);
                        ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Added arc beam to iso: {i + 1}");

                        if (j >= 2) beamName += $"CW {iso.Item2.ElementAt(i).Item2}{90}";
                        else beamName += $"CW {iso.Item2.ElementAt(i).Item2}";
                    }

                    b.Id = beamName;
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Assigned beam id: {beamName}");

                    b.CreateOrReplaceDRR(DRR);
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Assigned DRR to beam: {beamName}");

                    count++;
                }
            }
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        /// <summary>
        /// Helper method for calculating the appropriate jaw positions for the fields that will be used in the initial CSI plan (PTV_CSI is target)
        /// </summary>
        /// <param name="isFirstIso"></param>
        /// <param name="iso"></param>
        /// <param name="margins"></param>
        /// <param name="numIsos"></param>
        /// <returns></returns>
        private (bool, VRect<double>) GetXYJawPositionsForPTVCSI(bool isFirstIso, VVector iso, FitToStructureMargins margins, int numIsos)
        {
            ProvideUIUpdate("Fitting jaws to target");
            double x1, y1, x2, y2;
            x1 = x2 = y1 = y2 = 0.0;
            double startZ, stopZ;
            ProvideUIUpdate("Initial CSI plan!");
            if (isFirstIso)
            {
                ProvideUIUpdate("First isocenter in initial CSI plan!");
                //first isocenter in brain
                Structure brain = StructureTuningHelper.GetStructureFromId("PTV_Brain", selectedSS);
                if (brain == null || brain.IsEmpty)
                {
                    ProvideUIUpdate("Error! Could not retrieve brain target structure! Exiting", true);
                    return (true, new VRect<double>());
                }
                double InfMargin = margins.Y1 + 20.0;
                ProvideUIUpdate($"Adjusting inf field margin to >= 5cm");
                
                if (StructureTuningHelper.DoesStructureExistInSS("larynx", selectedSS, true))
                {
                    Structure larynx = StructureTuningHelper.GetStructureFromId("larynx", selectedSS);
                    ProvideUIUpdate($"Retrieved Larynx structure");
                    ProvideUIUpdate($"Larynx z min: {larynx.MeshGeometry.Positions.Min(p => p.Z):0.0} mm");
                    InfMargin = Math.Max(InfMargin,brain.MeshGeometry.Positions.Min(p => p.Z) - larynx.MeshGeometry.Positions.Min(p => p.Z));
                    ProvideUIUpdate($"Max of current inf margin and larynx z min: {InfMargin:0.0} cm");
                }
                else if (StructureTuningHelper.DoesStructureExistInSS("thyroid", selectedSS, true))
                {
                    Structure thyroid = StructureTuningHelper.GetStructureFromId("thyroid", selectedSS);
                    ProvideUIUpdate($"Retrieved Thyroid structure");
                    ProvideUIUpdate($"Larynx z min: {thyroid.MeshGeometry.Positions.Min(p => p.Z):0.0} mm");
                    InfMargin = Math.Max(InfMargin, brain.MeshGeometry.Positions.Min(p => p.Z) - thyroid.CenterPoint.z);
                    ProvideUIUpdate($"Max of current inf margin and thyroid z min: {InfMargin:0.0} cm");
                }
                else ProvideUIUpdate($"Could not find/retrieve Larynx or Thyroid structures. Setting inf margin to: {InfMargin:0.0} mm");
                if (numIsos == 2)
                {
                    ProvideUIUpdate("Only two isocenters in plan. Need to verify there will be adequate overlap between brain and spine fields");
                    //special logic for spine iso in case there are only two isos, and the spine iso field extent does not cover the entire PTV_spine
                    //in this case, we need to verify the inf margin placed on the brain fields to ensure at least 5cm of overlap
                    Structure spine = StructureTuningHelper.GetStructureFromId("PTV_Spine", selectedSS);
                    double infSpineIso = spine.MeshGeometry.Positions.Min(p => p.Z) + 180.0;
                    ProvideUIUpdate($"Inferior spine location: {infSpineIso:0.0} mm");
                    if (iso.z - infSpineIso >= 200.0)
                    {
                        ProvideUIUpdate($"Separation between brain and spines isos is >= 20cm: {iso.z - infSpineIso:0.0} mm");
                        //spine iso field should have y2 maxed at 20 cm. spatial location of that field extent
                        double infSpineIsoSupFieldExtent = infSpineIso + 200;
                        //take the max margin of the existing calculated margin and the difference between ptv_brain z min
                        //and inf spine iso field extent + 5 cm overlap
                        InfMargin = Math.Max(InfMargin, brain.MeshGeometry.Positions.Min(p => p.Z) - infSpineIsoSupFieldExtent + 50.0);
                        ProvideUIUpdate($"Max of current inf margin and (brain zmin - spineIsoFieldExtent + 5cm): {InfMargin:0.0} cm");
                    }
                }
                ProvideUIUpdate($"Updated brain field inf margin to: {InfMargin:0.0} mm");

                y1 = brain.MeshGeometry.Positions.Min(p => p.Z) - iso.z - InfMargin;
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
                y1 = spine.MeshGeometry.Positions.Min(p => p.Z) - iso.z - margins.Y1;
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
            return (false, VerifyProposedJawPositions(x1, y1, x2, y2));
        }

        /// <summary>
        /// Helper method to compute the jaw positions for the specified target using the specified margins
        /// </summary>
        /// <param name="iso"></param>
        /// <param name="margins"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        private (bool, VRect<double>) GetXYJawPositionsForStructure(VVector iso, FitToStructureMargins margins, Structure target = null)
        {
            ProvideUIUpdate("Fitting jaws to target");
            double x1, y1, x2, y2;
            x1 = x2 = y1 = y2 = 0.0;
            if(target == null || target.IsEmpty) return (true, new VRect<double>());
            (double latProjection, StringBuilder message) = ContourHelper.GetMaxLatProjectionDistance(target, iso);
            ProvideUIUpdate(message.ToString());
            x2 = latProjection + margins.X2;
            x1 = -x2;
            y2 = target.MeshGeometry.Positions.Max(p => p.Z) - iso.z + margins.Y2;
            y1 = target.MeshGeometry.Positions.Min(p => p.Z) - iso.z - margins.Y1;
            return (false, VerifyProposedJawPositions(x1, y1, x2, y2));
        }

        /// <summary>
        /// Helper method to verify the calculated jaw positions will not exceed the machine limits
        /// </summary>
        /// <param name="x1"></param>
        /// <param name="y1"></param>
        /// <param name="x2"></param>
        /// <param name="y2"></param>
        /// <returns></returns>
        private VRect<double> VerifyProposedJawPositions(double x1, double y1, double x2, double y2)
        {
            if (y2 > 200.0) y2 = 200.0;
            if (x2 > 150.0) x2 = 150.0;
            if (y1 < -200.0) y1 = -200.0;
            if (x1 < -150.0) x1 = -150.0;
            return new VRect<double>(x1, y1, x2, y2);
        }

        /// <summary>
        /// Helper method to compute the bounding box for the supplied structure. Used for fitting the jaws to PTV_CSI
        /// </summary>
        /// <param name="target"></param>
        /// <param name="zMin"></param>
        /// <param name="zMax"></param>
        /// <returns></returns>
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
                ProvideUIUpdate(100 * ++percentComplete / calcItems);
                //get body contour points
                pts = target.GetContoursOnImagePlane(slice);
                
                //find min and max x positions for the body on this slice (so we can adapt the box positions for each slice)
                for (int i = 0; i < pts.GetLength(0); i++)
                {
                    xMax = Math.Max(xMax, pts[i].Max(p => p.x));
                    xMin = Math.Min(xMin, pts[i].Min(p => p.x));
                    yMax = Math.Max(yMax, pts[i].Max(p => p.y));
                    yMin = Math.Min(yMin, pts[i].Min(p => p.y));
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
        #endregion
    }
}
