using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Media.Media3D;
using VMATAutoPlanMT.baseClasses;
using VMATTBICSIAutoplanningHelpers.Helpers;
using VMATTBICSIAutoplanningHelpers.UIHelpers;
using System.Runtime.ExceptionServices;

namespace VMATAutoPlanMT.VMAT_CSI
{
    public class generateTS_CSI : generateTSbase
    { 
        //structure, sparing type, added margin
        public List<Tuple<string, string, double>> spareStructList;
        //DICOM types
        //Possible values are "AVOIDANCE", "CAVITY", "CONTRAST_AGENT", "CTV", "EXTERNAL", "GTV", "IRRAD_VOLUME", 
        //"ORGAN", "PTV", "TREATED_VOLUME", "SUPPORT", "FIXATION", "CONTROL", and "DOSE_REGION". 
        protected List<Tuple<string, string>> TS_structures;
        List<Tuple<string, double, string>> targets;
        List<Tuple<string, string, int, DoseValue, double>> prescriptions;
        protected int numIsos;
        protected int numVMATIsos;
        public bool updateSparingList = false;

        public generateTS_CSI(List<Tuple<string, string>> ts, List<Tuple<string, string, double>> list, List<Tuple<string, double, string>> targs, List<Tuple<string,string,int,DoseValue,double>> presc, StructureSet ss)
        {
            TS_structures = new List<Tuple<string, string>>(ts);
            spareStructList = new List<Tuple<string, string, double>>(list);
            targets = new List<Tuple<string, double, string>>(targs);
            prescriptions = new List<Tuple<string, string, int, DoseValue, double>>(presc);
            selectedSS = ss;
        }

        #region Run Control
        //to handle system access exception violation
        [HandleProcessCorruptedStateExceptions]
        public override bool Run()
        {
            try
            {
                isoNames.Clear();
                if (preliminaryChecks()) return true;
                if (UnionLRStructures()) return true;
                if (spareStructList.Any()) if (CheckHighResolution()) return true;
                //remove all only ts structures NOT including targets
                if (RemoveOldTSStructures(TS_structures.Where(x => !x.Item2.ToLower().Contains("ctv") && !x.Item2.ToLower().Contains("ptv")).ToList())) return true;
                if (CheckForTargetStructures()) return true;
                //if (createTargetStructures()) return true;
                if (createTSStructures()) return true;
                if (performTSStructureManipulation()) return true;
                if (calculateNumIsos()) return true;
                UpdateUILabel("Finished!");
                ProvideUIUpdate(100, "Finished Structure Tuning!");
            }
            catch(Exception e) { ProvideUIUpdate(String.Format("{0}", e.Message), true); return true; }
            return false;
        }
        #endregion

        #region Preliminary Checks and Structure Unioning
        protected override bool preliminaryChecks()
        {
            UpdateUILabel("Performing Preliminary Checks: ");
            int calcItems = 2;
            int counter = 0;
            //check if user origin was set
            if (isUOriginInside(selectedSS)) return true;
            ProvideUIUpdate((int)(100 * ++counter / calcItems), "User origin is inside body");

            //verify brain and spine structures are present
            if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "brain") == null || selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "spinalcord" || x.Id.ToLower() == "spinal_cord") == null)
            {
                ProvideUIUpdate("Missing brain and/or spine structures! Please add and try again!", true);
                return true;
            }

            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Brain and spinal cord structures exist");
            ProvideUIUpdate(100, "Preliminary checks complete!");
            return false;
        }

        protected bool UnionLRStructures()
        {
            UpdateUILabel("Unioning Structures: ");
            ProvideUIUpdate(0, "Checking for L and R structures to union!");
            StructureTuningUIHelper helper = new StructureTuningUIHelper();
            List<Tuple<Structure, Structure, string>> structuresToUnion = helper.checkStructuresToUnion(selectedSS);
            if (structuresToUnion.Any())
            {
                int calcItems = structuresToUnion.Count;
                int numUnioned = 0;
                foreach (Tuple<Structure, Structure, string> itr in structuresToUnion)
                {
                    (bool, string) result = helper.unionLRStructures(itr, selectedSS);
                    if (!result.Item1) ProvideUIUpdate((int)(100 * ++numUnioned / calcItems), String.Format("Unioned {0}", itr.Item3));
                    else { ProvideUIUpdate(result.Item2, true); return true; }
                }
                ProvideUIUpdate(100, "Structures unioned successfully!");
            }
            else ProvideUIUpdate(100, "No structures to union!");
            return false;
        }

        private bool CheckHighResolution()
        {
            UpdateUILabel("High-Res Structures: ");
            ProvideUIUpdate("Checking for high resolution structures in structure set: ");
            List<Structure> highResStructList = new List<Structure> { };
            List<Tuple<string, string, double>> highResSpareList = new List<Tuple<string, string, double>> { };
            foreach (Tuple<string, string, double> itr in spareStructList)
            {
                if (itr.Item2 == "Crop target from structure")
                {
                    if (selectedSS.Structures.First(x => x.Id == itr.Item1).IsEmpty)
                    {
                        ProvideUIUpdate(String.Format("Requested {0} be cropped from target, but {0} is empty!", itr.Item1), true);
                        return true;
                    }
                    else if (selectedSS.Structures.First(x => x.Id == itr.Item1).IsHighResolution)
                    {
                        highResStructList.Add(selectedSS.Structures.First(x => x.Id == itr.Item1));
                        highResSpareList.Add(itr);
                    }
                }
            }
            //if there are high resolution structures, they will need to be converted to default resolution.
            if (highResStructList.Count() > 0)
            {
                ProvideUIUpdate("High-resolution structures:");
                foreach (Structure itr in highResStructList)
                {
                    string id = itr.Id;
                    ProvideUIUpdate(String.Format("{0}", id));
                }
                ProvideUIUpdate("Now converting to low-resolution!");
                //convert high res structures queued for TS manipulation to low resolution and update the queue with the resulting low res structure
                List<Tuple<string, string, double>> newData = convertHighToLowRes(highResStructList, highResSpareList, spareStructList);
                if (!newData.Any()) return true;
                spareStructList = new List<Tuple<string, string, double>>(newData);
                ProvideUIUpdate(100, "Finishing converting high resolution structures to default resolution");
                //inform the main UI class that the UI needs to be updated
                //updateSparingList = true;
            }
            else ProvideUIUpdate("No high resolution structures in the structure set!");
            return false;
        }
        #endregion

        #region Target Creation
        private bool CheckForTargetStructures()
        {
            UpdateUILabel("Checking For Missing Target Structures: ");
            ProvideUIUpdate(0, "Checking for missing target structures!");
            List<Tuple<string, string>> prospectiveTargets = TS_structures.Where(x => x.Item2.ToLower().Contains("ctv") || x.Item2.ToLower().Contains("ptv")).OrderBy(x => x.Item2).ToList();
            List<Tuple<string, string>> missingTargets = new List<Tuple<string, string>> { };
            int calcItems = prospectiveTargets.Count;
            int counter = 0;
            foreach (Tuple<string, string> itr in prospectiveTargets)
            {
                Structure tmp = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == itr.Item2.ToLower());
                if(tmp == null || tmp.IsEmpty)
                {
                    ProvideUIUpdate(String.Format("Target: {0} is missing or empty", itr.Item2));
                    missingTargets.Add(itr);
                }
                ProvideUIUpdate((int)(100 * ++counter / calcItems));
            }
            if (missingTargets.Any())
            {
                ProvideUIUpdate(String.Format("Targets missing from the structure set! Creating them now!"));
                if (createTargetStructures(missingTargets)) return true;
            }
            ProvideUIUpdate(String.Format("All requested targets are present and contoured! Skipping target creation!"));
            return false;
        }

        protected bool createTargetStructures(List<Tuple<string, string>> missingTargets)
        {
            UpdateUILabel("Create Missing Target Structures: ");
            ProvideUIUpdate(0, "Creating missing target structures!");
            //create the CTV and PTV structures
            //if these structures were present, they should have been removed (regardless if they were contoured or not). 
            List<Structure> addedTargets = new List<Structure> { };
            //List<Tuple<string, string>> prospectiveTargets = TS_structures.Where(x => x.Item2.ToLower().Contains("ctv") || x.Item2.ToLower().Contains("ptv")).OrderBy(x => x.Item2).ToList();
            //int calcItems = prospectiveTargets.Count;
            int calcItems = missingTargets.Count;
            int counter = 0;
            foreach (Tuple<string, string> itr in missingTargets)
            {
                if (selectedSS.CanAddStructure(itr.Item1, itr.Item2))
                {
                    addedStructures.Add(itr.Item2);
                    addedTargets.Add(selectedSS.AddStructure(itr.Item1, itr.Item2));
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Added target: {0}", itr.Item2));
                    //optParameters.Add(new Tuple<string,string>(itr.Item1, itr.Item2));
                }
                else
                {
                    ProvideUIUpdate(String.Format("Can't add {0} to the structure set!", itr.Item2), true);
                    //MessageBox.Show(String.Format("Can't add {0} to the structure set!", itr.Item2));
                    return true;
                }
            }

            Structure tmp = null;
            calcItems = addedTargets.Count + 5;
            counter = 0;
            foreach (Structure itr in addedTargets)
            {
                string targetId = itr.Id;
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Contouring target: {0}", targetId));
                if (itr.Id.ToLower().Contains("brain"))
                {
                    tmp = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "brain");
                    if (tmp != null && !tmp.IsEmpty)
                    {
                        if (itr.Id.ToLower().Contains("ctv"))
                        {
                            //CTV structure. Brain CTV IS the brain structure
                            itr.SegmentVolume = tmp.Margin(0.0);
                        }
                        else
                        {
                            //PTV structure
                            //5 mm uniform margin to generate PTV
                            itr.SegmentVolume = tmp.Margin(5.0);
                        }
                    }
                    else { ProvideUIUpdate(String.Format("Error! Could not retrieve brain structure! Exiting!"), true); return true; }
                }
                else if(itr.Id.ToLower().Contains("spine"))
                {
                    tmp = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "spinalcord" || x.Id.ToLower() == "spinal_cord");
                    if (tmp != null && !tmp.IsEmpty)
                    {
                        if (itr.Id.ToLower().Contains("ctv"))
                        {
                            //CTV structure. Brain CTV IS the brain structure
                            //AxisAlignedMargins(inner or outer margin, margin from negative x, margin for negative y, margin for negative z, margin for positive x, margin for positive y, margin for positive z)
                            //according to Nataliya: CTV_spine = spinal_cord+0.5cm ANT, +1.5cm Inf, and +1.0 cm in all other directions
                            itr.SegmentVolume = tmp.AsymmetricMargin(new AxisAlignedMargins(StructureMarginGeometry.Outer,
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
                            tmp = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ctv_spine");
                            if (tmp != null && !tmp.IsEmpty) itr.SegmentVolume = tmp.Margin(5.0);
                            else { ProvideUIUpdate(String.Format("Error! Could not retrieve CTV_Spine structure! Exiting!"), true); return true; }
                        }
                    }
                    else { ProvideUIUpdate(String.Format("Error! Could not retrieve brain structure! Exiting!"), true); return true; }
                }
            }

            if(addedStructures.FirstOrDefault(x => x.ToLower() == "ptv_csi") != null)
            {
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Generating: PTV_CSI"));
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Retrieving: PTV_CSI, PTV_Brain, and PTV_Spine"));
                //used to create the ptv_csi structures
                Structure combinedTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_csi");
                Structure brainTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_brain"));
                Structure spineTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_spine"));
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Unioning PTV_Brain and PTV_Spine to make PTV_CSI"));
                combinedTarget.SegmentVolume = brainTarget.Margin(0.0);
                combinedTarget.SegmentVolume = combinedTarget.Or(spineTarget.Margin(0.0));

                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Cropping PTV_CSI from body with 3 mm inner margin"));
                //1/3/2022, crop PTV structure from body by 3mm
                if (cropStructureFromBody(combinedTarget, -0.3)) return true;
            }
            else ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("PTV_CSI already exists in the structure set! Skipping!"));
            ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Targets added and contoured!"));
            return false;
        }
        #endregion

        #region Crop, Boolean, Ring Operations
        protected bool cropStructureFromBody(Structure theStructure, double margin)
        {
            //margin is in cm
            Structure body = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "body");
            if (body != null)
            {
                if (margin >= -5.0 && margin <= 5.0) theStructure.SegmentVolume = theStructure.And(body.Margin(margin * 10));
                else { ProvideUIUpdate("Cropping margin from body MUST be within +/- 5.0 cm!", true); return true; }
            }
            else 
            { 
                ProvideUIUpdate("Could not find body structure! Can't crop target from body!", true); 
                return true; 
            }
            return false;
        }

        protected bool cropTargetFromStructure(Structure target, Structure normal, double margin)
        {
            //margin is in cm
            if (target != null && normal != null)
            {
                if (margin >= -5.0 && margin <= 5.0) target.SegmentVolume = target.Sub(normal.Margin(margin * 10));
                else { ProvideUIUpdate("Cropping margin MUST be within +/- 5.0 cm!", true); return true; }
            }
            else 
            { 
                ProvideUIUpdate("Error either target or normal structures are missing! Can't crop target from normal structure!", true); 
                return true; 
            }
            return false;
        }

        protected bool contourOverlap(Structure target, Structure normal, double margin)
        {
            //margin is in cm
            if (target != null && normal != null)
            {
                if (margin >= -5.0 && margin <= 5.0) normal.SegmentVolume = target.And(normal.Margin(margin * 10));
                else { ProvideUIUpdate("Added margin MUST be within +/- 5.0 cm!", true); return true; }
            }
            else 
            { 
                ProvideUIUpdate("Error either target or normal structures are missing! Can't contour overlap between target and normal structure!", true); 
                return true; 
            }
            return false;
        }

        private bool createRing(Structure target, Structure ring, double margin, double thickness)
        {
            //margin is in cm
            if ((margin >= -5.0 && margin <= 5.0) && (thickness + margin >= -5.0 && thickness + margin <= 5.0))
            {
                ring.SegmentVolume = target.Margin((thickness + margin) * 10);
                ring.SegmentVolume = ring.Sub(target.Margin(margin * 10));
                cropStructureFromBody(ring, 0.0);
            }
            else 
            { 
                ProvideUIUpdate("Added margin or ring thickness + margin MUST be within +/- 5.0 cm! Exiting!", true); 
                return true; 
            }
            return false;
        }

        private bool ContourPRVVolume(string baseStructureId, Structure addedStructure, double margin)
        {
            Structure baseStructure = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == baseStructureId.ToLower());
            if(baseStructure != null)
            {
                if (margin >= -5.0 && margin <= 5.0) addedStructure.SegmentVolume = baseStructure.Margin(margin * 10);
                else
                {
                    ProvideUIUpdate(String.Format("Error! Requested PRV margin ({0:0.0} cm) is outside +/- 5 cm! Exiting!", margin), true);
                    return true;
                }
            }
            else
            {
                ProvideUIUpdate(String.Format("Error! Cannot find base structure: {0}! Exiting!", baseStructureId), true);
                return true;
            }
            return false;
        }

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
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Grabbing margin value!"));
                if(!double.TryParse(addedStructure.Id.Substring(pos1, pos2 - pos1), out margin))
                {
                    ProvideUIUpdate(String.Format("Margin parse failed for sub structure: {0}!", addedStructure.Id), true);
                    return true;
                }
                ProvideUIUpdate(margin.ToString());

                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Grabbing original structure {0}", originalStructureId));
                //logic to handle case where the original structure had to be converted to low resolution
                if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains(originalStructureId.ToLower()) && x.Id.ToLower().Contains("_low")) == null) originalStructure = selectedSS.Structures.First(x => x.Id.ToLower().Contains(originalStructureId.ToLower()));
                else originalStructure = selectedSS.Structures.First(x => x.Id.ToLower().Contains(originalStructureId.ToLower()) && x.Id.ToLower().Contains("_low"));

                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Creating {0} structure!", margin > 0 ? "outer" : "sub"));
                //convert from cm to mm
                addedStructure.SegmentVolume = originalStructure.Margin(margin * 10);
                if (addedStructure.IsEmpty)
                {
                    ProvideUIUpdate(String.Format("{0} was contoured, but is empty! Removing!", addedStructure.Id));
                    selectedSS.RemoveStructure(addedStructure);
                }
                ProvideUIUpdate(100, String.Format("Finished contouring {0}", addedStructure.Id));
            }
            else
            {
                ProvideUIUpdate(String.Format("Error! I can't find the keywords '-' or '+', and 'cm' in the structure id for: {0}", addedStructure.Id), true);
                return true;
            }
            return false;
        }
        #endregion

        #region TS Structure Creation and Manipulation
        protected override bool createTSStructures()
        {
            UpdateUILabel("Create TS Structures: ");
            ProvideUIUpdate(String.Format("Adding remaining tuning structures to stack!"));
            //get all TS structures that do not contain 'ctv' or 'ptv' in the title
            List<Tuple<string, string>> remainingTS = TS_structures.Where(x => !x.Item2.ToLower().Contains("ctv") && !x.Item2.ToLower().Contains("ptv")).ToList();
            int calcItems = remainingTS.Count;
            int counter = 0;
            foreach (Tuple<string, string> itr in remainingTS)
            {
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Adding TS to added structures: {0}", itr.Item2));
                //if those structures have NOT been added to the added structure list, go ahead and add them to stack
                if (addedStructures.FirstOrDefault(x => x.ToLower() == itr.Item2) == null) AddTSStructures(itr);
            }

            ProvideUIUpdate(100, String.Format("Finished adding tuning structures!"));
            ProvideUIUpdate(0, String.Format("Contouring tuning structures!"));
            //now contour the various structures
            foreach (string itr in addedStructures)
            {
                counter = 0;
                ProvideUIUpdate(0, String.Format("Contouring TS: {0}", itr));
                Structure addedStructure = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == itr.ToLower());
                if (itr.ToLower().Contains("ts_ring"))
                {
                    if (double.TryParse(itr.Substring(7, itr.Length - 7), out double ringDose))
                    {
                        calcItems = targets.Count;
                        foreach (Tuple<string, double, string> itr1 in targets)
                        {
                            Structure targetStructure = selectedSS.Structures.FirstOrDefault(x => x.Id == itr1.Item1);
                            if (targetStructure != null)
                            {
                                ProvideUIUpdate(String.Format("Generating ring {0} for target {1}", itr, itr1.Item1));
                                //margin in cm. 
                                double margin = ((itr1.Item2 - ringDose) / itr1.Item2) * 3.0;
                                if (margin > 0.0)
                                {
                                    //method to create ring of 2.0 cm thickness
                                    //first create structure that is a copy of the target structure with an outer margin of ((Rx - ring dose / Rx) * 30 mm) + 20 mm.
                                    //1/5/2023, nataliya stated the 50% Rx ring should be 1.5 cm from the target and have a thickness of 2 cm. Redefined the margin formula to equal 15 mm whenever (Rx - ring dose) / Rx = 0.5
                                    //addedStructure.SegmentVolume = addedStructure.Sub(targetStructure.Margin(margin));
                                    //keep only the parts of the ring that are inside the body!
                                    double thickness = margin + 2.0 > 5.0 ? 5.0 - margin : 2.0;
                                    if (createRing(targetStructure, addedStructure, margin, thickness)) return true;
                                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Finished contouring ring: {0}", itr));
                                }
                            }
                        }
                    }
                    else ProvideUIUpdate(String.Format("Could not parse ring dose for {0}! Skipping!", itr));
                }
                else if (itr.ToLower().Contains("ts_globes") || itr.ToLower().Contains("ts_lenses"))
                {
                    calcItems = 4;
                    //try to grab ptv_brain first
                    Structure targetStructure = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_brain" && !x.IsEmpty);
                    double margin = 0;

                    if (targetStructure == null)
                    {
                        //could not retrieve ptv_brain
                        calcItems += 1;
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Failed to retrieve PTV_Brain! Attempting to retrieve brain structure: {0}", "Brain"));
                        targetStructure = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "brain" && !x.IsEmpty);
                        //additional 5 mm margin for ring creation to account for the missing 5 mm margin going from brain --> PTV_Vrain
                        margin = 0.5;
                    }
                    if (targetStructure != null)
                    {
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Retrieved brain target: {0}", targetStructure.Id));
                        Structure normal = null;
                        if (itr.ToLower().Contains("globes")) normal = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("globes") && !x.IsEmpty);
                        else normal = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("lenses") && !x.IsEmpty);

                        if (normal != null)
                        {
                            ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Retrieved structure: {0}", normal.Id));
                            ProvideUIUpdate(String.Format("Generating ring {0} for target {1}", itr, targetStructure.Id));
                            //margin in cm. 
                            double thickness = 0;
                            if (itr.ToLower().Contains("globes"))
                            {
                                //need to add these margins to the existing margin distance to account for the situation where ptv_brain is not retrieved, but the brain structure is.
                                margin += 0.5;
                                thickness = 1.0;
                            }
                            else
                            {
                                margin += 1.5;
                                thickness = 2.0;
                            }
                            if (createRing(targetStructure, addedStructure, margin, thickness)) return true;
                            ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Finished contouring ring: {0}", addedStructure.Id));

                            ProvideUIUpdate(String.Format("Contouring overlap between ring and {0}", itr.ToLower().Contains("globes") ? "Globes" : "Lenses"));
                            if (contourOverlap(normal, addedStructure, 0.0)) return true;
                            ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Overlap Contoured!"));

                            if (addedStructure.IsEmpty)
                            {
                                ProvideUIUpdate(String.Format("{0} is empty! Removing now!", itr));
                                calcItems += 1;
                                selectedSS.RemoveStructure(addedStructure);
                                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Removed structure: {0}", itr));
                            }
                            ProvideUIUpdate(String.Format("Finished contouring: {0}", itr));
                        }
                        else ProvideUIUpdate(String.Format("Warning! Could not retrieve normal structure! Skipping {0}", itr));
                    }
                    else ProvideUIUpdate(String.Format("Warning! Could not retrieve Brain structure! Skipping {0}", itr));
                }
                else if (itr.ToLower().Contains("armsavoid"))
                {
                    if (CreateArmsAvoid(addedStructure)) return true;
                }
                else if (itr.ToLower().Contains("_prv"))
                {
                    //leave margin as 0.3 cm outer by default
                    if(ContourPRVVolume(addedStructure.Id.Substring(0, addedStructure.Id.LastIndexOf("_")), addedStructure, 0.3)) return true;
                }
                else if (!(itr.ToLower().Contains("ptv")))
                {
                    if (ContourInnerOuterStructure(addedStructure, ref counter)) return true;
                }
            }
            return false;
        }

        protected bool CreateArmsAvoid(Structure armsAvoid)
        {
            ProvideUIUpdate(String.Format("Preparing to contour TS_arms..."));
            //generate arms avoid structures
            //need lungs, body, and ptv spine structures
            Structure lungs = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "lungs" && !x.IsEmpty);
            Structure body = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "body" && !x.IsEmpty);
            if(lungs == null || body == null)
            {
                ProvideUIUpdate("Error! Body and/or lungs structures were not found or are empty! Exiting!", true);
                return true;
            }
            MeshGeometry3D mesh = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_csi").MeshGeometry;
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
            if (selectedSS.Structures.Where(x => x.Id.ToLower() == "dummyboxl").Any()) selectedSS.RemoveStructure(selectedSS.Structures.First(x => x.Id.ToLower() == "dummyboxl"));
            Structure dummyBoxL = selectedSS.AddStructure("CONTROL", "DummyBoxL");
            if (selectedSS.Structures.Where(x => x.Id.ToLower() == "dummyboxr").Any()) selectedSS.RemoveStructure(selectedSS.Structures.First(x => x.Id.ToLower() == "dummyboxr"));
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

            ProvideUIUpdate(String.Format("Number of image slices to contour: {0}", stopSlice - startSlice));
            ProvideUIUpdate(String.Format("Preparation complete!"));
            ProvideUIUpdate(String.Format("Contouring TS_arms now..."));
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

            ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Unioning left and right arms avoid structures together!"));
            //now contour the arms avoid structure as the union of the left and right dummy boxes
            armsAvoid.SegmentVolume = dummyBoxL.Margin(0.0);
            armsAvoid.SegmentVolume = armsAvoid.Or(dummyBoxR.Margin(0.0));
            ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Contouring overlap between arms avoid and body with 5mm outer margin!"));
            //contour the arms as the overlap between the current armsAvoid structure and the body with a 5mm outer margin
            if (cropStructureFromBody(armsAvoid, 0.5)) return true;

            ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Cleaning up!"));
            selectedSS.RemoveStructure(dummyBoxR);
            selectedSS.RemoveStructure(dummyBoxL);
            ProvideUIUpdate(100, String.Format("Finished contouring arms avoid!"));

            return false;
        }

        private bool CheckIsOverlap(Structure target, Structure normal, double margin)
        {
            bool isOverlap = false;
            Structure dummy = selectedSS.AddStructure("CONTROL", "Dummy");
            dummy.SegmentVolume = target.And(normal.Margin(margin * 10.0));
            if (dummy.IsEmpty) isOverlap = true;
            selectedSS.RemoveStructure(dummy);
            return isOverlap;
        }

        private bool CheckAllRequestedTargetCropManipulations(List<Tuple<string, string, double>> cropSpareList)
        {
            foreach (Tuple<string, string, double> itr in cropSpareList)
            {
                Structure normal = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == itr.Item1.ToLower());
                if (normal != null)
                {
                    List<Tuple<string, string>> tgts = new TargetsHelper().GetPlanTargetList(prescriptions);
                    //verify structures requested for cropping target from structure actually overlap with structure
                    //planid, targetid
                    foreach (Tuple<string, string> itr1 in tgts)
                    {
                        Structure target = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == itr1.Item2.ToLower());
                        if (target != null)
                        {
                            if (CheckIsOverlap(target, normal, itr.Item3))
                            {
                                ProvideUIUpdate(String.Format("Warning! {0} does not overlap with all plan target ({1}) structures! Removing from TS manipulation list!", normal.Id, target.Id));
                                spareStructList.RemoveAt(spareStructList.IndexOf(itr));
                            }
                            else ProvideUIUpdate(String.Format("{0} overlaps with target {1}", normal.Id, target.Id));
                        }
                        else ProvideUIUpdate(String.Format("Warning! Could not retrieve target: {0}! Skipping", itr1.Item2));
                    }
                }
                else ProvideUIUpdate(String.Format("Warning! Could not retrieve structure: {0}! Skipping", itr.Item1));
            }
            return false;
        }

        private bool performTSStructureManipulation()
        {
            //get target plan list
            //get list of structure manipulations that require cropping target from structure
            //evaluate overlap of each structure with each target
            //if structure dose not overlap BOTH targets, remove from structure manipulations list and remove added structure
            UpdateUILabel("Perform TS Manipulations: ");
            List<Tuple<string, string, double>> cropSpareList = spareStructList.Where(x => x.Item2.Contains("Crop target from structure")).ToList();
            if (cropSpareList.Any())
            {
                ProvideUIUpdate("Evaluating overlap between targets and normal structures requested for target cropping!");
                if(CheckAllRequestedTargetCropManipulations(cropSpareList)) return true;
            }
            ProvideUIUpdate(String.Format("Retrieved remaining list of TS manipulations"));
            //there are items in the sparing list requiring structure manipulation
            List<Tuple<string, string, double>> tmpSpareLst = spareStructList.Where(x => x.Item2.Contains("Crop target from structure") || x.Item2.Contains("Contour")).ToList();
            int counter = 0;
            int calcItems = tmpSpareLst.Count * targets.Count;
            foreach (Tuple<string, double, string> itr in targets)
            {
                //create a new TS target for optimization and copy the original target structure onto the new TS structure
                string newName = String.Format("TS_{0}", itr.Item1);
                if (newName.Length > 16) newName = newName.Substring(0, 16);
                ProvideUIUpdate(String.Format("Retrieving TS target: {0}", newName));
                Structure addedTSTarget = selectedSS.Structures.FirstOrDefault(x => x.Id == newName);
                if (addedTSTarget == null)
                {
                    ProvideUIUpdate(String.Format("TS target {0} does not exist. Creating it now!", newName));
                    addedTSTarget = AddTSStructures(new Tuple<string, string>("CONTROL", newName));
                    addedTSTarget.SegmentVolume = selectedSS.Structures.FirstOrDefault(x => x.Id == itr.Item1).Margin(0.0);
                }
                ProvideUIUpdate(String.Format("Cropping TS target from body with {0} mm inner margin", 3.0));
                if (cropStructureFromBody(addedTSTarget, -0.3)) return true;
                if (tmpSpareLst.Any())
                {
                    foreach (Tuple<string, string, double> itr1 in spareStructList)
                    {
                        Structure theStructure = selectedSS.Structures.FirstOrDefault(x => x.Id == itr1.Item1);
                        if (itr1.Item2.Contains("Crop"))
                        {
                            if(itr1.Item2.Contains("Body"))
                            {
                                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Cropping {0} from Body with margin {1} cm", itr1.Item1, itr1.Item3));
                                //crop from body
                                if(cropStructureFromBody(theStructure, itr1.Item3)) return true;
                            }
                            else
                            {
                                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Cropping target {0} from {1} with margin {2} cm", newName, itr1.Item1, itr1.Item3));
                                //crop target from structure
                                if(cropTargetFromStructure(addedTSTarget, theStructure, itr1.Item3)) return true;
                            }
                        }
                        else if(itr1.Item2.Contains("Contour"))
                        {
                            ProvideUIUpdate(String.Format("Contouring overlap between {0} and {1}", itr1.Item1, newName));
                            newName = String.Format("ts_{0}&&{1}", itr1.Item1, itr.Item1);
                            if (newName.Length > 16) newName = newName.Substring(0, 16);
                            Structure addedTSNormal = AddTSStructures(new Tuple<string, string>("CONTROL", newName));
                            Structure originalNormal = selectedSS.Structures.FirstOrDefault(x => x.Id == itr1.Item1);
                            addedTSNormal.SegmentVolume = originalNormal.Margin(0.0);
                            if(contourOverlap(addedTSTarget, addedTSNormal, itr1.Item3)) return true;
                            if (addedTSNormal.IsEmpty)
                            {
                                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("{0} was contoured, but it's empty! Removing!", newName));
                                selectedSS.RemoveStructure(addedTSNormal);
                            }
                            else ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Finished contouring {0}", newName));
                        }
                    }
                }
                else ProvideUIUpdate(String.Format("No TS manipulations requested!"));
            }
            return false;
        }
        #endregion

        #region Isocenter Calculation
        protected bool calculateNumIsos()
        {
            UpdateUILabel("Calculating Number of Isocenters:");
            ProvideUIUpdate("Calculating number of isocenters");
            int calcItems = 1;
            int counter = 0;
            //For these cases the maximum number of allowed isocenters is 3. One isocenter is reserved for the brain and either one or two isocenters are used for the spine (depending on length).
            //revised to get the number of unique plans list, for each unique plan, find the target with the greatest z-extent and determine the number of isocenters based off that target. 
            //plan Id, list of targets assigned to that plan
            List<Tuple<string, List<string>>> planIdTargets = new List<Tuple<string, List<string>>> { };
            string tmpPlanId = prescriptions.First().Item1;
            List<string> targs = new List<string> { };
            foreach (Tuple<string, string, int, DoseValue, double> itr in prescriptions)
            {
                if (itr.Item1 != tmpPlanId)
                {
                    planIdTargets.Add(new Tuple<string, List<string>>(tmpPlanId, new List<string>(targs)));
                    tmpPlanId = itr.Item1;
                    targs = new List<string> { itr.Item2 };
                }
                else targs.Add(itr.Item2);
            }
            planIdTargets.Add(new Tuple<string, List<string>>(tmpPlanId, new List<string>(targs)));
            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Generated list of plans each containing list of targets");

            foreach (Tuple<string, List<string>> itr in planIdTargets)
            {
                calcItems = itr.Item2.Count;
                counter = 0;
                //determine for each plan which target has the greatest z-extent
                double maxTargetLength = 0.0;
                string longestTargetInPlan = "";
                foreach (string s in itr.Item2)
                {
                    Structure targStruct = selectedSS.Structures.FirstOrDefault(x => x.Id == itr.Item2.First());
                    if (targStruct == null || targStruct.IsEmpty)
                    {
                        ProvideUIUpdate(String.Format("Error! No structure named: {0} found or contoured!", s), true);
                        return true;
                    }
                    Point3DCollection pts = targStruct.MeshGeometry.Positions;
                    double diff = pts.Max(p => p.Z) - pts.Min(p => p.Z);
                    if (diff > maxTargetLength) { longestTargetInPlan = s; maxTargetLength = diff; }
                    ProvideUIUpdate((int)(100 * ++counter / calcItems));
                }
                ProvideUIUpdate(String.Format("Determined target with greatest extent: {0}, Plan: {1}", longestTargetInPlan, itr.Item1));

                counter = 0;
                calcItems = 3;
                //If the target ID is PTV_CSI, calculate the number of isocenters based on PTV_spine and add one iso for the brain
                //planId, target list
                if (longestTargetInPlan.ToLower() == "ptv_csi")
                {
                    calcItems += 1;
                    //special rules for initial plan, which should have a target named PTV_CSI
                    //first, determine the number of isocenters required to treat PTV_Spine
                    //
                    //2/10/2023 according to Nataliya, PTV_Spine might not be present in the structure set 100% of the time. Therefore, just grab the spinal cord structure and add the margins
                    //used to create PTV_Spine (0.5 cm Ant and 1.5 cm Inf) manually.
                    Structure spineTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "spinalcord" || x.Id.ToLower() == "spinal_cord");
                    if (spineTarget == null || spineTarget.IsEmpty)
                    {
                        ProvideUIUpdate(String.Format("Error! No structure named spinalcord or spinal_cord was found or it was empty!"), true);
                        return true;
                    }
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), "Retrieved spinal cord structure");

                    Point3DCollection pts = spineTarget.MeshGeometry.Positions;
                    //ESAPI default distances are in mm
                    double addedMargin = 20.0;
                    double spineTargetExtent = (pts.Max(p => p.Z) - pts.Min(p => p.Z)) + addedMargin;
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
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("{0:0.0}, {1:0.0}, {2:0.0}", spineTargetExtent, (spineTargetExtent - 50.0) / (400.0 - 20.0), numVMATIsos));

                    //one iso reserved for PTV_Brain
                    numVMATIsos += 1;
                }
                else
                {
                    numVMATIsos = (int)Math.Ceiling(maxTargetLength / (400.0 - 20.0));
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("{0}", numVMATIsos));
                }
                if (numVMATIsos > 3) numVMATIsos = 3;

                //set isocenter names based on numIsos and numVMATIsos (be sure to pass 'true' for the third argument to indicate that this is a CSI plan(s))
                //plan Id, list of isocenter names for this plan
                isoNames.Add(Tuple.Create(itr.Item1, new List<string>(new IsoNameHelper().getIsoNames(numVMATIsos, numVMATIsos, true))));
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Added isocenter to stack!"));
            }
            ProvideUIUpdate(String.Format("Required Number of Isocenters: {0}", numVMATIsos));
            return false;
        }
        #endregion
    }
}