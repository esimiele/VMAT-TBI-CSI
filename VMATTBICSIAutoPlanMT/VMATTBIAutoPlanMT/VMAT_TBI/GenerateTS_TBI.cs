using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Media.Media3D;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Prompts;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using TSManipulationType = VMATTBICSIAutoPlanningHelpers.Enums.TSManipulationType;
using System.Text;
using System.Runtime.ExceptionServices;

namespace VMATTBIAutoPlanMT.VMAT_TBI
{
    public class GenerateTS_TBI : GenerateTSbase
    {
        public int GetNumberOfIsocenters() { return numIsos; }
        public int GetNumberOfVMATIsocenters() { return numVMATIsos; }

        //DICOM types
        //Possible values are "AVOIDANCE", "CAVITY", "CONTRAST_AGENT", "CTV", "EXTERNAL", "GTV", "IRRAD_VOLUME", 
        //"ORGAN", "PTV", "TREATED_VOLUME", "SUPPORT", "FIXATION", "CONTROL", and "DOSE_REGION". 
        private List<Tuple<string, string>> TS_structures;
        private int numIsos;
        private int numVMATIsos;
        private double targetMargin;
        private Structure flashStructure = null;
        private double flashMargin;

        public GenerateTS_TBI(List<Tuple<string, string>> ts, List<Tuple<string, TSManipulationType, double>> list, StructureSet ss, double tm, bool st, bool flash, Structure fSt, double fM)
        {
            //overloaded constructor for the case where the user wants to include flash in the simulation
            TS_structures = new List<Tuple<string, string>>(ts);
            TSManipulationList = new List<Tuple<string, TSManipulationType, double>>(list);
            selectedSS = ss;
            targetMargin = tm;
            useFlash = flash;
            flashStructure = fSt;
            flashMargin = fM;
        }

        [HandleProcessCorruptedStateExceptions]
        public override bool Run()
        {
            try 
            { 
                isoNames.Clear();
                if (PreliminaryChecks()) return true;
                if (UnionLRStructures()) return true;
                if (TSManipulationList.Any()) if (CheckHighResolution()) return true; 
                if (CreateTSStructures()) return true;
                if (PerformTSStructureManipulation()) return true;
                if (useFlash) if (CreateFlash()) return true;
                if (CleanUpDummyBox()) return true;
                UpdateUILabel("Finished!");
                ProvideUIUpdate(100, "Finished Structure Tuning!");
                ProvideUIUpdate($"Run time: {GetElapsedTime()} (mm:ss)");
            }
            catch(Exception e) 
            { 
                ProvideUIUpdate(String.Format("{0}", e.Message), true); 
                return true; 
            }
            return false;
        }

        #region preliminary checks
        protected override bool PreliminaryChecks()
        {
            UpdateUILabel("Performing Preliminary Checks: ");
            int calcItems = 4;
            int counter = 0;
            //check body structure exists and is contoured
            if(!StructureTuningHelper.DoesStructureExistInSS("body", selectedSS, true))
            {
                ProvideUIUpdate("Error! Body structure is either empty or null! Fix and try again!", true);
                return true;
            }
            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Body structure exists and is not empty");

            //check if user origin was set
            if (IsUOriginInside(selectedSS)) return true;
            ProvideUIUpdate((int)(100 * ++counter / calcItems), "User origin is inside body");
            
            if (CheckBodyExtentAndMatchline()) return true;
            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Body structure exists and matchline appropriate");

            if (CheckIfScriptRunPreviously()) return true;
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        private bool CheckIfScriptRunPreviously()
        {
            if(StructureTuningHelper.DoesStructureExistInSS("human_body", selectedSS, true))
            {
                if(selectedSS.Structures.Any(x => x.Id.ToLower().Contains("flash")))
                {
                    Structure body = StructureTuningHelper.GetStructureFromId("body", selectedSS);
                    Structure humanBody = StructureTuningHelper.GetStructureFromId("human_body", selectedSS);
                    (bool failCopyTarget, StringBuilder copyErrorMessage) = ContourHelper.CopyStructureOntoStructure(humanBody, body);
                    if (failCopyTarget)
                    {
                        ProvideUIUpdate(copyErrorMessage.ToString(), true);
                        return true;
                    }
                    ProvideUIUpdate($"Script has been run previously and flash structures exist!");
                    ProvideUIUpdate($"Copying {humanBody.Id} structure onto {body.Id}!");
                }
            }
            return false;
        }


        private bool CheckBodyExtentAndMatchline()
        {
            //get the points collection for the Body (used for calculating number of isocenters)
            Structure body = StructureTuningHelper.GetStructureFromId("Body", selectedSS);
            Point3DCollection pts = body.MeshGeometry.Positions;

            //check if patient length is > 116cm, if so, check for matchline contour
            if ((pts.Max(p => p.Z) - pts.Min(p => p.Z)) > 1160.0 && !StructureTuningHelper.DoesStructureExistInSS("matchline", selectedSS, true))
            {
                ProvideUIUpdate($"Body extent ({pts.Max(p => p.Z) - pts.Min(p => p.Z)} mm) is greater than 116.0 cm and no matchline structure was found!");
                //check to see if the user wants to proceed even though there is no matchplane contour or the matchplane contour exists, but is not filled
                ConfirmPrompt CP = new ConfirmPrompt("No matchplane contour found even though patient length > 116.0 cm!" + Environment.NewLine + Environment.NewLine + "Continue?!");
                CP.ShowDialog();
                if (!CP.GetSelection())
                {
                    ProvideUIUpdate("",true);
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region helper methods for create ts structures
        private bool CleanUpDummyBox()
        {
            UpdateUILabel("Cleaning up:");
            if (StructureTuningHelper.DoesStructureExistInSS("DummyBox", selectedSS))
            {
                Structure dummyBox = StructureTuningHelper.GetStructureFromId("dummybox", selectedSS);
                ProvideUIUpdate($"Removing {dummyBox.Id} structure now!");
                selectedSS.RemoveStructure(dummyBox);
            }
            else ProvideUIUpdate("DummyBox structure not found! Nothing to remove.");
            return false;
        }

        private bool CopyBodyStructureOnToStructure(Structure addedStructure)
        {
            Structure body = StructureTuningHelper.GetStructureFromId("Body", selectedSS);
            addedStructure.SegmentVolume = body.Margin(0.0);
            ProvideUIUpdate($"Copied body structure onto {addedStructure.Id}");
            return false;
        }

        private bool ContourLungsEvalVolume(Structure addedStructure)
        {
            Structure lung_block_left = StructureTuningHelper.GetStructureFromId("lung_block_l", selectedSS);
            ProvideUIUpdate("Retrived left lung block structure");
            Structure lung_block_right = StructureTuningHelper.GetStructureFromId("lung_block_r", selectedSS);
            ProvideUIUpdate("Retrived right lung block structure");
            if(lung_block_left == null || lung_block_left.IsEmpty)
            {
                ProvideUIUpdate($"Error! Lung_Block_L volume is null or empty! Could not contour Lungs_Eval structure! Exiting!", true);
                return true;
            }
            if (lung_block_right == null || lung_block_right.IsEmpty)
            {
                ProvideUIUpdate($"Error! Lung_Block_R volume is null or empty! Could not contour Lungs_Eval structure! Exiting!", true);
                return true;
            }
            addedStructure.SegmentVolume = lung_block_left.Or(lung_block_right.Margin(0.0));
            ProvideUIUpdate($"Contoured lung eval structure: {addedStructure.Id}");
            return false;
        }

        private bool ContourBlockVolume(Structure addedStructure)
        {
            Structure baseStructure;
            AxisAlignedMargins margins;
            ProvideUIUpdate($"Contouring block structure:");
            if (addedStructure.Id.ToLower().Contains("lung_block_l"))
            {
                //AxisAlignedMargins(inner or outer margin, margin from negative x, margin for negative y, margin for negative z, margin for positive x, margin for positive y, margin for positive z)
                baseStructure = StructureTuningHelper.GetStructureFromId("lung_l", selectedSS);
                margins = new AxisAlignedMargins(StructureMarginGeometry.Inner, 10.0, 10.0, 15.0, 10.0, 10.0, 10.0);
            }
            else if (addedStructure.Id.ToLower().Contains("lung_block_r"))
            {
                baseStructure = StructureTuningHelper.GetStructureFromId("lung_r", selectedSS);
                margins = new AxisAlignedMargins(StructureMarginGeometry.Inner, 10.0, 10.0, 15.0, 10.0, 10.0, 10.0);
            }
            else if (addedStructure.Id.ToLower().Contains("kidney_block_l"))
            {
                baseStructure = StructureTuningHelper.GetStructureFromId("kidney_l", selectedSS);
                margins = new AxisAlignedMargins(StructureMarginGeometry.Outer, 5.0, 20.0, 20.0, 20.0, 20.0, 20.0);
            }
            else
            {
                baseStructure = StructureTuningHelper.GetStructureFromId("kidney_r", selectedSS);
                margins = new AxisAlignedMargins(StructureMarginGeometry.Outer, 5.0, 20.0, 20.0, 20.0, 20.0, 20.0);
            }
            if(baseStructure == null || baseStructure.IsEmpty)
            {
                ProvideUIUpdate($"Error! Could not retrieve base structure to contour {addedStructure.Id}! Exiting!", true);
                return true;
            }
            ProvideUIUpdate($"Base structure: {baseStructure.Id}");
            ProvideUIUpdate("Margins:");
            ProvideUIUpdate($"Inner or outer: {margins.Geometry}");
            ProvideUIUpdate($"X1: {margins.X1}");
            ProvideUIUpdate($"X2: {margins.X2}");
            ProvideUIUpdate($"Y1: {margins.Y1}");
            ProvideUIUpdate($"Y2: {margins.Y2}");
            ProvideUIUpdate($"Z1: {margins.Z1}");
            ProvideUIUpdate($"Z2: {margins.Z2}");

            addedStructure.SegmentVolume = baseStructure.AsymmetricMargin(margins);
            ProvideUIUpdate($"Contoured block volume for structure: {addedStructure.Id}");
            return false;
        }
        #endregion

        #region create tuning structures
        protected override bool CreateTSStructures()
        {
            UpdateUILabel("Create TS Structures:");
            ProvideUIUpdate("Adding remaining tuning structures to stack!"); 
            if (RemoveOldTSStructures(TS_structures, true)) return true;
            int calcItems = TS_structures.Count;
            int counter = 0;
            //Need to add the Human body, PTV_BODY, and TS_PTV_VMAT contours manually
            //if these structures were present, they should have been removed (regardless if they were contoured or not). 
            foreach (Tuple<string, string> itr in TS_structures)
            {
                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Adding {itr.Item2} to the structure set!");
                AddTSStructures(itr);
            }

            ProvideUIUpdate(100, "Finished adding tuning structures!");
            ProvideUIUpdate(0, "Contouring tuning structures!");
            counter = 0;
            //now contour the various structures
            foreach (string itr in addedStructures)
            {
                ProvideUIUpdate($"Contouring TS: {itr}");
                Structure addedStructure = StructureTuningHelper.GetStructureFromId(itr, selectedSS);
                ProvideUIUpdate($"Retrieved structure: {addedStructure.Id}");
                if (itr.ToLower().Contains("human"))
                {
                    if (CopyBodyStructureOnToStructure(addedStructure)) return true;
                }
                else if (itr.ToLower().Contains("ptv_body"))
                {
                    if (GeneratePTVFromBody(addedStructure)) return true;
                }
                else if (itr.ToLower().Contains("_block"))
                {
                    if (ContourBlockVolume(addedStructure)) return true;
                }
                else if (itr.ToLower().Contains("_eval"))
                {
                    if (ContourLungsEvalVolume(addedStructure)) return true;
                }
                ProvideUIUpdate((int)(100 * ++counter / calcItems));
            }

            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        private bool GeneratePTVFromBody(Structure addedStructure)
        {
            if (CopyBodyStructureOnToStructure(addedStructure)) return true;
            (bool fail, StringBuilder errorMessage) = ContourHelper.CropStructureFromBody(addedStructure, selectedSS, -targetMargin);
            if (fail)
            {
                ProvideUIUpdate(errorMessage.ToString());
                return true;
            }
            ProvideUIUpdate($"Cropped {addedStructure.Id} from body with -{targetMargin} cm margin");
            return false;
        }
        #endregion

        protected override bool PerformTSStructureManipulation()
        {
            UpdateUILabel("Perform TS Manipulations: ");
            int counter = 0;
            int calcItems = TSManipulationList.Count;
            //determine if any TS structures need to be added to the selected structure set (i.e., were not present or were removed in the first foreach loop)
            //this is provided here to only add additional TS if they are relevant to the current case (i.e., it doesn't make sense to add the brain TS's if we 
            //are not interested in sparing brain)
            if (TSManipulationList.Any())
            {
                Structure ptv_body = StructureTuningHelper.GetStructureFromId("ptv_body", selectedSS);
                if(ptv_body == null || ptv_body.IsEmpty)
                {
                    ProvideUIUpdate($"Error! PTV_Body structure is null or empty! Cannot perform tuning structure manipulations! Exiting!", true);
                    return true;
                }
                foreach (Tuple<string, TSManipulationType, double> itr in TSManipulationList)
                {
                    if (ManipulateTuningStructures(itr, ptv_body, ref counter, ref calcItems)) return true;
                }
                if (GenerateTSTarget(ptv_body)) return true;
            }
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        private bool GenerateTSTarget(Structure baseTarget)
        {
            UpdateUILabel($"Create TS_{baseTarget.Id}:");
            int percentComplete = 0;
            int calcItems = 2;
            Structure addedTSTarget = GetTSTarget(baseTarget.Id);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Contoured TS target: {addedTSTarget.Id}");
            
            if (StructureTuningHelper.DoesStructureExistInSS("matchline", selectedSS, true))
            {
                ProvideUIUpdate($"Cutting {addedTSTarget} at the matchline!");

                //find the image plane where the matchline is location. Record this value and break the loop. Also find the first slice where the ptv_body contour starts and record this value
                Structure matchline = StructureTuningHelper.GetStructureFromId("matchline", selectedSS);
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Retrieved matchline structure: {matchline.Id}");

                (bool failContourDummyBox, Structure dummyBox) = ContourDummyBox(matchline, addedTSTarget);
                if (failContourDummyBox) return true;

                if (ContourTSLegs("TS_PTV_Legs", dummyBox, addedTSTarget)) return true;

                //matchplane exists and needs to be cut from TS_PTV_Body. Also remove all TS_PTV_Body segements inferior to match plane
                if (CutTSTargetFromMatchline(addedTSTarget, matchline, dummyBox)) return true;
            }
            return false;
        }

        private (bool, Structure) ContourDummyBox(Structure matchline, Structure target)
        {
            UpdateUILabel("Cut TS Target at matchline:");
            int percentComplete = 0;
            int calcItems = 12;

            int matchplaneLocation = CalculationHelper.ComputeSlice(matchline.CenterPoint.z, selectedSS);
            ProvideUIUpdate($"Matchline center z position: {matchline.CenterPoint.z} mm");
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Matchline slice number: {matchplaneLocation}");

            int addedTSTargetMinZ = CalculationHelper.ComputeSlice(target.MeshGeometry.Positions.Min(p => p.Z), selectedSS);
            ProvideUIUpdate($"{target.Id} min z position: {target.MeshGeometry.Positions.Min(p => p.Z)} mm");
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Matchline slice number: {addedTSTargetMinZ}");

            //number of buffer slices equal to 5 cn in z direction
            //needed because the dummybox will be reused for flash generation (if applicable)
            double bufferInMM = 50.0;
            int bufferSlices = (int)Math.Ceiling(bufferInMM / selectedSS.Image.ZRes);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Calculated number of buffer slices for dummy box: {bufferSlices}");
            calcItems += matchplaneLocation - (addedTSTargetMinZ - bufferSlices);

            (bool failDummy, Structure dummyBox) = CheckAndGenerateStructure("DummyBox");
            if (failDummy) return (failDummy, dummyBox);
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Created structure: {dummyBox.Id}");

            (VVector[] pts, StringBuilder latBoxMessage) = ContourHelper.GetLateralBoundingBoxForStructure(target, 5.0);
            ProvideUIUpdate(latBoxMessage.ToString());
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Calculated lateral bounding box for: {target.Id}");
            foreach (VVector v in pts) ProvideUIUpdate($"({v.x}, {v.y}, {v.z}) mm");

            ProvideUIUpdate($"Contouring {dummyBox.Id} now:");
            //give 5cm margin on TS_PTV_LEGS (one slice of the CT should be 5mm) in case user wants to include flash up to 5 cm
            for (int i = matchplaneLocation; i > addedTSTargetMinZ - bufferSlices; i--)
            {
                ProvideUIUpdate((int)(100 * ++percentComplete / calcItems));
                dummyBox.AddContourOnImagePlane(pts, i);
            }
            ProvideUIUpdate($"Finished contouring {dummyBox.Id}");
            return (false, dummyBox);
        }

        private bool ContourTSLegs(string TSLegsId, Structure dummyBox, Structure target)
        {
            UpdateUILabel($"Contour {TSLegsId}:");
            int percentComplete = 0;
            int calcItems = 3;

            //do the structure manipulation
            (bool failTSLegs, Structure TS_legs) = CheckAndGenerateStructure(TSLegsId);
            if (failTSLegs) return true;
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Created structure: {TS_legs.Id}");

            (bool failCopyTarget, StringBuilder copyErrorMessage) = ContourHelper.CopyStructureOntoStructure(target, TS_legs);
            if (failCopyTarget)
            {
                ProvideUIUpdate(copyErrorMessage.ToString(), true);
                return true;
            }
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Copied structure {target.Id} onto {TS_legs.Id}");

            (bool failOverlap, StringBuilder overlapMessage) = ContourHelper.ContourOverlap(dummyBox, TS_legs, 0.0);
            if (failOverlap)
            {
                ProvideUIUpdate(overlapMessage.ToString(), true);
                return true;
            }
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Contoured overlap between: {TS_legs.Id} and {dummyBox.Id} onto {TS_legs.Id}");
            return false;
        }

        private bool CutTSTargetFromMatchline(Structure addedTSTarget, Structure matchline, Structure dummyBox)
        {
            UpdateUILabel($"Cut {addedTSTarget.Id} at matchline:");
            int percentComplete = 0;
            int calcItems = 12;
            
            //subtract both dummybox and matchline from TS_PTV_VMAT
            (bool failCropBox, StringBuilder cropBoxMessage) = ContourHelper.CropTargetFromStructure(addedTSTarget, dummyBox, 0.0);
            if (failCropBox)
            {
                ProvideUIUpdate(cropBoxMessage.ToString(), true);
                return true;
            }
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Cropped target {addedTSTarget.Id} from {dummyBox.Id}");

            (bool failCropMatch, StringBuilder cropMatchMessage) = ContourHelper.CropTargetFromStructure(addedTSTarget, matchline, 0.0);
            if (failCropMatch)
            {
                ProvideUIUpdate(cropMatchMessage.ToString(), true);
                return true;
            }
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Cropped target {addedTSTarget.Id} from {matchline.Id}");
           
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        private bool ContourBolus(Structure bolusFlash)
        {
            //if the flashStructure is not null, then the user wants local flash --> use flashStructure. If not, then the user wants global flash --> use body structure
            //add user-specified margin on top of structure of interest to generate flash. 
            //8 -14-2020, an asymmetric margin is used because I might intend to change this code so flash is added in all directions except for sup/inf/post
            if (flashStructure != null)
            {
                bolusFlash.SegmentVolume = flashStructure.AsymmetricMargin(new AxisAlignedMargins(StructureMarginGeometry.Outer,
                                                                                                                                flashMargin * 10.0,
                                                                                                                                flashMargin * 10.0,
                                                                                                                                flashMargin * 10.0,
                                                                                                                                flashMargin * 10.0,
                                                                                                                                flashMargin * 10.0,
                                                                                                                                flashMargin * 10.0));
            }
            else
            {
                Structure body = StructureTuningHelper.GetStructureFromId("body", selectedSS);
                bolusFlash.SegmentVolume = body.AsymmetricMargin(new AxisAlignedMargins(StructureMarginGeometry.Outer,
                                                                                                                      flashMargin * 10.0,
                                                                                                                      flashMargin * 10.0,
                                                                                                                      flashMargin * 10.0,
                                                                                                                      flashMargin * 10.0,
                                                                                                                      flashMargin * 10.0,
                                                                                                                      flashMargin * 10.0));
            }
            return false;
        }

        private bool CreateFlash()
        {
            UpdateUILabel("Create flash:");
            int percentComplete = 0;
            int calcItems = 10;
            //create flash for the plan per the users request
            //NOTE: IT IS IMPORTANT THAT ALL OF THE STRUCTURES CREATED IN THIS METHOD (I.E., ALL STRUCTURES USED TO GENERATE FLASH HAVE THE KEYWORD 'FLASH' SOMEWHERE IN THE STRUCTURE ID)!
            //first need to create a bolus structure (remove it if it already exists)
            (bool failBolus, Structure bolusFlash) = CheckAndGenerateStructure("BOLUS_FLASH");
            if (failBolus) return true;
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Created structure: {bolusFlash.Id}");

            //contour the bolus
            if (ContourBolus(bolusFlash)) return true;

            Structure body = StructureTuningHelper.GetStructureFromId("body", selectedSS);
            //now subtract the body structure from the newly-created bolus structure
            (bool failCrop, StringBuilder cropMessage) = ContourHelper.CropTargetFromStructure(bolusFlash, body, 0.0);
            if (failCrop)
            {
                ProvideUIUpdate(cropMessage.ToString(), true);
                return true;
            }
            
            //if the subtracted structre is empty, then that indicates that the structure used to generate the flash was NOT close enough to the body surface to generate flash for the user-specified margin
            if (bolusFlash.IsEmpty)
            {
                ProvideUIUpdate("Error! Created bolus structure does not extrude from the body! \nIs this the right structure?", true);
                return true;
            }

            //assign the water to the bolus volume (HU = 0.0)
            bolusFlash.SetAssignedHU(0.0);

            //crop flash at matchline ONLY if global flash is used
            Structure dummyBox = StructureTuningHelper.GetStructureFromId("dummybox", selectedSS);
            if (flashStructure == null && StructureTuningHelper.DoesStructureExistInSS("matchline", selectedSS, true))
            {
                if (CutTSTargetFromMatchline(bolusFlash, StructureTuningHelper.GetStructureFromId("matchline", selectedSS), dummyBox)) return true;
                //(bool failCropBolus, StringBuilder cropBolusMessage) = ContourHelper.CropTargetFromStructure(bolusFlash, dummyBox, 0.0);
                //if (failCropBolus)
                //{
                //    ProvideUIUpdate(cropBolusMessage.ToString(), true);
                //    return true;
                //}
                //(bool failCropMatchline, StringBuilder cropMatchlineMessage) = ContourHelper.CropTargetFromStructure(bolusFlash, StructureTuningHelper.GetStructureFromId("matchline", selectedSS), 0.0);
                //if (failCropMatchline)
                //{
                //    ProvideUIUpdate(cropMatchlineMessage.ToString(), true);
                //    return true;
                //}
            }

            //Now extend the body contour to include the bolus_flash structure. The reason for this is because Eclipse automatically sets the dose calculation grid to the body structure contour (no overriding this)
            (bool unionFail, StringBuilder unionMessage) = ContourHelper.ContourUnion(bolusFlash, body, 0.0);
            if (unionFail)
            {
                ProvideUIUpdate(unionMessage.ToString(), true);
                return true;
            }

            //now create the ptv_flash structure
            (bool failPTVFlash, Structure ptv_flash) = CheckAndGenerateStructure("TS_PTV_FLASH");
            if (failPTVFlash) return true;

            //copy the NEW body structure (i.e., body + bolus_flash)
            if (GeneratePTVFromBody(ptv_flash)) return true;

            foreach (Tuple<string, TSManipulationType, double> itr in TSManipulationList.Where(x => x.Item2 == TSManipulationType.ContourOverlapWithTarget || x.Item2 == TSManipulationType.CropTargetFromStructure))
            {
                ManipulateTuningStructures(itr, ptv_flash, ref percentComplete, ref calcItems);
            }

            //now create the ptv_flash structure
            (bool failFlashTarget, Structure flash_target) = CheckAndGenerateStructure("TS_FLASH_TARGET");
            if (failFlashTarget) return true;

            (bool failCopyTarget, StringBuilder copyErrorMessage) = ContourHelper.CopyStructureOntoStructure(ptv_flash, flash_target);
            if (failCopyTarget)
            {
                ProvideUIUpdate(copyErrorMessage.ToString(), true);
                return true;
            }
            ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Copied structure {ptv_flash.Id} onto {flash_target.Id}");


            if(StructureTuningHelper.DoesStructureExistInSS("matchline", selectedSS, true))
            {
                //(bool failLegsFlash, Structure legsFlash) = CheckAndGenerateStructure("TS_LEGS_FLASH");
                //if (failLegsFlash) return true;

                //(bool failCopyLegsFlash, StringBuilder copyLegsFlashMessage) = ContourHelper.CopyStructureOntoStructure(ptv_flash, legsFlash);
                //if (failCopyLegsFlash)
                //{
                //    ProvideUIUpdate(copyLegsFlashMessage.ToString(), true);
                //    return true;
                //}

                //(bool failOverlap, StringBuilder overlapMessage) = ContourHelper.ContourOverlap(dummyBox, legsFlash, 0.0);
                //if (failOverlap)
                //{
                //    ProvideUIUpdate(overlapMessage.ToString(), true);
                //    return true;
                //}

                if (CutTSTargetFromMatchline(ptv_flash, StructureTuningHelper.GetStructureFromId("matchline", selectedSS), dummyBox)) return true;

                ////subtract both dummybox and matchline from TS_PTV_flash
                //(bool failCropBox, StringBuilder cropBoxMessage) = ContourHelper.CropTargetFromStructure(ptv_flash, dummyBox, 0.0);
                //if (failCropBox)
                //{
                //    ProvideUIUpdate(cropBoxMessage.ToString(), true);
                //    return true;
                //}
                //ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Cropped target {ptv_flash.Id} from {dummyBox.Id}");

                //(bool failCropMatch, StringBuilder cropMatchMessage) = ContourHelper.CropTargetFromStructure(ptv_flash, StructureTuningHelper.GetStructureFromId("matchline", selectedSS), 0.0);
                //if (failCropMatch)
                //{
                //    ProvideUIUpdate(cropMatchMessage.ToString(), true);
                //    return true;
                //}
                //ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Cropped target {ptv_flash.Id} from {ptv_flash.Id}");

                //selectedSS.RemoveStructure(dummyBox);
            }

            return false;
        }

        protected override bool CalculateNumIsos()
        {
            Point3DCollection pts = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "body").MeshGeometry.Positions;
            //calculate number of required isocenters
            if (!(selectedSS.Structures.Where(x => x.Id.ToLower() == "matchline").Any()))
            {
                //no matchline implying that this patient will be treated with VMAT only. For these cases the maximum number of allowed isocenters is 3.
                //the reason for the explicit statements calculating the number of isos and then truncating them to 3 was to account for patients requiring < 3 isos and if, later on, we want to remove the restriction of 3 isos
                numIsos = numVMATIsos = (int)Math.Ceiling(((pts.Max(p => p.Z) - pts.Min(p => p.Z)) / (400.0 - 20.0)));
                if (numIsos > 3) numIsos = numVMATIsos = 3;
            }
            else
            {
                //matchline structure is present, but empty
                if (selectedSS.Structures.First(x => x.Id.ToLower() == "matchline").IsEmpty)
                {
                    ConfirmPrompt CP = new ConfirmPrompt("I found a matchline structure in the structure set, but it's empty!" + Environment.NewLine + Environment.NewLine + "Do you want to continue without using the matchline structure?!");
                    CP.ShowDialog();
                    if (!CP.GetSelection()) return true;

                    //continue and ignore the empty matchline structure (same calculation as VMAT only)
                    numIsos = numVMATIsos = (int)Math.Ceiling(((pts.Max(p => p.Z) - pts.Min(p => p.Z)) / (400.0 - 20.0)));
                    if (numIsos > 3) numIsos = numVMATIsos = 3;
                }
                //matchline structure is present and not empty
                else
                {
                    //get number of isos for PTV superior to matchplane (always truncate this value to a maximum of 4 isocenters)
                    numVMATIsos = (int)Math.Ceiling(((pts.Max(p => p.Z) - selectedSS.Structures.First(x => x.Id.ToLower() == "matchline").CenterPoint.z) / (400.0 - 20.0)));
                    if (numVMATIsos > 4) numVMATIsos = 4;

                    //get number of iso for PTV inferior to matchplane
                    //if (selectedSS.Structures.First(x => x.Id.ToLower() == "matchline").CenterPoint.z - pts.Min(p => p.Z) - 3.0 <= (400.0 - 20.0)) numIsos = numVMATIsos + 1;

                    //5-20-2020 Nataliya said to only add a second legs iso if the extent of the TS_PTV_LEGS is > 40.0 cm
                    if (selectedSS.Structures.First(x => x.Id.ToLower() == "matchline").CenterPoint.z - pts.Min(p => p.Z) - 3.0 <= (400.0 - 0.0)) numIsos = numVMATIsos + 1;
                    else numIsos = numVMATIsos + 2;
                    //MessageBox.Show(String.Format("{0}", selectedSS.Structures.First(x => x.Id.ToLower() == "matchline").CenterPoint.z - pts.Min(p => p.Z) - 3.0));
                }
            }

            //set isocenter names based on numIsos and numVMATIsos (determined these names from prior cases)
            isoNames.Add(Tuple.Create("_VMAT TBI", new List<string>(IsoNameHelper.GetIsoNames(numVMATIsos, numIsos, true))));
            return false;
        }
    }
}
