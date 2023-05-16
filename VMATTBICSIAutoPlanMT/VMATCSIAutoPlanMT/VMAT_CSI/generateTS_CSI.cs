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
        public List<Tuple<string, string, List<Tuple<string, string>>>> GetTargetManipulations() { return targetManipulations; }
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
        //planId, lower dose target id, list<manipulation target id, operation>
        private List<Tuple<string, string, List<Tuple<string, string>>>> targetManipulations = new List<Tuple<string, string, List<Tuple<string, string>>>> { };
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
            if (!selectedSS.Structures.Any(x => (string.Equals(x.Id.ToLower(), "spinalcord") || string.Equals(x.Id.ToLower(), "spinal_cord")) && !x.IsEmpty))
            {
                ProvideUIUpdate("Missing brain and/or spine structures! Please add and try again!", true);
                return true;
            }

            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Brain and spinal cord structures exist");
            ProvideUIUpdate(100, "Preliminary checks complete!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        protected bool UnionLRStructures()
        {
            UpdateUILabel("Unioning Structures: ");
            ProvideUIUpdate(0, "Checking for L and R structures to union!");
            List<Tuple<Structure, Structure, string>> structuresToUnion = StructureTuningHelper.CheckStructuresToUnion(selectedSS);
            if (structuresToUnion.Any())
            {
                int calcItems = structuresToUnion.Count;
                int numUnioned = 0;
                foreach (Tuple<Structure, Structure, string> itr in structuresToUnion)
                {
                    (bool fail, StringBuilder output) = StructureTuningHelper.UnionLRStructures(itr, selectedSS);
                    if (!fail) ProvideUIUpdate((int)(100 * ++numUnioned / calcItems), $"Unioned {itr.Item3}");
                    else 
                    { 
                        ProvideUIUpdate(output.ToString(), true); 
                        return true; 
                    }
                }
                ProvideUIUpdate(100, "Structures unioned successfully!");
            }
            else ProvideUIUpdate(100, "No structures to union!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        private bool CheckHighResolution()
        {
            UpdateUILabel("High-Res Structures: ");
            ProvideUIUpdate("Checking for high resolution structures in structure set: ");
            List<Tuple<string, TSManipulationType, double>> highResManipulationList = new List<Tuple<string, TSManipulationType, double>> { };
            foreach (Tuple<string, TSManipulationType, double> itr in TSManipulationList)
            {
                if (itr.Item2 == TSManipulationType.CropTargetFromStructure || itr.Item2 == TSManipulationType.ContourOverlapWithTarget || itr.Item2 == TSManipulationType.CropFromBody)
                {
                    if (selectedSS.Structures.First(x => string.Equals(x.Id, itr.Item1)).IsEmpty)
                    {
                        ProvideUIUpdate($"Requested manipulation of {0}, but {itr.Item1} is empty!", true);
                        return true;
                    }
                    else if (selectedSS.Structures.First(x => string.Equals(x.Id, itr.Item1)).IsHighResolution)
                    {
                        highResManipulationList.Add(itr);
                    }
                }
            }
            //if there are high resolution structures, they will need to be converted to default resolution.
            if (highResManipulationList.Any())
            {
                ProvideUIUpdate("High-resolution structures:");
                foreach (Tuple<string, TSManipulationType, double> itr in highResManipulationList)
                {
                    ProvideUIUpdate($"{itr.Item1}");
                }
                ProvideUIUpdate("Now converting to low-resolution!");
                //convert high res structures queued for TS manipulation to low resolution and update the queue with the resulting low res structure
                if (ConvertHighToLowRes(highResManipulationList)) return true;
                ProvideUIUpdate(100, "Finishing converting high resolution structures to default resolution");
            }
            else ProvideUIUpdate("No high resolution structures in the structure set!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }
        #endregion


        #region Specialized Crop, Boolean, Ring Operations
        private bool ContourInnerOuterStructure(Structure addedStructure, ref int counter)
        {
            int calcItems = 4;
            //all other sub structures
            Structure originalStructure = null;
            double margin = 0.0;
            int pos1 = addedStructure.Id.IndexOf("-");
            if(pos1 == -1) pos1 = addedStructure.Id.IndexOf("+");
            int pos2 = addedStructure.Id.IndexOf("cm");
            if (pos1 != -1 && pos2 != -1)
            {
                string originalStructureId = addedStructure.Id.Substring(0, pos1);
                ProvideUIUpdate((int)(100 * ++counter / calcItems), "Grabbing margin value!");
                if(!double.TryParse(addedStructure.Id.Substring(pos1, pos2 - pos1), out margin))
                {
                    ProvideUIUpdate($"Margin parse failed for sub structure: {addedStructure.Id}!", true);
                    return true;
                }
                ProvideUIUpdate(margin.ToString());

                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Grabbing original structure {originalStructureId}");
                //logic to handle case where the original structure had to be converted to low resolution
                if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains(originalStructureId.ToLower()) && x.Id.ToLower().Contains("_low")) == null) originalStructure = selectedSS.Structures.First(x => x.Id.ToLower().Contains(originalStructureId.ToLower()));
                else originalStructure = selectedSS.Structures.First(x => x.Id.ToLower().Contains(originalStructureId.ToLower()) && x.Id.ToLower().Contains("_low"));

                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Creating {(margin > 0 ? "outer" : "sub")} structure!");
                //convert from cm to mm
                addedStructure.SegmentVolume = originalStructure.Margin(margin * 10);
                if (addedStructure.IsEmpty)
                {
                    ProvideUIUpdate($"{addedStructure.Id} was contoured, but is empty! Removing!");
                    selectedSS.RemoveStructure(addedStructure);
                }
                ProvideUIUpdate(100, $"Finished contouring {addedStructure.Id}");
            }
            else
            {
                ProvideUIUpdate($"Error! I can't find the keywords '-' or '+', and 'cm' in the structure id for: {addedStructure.Id}", true);
                return true;
            }
            return false;
        }

        public bool ContourOverlapAndUnion(Structure target, Structure normal, Structure unionStructure, double marginInCm)
        {
            bool fail = false;
            //margin is in cm
            if (target != null && normal != null)
            {
                if (marginInCm >= -5.0 && marginInCm <= 5.0)
                {
                    Structure dummy = selectedSS.AddStructure("CONTROL", "Dummy");
                    dummy.SegmentVolume = target.And(normal.Margin(marginInCm * 10));
                    unionStructure.SegmentVolume = unionStructure.Or(dummy.Margin(0.0));
                    selectedSS.RemoveStructure(dummy);
                }
                else
                {
                    ProvideUIUpdate("Added margin MUST be within +/- 5.0 cm!", true);
                    fail = true;
                }
            }
            else
            {
                ProvideUIUpdate("Error either target or normal structures are missing! Can't contour overlap between target and normal structure!", true);
                fail = true;
            }
            return fail;
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
                    Structure target = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id,itr.Item1));
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
            int startSlice = (int)((mesh.Bounds.Z - selectedSS.Image.Origin.z) / selectedSS.Image.ZRes) - 5;
            int stopSlice = (int)(((mesh.Bounds.Z + mesh.Bounds.SizeZ) - selectedSS.Image.Origin.z) / selectedSS.Image.ZRes) + 5;
            calcItems += stopSlice - startSlice - 1;
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Number of slices to contour: {stopSlice - startSlice}");
            if(addedStructure.CanEditSegmentVolume(out string error))
            {
                for (int slice = startSlice; slice < stopSlice; slice++)
                {
                    ProvideUIUpdate((int)(100 * ++percentComplete / calcItems));
                    VVector[][] points = target.GetContoursOnImagePlane(slice);
                    //we only want the outer contour points of the target
                    addedStructure.AddContourOnImagePlane(GenerateContourPoints(points[0], (margin + thickness) * 10), slice);
                    addedStructure.SubtractContourOnImagePlane(GenerateContourPoints(points[0], margin * 10), slice);
                }
            }
            else
            {
                ProvideUIUpdate($"Could not create partial ring for {addedStructure.Id} because: {error}");
                fail = true;
            }
            return (fail, sb);
        }

        private VVector[] GenerateContourPoints(VVector[] points, double distance)
        {
            VVector[] newPoints = new VVector[points.GetLength(0)];
            //ProvideUIUpdate("New Slice");
            double centerX = (points.Max(p => p.x) + points.Min(p => p.x)) / 2;
            double centerY = (points.Max(p => p.y) + points.Min(p => p.y)) / 2;
            for (int i = 0; i < points.GetLength(0); i++)
            {
                double r = Math.Sqrt(Math.Pow(points[i].x - centerX, 2) + Math.Pow(points[i].y - centerY, 2));
                VVector u = new VVector((points[i].x - centerX) / r, (points[i].y - centerY) / r, 0);
                newPoints[i] = new VVector(u.x * (r + distance) + centerX, u.y * (r + distance) + centerY, 0);
                //ProvideUIUpdate($"{points[i][j].x - centerX:0.00}, {points[i][j].y - centerY:0.00}, {r:0.00}, {u.x:0.00}, {u.y:0.00}, {centerX:0.00}, {centerY:0.00}");
            }
            return newPoints;
        }

        private bool GenerateTSGlobesLenses(Structure addedStructure)
        {
            int counter = 0;
            int calcItems = 4;
            bool isGlobes = false;
            if (addedStructure.Id.ToLower().Contains("globes")) isGlobes = true;

            //try to grab ptv_brain first
            //Structure targetStructure = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "ptv_brain") && !x.IsEmpty);
            Structure targetStructure = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "ptv_csi") && !x.IsEmpty);
            double margin = 0;

            if (targetStructure == null)
            {
                ProvideUIUpdate((int)(100 * ++counter / calcItems), "Failed to retrieve PTV_CSI to generate partial ring! Exiting!");
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
                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Retrieved PTV_CSI target: {targetStructure.Id}");
                Structure normal = null;
                if (isGlobes) normal = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("globes") && !x.IsEmpty);
                else normal = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("lenses") && !x.IsEmpty);

                if (normal != null)
                {
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Retrieved structure: {normal.Id}");
                    ProvideUIUpdate($"Generating ring {addedStructure.Id} for target {targetStructure.Id}");
                    //margin in cm. 
                    double thickness = 0;
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
                if (!addedStructures.Where(x => string.Equals(x.ToLower(), itr.Item2)).Any()) AddTSStructures(itr);
            }

            ProvideUIUpdate(100, "Finished adding tuning structures!");
            ProvideUIUpdate(0, "Contouring tuning structures!");
            //now contour the various structures
            foreach (string itr in addedStructures.Where(x => !x.ToLower().Contains("ctv") && !x.ToLower().Contains("ptv")))
            {
                counter = 0;
                ProvideUIUpdate(0, $"Contouring TS: {itr}");
                Structure addedStructure = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), itr.ToLower()));
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
                    if (ContourInnerOuterStructure(addedStructure, ref counter)) return true;
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
            Structure lungs = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "lungs") && !x.IsEmpty);
            Structure body = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "body") && !x.IsEmpty);
            if(lungs == null || body == null)
            {
                ProvideUIUpdate("Error! Body and/or lungs structures were not found or are empty! Exiting!", true);
                return true;
            }
            MeshGeometry3D mesh = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "ptv_csi")).MeshGeometry;
            //get most inferior slice of ptv_csi (mesgeometry.bounds.z indicates the most inferior part of a structure)
            int startSlice = (int)((mesh.Bounds.Z - selectedSS.Image.Origin.z) / selectedSS.Image.ZRes);
            //only go to the most superior part of the lungs for contouring the arms
            int stopSlice = (int)((lungs.MeshGeometry.Positions.Max(p => p.Z) - selectedSS.Image.Origin.z) / selectedSS.Image.ZRes) + 1;

            //initialize variables
            double xMax = 0;
            double xMin = 0;
            double yMax = 0;
            double yMin = 0;
            VVector[][] bodyPts;
            //generate two dummy structures (L and R)
            if (selectedSS.Structures.Where(x => string.Equals(x.Id.ToLower(), "dummyboxl")).Any()) selectedSS.RemoveStructure(selectedSS.Structures.First(x => string.Equals(x.Id.ToLower(), "dummyboxl")));
            Structure dummyBoxL = selectedSS.AddStructure("CONTROL", "DummyBoxL");
            if (selectedSS.Structures.Where(x => string.Equals(x.Id.ToLower(), "dummyboxr")).Any()) selectedSS.RemoveStructure(selectedSS.Structures.First(x => string.Equals(x.Id.ToLower(), "dummyboxr")));
            Structure dummyBoxR = selectedSS.AddStructure("CONTROL", "DummyBoxR");
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

            for (int slice = startSlice; slice < stopSlice; slice++)
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
            (bool fail, StringBuilder errorMessage) = ContourHelper.CropStructureFromBody(armsAvoid, selectedSS, 0.5);
            if (fail)
            {
                ProvideUIUpdate(errorMessage.ToString());
                return true;
            }

            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Cleaning up!");
            selectedSS.RemoveStructure(dummyBoxR);
            selectedSS.RemoveStructure(dummyBoxL);
            ProvideUIUpdate(100, "Finished contouring arms avoid!");
            return false;
        }

        private bool IsOverlap(Structure target, Structure normal, double marginInCm)
        {
            bool isOverlap = false;
            Structure dummy = selectedSS.AddStructure("CONTROL", "Dummy");
            dummy.SegmentVolume = target.And(normal.Margin(marginInCm * 10.0));
            if (!dummy.IsEmpty) isOverlap = true;
            selectedSS.RemoveStructure(dummy);
            return isOverlap;
        }

        private Structure GetTSTarget(string targetId)
        {
            string newName = $"TS_{targetId}";
            if (newName.Length > 16) newName = newName.Substring(0, 16);
            ProvideUIUpdate($"Retrieving TS target: {newName}");
            Structure addedTSTarget = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id, newName));
            if (addedTSTarget == null)
            {
                ProvideUIUpdate($"TS target {newName} does not exist. Creating it now!");
                addedTSTarget = AddTSStructures(new Tuple<string, string>("CONTROL", newName));
                ProvideUIUpdate($"Copying target {targetId} contours onto {newName}");
                addedTSTarget.SegmentVolume = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id, targetId)).Margin(0.0);
            }
            return addedTSTarget;
        }

        private bool PerformTSStructureManipulation()
        {
            UpdateUILabel("Perform TS Manipulations: ");
            int counter = 0;
            int calcItems = TSManipulationList.Count * prescriptions.Count;
            foreach (Tuple<string, string, int, DoseValue, double> itr in prescriptions)
            {
                //create a new TS target for optimization and copy the original target structure onto the new TS structure
                Structure addedTSTarget = GetTSTarget(itr.Item2);
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
                        Structure theStructure = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id, itr1.Item1));
                        if(itr1.Item2 == TSManipulationType.CropFromBody)
                        {
                            ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Cropping {itr1.Item1} from Body with margin {itr1.Item3} cm");
                            //crop from body
                            (bool failOp, StringBuilder errorOpMessage) = ContourHelper.CropStructureFromBody(theStructure, selectedSS, itr1.Item3);
                            if (failOp)
                            {
                                ProvideUIUpdate(errorOpMessage.ToString());
                                return true;
                            }
                        }
                        else if(itr1.Item2 == TSManipulationType.CropTargetFromStructure)
                        {
                            ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Cropping target {addedTSTarget.Id} from {itr1.Item1} with margin {itr1.Item3} cm");
                            //crop target from structure
                            (bool failCrop, StringBuilder errorCropMessage) = ContourHelper.CropTargetFromStructure(addedTSTarget, theStructure, itr1.Item3);
                            if (failCrop)
                            {
                                ProvideUIUpdate(errorCropMessage.ToString());
                                return true;
                            }
                        }
                        else if(itr1.Item2 == TSManipulationType.ContourOverlapWithTarget)
                        {
                            ProvideUIUpdate($"Contouring overlap between {itr1.Item1} and {addedTSTarget.Id}");
                            string overlapName = $"ts_{itr1.Item1}&&{itr.Item2}";
                            if (overlapName.Length > 16) overlapName = overlapName.Substring(0, 16);
                            Structure addedTSNormal = AddTSStructures(new Tuple<string, string>("CONTROL", overlapName));
                            Structure originalNormal = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id, itr1.Item1));
                            addedTSNormal.SegmentVolume = originalNormal.Margin(0.0);
                            (bool failOverlap, StringBuilder errorOverlapMessage) = ContourHelper.ContourOverlap(addedTSTarget, addedTSNormal, itr1.Item3);
                            if (failOverlap)
                            {
                                ProvideUIUpdate(errorOverlapMessage.ToString());
                                return true;
                            }
                            if (addedTSNormal.IsEmpty)
                            {
                                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"{overlapName} was contoured, but it's empty! Removing!");
                                selectedSS.RemoveStructure(addedTSNormal);
                            }
                            else ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Finished contouring {overlapName}");
                        }
                    }
                }
                else ProvideUIUpdate("No TS manipulations requested!");
                normVolumes.Add(Tuple.Create(itr.Item1, addedTSTarget.Id));
            }
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
                Structure normal = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), itr.ToLower()));
                if (normal != null)
                {
                    ProvideUIUpdate((int)(100 * ++percentCompletion / calcItems), $"Retrieved normal structure: {normal.Id}");
                    //verify structures requested for cropping target from structure actually overlap with structure
                    //planid, targetid
                    foreach (Tuple<string, string> itr1 in tgts)
                    {
                        Structure target = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), itr1.Item2.ToLower()));
                        if (target != null)
                        {
                            ProvideUIUpdate((int)(100 * ++percentCompletion / calcItems), $"Retrieved target structure: {target.Id}");
                            if (!IsOverlap(target, normal, 0.0))
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
                    Structure target = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), targetId.ToLower()));
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
                            if (ContourOverlapAndUnion(normal, target, overlapRresult.overlapStructure, 0.0)) return true;

                            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Cropping structure ({itr}) from target ({target.Id})");
                            (bool failCrop, StringBuilder errorCropMessage) = ContourHelper.CropTargetFromStructure(cropResult.cropStructure, normal, 0.0);
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

        #region Isocenter Calculation
        protected bool CalculateNumIsos()
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
                if (string.Equals(longestTargetInPlan.Id.ToLower(), "ptv_csi"))
                {
                    calcItems += 1;
                    //special rules for initial plan, which should have a target named PTV_CSI
                    //first, determine the number of isocenters required to treat PTV_Spine
                    //
                    //2/10/2023 according to Nataliya, PTV_Spine might not be present in the structure set 100% of the time. Therefore, just grab the spinal cord structure and add the margins
                    //used to create PTV_Spine (0.5 cm Ant and 1.5 cm Inf) manually.
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
                    //
                    //subtract 50 mm from the numerator as ptv_spine extends 10 mm into ptv_brain and brain fields have a 40 mm inferior margin on the ptv_brain (10 + 40 = 50 mm). 
                    //Overlap is accounted for in the denominator.
                    numVMATIsos = (int)Math.Ceiling((spineTargetExtent - 50.0) / (400.0 - 20.0));
                    ProvideUIUpdate((int)(100 * ++counter / calcItems));
                    ProvideUIUpdate($"Spine target extent: {spineTargetExtent:0.0}");
                    ProvideUIUpdate($"Num VMAT isos as double: {(spineTargetExtent - 50.0) / (400.0 - 20.0):0.0}");
                    ProvideUIUpdate($"Calculated num VMAT isos: {numVMATIsos:0.0}");

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
                isoNames.Add(Tuple.Create(itr.Item1, new List<string>(IsoNameHelper.GetIsoNames(numVMATIsos, numVMATIsos, true))));
                ProvideUIUpdate((int)(100 * ++counter / calcItems), "Added isocenter to stack!");
            }
            ProvideUIUpdate($"Required Number of Isocenters: {numVMATIsos}");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        private (bool, double) GetSpineTargetExtent(ref int counter, ref int calcItems, double addedMarginInCm)
        {
            bool fail = false;
            Structure spineTarget = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "spinalcord") || string.Equals(x.Id.ToLower(), "spinal_cord"));
            double spineTargetExtent = 0.0;
            if (spineTarget == null || spineTarget.IsEmpty)
            {
                ProvideUIUpdate("Error! No structure named spinalcord or spinal_cord was found or it was empty!", true);
            }
            else
            {
                ProvideUIUpdate((int)(100 * ++counter / calcItems), "Retrieved spinal cord structure");
                Point3DCollection pts = spineTarget.MeshGeometry.Positions;
                //ESAPI default distances are in mm
                double addedMargin = addedMarginInCm * 10;
                spineTargetExtent = (pts.Max(p => p.Z) - pts.Min(p => p.Z)) + addedMargin;
            }
            return (fail, spineTargetExtent);
        }
        #endregion
    }
}