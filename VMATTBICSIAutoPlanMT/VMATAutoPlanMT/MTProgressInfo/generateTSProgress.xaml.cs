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

namespace VMATAutoPlanMT.MTProgressInfo
{
    /// <summary>
    /// Interaction logic for generateTSProgress.xaml
    /// </summary>
    public partial class generateTSProgress : Window
    {
        ESAPIworker slave;
        generateTS_CSI generate;
        public int calcItems;
        StructureSet ss;
        public List<string> addedStructures = new List<string> { };
        public generateTSProgress(ESAPIworker e, generateTS_CSI gen)
        {
            InitializeComponent();
            slave = e;
            generate = gen;
            ss = slave.data.selectedSS;
            try
            {
                doStuff();
            }
            catch (Exception except) { System.Windows.MessageBox.Show(except.Message); }
        }

        public void doStuff()
        {
            slave.DoWork(d =>
            {
                Dispatcher.BeginInvoke((Action)(() => { provideUpdate("Running"); }));
                calcItems = 1;
                generate.preliminaryChecks(d.selectedSS, d.spareStructList);
                Dispatcher.BeginInvoke((Action)(() => { provideUpdate(100, "Preliminary checks complete!"); }));
                Dispatcher.BeginInvoke((Action)(() => { provideUpdate(0, "Checking for L and R structures to union!"); }));
                UnionLRStructures(d.selectedSS);
                calcItems = 1;
                Dispatcher.BeginInvoke((Action)(() => { provideUpdate(0, "Removing prior/existing tuning structures!"); }));
                generate.RemoveOldTSStructures(d.TS_structures, d.selectedSS);
                Dispatcher.BeginInvoke((Action)(() => { provideUpdate(100, "Removed prior/existing tuning structures!"); }));
                Dispatcher.BeginInvoke((Action)(() => { provideUpdate(0, "Creating target structures!"); }));
                createTargetStructures(d.selectedSS, d.TS_structures);
                Dispatcher.BeginInvoke((Action)(() => { provideUpdate(0, "Creating tuning structures!"); }));
                createTSStructures(d.selectedSS, d.TS_structures, d.targets);
                Dispatcher.BeginInvoke((Action)(() => { provideUpdate(100, "Finished contouring structures!"); }));
            });
        }
        public bool UnionLRStructures(StructureSet selectedSS)
        {
            int numUnioned = 0;
            StructureTuningUIHelper helper = new StructureTuningUIHelper();
            List<Tuple<Structure, Structure, string>> structuresToUnion = helper.checkStructuresToUnion(selectedSS);
            if (structuresToUnion.Any())
            {
                calcItems = structuresToUnion.Count;
                foreach (Tuple<Structure, Structure, string> itr in structuresToUnion)
                {
                    if (!helper.unionLRStructures(itr, selectedSS)) Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++numUnioned / calcItems), String.Format("Unioned {0}", itr.Item3)); }));
                    else return true;
                }
                Dispatcher.BeginInvoke((Action)(() => { provideUpdate(100, "Structures unioned successfully!"); }));
            }
            else Dispatcher.BeginInvoke((Action)(() => { provideUpdate(100, "No structures to union!"); }));
            return false;
        }

        public bool createTargetStructures(StructureSet selectedSS, List<Tuple<string,string>> TS_structures)
        {
            //create the CTV and PTV structures
            //if these structures were present, they should have been removed (regardless if they were contoured or not). 
            List<Structure> addedTargets = new List<Structure> { };
            List<Tuple<string, string>> prospectiveTargets = TS_structures.Where(x => x.Item2.ToLower().Contains("ctv") || x.Item2.ToLower().Contains("ptv")).OrderBy(x => x.Item2).ToList();
            calcItems = prospectiveTargets.Count;
            int counter = 0;
            foreach (Tuple<string, string> itr in prospectiveTargets)
            {
                if (selectedSS.CanAddStructure(itr.Item1, itr.Item2))
                {
                    addedStructures.Add(itr.Item2);
                    addedTargets.Add(selectedSS.AddStructure(itr.Item1, itr.Item2));
                    Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Added target: {0}", itr.Item2)); }));
                    //optParameters.Add(new Tuple<string,string>(itr.Item1, itr.Item2));
                }
                else
                {
                    MessageBox.Show(String.Format("Can't add {0} to the structure set!", itr.Item2));
                    return true;
                }
            }

            Structure tmp = null;
            calcItems = addedTargets.Count + 3;
            counter = 0;
            foreach (Structure itr in addedTargets)
            {
                string targetId = itr.Id;
                Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Contoured target: {0}", targetId)); }));
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
                else if (itr.Id.ToLower().Contains("spine"))
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

            Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Generating: PTV_CSI")); }));
            //used to create the ptv_csi structures
            Structure combinedTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_csi");
            Structure brainTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_brain"));
            Structure spineTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_spine"));
            combinedTarget.SegmentVolume = brainTarget.Margin(0.0);
            combinedTarget.SegmentVolume = combinedTarget.Or(spineTarget.Margin(0.0));

            Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Cropping PTV_CSI from body with 5 mm inner margin")); }));

            //1/3/2022, crop PTV structure from body by 5mm
            generate.cropStructureFromBody(combinedTarget, -0.5);
            Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Targets added and contoured!")); }));
            return false;
        }

        public bool createTSStructures(StructureSet selectedSS, List<Tuple<string, string>> TS_structures, List<Tuple<string,double,string>> targets)
        {
            //determine if any TS structures need to be added to the selected structure set
            //foreach (Tuple<string, string, double> itr in spareStructList)
            //{
            //    //optParameters.Add(Tuple.Create(itr.Item1, itr.Item2));
            //    //this is here to add
            //    if (itr.Item2 == "Crop target from structure") foreach (Tuple<string, string> itr1 in TS_structures.Where(x => x.Item2.ToLower().Contains(itr.Item1.ToLower()))) AddTSStructures(itr1);
            //}
            //get all TS structures that do not contain 'ctv' or 'ptv' in the title
            Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format("Adding remaining tuning structures to stack!")); }));
            List<Tuple<string,string>> remainingTS = TS_structures.Where(x => !x.Item2.ToLower().Contains("ctv") && !x.Item2.ToLower().Contains("ptv")).ToList();
            calcItems = remainingTS.Count;
            int counter = 0;
            foreach (Tuple<string, string> itr in remainingTS)
            {
                Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Adding TS to added structures: {0}", itr.Item2)); }));
                generate.AddTSStructures(itr);
                addedStructures.Add(itr.Item2);
            }

            counter = 0;
            calcItems += 1;
            Dispatcher.BeginInvoke((Action)(() => { provideUpdate(0, String.Format("Contouring tuning structures!")); }));
            //now contour the various structures
            foreach (string itr in addedStructures.Where(x => !x.ToLower().Contains("ctv") && !x.ToLower().Contains("ptv")))
            {
                Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format( "Contouring TS: {0}", itr)); }));

                //MessageBox.Show(String.Format("create TS: {0}", itr));
                Structure addedStructure = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == itr.ToLower());
                if (itr.ToLower().Contains("ts_ring"))
                {
                    if (double.TryParse(itr.Substring(7, itr.Length - 7), out double ringDose))
                    {
                        foreach (Tuple<string, double, string> itr1 in targets)
                        {
                            Structure targetStructure = selectedSS.Structures.FirstOrDefault(x => x.Id == itr1.Item1);
                            if (targetStructure != null)
                            {
                                //margin in mm. 
                                double margin = ((itr1.Item2 - ringDose) / itr1.Item2) * 30.0;
                                if (margin > 0.0)
                                {
                                    //method to create ring of 2.0 cm thickness
                                    //first create structure that is a copy of the target structure with an outer margin of ((Rx - ring dose / Rx) * 30 mm) + 20 mm.
                                    //1/5/2023, nataliya stated the 50% Rx ring should be 1.5 cm from the target and have a thickness of 2 cm. Redefined the margin formula to equal 15 mm whenever (Rx - ring dose) / Rx = 0.5
                                    addedStructure.SegmentVolume = targetStructure.Margin(margin + 20.0 > 50.0 ? 50.0 : margin + 20.0);
                                    //now, contour the ring as the original ring minus the dummy structure
                                    addedStructure.SegmentVolume = addedStructure.Sub(targetStructure.Margin(margin));
                                    //keep only the parts of the ring that are inside the body!
                                    generate.cropStructureFromBody(addedStructure, 0.0);
                                }
                            }
                        }
                    }
                    else MessageBox.Show(String.Format("Could not parse ring dose for {0}! Skipping!", itr));
                }
                else if (itr.ToLower().Contains("armsavoid")) generate.createArmsAvoid(addedStructure);
                else if (!(itr.ToLower().Contains("ptv")))
                {
                    //all other sub structures
                    Structure originalStructure = null;
                    double margin = 0.0;
                    int pos1 = itr.IndexOf("-");
                    int pos2 = itr.IndexOf("cm");
                    if (pos1 != -1 && pos2 != -1)
                    {
                        string originalStructureId = itr.Substring(0, pos1);
                        double.TryParse(itr.Substring(pos1, pos2 - pos1), out margin);

                        if (selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains(originalStructureId.ToLower()) && x.Id.ToLower().Contains("_low")) == null) originalStructure = selectedSS.Structures.First(x => x.Id.ToLower().Contains(originalStructureId.ToLower()));
                        else originalStructure = selectedSS.Structures.First(x => x.Id.ToLower().Contains(originalStructureId.ToLower()) && x.Id.ToLower().Contains("_low"));

                        //convert from cm to mm
                        addedStructure.SegmentVolume = originalStructure.Margin(margin * 10);
                        if (addedStructure.IsEmpty) selectedSS.RemoveStructure(addedStructure);
                    }
                }

            }
            return false;
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
