using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Media.Media3D;
using SimpleProgressWindow;
using TSManipulationType = VMATTBICSIAutoPlanningHelpers.Enums.TSManipulationType;

namespace VMATTBICSIAutoPlanningHelpers.BaseClasses
{
    public class GenerateTSbase : SimpleMTbase
    {
        public List<Tuple<string, List<string>>> GetIsoNames() { return isoNames; }
        public List<string> GetAddedStructures() { return addedStructures; }
        public List<Tuple<string, TSManipulationType>> GetOptParameters() { return optParameters; }
        public List<Tuple<string, TSManipulationType, double>> GetSparingList() { return TSManipulationList; }
        public bool GetUpdateSparingListStatus() { return updateTSManipulationList; }
        public string GetErrorStackTrace() { return stackTraceError; }

        protected StructureSet selectedSS;
        //structure, manipulation type, added margin (if applicable)
        protected List<Tuple<string, TSManipulationType, double>> TSManipulationList;
        protected List<string> addedStructures = new List<string> { };
        protected List<Tuple<string, TSManipulationType>> optParameters = new List<Tuple<string, TSManipulationType>> { };
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

        protected virtual bool CreateFlash()
        {
            //no virtual method implementation as this method is really only useful for VMAT TBI as VMAT CSI already has a healthy margin going from CTV->PTV
            return false;
        }
        #endregion

        #region helper functions related to TS generation and manipulation
        private (bool, List<Structure>) VerifyRemoveTSStructures(List<Tuple<string, string>> structuresToRemove)
        {
            List<Structure> removeList = new List<Structure> { };
            bool fail = false;
            int calcItems = structuresToRemove.Count;
            int counter = 0; 
            foreach (Tuple<string, string> itr in structuresToRemove)
            {
                Structure tmp = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), itr.Item2.ToLower()));
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

        private bool VerifyAddTSStructures(List<Tuple<string, string>> structuresToAdd)
        {
            bool fail = false;
            ProvideUIUpdate("Verifying requested TS structures can be added to the structure set!");
            int counter = 0;
            int calcItems = structuresToAdd.Count;
            foreach(Tuple<string, string> itr in structuresToAdd)
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
                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Removing: {itr.Id}");
                selectedSS.RemoveStructure(itr);
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
            RemoveStructures(removeList);

            if (VerifyAddTSStructures(structuresToRemove)) return true;
            ProvideUIUpdate(100, "Prior tuning structures successfully removed!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
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

        protected bool ConvertHighToLowRes(List<Tuple<string, TSManipulationType, double>> highResManipulationList)
        {
            int percentComplete = 0;
            int calcItems = highResManipulationList.Count * 5;
            foreach (Tuple<string, TSManipulationType, double> itr in highResManipulationList)
            {
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieving high resolution structure: {itr.Item1}");
                //this structure should be present and contoured in structure set (checked previously)
                Structure highResStruct = selectedSS.Structures.First(x => string.Equals(x.Id, itr.Item1));
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

        protected Structure AddTSStructures(Tuple<string, string> itr1)
        {
            Structure addedStructure = null;
            string dicomType = itr1.Item1;
            string structName = itr1.Item2;
            if (selectedSS.CanAddStructure(dicomType, structName))
            {
                addedStructure = selectedSS.AddStructure(dicomType, structName);
                addedStructures.Add(structName);
                optParameters.Add(Tuple.Create(structName, TSManipulationType.None));
            }
            else ProvideUIUpdate($"Can't add {structName} to the structure set!");
            return addedStructure;
        }

        protected bool IsUOriginInside(StructureSet ss)
        {
            if (!ss.Image.HasUserOrigin || !ss.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "body")).IsPointInsideSegment(ss.Image.UserOrigin))
            {
                ProvideUIUpdate("Did you forget to set the user origin? \nUser origin is NOT inside body contour! \nPlease fix and try again!", true);
                return true;
            }
            return false;
        }
        #endregion
    }
}
