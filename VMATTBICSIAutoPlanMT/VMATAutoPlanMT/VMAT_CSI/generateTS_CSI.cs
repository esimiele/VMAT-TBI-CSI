using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Media.Media3D;
using VMATAutoPlanMT.baseClasses;
using VMATAutoPlanMT.helpers;
using VMATAutoPlanMT.Prompts;
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
        public List<Tuple<string, string>> TS_structures;
        List<Tuple<string, double, string>> targets;
        List<Tuple<string, string, int, DoseValue, double>> prescriptions;
        public int numIsos;
        public int numVMATIsos;
        public bool updateSparingList = false;

        public generateTS_CSI(List<Tuple<string, string>> ts, List<Tuple<string, string, double>> list, List<Tuple<string, double, string>> targs, List<Tuple<string,string,int,DoseValue,double>> presc, StructureSet ss)
        {
            TS_structures = new List<Tuple<string, string>>(ts);
            spareStructList = new List<Tuple<string, string, double>>(list);
            targets = new List<Tuple<string, double, string>>(targs);
            prescriptions = new List<Tuple<string, string, int, DoseValue, double>>(presc);
            selectedSS = ss;
        }

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
            catch(Exception e) { ProvideUIUpdate(String.Format("{0}", e.Message)); return true; }
            return false;
        }

        public override bool preliminaryChecks()
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
                MessageBox.Show("Missing brain and/or spine structures! Please add and try again!");
                return true;
            }

            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Brain and spinal cord structures exist");
            ProvideUIUpdate(100, "Preliminary checks complete!");
            return false;
        }

        public bool UnionLRStructures()
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
                    if (!helper.unionLRStructures(itr, selectedSS)) ProvideUIUpdate((int)(100 * ++numUnioned / calcItems), String.Format("Unioned {0}", itr.Item3));
                    else return true;
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
                        ProvideUIUpdate(String.Format("Requested {0} be cropped from target, but {0} is empty!", itr.Item1));
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
                //ask user if they are ok with converting the relevant high resolution structures to default resolution
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

        public bool calculateNumIsos()
        {
            UpdateUILabel("Calculating Number of Isocenters:");
            ProvideUIUpdate("Calculating number of isocenters");
            //For these cases the maximum number of allowed isocenters is 3. One isocenter is reserved for the brain and either one or two isocenters are used for the spine (depending on length).
            //revised to get the number of unique plans list, for each unique plan, find the target with the greatest z-extent and determine the number of isocenters based off that target. 
            //plan Id, list of targets assigned to that plan
            List<Tuple<string, List<string>>> planIdTargets = new List<Tuple<string, List<string>>> { };
            string tmpPlanId = prescriptions.First().Item1;
            List<string> targs = new List<string> { };
            foreach(Tuple<string,string,int,DoseValue,double> itr in prescriptions)
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

            foreach(Tuple<string,List<string>> itr in planIdTargets)
            {
                //determine for each plan which target has the greatest z-extent
                double maxTargetLength = 0.0;
                string longestTargetInPlan = "";
                foreach (string s in itr.Item2)
                {
                    Structure targStruct = selectedSS.Structures.FirstOrDefault(x => x.Id == itr.Item2.First());
                    if (targStruct == null || targStruct.IsEmpty)
                    {
                        MessageBox.Show(String.Format("Error! No structure named: {0} found or contoured!", s));
                        return true;
                    }
                    Point3DCollection pts = targStruct.MeshGeometry.Positions;
                    double diff = pts.Max(p => p.Z) - pts.Min(p => p.Z);
                    if (diff > maxTargetLength) { longestTargetInPlan = s; maxTargetLength = diff; }
                }

                //If the target ID is PTV_CSI, calculate the number of isocenters based on PTV_spine and add one iso for the brain
                //planId, target list
                if (longestTargetInPlan == "PTV_CSI")
                {
                    //special rules for initial plan, which should have a target named PTV_CSI
                    //determine the number of isocenters required to treat PTV_Spine
                    Structure spineTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_spine");
                    if (spineTarget == null || spineTarget.IsEmpty)
                    {
                        MessageBox.Show(String.Format("Error! No structure named PTV_Spine was found or it was empty!"));
                        return true;
                    }
                    Point3DCollection pts = spineTarget.MeshGeometry.Positions;

                    //Grab the thyroid structure, if it does not exist, add a 50 mm buffer to the field extent (rough estimate of most inferior position of thyroid)
                    //Structure thyroidStruct = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("thyroid"));
                    //if (thyroidStruct == null || thyroidStruct.IsEmpty) numVMATIsos = (int)Math.Ceiling((pts.Max(p => p.Z) - pts.Min(p => p.Z)) / (400.0 + 50.0));
                    //else
                    //{
                    //    //If it exists, grab the minimum z position and subtract this from the ptv_spine extent (the brain fields extend down to the most inferior part of the thyroid)
                    //    Point3DCollection thyroidPts = thyroidStruct.MeshGeometry.Positions;
                    //    numVMATIsos = (int)Math.Ceiling((thyroidPts.Min(p => p.Z) - pts.Min(p => p.Z)) / 400.0);
                    //}
                    numVMATIsos = (int)Math.Ceiling((pts.Max(p => p.Z) - pts.Min(p => p.Z)) / (400.0 - 20.0));

                    //MessageBox.Show(String.Format("{0}, {1}, {2}", pts.Max(p => p.Z) - pts.Min(p => p.Z), pts.Max(p => p.Z) - pts.Min(p => p.Z) - thyroidPts.Min(p => p.Z), (pts.Max(p => p.Z) - pts.Min(p => p.Z) - thyroidPts.Min(p => p.Z)) / 400.0));
                    //one iso reserved for PTV_Brain
                    numVMATIsos += 1;
                }
                else numVMATIsos = (int)Math.Ceiling(maxTargetLength / (400.0 - 20.0));
                if (numVMATIsos > 3) numVMATIsos = 3;

                //set isocenter names based on numIsos and numVMATIsos (be sure to pass 'true' for the third argument to indicate that this is a CSI plan(s))
                //plan Id, list of isocenter names for this plan
                isoNames.Add(Tuple.Create(itr.Item1, new List<string>(new isoNameHelper().getIsoNames(numVMATIsos, numVMATIsos, true))));
            }
            ProvideUIUpdate(String.Format("Required Number of Isocenters: {0}", numVMATIsos));

            return false;
        }

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

        public bool createTargetStructures(List<Tuple<string, string>> missingTargets)
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
                    ProvideUIUpdate(String.Format("Can't add {0} to the structure set!", itr.Item2));
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
                    else { ProvideUIUpdate(String.Format("Error! Could not retrieve brain structure! Exiting!")); return true; }
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
                            else { ProvideUIUpdate(String.Format("Error! Could not retrieve CTV_Spine structure! Exiting!")); return true; }
                        }
                    }
                    else { ProvideUIUpdate(String.Format("Error! Could not retrieve brain structure! Exiting!")); return true; }
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

                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Cropping PTV_CSI from body with 5 mm inner margin"));
                //1/3/2022, crop PTV structure from body by 5mm
                cropStructureFromBody(combinedTarget, -0.5);
            }
            else ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("PTV_CSI already exists in the structure set! Skipping!"));
            ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Targets added and contoured!"));
            return false;
        }

        public bool cropStructureFromBody(Structure theStructure, double margin)
        {
            //margin is in cm
            Structure body = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "body");
            if (body != null)
            {
                if (margin >= -5.0 && margin <= 5.0) theStructure.SegmentVolume = theStructure.And(body.Margin(margin * 10));
                else { MessageBox.Show("Cropping margin from body MUST be within +/- 5.0 cm!"); return true; }
            }
            else { MessageBox.Show("Could not find body structure! Can't crop target from body!"); return true; }
            return false;
        }

        public bool cropTargetFromStructure(Structure target, Structure normal, double margin)
        {
            //margin is in cm
            if (target != null && normal != null)
            {
                if (margin >= -5.0 && margin <= 5.0) target.SegmentVolume = target.Sub(normal.Margin(margin * 10));
                else { MessageBox.Show("Cropping margin MUST be within +/- 5.0 cm!"); return true; }
            }
            else { MessageBox.Show("Error either target or normal structures are missing! Can't crop target from normal structure!"); return true; }
            return false;
        }

        public bool contourOverlap(Structure target, Structure normal, double margin)
        {
            //margin is in cm
            if (target != null && normal != null)
            {
                if (margin >= -5.0 && margin <= 5.0) normal.SegmentVolume = target.And(normal.Margin(margin * 10));
                else { MessageBox.Show("Added margin MUST be within +/- 5.0 cm!"); return true; }
            }
            else { MessageBox.Show("Error either target or normal structures are missing! Can't contour overlap between target and normal structure!"); return true; }
            return false;
        }

        public override bool createTSStructures()
        {
            //determine if any TS structures need to be added to the selected structure set
            //foreach (Tuple<string, string, double> itr in spareStructList)
            //{
            //    //optParameters.Add(Tuple.Create(itr.Item1, itr.Item2));
            //    //this is here to add
            //    if (itr.Item2 == "Crop target from structure") foreach (Tuple<string, string> itr1 in TS_structures.Where(x => x.Item2.ToLower().Contains(itr.Item1.ToLower()))) AddTSStructures(itr1);
            //}
            //get all TS structures that do not contain 'ctv' or 'ptv' in the title
            UpdateUILabel("Create TS Structures: ");
            ProvideUIUpdate(String.Format("Adding remaining tuning structures to stack!"));
            List<Tuple<string, string>> remainingTS = TS_structures.Where(x => !x.Item2.ToLower().Contains("ctv") && !x.Item2.ToLower().Contains("ptv")).ToList();
            int calcItems = remainingTS.Count;
            int counter = 0;
            foreach (Tuple<string, string> itr in TS_structures.Where(x => !x.Item2.ToLower().Contains("ctv") && !x.Item2.ToLower().Contains("ptv")))
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
                //MessageBox.Show(String.Format("create TS: {0}", itr));
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
                                //margin in mm. 
                                double margin = ((itr1.Item2 - ringDose) / itr1.Item2) * 30.0;
                                if (margin > 0.0)
                                {
                                    //method to create ring of 2.0 cm thickness
                                    //first create structure that is a copy of the target structure with an outer margin of ((Rx - ring dose / Rx) * 30 mm) + 20 mm.
                                    //1/5/2023, nataliya stated the 50% Rx ring should be 1.5 cm from the target and have a thickness of 2 cm. Redefined the margin formula to equal 15 mm whenever (Rx - ring dose) / Rx = 0.5
                                    addedStructure.SegmentVolume = targetStructure.Margin(margin + 20.0 > 50.0 ? 50.0 : margin + 20.0);
                                    //next, add a dummy structure that is a copy of the target structure with an outer margin of ((Rx - ring dose / Rx) * 30 mm)
                                    //if (selectedSS.Structures.Where(x => x.Id.ToLower() == "dummy").Any()) selectedSS.RemoveStructure(selectedSS.Structures.First(x => x.Id.ToLower() == "dummy"));
                                    //Structure dummy = selectedSS.AddStructure("CONTROL", "Dummy");
                                    //dummy.SegmentVolume = targetStructure.Margin(margin);
                                    //now, contour the ring as the original ring minus the dummy structure
                                    addedStructure.SegmentVolume = addedStructure.Sub(targetStructure.Margin(margin));
                                    //addedStructure.SegmentVolume = addedStructure.Sub(dummy.Margin(0.0));
                                    //keep only the parts of the ring that are inside the body!
                                    cropStructureFromBody(addedStructure, 0.0);
                                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Finished contouring ring: {0}", itr));
                                    //selectedSS.RemoveStructure(dummy);
                                }
                            }
                        }
                    }
                    else ProvideUIUpdate(String.Format("Could not parse ring dose for {0}! Skipping!", itr));
                }
                else if (itr.ToLower().Contains("armsavoid")) createArmsAvoid(addedStructure);
                else if (!(itr.ToLower().Contains("ptv")))
                {
                    calcItems = 4;
                    //all other sub structures
                    Structure originalStructure = null;
                    double margin = 0.0;
                    int pos1 = itr.IndexOf("-");
                    int pos2 = itr.IndexOf("cm");
                    if (pos1 != -1 && pos2 != -1)
                    {
                        string originalStructureId = itr.Substring(0, pos1);
                        ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Grabbing margin value!"));
                        double.TryParse(itr.Substring(pos1, pos2 - pos1), out margin);

                        ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Grabbing original structure {0}", originalStructureId));
                        if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains(originalStructureId.ToLower()) && x.Id.ToLower().Contains("_low")) == null) originalStructure = selectedSS.Structures.First(x => x.Id.ToLower().Contains(originalStructureId.ToLower()));
                        else originalStructure = selectedSS.Structures.First(x => x.Id.ToLower().Contains(originalStructureId.ToLower()) && x.Id.ToLower().Contains("_low"));

                        ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Creating {0} structure!", margin > 0 ? "outer" : "sub"));
                        //convert from cm to mm
                        addedStructure.SegmentVolume = originalStructure.Margin(margin * 10);
                        if (addedStructure.IsEmpty)
                        {
                            ProvideUIUpdate(String.Format("{0} was contoured, but is empty! Removing!", itr));
                            selectedSS.RemoveStructure(addedStructure);
                        }
                        ProvideUIUpdate(100, String.Format("Finished contouring {0}", itr));
                    }
                }
            }
            return false;
        }

        public bool createArmsAvoid(Structure armsAvoid)
        {
            ProvideUIUpdate(String.Format("Preparing to contour TS_arms..."));
            //generate arms avoid structures
            //need lungs, body, and ptv spine structures
            Structure lungs = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "lungs");
            Structure body = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "body");
            MeshGeometry3D mesh = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_spine").MeshGeometry;
            //get most inferior slice of ptv_spine (mesgeometry.bounds.z indicates the most inferior part of a structure)
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
            cropStructureFromBody(armsAvoid, 0.5);

            ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Cleaning up!"));
            selectedSS.RemoveStructure(dummyBoxR);
            selectedSS.RemoveStructure(dummyBoxL);
            ProvideUIUpdate(100, String.Format("Finished contouring arms avoid!"));

            return false;
        }

        private bool performTSStructureManipulation()
        {
            UpdateUILabel("Perform TS Manipulations: ");
            ProvideUIUpdate(String.Format("Retrieved list of TS manipulations"));
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
                if (tmpSpareLst.Any())
                {
                    foreach (Tuple<string, string, double> itr1 in spareStructList)
                    {
                        //MessageBox.Show(String.Format("manipulate TS: {0}", itr1.Item1));
                        Structure theStructure = selectedSS.Structures.FirstOrDefault(x => x.Id == itr1.Item1);
                        if (itr1.Item2.Contains("Crop"))
                        {
                            if(itr1.Item2.Contains("Body"))
                            {
                                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Cropping {0} from Body with margin {1} cm", itr1.Item1, itr1.Item3));
                                //crop from body
                                cropStructureFromBody(theStructure, itr1.Item3);
                            }
                            else
                            {
                                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Cropping {0} from target {1} with margin {2} cm", itr1.Item1, newName, itr1.Item3));
                                //crop target from structure
                                cropTargetFromStructure(addedTSTarget, theStructure, itr1.Item3);
                            }
                        }
                        else if(itr1.Item2.Contains("Contour"))
                        {
                            ProvideUIUpdate(String.Format("Contouring overlap between {0} and {1}", itr1.Item1, newName));
                            newName = String.Format("ts_{0}_overlap", itr1.Item1);
                            if (newName.Length > 16) newName = newName.Substring(0, 16);
                            Structure addedTSNormal = AddTSStructures(new Tuple<string, string>("CONTROL", newName));
                            Structure originalNormal = selectedSS.Structures.FirstOrDefault(x => x.Id == itr1.Item1);
                            addedTSNormal.SegmentVolume = originalNormal.Margin(0.0);
                            contourOverlap(addedTSTarget, addedTSNormal, itr1.Item3);
                            //Structure tmp = selectedSS.AddStructure("CONTROL", "dummy");
                            //tmp.SegmentVolume = addedTSNormal.Margin(0.0);
                            //tmp.Sub(originalNormal.Margin(0.0));
                            //if (tmp.IsEmpty) selectedSS.RemoveStructure(addedTSNormal);
                            //selectedSS.RemoveStructure(tmp);
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
    }
}