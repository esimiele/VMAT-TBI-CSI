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
using TSManipulationType = VMATTBICSIAutoPlanningHelpers.Enums.TSManipulationType;

namespace VMATCSIAutoPlanMT.VMAT_CSI
{
    public class GenerateTS_CSI : GenerateTSbase
    {
        public List<Tuple<string, string, List<Tuple<string, string>>>> GetTargetCropOverlapManipulations() { return targetManipulations; }
        public List<Tuple<string, List<Tuple<string, string>>>> GetTsTargets() { return tsTargets; }
        public List<Tuple<string, string>> GetNormalizationVolumes() { return normVolumes; }
        public List<Tuple<string, string, double>> GetAddedRings() { return addedRings; }

        //DICOM types
        //Possible values are "AVOIDANCE", "CAVITY", "CONTRAST_AGENT", "CTV", "EXTERNAL", "GTV", "IRRAD_VOLUME", 
        //"ORGAN", "PTV", "TREATED_VOLUME", "SUPPORT", "FIXATION", "CONTROL", and "DOSE_REGION". 
        private List<Tuple<string, string>> createTSStructureList;
        //plan id, structure id, num fx, dose per fx, cumulative dose
        private List<Tuple<string, string, int, DoseValue, double>> prescriptions;
        //target id, margin (cm), thickness (cm), dose (cGy)
        private List<Tuple<string, double, double, double>> rings;
        //target id, ring id, dose (cGy)
        private List<Tuple<string, string, double>> addedRings = new List<Tuple<string, string, double>> { };
        //plan id, list<target id, ts target id>
        private List<Tuple<string, List<Tuple<string, string>>>> tsTargets = new List<Tuple<string, List<Tuple<string, string>>>> { };
        //planId, lower dose target id, list<manipulation target id, operation>
        private List<Tuple<string, string, List<Tuple<string, string>>>> targetManipulations = new List<Tuple<string, string, List<Tuple<string, string>>>> { };
        //plan id, normalization volume
        private List<Tuple<string, string>> normVolumes = new List<Tuple<string, string>> { };
        private List<string> cropAndOverlapStructures = new List<string> { };
        private int numVMATIsos;

        public GenerateTS_CSI(List<Tuple<string, string>> ts, List<Tuple<string, TSManipulationType, double>> list, List<Tuple<string, double, double, double>> tgtRings, List<Tuple<string,string,int,DoseValue,double>> presc, StructureSet ss, List<string> cropStructs)
        {
            createTSStructureList = new List<Tuple<string, string>>(ts);
            rings = new List<Tuple<string, double, double, double>>(tgtRings);
            TSManipulationList = new List<Tuple<string, TSManipulationType, double>>(list);
            prescriptions = new List<Tuple<string, string, int, DoseValue, double>>(presc);
            selectedSS = ss;
            cropAndOverlapStructures = new List<string>(cropStructs);
        }

        #region Run Control
        //to handle system access exception violation
        [HandleProcessCorruptedStateExceptions]
        public override bool Run()
        {
            try
            {
                isoNames.Clear();
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
                stackTraceError = e.StackTrace; 
                return true; 
            }
        }
        #endregion

        #region Preliminary Checks and Structure Unioning
        protected override bool PreliminaryChecks()
        {
            UpdateUILabel("Performing Preliminary Checks: ");
            int calcItems = 2;
            int counter = 0;
            //check if user origin was set
            if (IsUOriginInside(selectedSS)) return true;
            ProvideUIUpdate((int)(100 * ++counter / calcItems), "User origin is inside body");

            //only need spinal cord to determine number of spine isocenters. Otherwise, just need target structures for this class
            if (!StructureTuningHelper.DoesStructureExistInSS(new List<string> { "spinalcord", "spinal_cord"}, selectedSS, true))
            {
                ProvideUIUpdate("Missing brain and/or spine structures! Please add and try again!", true);
                return true;
            }

            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Brain and spinal cord structures exist");
            ProvideUIUpdate(100, "Preliminary checks complete!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }
        #endregion

        #region TS Structure Creation and Manipulation
        private bool GenerateRings()
        {
            if (rings.Any())
            {
                UpdateUILabel("Generating rings:");
                ProvideUIUpdate("Generating requested ring structures for targets!");
                int percentCompletion = 0;
                int calcItems = 3 * rings.Count();
                foreach(Tuple<string,double,double,double> itr in rings)
                {
                    Structure target = StructureTuningHelper.GetStructureFromId(itr.Item1, selectedSS);
                    if (target != null)
                    {
                        ProvideUIUpdate((int)(100 * ++percentCompletion / calcItems), $"Retrieved target: {target.Id}");
                        string ringName = $"TS_ring{itr.Item4}";
                        if(selectedSS.Structures.Any(x => string.Equals(x.Id, ringName)))
                        {
                            //name is taken, append a '1' to it
                            ringName += "1";
                        }
                        Structure ring = AddTSStructures(new Tuple<string, string>("CONTROL", ringName));
                        if (ring == null) return true;
                        ProvideUIUpdate((int)(100 * ++percentCompletion / calcItems), $"Created empty ring: {ring.Id}");
                        (bool fail, StringBuilder errorMessage) = ContourHelper.CreateRing(target, ring, selectedSS, itr.Item2, itr.Item3);
                        if (fail)
                        {
                            ProvideUIUpdate(errorMessage.ToString());
                            return true;
                        }
                        ProvideUIUpdate($"Contouring ring: {ring.Id}");
                        addedRings.Add(Tuple.Create(target.Id, ring.Id, itr.Item4));
                        ProvideUIUpdate((int)(100 * ++percentCompletion / calcItems), $"Finished contouring ring: {itr}");
                    }
                    else ProvideUIUpdate((int)(100 * ++percentCompletion / calcItems), $"Could NOT retrieve target: {itr.Item1}! Skipping ring: TS_ring{itr.Item4}");
                }
            }
            else ProvideUIUpdate("No ring structures requested!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        private (bool, StringBuilder) ContourPartialRing(Structure target, Structure normal, Structure addedStructure, double margin, double thickness)
        {
            StringBuilder sb = new StringBuilder();
            bool fail = false;
            ProvideUIUpdate($"Contouring partial ring to generate {addedStructure.Id}");
            int percentComplete = 0;
            int calcItems = 1;
            //get the high res structure mesh geometry
            MeshGeometry3D mesh = normal.MeshGeometry;
            //get the start and stop image planes for this structure (+/- 5 slices)
            int startSlice = CalculationHelper.ComputeSlice(mesh.Bounds.Z, selectedSS) - 5;
            int stopSlice = CalculationHelper.ComputeSlice(mesh.Bounds.Z + mesh.Bounds.SizeZ, selectedSS) + 5;
            calcItems += stopSlice - startSlice - 1;
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Number of slices to contour: {stopSlice - startSlice}");
            if(addedStructure.CanEditSegmentVolume(out string error))
            {
                for (int slice = startSlice; slice < stopSlice; slice++)
                {
                    ProvideUIUpdate((int)(100 * ++percentComplete / calcItems));
                    VVector[][] points = target.GetContoursOnImagePlane(slice);
                    //we only want the outer contour points of the target
                    addedStructure.AddContourOnImagePlane(ContourHelper.GenerateContourPoints(points[0], (margin + thickness) * 10), slice);
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

        private bool GenerateTSGlobesLenses(Structure addedStructure)
        {
            int counter = 0;
            int calcItems = 4;
            bool isGlobes = false;
            if (addedStructure.Id.ToLower().Contains("globes")) isGlobes = true;

            //try to grab ptv_brain first
            //6/11/23 THIS CODE WILL NEED TO BE MODIFIED FOR SIB PLANS
            string initTargetId = TargetsHelper.GetHighestRxTargetIdForPlan(prescriptions, prescriptions.First().Item1);
            Structure targetStructure = StructureTuningHelper.GetStructureFromId(initTargetId, selectedSS);
            double margin = 0;

            if (targetStructure == null || targetStructure.IsEmpty)
            {
                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Failed to retrieve {initTargetId} to generate partial ring! Exiting!");
                return true;
                ////could not retrieve ptv_brain
                //calcItems += 1;
                //ProvideUIUpdate((int)(100 * ++counter / calcItems), "Failed to retrieve PTV_Brain! Attempting to retrieve brain structure: Brain");
                //targetStructure = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "brain") && !x.IsEmpty);
                ////additional 5 mm margin for ring creation to account for the missing 5 mm margin going from brain --> PTV_Vrain
                //margin = 0.5;
            }
            if (targetStructure != null)
            {
                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Retrieved initial plan target: {targetStructure.Id}");
                Structure normal;
                if (isGlobes) normal = StructureTuningHelper.GetStructureFromId("globes", selectedSS);
                else normal = StructureTuningHelper.GetStructureFromId("lenses", selectedSS);

                if (normal != null && !normal.IsEmpty)
                {
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Retrieved structure: {normal.Id}");
                    ProvideUIUpdate($"Generating ring {addedStructure.Id} for target {targetStructure.Id}");
                    //margin in cm. 
                    double thickness;
                    if (isGlobes)
                    {
                        //need to add these margins to the existing margin distance to account for the situation where ptv_brain is not retrieved, but the brain structure is.
                        margin += 1.0;
                        thickness = 2.0;
                    }
                    else
                    {
                        margin += 0.7;
                        thickness = 2.0;
                    }
                    (bool partialRingFail, StringBuilder partialRingErrorMessage) = ContourPartialRing(targetStructure, normal, addedStructure, margin, thickness);
                    if (partialRingFail)
                    {
                        stackTraceError = partialRingErrorMessage.ToString();
                        return true;
                    }
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Finished contouring ring: {addedStructure.Id}");

                    ProvideUIUpdate($"Contouring overlap between ring and {(isGlobes ? "Globes" : "Lenses")}");
                    (bool overlapFail, StringBuilder overlapErrorMessage) = ContourHelper.ContourOverlap(normal, addedStructure, 0.0);
                    if (overlapFail)
                    {
                        ProvideUIUpdate(overlapErrorMessage.ToString());
                        return true;
                    }
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), "Overlap Contoured!");

                    if (addedStructure.IsEmpty)
                    {
                        ProvideUIUpdate($"{addedStructure.Id} is empty!");
                        calcItems += 1;
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Removing structure: {addedStructure.Id}");
                        selectedSS.RemoveStructure(addedStructure);
                    }
                    else if(addedStructure.Volume <= 0.1)
                    {
                        ProvideUIUpdate($"{addedStructure.Id} volume <= 0.1 cc!");
                        calcItems += 1;
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Removing structure: {addedStructure.Id}");
                        selectedSS.RemoveStructure(addedStructure);
                    }
                    else ProvideUIUpdate($"Finished contouring: {addedStructure.Id}");
                }
                else ProvideUIUpdate($"Warning! Could not retrieve normal structure! Skipping {addedStructure.Id}");
            }
            else ProvideUIUpdate($"Warning! Could not retrieve Brain structure! Skipping {addedStructure.Id}");
            return false;
        }

        protected override bool CreateTSStructures()
        {
            UpdateUILabel("Create TS Structures:");
            ProvideUIUpdate("Adding remaining tuning structures to stack!");
            //get all TS structures that do not contain 'ctv' or 'ptv' in the title
            List<Tuple<string, string>> remainingTS = createTSStructureList.Where(x => !x.Item2.ToLower().Contains("ctv") && !x.Item2.ToLower().Contains("ptv")).ToList();
            int calcItems = remainingTS.Count;
            int counter = 0;
            foreach (Tuple<string, string> itr in remainingTS)
            {
                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Adding TS to added structures: {itr.Item2}");
                //if those structures have NOT been added to the added structure list, go ahead and add them to stack
                if (!addedStructures.Any(x => string.Equals(x.ToLower(), itr.Item2))) AddTSStructures(itr);
            }

            ProvideUIUpdate(100, "Finished adding tuning structures!");
            ProvideUIUpdate(0, "Contouring tuning structures!");
            //now contour the various structures
            foreach (string itr in addedStructures.Where(x => !x.ToLower().Contains("ctv") && !x.ToLower().Contains("ptv")))
            {
                ProvideUIUpdate(0, $"Contouring TS: {itr}");
                Structure addedStructure = StructureTuningHelper.GetStructureFromId(itr, selectedSS);
                if (itr.ToLower().Contains("ts_globes") || itr.ToLower().Contains("ts_lenses"))
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
                    if (ContourInnerOuterStructure(addedStructure)) return true;
                }
            }
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        protected bool CreateArmsAvoid(Structure armsAvoid)
        {
            ProvideUIUpdate("Preparing to contour TS_arms...");
            //generate arms avoid structures
            //need lungs, body, and ptv spine structures
            Structure lungs = StructureTuningHelper.GetStructureFromId("lungs", selectedSS);
            Structure body = StructureTuningHelper.GetStructureFromId("body", selectedSS);
            if(lungs == null || body == null || lungs.IsEmpty || body.IsEmpty)
            {
                ProvideUIUpdate("Error! Body and/or lungs structures were not found or are empty! Exiting!", true);
                return true;
            }
            //get longest target for initial plan (first item in gettargetlistforeachplan should be the plan,list of targets for initial plan)
            (bool fail, Structure initPlanTarget, double length, StringBuilder errorMessage) = TargetsHelper.GetLongestTargetInPlan(TargetsHelper.GetTargetListForEachPlan(prescriptions).First(), selectedSS);
            if(fail) 
            {
                ProvideUIUpdate(errorMessage.ToString(), true);
                return true;
            }
            MeshGeometry3D mesh = initPlanTarget.MeshGeometry;
            //get most inferior slice of ptv_csi (mesgeometry.bounds.z indicates the most inferior part of a structure)
            int startSlice = CalculationHelper.ComputeSlice(initPlanTarget.MeshGeometry.Positions.Min(p => p.Z), selectedSS);
            //only go to the most superior part of the lungs for contouring the arms
            int stopSlice = CalculationHelper.ComputeSlice(lungs.MeshGeometry.Positions.Max(p => p.Z), selectedSS);

            //initialize variables
            double xMax = 0;
            double xMin = 0;
            double yMax = 0;
            double yMin = 0;
            VVector[][] bodyPts;
            //generate two dummy structures (L and R)
            Structure dummyBoxL = StructureTuningHelper.GetStructureFromId("DummyBoxL", selectedSS, true);
            Structure dummyBoxR = StructureTuningHelper.GetStructureFromId("DummyBoxR", selectedSS, true);
            //use the center point of the lungs as the y axis anchor
            double yCenter = lungs.CenterPoint.y;
            //extend box in y direction +/- 20 cm
            yMax = yCenter + 200.0;
            yMin = yCenter - 200.0;

            //set box width in lateral direction
            double boxXWidth = 50.0;
            //empty vectors to hold points for left and right dummy boxes for each slice
            VVector[] ptsL = new[] { new VVector() };
            VVector[] ptsR = new[] { new VVector() };

            ProvideUIUpdate($"Number of image slices to contour: {stopSlice - startSlice}");
            ProvideUIUpdate("Preparation complete!");
            ProvideUIUpdate("Contouring TS_arms now...");
            int calcItems = stopSlice - startSlice + 3;
            int counter = 0;

            for (int slice = startSlice; slice <= stopSlice; slice++)
            {
                //get body contour points
                bodyPts = body.GetContoursOnImagePlane(slice);
                xMax = -500000000000.0;
                xMin = 500000000000.0;
                //find min and max x positions for the body on this slice (so we can adapt the box positions for each slice)
                for (int i = 0; i < bodyPts.GetLength(0); i++)
                {
                    if (bodyPts[i].Max(p => p.x) > xMax) xMax = bodyPts[i].Max(p => p.x);
                    if (bodyPts[i].Min(p => p.x) < xMin) xMin = bodyPts[i].Min(p => p.x);
                }

                //box with contour points located at (x,y), (x,0), (x,-y), (0,-y), (-x,-y), (-x,0), (-x, y), (0,y)
                ptsL = new[] {
                                new VVector(xMax, yMax, 0),
                                new VVector(xMax, 0, 0),
                                new VVector(xMax, yMin, 0),
                                new VVector(0, yMin, 0),
                                new VVector(xMax-boxXWidth, yMin, 0),
                                new VVector(xMax-boxXWidth, 0, 0),
                                new VVector(xMax-boxXWidth, yMax, 0),
                                new VVector(0, yMax, 0)};

                ptsR = new[] {
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
                ProvideUIUpdate((int)(100 * ++counter / calcItems));
            }

            //extend the arms avoid structure superiorly by x number of slices
            //for (int slice = stop; slice < stop + 10; slice++)
            //{
            //    dummyBoxL.AddContourOnImagePlane(ptsL, slice);
            //    dummyBoxR.AddContourOnImagePlane(ptsR, slice);
            //}

            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Unioning left and right arms avoid structures together!");
            //now contour the arms avoid structure as the union of the left and right dummy boxes
            armsAvoid.SegmentVolume = dummyBoxL.Margin(0.0);
            armsAvoid.SegmentVolume = armsAvoid.Or(dummyBoxR.Margin(0.0));
            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Contouring overlap between arms avoid and body with 5mm outer margin!");
            //contour the arms as the overlap between the current armsAvoid structure and the body with a 5mm outer margin
            (bool failCrop, StringBuilder cropErrorMessage) = ContourHelper.CropStructureFromBody(armsAvoid, selectedSS, 0.5);
            if (failCrop)
            {
                ProvideUIUpdate(cropErrorMessage.ToString());
                return true;
            }

            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Cleaning up!");
            selectedSS.RemoveStructure(dummyBoxR);
            selectedSS.RemoveStructure(dummyBoxL);
            ProvideUIUpdate(100, "Finished contouring arms avoid!");
            return false;
        }

        protected override bool PerformTSStructureManipulation()
        {
            UpdateUILabel("Perform TS Manipulations: ");
            int counter = 0;
            int calcItems = TSManipulationList.Count * prescriptions.Count;
            string tmpPlanId = prescriptions.First().Item1;
            List<Tuple<string, string>> tmpTSTargetList = new List<Tuple<string, string>> { };
            string prevTargetId = "";
            //prescriptions are inherently sorted by increasing cumulative Rx to targets
            foreach (Tuple<string, string, int, DoseValue, double> itr in prescriptions)
            {
                if(!string.Equals(itr.Item1, tmpPlanId))
                {
                    //new plan
                    tsTargets.Add(new Tuple<string, List<Tuple<string, string>>>(tmpPlanId, new List<Tuple<string, string>>(tmpTSTargetList)));
                    normVolumes.Add(Tuple.Create(tmpPlanId, prevTargetId));
                    tmpTSTargetList = new List<Tuple<string, string>> { };
                    tmpPlanId = itr.Item1;
                }
                //create a new TS target for optimization and copy the original target structure onto the new TS structure
                Structure addedTSTarget = GetTSTarget(itr.Item2);
                prevTargetId = addedTSTarget.Id;
                tmpTSTargetList.Add(new Tuple<string, string>(itr.Item2, addedTSTarget.Id));
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
                    foreach (Tuple<string, TSManipulationType, double> itr1 in TSManipulationList)
                    {
                        if (ManipulateTuningStructures(itr1, addedTSTarget, ref counter, ref calcItems)) return true;
                    }
                }
                else ProvideUIUpdate("No TS manipulations requested!");
            }
            normVolumes.Add(Tuple.Create(tmpPlanId, prevTargetId));
            tsTargets.Add(new Tuple<string, List<Tuple<string, string>>>(tmpPlanId, new List<Tuple<string, string>>(tmpTSTargetList)));
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        private bool CheckAllRequestedTargetCropAndOverlapManipulations()
        {
            List<string> structuresToRemove = new List<string> { };
            List<Tuple<string, string>> tgts = TargetsHelper.GetPlanTargetList(prescriptions);
            int percentCompletion = 0;
            int calcItems = ((1 + 2 * tgts.Count) * cropAndOverlapStructures.Count) + 1;
            ProvideUIUpdate((int)(100 * ++percentCompletion / calcItems), "Retrieved plan-target list");

            foreach (string itr in cropAndOverlapStructures)
            {
                Structure normal = StructureTuningHelper.GetStructureFromId(itr, selectedSS);
                if (normal != null)
                {
                    ProvideUIUpdate((int)(100 * ++percentCompletion / calcItems), $"Retrieved normal structure: {normal.Id}");
                    //verify structures requested for cropping target from structure actually overlap with structure
                    //planid, targetid
                    foreach (Tuple<string, string> itr1 in tgts)
                    {
                        Structure target = StructureTuningHelper.GetStructureFromId(itr1.Item2, selectedSS);
                        if (target != null)
                        {
                            ProvideUIUpdate((int)(100 * ++percentCompletion / calcItems), $"Retrieved target structure: {target.Id}");
                            if (!StructureTuningHelper.IsOverlap(target, normal, selectedSS, 0.0))
                            {
                                ProvideUIUpdate((int)(100 * ++percentCompletion / calcItems), $"Warning! {normal.Id} does not overlap with all plan target ({target.Id}) structures!");
                                ProvideUIUpdate("Removing from TS manipulation list!");
                                structuresToRemove.Add(itr);
                                break;
                            }
                            else ProvideUIUpdate((int)(100 * ++percentCompletion / calcItems), $"{normal.Id} overlaps with target {target.Id}");
                        }
                        else ProvideUIUpdate($"Warning! Could not retrieve target: {itr1.Item2}! Skipping");
                    }
                }
                else
                {
                    ProvideUIUpdate($"Warning! Could not retrieve structure: {itr}! Skipping and removing from list!");
                    structuresToRemove.Add(itr);
                }
            }

            RemoveStructuresFromCropOverlapList(structuresToRemove);
            ProvideUIUpdate(100, "Removed missing structures or normals that do not overlap with all targets from crop/overlap list");
            return false;
        }

        private void RemoveStructuresFromCropOverlapList(List<string> structuresToRemove)
        {
            foreach (string itr in structuresToRemove)
            {
                ProvideUIUpdate($"Removing {itr} from crop/overlap list");
                cropAndOverlapStructures.RemoveAt(cropAndOverlapStructures.IndexOf(itr));
            }
        }

        private (bool, Structure) CreateCropStructure(Structure target)
        {
            bool fail = false;
            string cropName = $"{target.Id}crop";
            if (cropName.Length > 16) cropName = cropName.Substring(0, 16);
            Structure cropStructure;
            if (!string.Equals(cropName, target.Id))
            {
                cropStructure = AddTSStructures(new Tuple<string, string>("CONTROL", cropName));
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
            overlapStructure = AddTSStructures(new Tuple<string, string>("CONTROL", overlapName));
            if (overlapStructure == null)
            {
                ProvideUIUpdate($"Error! Could not create overlap structure: {overlapName}! Exiting");
                fail = true;
            }
            else ProvideUIUpdate($"Created overlap structure: {overlapName}");
            return (fail, overlapStructure);
        }

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
            //sort by cumulative Rx to the targets (item 2)
            List<Tuple<string, string, int, DoseValue, double>> sortedPrescriptions = prescriptions.OrderBy(x => x.Item5).ToList();
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), "Sorted prescriptions by cumulative dose");

            if (cropAndOverlapStructures.Any())
            {
                normVolumes.Clear();
                for (int i = 0; i < sortedPrescriptions.Count(); i++)
                {
                    string targetId = $"TS_{sortedPrescriptions.ElementAt(i).Item2}";
                    Structure target = StructureTuningHelper.GetStructureFromId(targetId, selectedSS);
                    List<Tuple<string, string>> tmp = new List<Tuple<string, string>> { };
                    ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved target: {targetId}");
                    if (target != null)
                    {
                        (bool fail, Structure cropStructure) cropResult = CreateCropStructure(target);
                        if (cropResult.fail) return true;
                        tmp.Add(Tuple.Create(cropResult.cropStructure.Id, "crop"));
                        ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Added crop structure ({cropResult.Item2.Id}) to stack");

                        (bool fail, Structure overlapStructure) overlapRresult = CreateOverlapStructure(target, i);
                        if (overlapRresult.fail) return true;
                        tmp.Add(Tuple.Create(overlapRresult.overlapStructure.Id, "overlap"));
                        ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Added overlap structure ({overlapRresult.Item2.Id}) to stack");

                        foreach (string itr in cropAndOverlapStructures)
                        {
                            Structure normal = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), itr.ToLower()));
                            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved normal structure: {normal.Id}");

                            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Contouring overlap between structure ({itr}) and target ({target.Id})");
                            (bool fail, StringBuilder errorMessage) cropAndContourOverlapResult = ContourHelper.ContourOverlapAndUnion(normal, target, overlapRresult.overlapStructure, selectedSS, 0.0);
                            if (cropAndContourOverlapResult.fail)
                            {
                                ProvideUIUpdate(cropAndContourOverlapResult.errorMessage.ToString(), true);
                                return true;
                            }

                            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Cropping structure ({itr}) from target ({target.Id})");
                            (bool failCrop, StringBuilder errorCropMessage) = ContourHelper.CropStructureFromStructure(cropResult.cropStructure, normal, 0.0);
                            if (failCrop)
                            {
                                ProvideUIUpdate(errorCropMessage.ToString());
                                return true;
                            }
                        }
                        normVolumes.Add(Tuple.Create(sortedPrescriptions.ElementAt(i).Item1, cropResult.Item2.Id));
                        targetManipulations.Add(Tuple.Create(sortedPrescriptions.ElementAt(i).Item1, target.Id, tmp));
                    }
                    else ProvideUIUpdate($"Could not retrieve ts target: {targetId}");
                }
            }
            else ProvideUIUpdate(100, "No structures remaining to crop and contour overlap with structures! Skipping!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }
        #endregion

        #region recontour the brain spine targets
        private bool RegeneratePTVBrainSpine()
        {
            UpdateUILabel("Regenerating PTV Spine/PTV Brain:");
            ProvideUIUpdate("Regenerating PTV Spine/PTV Brain:");
            int percentComplete = 0;
            int calcItems = 9;
            Structure ptvBrain = StructureTuningHelper.GetStructureFromId("PTV_Brain", selectedSS, true);
            Structure ptvSpine = StructureTuningHelper.GetStructureFromId("PTV_Spine", selectedSS, true);
            if(ptvBrain == null || ptvSpine == null)
            {
                ProvideUIUpdate($"Error! PTV_Brain or PTV_Spine are null! Fix and try again!", true);
                return true;
            }
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved structure: {ptvBrain.Id}");
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved structure: {ptvSpine.Id}");

            if (ptvBrain.ApprovalHistory.First().ApprovalStatus == StructureApprovalStatus.Approved || ptvSpine.ApprovalHistory.First().ApprovalStatus == StructureApprovalStatus.Approved)
            {
                ProvideUIUpdate($"Error! PTV_Brain or PTV_Spine are approved and I can't modify them! Fix and try again!", true);
                return true;
            }
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Verified approval status of {ptvBrain.Id} and {ptvSpine.Id}");

            int cutSlice = -1;
            Structure cutStructure = StructureTuningHelper.GetStructureFromId("brain", selectedSS);
            double cutPos = 0.0;
            if(cutStructure == null || cutStructure.IsEmpty)
            {
                cutStructure = StructureTuningHelper.GetStructureFromId("spinal_cord", selectedSS);
                if (cutStructure == null) cutStructure = StructureTuningHelper.GetStructureFromId("spinalcord", selectedSS);
                if (cutStructure == null || cutStructure.IsEmpty)
                {
                    //give up
                    ProvideUIUpdate($"Error! Brain/Spinal cord structures are null or empty! Fix and try again!", true);
                    return true;
                }
                else cutPos = cutStructure.MeshGeometry.Positions.Max(p => p.Z);
            }
            else cutPos = cutStructure.MeshGeometry.Positions.Min(p => p.Z);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved structure used to determine cut plan: {cutStructure.Id}");
            ProvideUIUpdate($"Dicom origin ({selectedSS.Image.Origin.x:0.0}, {selectedSS.Image.Origin.y:0.0}, {selectedSS.Image.Origin.z:0.0}) mm");
            ProvideUIUpdate($"Image z resolution: {selectedSS.Image.ZRes} mm");
            ProvideUIUpdate($"Number of z slices: {selectedSS.Image.ZSize}");
            ProvideUIUpdate($"Z cut position: {cutPos:0.0} mm");

            cutSlice = CalculationHelper.ComputeSlice(cutPos, selectedSS);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Z cut slice: {cutSlice}");

            Structure csiInitTarget = StructureTuningHelper.GetStructureFromId(TargetsHelper.GetHighestRxTargetIdForPlan(prescriptions, prescriptions.First().Item1), selectedSS);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved structure: {csiInitTarget.Id}");
            if(!ptvSpine.IsEmpty) ClearContourPointsFromAllPlans(ptvSpine);
            if(!ptvBrain.IsEmpty) ClearContourPointsFromAllPlans(ptvBrain);

            //stop slice for ptv spine is the cut plan
            ContourStructure(ptvSpine, csiInitTarget, CalculationHelper.ComputeSlice(csiInitTarget.MeshGeometry.Positions.Min(p => p.Z), selectedSS), cutSlice);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Contoured structure: {ptvSpine.Id}");
            ////start slice for ptv brain is the cut plan
            ContourStructure(ptvBrain, csiInitTarget, cutSlice, CalculationHelper.ComputeSlice(csiInitTarget.MeshGeometry.Positions.Max(p => p.Z), selectedSS));
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Contoured structure: {ptvBrain.Id}");
            return false;
        }

        private bool ClearContourPointsFromAllPlans(Structure structToRemove)
        {
            ProvideUIUpdate($"Removing structure: {structToRemove.Id}");
            int startSlice = CalculationHelper.ComputeSlice(structToRemove.MeshGeometry.Positions.Min(p => p.Z), selectedSS);
            int stopSlice = CalculationHelper.ComputeSlice(structToRemove.MeshGeometry.Positions.Max(p => p.Z), selectedSS);
            ProvideUIUpdate($"Start slice: {startSlice}");
            ProvideUIUpdate($"Stop slice: {stopSlice}");
            int percentComplete = 0;
            int calcItems = stopSlice - startSlice;
            for (int slice = startSlice; slice <= stopSlice; slice++)
            {
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems));
                if (structToRemove.GetContoursOnImagePlane(slice).Any()) structToRemove.ClearAllContoursOnImagePlane(slice);
            }
            return false;
        }

        private bool ContourStructure(Structure structToContour, Structure baseStructure, int startSlice, int stopSlice)
        {
            ProvideUIUpdate($"Contouring structure: {structToContour.Id}");
            ProvideUIUpdate($"Base structure: {baseStructure.Id}");
            ProvideUIUpdate($"Start slice: {startSlice}");
            ProvideUIUpdate($"Stop slice: {stopSlice}");
            int percentComplete = 0;
            int calcItems = stopSlice - startSlice;
            for(int slice = startSlice; slice <= stopSlice; slice++)
            {
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems));
                VVector[][] pts = baseStructure.GetContoursOnImagePlane(slice);
                if (pts.Any())
                {
                    for (int i = 0; i < pts.GetLength(0); i++)
                    {
                        if(structToContour.IsPointInsideSegment(pts[i][0]) || structToContour.IsPointInsideSegment(pts[i][pts[i].GetLength(0) - 1]))structToContour.SubtractContourOnImagePlane(pts[i], slice);
                        else structToContour.AddContourOnImagePlane(pts[i], slice);
                    }
                }
            }
            return false;
        }
        #endregion

        #region Isocenter Calculation
        protected override bool CalculateNumIsos()
        {
            UpdateUILabel("Calculating Number of Isocenters:");
            int calcItems = 1;
            int counter = 0;
            //For these cases the maximum number of allowed isocenters is 3. One isocenter is reserved for the brain and either one or two isocenters are used for the spine (depending on length).
            //revised to get the number of unique plans list, for each unique plan, find the target with the greatest z-extent and determine the number of isocenters based off that target. 
            //plan Id, list of targets assigned to that plan

            List<Tuple<string, List<string>>> planIdTargets = new List<Tuple<string, List<string>>>(TargetsHelper.GetTargetListForEachPlan(prescriptions));
            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Generated list of plans each containing list of targets");

            foreach (Tuple<string, List<string>> itr in planIdTargets)
            {
                calcItems = itr.Item2.Count;
                counter = 0;
                //determine for each plan which target has the greatest z-extent
                (bool fail, Structure longestTargetInPlan, double maxTargetLength, StringBuilder errorMessage) = TargetsHelper.GetLongestTargetInPlan(itr, selectedSS);
                if(fail)
                {
                    ProvideUIUpdate($"Error! No structure named: {errorMessage} found or contoured!", true);
                    return true;
                }
                ProvideUIUpdate($"Determined target with greatest extent: {longestTargetInPlan.Id}, Plan: {itr.Item1}");

                counter = 0;
                calcItems = 3;
                //If the target ID is PTV_CSI, calculate the number of isocenters based on PTV_spine and add one iso for the brain
                //planId, target list
                if (string.Equals(longestTargetInPlan.Id, TargetsHelper.GetHighestRxTargetIdForPlan(prescriptions, prescriptions.First().Item1)))
                {
                    calcItems += 1;
                    //special rules for initial plan,
                    //first, determine the number of isocenters required to treat PTV_Spine
                    //Grab extent of PTV_Spine and add a 2 cm margin to this distance to give 2 cm buffer on the sup portion of the target to ensure adequate coverage/overlap between upper spine field and brain fields
                    (bool isFail, double spineTargetExtent) = GetSpineTargetExtent(ref counter, ref calcItems, 2.0);
                    if (isFail) return true;

                    //Grab the thyroid structure, if it does not exist, add a 50 mm buffer to the field extent (rough estimate of most inferior position of thyroid)
                    //Structure thyroidStruct = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("thyroid"));
                    //if (thyroidStruct == null || thyroidStruct.IsEmpty) numVMATIsos = (int)Math.Ceiling((pts.Max(p => p.Z) - pts.Min(p => p.Z)) / (400.0 + 50.0));
                    //else
                    //{
                    //    //If it exists, grab the minimum z position and subtract this from the ptv_spine extent (the brain fields extend down to the most inferior part of the thyroid)
                    //    Point3DCollection thyroidPts = thyroidStruct.MeshGeometry.Positions;
                    //    numVMATIsos = (int)Math.Ceiling((thyroidPts.Min(p => p.Z) - pts.Min(p => p.Z)) / 400.0);
                    //}

                    //subtract 40 mm from the numerator as the brain fields have a 40 mm inferior margin on the ptv_brain 
                    //Overlap (2 cm) is accounted for in the denominator.
                    double brainInfMargin = 40.0;
                    double minFieldOverlap = 20.0;
                    double maxFieldExtent = 400.0;
                    double numVMATIsosAsDouble = (spineTargetExtent - brainInfMargin) / (maxFieldExtent - minFieldOverlap);
                    ProvideUIUpdate($"Spine target extent: {spineTargetExtent:0.00}");
                    ProvideUIUpdate($"Num VMAT isos as double: {(spineTargetExtent - 40.0) / (400.0 - 20.0):0.00}");
                    if (numVMATIsosAsDouble > 1 && numVMATIsosAsDouble % 1 < 0.1)
                    {
                        ProvideUIUpdate($"Calculated number of vmat isos MOD 1 is < 0.1 (i.e. an extra {0.1 * 38.0:0.0} cm of field is required to cover the spine");
                        numVMATIsosAsDouble = Math.Floor(numVMATIsosAsDouble);
                        ProvideUIUpdate($"Truncating number of isos to {numVMATIsosAsDouble}");
                    }
                    else numVMATIsosAsDouble = Math.Ceiling(numVMATIsosAsDouble);
                    numVMATIsos = (int)numVMATIsosAsDouble;
                    ProvideUIUpdate((int)(100 * ++counter / calcItems));
                    ProvideUIUpdate($"Final calculated number of VMAT isocenters: {numVMATIsos}");

                    //one iso reserved for PTV_Brain
                    numVMATIsos += 1;
                }
                else
                {
                    numVMATIsos = (int)Math.Ceiling(maxTargetLength / (400.0 - 20.0));
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"{numVMATIsos}");
                }
                if (numVMATIsos > 3) numVMATIsos = 3;

                //set isocenter names based on numIsos and numVMATIsos (be sure to pass 'true' for the third argument to indicate that this is a CSI plan(s))
                //plan Id, list of isocenter names for this plan
                isoNames.Add(Tuple.Create(itr.Item1, new List<string>(IsoNameHelper.GetCSIIsoNames(numVMATIsos))));
                ProvideUIUpdate((int)(100 * ++counter / calcItems), "Added isocenter to stack!");
            }
            ProvideUIUpdate($"Required Number of Isocenters: {numVMATIsos}");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        private (bool, double) GetSpineTargetExtent(ref int counter, ref int calcItems, double addedMarginInCm)
        {
            bool fail = false;
            double spineTargetExtent = 0.0;
            Structure spineTarget = StructureTuningHelper.GetStructureFromId("PTV_Spine", selectedSS);
            if (spineTarget == null || spineTarget.IsEmpty)
            {
                ProvideUIUpdate("Error! No structure named PTV_Spine was found or it was empty!", true);
            }
            else
            {
                ProvideUIUpdate((int)(100 * ++counter / calcItems), "Retrieved spinal cord structure");
                Point3DCollection pts = spineTarget.MeshGeometry.Positions;
                //ESAPI default distances are in mm
                spineTargetExtent = (pts.Max(p => p.Z) - pts.Min(p => p.Z)) + addedMarginInCm * 10;
            }
            return (fail, spineTargetExtent);
        }
        #endregion
    }
}