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
        public generateTSProgress(ESAPIworker e, generateTS_CSI gen)
        {
            InitializeComponent();
            slave = e;
            generate = gen;
            doStuff();
        }

        public void doStuff()
        {
            slave.DoWork(d =>
            {
                try
                {
                    Dispatcher.BeginInvoke((Action)(() => { provideUpdate("Running"); }));
                    calcItems = 1;
                    //generate.preliminaryChecks(d.selectedSS, d.spareStructList);
                    Dispatcher.BeginInvoke((Action)(() => { provideUpdate(100, "Preliminary checks complete!"); }));
                    UnionLRStructures(d.selectedSS);
                    calcItems = 1;
                    //generate.RemoveOldTSStructures(d.TS_structures, d.selectedSS);
                    Dispatcher.BeginInvoke((Action)(() => { provideUpdate(100, "Removed prior tuning structures!"); }));
                }
                catch (Exception e) { Dispatcher.BeginInvoke((Action)(() => { provideUpdate(e.Message); })); }
            });
        }
        public bool UnionLRStructures(StructureSet selectedSS)
        {
            int numUnioned = 0;
            StructureTuningUIHelper helper = new StructureTuningUIHelper();
            List<Tuple<Structure, Structure, string>> structuresToUnion = helper.checkStructuresToUnion(selectedSS);
            string msg = "Structures unioned:" + Environment.NewLine;
            if (structuresToUnion.Any())
            {
                calcItems = structuresToUnion.Count;
                //foreach (Tuple<Structure, Structure, string> itr in structuresToUnion) msg += String.Format("{0}, {1}", itr.Item1.Id, itr.Item2.Id) + Environment.NewLine;
                //msg += Environment.NewLine + "Continue?";
                //confirmUI CUI = new confirmUI();
                //CUI.message.Text = msg;
                //CUI.message.Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
                //CUI.ShowDialog();
                //if (CUI.confirm) UnionStructures(structuresToUnion, helper);
                foreach (Tuple<Structure, Structure, string> itr in structuresToUnion)
                {
                    Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++numUnioned / calcItems), String.Format("Unioned {0}, {1} --> {2}", itr.Item1.Id, itr.Item2.Id, itr.Item3)); }));
                    //if (!helper.unionLRStructures(itr, selectedSS)) Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++numUnioned / calcItems), String.Format("Unioned {0} & {1} --> {2}", itr.Item1.Id, itr.Item2.Id, itr.Item3)); }));
                    //else return true;
                }
                //msg += Environment.NewLine;
                //msg += "Please review the contours after saving!";
            }

            //if (numUnioned > 0) MessageBox.Show(msg);
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
                    //addedStructures.Add(itr.Item2);
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
            calcItems = addedTargets.Count;
            counter = 0;
            foreach (Structure itr in addedTargets)
            {
                Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * ++counter / calcItems), String.Format("Contoured target: {0}", itr.Id)); }));
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

            //used to create the ptv_csi structures
            Structure combinedTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_csi");
            Structure brainTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_brain"));
            Structure spineTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_spine"));
            combinedTarget.SegmentVolume = brainTarget.Margin(0.0);
            combinedTarget.SegmentVolume = combinedTarget.Or(spineTarget.Margin(0.0));

            //1/3/2022, crop PTV structure from body by 5mm
            generate.cropStructureFromBody(combinedTarget, -0.5);
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
