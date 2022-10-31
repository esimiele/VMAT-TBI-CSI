using System;
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
                if (itr.Item2 == "Crop from target")
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
            //revise to get the number of unique plans list, for each unique plan, find the target with the greatest z-extent and determine the number of isocenters based off that. If the target
            //ID is PTV_CSI, calculate the number of isocenters based on PTV_spine and add one iso for the brain
            //planId, target list
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

                //For these cases the maximum number of allowed isocenters is 3. One isocenter is reserved for the brain and either one or two isocenters are used for the spine (depending on length).
                if (longestTargetInPlan == "PTV_CSI")
                {
                    //special rules for initial plan, which should have a target named PTV_CSI
                    Structure spineTarget = selectedSS.Structures.FirstOrDefault(x => x.Id == "PTV_Spine");
                    if (spineTarget == null || spineTarget.IsEmpty)
                    {
                        MessageBox.Show(String.Format("Error! No structure named: PTV_Spine found or contoured!"));
                        return true;
                    }
                    Point3DCollection pts = spineTarget.MeshGeometry.Positions;

                    //Grab the thyroid structure. If it exists, grab the minimum z position and subtract this from the ptv_spine extent (the brain fields extend down to the most inferior part of the thyroid)
                    //If it does not exist, add a 50 mm buffer to the field extent (rough estimate of most inferior position of thyroid)
                    Structure thyroidStruct = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("thyroid"));
                    if (thyroidStruct == null ||thyroidStruct.IsEmpty) numVMATIsos = (int)Math.Ceiling((pts.Max(p => p.Z) - pts.Min(p => p.Z)) / (400.0 + 50.0));
                    else
                    {
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

            //foreach (Tuple<string,string,int,DoseValue,double> itr in prescriptions)
            //{
            //    //logic used to account for the initial CSI plan where we want the first isocenter centered in the brain and the remaining isos to be in the spine
            //    //string targetID = itr.Item2;
            //    //numVMATIsos = 0;
            //    //if (targetID == "PTV_CSI")
            //    //{
            //    //    targetID = "PTV_Spine";
            //    //    numVMATIsos = 1;
            //    //}
            //    //get the points collection for the target for each plan (used for calculating number of isocenters)
            //    Point3DCollection pts = selectedSS.Structures.FirstOrDefault(x => x.Id == itr.Item2).MeshGeometry.Positions;

            //    //For these cases the maximum number of allowed isocenters is 3. One isocenter is reserved for the brain and either one or two isocenters are used for the spine (depending on length).
            //    numVMATIsos = (int)Math.Ceiling((pts.Max(p => p.Z) - pts.Min(p => p.Z)) / (400.0 - 20.0));
            //    if (numVMATIsos > 3) numVMATIsos = 3;

            //    //set isocenter names based on numIsos and numVMATIsos (reuse same naming convention as VMAT TBI for simplicity)
            //    //plan Id, list of isocenter names for this plan
            //    isoNames.Add(Tuple.Create(itr.Item1, new List<string>(new isoNameHelper().getIsoNames(numVMATIsos, numVMATIsos))));
            //}
            
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

            //used to create the ptv_csi and ts_ptv_csi structures
            Structure combinedTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_csi");
            Structure brainTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_brain"));
            Structure spineTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_spine"));
            combinedTarget.SegmentVolume = brainTarget.Margin(0.0);
            combinedTarget.SegmentVolume = combinedTarget.Or(spineTarget.Margin(0.0));
            return false;
        }

        public override bool createTSStructures()
        {
            //determine if any TS structures need to be added to the selected structure set
            foreach (Tuple<string, string, double> itr in spareStructList)
            {
                optParameters.Add(Tuple.Create(itr.Item1, itr.Item2));
                if (itr.Item2 == "Crop from target") foreach (Tuple<string, string> itr1 in TS_structures.Where(x => x.Item2.ToLower().Contains(itr.Item1.ToLower()))) AddTSStructures(itr1);
            }
            //add ring structures to the stack
            foreach (Tuple<string, string> itr in TS_structures.Where(x => x.Item2.ToLower().Contains("ts_ring"))) AddTSStructures(itr);

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
                    Structure tmp1 = null;
                    double margin = 0.0;
                    if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains(itr.ToLower()) && x.Id.ToLower().Contains("_low")) == null) tmp1 = selectedSS.Structures.First(x => x.Id.ToLower().Contains(itr.ToLower()));
                    else tmp1 = selectedSS.Structures.First(x => x.Id.ToLower().Contains(itr.ToLower()) && x.Id.ToLower().Contains("_low"));
                    //all structures in TS_structures and scleroStructures are inner margins, which is why the below code works.
                    int pos1 = itr.IndexOf("-");
                    int pos2 = itr.IndexOf("cm");
                    if (pos1 != -1 && pos2 != -1) double.TryParse(itr.Substring(pos1, pos2 - pos1), out margin);

                    //convert from cm to mm
                    tmp.SegmentVolume = tmp1.Margin(margin * 10);
                }
                else if (itr.ToLower() == "ptv_csi")
                {
                    //subtract all the structures the user wants to spare from PTV_CSI
                    foreach (Tuple<string, string, double> spare in spareStructList)
                    {
                        if (spare.Item2 == "Crop from target")
                        {
                            
                            Structure tmp1 = selectedSS.Structures.First(x => x.Id.ToLower() == spare.Item1.ToLower());
                            tmp.SegmentVolume = tmp.Sub(tmp1.Margin((spare.Item3) * 10));
                        }
                    }
                }
                else if (itr.ToLower() == "ts_ptv_csi")
                {
                    //copy the ptv_csi contour onto the TS_ptv_csi contour
                    Structure tmp1 = selectedSS.Structures.First(x => x.Id.ToLower() == "ptv_csi");
                    tmp.SegmentVolume = tmp1.Margin(0.0);

                    //matchplane exists and needs to be cut from TS_PTV_Body. Also remove all TS_PTV_Body segements inferior to match plane
                    //if (selectedSS.Structures.Where(x => x.Id.ToLower() == "matchline").Any())
                    //{
                    //    //find the image plane where the matchline is location. Record this value and break the loop. Also find the first slice where the ptv_body contour starts and record this value
                    //    Structure matchline = selectedSS.Structures.First(x => x.Id.ToLower() == "matchline");
                    //    bool lowLimNotFound = true;
                    //    int lowLim = -1;
                    //    if (!matchline.IsEmpty)
                    //    {
                    //        int matchplaneLocation = 0;
                    //        for (int i = 0; i != selectedSS.Image.ZSize - 1; i++)
                    //        {
                    //            if (matchline.GetContoursOnImagePlane(i).Any())
                    //            {
                    //                matchplaneLocation = i;
                    //                break;
                    //            }
                    //            if (lowLimNotFound && tmp1.GetContoursOnImagePlane(i).Any())
                    //            {
                    //                lowLim = i;
                    //                lowLimNotFound = false;
                    //            }
                    //        }

                    //        if (selectedSS.Structures.Where(x => x.Id.ToLower() == "dummybox").Any()) selectedSS.RemoveStructure(selectedSS.Structures.First(x => x.Id.ToLower() == "dummybox"));
                    //        Structure dummyBox = selectedSS.AddStructure("CONTROL", "DummyBox");

                    //        //get min/max positions of ptv_body contour to contour the dummy box for creating TS_PTV_Legs
                    //        Point3DCollection ptv_bodyPts = tmp1.MeshGeometry.Positions;
                    //        double xMax = ptv_bodyPts.Max(p => p.X) + 50.0;
                    //        double xMin = ptv_bodyPts.Min(p => p.X) - 50.0;
                    //        double yMax = ptv_bodyPts.Max(p => p.Y) + 50.0;
                    //        double yMin = ptv_bodyPts.Min(p => p.Y) - 50.0;

                    //        //box with contour points located at (x,y), (x,0), (x,-y), (0,-y), (-x,-y), (-x,0), (-x, y), (0,y)
                    //        VVector[] pts = new[] {
                    //                    new VVector(xMax, yMax, 0),
                    //                    new VVector(xMax, 0, 0),
                    //                    new VVector(xMax, yMin, 0),
                    //                    new VVector(0, yMin, 0),
                    //                    new VVector(xMin, yMin, 0),
                    //                    new VVector(xMin, 0, 0),
                    //                    new VVector(xMin, yMax, 0),
                    //                    new VVector(0, yMax, 0)};

                    //        //give 5cm margin on TS_PTV_LEGS (one slice of the CT should be 5mm) in case user wants to include flash up to 5 cm
                    //        for (int i = matchplaneLocation - 1; i > lowLim - 10; i--) dummyBox.AddContourOnImagePlane(pts, i);

                    //        //do the structure manipulation
                    //        if (selectedSS.Structures.Where(x => x.Id.ToLower() == "ts_ptv_legs").Any()) selectedSS.RemoveStructure(selectedSS.Structures.First(x => x.Id.ToLower() == "ts_ptv_legs"));
                    //        Structure TS_legs = selectedSS.AddStructure("CONTROL", "TS_PTV_Legs");
                    //        TS_legs.SegmentVolume = dummyBox.And(tmp.Margin(0));
                    //        //subtract both dummybox and matchline from TS_PTV_VMAT
                    //        tmp.SegmentVolume = tmp.Sub(dummyBox.Margin(0.0));
                    //        tmp.SegmentVolume = tmp.Sub(matchline.Margin(0.0));
                    //        //remove the dummybox structure if flash is NOT being used as its no longer needed
                    //        if (!useFlash) selectedSS.RemoveStructure(dummyBox);
                    //    }
                    //}
                }
            }
            return false;
        }
    }
}