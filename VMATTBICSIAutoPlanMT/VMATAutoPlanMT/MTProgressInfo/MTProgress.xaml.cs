using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VMATAutoPlanMT.VMAT_CSI;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Threading;
using VMATAutoPlanMT.helpers;
using System.Windows.Media.Media3D;
using VMATAutoPlanMT.baseClasses;
using System.Reflection;


namespace VMATAutoPlanMT.MTProgressInfo
{
    /// <summary>
    /// Interaction logic for generateTSProgress.xaml
    /// </summary>
    /// 
    public partial class MTProgress : Window
    {
        ESAPIworker slave;
        MTbase callerClass;
        //generateTSbase ts;
        public List<string> addedStructures = new List<string> { };

        public MTProgress()
        {
            InitializeComponent();
        }

        public void setCallerClass<T>(ESAPIworker e, T caller)
        {
            callerClass = caller as MTbase;
            //var genericBase = caller.GetType().GetMethod("PerformStructureGeneration");
            //if (caller.GetType() == typeof(generateTS_CSI))
            //{
            //    generate = caller as generateTS_CSI;
            //    MessageBox.Show("success!");
            //}
            slave = e;
            //generate = gen;
            try
            {
                doStuff();
            }
            catch (Exception except) { MessageBox.Show(except.Message); }
        }

        public void doStuff()
        {
            slave.DoWork(d =>
            {
                //get instance of current dispatcher
                Dispatcher dispatch = Dispatcher;
                callerClass.SetDispatcherAndUIInstance(dispatch, this);
                callerClass.Run();
                //Dispatcher.BeginInvoke((Action)(() => { provideUpdate("Running"); }));
                //generate.PerformStructureGeneration(dispatch, this);
                //generate.PerformStructureGeneration();
                //Dispatcher.BeginInvoke((Action)(() => { UpdateLabel("Preliminary Checks: "); }));
                //calcItems = 1;
                //generate.preliminaryChecksTest(dispatch, this, d.selectedSS, d.spareStructList);
                //Dispatcher.BeginInvoke((Action)(() => { provideUpdate(100, "Preliminary checks complete!"); }));
                //Dispatcher.BeginInvoke((Action)(() => { UpdateLabel("Union Structures: "); }));
                //Dispatcher.BeginInvoke((Action)(() => { provideUpdate(0, "Checking for L and R structures to union!"); }));
                //UnionLRStructures(d.selectedSS);
                //Dispatcher.BeginInvoke((Action)(() => { provideUpdate(100, "Structures unioned!"); }));
                //calcItems = 1;
                //if (d.spareStructList.Any())
                //{
                //    Dispatcher.BeginInvoke((Action)(() => { UpdateLabel("Check high resolution: "); }));
                //    Dispatcher.BeginInvoke((Action)(() => { provideUpdate(0, "Checking for high resolution structures in the structure set"); }));
                //    d.spareStructList = CheckHighResolution(d.selectedSS, d.spareStructList);
                //    if(!d.spareStructList.Any())
                //    {
                //        Dispatcher.BeginInvoke((Action)(() => { provideUpdate("Error! No sparing structures in the list! Exiting! "); }));
                //        return;
                //    }
                //    Dispatcher.BeginInvoke((Action)(() => { provideUpdate(100,"Finishing converting high resolution structures to default resolution"); }));
                //}
                //Dispatcher.BeginInvoke((Action)(() => { UpdateLabel("Remove Prior Tuning Structures: "); }));
                //Dispatcher.BeginInvoke((Action)(() => { provideUpdate(0, "Removing prior/existing tuning structures!"); }));
                //generate.RemoveOldTSStructures(d.TS_structures, d.selectedSS);
                //Dispatcher.BeginInvoke((Action)(() => { provideUpdate(100, "Removed prior/existing tuning structures!"); }));
                //Dispatcher.BeginInvoke((Action)(() => { UpdateLabel("Create Target Structures: "); }));
                //Dispatcher.BeginInvoke((Action)(() => { provideUpdate(0, "Creating target structures!"); }));
                //createTargetStructures(d.selectedSS, d.TS_structures);
                //Dispatcher.BeginInvoke((Action)(() => { provideUpdate(0, "Finished contouring target structures!"); }));
                //Dispatcher.BeginInvoke((Action)(() => { UpdateLabel("Create Tuning Structures: "); }));
                //Dispatcher.BeginInvoke((Action)(() => { provideUpdate(0, "Creating tuning structures!"); }));
                //createTSStructures(d.selectedSS, d.TS_structures, d.targets);
                //Dispatcher.BeginInvoke((Action)(() => { UpdateLabel("Manipulate Tuning Structures: "); }));
                //Dispatcher.BeginInvoke((Action)(() => { provideUpdate(0, "Start tuning structure manipulation!"); }));
                //performTSStructureManipulation(d.selectedSS, d.targets, d.spareStructList);
                //Dispatcher.BeginInvoke((Action)(() => { provideUpdate(0, "Finished tuning structure manipulation!"); }));
                Dispatcher.BeginInvoke((Action)(() => { provideUpdate(100, "Finished contouring structures!"); }));
            });
        }

        //public bool UnionLRStructures(StructureSet selectedSS)
        //{
        //    int numUnioned = 0;
        //    StructureTuningUIHelper helper = new StructureTuningUIHelper();
        //    List<Tuple<Structure, Structure, string>> structuresToUnion = helper.checkStructuresToUnion(selectedSS);
        //    if (structuresToUnion.Any())
        //    {
        //        calcItems = structuresToUnion.Count;
        //        foreach (Tuple<Structure, Structure, string> itr in structuresToUnion)
        //        {
        //            if (!helper.unionLRStructures(itr, selectedSS)) Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++numUnioned / calcItems), String.Format("Unioned {0}", itr.Item3)); }));
        //            else return true;
        //        }
        //        Dispatcher.BeginInvoke((Action)(() => { provideUpdate(100, "Structures unioned successfully!"); }));
        //    }
        //    else Dispatcher.BeginInvoke((Action)(() => { provideUpdate(100, "No structures to union!"); }));
        //    return false;
        //}

        //private List<Tuple<string, string, double>> CheckHighResolution(StructureSet ss, List<Tuple<string, string, double>> spareStructList)
        //{
        //    //check if selected structures are empty or of high-resolution (i.e., no operations can be performed on high-resolution structures)
        //    List<Structure> highResStructList = new List<Structure> { };
        //    List<Tuple<string, string, double>> highResSpareList = new List<Tuple<string, string, double>> { };
        //    foreach (Tuple<string, string, double> itr in spareStructList)
        //    {
        //        if (itr.Item2 == "Crop target from structure")
        //        {
        //            if (ss.Structures.First(x => x.Id == itr.Item1).IsEmpty)
        //            {
        //                return new List<Tuple<string, string, double>> { };
        //            }
        //            else if (ss.Structures.First(x => x.Id == itr.Item1).IsHighResolution)
        //            {
        //                highResStructList.Add(ss.Structures.First(x => x.Id == itr.Item1));
        //                highResSpareList.Add(itr);
        //            }
        //        }
        //    }
        //    //if there are high resolution structures, they will need to be converted to default resolution.
        //    if (highResStructList.Count() > 0)
        //    {
        //        Dispatcher.BeginInvoke((Action)(() => { provideUpdate("High-resolution structures:"); }));
        //        foreach(Structure itr in highResStructList)
        //        {
        //            string id = itr.Id;
        //            Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format("{0}", id)); }));
        //        }
        //        Dispatcher.BeginInvoke((Action)(() => { provideUpdate("Now converting to low-resolution!"); }));
        //        //ask user if they are ok with converting the relevant high resolution structures to default resolution
        //        List<Tuple<string, string, double>> newData = generate.convertHighToLowRes(highResStructList, highResSpareList, spareStructList);
        //        if (!newData.Any()) return new List<Tuple<string, string, double>> { };
        //        spareStructList = new List<Tuple<string, string, double>>(newData);
        //        //inform the main UI class that the UI needs to be updated
        //        //updateSparingList = true;
        //    }
        //    return spareStructList;
        //}

        //public bool createTargetStructures(StructureSet selectedSS, List<Tuple<string,string>> TS_structures)
        //{
        //    //create the CTV and PTV structures
        //    //if these structures were present, they should have been removed (regardless if they were contoured or not). 
        //    List<Structure> addedTargets = new List<Structure> { };
        //    List<Tuple<string, string>> prospectiveTargets = TS_structures.Where(x => x.Item2.ToLower().Contains("ctv") || x.Item2.ToLower().Contains("ptv")).OrderBy(x => x.Item2).ToList();
        //    calcItems = prospectiveTargets.Count;
        //    int counter = 0;
        //    foreach (Tuple<string, string> itr in prospectiveTargets)
        //    {
        //        if (selectedSS.CanAddStructure(itr.Item1, itr.Item2))
        //        {
        //            addedStructures.Add(itr.Item2);
        //            addedTargets.Add(selectedSS.AddStructure(itr.Item1, itr.Item2));
        //            Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Added target: {0}", itr.Item2)); }));
        //            //optParameters.Add(new Tuple<string,string>(itr.Item1, itr.Item2));
        //        }
        //        else
        //        {
        //            MessageBox.Show(String.Format("Can't add {0} to the structure set!", itr.Item2));
        //            return true;
        //        }
        //    }

        //    Structure tmp = null;
        //    calcItems = addedTargets.Count + 3;
        //    counter = 0;
        //    foreach (Structure itr in addedTargets)
        //    {
        //        string targetId = itr.Id;
        //        Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Contoured target: {0}", targetId)); }));
        //        if (itr.Id.ToLower().Contains("brain"))
        //        {
        //            tmp = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "brain");
        //            if (tmp != null && !tmp.IsEmpty)
        //            {
        //                if (itr.Id.ToLower().Contains("ctv"))
        //                {
        //                    //CTV structure. Brain CTV IS the brain structure
        //                    itr.SegmentVolume = tmp.Margin(0.0);
        //                }
        //                else
        //                {
        //                    //PTV structure
        //                    //5 mm uniform margin to generate PTV
        //                    itr.SegmentVolume = tmp.Margin(5.0);
        //                }
        //            }
        //            else { MessageBox.Show("Error! Could not retrieve brain structure! Exiting!"); return true; }
        //        }
        //        else if (itr.Id.ToLower().Contains("spine"))
        //        {
        //            tmp = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "spinalcord" || x.Id.ToLower() == "spinal_cord");
        //            if (tmp != null && !tmp.IsEmpty)
        //            {
        //                if (itr.Id.ToLower().Contains("ctv"))
        //                {
        //                    //CTV structure. Brain CTV IS the brain structure
        //                    //AxisAlignedMargins(inner or outer margin, margin from negative x, margin for negative y, margin for negative z, margin for positive x, margin for positive y, margin for positive z)
        //                    //according to Nataliya: CTV_spine = spinal_cord+0.5cm ANT, +1.5cm Inf, and +1.0 cm in all other directions
        //                    itr.SegmentVolume = tmp.AsymmetricMargin(new AxisAlignedMargins(StructureMarginGeometry.Outer,
        //                                                                                    10.0,
        //                                                                                    5.0,
        //                                                                                    15.0,
        //                                                                                    10.0,
        //                                                                                    10.0,
        //                                                                                    10.0));
        //                }
        //                else
        //                {
        //                    //PTV structure
        //                    //5 mm uniform margin to generate PTV
        //                    tmp = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ctv_spine");
        //                    if (tmp != null && !tmp.IsEmpty) itr.SegmentVolume = tmp.Margin(5.0);
        //                    else { MessageBox.Show("Error! Could not retrieve CTV_Spine structure! Exiting!"); return true; }
        //                }
        //            }
        //            else { MessageBox.Show("Error! Could not retrieve brain structure! Exiting!"); return true; }
        //        }
        //    }

        //    Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Generating: PTV_CSI")); }));
        //    //used to create the ptv_csi structures
        //    Structure combinedTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_csi");
        //    Structure brainTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_brain"));
        //    Structure spineTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_spine"));
        //    combinedTarget.SegmentVolume = brainTarget.Margin(0.0);
        //    combinedTarget.SegmentVolume = combinedTarget.Or(spineTarget.Margin(0.0));

        //    Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Cropping PTV_CSI from body with 5 mm inner margin")); }));

        //    //1/3/2022, crop PTV structure from body by 5mm
        //    generate.cropStructureFromBody(combinedTarget, -0.5);
        //    Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Targets added and contoured!")); }));
        //    return false;
        //}

        //public bool createTSStructures(StructureSet selectedSS, List<Tuple<string, string>> TS_structures, List<Tuple<string, double, string>> targets)
        //{
        //    //determine if any TS structures need to be added to the selected structure set
        //    //foreach (Tuple<string, string, double> itr in spareStructList)
        //    //{
        //    //    //optParameters.Add(Tuple.Create(itr.Item1, itr.Item2));
        //    //    //this is here to add
        //    //    if (itr.Item2 == "Crop target from structure") foreach (Tuple<string, string> itr1 in TS_structures.Where(x => x.Item2.ToLower().Contains(itr.Item1.ToLower()))) AddTSStructures(itr1);
        //    //}
        //    //get all TS structures that do not contain 'ctv' or 'ptv' in the title
        //    Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format("Adding remaining tuning structures to stack!")); }));
        //    List<Tuple<string, string>> remainingTS = TS_structures.Where(x => !x.Item2.ToLower().Contains("ctv") && !x.Item2.ToLower().Contains("ptv")).ToList();
        //    calcItems = remainingTS.Count;
        //    int counter = 0;
        //    foreach (Tuple<string, string> itr in remainingTS)
        //    {
        //        Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Adding TS to added structures: {0}", itr.Item2)); }));
        //        generate.AddTSStructures(itr);
        //        addedStructures.Add(itr.Item2);
        //    }

        //    Dispatcher.BeginInvoke((Action)(() => { provideUpdate(0, String.Format("Contouring tuning structures!")); }));
        //    //now contour the various structures
        //    foreach (string itr in addedStructures.Where(x => !x.ToLower().Contains("ctv") && !x.ToLower().Contains("ptv")))
        //    {
        //        counter = 0;
        //        Dispatcher.BeginInvoke((Action)(() => { provideUpdate(0, String.Format("Contouring TS: {0}", itr)); }));

        //        //MessageBox.Show(String.Format("create TS: {0}", itr));
        //        Structure addedStructure = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == itr.ToLower());
        //        if (itr.ToLower().Contains("ts_ring"))
        //        {
        //            if (double.TryParse(itr.Substring(7, itr.Length - 7), out double ringDose))
        //            {
        //                calcItems = targets.Count;
        //                foreach (Tuple<string, double, string> itr1 in targets)
        //                {
        //                    Structure targetStructure = selectedSS.Structures.FirstOrDefault(x => x.Id == itr1.Item1);
        //                    if (targetStructure != null)
        //                    {
        //                        Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format("Generating ring {0} for target {1}", itr, itr1.Item1)); }));
        //                        //margin in mm. 
        //                        double margin = ((itr1.Item2 - ringDose) / itr1.Item2) * 30.0;
        //                        //Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format("Generating ring {0} for target {1}", itr, itr1.Item1)); }));
        //                        if (margin > 0.0)
        //                        {
        //                            //method to create ring of 2.0 cm thickness
        //                            //first create structure that is a copy of the target structure with an outer margin of ((Rx - ring dose / Rx) * 30 mm) + 20 mm.
        //                            //1/5/2023, nataliya stated the 50% Rx ring should be 1.5 cm from the target and have a thickness of 2 cm. Redefined the margin formula to equal 15 mm whenever (Rx - ring dose) / Rx = 0.5
        //                            addedStructure.SegmentVolume = targetStructure.Margin(margin + 20.0 > 50.0 ? 50.0 : margin + 20.0);
        //                            //now, contour the ring as the original ring minus the dummy structure
        //                            addedStructure.SegmentVolume = addedStructure.Sub(targetStructure.Margin(margin));
        //                            //keep only the parts of the ring that are inside the body!
        //                            generate.cropStructureFromBody(addedStructure, 0.0);
        //                            Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Finished contouring ring: {0}", itr)); }));
        //                        }
        //                    }
        //                }
        //            }
        //            else Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format("Could not parse ring dose for {0}! Skipping!", itr)); }));
        //        }
        //        else if (itr.ToLower().Contains("armsavoid")) createArmsAvoid(addedStructure, selectedSS);
        //        else if (!(itr.ToLower().Contains("ptv")))
        //        {
        //            calcItems = 4;
        //            //all other sub structures
        //            Structure originalStructure = null;
        //            double margin = 0.0;
        //            int pos1 = itr.IndexOf("-");
        //            int pos2 = itr.IndexOf("cm");
        //            if (pos1 != -1 && pos2 != -1)
        //            {
        //                string originalStructureId = itr.Substring(0, pos1);
        //                Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Grabbing margin value!")); }));
        //                double.TryParse(itr.Substring(pos1, pos2 - pos1), out margin);

        //                Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Grabbing original structure!")); }));
        //                if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains(originalStructureId.ToLower()) && x.Id.ToLower().Contains("_low")) == null) originalStructure = selectedSS.Structures.First(x => x.Id.ToLower().Contains(originalStructureId.ToLower()));
        //                else originalStructure = selectedSS.Structures.First(x => x.Id.ToLower().Contains(originalStructureId.ToLower()) && x.Id.ToLower().Contains("_low"));

        //                Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Creating {0} structure!", margin > 0 ? "outer" : "sub")); }));
        //                //convert from cm to mm
        //                addedStructure.SegmentVolume = originalStructure.Margin(margin * 10);
        //                if (addedStructure.IsEmpty) selectedSS.RemoveStructure(addedStructure);
        //                Dispatcher.BeginInvoke((Action)(() => { provideUpdate(100); }));
        //            }
        //        }
        //    }
        //    return false;
        //}

        //public bool createArmsAvoid(Structure armsAvoid, StructureSet selectedSS)
        //{
        //    Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format("Preparing to contour TS_arms...")); }));

        //    //generate arms avoid structures
        //    //need lungs, body, and ptv spine structures
        //    Structure lungs = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "lungs");
        //    Structure body = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "body");
        //    MeshGeometry3D mesh = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_spine").MeshGeometry;
        //    //get most inferior slice of ptv_spine (mesgeometry.bounds.z indicates the most inferior part of a structure)
        //    int startSlice = (int)((mesh.Bounds.Z - selectedSS.Image.Origin.z) / selectedSS.Image.ZRes);
        //    //only go to the most superior part of the lungs for contouring the arms
        //    int stopSlice = (int)((lungs.MeshGeometry.Positions.Max(p => p.Z) - selectedSS.Image.Origin.z) / selectedSS.Image.ZRes) + 1;

        //    //initialize variables
        //    double xMax = 0;
        //    double xMin = 0;
        //    double yMax = 0;
        //    double yMin = 0;
        //    VVector[][] bodyPts;
        //    //generate two dummy structures (L and R)
        //    if (selectedSS.Structures.Where(x => x.Id.ToLower() == "dummyboxl").Any()) selectedSS.RemoveStructure(selectedSS.Structures.First(x => x.Id.ToLower() == "dummyboxl"));
        //    Structure dummyBoxL = selectedSS.AddStructure("CONTROL", "DummyBoxL");
        //    if (selectedSS.Structures.Where(x => x.Id.ToLower() == "dummyboxr").Any()) selectedSS.RemoveStructure(selectedSS.Structures.First(x => x.Id.ToLower() == "dummyboxr"));
        //    Structure dummyBoxR = selectedSS.AddStructure("CONTROL", "DummyBoxR");
        //    //use the center point of the lungs as the y axis anchor
        //    double yCenter = lungs.CenterPoint.y;
        //    //extend box in y direction +/- 20 cm
        //    yMax = yCenter + 200.0;
        //    yMin = yCenter - 200.0;

        //    //set box width in lateral direction
        //    double boxXWidth = 50.0;
        //    //empty vectors to hold points for left and right dummy boxes for each slice
        //    VVector[] ptsL = new[] { new VVector() };
        //    VVector[] ptsR = new[] { new VVector() };

        //    Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format("Preparation complete!")); }));
        //    Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format("Contouring TS_arms now...")); }));
        //    calcItems = stopSlice - startSlice + 3;
        //    Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format("Number of image slices to contour: {0}", stopSlice - startSlice)); }));
        //    int counter = 0;

        //    for (int slice = startSlice; slice < stopSlice; slice++)
        //    {
        //        //get body contour points
        //        bodyPts = body.GetContoursOnImagePlane(slice);
        //        xMax = -500000000000.0;
        //        xMin = 500000000000.0;
        //        //find min and max x positions for the body on this slice (so we can adapt the box positions for each slice)
        //        for (int i = 0; i < bodyPts.GetLength(0); i++)
        //        {
        //            if (bodyPts[i].Max(p => p.x) > xMax) xMax = bodyPts[i].Max(p => p.x);
        //            if (bodyPts[i].Min(p => p.x) < xMin) xMin = bodyPts[i].Min(p => p.x);
        //        }

        //        //box with contour points located at (x,y), (x,0), (x,-y), (0,-y), (-x,-y), (-x,0), (-x, y), (0,y)
        //        ptsL = new[] {
        //                            new VVector(xMax, yMax, 0),
        //                            new VVector(xMax, 0, 0),
        //                            new VVector(xMax, yMin, 0),
        //                            new VVector(0, yMin, 0),
        //                            new VVector(xMax-boxXWidth, yMin, 0),
        //                            new VVector(xMax-boxXWidth, 0, 0),
        //                            new VVector(xMax-boxXWidth, yMax, 0),
        //                            new VVector(0, yMax, 0)};

        //        ptsR = new[] {
        //                            new VVector(xMin + boxXWidth, yMax, 0),
        //                            new VVector(xMin + boxXWidth, 0, 0),
        //                            new VVector(xMin + boxXWidth, yMin, 0),
        //                            new VVector(0, yMin, 0),
        //                            new VVector(xMin, yMin, 0),
        //                            new VVector(xMin, 0, 0),
        //                            new VVector(xMin, yMax, 0),
        //                            new VVector(0, yMax, 0)};

        //        //add contours on this slice
        //        dummyBoxL.AddContourOnImagePlane(ptsL, slice);
        //        dummyBoxR.AddContourOnImagePlane(ptsR, slice);
        //        Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems)); }));
        //    }

        //    //extend the arms avoid structure superiorly by x number of slices
        //    //for (int slice = stop; slice < stop + 10; slice++)
        //    //{
        //    //    dummyBoxL.AddContourOnImagePlane(ptsL, slice);
        //    //    dummyBoxR.AddContourOnImagePlane(ptsR, slice);
        //    //}

        //    //now contour the arms avoid structure as the union of the left and right dummy boxes
        //    Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Unioning left and right arms avoid structures together!")); }));
        //    armsAvoid.SegmentVolume = dummyBoxL.Margin(0.0);
        //    armsAvoid.SegmentVolume = armsAvoid.Or(dummyBoxR.Margin(0.0));
        //    Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Subtracting arms avoid from body with 5mm outer margin!")); }));
        //    //contour the arms as the overlap between the current armsAvoid structure and the body with a 5mm outer margin
        //    generate.cropStructureFromBody(armsAvoid, 0.5);

        //    Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Cleaning up!")); }));
        //    selectedSS.RemoveStructure(dummyBoxR);
        //    selectedSS.RemoveStructure(dummyBoxL);
        //    Dispatcher.BeginInvoke((Action)(() => { provideUpdate(100, String.Format("Finished contouring arms avoid!")); }));
        //    return false;
        //}

        //private bool performTSStructureManipulation(StructureSet selectedSS, List<Tuple<string, double, string>> targets, List<Tuple<string, string, double>> spareStructList)
        //{
        //    Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format("Retrieved list of TS manipulations")); }));
        //    //there are items in the sparing list requiring structure manipulation
        //    List<Tuple<string, string, double>> tmpSpareLst = spareStructList.Where(x => x.Item2.Contains("Crop target from structure") || x.Item2.Contains("Contour")).ToList();
        //    if (tmpSpareLst.Any())
        //    {
        //        int counter = 0;
        //        calcItems = tmpSpareLst.Count * targets.Count;
        //        foreach (Tuple<string, double, string> itr in targets)
        //        {
        //            //create a new TS target for optimization and copy the original target structure onto the new TS structure
        //            string newName = String.Format("TS_{0}", itr.Item1);
        //            if (newName.Length > 16) newName = newName.Substring(0, 16);
        //            Structure addedTSTarget = selectedSS.Structures.FirstOrDefault(x => x.Id == newName);
        //            if (addedTSTarget == null)
        //            {
        //                Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format("Creating TS target: {0}", newName)); }));
        //                addedTSTarget = generate.AddTSStructures(new Tuple<string, string>("CONTROL", newName));
        //                addedTSTarget.SegmentVolume = selectedSS.Structures.FirstOrDefault(x => x.Id == itr.Item1).Margin(0.0);
        //            }
        //            foreach (Tuple<string, string, double> itr1 in tmpSpareLst)
        //            {
        //                Structure theStructure = selectedSS.Structures.FirstOrDefault(x => x.Id == itr1.Item1);
        //                if (itr1.Item2.Contains("Crop"))
        //                {
        //                    if (itr1.Item2.Contains("Body"))
        //                    {
        //                        Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Cropping {0} from Body", itr1.Item1)); }));
        //                        //crop from body
        //                        generate.cropStructureFromBody(theStructure, itr1.Item3);
        //                    }
        //                    else
        //                    {
        //                        Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Cropping {0} from target {1}", itr1.Item1, newName)); }));
        //                        //crop target from structure
        //                        generate.cropTargetFromStructure(addedTSTarget, theStructure, itr1.Item3);
        //                    }
        //                }
        //                else if (itr1.Item2.Contains("Contour"))
        //                {
        //                    Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format("Contouring overlap between {0} and {1}", itr1.Item1, newName)); }));
        //                    newName = String.Format("ts_{0}_overlap", itr.Item1);
        //                    if (newName.Length > 16) newName = newName.Substring(0, 16);
        //                    Structure addedTSNormal = generate.AddTSStructures(new Tuple<string, string>("CONTROL", newName));
        //                    addedTSNormal.SegmentVolume = theStructure.Margin(0.0);
        //                    generate.contourOverlap(addedTSTarget, addedTSNormal, itr1.Item3);
        //                    Structure tmp = selectedSS.AddStructure("CONTROL", "dummy");
        //                    tmp.SegmentVolume = addedTSNormal.Margin(0.0);
        //                    tmp.Sub(theStructure.Margin(0.0));
        //                    if (tmp.IsEmpty) selectedSS.RemoveStructure(addedTSNormal);
        //                    selectedSS.RemoveStructure(tmp);
        //                    Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Contoured overlap between {0} and {1}", itr1.Item1, newName)); }));
        //                }
        //            }
        //        }
        //    }
        //    else Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format("No TS manipulations requested!")); }));
        //    return false;
        //}

        public void UpdateLabel(string message)
        {
            theLabel.Text = message;
        }

        public void provideUpdate(int percentComplete, string message)
        {
            progress.Value = percentComplete;
            progressTB.Text += message + System.Environment.NewLine;
            scroller.ScrollToBottom();
            //updateLogFile(message);
        }

        public void provideUpdate(int percentComplete) { progress.Value = percentComplete; }

        public void provideUpdate(string message) { progressTB.Text += message + System.Environment.NewLine; scroller.ScrollToBottom(); }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SizeToContent = SizeToContent.WidthAndHeight;
        }
    }
}
