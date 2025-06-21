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
using VMATTBICSIAutoPlanningHelpers.Models;
using System.Security.Cryptography.X509Certificates;

namespace VMATTBIAutoPlanMT.VMAT_TBI
{
    public class GenerateTS_TBI : GenerateTSbase
    {
        //Get methods
        public int NumberofIsocenters { get; private set; } = -1;
        public int NumberofVMATIsocenters { get; private set; } = -1;
        //plan id, normalization volume
        public Dictionary<string, string> NormalizationVolumes { get; private set; } = new Dictionary<string, string> { };

        //DICOM types
        //Possible values are "AVOIDANCE", "CAVITY", "CONTRAST_AGENT", "CTV", "EXTERNAL", "GTV", "IRRAD_VOLUME", 
        //"ORGAN", "PTV", "TREATED_VOLUME", "SUPPORT", "FIXATION", "CONTROL", and "DOSE_REGION". 
        //plan id, structure id, num fx, dose per fx, cumulative dose
        private List<PrescriptionModel> prescriptions;
        private List<RequestedTSStructureModel> TS_structures;
        //plan id, list<original target id, ts target id>

        //data members
        private double targetMargin;
        private Structure flashStructure = null;
        private double flashMargin;
        private bool useFlash = false;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="ts"></param>
        /// <param name="list"></param>
        /// <param name="presc"></param>
        /// <param name="ss"></param>
        /// <param name="tm"></param>
        /// <param name="flash"></param>
        /// <param name="fSt"></param>
        /// <param name="fM"></param>
        /// <param name="closePW"></param>
        public GenerateTS_TBI(List<RequestedTSStructureModel> ts, 
                              List<RequestedTSManipulationModel> list, 
                              List<PrescriptionModel> presc, 
                              StructureSet ss, double tm, bool flash, Structure fSt, double fM, bool closePW)
        {
            //overloaded constructor for the case where the user wants to include flash in the simulation
            TS_structures = new List<RequestedTSStructureModel>(ts);
            TSManipulationList = new List<RequestedTSManipulationModel>(list);
            prescriptions = new List<PrescriptionModel>(presc);
            selectedSS = ss;
            targetMargin = tm;
            useFlash = flash;
            flashStructure = fSt;
            flashMargin = fM;
            SetCloseOnFinish(closePW, 3000);
        }

        #region Run control
        /// <summary>
        /// Run control
        /// </summary>
        /// <returns></returns>
        [HandleProcessCorruptedStateExceptions]
        public override bool Run()
        {
            try 
            { 
                PlanIsocentersList.Clear();
                if (PreliminaryChecks()) return true;
                if (UnionLRStructures()) return true;
                if (TSManipulationList.Any()) if (CheckHighResolution()) return true; 
                if (CreateTSStructures()) return true;
                if (PerformTSStructureManipulation()) return true;
                if (useFlash) if (CreateFlash()) return true;
                if (CleanUpDummyBox()) return true;
                if (CalculateNumIsos()) return true;
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
        #endregion

        #region Preliminary checks
        /// <summary>
        /// Preliminary checks to ensure the body exists, the user origin is inside the body, body extent and matchline presence, and check if the prep 
        /// script was running previously
        /// </summary>
        /// <returns></returns>
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
            ProvideUIUpdate(100 * ++counter / calcItems, "Body structure exists and is not empty");

            //check if user origin was set
            if (IsUOriginInside()) return true;
            ProvideUIUpdate(100 * ++counter / calcItems, "User origin is inside body");
            
            if (CheckBodyExtentAndMatchline()) return true;
            ProvideUIUpdate(100 * ++counter / calcItems, "Body structure exists and matchline appropriate");

            if (CheckIfScriptRunPreviously()) return true;
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        /// <summary>
        /// Check the structure set for indications that this script was run previously
        /// </summary>
        /// <returns></returns>
        private bool CheckIfScriptRunPreviously()
        {
            if(StructureTuningHelper.DoesStructureExistInSS("human_body", selectedSS, true))
            {
                if(selectedSS.Structures.Any(x => x.Id.ToLower().Contains("flash")))
                {
                    //copy human_body back onto body if flash was used in previous run of the script
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

        /// <summary>
        /// Check the body height against the limits for treating in the HFS position. If body is taller than limit (116 cm), verify that the matchline
        /// structure is present and contoured
        /// </summary>
        /// <returns></returns>
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

        #region Helper methods for create ts structures
        /// <summary>
        /// Helper method to remove the dummy box structure used to cut the target at the matchline
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Simple helper method to copy the body structure onto the supplied structure
        /// </summary>
        /// <param name="addedStructure"></param>
        /// <returns></returns>
        private bool CopyBodyStructureOnToStructure(Structure addedStructure)
        {
            (bool fail, StringBuilder message) = ContourHelper.CopyStructureOntoStructure(StructureTuningHelper.GetStructureFromId("Body", selectedSS), addedStructure);
            if(fail)
            {
                ProvideUIUpdate(message.ToString(), true);
                return true;
            }
            ProvideUIUpdate($"Copied body structure onto {addedStructure.Id}");
            return false;
        }

        /// <summary>
        /// Simple method to union the left and right lung block structures (for scleroderma trial)
        /// </summary>
        /// <param name="addedStructure"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Dedicated method for contouring the lung and kidney block volumes required by the scleroderma trial
        /// </summary>
        /// <param name="addedStructure"></param>
        /// <returns></returns>
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
            ProvideUIUpdate($"X1: {margins.X1:0.0} mm");
            ProvideUIUpdate($"X2: {margins.X2:0.0} mm");
            ProvideUIUpdate($"Y1: {margins.Y1:0.0} mm");
            ProvideUIUpdate($"Y2: {margins.Y2:0.0} mm");
            ProvideUIUpdate($"Z1: {margins.Z1:0.0} mm");
            ProvideUIUpdate($"Z2: {margins.Z2:0.0} mm");

            addedStructure.SegmentVolume = baseStructure.AsymmetricMargin(margins);
            ProvideUIUpdate($"Contoured block volume for structure: {addedStructure.Id}");
            return false;
        }
        #endregion

        #region Create tuning structures
        /// <summary>
        /// Method to direct and control the flow of TS generation for TBI
        /// </summary>
        /// <returns></returns>
        protected override bool CreateTSStructures()
        {
            UpdateUILabel("Create TS Structures:");
            ProvideUIUpdate("Adding remaining tuning structures to stack!"); 
            if (RemoveOldTSStructures(TS_structures, true)) return true;

            int counter = 0;
            int calcItems = TS_structures.Count;

            foreach (RequestedTSStructureModel itr in TS_structures)
            {
                ProvideUIUpdate(100 * ++counter / calcItems, $"Adding {itr.StructureId} to the structure set!");
                AddTSStructures(itr);
            }

            ProvideUIUpdate(100, "Finished adding tuning structures!");
            ProvideUIUpdate(0, "Contouring tuning structures!");

            counter = 0;
            calcItems = AddedStructureIds.Count;
            //now contour the various structures
            foreach (string itr in AddedStructureIds)
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
                ProvideUIUpdate(100 * ++counter / calcItems);
            }

            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        /// <summary>
        /// Simple helper method to copy the body structure onto the supplied structure, then crop the supplied structure from the body by the requested
        /// inner margin
        /// </summary>
        /// <param name="addedStructure"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Directory method for controlling the flow of TS structure manipulations
        /// </summary>
        /// <returns></returns>
        protected override bool PerformTSStructureManipulation()
        {
            UpdateUILabel("Perform TS Manipulations: ");
            int counter = 0;
            int calcItems = TSManipulationList.Count * prescriptions.Count;
            
            List<TargetModel> tmpTSTargetList = new List<TargetModel> { };
            //prescriptions are inherently sorted by increasing cumulative Rx to targets
            foreach (PrescriptionModel itr in prescriptions)
            {
                Structure target = null;
                //special logic. We want to actually manipulate ptv_body itself rather than a TS_PTV_Body structure
                if (string.Equals(itr.TargetId.ToLower(), "ptv_body"))
                {
                    target = StructureTuningHelper.GetStructureFromId(itr.TargetId, selectedSS);
                }
                else
                {
                    //target Id is not ptv_body, generate a new TSTarget
                    target = GetTSTarget(itr.TargetId);
                    tmpTSTargetList.Add(new TargetModel(itr.TargetId, itr.CumulativeDoseToTarget, target.Id));
                }
                if (target == null || target.IsEmpty)
                {
                    ProvideUIUpdate($"Error! Target structure: {itr.TargetId} is null or empty! Cannot perform tuning structure manipulations! Exiting!", true);
                    return true;
                }
                if (TSManipulationList.Any())
                {
                    //perform all relevant TS manipulations for the specified target
                    foreach (RequestedTSManipulationModel itr1 in TSManipulationList)
                    {
                        if (ManipulateTuningStructures(itr1, target)) return true;
                        ProvideUIUpdate(100 * ++counter / calcItems);
                    }
                }
                else ProvideUIUpdate("No TS manipulations requested!");
                if (string.Equals(itr.TargetId.ToLower(), "ptv_body"))
                {
                    //ts_ptv_vmat needs to be handled AFTER ts manipulation because ptv_body itself needs to be cropped from all the relevant structures
                    (bool fail, string tsPTVVMATId) = GenerateTSPTVBodyTarget(target, "TS_PTV_VMAT");
                    if (fail) return true;
                    tmpTSTargetList.Add(new TargetModel(itr.TargetId, itr.CumulativeDoseToTarget, tsPTVVMATId));
                }
            }
            //only one plan is allowed for the prescriptions --> last item is the highest Rx target for this plan and needs to be set as the normalization volume
            NormalizationVolumes.Add(prescriptions.Last().PlanId, tmpTSTargetList.OrderByDescending(x => x.TargetRxDose).First().TsTargetId);
            PlanTargets.Add(new PlanTargetsModel(prescriptions.Last().PlanId, new List<TargetModel> (tmpTSTargetList)));

            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        /// <summary>
        /// Method to Generate TS_PTV_Body target and perform the necessary cropping operations if a matchline structure is present in the structure set
        /// </summary>
        /// <param name="baseTarget"></param>
        /// <param name="requestedTsTargetId"></param>
        /// <returns></returns>
        private (bool, string) GenerateTSPTVBodyTarget(Structure baseTarget, string requestedTsTargetId)
        {
            UpdateUILabel($"Create {requestedTsTargetId}:");
            int percentComplete = 0;
            int calcItems = 2;
            Structure addedTSTarget = GetTSTarget(baseTarget.Id, requestedTsTargetId);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Contoured TS target: {addedTSTarget.Id}");
            
            if (StructureTuningHelper.DoesStructureExistInSS("matchline", selectedSS, true))
            {
                ProvideUIUpdate($"Cutting {addedTSTarget} at the matchline!");

                //find the image plane where the matchline is location. Record this value and break the loop. Also find the first slice where the ptv_body contour starts and record this value
                Structure matchline = StructureTuningHelper.GetStructureFromId("matchline", selectedSS);
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved matchline structure: {matchline.Id}");

                (bool failContourDummyBox, Structure dummyBox) = ContourDummyBox(matchline, addedTSTarget);
                if (failContourDummyBox) return (true, addedTSTarget.Id);

                if (ContourTSLegs("TS_PTV_Legs", dummyBox, addedTSTarget)) return (true, addedTSTarget.Id);

                //matchplane exists and needs to be cut from TS_PTV_Body. Also remove all TS_PTV_Body segements inferior to match plane
                if (CutTSTargetFromMatchline(addedTSTarget, matchline, dummyBox)) return (true, addedTSTarget.Id);
            }
            return (false, addedTSTarget.Id);
        }

        /// <summary>
        /// Utility method to contour a box structure starting on the matchline structure slice and continuing to the most-inferior slice of the body
        /// plus an additional 5 cm margin (in case the user requested flash)
        /// </summary>
        /// <param name="matchline"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        private (bool, Structure) ContourDummyBox(Structure matchline, Structure target)
        {
            UpdateUILabel("Cut TS Target at matchline:");
            int percentComplete = 0;
            int calcItems = 12;

            int matchplaneLocation = CalculationHelper.ComputeSlice(matchline.CenterPoint.z, selectedSS);
            ProvideUIUpdate($"Matchline center z position: {matchline.CenterPoint.z:0.0} mm");
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Matchline slice number: {matchplaneLocation}");

            int addedTSTargetMinZ = CalculationHelper.ComputeSlice(target.MeshGeometry.Positions.Min(p => p.Z), selectedSS);
            ProvideUIUpdate($"{target.Id} min z position: {target.MeshGeometry.Positions.Min(p => p.Z):0.0} mm");
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Matchline slice number: {addedTSTargetMinZ}");

            //number of buffer slices equal to 5 cn in z direction
            //needed because the dummybox will be reused for flash generation (if applicable)
            double bufferInMM = 50.0;
            int bufferSlices = (int)Math.Ceiling(bufferInMM / selectedSS.Image.ZRes);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Calculated number of buffer slices for dummy box: {bufferSlices}");
            calcItems += matchplaneLocation - (addedTSTargetMinZ - bufferSlices);

            (bool failDummy, Structure dummyBox) = RemoveAndGenerateStructure("DummyBox");
            if (failDummy) return (failDummy, dummyBox);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Created structure: {dummyBox.Id}");

            (VVector[] pts, StringBuilder latBoxMessage) = ContourHelper.GetLateralBoundingBoxForStructure(target, 5.0);
            ProvideUIUpdate(latBoxMessage.ToString());
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Calculated lateral bounding box for: {target.Id}");
            foreach (VVector v in pts) ProvideUIUpdate($"({v.x:0.0}, {v.y:0.0}, {v.z:0.0}) mm");

            ProvideUIUpdate($"Contouring {dummyBox.Id} now:");
            //give 5cm margin on TS_PTV_LEGS (one slice of the CT should be 5mm) in case user wants to include flash up to 5 cm
            for (int i = matchplaneLocation; i > addedTSTargetMinZ - bufferSlices; i--)
            {
                ProvideUIUpdate(100 * ++percentComplete / calcItems);
                dummyBox.AddContourOnImagePlane(pts, i);
            }
            ProvideUIUpdate($"Finished contouring {dummyBox.Id}");
            return (false, dummyBox);
        }

        /// <summary>
        /// Helper method to contour the legs target if there is a matchline structure present in the structure set
        /// </summary>
        /// <param name="TSLegsId"></param>
        /// <param name="dummyBox"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        private bool ContourTSLegs(string TSLegsId, Structure dummyBox, Structure target)
        {
            UpdateUILabel($"Contour {TSLegsId}:");
            int percentComplete = 0;
            int calcItems = 3;

            //do the structure manipulation
            (bool failTSLegs, Structure TS_legs) = RemoveAndGenerateStructure(TSLegsId);
            if (failTSLegs) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Created structure: {TS_legs.Id}");

            (bool failCopyTarget, StringBuilder copyErrorMessage) = ContourHelper.CopyStructureOntoStructure(target, TS_legs);
            if (failCopyTarget)
            {
                ProvideUIUpdate(copyErrorMessage.ToString(), true);
                return true;
            }
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Copied structure {target.Id} onto {TS_legs.Id}");

            (bool failOverlap, StringBuilder overlapMessage) = ContourHelper.ContourOverlap(dummyBox, TS_legs, 0.0);
            if (failOverlap)
            {
                ProvideUIUpdate(overlapMessage.ToString(), true);
                return true;
            }
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Contoured overlap between: {TS_legs.Id} and {dummyBox.Id} onto {TS_legs.Id}");
            return false;
        }

        /// <summary>
        /// Simple helper method to crop the supplied target from the supplied dummy box and matchline structures
        /// </summary>
        /// <param name="addedTSTarget"></param>
        /// <param name="matchline"></param>
        /// <param name="dummyBox"></param>
        /// <returns></returns>
        private bool CutTSTargetFromMatchline(Structure addedTSTarget, Structure matchline, Structure dummyBox)
        {
            UpdateUILabel($"Cut {addedTSTarget.Id} at matchline:");
            //subtract both dummybox and matchline from TS_PTV_VMAT
            (bool failCropBox, StringBuilder cropBoxMessage) = ContourHelper.CropStructureFromStructure(addedTSTarget, dummyBox, 0.0);
            if (failCropBox)
            {
                ProvideUIUpdate(cropBoxMessage.ToString(), true);
                return true;
            }
            ProvideUIUpdate($"Cropped target {addedTSTarget.Id} from {dummyBox.Id}");

            (bool failCropMatch, StringBuilder cropMatchMessage) = ContourHelper.CropStructureFromStructure(addedTSTarget, matchline, 0.0);
            if (failCropMatch)
            {
                ProvideUIUpdate(cropMatchMessage.ToString(), true);
                return true;
            }
            ProvideUIUpdate($"Cropped target {addedTSTarget.Id} from {matchline.Id}");
           
            return false;
        }

        /// <summary>
        /// Helper method to create virtual bolus based on a specific OAR structure or on the entire body
        /// </summary>
        /// <param name="bolusFlash"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Utility method for creating virtual bolus/flash
        /// </summary>
        /// <returns></returns>
        private bool CreateFlash()
        {
            UpdateUILabel("Create flash:");
            int percentComplete = 0;
            int calcItems = 13;
            //create flash for the plan per the users request
            //NOTE: IT IS IMPORTANT THAT ALL OF THE STRUCTURES CREATED IN THIS METHOD (I.E., ALL STRUCTURES USED TO GENERATE FLASH HAVE THE KEYWORD 'FLASH' SOMEWHERE IN THE STRUCTURE ID)!
            //first need to create a bolus structure (remove it if it already exists)
            (bool failBolus, Structure bolusFlash) = RemoveAndGenerateStructure("BOLUS_FLASH");
            if (failBolus) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Created structure: {bolusFlash.Id}");

            //contour the bolus
            if (ContourBolus(bolusFlash)) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Contoured bolus structure: {bolusFlash.Id}");

            //get body structure
            Structure body = StructureTuningHelper.GetStructureFromId("body", selectedSS);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved body structure: {body.Id}");

            //now subtract the body structure from the newly-created bolus structure
            (bool failCrop, StringBuilder cropMessage) = ContourHelper.CropStructureFromStructure(bolusFlash, body, 0.0);
            if (failCrop)
            {
                ProvideUIUpdate(cropMessage.ToString(), true);
                return true;
            }
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Cropped {body.Id} from {bolusFlash.Id}");

            //if the subtracted structre is empty, then that indicates that the structure used to generate the flash was NOT close enough to the body surface to generate flash for the user-specified margin
            if (bolusFlash.IsEmpty)
            {
                ProvideUIUpdate("Error! Created bolus structure does not extrude from the body! \nIs this the right structure?", true);
                return true;
            }

            //assign the water to the bolus volume (HU = 0.0)
            bolusFlash.SetAssignedHU(0.0);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Assigned {bolusFlash.Id} HU to 0.0");

            if (flashStructure == null && StructureTuningHelper.DoesStructureExistInSS("matchline", selectedSS, true))
            {
                //crop flash at matchline ONLY if global flash is used
                Structure dummyBox = StructureTuningHelper.GetStructureFromId("dummybox", selectedSS);
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved dummy box structure: {dummyBox.Id}");

                if (CutTSTargetFromMatchline(bolusFlash, StructureTuningHelper.GetStructureFromId("matchline", selectedSS), dummyBox)) return true;
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Cut {bolusFlash.Id} structure at matchline structure");
            }

            //Now extend the body contour to include the bolus_flash structure. The reason for this is because Eclipse automatically sets the dose calculation grid to the body structure contour (no overriding this)
            (bool unionFail, StringBuilder unionMessage) = ContourHelper.ContourUnion(bolusFlash, body, 0.0);
            if (unionFail)
            {
                ProvideUIUpdate(unionMessage.ToString(), true);
                return true;
            }
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Contour union betwen between {bolusFlash.Id} and body onto body");

            //now create the ptv_flash structure
            (bool failPTVFlash, Structure ptvBodyFlash) = RemoveAndGenerateStructure("PTV_BODY_FLASH");
            if (failPTVFlash) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Created structure: {ptvBodyFlash.Id}");

            //copy the NEW body structure (i.e., body + bolus_flash)
            if (GeneratePTVFromBody(ptvBodyFlash)) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Contoured {ptvBodyFlash.Id} structure from body structure");

            foreach (RequestedTSManipulationModel itr in TSManipulationList.Where(x => x.ManipulationType == TSManipulationType.ContourOverlapWithTarget || x.ManipulationType == TSManipulationType.CropTargetFromStructure))
            {
                ManipulateTuningStructures(itr, ptvBodyFlash);
                ProvideUIUpdate(100 * ++percentComplete / calcItems);
            }

            //now create the ptv_flash structure (analogous to PTV_Body)
            (bool failFlashTarget, Structure TSPTVFlash) = RemoveAndGenerateStructure("TS_PTV_FLASH");
            if (failFlashTarget) return true;
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Created structure: {TSPTVFlash.Id}");

            (bool failCopyTarget, StringBuilder copyErrorMessage) = ContourHelper.CopyStructureOntoStructure(ptvBodyFlash, TSPTVFlash);
            if (failCopyTarget)
            {
                ProvideUIUpdate(copyErrorMessage.ToString(), true);
                return true;
            }
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Copied structure {ptvBodyFlash.Id} onto {TSPTVFlash.Id}");

            if(StructureTuningHelper.DoesStructureExistInSS("matchline", selectedSS, true))
            {
                //crop flash at matchline ONLY if global flash is used
                Structure dummyBox = StructureTuningHelper.GetStructureFromId("dummybox", selectedSS);
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved dummy box structure: {dummyBox.Id}");

                if (CutTSTargetFromMatchline(TSPTVFlash, StructureTuningHelper.GetStructureFromId("matchline", selectedSS), dummyBox)) return true;
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Cut {TSPTVFlash.Id} structure at matchline structure");
            }
            NormalizationVolumes = new Dictionary<string, string>(UpdateNormVolumesWithFlash(NormalizationVolumes));
            PlanTargets = new List<PlanTargetsModel>(UpdateTsTargetsWithFlash(PlanTargets));
            return false;
        }

        /// <summary>
        /// Helper method to update the TS targets list with the analogous flash targets
        /// </summary>
        /// <returns></returns>
        private List<PlanTargetsModel> UpdateTsTargetsWithFlash(List<PlanTargetsModel> plantargets)
        {
            //we know ts_PTV_VMAT was listed as a ts target, so we will need to go in and replace that with the corresponding flash targets
            List<TargetModel> targets = plantargets.First().Targets;
            if(targets.Any(x => string.Equals(x.TsTargetId, "TS_PTV_VMAT", StringComparison.OrdinalIgnoreCase)))
            {
                targets.First(x => string.Equals(x.TsTargetId, "TS_PTV_VMAT", StringComparison.OrdinalIgnoreCase)).TsTargetId = "TS_PTV_FLASH";
            }
            return plantargets;
        }

        /// <summary>
        /// Helper method to update the normalization volumes list with the analogous flash targets
        /// </summary>
        /// <returns></returns>
        private Dictionary<string,string> UpdateNormVolumesWithFlash(Dictionary<string,string> volumes)
        {
            Dictionary<string,string> updatedNormVolumes = new Dictionary<string,string>(volumes);
            //only update the normalization volumes if ts_ptv_vmat was set to the normalization volume for this plan
            if (string.Equals(updatedNormVolumes.First().Value, "TS_PTV_VMAT"))
            {
                //normalization volume for plan is ts_ptv_vmat
                //--> update to ts_ptv_flash
                updatedNormVolumes.Clear();
                updatedNormVolumes.Add(prescriptions.First().PlanId, "TS_PTV_FLASH");
            }
            return updatedNormVolumes;
        }

        /// <summary>
        /// Method to calculate the required number of VMAT isocenters and the total number of isocenters (including AP/PA isocenters is needed)
        /// </summary>
        /// <returns></returns>
        protected override bool CalculateNumIsos()
        {
            UpdateUILabel("Calculate number of isos:");
            int percentComplete = 0;
            int calcItems = 5;
            Structure body = StructureTuningHelper.GetStructureFromId("body", selectedSS);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved body structure");
            Point3DCollection pts = body.MeshGeometry.Positions;
            double bodyExtent = pts.Max(p => p.Z) - pts.Min(p => p.Z);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Calculated maximum extent of body: {bodyExtent:0.0} mm");

            double minFieldOverlap = 20.0;
            double maxFieldExtent = 400.0;
            //calculate number of required isocenters
            if (!StructureTuningHelper.DoesStructureExistInSS("matchline", selectedSS))
            {
                ProvideUIUpdate("matchline structure not present in structure set");
                //no matchline implying that this patient will be treated with VMAT only. For these cases the maximum number of allowed isocenters is 3.
                //the reason for the explicit statements calculating the number of isos and then truncating them to 3 was to account for patients requiring < 3 isos and if, later on, we want to remove the restriction of 3 isos
                NumberofIsocenters = NumberofVMATIsocenters = (int)Math.Ceiling(bodyExtent / (maxFieldExtent - minFieldOverlap));
                if (NumberofIsocenters > 3) NumberofIsocenters = NumberofVMATIsocenters = 3;
                ProvideUIUpdate(100 * ++percentComplete / calcItems);
            }
            else
            {
                //matchline structure is present, but empty
                if (!StructureTuningHelper.DoesStructureExistInSS("matchline", selectedSS, true))
                {
                    ConfirmPrompt CP = new ConfirmPrompt("I found a matchline structure in the structure set, but it's empty!" + Environment.NewLine + Environment.NewLine + "Do you want to continue without using the matchline structure?!");
                    CP.ShowDialog();
                    if (!CP.GetSelection()) return true;

                    //continue and ignore the empty matchline structure (same calculation as VMAT only)
                    NumberofIsocenters = NumberofVMATIsocenters = (int)Math.Ceiling(bodyExtent / (maxFieldExtent - minFieldOverlap));
                    if (NumberofIsocenters > 3) NumberofIsocenters = NumberofVMATIsocenters = 3;
                    ProvideUIUpdate(100 * ++percentComplete / calcItems);

                }
                //matchline structure is present and not empty
                else
                {
                    calcItems += 2;
                    Structure matchline = StructureTuningHelper.GetStructureFromId("matchline", selectedSS);
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved matchline structure");
                    //get number of isos for PTV superior to matchplane (always truncate this value to a maximum of 4 isocenters)
                    NumberofVMATIsocenters = (int)Math.Ceiling((pts.Max(p => p.Z) - matchline.CenterPoint.z) / (maxFieldExtent - minFieldOverlap));
                    if (NumberofVMATIsocenters > 4) NumberofVMATIsocenters = 4;
                    ProvideUIUpdate($"Separation between body z max and matchline center z: {(pts.Max(p => p.Z) - matchline.CenterPoint.z):0.0}");
                    ProvideUIUpdate($"numVAMTIsos calculated as double: {(pts.Max(p => p.Z) - matchline.CenterPoint.z) / (maxFieldExtent - minFieldOverlap):0.0}");
                    ProvideUIUpdate(100 * ++percentComplete / calcItems);

                    //Only add a second legs iso if the extent of the body is > 40.0 cm
                    ProvideUIUpdate($"Separation between matchline z center and body z min: {matchline.CenterPoint.z - pts.Min(p => p.Z):0.0}");
                    if (matchline.CenterPoint.z - pts.Min(p => p.Z) <= maxFieldExtent)
                    {
                        ProvideUIUpdate($"Separation between matchline z center and body z min is <= maximum field extent ({maxFieldExtent})");
                        ProvideUIUpdate($"Only one APPA isocenters is required for coverage");
                        NumberofIsocenters = NumberofVMATIsocenters + 1;
                    }
                    else
                    {
                        ProvideUIUpdate($"Separation between matchline z center and body z min is > maximum field extent ({maxFieldExtent})");
                        ProvideUIUpdate($"Two APPA isocenters are required for coverage");
                        NumberofIsocenters = NumberofVMATIsocenters + 2;
                    }
                    ProvideUIUpdate(100 * ++percentComplete / calcItems);
                }
            }
            ProvideUIUpdate($"Calculated required number of VMAT Isos: {NumberofVMATIsocenters}");
            ProvideUIUpdate($"Calculated total number of Isos: {NumberofIsocenters}");

            //set isocenter names based on numIsos and numVMATIsos (determined these names from prior cases)
            PlanIsocentersList.Add(new PlanIsocenterModel(prescriptions.First().PlanId, IsoNameHelper.GetTBIVMATIsoNames(NumberofVMATIsocenters, NumberofIsocenters)));
            if (NumberofIsocenters > NumberofVMATIsocenters)
            {
                if (NumberofIsocenters == NumberofVMATIsocenters + 2)
                {
                    PlanIsocentersList.Add(new PlanIsocenterModel("_upper legs", new IsocenterModel("upper legs")));
                    PlanIsocentersList.Add(new PlanIsocenterModel("_lower legs", new IsocenterModel("lower legs")));
                }
                else
                {
                    PlanIsocentersList.Add(new PlanIsocenterModel("_legs", new IsocenterModel("legs")));
                }
            }
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved appropriate isocenter names:");
            foreach(PlanIsocenterModel itr in PlanIsocentersList)
            {
                ProvideUIUpdate($"Plan Id: {itr.PlanId}");
                foreach(IsocenterModel itr1 in itr.Isocenters)
                {
                    ProvideUIUpdate($" {itr1.IsocenterId}");
                }
            }
            return false;
        }
    }
}
