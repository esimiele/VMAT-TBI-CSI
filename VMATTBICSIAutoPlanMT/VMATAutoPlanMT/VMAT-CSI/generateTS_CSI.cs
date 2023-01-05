﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Media.Media3D;

namespace VMATAutoPlanMT
{
    public class generateTS_CSI : generateTSbase
    { 
        //structure, sparing type, added margin
        public List<Tuple<string, string, double>> spareStructList;
        //DICOM types
        //Possible values are "AVOIDANCE", "CAVITY", "CONTRAST_AGENT", "CTV", "EXTERNAL", "GTV", "IRRAD_VOLUME", 
        //"ORGAN", "PTV", "TREATED_VOLUME", "SUPPORT", "FIXATION", "CONTROL", and "DOSE_REGION". 
        List<Tuple<string, string>> TS_structures;
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

        public override bool generateStructures()
        {
            isoNames.Clear();
            if (preliminaryChecks()) return true;
            if (RemoveOldTSStructures(TS_structures)) return true;
            if (createTargetStructures()) return true;
            if (createTSStructures()) return true;
            if (performTSStructureManipulation()) return true;
            if (calculateNumIsos()) return true;
            MessageBox.Show("Structures generated successfully!\nPlease proceed to the beam placement tab!");
            return false;
        }

        public override bool preliminaryChecks()
        {
            //check if user origin was set
            if (isUOriginInside()) return true;

            //verify brain and spine structures are present
            if(selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "brain") == null || selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "spinalcord" || x.Id.ToLower() == "spinal_cord") == null)
            {
                MessageBox.Show("Missing brain and/or spine structures! Please add and try again!");
                return true;
            }

            //check if selected structures are empty or of high-resolution (i.e., no operations can be performed on high-resolution structures)
            string output = "The following structures are high-resolution:" + System.Environment.NewLine;
            List<Structure> highResStructList = new List<Structure> { };
            List<Tuple<string, string, double>> highResSpareList = new List<Tuple<string, string, double>> { };
            foreach (Tuple<string, string, double> itr in spareStructList)
            {
                if (itr.Item2 == "Crop target from structure")
                {
                    if (selectedSS.Structures.First(x => x.Id == itr.Item1).IsEmpty)
                    {
                        MessageBox.Show(String.Format("Error! \nThe selected structure that will be subtracted from PTV_Body and TS_PTV_VMAT is empty! \nContour the structure and try again."));
                        return true;
                    }
                    else if (selectedSS.Structures.First(x => x.Id == itr.Item1).IsHighResolution)
                    {
                        highResStructList.Add(selectedSS.Structures.First(x => x.Id == itr.Item1));
                        highResSpareList.Add(itr);
                        output += String.Format("{0}", itr.Item1) + System.Environment.NewLine;
                    }
                }
            }
            //if there are high resolution structures, they will need to be converted to default resolution.
            if (highResStructList.Count() > 0)
            {
                //ask user if they are ok with converting the relevant high resolution structures to default resolution
                output += "They must be converted to default resolution before proceeding!";
                confirmUI CUI = new confirmUI();
                CUI.message.Text = output + Environment.NewLine + Environment.NewLine + "Continue?!";
                CUI.message.Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
                CUI.ShowDialog();
                if (!CUI.confirm) return true;

                List<Tuple<string, string, double>> newData = convertHighToLowRes(highResStructList, highResSpareList, spareStructList);
                if (!newData.Any()) return true;
                spareStructList = new List<Tuple<string, string, double>>(newData);
                //inform the main UI class that the UI needs to be updated
                updateSparingList = true;
            }
            return false;
        }

        public bool calculateNumIsos()
        {
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
                    Structure spineTarget = selectedSS.Structures.FirstOrDefault(x => x.Id == "PTV_Spine");
                    if (spineTarget == null || spineTarget.IsEmpty)
                    {
                        MessageBox.Show(String.Format("Error! No structure named: PTV_Spine found or contoured!"));
                        return true;
                    }
                    Point3DCollection pts = spineTarget.MeshGeometry.Positions;

                    //Grab the thyroid structure, if it does not exist, add a 50 mm buffer to the field extent (rough estimate of most inferior position of thyroid)
                    Structure thyroidStruct = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("thyroid"));
                    if (thyroidStruct == null || thyroidStruct.IsEmpty) numVMATIsos = (int)Math.Ceiling((pts.Max(p => p.Z) - pts.Min(p => p.Z)) / (400.0 + 50.0));
                    else
                    {
                        //If it exists, grab the minimum z position and subtract this from the ptv_spine extent (the brain fields extend down to the most inferior part of the thyroid)
                        Point3DCollection thyroidPts = thyroidStruct.MeshGeometry.Positions;
                        numVMATIsos = (int)Math.Ceiling((thyroidPts.Min(p => p.Z) - pts.Min(p => p.Z)) / 400.0);
                    }
                    //MessageBox.Show(String.Format("{0}, {1}, {2}", pts.Max(p => p.Z) - pts.Min(p => p.Z), pts.Max(p => p.Z) - pts.Min(p => p.Z) - thyroidPts.Min(p => p.Z), (pts.Max(p => p.Z) - pts.Min(p => p.Z) - thyroidPts.Min(p => p.Z)) / 400.0));
                    //one iso reserved for PTV_Brain
                    numVMATIsos += 1;
                }
                else numVMATIsos = (int)Math.Ceiling(maxTargetLength / (400.0 - 20.0));
                if (numVMATIsos > 3) numVMATIsos = 3;

                //set isocenter names based on numIsos and numVMATIsos (reuse same naming convention as VMAT TBI for simplicity)
                //plan Id, list of isocenter names for this plan
                isoNames.Add(Tuple.Create(itr.Item1, new List<string>(new isoNameHelper().getIsoNames(numVMATIsos, numVMATIsos))));
            }
            
            return false;
        }

        public bool createTargetStructures()
        {
            //create the CTV and PTV structures
            //if these structures were present, they should have been removed (regardless if they were contoured or not). 
            List<Structure> addedTargets = new List<Structure> { };
            foreach (Tuple<string, string> itr in TS_structures.Where(x => x.Item2.ToLower().Contains("ctv") || x.Item2.ToLower().Contains("ptv")).OrderBy(x => x.Item2))
            {
                if (selectedSS.CanAddStructure(itr.Item1, itr.Item2))
                {
                    addedStructures.Add(itr.Item2);
                    addedTargets.Add(selectedSS.AddStructure(itr.Item1, itr.Item2));
                    //optParameters.Add(new Tuple<string,string>(itr.Item1, itr.Item2));
                }
                else
                {
                    MessageBox.Show(String.Format("Can't add {0} to the structure set!", itr.Item2));
                    return true;
                }
            }

            Structure tmp = null;
            foreach (Structure itr in addedTargets)
            {
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
                    else { MessageBox.Show("Error! Could not retrieve brain structure! Exiting!"); return true; }
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
                            else { MessageBox.Show("Error! Could not retrieve CTV_Spine structure! Exiting!"); return true; }
                        }
                    }
                    else { MessageBox.Show("Error! Could not retrieve brain structure! Exiting!"); return true; }
                }
            }

            //used to create the ptv_csi structures
            Structure combinedTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_csi");
            Structure brainTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_brain"));
            Structure spineTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_spine"));
            combinedTarget.SegmentVolume = brainTarget.Margin(0.0);
            combinedTarget.SegmentVolume = combinedTarget.Or(spineTarget.Margin(0.0));

            //1/3/2022, crop PTV structure from body by 5mm
            cropStructureFromBody(combinedTarget, -5.0);
            return false;
        }

        private bool cropStructureFromBody(Structure theStructure, double margin)
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

        private bool cropTargetFromStructure(Structure target, Structure normal, double margin)
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

        private bool contourOverlap(Structure target, Structure normal, double margin)
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
            foreach (Tuple<string, string> itr in TS_structures.Where(x => !x.Item2.ToLower().Contains("ctv") && !x.Item2.ToLower().Contains("ptv")))
            {
                //if those structures have NOT been added to the added structure list, go ahead and add them to stack
                if (addedStructures.FirstOrDefault(x => x.ToLower() == itr.Item2) == null) AddTSStructures(itr);
            }

            //now contour the various structures
            foreach (string itr in addedStructures)
            {
                Structure tmp = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == itr.ToLower());
                if (itr.ToLower().Contains("ts_ring"))
                {
                    if (double.TryParse(itr.Substring(7, itr.Length - 7), out double ringDose))
                    {
                        foreach (Tuple<string, double, string> itr1 in targets)
                        {
                            Structure tmp1 = selectedSS.Structures.FirstOrDefault(x => x.Id == itr1.Item1);
                            if (tmp1 != null)
                            {
                                //margin in mm. 
                                double margin = (itr1.Item2 - ringDose) / 150.0;
                                if (margin > 0.0)
                                {
                                    //method to create ring of 1.0 cm thickness
                                    //first create structure that is a copy of the target structure with an outer margin of (Rx - ring dose / 150 cGy/mm) + 10 mm.
                                    //This uses a loose rule of thumb of 150.0 cGy/mm compared to the rule of thumb Nataliya provided of 200.0 cGy/mm for standard VMAT plans
                                    tmp.SegmentVolume = tmp1.Margin(margin + 10.0);
                                    //next, add a dummy structure that is a copy of the target structure with an outer margin of (Rx - ring dose / 2 Gy/mm)
                                    if (selectedSS.Structures.Where(x => x.Id.ToLower() == "dummy").Any()) selectedSS.RemoveStructure(selectedSS.Structures.First(x => x.Id.ToLower() == "dummy"));
                                    Structure dummy = selectedSS.AddStructure("CONTROL", "Dummy");
                                    dummy.SegmentVolume = tmp1.Margin(margin);
                                    //now, contour the ring as the original ring minus the dummy structure
                                    tmp.SegmentVolume = tmp.Sub(dummy.Margin(0.0));
                                    tmp.SegmentVolume = tmp.And(selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "body"));
                                    //remove the dummy structure
                                    selectedSS.RemoveStructure(dummy);
                                }
                            }
                        }
                    }
                    else MessageBox.Show(String.Format("Could not parse ring dose for {0}! Skipping!", itr));
                }
                else if (!(itr.ToLower().Contains("ptv")))
                {
                    //sub structures
                    Structure tmp1 = null;
                    double margin = 0.0;
                    int pos1 = itr.IndexOf("-");
                    int pos2 = itr.IndexOf("cm");
                    if (pos1 != -1 && pos2 != -1)
                    {
                        string originalStructure = itr.Substring(0, pos1);
                        double.TryParse(itr.Substring(pos1, pos2 - pos1), out margin);

                        if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains(originalStructure.ToLower()) && x.Id.ToLower().Contains("_low")) == null) tmp1 = selectedSS.Structures.First(x => x.Id.ToLower().Contains(originalStructure.ToLower()));
                        else tmp1 = selectedSS.Structures.First(x => x.Id.ToLower().Contains(originalStructure.ToLower()) && x.Id.ToLower().Contains("_low"));

                        //convert from cm to mm
                        tmp.SegmentVolume = tmp1.Margin(margin * 10);
                    }
                }
                
            }
            return false;
        }

        private bool performTSStructureManipulation()
        {
            //there are items in the sparing list requiring structure manipulation
            List<Tuple<string, string, double>> tmpSpareLst = spareStructList.Where(x => x.Item2.Contains("Crop target from structure") || x.Item2.Contains("Contour")).ToList();
            if(tmpSpareLst.Any())
            {
                foreach(Tuple<string, double, string> itr in targets)
                {
                    //create a new TS target for optimization and copy the original target structure onto the new TS structure
                    Structure addedTSTarget = AddTSStructures(new Tuple<string,string>("CONTROL", String.Format("ts_{0}", itr.Item1).Substring(0,15)));
                    addedTSTarget.SegmentVolume = selectedSS.Structures.FirstOrDefault(x => x.Id == itr.Item1).Margin(0.0);
                    foreach (Tuple<string, string, double> itr1 in spareStructList)
                    {
                        Structure theStructure = selectedSS.Structures.FirstOrDefault(x => x.Id == itr1.Item1);
                        if (itr1.Item2.Contains("Crop"))
                        {
                            if(itr1.Item2.Contains("Body"))
                            {
                                //crop from body
                                cropStructureFromBody(theStructure, itr1.Item3);
                            }
                            else
                            {
                                //crop target from structure
                                cropTargetFromStructure(addedTSTarget, theStructure, itr1.Item3);
                            }
                        }
                        else if(itr1.Item2.Contains("Contour"))
                        {
                            Structure addedTSNormal = AddTSStructures(new Tuple<string, string>("CONTROL", String.Format("ts_{0}_overlap", itr.Item1).Substring(0,15)));
                            Structure originalNormal = selectedSS.Structures.FirstOrDefault(x => x.Id == itr1.Item1);
                            addedTSNormal.SegmentVolume = originalNormal.Margin(0.0);
                            contourOverlap(addedTSTarget, addedTSNormal, itr1.Item3);
                            Structure tmp = selectedSS.AddStructure("CONTROL", "dummy");
                            tmp.SegmentVolume = addedTSNormal.Margin(0.0);
                            tmp.Sub(originalNormal.Margin(0.0));
                            if (tmp.IsEmpty) selectedSS.RemoveStructure(addedTSNormal);
                            selectedSS.RemoveStructure(tmp);
                        }
                        //if (itr1.Item1.ToLower() == "ptv_csi")
                        //{
                        //    //subtract all the structures the user wants to spare from PTV_CSI
                        //    foreach (Tuple<string, string, double> spare in spareStructList)
                        //    {
                        //        if (spare.Item2 == "Crop target from structure")
                        //        {
                        //            Structure tmp1 = selectedSS.Structures.First(x => x.Id.ToLower() == spare.Item1.ToLower());
                        //            tmp.SegmentVolume = tmp.Sub(tmp1.Margin((spare.Item3) * 10));
                        //        }
                        //    }
                        //}
                        ////optParameters.Add(Tuple.Create(itr.Item1, itr.Item2));
                        ////this is here to add
                        //if (itr1.Item2 == "Crop target from structure") foreach (Tuple<string, string> itr1 in TS_structures.Where(x => x.Item2.ToLower().Contains(itr.Item1.ToLower()))) AddTSStructures(itr1);
                    }
                }
            }
            Structure tsTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ts_ptv_csi");
            
            
            return false;
        }
    }
}