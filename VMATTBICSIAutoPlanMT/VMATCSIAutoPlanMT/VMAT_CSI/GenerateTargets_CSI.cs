﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleProgressWindow;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATCSIAutoPlanMT.VMAT_CSI
{
    public class GenerateTargets_CSI : SimpleMTbase
    {
        public List<string> GetAddedTargetStructures() { return addedTargetIds; }
        public string GetErrorStackTrace() { return stackTraceError; }

        //DICOM types
        //Possible values are "AVOIDANCE", "CAVITY", "CONTRAST_AGENT", "CTV", "EXTERNAL", "GTV", "IRRAD_VOLUME", 
        //"ORGAN", "PTV", "TREATED_VOLUME", "SUPPORT", "FIXATION", "CONTROL", and "DOSE_REGION". 
        private List<Tuple<string, string>> createPrelimTargetList;
        StructureSet selectedSS;
        protected List<string> addedTargetIds = new List<string> { };
        protected string stackTraceError;

        public GenerateTargets_CSI(List<Tuple<string, string>> tgts, StructureSet ss, bool closePW)
        {
            createPrelimTargetList = new List<Tuple<string, string>>(tgts);
            selectedSS = ss;
            SetCloseOnFinish(closePW,3000);
        }

        public override bool Run()
        {
            try
            {
                if (PreliminaryChecks()) return true;
                if (CheckForTargetStructures()) return true;
                UpdateUILabel("Finished!");
                ProvideUIUpdate(100, "Finished Generating Preliminary Targets!");
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

        private bool PreliminaryChecks()
        {
            UpdateUILabel("Performing Preliminary Checks: ");
            int calcItems = 3;
            int counter = 0;

            //verify body structure is present and contour
            if (!StructureTuningHelper.DoesStructureExistInSS("body", selectedSS, true))
            {
                ProvideUIUpdate("Missing body structure! Generating it now!");
                if (GenerateBodyStructure()) return true;
            }
            ProvideUIUpdate(100 * ++counter / calcItems);

            //verify brain and spine structures are present
            if (!StructureTuningHelper.DoesStructureExistInSS("brain", selectedSS, true) || !StructureTuningHelper.DoesStructureExistInSS(new List<string> { "spinal_cord", "spinalcord"}, selectedSS, true))
            {
                ProvideUIUpdate("Missing brain and/or spine structures! Please add and try again!", true);
                return true;
            }
            ProvideUIUpdate(100 * ++counter / calcItems, "Brain and spinal cord structures exist");

            if (CheckHighResolutionAndConvert(new List<string> { "brain", "spinal_cord", "spinalcord" })) return true;
            ProvideUIUpdate(100 * ++counter / calcItems, "Check and converted any high res base targets");

            ProvideUIUpdate(100, "Preliminary checks complete!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        private bool GenerateBodyStructure()
        {
            UpdateUILabel("Generating Body structure:");
            Structure body = selectedSS.CreateAndSearchBody(selectedSS.GetDefaultSearchBodyParameters());
            if (!string.Equals(body.Id, "Body"))
            {
                try
                {
                    body.Id = "Body";
                }
                catch(Exception e)
                {
                    ProvideUIUpdate($"Error. Could not change {body.Id} to 'Body' because {e.Message}", true);
                    return true;
                }
            }
            ProvideUIUpdate($"Body structure generated");
            return false;
        }

        private bool CheckHighResolutionAndConvert(List<string> baseTargets)
        {
            UpdateUILabel("Checking for high res structures:");
            foreach(string itr in baseTargets)
            {
                Structure tmp = StructureTuningHelper.GetStructureFromId(itr, selectedSS);
                if(tmp != null && !tmp.IsEmpty)
                {
                    ProvideUIUpdate($"Checking if {tmp.Id} is high resolution");
                    if(tmp.IsHighResolution)
                    {
                        string id = tmp.Id;
                        ProvideUIUpdate($"{id} is high resolution. Converting to default resolution now");
                        
                        OverWriteHighResStructureWithLowResStructure(tmp);
                        ProvideUIUpdate($"{id} has been converted to low resolution");
                    }
                    else
                    {
                        ProvideUIUpdate($"{tmp.Id} is already defualt resolution");
                    }
                }
            }
            return false;
        }

        private bool OverWriteHighResStructureWithLowResStructure(Structure theStructure)
        {
            ProvideUIUpdate($"Retrieving all contour points for: {theStructure.Id}");
            int startSlice = CalculationHelper.ComputeSlice(theStructure.MeshGeometry.Positions.Min(p => p.Z), selectedSS);
            int stopSlice = CalculationHelper.ComputeSlice(theStructure.MeshGeometry.Positions.Max(p => p.Z), selectedSS);
            ProvideUIUpdate($"Start slice: {startSlice}");
            ProvideUIUpdate($"Stop slice: {stopSlice}");
            VVector[][][] structurePoints = GetAllContourPoints(theStructure, startSlice, stopSlice);
            ProvideUIUpdate($"Contour points for: {theStructure.Id} loaded");
            
            ProvideUIUpdate($"Removing and re-adding {theStructure.Id} to structure set");
            (bool fail, Structure lowResStructure) = RemoveAndReAddStructure(theStructure);
            if (fail) return true;

            ProvideUIUpdate($"Contouring {lowResStructure.Id} now");
            ContourLowResStructure(structurePoints, lowResStructure, startSlice, stopSlice);
            return false;
        }

        private VVector[][][] GetAllContourPoints(Structure theStructure, int startSlice, int stopSlice)
        {
            int percentComplete = 0;
            int calcItems = stopSlice - startSlice + 1;
            VVector[][][] structurePoints = new VVector[stopSlice - startSlice + 1][][];
            for (int slice = startSlice; slice <= stopSlice; slice++)
            {
                structurePoints[percentComplete++] = theStructure.GetContoursOnImagePlane(slice);
                ProvideUIUpdate(100 * percentComplete / calcItems);
            }
            return structurePoints;
        }

        private (bool, Structure) RemoveAndReAddStructure(Structure theStructure)
        {
            UpdateUILabel("Removing and re-adding structure:");
            Structure newStructure = null;
            string id = theStructure.Id;
            string dicomType = theStructure.DicomType;
            if (selectedSS.CanRemoveStructure(theStructure))
            {
                selectedSS.RemoveStructure(theStructure);
                if(selectedSS.CanAddStructure(dicomType,id))
                {
                    newStructure = selectedSS.AddStructure(dicomType, id);
                    ProvideUIUpdate($"{newStructure.Id} has been added to the structure set");
                }
                else
                {
                    ProvideUIUpdate($"Could not re-add structure: {id}. Exiting", true);
                    return (true, newStructure);
                }
            }
            else
            {
                ProvideUIUpdate($"Could not remove structure: {id}. Exiting", true);
                return (true, newStructure);
            }
            return (false, newStructure);
        }

        private bool ContourLowResStructure(VVector[][][] structurePoints, Structure lowResStructure, int startSlice, int stopSlice)
        {
            UpdateUILabel($"Contouring {lowResStructure.Id}:");
            //Write the high res contour points on the newly added low res structure
            int percentComplete = 0;
            int calcItems = stopSlice - startSlice + 1;
            for (int slice = startSlice; slice <= stopSlice; slice++)
            {
                VVector[][] points = structurePoints[percentComplete];
                for (int i = 0; i < points.GetLength(0); i++)
                {
                    if (lowResStructure.IsPointInsideSegment(points[i][0]) ||
                        lowResStructure.IsPointInsideSegment(points[i][points[i].GetLength(0) - 1]) ||
                        lowResStructure.IsPointInsideSegment(points[i][(int)(points[i].GetLength(0) / 2)]))
                    {
                        lowResStructure.SubtractContourOnImagePlane(points[i], slice);
                    }
                    else lowResStructure.AddContourOnImagePlane(points[i], slice);
                }
                ProvideUIUpdate(100 * ++percentComplete / calcItems);
            }
            return false;
        }

        #region Target Creation
        private bool CheckForTargetStructures()
        {
            UpdateUILabel("Checking For Missing Target Structures: ");
            ProvideUIUpdate(0, "Checking for missing target structures!");
            List<Tuple<string, string>> missingTargets = new List<Tuple<string, string>> { };
            int calcItems = createPrelimTargetList.Count;
            int counter = 0;
            foreach (Tuple<string, string> itr in createPrelimTargetList)
            {
                Structure tmp = StructureTuningHelper.GetStructureFromId(itr.Item2, selectedSS);
                if (tmp == null)
                {
                    ProvideUIUpdate($"Target: {itr.Item2} is missing");
                    missingTargets.Add(itr);
                }
                else if (tmp.IsEmpty)
                {
                    ProvideUIUpdate($"Target: {itr.Item2} exists, but is empty");
                    addedTargetIds.Add(tmp.Id);
                }
                else ProvideUIUpdate($"Target: {itr.Item2} is exists and is contoured");
                ProvideUIUpdate(100 * ++counter / calcItems);
            }
            if (missingTargets.Any())
            {
                ProvideUIUpdate("Targets missing from the structure set! Creating them now!");
                if (CreateTargetStructures(missingTargets)) return true;
            }
            if(addedTargetIds.Any())
            {
                ProvideUIUpdate("Contouring targets now");
                if (ContourTargetStructures()) return true;
            }
            ProvideUIUpdate("All requested targets are present and contoured! Skipping target creation!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        private bool CreateTargetStructures(List<Tuple<string, string>> missingTargets)
        {
            UpdateUILabel("Create Missing Target Structures: ");
            ProvideUIUpdate(0, "Creating missing target structures!");
            //create the CTV and PTV structures
            //int calcItems = prospectiveTargets.Count;
            int calcItems = missingTargets.Count;
            int counter = 0;
            foreach (Tuple<string, string> itr in missingTargets)
            {
                if (selectedSS.CanAddStructure(itr.Item1, itr.Item2))
                {
                    addedTargetIds.Add(itr.Item2);
                    selectedSS.AddStructure(itr.Item1, itr.Item2);
                    ProvideUIUpdate(100 * ++counter / calcItems, $"Added target: {itr.Item2}");
                }
                else
                {
                    ProvideUIUpdate($"Can't add {itr.Item2} to the structure set!", true);
                    return true;
                }
            }
            return false;
        }

        private bool ContourTargetStructures()
        {
            Structure tmp = null;
            int calcItems = addedTargetIds.Count + 5;
            int counter = 0;
            foreach (string itr in addedTargetIds.OrderBy(x => x.ElementAt(0)))
            {
                ProvideUIUpdate(100 * ++counter / calcItems, $"Contouring target: {itr}");
                Structure theTarget = StructureTuningHelper.GetStructureFromId(itr, selectedSS);
                if (itr.ToLower().Contains("brain"))
                {
                    tmp = StructureTuningHelper.GetStructureFromId("brain", selectedSS);
                    if (tmp != null && !tmp.IsEmpty)
                    {
                        if (itr.ToLower().Contains("ctv"))
                        {
                            //CTV structure. Brain CTV IS the brain structure
                            theTarget.SegmentVolume = tmp.Margin(0.0);
                        }
                        else
                        {
                            //PTV structure
                            //5 mm uniform margin to generate PTV
                            theTarget.SegmentVolume = tmp.Margin(5.0);
                        }
                    }
                    else
                    {
                        ProvideUIUpdate("Error! Could not retrieve brain structure! Exiting!", true);
                        return true;
                    }
                }
                else if (itr.ToLower().Contains("spine"))
                {
                    tmp = StructureTuningHelper.GetStructureFromId("spinalcord", selectedSS);
                    if (tmp == null) tmp = StructureTuningHelper.GetStructureFromId("spinal_cord", selectedSS);
                    if (tmp != null && !tmp.IsEmpty)
                    {
                        if (itr.ToLower().Contains("ctv"))
                        {
                            //CTV structure. Brain CTV IS the brain structure
                            //AxisAlignedMargins(inner or outer margin, margin from negative x, margin for negative y, margin for negative z, margin for positive x, margin for positive y, margin for positive z)
                            //according to Nataliya: CTV_spine = spinal_cord+0.5cm ANT, +1.5cm Inf, and +1.0 cm in all other directions
                            theTarget.SegmentVolume = tmp.AsymmetricMargin(new AxisAlignedMargins(StructureMarginGeometry.Outer,
                                                                                            10.0,
                                                                                            5.0,
                                                                                            15.0,
                                                                                            10.0,
                                                                                            10.0,
                                                                                            10.0));
                        }
                        else
                        {
                            //PTV structure
                            //5 mm uniform margin to generate PTV
                            tmp = StructureTuningHelper.GetStructureFromId("CTV_Spine", selectedSS);
                            if (tmp != null && !tmp.IsEmpty) theTarget.SegmentVolume = tmp.Margin(5.0);
                            else { ProvideUIUpdate("Error! Could not retrieve CTV_Spine structure! Exiting!", true); return true; }
                        }
                    }
                    else
                    {
                        ProvideUIUpdate("Error! Could not retrieve spinal cord structure! Exiting!", true);
                        return true;
                    }
                }
            }

            if (addedTargetIds.Any(x => string.Equals(x.ToLower(), "ptv_csi")))
            {
                ProvideUIUpdate(100 * ++counter / calcItems, "Generating: PTV_CSI");
                ProvideUIUpdate(100 * ++counter / calcItems, "Retrieving: PTV_CSI, PTV_Brain, and PTV_Spine");
                //used to create the ptv_csi structures
                Structure combinedTarget = StructureTuningHelper.GetStructureFromId("PTV_CSI", selectedSS);
                Structure brainTarget = StructureTuningHelper.GetStructureFromId("PTV_Brain", selectedSS);
                Structure spineTarget = StructureTuningHelper.GetStructureFromId("PTV_Spine", selectedSS);
                ProvideUIUpdate(100 * ++counter / calcItems, "Unioning PTV_Brain and PTV_Spine to make PTV_CSI");
                combinedTarget.SegmentVolume = brainTarget.Margin(0.0);
                combinedTarget.SegmentVolume = combinedTarget.Or(spineTarget.Margin(0.0));

                ProvideUIUpdate(100 * ++counter / calcItems, "Cropping PTV_CSI from body with 3 mm inner margin");
                //1/3/2022, crop PTV structure from body by 3mm
                (bool fail, StringBuilder errorMessage) = ContourHelper.CropStructureFromBody(combinedTarget, selectedSS, -0.3, selectedSS.Structures.First(x => x.Id.ToLower().Contains("body")).Id);
                if (fail)
                {
                    ProvideUIUpdate(errorMessage.ToString());
                    return true;
                }
            }
            else ProvideUIUpdate(100 * ++counter / calcItems, "PTV_CSI already exists in the structure set! Skipping!");
            ProvideUIUpdate(100 * ++counter / calcItems, "Targets added and contoured!");
            return false;
        }
        #endregion
    }
}
