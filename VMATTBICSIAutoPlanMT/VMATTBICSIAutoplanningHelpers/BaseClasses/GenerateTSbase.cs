using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Media.Media3D;
using SimpleProgressWindow;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.Models;
using TSManipulationType = VMATTBICSIAutoPlanningHelpers.Enums.TSManipulationType;
using System.Text;

namespace VMATTBICSIAutoPlanningHelpers.BaseClasses
{
    public class GenerateTSbase : SimpleMTbase
    {
        //Get methods
        //plan Id, list of isocenter names for this plan
        public List<PlanIsocenterModel> PlanIsocentersList { get; protected set; } = new List<PlanIsocenterModel> { };
        public List<string> AddedStructureIds { get; protected set; } = new List<string> { };
        public List<PlanTargetsModel> PlanTargets { get; protected set; } = new List<PlanTargetsModel> { };
        public List<RequestedTSManipulationModel> TSManipulationList { get; protected set; } = new List<RequestedTSManipulationModel> { };
        //flag to indicate to the main UI that the structure manipulation list needs to be updated
        public bool DoesTSManipulationListRequireUpdating { get; protected set; } = false;
        public string StrackTraceError { get; protected set; } = string.Empty;

        protected StructureSet selectedSS;

        #region virtual methods
        protected virtual bool PreliminaryChecks()
        {
            //specific to each case (TBI or CSI)
            return false;
        }

        protected virtual bool CreateTSStructures()
        {
            //no virtual method implementation as this code really can't be abstracted
            return false;
        }

        protected virtual bool PerformTSStructureManipulation()
        {
            return false;
        }


        protected virtual bool CalculateNumIsos()
        {
            return false;
        }
        #endregion

        #region helper functions related to TS generation and manipulation
        /// <summary>
        /// Helper method to union all identified left and right structures
        /// </summary>
        /// <returns></returns>
        protected bool UnionLRStructures()
        {
            UpdateUILabel("Unioning Structures: ");
            ProvideUIUpdate(0, "Checking for L and R structures to union!");
            List<UnionStructureModel> structuresToUnion = StructureTuningHelper.CheckStructuresToUnion(selectedSS);
            if (structuresToUnion.Any())
            {
                int calcItems = structuresToUnion.Count;
                int numUnioned = 0;
                foreach (UnionStructureModel itr in structuresToUnion)
                {
                    (bool fail, StringBuilder output) = StructureTuningHelper.UnionLRStructures(itr, selectedSS);
                    if (!fail) ProvideUIUpdate(100 * ++numUnioned / calcItems, $"Unioned {itr.ProposedUnionStructureId}");
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

        /// <summary>
        /// Helper method to retrieve a tuning/optimization structure target with id equal to TS_<targetId> or requestedTSTargetId
        /// </summary>
        /// <param name="targetId"></param>
        /// <param name="requestedTSTargetId"></param>
        /// <returns></returns>
        protected Structure GetTSTarget(string targetId, string requestedTSTargetId = "")
        {
            string newName;
            if (string.IsNullOrEmpty(requestedTSTargetId)) newName = $"TS_{targetId}";
            else newName = requestedTSTargetId;
            if (newName.Length > 16) newName = newName.Substring(0, 16);
            ProvideUIUpdate($"Retrieving TS target: {newName}");
            Structure addedTSTarget = StructureTuningHelper.GetStructureFromId(newName, selectedSS);
            if (addedTSTarget == null)
            {
                //left here because of special logic to generate the structure if it doesn't exist
                ProvideUIUpdate($"TS target {newName} does not exist. Creating it now!");
                addedTSTarget = AddTSStructures(new RequestedTSStructureModel("CONTROL", newName));
            }
            if(addedTSTarget.IsEmpty)
            {
                if (StructureTuningHelper.DoesStructureExistInSS(targetId, selectedSS, true))
                {
                    ProvideUIUpdate($"Copying target {targetId} contours onto {newName}");
                    (bool fail, StringBuilder errorMessage) = ContourHelper.CopyStructureOntoStructure(StructureTuningHelper.GetStructureFromId(targetId, selectedSS), addedTSTarget);
                    if (fail) ProvideUIUpdate($"Error! Could not copy {targetId} onto {addedTSTarget.Id} because: {errorMessage}", true);
                }
                else ProvideUIUpdate($"Error! Could not retrieve {targetId} structure!", true);
            }
            return addedTSTarget;
        }

        /// <summary>
        /// Helper method to direct the manipulation of the tuning structures
        /// </summary>
        /// <param name="manipulationItem"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        protected bool ManipulateTuningStructures(RequestedTSManipulationModel manipulationItem, Structure target)
        {
            Structure theStructure = StructureTuningHelper.GetStructureFromId(manipulationItem.StructureId, selectedSS);
            if (manipulationItem.ManipulationType == TSManipulationType.CropFromBody)
            {
                ProvideUIUpdate($"Cropping {manipulationItem.StructureId} from Body with margin {manipulationItem.MarginInCM} cm");
                //crop from body
                (bool failOp, StringBuilder errorOpMessage) = ContourHelper.CropStructureFromBody(theStructure, selectedSS, manipulationItem.MarginInCM);
                if (failOp)
                {
                    ProvideUIUpdate(errorOpMessage.ToString());
                    return true;
                }
            }
            else if (manipulationItem.ManipulationType == TSManipulationType.CropTargetFromStructure)
            {
                ProvideUIUpdate($"Cropping target {target.Id} from {manipulationItem.StructureId} with margin {manipulationItem.MarginInCM} cm");
                //crop target from structure
                (bool failCrop, StringBuilder errorCropMessage) = ContourHelper.CropStructureFromStructure(target, theStructure, manipulationItem.MarginInCM);
                if (failCrop)
                {
                    ProvideUIUpdate(errorCropMessage.ToString());
                    return true;
                }
            }
            else if (manipulationItem.ManipulationType == TSManipulationType.ContourOverlapWithTarget)
            {
                if (CreateOverlapStructure(target, theStructure, manipulationItem.MarginInCM)) return true;
            }
            else if (manipulationItem.ManipulationType == TSManipulationType.ContourSubStructure || manipulationItem.ManipulationType == TSManipulationType.ContourOuterStructure)
            {
                if (ContourInnerOuterStructure(theStructure, manipulationItem.MarginInCM)) return true;
            }
            return false;
        }

        /// <summary>
        /// Helper method to create an overlap structure, copy the OAR onto the overlap structure, then contour the overlap between overlap structure 
        /// and the target. Once contoured a check is performed to ensure that the overlap structure is not empty
        /// </summary>
        /// <param name="target"></param>
        /// <param name="OAR"></param>
        /// <param name="margin"></param>
        /// <returns></returns>
        private bool CreateOverlapStructure(Structure target, Structure OAR, double margin)
        {
            int percentComplete = 0;
            int calcItems = 5;
            ProvideUIUpdate($"Contouring overlap between {OAR.Id} and {target.Id}");
            string overlapName = $"ts_{OAR.Id}&&{target.Id}";
            if (overlapName.Length > 16) overlapName = overlapName.Substring(0, 16);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Overlap structure Id: {overlapName}");
            //add a new structure (default resolution by default)
            if (selectedSS.CanAddStructure("CONTROL", overlapName))
            {
                Structure overlapStructure = AddTSStructures(new RequestedTSStructureModel("CONTROL", overlapName));
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Created empty structure {overlapName}");

                (bool copyFail, StringBuilder errorMessage) = ContourHelper.CopyStructureOntoStructure(OAR, overlapStructure);
                if(copyFail)
                {
                    ProvideUIUpdate(errorMessage.ToString(), true);
                    return true;
                }
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Copied {OAR.Id} onto {overlapName}");

                (bool failOverlap, StringBuilder errorOverlapMessage) = ContourHelper.ContourOverlap(target, overlapStructure, margin);
                if (failOverlap)
                {
                    ProvideUIUpdate(errorOverlapMessage.ToString());
                    return true;
                }
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Contoured overlap between {target.Id} and {overlapName}");

                if (overlapStructure.IsEmpty)
                {
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, $"{overlapName} was contoured, but it's empty! Removing!");
                    selectedSS.RemoveStructure(overlapStructure);
                }
                else ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Finished contouring {overlapName}");
            }
            else
            {
                ProvideUIUpdate($"Error! Cannot add new structure: {overlapName}!\nCorrect this issue and try again!", true);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Simple method to identify the index of the supplied TS manipulation item in the TS manipulation list, remove it, and update the list with
        /// the same item except the structure id is swapped with the low resolution structure equivalent
        /// </summary>
        /// <param name="highResManipulationItem"></param>
        /// <param name="lowResId"></param>
        /// <returns></returns>
        private bool UpdateManipulationList(RequestedTSManipulationModel highResManipulationItem, string lowResId)
        {
            bool fail = false;
            //get the index of the high resolution structure in the TS Manipulation list and repace this entry with the newly created low resolution structure
            int index = TSManipulationList.IndexOf(highResManipulationItem);
            if (index != -1)
            {
                TSManipulationList.RemoveAt(index);
                TSManipulationList.Insert(index, 
                                          new RequestedTSManipulationModel(lowResId, highResManipulationItem.ManipulationType, highResManipulationItem.MarginInCM));
            }
            else fail = true;
            return fail;
        }

        /// <summary>
        /// Simple helper method to create an inner/outer structure. Analogous to the margin for structure tool in contouring
        /// </summary>
        /// <param name="originalStructure"></param>
        /// <param name="margin"></param>
        /// <returns></returns>
        protected bool ContourInnerOuterStructure(Structure originalStructure, double margin)
        {
            int counter = 0;
            int calcItems = 2;
            //all other sub structures
            ProvideUIUpdate(100 * ++counter / calcItems, $"Creating {(margin > 0 ? "outer" : "sub")} structure!");
            (bool fail, Structure addedStructure) = RemoveAndGenerateStructure($"{originalStructure.Id}{(margin > 0 ? "+" : "-")}{Math.Abs(margin):0.0}cm");
            if (fail) return true;
            //convert from cm to mm
            addedStructure.SegmentVolume = originalStructure.Margin(margin * 10);
            if (addedStructure.IsEmpty)
            {
                ProvideUIUpdate(100 * ++counter / calcItems, $"{addedStructure.Id} was contoured, but is empty! Removing!");
                selectedSS.RemoveStructure(addedStructure);
            }
            else ProvideUIUpdate(100, $"Finished contouring {addedStructure.Id}");
            return false;
        }

        /// <summary>
        /// Helper method to evaluate the structure manipulation list for empty or high resolution structures. If any 
        /// high resolution structures are identified, convert them to low resolution
        /// </summary>
        /// <returns></returns>
        protected bool CheckHighResolution()
        {
            UpdateUILabel("High-Res Structures: ");
            ProvideUIUpdate("Checking for high resolution structures in structure set: ");
            List<RequestedTSManipulationModel> highResManipulationList = new List<RequestedTSManipulationModel> { };
            foreach (RequestedTSManipulationModel itr in TSManipulationList)
            {
                //only need to check structures that will be involved in crop and contour overlap operations
                if (itr.ManipulationType == TSManipulationType.CropTargetFromStructure || itr.ManipulationType == TSManipulationType.ContourOverlapWithTarget || itr.ManipulationType == TSManipulationType.CropFromBody)
                {
                    Structure tmp = StructureTuningHelper.GetStructureFromId(itr.StructureId, selectedSS);
                    if (tmp.IsEmpty)
                    {
                        ProvideUIUpdate($"Requested manipulation of {0}, but {itr.StructureId} is empty!", true);
                        return true;
                    }
                    else if (tmp.IsHighResolution)
                    {
                        highResManipulationList.Add(itr);
                    }
                }
            }
            //if there are high resolution structures, they will need to be converted to default resolution.
            if (highResManipulationList.Any())
            {
                ProvideUIUpdate("High-resolution structures:");
                foreach (RequestedTSManipulationModel itr in highResManipulationList)
                {
                    ProvideUIUpdate($"{itr.StructureId}");
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

        /// <summary>
        /// Helper method to create an empty low resolution structure with id of <highResStructure.Id>_lowRes
        /// </summary>
        /// <param name="highResStructure"></param>
        /// <returns></returns>
        protected (bool, Structure) CreateLowResStructure(Structure highResStructure)
        {
            Structure lowRes = null;
            bool fail = false;
            string newName = highResStructure.Id + "_lowRes";
            if (newName.Length > 16) newName = newName.Substring(0, 16);
            //add a new structure (default resolution by default)
            if (selectedSS.CanAddStructure("CONTROL", newName)) lowRes = selectedSS.AddStructure("CONTROL", newName);
            else
            {
                ProvideUIUpdate($"Error! Cannot add new structure: {newName}!\nCorrect this issue and try again!", true);
                fail = true;
            }
            return (fail, lowRes);
        }

        /// <summary>
        /// Utility method to convert the structures in the supplied manipulation list from high resolution to low resolution
        /// </summary>
        /// <param name="highResManipulationList"></param>
        /// <returns></returns>
        private bool ConvertHighToLowRes(List<RequestedTSManipulationModel> highResManipulationList)
        {
            int percentComplete = 0;
            int calcItems = highResManipulationList.Count * 5;
            foreach (RequestedTSManipulationModel itr in highResManipulationList)
            {
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieving high resolution structure: {itr.StructureId}");
                //this structure should be present and contoured in structure set (checked previously)
                Structure highResStruct = StructureTuningHelper.GetStructureFromId(itr.StructureId, selectedSS);
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Converting: {itr.StructureId} to low resolution");
                
                //get the high res structure mesh geometry
                MeshGeometry3D mesh = highResStruct.MeshGeometry;
                //get the start and stop image planes for this structure
                int startSlice = CalculationHelper.ComputeSlice(mesh.Positions.Min(p => p.Z), selectedSS);
                int stopSlice = CalculationHelper.ComputeSlice(mesh.Positions.Max(p => p.Z), selectedSS);
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Number of slices to contour: {stopSlice - startSlice + 1}");

                //create an Id for the low resolution struture that will be created. The name will be '_lowRes' appended to the current structure Id
                (bool fail, Structure lowRes) = CreateLowResStructure(highResStruct);
                if (fail) return true;
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Added low-res structure: {lowRes.Id}");
                ProvideUIUpdate($"Contouring {lowRes.Id} now");

                ContourLowResStructure(highResStruct, lowRes, startSlice, stopSlice);
                
                ProvideUIUpdate(100 * ++percentComplete / calcItems, String.Format("Removing existing high-res structure from manipulation list and replacing with low-res"));
                if(UpdateManipulationList(itr, lowRes.Id)) return true;
            }
            //inform the main UI class that the UI needs to be updated
            DoesTSManipulationListRequireUpdating = true;
            return false;
        }

        /// <summary>
        /// Helper method to contour the supplied low resolution structure using the contour points from the supplied 
        /// high resolution structure in the range of startSlice - stopSlice
        /// </summary>
        /// <param name="highResStructure"></param>
        /// <param name="lowRes"></param>
        /// <param name="startSlice"></param>
        /// <param name="stopSlice"></param>
        /// <returns></returns>
        protected bool ContourLowResStructure(Structure highResStructure, Structure lowRes, int startSlice, int stopSlice)
        {
            int percentComplete = 0;
            int calcItems = stopSlice - startSlice + 1;
            //foreach slice that contains contours, get the contours, and determine if you need to add or subtract the contours on the given image plane for the new low resolution structure. You need to subtract contours if the points lie INSIDE the current structure contour.
            //We can sample three points (first, middle, and last points in array) to see if they are inside the current contour. If any of them are, subtract the set of contours from the image plane. Otherwise, add the contours to the image plane. NOTE: THIS LOGIC ASSUMES
            //THAT YOU DO NOT OBTAIN THE CUTOUT CONTOUR POINTS BEFORE THE OUTER CONTOUR POINTS (it seems that ESAPI generally passes the main structure contours first before the cutout contours, but more testing is needed)
            for (int slice = startSlice; slice <= stopSlice; slice++)
            {
                ProvideUIUpdate(100 * ++percentComplete / calcItems);
                VVector[][] points = highResStructure.GetContoursOnImagePlane(slice);
                for (int i = 0; i < points.GetLength(0); i++)
                {
                    if (lowRes.IsPointInsideSegment(points[i][0]) ||
                        lowRes.IsPointInsideSegment(points[i][points[i].GetLength(0) - 1]) ||
                        lowRes.IsPointInsideSegment(points[i][(int)(points[i].GetLength(0) / 2)]))
                    {
                        lowRes.SubtractContourOnImagePlane(points[i], slice);
                    }
                    else lowRes.AddContourOnImagePlane(points[i], slice);
                }
            }
            return false;
        }

        /// <summary>
        /// Simple utility method to check that the supplied list of structure ids can be added as structures to the structure set
        /// </summary>
        /// <param name="structuresToAdd"></param>
        /// <returns></returns>
        protected bool VerifyAddTSStructures(List<RequestedTSStructureModel> structuresToAdd)
        {
            bool fail = false;
            ProvideUIUpdate("Verifying requested TS structures can be added to the structure set!");
            int counter = 0;
            int calcItems = structuresToAdd.Count;
            foreach (RequestedTSStructureModel itr in structuresToAdd)
            {
                if (!selectedSS.CanAddStructure(itr.DICOMType, itr.StructureId))
                {
                    ProvideUIUpdate($"Error! {itr.StructureId} can't be added the structure set!", true);
                    fail = true;
                }
                ProvideUIUpdate(100 * ++counter / calcItems);
            }
            return fail;
        }

        /// <summary>
        /// Simple utility method to remove the supplied list of structures from the structure set
        /// </summary>
        /// <param name="structuresToRemove"></param>
        /// <returns></returns>
        private bool RemoveStructures(List<Structure> structuresToRemove)
        {
            int calcItems = structuresToRemove.Count;
            int counter = 0;
            foreach (Structure itr in structuresToRemove)
            {
                if (selectedSS.CanRemoveStructure(itr))
                {
                    ProvideUIUpdate(100 * ++counter / calcItems, $"Removing: {itr.Id}");
                    selectedSS.RemoveStructure(itr);
                }
                else
                {
                    ProvideUIUpdate($"Error! Could not remove structure: {itr.Id}!", true);
                    if (string.IsNullOrEmpty(itr.DicomType)) ProvideUIUpdate($"{itr.Id} DICOM type: None");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Helper function to remove the tuning structures that will be generated in this run and any existing tuning structures. Once removed
        /// re-add the tuning structures that will be generated in this run to the structure set
        /// </summary>
        /// <param name="structures"></param>
        /// <param name="removeTSTargets"></param>
        /// <returns></returns>
        protected bool RemoveOldTSStructures(List<RequestedTSStructureModel> structures, bool removeTSTargets = false)
        {
            UpdateUILabel("Remove Prior Tuning Structures: ");
            ProvideUIUpdate(0, "Removing prior tuning structures");

            //Get the list of requested ts structures to remove (also add targets if requested)
            List<RequestedTSStructureModel> structuresToRemove;
            if (!removeTSTargets) structuresToRemove = structures.Where(x => !x.StructureId.ToLower().Contains("ctv") && !x.StructureId.ToLower().Contains("ptv")).ToList();
            else structuresToRemove = structures;

            //From the above list, get the list of structures that can actually be removed from the structure set
            (bool fail, List<Structure> removeList) = VerifyRemoveTSStructures(structuresToRemove);
            if (fail) return true;

            ProvideUIUpdate(0, "Adding any remaining tuning structures to the stack");
            //now grab all existing structures in structure set where id starts with 'TS_'
            List<Structure> tsStructs = selectedSS.Structures.Where(x => x.Id.Length > 2 && string.Equals(x.Id.ToLower().Substring(0, 3), "ts_")).ToList();
            //Add the difference between tsStructures and removeList to removeList (i.e., existing ts structures that need to be removed that weren't contained
            //in the requested ts generation structures for this run
            removeList.AddRange(tsStructs.Except(removeList));
            //remove the structures
            if (RemoveStructures(removeList)) return true;

            //now re-add the structurestoremove list of structures to the structure set
            if (VerifyAddTSStructures(structuresToRemove)) return true;
            ProvideUIUpdate(100, "Prior tuning structures successfully removed!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        /// <summary>
        /// Helper method to check the supplied list of structures to see if they can be removed from the structure set. Build a list of structures that
        /// can be removed from the structure set
        /// </summary>
        /// <param name="structuresToRemove"></param>
        /// <returns></returns>
        private (bool, List<Structure>) VerifyRemoveTSStructures(List<RequestedTSStructureModel> structuresToRemove)
        {
            List<Structure> removeList = new List<Structure> { };
            bool fail = false;
            int calcItems = structuresToRemove.Count;
            int counter = 0;
            foreach (RequestedTSStructureModel itr in structuresToRemove)
            {
                Structure tmp = StructureTuningHelper.GetStructureFromId(itr.StructureId, selectedSS);
                //structure is present in selected structure set
                if (tmp != null)
                {
                    //check to see if the dicom type is "none"
                    if (!string.IsNullOrEmpty(tmp.DicomType))
                    {
                        if (selectedSS.CanRemoveStructure(tmp))
                        {
                            ProvideUIUpdate(100 * ++counter / calcItems, $"Adding: {itr.StructureId} to the structure removal list");
                            removeList.Add(tmp);
                        }
                        else
                        {
                            ProvideUIUpdate(0, $"Error! {itr.StructureId} can't be removed from the structure set!", true);
                            fail = true;

                        }
                    }
                    else
                    {
                        ProvideUIUpdate(0, $"{itr.StructureId} is of DICOM type 'None'! ESAPI can't operate on DICOM type 'None'", true);
                        fail = true;
                    }
                }
            }
            return (fail, removeList);
        }

        /// <summary>
        /// Helper method to check if a structure exists, remove it if it does, and generate a new structure with the specified Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        protected (bool, Structure) RemoveAndGenerateStructure(string id)
        {
            bool fail = false;
            Structure theStructure = null;
            if (StructureTuningHelper.DoesStructureExistInSS(id, selectedSS))
            {
                if (selectedSS.CanRemoveStructure(StructureTuningHelper.GetStructureFromId(id, selectedSS)))
                {
                    selectedSS.RemoveStructure(StructureTuningHelper.GetStructureFromId(id, selectedSS));
                }
                else
                {
                    ProvideUIUpdate("Error! Could not add dummy box to structure set to cut target at matchplane! Exiting!", true);
                    fail = true;
                }
            }
            if (!VerifyAddTSStructures(new List<RequestedTSStructureModel> { new RequestedTSStructureModel("CONTROL", id) }))
            {
                theStructure = AddTSStructures(new RequestedTSStructureModel("CONTROL", id));
            }
            else fail = true;
            return (fail, theStructure);
        }

        /// <summary>
        /// Helper method to create a Structure using the supplied dicom type and structure id
        /// </summary>
        /// <param name="itr1"></param>
        /// <returns></returns>
        protected Structure AddTSStructures(RequestedTSStructureModel itr1)
        {
            Structure addedStructure = null;
            string dicomType = itr1.DICOMType;
            string structName = itr1.StructureId;
            if (selectedSS.CanAddStructure(dicomType, structName))
            {
                addedStructure = selectedSS.AddStructure(dicomType, structName);
                AddedStructureIds.Add(structName);
            }
            else ProvideUIUpdate($"Can't add {structName} to the structure set!");
            return addedStructure;
        }

        /// <summary>
        /// Simple helper method to check if the user origin has been assigned and exists inside the patient body
        /// </summary>
        /// <returns></returns>
        protected bool IsUOriginInside()
        {
            if (!selectedSS.Image.HasUserOrigin || 
                !StructureTuningHelper.DoesStructureExistInSS("Body", selectedSS, true) || 
                !StructureTuningHelper.GetStructureFromId("Body", selectedSS).IsPointInsideSegment(selectedSS.Image.UserOrigin))
            {
                ProvideUIUpdate("Did you forget to set the user origin? \nUser origin is NOT inside body contour! \nPlease fix and try again!", true);
                return true;
            }
            return false;
        }
        #endregion
    }
}
