using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Media.Media3D;
using SimpleProgressWindow;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using TSManipulationType = VMATTBICSIAutoPlanningHelpers.Enums.TSManipulationType;
using System.Text;

namespace VMATTBICSIAutoPlanningHelpers.BaseClasses
{
    public class GenerateTSbase : SimpleMTbase
    {
        public List<Tuple<string, List<string>>> GetIsoNames() { return isoNames; }
        public List<string> GetAddedStructures() { return addedStructures; }
        public List<Tuple<string, TSManipulationType, double>> GetSparingList() { return TSManipulationList; }
        public bool GetUpdateSparingListStatus() { return updateTSManipulationList; }
        public string GetErrorStackTrace() { return stackTraceError; }
        private readonly object locker = new object();

        protected StructureSet selectedSS;
        //structure, manipulation type, added margin (if applicable)
        protected List<Tuple<string, TSManipulationType, double>> TSManipulationList;
        protected List<string> addedStructures = new List<string> { };
        protected bool useFlash = false;
        //plan Id, list of isocenter names for this plan
        protected List<Tuple<string,List<string>>> isoNames = new List<Tuple<string, List<string>>> { };
        protected bool updateTSManipulationList = false;
        protected string stackTraceError;

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

        protected Structure GetTSTarget(string targetId)
        {
            string newName = $"TS_{targetId}";
            if (newName.Length > 16) newName = newName.Substring(0, 16);
            ProvideUIUpdate($"Retrieving TS target: {newName}");
            Structure addedTSTarget = StructureTuningHelper.GetStructureFromId(newName, selectedSS);
            if (addedTSTarget == null)
            {
                //left here because of special logic to generate the structure if it doesn't exist
                ProvideUIUpdate($"TS target {newName} does not exist. Creating it now!");
                addedTSTarget = AddTSStructures(new Tuple<string, string>("CONTROL", newName));
                ProvideUIUpdate($"Copying target {targetId} contours onto {newName}");
                addedTSTarget.SegmentVolume = StructureTuningHelper.GetStructureFromId(targetId, selectedSS).Margin(0.0);
            }
            return addedTSTarget;
        }

        protected bool ManipulateTuningStructures(Tuple<string, TSManipulationType, double> manipulationItem, Structure target, ref int counter, ref int calcItems)
        {
            Structure theStructure = StructureTuningHelper.GetStructureFromId(manipulationItem.Item1, selectedSS);
            if (manipulationItem.Item2 == TSManipulationType.CropFromBody)
            {
                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Cropping {manipulationItem.Item1} from Body with margin {manipulationItem.Item3} cm");
                //crop from body
                (bool failOp, StringBuilder errorOpMessage) = ContourHelper.CropStructureFromBody(theStructure, selectedSS, manipulationItem.Item3);
                if (failOp)
                {
                    ProvideUIUpdate(errorOpMessage.ToString());
                    return true;
                }
            }
            else if (manipulationItem.Item2 == TSManipulationType.CropTargetFromStructure)
            {
                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Cropping target {target.Id} from {manipulationItem.Item1} with margin {manipulationItem.Item3} cm");
                //crop target from structure
                (bool failCrop, StringBuilder errorCropMessage) = ContourHelper.CropStructureFromStructure(target, theStructure, manipulationItem.Item3);
                if (failCrop)
                {
                    ProvideUIUpdate(errorCropMessage.ToString());
                    return true;
                }
            }
            else if (manipulationItem.Item2 == TSManipulationType.ContourOverlapWithTarget)
            {
                ProvideUIUpdate($"Contouring overlap between {manipulationItem.Item1} and {target.Id}");
                string overlapName = $"ts_{manipulationItem.Item1}&&{target.Id}";
                if (overlapName.Length > 16) overlapName = overlapName.Substring(0, 16);
                Structure addedTSNormal = AddTSStructures(new Tuple<string, string>("CONTROL", overlapName));
                addedTSNormal.SegmentVolume = theStructure.Margin(0.0);
                (bool failOverlap, StringBuilder errorOverlapMessage) = ContourHelper.ContourOverlap(target, addedTSNormal, manipulationItem.Item3);
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
            else if (manipulationItem.Item2 == TSManipulationType.ContourSubStructure || manipulationItem.Item2 == TSManipulationType.ContourOuterStructure)
            {
                if (ContourInnerOuterStructure(theStructure, manipulationItem.Item3)) return true;
            }
            return false;
        }

        private (bool, Structure) CreateLowResStructure(Structure highResStructure)
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

        private bool ContourLowResStructure(Structure highResStructure, Structure lowRes, int startSlice, int stopSlice)
        {
            ProvideUIUpdate($"Contouring {lowRes.Id} now");
            int counter = 0;
            int calcItems = startSlice - stopSlice - 1;
            //foreach slice that contains contours, get the contours, and determine if you need to add or subtract the contours on the given image plane for the new low resolution structure. You need to subtract contours if the points lie INSIDE the current structure contour.
            //We can sample three points (first, middle, and last points in array) to see if they are inside the current contour. If any of them are, subtract the set of contours from the image plane. Otherwise, add the contours to the image plane. NOTE: THIS LOGIC ASSUMES
            //THAT YOU DO NOT OBTAIN THE CUTOUT CONTOUR POINTS BEFORE THE OUTER CONTOUR POINTS (it seems that ESAPI generally passes the main structure contours first before the cutout contours, but more testing is needed)
            for (int slice = startSlice; slice < stopSlice; slice++)
            {
                ProvideUIUpdate((int)(100 * ++counter / calcItems));
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

        private bool UpdateManipulationList(Tuple<string, TSManipulationType, double> highResManipulationItem, string lowResId)
        {
            bool fail = false;
            //get the index of the high resolution structure in the TS Manipulation list and repace this entry with the newly created low resolution structure
            int index = TSManipulationList.IndexOf(highResManipulationItem);
            if (index != -1)
            {
                TSManipulationList.RemoveAt(index);
                TSManipulationList.Insert(index, new Tuple<string, TSManipulationType, double>(lowResId, highResManipulationItem.Item2, highResManipulationItem.Item3));
            }
            else fail = true;
            return fail;
        }

        protected bool ContourInnerOuterStructure(Structure originalStructure, double margin)
        {
            int counter = 0;
            int calcItems = 2;
            //all other sub structures
            ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Creating {(margin > 0 ? "outer" : "sub")} structure!");
            (bool fail, Structure addedStructure) = CheckAndGenerateStructure($"{originalStructure}{margin:0.0}cm");
            if (fail) return true;
            //convert from cm to mm
            //lock(locker)
            //{
                addedStructure.SegmentVolume = originalStructure.Margin(margin * 10);
           //}
            if (addedStructure.IsEmpty)
            {
                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"{addedStructure.Id} was contoured, but is empty! Removing!");
                selectedSS.RemoveStructure(addedStructure);
            }
            else ProvideUIUpdate(100, $"Finished contouring {addedStructure.Id}");
            return false;
        }

        protected bool ContourInnerOuterStructure(Structure addedStructure)
        {
            int counter = 0;
            int calcItems = 4;
            //all other sub structures
            Structure originalStructure = null;
            double margin = 0.0;
            int pos1 = addedStructure.Id.IndexOf("-");
            if (pos1 == -1) pos1 = addedStructure.Id.IndexOf("+");
            int pos2 = addedStructure.Id.IndexOf("cm");
            if (pos1 != -1 && pos2 != -1)
            {
                string originalStructureId = addedStructure.Id.Substring(0, pos1);
                ProvideUIUpdate((int)(100 * ++counter / calcItems), "Grabbing margin value!");
                if (!double.TryParse(addedStructure.Id.Substring(pos1, pos2 - pos1), out margin))
                {
                    ProvideUIUpdate($"Margin parse failed for sub structure: {addedStructure.Id}!", true);
                    return true;
                }
                ProvideUIUpdate(margin.ToString());

                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Grabbing original structure {originalStructureId}");
                //logic to handle case where the original structure had to be converted to low resolution
                originalStructure = StructureTuningHelper.GetStructureFromId(originalStructureId.ToLower(), selectedSS);
                if (originalStructure == null) originalStructure = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains(originalStructureId.ToLower()) && x.Id.ToLower().Contains("_low"));
                if (originalStructure == null)
                {
                    ProvideUIUpdate($"Warning! Could not retrieve base structure {originalStructureId} to generate {addedStructure.Id}!");
                    ProvideUIUpdate($"Removing {addedStructure.Id}");
                    selectedSS.RemoveStructure(addedStructure);
                    return false;
                }

                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Creating {(margin > 0 ? "outer" : "sub")} structure!");
                //convert from cm to mm
                addedStructure.SegmentVolume = originalStructure.Margin(margin * 10);
                if (addedStructure.IsEmpty)
                {
                    ProvideUIUpdate($"{addedStructure.Id} was contoured, but is empty! Removing!");
                    selectedSS.RemoveStructure(addedStructure);
                }
                else ProvideUIUpdate(100, $"Finished contouring {addedStructure.Id}");
            }
            else
            {
                ProvideUIUpdate($"Error! I can't find the keywords '-' or '+', and 'cm' in the structure id for: {addedStructure.Id}", true);
                return true;
            }
            return false;
        }

        protected bool CheckHighResolution(bool autoConvertToLowRes = true)
        {
            UpdateUILabel("High-Res Structures: ");
            ProvideUIUpdate("Checking for high resolution structures in structure set: ");
            List<Tuple<string, TSManipulationType, double>> highResManipulationList = new List<Tuple<string, TSManipulationType, double>> { };
            foreach (Tuple<string, TSManipulationType, double> itr in TSManipulationList)
            {
                if (itr.Item2 == TSManipulationType.CropTargetFromStructure || itr.Item2 == TSManipulationType.ContourOverlapWithTarget || itr.Item2 == TSManipulationType.CropFromBody)
                {
                    Structure tmp = StructureTuningHelper.GetStructureFromId(itr.Item1, selectedSS);
                    if (tmp.IsEmpty)
                    {
                        ProvideUIUpdate($"Requested manipulation of {0}, but {itr.Item1} is empty!", true);
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

        protected bool ConvertHighToLowRes(List<Tuple<string, TSManipulationType, double>> highResManipulationList)
        {
            int percentComplete = 0;
            int calcItems = highResManipulationList.Count * 5;
            foreach (Tuple<string, TSManipulationType, double> itr in highResManipulationList)
            {
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieving high resolution structure: {itr.Item1}");
                //this structure should be present and contoured in structure set (checked previously)
                Structure highResStruct = StructureTuningHelper.GetStructureFromId(itr.Item1, selectedSS);
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Converting: {itr.Item1} to low resolution");
                
                //get the high res structure mesh geometry
                MeshGeometry3D mesh = highResStruct.MeshGeometry;
                //get the start and stop image planes for this structure
                int startSlice = (int)((mesh.Bounds.Z - selectedSS.Image.Origin.z) / selectedSS.Image.ZRes);
                int stopSlice = (int)(((mesh.Bounds.Z + mesh.Bounds.SizeZ) - selectedSS.Image.Origin.z) / selectedSS.Image.ZRes) + 1;
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Number of slices to contour: {stopSlice - startSlice}");

                //create an Id for the low resolution struture that will be created. The name will be '_lowRes' appended to the current structure Id
                (bool fail, Structure lowRes) = CreateLowResStructure(highResStruct);
                if (fail) return true;
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Added low-res structure: {lowRes.Id}");
                ContourLowResStructure(highResStruct, lowRes, startSlice, stopSlice);
                
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), String.Format("Removing existing high-res structure from manipulation list and replacing with low-res"));
                if(UpdateManipulationList(itr, lowRes.Id)) return true;
            }
            //inform the main UI class that the UI needs to be updated
            updateTSManipulationList = true;
            return false;
        }

        private (bool, List<Structure>) VerifyRemoveTSStructures(List<Tuple<string, string>> structuresToRemove)
        {
            List<Structure> removeList = new List<Structure> { };
            bool fail = false;
            int calcItems = structuresToRemove.Count;
            int counter = 0;
            foreach (Tuple<string, string> itr in structuresToRemove)
            {
                Structure tmp = StructureTuningHelper.GetStructureFromId(itr.Item2, selectedSS);
                //structure is present in selected structure set
                if (tmp != null)
                {
                    //check to see if the dicom type is "none"
                    if (!string.IsNullOrEmpty(tmp.DicomType))
                    {
                        if (selectedSS.CanRemoveStructure(tmp))
                        {
                            ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Adding: {itr.Item2} to the structure removal list");
                            removeList.Add(tmp);
                        }
                        else
                        {
                            ProvideUIUpdate(0, $"Error! {itr.Item2} can't be removed from the structure set!", true);
                            fail = true;

                        }
                    }
                    else
                    {
                        ProvideUIUpdate(0, $"{itr.Item2} is of DICOM type 'None'! ESAPI can't operate on DICOM type 'None'", true);
                        fail = true;
                    }
                }
            }
            return (fail, removeList);
        }

        protected bool VerifyAddTSStructures(List<Tuple<string, string>> structuresToAdd)
        {
            bool fail = false;
            ProvideUIUpdate("Verifying requested TS structures can be added to the structure set!");
            int counter = 0;
            int calcItems = structuresToAdd.Count;
            foreach (Tuple<string, string> itr in structuresToAdd)
            {
                if (!selectedSS.CanAddStructure(itr.Item1, itr.Item2))
                {
                    ProvideUIUpdate($"Error! {itr.Item2} can't be added the structure set!", true);
                    fail = true;
                }
                ProvideUIUpdate((int)(100 * ++counter / calcItems));
            }
            return fail;
        }

        private bool RemoveStructures(List<Structure> structuresToRemove)
        {
            int calcItems = structuresToRemove.Count;
            int counter = 0;
            foreach (Structure itr in structuresToRemove)
            {
                if (selectedSS.CanRemoveStructure(itr))
                {
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Removing: {itr.Id}");
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

        protected bool RemoveOldTSStructures(List<Tuple<string, string>> structures, bool removeTSTargetsToo = false)
        {
            UpdateUILabel("Remove Prior Tuning Structures: ");
            ProvideUIUpdate(0, "Removing prior tuning structures");
            //remove existing TS structures if they exist and re-add them to the structure list

            List<Tuple<string, string>> structuresToRemove;
            if (!removeTSTargetsToo) structuresToRemove = structures.Where(x => !x.Item2.ToLower().Contains("ctv") && !x.Item2.ToLower().Contains("ptv")).ToList();
            else structuresToRemove = structures;

            (bool fail, List<Structure> removeList) = VerifyRemoveTSStructures(structuresToRemove);
            if (fail) return true;
            ProvideUIUpdate(0, "Adding any remaining tuning structures to the stack");
            //4-15-2022 
            //remove ALL tuning structures from any previous runs (structure id starts with 'TS_'). Be sure to exclude any requested TS structures from the config file as we just added them!
            List<Structure> tsStructs = selectedSS.Structures.Where(x => x.Id.Length > 2 && string.Equals(x.Id.ToLower().Substring(0, 3), "ts_")).ToList();
            removeList.AddRange(tsStructs.Except(removeList));
            if (RemoveStructures(removeList)) return true;

            if (VerifyAddTSStructures(structuresToRemove)) return true;
            ProvideUIUpdate(100, "Prior tuning structures successfully removed!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        protected (bool, Structure) CheckAndGenerateStructure(string id)
        {
            bool fail = false;
            Structure theStructure = null;
            if (StructureTuningHelper.DoesStructureExistInSS(id, selectedSS))
            {
                if (selectedSS.CanRemoveStructure(StructureTuningHelper.GetStructureFromId(id, selectedSS))) selectedSS.RemoveStructure(StructureTuningHelper.GetStructureFromId(id, selectedSS));
                else
                {
                    ProvideUIUpdate("Error! Could not add dummy box to structure set to cut target at matchplane! Exiting!", true);
                    fail = true;
                }
            }
            if (!VerifyAddTSStructures(new List<Tuple<string, string>> { new Tuple<string, string>("CONTROL", id) }))
            {
                theStructure = AddTSStructures(new Tuple<string, string>("CONTROL", id));
            }
            else fail = true;
            return (fail, theStructure);
        }

        protected Structure AddTSStructures(Tuple<string, string> itr1)
        {
            Structure addedStructure = null;
            string dicomType = itr1.Item1;
            string structName = itr1.Item2;
            if (selectedSS.CanAddStructure(dicomType, structName))
            {
                addedStructure = selectedSS.AddStructure(dicomType, structName);
                addedStructures.Add(structName);
            }
            else ProvideUIUpdate($"Can't add {structName} to the structure set!");
            return addedStructure;
        }

        protected bool IsUOriginInside(StructureSet ss)
        {
            if (!ss.Image.HasUserOrigin || !StructureTuningHelper.GetStructureFromId("Body", ss).IsPointInsideSegment(ss.Image.UserOrigin))
            {
                ProvideUIUpdate("Did you forget to set the user origin? \nUser origin is NOT inside body contour! \nPlease fix and try again!", true);
                return true;
            }
            return false;
        }
        #endregion
    }
}
