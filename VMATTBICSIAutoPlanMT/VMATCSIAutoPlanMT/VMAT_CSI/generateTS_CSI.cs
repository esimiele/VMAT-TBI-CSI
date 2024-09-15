using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Media3D;
using System.Runtime.ExceptionServices;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATCSIAutoPlanMT.VMAT_CSI
{
    public class GenerateTS_CSI : GenerateTSbase
    {
        //get methods
        public List<TSTargetCropOverlapModel> TargetCropOverlapManipulations { get; private set; } = new List<TSTargetCropOverlapModel> { };
        public Dictionary<string, string> NormalizationVolumes { get; private set; } = new Dictionary<string, string> { };
        public List<TSRingStructureModel> AddedRings { get; private set; } = new List<TSRingStructureModel> { };

        //DICOM types
        //Possible values are "AVOIDANCE", "CAVITY", "CONTRAST_AGENT", "CTV", "EXTERNAL", "GTV", "IRRAD_VOLUME", 
        //"ORGAN", "PTV", "TREATED_VOLUME", "SUPPORT", "FIXATION", "CONTROL", and "DOSE_REGION". 
        //dicom type, structure Id
        private List<RequestedTSStructureModel> createTSStructureList;
        //plan id, structure id, num fx, dose per fx, cumulative dose
        private List<PrescriptionModel> prescriptions;
        private List<TSRingStructureModel> requestedRings;
        //plan id, normalization volume
        //structure id of oars requested for crop/overlap eval with targets
        private List<string> cropAndOverlapStructures = new List<string> { };
        private int numVMATIsos;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="ts"></param>
        /// <param name="list"></param>
        /// <param name="tgtRings"></param>
        /// <param name="presc"></param>
        /// <param name="ss"></param>
        /// <param name="cropStructs"></param>
        /// <param name="closePW"></param>
        public GenerateTS_CSI(List<RequestedTSStructureModel> ts, List<RequestedTSManipulationModel> list, List<TSRingStructureModel> tgtRings, List<PrescriptionModel> presc, StructureSet ss, List<string> cropStructs, bool closePW)
        {
            createTSStructureList = new List<RequestedTSStructureModel>(ts);
            requestedRings = new List<TSRingStructureModel>(tgtRings);
            TSManipulationList = new List<RequestedTSManipulationModel>(list);
            prescriptions = new List<PrescriptionModel>(presc);
            selectedSS = ss;
            cropAndOverlapStructures = new List<string>(cropStructs);
            SetCloseOnFinish(closePW, 3000);
        }

        #region Run Control
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
                PlanIsocentersList.Clear();
                if (PreliminaryChecks()) return true;
                if (UnionLRStructures()) return true;
                if (TSManipulationList.Any()) if (CheckHighResolution()) return true;
                //remove all only ts structures NOT including targets
                if (RemoveOldTSStructures(createTSStructureList)) return true;
                if (CreateTSStructures()) return true;
                if (PerformTSStructureManipulation()) return true;
                if(cropAndOverlapStructures.Any())
                {
                    if (CropAndContourOverlapWithTargets()) return true;
                }
                if (GenerateRings()) return true;
                if (RegeneratePTVBrainSpine()) return true;
                if (CalculateNumIsos()) return true;
                UpdateUILabel("Finished!");
                ProvideUIUpdate(100, "Finished Structure Tuning!");
                ProvideUIUpdate($"Run time: {GetElapsedTime()} (mm:ss)");
                return false;
            }
            catch (Exception e) 
            { 
                ProvideUIUpdate($"{e.Message}", true);
                StrackTraceError = e.StackTrace; 
                return true; 
            }
        }
        #endregion

        #region Preliminary Checks
        /// <summary>
        /// Preliminary checks to ensure body exists and is contoured, user origin is inside body, and spinal cord structure exists
        /// </summary>
        /// <returns></returns>
        protected override bool PreliminaryChecks()
        {
            UpdateUILabel("Performing Preliminary Checks: ");
            int calcItems = 3;
            int counter = 0;

            if(!StructureTuningHelper.DoesStructureExistInSS("body",selectedSS,true))
            {
                ProvideUIUpdate("Error! Body structure not found or is empty! Exiting", true);
                return true;
            }
            ProvideUIUpdate(100 * ++counter / calcItems, "Body structure found and is contoured");

            //check if user origin was set
            if (IsUOriginInside()) return true;
            ProvideUIUpdate(100 * ++counter / calcItems, "User origin is inside body");

            //only need spinal cord to determine number of spine isocenters. Otherwise, just need target structures for this class
            if (!StructureTuningHelper.DoesStructureExistInSS(new List<string> { "spinalcord", "spinal_cord"}, selectedSS, true))
            {
                ProvideUIUpdate("Missing brain and/or spine structures! Please add and try again!", true);
                return true;
            }

            ProvideUIUpdate(100 * ++counter / calcItems, "Brain and spinal cord structures exist");
            ProvideUIUpdate(100, "Preliminary checks complete!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }
        #endregion

        #region TS Structure Creation and Manipulation
        /// <summary>
        /// Helper method to create and contour the requested ring structure (with user-supplied margin, thickness, and dose level)
        /// </summary>
        /// <returns></returns>
        private bool GenerateRings()
        {
            if (requestedRings.Any())
            {
                UpdateUILabel("Generating rings:");
                ProvideUIUpdate("Generating requested ring structures for targets!");
                int percentCompletion = 0;
                int calcItems = 3 * requestedRings.Count();
                foreach(TSRingStructureModel itr in requestedRings)
                {
                    Structure target = StructureTuningHelper.GetStructureFromId(itr.TargetId, selectedSS);
                    if (target != null)
                    {
                        ProvideUIUpdate(100 * ++percentCompletion / calcItems, $"Retrieved target: {target.Id}");
                        string ringName = $"TS_ring{itr.DoseLevel}";
                        if(selectedSS.Structures.Any(x => string.Equals(x.Id, ringName)))
                        {
                            ProvideUIUpdate($"Warning! Structure Id is taken: {ringName}! Attempting to update Id!");
                            ringName += "_1";
                            if (selectedSS.Structures.Any(x => string.Equals(x.Id, ringName)))
                            {
                                ProvideUIUpdate($"Error! Unable to update ring structure Id to: {ringName}! Exiting", true);
                                return true;
                            }
                        }

                        Structure ring = AddTSStructures(new RequestedTSStructureModel("CONTROL", ringName));
                        if (ring == null) return true;
                        ProvideUIUpdate(100 * ++percentCompletion / calcItems, $"Created empty ring: {ring.Id}");

                        ProvideUIUpdate($"Contouring ring: {ring.Id}");
                        (bool fail, StringBuilder errorMessage) = ContourHelper.CreateRing(target, ring, selectedSS, itr.MarginFromTargetInCM, itr.RingThicknessInCM);
                        if (fail)
                        {
                            ProvideUIUpdate(errorMessage.ToString());
                            return true;
                        }
                        TSRingStructureModel addRing = new TSRingStructureModel(itr);
                        addRing.RingId = ring.Id;
                        AddedRings.Add(addRing);
                        ProvideUIUpdate(100 * ++percentCompletion / calcItems, $"Finished contouring ring: {itr}");
                    }
                    else ProvideUIUpdate(100 * ++percentCompletion / calcItems, $"Could NOT retrieve target: {itr.TargetId}! Skipping ring: TS_ring{itr.DoseLevel}");
                }
            }
            else ProvideUIUpdate("No ring structures requested!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        /// <summary>
        /// Custom method to create a ring structure on a give CT slice. This method is used in the generation of TS_Eyes and TS_Lenses to avoid
        /// using the built-in methods of structure manipulation provided by the API (slow and prone to memory errors)
        /// </summary>
        /// <param name="target"></param>
        /// <param name="normal"></param>
        /// <param name="addedStructure"></param>
        /// <param name="margin"></param>
        /// <param name="thickness"></param>
        /// <returns></returns>
        private (bool, StringBuilder) ContourPartialRing(Structure target, Structure normal, Structure addedStructure, double margin, double thickness)
        {
            StringBuilder sb = new StringBuilder();
            bool fail = false;
            ProvideUIUpdate($"Contouring partial ring to generate {addedStructure.Id}");
            int percentComplete = 0;
            int calcItems = 1;
            //get the start and stop image planes for this structure (+/- 5 slices)
            int startSlice = CalculationHelper.ComputeSlice(normal.MeshGeometry.Positions.Min(p => p.Z), selectedSS) - 5;
            int stopSlice = CalculationHelper.ComputeSlice(normal.MeshGeometry.Positions.Max(p => p.Z), selectedSS) + 5;
            calcItems += stopSlice - startSlice + 1;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Number of slices to contour: {stopSlice - startSlice + 1}");
            if(addedStructure.CanEditSegmentVolume(out string error))
            {
                for (int slice = startSlice; slice <= stopSlice; slice++)
                {
                    ProvideUIUpdate(100 * ++percentComplete / calcItems);
                    //get the target contour points on this CT slice
                    VVector[][] points = target.GetContoursOnImagePlane(slice);
                    //Generate contour points for partial ring from target points + supplied margin + thickness
                    addedStructure.AddContourOnImagePlane(ContourHelper.GenerateContourPoints(points[0], (margin + thickness) * 10), slice);
                    //Subtract contour points for partial ring from target points + supplied margin
                    addedStructure.SubtractContourOnImagePlane(ContourHelper.GenerateContourPoints(points[0], margin * 10), slice);
                }
            }
            else
            {
                ProvideUIUpdate($"Could not create partial ring for {addedStructure.Id} because: {error}");
                fail = true;
            }
            return (fail, sb);
        }

        /// <summary>
        /// Method to generate TS Eyes and TS Lenses per Nataliya's instructions
        /// </summary>
        /// <param name="addedStructure"></param>
        /// <returns></returns>
        private bool GenerateTSGlobesLenses(Structure addedStructure)
        {
            int counter = 0;
            int calcItems = 4;

            string addedStructureId = addedStructure.Id;
            string normalId;
            double thickness;
            double margin;
            if (addedStructureId.ToLower().Contains("eyes"))
            {
                //TS_eyes
                normalId = "Eyes";
                //margin in cm. 
                margin = 1.0;
                thickness = 2.0;
            }
            else
            {
                //TS_Lenses
                normalId = "Lenses"; 
                margin = 0.7;
                thickness = 2.0;
            }

            //grab the highest Rx target for the initial CSI plan (should be PTV_CSI)
            //6/11/23 THIS CODE WILL NEED TO BE MODIFIED FOR SIB PLANS
            string initTargetId = TargetsHelper.GetHighestRxTargetIdForPlan(prescriptions, prescriptions.First().PlanId);

            if (!StructureTuningHelper.DoesStructureExistInSS(initTargetId, selectedSS, true))
            {
                ProvideUIUpdate(100 * ++counter / calcItems, $"Failed to retrieve {initTargetId} to generate partial ring! Exiting!", true);
                return true;
            }
            Structure targetStructure = StructureTuningHelper.GetStructureFromId(initTargetId, selectedSS);
            ProvideUIUpdate(100 * ++counter / calcItems, $"Retrieved initial plan target: {targetStructure.Id}");

            if (StructureTuningHelper.DoesStructureExistInSS(normalId, selectedSS, true))
            {
                Structure normal = StructureTuningHelper.GetStructureFromId(normalId, selectedSS);
                ProvideUIUpdate(100 * ++counter / calcItems, $"Retrieved structure: {normal.Id}");
                ProvideUIUpdate($"Generating ring {addedStructureId} for target {targetStructure.Id}");

                (bool partialRingFail, StringBuilder partialRingErrorMessage) = ContourPartialRing(targetStructure, normal, addedStructure, margin, thickness);
                if (partialRingFail)
                {
                    StrackTraceError = partialRingErrorMessage.ToString();
                    return true;
                }
                ProvideUIUpdate(100 * ++counter / calcItems, $"Finished contouring ring: {addedStructureId}");

                if(normal.IsHighResolution)
                {
                    ProvideUIUpdate($"Normal structure ({normal.Id}) is high resolution. Attempting to convert {addedStructureId} to high resolution");
                    if(addedStructure.CanConvertToHighResolution())
                    {
                        addedStructure.ConvertToHighResolution();
                        ProvideUIUpdate($"Converted {addedStructureId} to high resolution");
                    }
                    else
                    {
                        ProvideUIUpdate($"Error! Could not convert {addedStructureId} to high resolution! Exiting!", true);
                        return true;
                    }
                }

                ProvideUIUpdate($"Contouring overlap between ring and {normalId}");
                (bool overlapFail, StringBuilder overlapErrorMessage) = ContourHelper.ContourOverlap(normal, addedStructure, 0.0);
                if (overlapFail)
                {
                    ProvideUIUpdate(overlapErrorMessage.ToString());
                    return true;
                }
                ProvideUIUpdate(100 * ++counter / calcItems, "Overlap Contoured!");

                if (CheckTSGlobesLensesStructureIntegrity(addedStructure)) return true;
                ProvideUIUpdate($"Finished contouring: {addedStructureId}");
            }
            else ProvideUIUpdate($"Warning! Could not retrieve normal structure! Skipping {addedStructureId}");
            return false;
        }

        /// <summary>
        /// Helper method to verify the integrity of ts eyes/lenses following contouring. Checks if the resulting structure is empty & 
        /// volume <= 0.1cc. If either or true, the structure is removed from the structure set
        /// </summary>
        /// <param name="addedStructure"></param>
        /// <returns></returns>
        private bool CheckTSGlobesLensesStructureIntegrity(Structure addedStructure)
        {
            bool removalRequired = false;
            if (addedStructure.IsEmpty)
            {
                ProvideUIUpdate($"{addedStructure.Id} is empty!");
                removalRequired = true;
            }
            else if (addedStructure.Volume <= 0.1)
            {
                ProvideUIUpdate($"{addedStructure.Id} volume <= 0.1 cc!");
                removalRequired = true;
            }
            if (removalRequired)
            {
                if(selectedSS.CanRemoveStructure(addedStructure))
                {
                    ProvideUIUpdate($"Removing structure: {addedStructure.Id}");
                    selectedSS.RemoveStructure(addedStructure);
                }
                else
                {
                    ProvideUIUpdate($"Error! Unable to remove {addedStructure.Id}! Exiting", true);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Method to create/generate the requested tuning structures
        /// </summary>
        /// <returns></returns>
        protected override bool CreateTSStructures()
        {
            UpdateUILabel("Create TS Structures:");
            ProvideUIUpdate("Adding remaining tuning structures to stack!");
            //get all TS structures that do not contain 'ctv' or 'ptv' in the title
            List<RequestedTSStructureModel> remainingTS = createTSStructureList.Where(x => !x.StructureId.ToLower().Contains("ctv") && !x.StructureId.ToLower().Contains("ptv")).ToList();
            int calcItems = remainingTS.Count;
            int counter = 0;
            foreach (RequestedTSStructureModel itr in remainingTS)
            {
                //if those structures have NOT been added to the added structure list, go ahead and add them to stack
                if (!AddedStructureIds.Any(x => string.Equals(x.ToLower(), itr.StructureId)))
                {
                    ProvideUIUpdate(100 * ++counter / calcItems, $"Adding TS to added structures: {itr.StructureId}");
                    AddTSStructures(itr);
                }
            }

            ProvideUIUpdate(100, "Finished adding tuning structures!");
            ProvideUIUpdate(0, "Contouring tuning structures!");
            //now contour the various structures
            foreach (string itr in AddedStructureIds.Where(x => !x.ToLower().Contains("ctv") && !x.ToLower().Contains("ptv")))
            {
                ProvideUIUpdate(0, $"Contouring TS: {itr}");
                Structure addedStructure = StructureTuningHelper.GetStructureFromId(itr, selectedSS);
                if (itr.ToLower().Contains("ts_eyes") || itr.ToLower().Contains("ts_lenses"))
                {
                    if (GenerateTSGlobesLenses(addedStructure)) return true;
                }
                else if (itr.ToLower().Contains("armsavoid"))
                {
                    if (CreateArmsAvoid(addedStructure)) return true;
                }
                else if (itr.ToLower().Contains("_prv"))
                {
                    //leave margin as 0.3 cm outer by default
                    (bool fail, StringBuilder errorMessage) = ContourHelper.ContourPRVVolume(addedStructure.Id.Substring(0, addedStructure.Id.LastIndexOf("_")), addedStructure, selectedSS, 0.3);
                    if (fail)
                    {
                        ProvideUIUpdate(errorMessage.ToString());
                        return true;
                    }
                }
                else
                {
                    ProvideUIUpdate($"The requested tuning structure generation operation is not recognized: {itr}. Skipping!");
                }
            }
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        /// <summary>
        /// Helper method to create ts_armsavoid
        /// </summary>
        /// <param name="armsAvoid"></param>
        /// <returns></returns>
        protected bool CreateArmsAvoid(Structure armsAvoid)
        {
            ProvideUIUpdate("Preparing to contour TS_arms...");
            //generate arms avoid structures
            //need lungs, body, and ptv spine structures
            if(!StructureTuningHelper.DoesStructureExistInSS("lungs", selectedSS, true) || !StructureTuningHelper.DoesStructureExistInSS("body", selectedSS, true))
            {
                ProvideUIUpdate("Error! Body and/or lungs structures were not found or are empty! Exiting!", true);
                return true;
            }
            Structure lungs = StructureTuningHelper.GetStructureFromId("lungs", selectedSS);
            Structure body = StructureTuningHelper.GetStructureFromId("body", selectedSS);

            //get longest target for initial plan (first item in gettargetlistforeachplan should be the plan,list of targets for initial plan)
            (bool fail, Structure initPlanTarget, double length, StringBuilder errorMessage) = TargetsHelper.GetLongestTargetInPlan(TargetsHelper.GetTargetListForEachPlan(prescriptions).First(), selectedSS);
            if(fail) 
            {
                ProvideUIUpdate(errorMessage.ToString(), true);
                return true;
            }
            //get most inferior slice of ptv_csi (mesgeometry.bounds.z indicates the most inferior part of a structure)
            int startSlice = CalculationHelper.ComputeSlice(initPlanTarget.MeshGeometry.Positions.Min(p => p.Z), selectedSS);
            //only go to the most superior part of the lungs for contouring the arms
            int stopSlice = CalculationHelper.ComputeSlice(lungs.MeshGeometry.Positions.Max(p => p.Z), selectedSS);

            //generate two dummy structures (L and R)
            Structure dummyBoxL = StructureTuningHelper.GetStructureFromId("DummyBoxL", selectedSS, true);
            Structure dummyBoxR = StructureTuningHelper.GetStructureFromId("DummyBoxR", selectedSS, true);

            //use the center point of the lungs as the y axis anchor
            //extend box in y direction +/- 20 cm
            double yMax = lungs.CenterPoint.y + 200.0;
            double yMin = lungs.CenterPoint.y - 200.0;
            //set box width in lateral direction
            double boxXWidth = 50.0;

            ProvideUIUpdate($"Number of image slices to contour: {stopSlice - startSlice + 1}");
            ProvideUIUpdate("Preparation complete!");
            ProvideUIUpdate("Contouring TS_arms now...");
            int calcItems = stopSlice - startSlice + 4;
            int counter = 0;

            for (int slice = startSlice; slice <= stopSlice; slice++)
            {
                //get body contour points
                VVector[][] bodyPts = body.GetContoursOnImagePlane(slice);
                double xMax = -500000000000.0;
                double xMin = 500000000000.0;
                //find min and max x positions for the body on this slice (so we can adapt the box positions for each slice)
                for (int i = 0; i < bodyPts.GetLength(0); i++)
                {
                    xMax = Math.Max(bodyPts[i].Max(p => p.x), xMax);
                    xMin = Math.Min(bodyPts[i].Min(p => p.x), xMin);
                }

                //box with contour points located at (x,y), (x,0), (x,-y), (0,-y), (-x,-y), (-x,0), (-x, y), (0,y)
                VVector[] ptsL = new[] {
                                        new VVector(xMax, yMax, 0),
                                        new VVector(xMax, 0, 0),
                                        new VVector(xMax, yMin, 0),
                                        new VVector(0, yMin, 0),
                                        new VVector(xMax-boxXWidth, yMin, 0),
                                        new VVector(xMax-boxXWidth, 0, 0),
                                        new VVector(xMax-boxXWidth, yMax, 0),
                                        new VVector(0, yMax, 0)};

                VVector[] ptsR = new[] {
                                        new VVector(xMin + boxXWidth, yMax, 0),
                                        new VVector(xMin + boxXWidth, 0, 0),
                                        new VVector(xMin + boxXWidth, yMin, 0),
                                        new VVector(0, yMin, 0),
                                        new VVector(xMin, yMin, 0),
                                        new VVector(xMin, 0, 0),
                                        new VVector(xMin, yMax, 0),
                                        new VVector(0, yMax, 0)};

                //added in case structures are existing and need to be removed (shouldn't be an issue if they are already null)
                dummyBoxL.ClearAllContoursOnImagePlane(slice);
                dummyBoxR.ClearAllContoursOnImagePlane(slice);
                //add contours on this slice
                dummyBoxL.AddContourOnImagePlane(ptsL, slice);
                dummyBoxR.AddContourOnImagePlane(ptsR, slice);
                ProvideUIUpdate(100 * ++counter / calcItems);
            }

            ProvideUIUpdate(100 * ++counter / calcItems, "Unioning left and right arms avoid structures together!");
            //now contour the arms avoid structure as the union of the left and right dummy boxes
            (bool failUnion, StringBuilder unionErrorMessage) = ContourHelper.ContourUnion(new List<Structure> { dummyBoxL, dummyBoxR }, armsAvoid);
            if (failUnion)
            {
                ProvideUIUpdate(unionErrorMessage.ToString());
                return true;
            }

            ProvideUIUpdate(100 * ++counter / calcItems, "Contouring overlap between arms avoid and body with 5mm outer margin!");
            //contour the arms as the overlap between the current armsAvoid structure and the body with a 5mm outer margin
            (bool failCrop, StringBuilder cropErrorMessage) = ContourHelper.CropStructureFromBody(armsAvoid, selectedSS, 0.5);
            if (failCrop)
            {
                ProvideUIUpdate(cropErrorMessage.ToString());
                return true;
            }

            ProvideUIUpdate(100 * ++counter / calcItems, "Cleaning up!");
            selectedSS.RemoveStructure(dummyBoxR);
            selectedSS.RemoveStructure(dummyBoxL);
            ProvideUIUpdate(100, "Finished contouring arms avoid!");
            return false;
        }

        /// <summary>
        /// Method to perform tuning structure manipulations
        /// </summary>
        /// <returns></returns>
        protected override bool PerformTSStructureManipulation()
        {
            UpdateUILabel("Perform TS Manipulations: ");
            int counter = 0;
            int calcItems = TSManipulationList.Count * prescriptions.Count;
            string tmpPlanId = prescriptions.First().PlanId;
            List<TargetModel> tmpTSTargetList = new List<TargetModel> { };
            string prevTargetId = "";
            //prescriptions are inherently sorted by increasing cumulative Rx to targets
            foreach (PrescriptionModel itr in prescriptions)
            {
                if(!string.Equals(itr.PlanId, tmpPlanId))
                {
                    //new plan
                    PlanTargets.Add(new PlanTargetsModel(tmpPlanId, new List<TargetModel>(tmpTSTargetList)));
                    //last target id represents highest Rx target for previous plan
                    NormalizationVolumes.Add(tmpPlanId, prevTargetId);
                    tmpTSTargetList = new List<TargetModel> { };
                    tmpPlanId = itr.PlanId;
                }
                //create a new TS target for optimization and copy the original target structure onto the new TS structure
                Structure addedTSTarget = GetTSTarget(itr.TargetId);
                prevTargetId = addedTSTarget.Id;
                tmpTSTargetList.Add(new TargetModel(itr.TargetId, itr.CumulativeDoseToTarget, addedTSTarget.Id));

                //ensure the target is cropped 3mm from body
                ProvideUIUpdate($"Cropping TS target from body with {3.0} mm inner margin");
                (bool fail, StringBuilder errorMessage) = ContourHelper.CropStructureFromBody(addedTSTarget, selectedSS, -0.3);
                if (fail)
                {
                    ProvideUIUpdate(errorMessage.ToString());
                    return true;
                }

                if (TSManipulationList.Any())
                {
                    //normal structure id, manipulation type, added margin (if applicable)
                    foreach (RequestedTSManipulationModel itr1 in TSManipulationList)
                    {
                        if (ManipulateTuningStructures(itr1, addedTSTarget)) return true;
                        ProvideUIUpdate(100 * ++counter / calcItems);
                    }
                }
                else ProvideUIUpdate("No TS manipulations requested!");
            }
            //iterated through entire prescription list, need to add final values to normVolumes and tsTargets
            NormalizationVolumes.Add(tmpPlanId, prevTargetId);
            PlanTargets.Add(new PlanTargetsModel(tmpPlanId, new List<TargetModel>(tmpTSTargetList)));
            ProvideUIUpdate("Finished performing TS manipulations");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        /// <summary>
        /// Helper method to check each structure in the crop/overlap list to ensure each structure actually overlaps with each target.
        /// If a structure does not overlap with all targets, remove that structure from the crop/overlap list
        /// </summary>
        /// <returns></returns>
        private bool CheckAllRequestedTargetCropAndOverlapManipulations()
        {
            List<string> structuresToRemove = new List<string> { };
            Dictionary<string, string> tgts = TargetsHelper.GetHighestRxPlanTargetList(prescriptions);
            int percentCompletion = 0;
            int calcItems = ((1 + 2 * tgts.Count) * cropAndOverlapStructures.Count) + 1;
            ProvideUIUpdate(100 * ++percentCompletion / calcItems, "Retrieved plan-target list");
            Dictionary<string, string> highResCropOverlapStructures = new Dictionary<string, string> { };
            foreach (string itr in cropAndOverlapStructures)
            {
                Structure normal = StructureTuningHelper.GetStructureFromId(itr, selectedSS);
                if (normal != null)
                {
                    ProvideUIUpdate(100 * ++percentCompletion / calcItems, $"Retrieved normal structure: {normal.Id}");
                    //verify structures requested for cropping target from structure actually overlap with structure
                    if(!DoesStructureOverlapWithAllTargets(normal, tgts))
                    {
                        //structure does not overlap with all targets
                        ProvideUIUpdate("Removing from TS manipulation list!");
                        structuresToRemove.Add(itr);
                    }
                    else
                    {
                        //structure does overlap with all targets. Need to check if structure is high resolution
                        if(normal.IsHighResolution)
                        {
                            ProvideUIUpdate($"Structure {normal.Id} is high resolution. Converting to low resolution now");
                            //get the high res structure mesh geometry
                            MeshGeometry3D mesh = normal.MeshGeometry;
                            //get the start and stop image planes for this structure
                            int startSlice = CalculationHelper.ComputeSlice(mesh.Positions.Min(p => p.Z), selectedSS);
                            int stopSlice = CalculationHelper.ComputeSlice(mesh.Positions.Max(p => p.Z), selectedSS);

                            //create an Id for the low resolution struture that will be created. The name will be '_lowRes' appended to the current structure Id
                            (bool fail, Structure lowRes) = CreateLowResStructure(normal);
                            if (fail) return true;
                            ProvideUIUpdate($"Contouring {lowRes.Id} now");

                            ContourLowResStructure(normal, lowRes, startSlice, stopSlice);
                            highResCropOverlapStructures.Add(itr, lowRes.Id);
                        }
                    }
                }
                else
                {
                    ProvideUIUpdate($"Warning! Could not retrieve structure: {itr}! Skipping and removing from list!");
                    structuresToRemove.Add(itr);
                }
            }

            if(structuresToRemove.Any()) RemoveStructuresFromCropOverlapList(structuresToRemove);
            foreach(KeyValuePair<string,string> itr in highResCropOverlapStructures)
            {
                int index = cropAndOverlapStructures.IndexOf(itr.Key);
                cropAndOverlapStructures.RemoveAt(index);
                cropAndOverlapStructures.Insert(index, itr.Value);
            }
            ProvideUIUpdate(100, "Removed missing structures or normals that do not overlap with all targets from crop/overlap list");
            return false;
        }

        /// <summary>
        /// Helper method to check if the supplied normal structure overlaps with all targets listed in the prescriptions
        /// </summary>
        /// <param name="normal"></param>
        /// <param name="tgts"></param>
        /// <returns></returns>
        private bool DoesStructureOverlapWithAllTargets(Structure normal, Dictionary<string, string> tgts)
        {
            int percentComplete = 0;
            int calcItems = 2;
            foreach (KeyValuePair<string, string> itr1 in tgts)
            {
                Structure target = StructureTuningHelper.GetStructureFromId(itr1.Value, selectedSS);
                if (target != null)
                {
                    ProvideUIUpdate(100 * ++percentComplete/ calcItems, $"Retrieved target structure: {target.Id}");
                    if (!StructureTuningHelper.IsOverlap(target, normal.MeshGeometry.Positions))
                    {
                        ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Warning! {normal.Id} does not overlap with all plan target ({target.Id}) structures!");
                        return false;
                    }
                    else ProvideUIUpdate(100 * ++percentComplete / calcItems, $"{normal.Id} overlaps with target {target.Id}");
                }
                else ProvideUIUpdate($"Warning! Could not retrieve target: {itr1.Value}! Skipping");
            }
            ProvideUIUpdate($"Normal structure ({normal.Id}) overlaps with all targets");
            return true;
        }

        /// <summary>
        /// Helper method to remove the supplied structure ids from the requested crop/overlap structure list
        /// </summary>
        /// <param name="structuresToRemove"></param>
        private void RemoveStructuresFromCropOverlapList(List<string> structuresToRemove)
        {
            foreach (string itr in structuresToRemove)
            {
                ProvideUIUpdate($"Removing {itr} from crop/overlap list");
                cropAndOverlapStructures.RemoveAt(cropAndOverlapStructures.IndexOf(itr));
            }
        }

        /// <summary>
        /// Helper method to create a target crop structure and copy the target contour onto the target crop structure
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        private (bool, Structure) CreateCropStructure(Structure target)
        {
            bool fail = false;
            string cropName = $"{target.Id}crop";
            if (cropName.Length > 16) cropName = cropName.Substring(0, 16);
            Structure cropStructure;
            if (!string.Equals(cropName, target.Id))
            {
                cropStructure = AddTSStructures(new RequestedTSStructureModel("CONTROL", cropName));
                if (cropStructure == null)
                {
                    ProvideUIUpdate($"Error! Could not create crop structure: {cropName}! Exiting", true);
                    fail = true;
                    return (fail, null);
                }
                cropStructure.SegmentVolume = target.Margin(0.0);
                ProvideUIUpdate($"Created and contoured crop structure: {cropName}");
            }
            else
            {
                ProvideUIUpdate($"Warning! Ran out of characters for structure Id! Using existing TS target: {target.Id}");
                cropStructure = target;
            }
            return (fail, cropStructure);
        }

        /// <summary>
        /// Helper method to create an empty target overlap structure
        /// </summary>
        /// <param name="target"></param>
        /// <param name="prescriptionCount"></param>
        /// <returns></returns>
        private (bool, Structure) CreateOverlapStructure(Structure target, int prescriptionCount)
        {
            bool fail = false;
            string overlapName = $"{target.Id}over";
            if (overlapName.Length > 16) overlapName = overlapName.Substring(0, 16);
            Structure overlapStructure;
            if (string.Equals(overlapName, target.Id))
            {
                ProvideUIUpdate($"Warning! Ran out of characters for structure Id! Using structure Id: TS_overlap{prescriptionCount}");
                overlapName = $"TS_overlap{prescriptionCount}";
            }
            overlapStructure = AddTSStructures(new RequestedTSStructureModel("CONTROL", overlapName));
            if (overlapStructure == null)
            {
                ProvideUIUpdate($"Error! Could not create overlap structure: {overlapName}! Exiting");
                fail = true;
            }
            else ProvideUIUpdate($"Created overlap structure: {overlapName}");
            return (fail, overlapStructure);
        }

        /// <summary>
        /// Method to perform crop/overlap operation between the prescription targets and supplied list of oar structures
        /// </summary>
        /// <returns></returns>
        private bool CropAndContourOverlapWithTargets()
        {
            //only do this for the highest dose target in each plan!
            UpdateUILabel("Crop/overlap with targets:");
            //evaluate overlap of each structure with each target
            //if structure dose not overlap BOTH targets, remove from structure manipulations list and remove added structure
            ProvideUIUpdate("Evaluating overlap between targets and normal structures requested for target cropping!");
            if (CheckAllRequestedTargetCropAndOverlapManipulations()) return true;

            int percentComplete = 0;
            int calcItems = 1 + (3 + 3 * cropAndOverlapStructures.Count) * prescriptions.Count();

            //sort by cumulative Rx to the targets (item 5)
            List<PrescriptionModel> sortedPrescriptions = prescriptions.OrderBy(x => x.CumulativeDoseToTarget).ToList();
            ProvideUIUpdate(100 * ++percentComplete / calcItems, "Sorted prescriptions by cumulative dose");

            if (cropAndOverlapStructures.Any())
            {
                //clear the normalization volumes list as this will be updated with the crop/overlap targets
                NormalizationVolumes.Clear();
                for (int i = 0; i < sortedPrescriptions.Count(); i++)
                {
                    string targetId = $"TS_{sortedPrescriptions.ElementAt(i).TargetId}";
                    if (StructureTuningHelper.DoesStructureExistInSS(targetId, selectedSS, true))
                    {
                        Structure target = StructureTuningHelper.GetStructureFromId(targetId, selectedSS);
                        ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved target: {targetId}");

                        (bool fail, Structure cropStructure) cropResult = CreateCropStructure(target);
                        if (cropResult.fail) return true;
                        ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Added crop structure ({cropResult.Item2.Id}) to stack");

                        (bool fail, Structure overlapStructure) overlapRresult = CreateOverlapStructure(target, i);
                        if (overlapRresult.fail) return true;
                        ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Added overlap structure ({overlapRresult.Item2.Id}) to stack");

                        foreach (string itr in cropAndOverlapStructures)
                        {
                            if(!StructureTuningHelper.DoesStructureExistInSS(itr, selectedSS, true))
                            {
                                ProvideUIUpdate($"Error! Requested normal for crop/overlap structure ({itr}) is empty or missing from structure set! Please fix and try again!", true);
                                return true;
                            }
                            Structure normal = StructureTuningHelper.GetStructureFromId(itr, selectedSS);
                            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved normal structure: {normal.Id}");

                            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Contouring overlap between structure ({itr}) and target ({target.Id})");
                            (bool fail, StringBuilder errorMessage) cropAndContourOverlapResult = ContourHelper.ContourOverlapAndUnion(normal, target, overlapRresult.overlapStructure, selectedSS, 0.0);
                            if (cropAndContourOverlapResult.fail)
                            {
                                ProvideUIUpdate(cropAndContourOverlapResult.errorMessage.ToString(), true);
                                return true;
                            }

                            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Cropping structure ({itr}) from target ({target.Id})");
                            (bool failCrop, StringBuilder errorCropMessage) = ContourHelper.CropStructureFromStructure(cropResult.cropStructure, normal, 0.0);
                            if (failCrop)
                            {
                                ProvideUIUpdate(errorCropMessage.ToString());
                                return true;
                            }
                        }
                        NormalizationVolumes.Add(sortedPrescriptions.ElementAt(i).PlanId, cropResult.Item2.Id);
                        TargetCropOverlapManipulations.Add(new TSTargetCropOverlapModel(sortedPrescriptions.ElementAt(i).PlanId, target.Id, cropResult.cropStructure.Id, VMATTBICSIAutoPlanningHelpers.Enums.TSManipulationType.CropTargetFromStructure));
                        TargetCropOverlapManipulations.Add(new TSTargetCropOverlapModel(sortedPrescriptions.ElementAt(i).PlanId, target.Id, overlapRresult.overlapStructure.Id, VMATTBICSIAutoPlanningHelpers.Enums.TSManipulationType.ContourOverlapWithTarget));
                    }
                    else ProvideUIUpdate($"Could not retrieve ts target: {targetId}");
                }
            }
            else ProvideUIUpdate(100, "No structures remaining to crop and contour overlap with structures! Skipping!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }
        #endregion

        #region Recontour the brain spine targets
        /// <summary>
        /// Helper method to take the approved PTV_CSI target (or the highest Rx target for the initial plan) and use its contour points
        /// to re-contour ptv_brain and ptv_spine
        /// </summary>
        /// <returns></returns>
        private bool RegeneratePTVBrainSpine()
        {
            UpdateUILabel("Regenerating PTV Spine/PTV Brain:");
            ProvideUIUpdate("Regenerating PTV Spine/PTV Brain:");
            int percentComplete = 0;
            int calcItems = 9;

            Structure ptvBrain = StructureTuningHelper.GetStructureFromId("PTV_Brain", selectedSS, true);
            Structure ptvSpine = StructureTuningHelper.GetStructureFromId("PTV_Spine", selectedSS, true);
            if (ptvBrain == null || ptvSpine == null)
            {
                ProvideUIUpdate($"Error! PTV_Brain or PTV_Spine are null! Fix and try again!", true);
                return true;
            }
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved structure: {ptvBrain.Id}");
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved structure: {ptvSpine.Id}");
            if (!ptvSpine.IsEmpty) ClearContourPointsFromAllPlanes(ptvSpine);
            if (!ptvBrain.IsEmpty) ClearContourPointsFromAllPlanes(ptvBrain);
            ProvideUIUpdate($"Cleared all contour points for {ptvBrain.Id} and {ptvSpine.Id}");

            if (ptvBrain.ApprovalHistory.First().ApprovalStatus == StructureApprovalStatus.Approved || ptvSpine.ApprovalHistory.First().ApprovalStatus == StructureApprovalStatus.Approved)
            {
                ProvideUIUpdate($"Error! PTV_Brain or PTV_Spine are approved and I can't modify them! Fix and try again!", true);
                return true;
            }
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Verified approval status of {ptvBrain.Id} and {ptvSpine.Id}");

            int cutSlice = -1;
            (bool fail, double cutPos) = GetCutSliceZPosition();
            if (fail) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems);

            cutSlice = CalculationHelper.ComputeSlice(cutPos, selectedSS);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Z cut slice: {cutSlice}");

            Structure csiInitTarget = StructureTuningHelper.GetStructureFromId(TargetsHelper.GetHighestRxTargetIdForPlan(prescriptions, prescriptions.First().PlanId), selectedSS);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved structure: {csiInitTarget.Id}");

            //stop slice for ptv spine is the cut plane
            ContourStructure(ptvSpine, csiInitTarget, CalculationHelper.ComputeSlice(csiInitTarget.MeshGeometry.Positions.Min(p => p.Z), selectedSS), cutSlice);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Contoured structure: {ptvSpine.Id}");
            
            //start slice for ptv brain is the cut plane
            ContourStructure(ptvBrain, csiInitTarget, cutSlice, CalculationHelper.ComputeSlice(csiInitTarget.MeshGeometry.Positions.Max(p => p.Z), selectedSS));
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Contoured structure: {ptvBrain.Id}");
            return false;
        }

        /// <summary>
        /// Helper method to determine the z position of the cut plane that should be used to split the initial csi target into ptv_brain
        /// and ptv_spine. Either min z of brain or max z of spinal cord will work
        /// </summary>
        /// <returns></returns>
        private (bool, double) GetCutSliceZPosition()
        {
            Structure cutStructure = StructureTuningHelper.GetStructureFromId("brain", selectedSS);
            double cutPos = 0.0;
            if (cutStructure == null || cutStructure.IsEmpty)
            {
                cutStructure = StructureTuningHelper.GetStructureFromId("spinal_cord", selectedSS);
                if (cutStructure == null) cutStructure = StructureTuningHelper.GetStructureFromId("spinalcord", selectedSS);
                if (cutStructure == null || cutStructure.IsEmpty)
                {
                    //give up
                    ProvideUIUpdate($"Error! Brain/Spinal cord structures are null or empty! Fix and try again!", true);
                    return (true,0.0);
                }
                else cutPos = cutStructure.MeshGeometry.Positions.Max(p => p.Z);
            }
            else cutPos = cutStructure.MeshGeometry.Positions.Min(p => p.Z);
            ProvideUIUpdate($"Retrieved structure used to determine cut plan: {cutStructure.Id}");
            ProvideUIUpdate($"Dicom origin ({selectedSS.Image.Origin.x:0.0}, {selectedSS.Image.Origin.y:0.0}, {selectedSS.Image.Origin.z:0.0}) mm");
            ProvideUIUpdate($"Image z resolution: {selectedSS.Image.ZRes:0.0} mm");
            ProvideUIUpdate($"Number of z slices: {selectedSS.Image.ZSize}");
            ProvideUIUpdate($"Z cut position: {cutPos:0.0} mm");
            return (false, cutPos);
        }

        /// <summary>
        /// Helper method to clear all contour points from all image planes for the supplied structure
        /// </summary>
        /// <param name="structToRemove"></param>
        /// <returns></returns>
        private bool ClearContourPointsFromAllPlanes(Structure structToRemove)
        {
            ProvideUIUpdate($"Removing structure: {structToRemove.Id}");
            int startSlice = CalculationHelper.ComputeSlice(structToRemove.MeshGeometry.Positions.Min(p => p.Z), selectedSS);
            int stopSlice = CalculationHelper.ComputeSlice(structToRemove.MeshGeometry.Positions.Max(p => p.Z), selectedSS);
            ProvideUIUpdate($"Start slice: {startSlice}");
            ProvideUIUpdate($"Stop slice: {stopSlice}");
            int percentComplete = 0;
            int calcItems = stopSlice - startSlice + 1;
            for (int slice = startSlice; slice <= stopSlice; slice++)
            {
                ProvideUIUpdate(100 * ++percentComplete / calcItems);
                if (structToRemove.GetContoursOnImagePlane(slice).Any()) structToRemove.ClearAllContoursOnImagePlane(slice);
            }
            return false;
        }

        /// <summary>
        /// Helper method to copy the contour points from the supplied base structure onto the structure to contour
        /// </summary>
        /// <param name="structToContour"></param>
        /// <param name="baseStructure"></param>
        /// <param name="startSlice"></param>
        /// <param name="stopSlice"></param>
        /// <returns></returns>
        private bool ContourStructure(Structure structToContour, Structure baseStructure, int startSlice, int stopSlice)
        {
            ProvideUIUpdate($"Contouring structure: {structToContour.Id}");
            ProvideUIUpdate($"Base structure: {baseStructure.Id}");
            ProvideUIUpdate($"Start slice: {startSlice}");
            ProvideUIUpdate($"Stop slice: {stopSlice}");
            int percentComplete = 0;
            int calcItems = stopSlice - startSlice + 1;
            for(int slice = startSlice; slice <= stopSlice; slice++)
            {
                ProvideUIUpdate(100 * ++percentComplete / calcItems);
                VVector[][] pts = baseStructure.GetContoursOnImagePlane(slice);
                if (pts.Any())
                {
                    for (int i = 0; i < pts.GetLength(0); i++)
                    {
                        if (structToContour.IsPointInsideSegment(pts[i][0]) || structToContour.IsPointInsideSegment(pts[i][pts[i].GetLength(0) - 1]))
                        {
                            structToContour.SubtractContourOnImagePlane(pts[i], slice);
                        }
                        else structToContour.AddContourOnImagePlane(pts[i], slice);
                    }
                }
            }
            return false;
        }
        #endregion

        #region Isocenter Calculation
        /// <summary>
        /// Method to calculate the required number of vmat isocenters for each plan
        /// </summary>
        /// <returns></returns>
        protected override bool CalculateNumIsos()
        {
            UpdateUILabel("Calculating Number of Isocenters:");
            int calcItems = 1;
            int counter = 0;

            //For these cases the maximum number of allowed isocenters is 3. One isocenter is reserved for the brain and either one or two isocenters are used for the spine (depending on length).
            //revised to get the number of unique plans list, for each unique plan, find the target with the greatest z-extent and determine the number of isocenters based off that target. 
            //plan Id, list of targets assigned to that plan

            List<PlanTargetsModel> planIdTargets = new List<PlanTargetsModel>(TargetsHelper.GetTargetListForEachPlan(prescriptions));
            ProvideUIUpdate(100 * ++counter / calcItems, "Generated list of plans each containing list of targets");

            foreach (PlanTargetsModel itr in planIdTargets)
            {
                calcItems = itr.Targets.Count;
                counter = 0;
                //determine for each plan which target has the greatest z-extent
                (bool fail, Structure longestTargetInPlan, double maxTargetLength, StringBuilder errorMessage) = TargetsHelper.GetLongestTargetInPlan(itr, selectedSS);
                if(fail)
                {
                    ProvideUIUpdate($"Error! No structure named: {errorMessage} found or contoured!", true);
                    return true;
                }
                ProvideUIUpdate($"Determined target with greatest extent: {longestTargetInPlan.Id}, Plan: {itr.PlanId}");

                counter = 0;
                calcItems = 3;

                //Minimum requested field overlap.
                double minFieldOverlap = 50.0;
                double maxFieldExtent = 400.0;
                //subtract 50 mm from the numerator as the brain fields have a 50 mm inferior margin on the ptv_brain 
                double brainInfMargin = 50.0;

                //If the target ID is PTV_CSI, calculate the number of isocenters based on PTV_spine and add one iso for the brain
                //planId, target list
                if (string.Equals(longestTargetInPlan.Id, TargetsHelper.GetHighestRxTargetIdForPlan(prescriptions, prescriptions.First().PlanId)))
                {
                    calcItems += 1;
                    //special rules for initial plan,
                    //first, determine the number of isocenters required to treat PTV_Spine
                    //Grab extent of PTV_Spine and add a 2 cm margin to this distance to give 2 cm buffer on the sup portion of the target to ensure adequate coverage/overlap between upper spine field and brain fields
                    (bool isFail, double spineTargetExtent) = GetSpineTargetExtent(2.0);
                    if (isFail) return true;
                    ProvideUIUpdate(100 * ++counter / calcItems);

                    numVMATIsos = CalculateNumVMATIsosForPTVCSI(spineTargetExtent, brainInfMargin, maxFieldExtent, minFieldOverlap);
                    ProvideUIUpdate(100 * ++counter / calcItems, $"Final calculated number of VMAT isocenters: {numVMATIsos}");
                }
                else
                {
                    numVMATIsos = (int)Math.Ceiling(maxTargetLength / (maxFieldExtent - minFieldOverlap));
                    ProvideUIUpdate(100 * ++counter / calcItems, $"{numVMATIsos}");
                }
                if (numVMATIsos > 3) numVMATIsos = 3;

                //set isocenter names based on numIsos and numVMATIsos (be sure to pass 'true' for the third argument to indicate that this is a CSI plan(s))
                //plan Id, list of isocenter names for this plan
                PlanIsocentersList.Add(new PlanIsocenterModel(itr.PlanId, IsoNameHelper.GetCSIIsoNames(numVMATIsos)));
                ProvideUIUpdate(100 * ++counter / calcItems, "Added isocenter to stack!");
            }
            ProvideUIUpdate($"Required Number of Isocenters: {numVMATIsos}");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        /// <summary>
        /// Helper method to calculate the number of vmat iso
        /// </summary>
        /// <param name="spineTargetExtent"></param>
        /// <param name="brainInfMargin"></param>
        /// <param name="maxFieldExtent"></param>
        /// <param name="minFieldOverlap"></param>
        /// <returns></returns>
        private int CalculateNumVMATIsosForPTVCSI(double spineTargetExtent, double brainInfMargin, double maxFieldExtent, double minFieldOverlap)
        {
            double numVMATIsosAsDouble = (spineTargetExtent - brainInfMargin) / (maxFieldExtent - minFieldOverlap);
            ProvideUIUpdate($"Spine target extent: {spineTargetExtent:0.00}");
            ProvideUIUpdate($"Num VMAT isos as double: {(spineTargetExtent - brainInfMargin) / (maxFieldExtent - minFieldOverlap):0.00}");
            if (numVMATIsosAsDouble > 1 && numVMATIsosAsDouble % 1 < 0.1)
            {
                ProvideUIUpdate($"Calculated number of vmat isos MOD 1 is < 0.1 (i.e. an extra {0.1 * (maxFieldExtent - minFieldOverlap):0.0} mm of field is required to cover the spine");
                numVMATIsosAsDouble = Math.Floor(numVMATIsosAsDouble);
                ProvideUIUpdate($"Truncating number of isos to {numVMATIsosAsDouble}");
            }
            else numVMATIsosAsDouble = Math.Ceiling(numVMATIsosAsDouble);
            ProvideUIUpdate($"Adding one additional isocenter for the brain");
            //one iso reserved for PTV_Brain
            return (int)numVMATIsosAsDouble + 1;
        }

        /// <summary>
        /// Helper method to calculate the extent of PTV_Spine with a user-supplied additional margin
        /// </summary>
        /// <param name="addedMarginInCm"></param>
        /// <returns></returns>
        private (bool, double) GetSpineTargetExtent(double addedMarginInCm)
        {
            bool fail = false;
            double spineTargetExtent = 0.0;
            if (StructureTuningHelper.DoesStructureExistInSS("PTV_Spine", selectedSS, true))
            {
                Structure spineTarget = StructureTuningHelper.GetStructureFromId("PTV_Spine", selectedSS);
                ProvideUIUpdate("Retrieved spinal cord structure");
                Point3DCollection pts = spineTarget.MeshGeometry.Positions;
                //ESAPI default distances are in mm
                spineTargetExtent = (pts.Max(p => p.Z) - pts.Min(p => p.Z)) + addedMarginInCm * 10;
            }
            else
            {
                ProvideUIUpdate("Error! No structure named PTV_Spine was found or it was empty!", true);
            }
            return (fail, spineTargetExtent);
        }
        #endregion
    }
}