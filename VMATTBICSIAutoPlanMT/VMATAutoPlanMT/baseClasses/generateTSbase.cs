using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using VMATAutoPlanMT.MTProgressInfo;
using System.Windows.Threading;

namespace VMATAutoPlanMT.baseClasses
{
    public class generateTSbase : MTbase
    {
        public StructureSet selectedSS;
        public List<string> addedStructures = new List<string> { };
        public List<Tuple<string, string>> optParameters = new List<Tuple<string, string>> { };
        public bool useFlash = false;
        //plan Id, list of isocenter names for this plan
        public List<Tuple<string,List<string>>> isoNames = new List<Tuple<string, List<string>>> { };
        

        public generateTSbase()
        {

        }

        public virtual bool PerformStructureGeneration()
        {
            return false;
        }

        public virtual bool generateStructures()
        {
            ESAPIworker slave = new ESAPIworker();
            //create a new frame (multithreading jargon)
            DispatcherFrame frame = new DispatcherFrame();
            slave.RunOnNewThread(() =>
            {
                //pass the progress window the newly created thread and this instance of the optimizationLoop class.
                MTProgress pw = new MTProgress();
                pw.setCallerClass(slave, this);
                pw.ShowDialog();

                //tell the code to hold until the progress window closes.
                frame.Continue = false;
            });
            Dispatcher.PushFrame(frame);
            return false;
        }

        public virtual bool preliminaryChecks()
        {
            //specific to each case (TBI or CSI)
            return false;
        }

        public bool isUOriginInside(StructureSet ss)
        {
            if (!ss.Image.HasUserOrigin || !(ss.Structures.FirstOrDefault(x => x.Id.ToLower() == "body").IsPointInsideSegment(ss.Image.UserOrigin)))
            {
                //MessageBox.Show("Did you forget to set the user origin? \nUser origin is NOT inside body contour! \nPlease fix and try again!");
                ProvideUIUpdate("Did you forget to set the user origin? \nUser origin is NOT inside body contour! \nPlease fix and try again!");
                return true;
            }
            return false;
        }

        public virtual bool createTSStructures()
        {
            //no virtual method implementation as this code really can't be abstracted
            return false;
        }

        public virtual bool createFlash()
        {
            //no virtual method implementation as this method is really only useful for VMAT TBI as VMAT CSI already has a healthy margin going from CTV->PTV
            return false;
        }

        public virtual bool RemoveOldTSStructures(List<Tuple<string,string>> structures)
        {
            UpdateUILabel("Remove Prior Tuning Structures: ");
            ProvideUIUpdate(0, "Removing prior tuning structures");
            //remove existing TS structures if they exist and re-add them to the structure list
            int calcItems = structures.Count;
            int counter = 0;
            foreach (Tuple<string, string> itr in structures)
            {
                Structure tmp = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == itr.Item2.ToLower());
                //structure is present in selected structure set
                if (tmp != null)
                {
                    //check to see if the dicom type is "none"
                    if (!(tmp.DicomType == ""))
                    {
                        if (selectedSS.CanRemoveStructure(tmp))
                        {
                            ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Removing: {0}", itr.Item2));
                            selectedSS.RemoveStructure(tmp);
                        }
                        else
                        {
                            ProvideUIUpdate(0, String.Format("Error! {0} can't be removed from the structure set!", itr.Item2));
                            //MessageBox.Show(String.Format("Error! \n{0} can't be removed from the structure set!", tmp.Id));
                            return true;
                        }

                        if (!selectedSS.CanAddStructure(itr.Item1, itr.Item2))
                        {
                            ProvideUIUpdate(0, String.Format("Error! {0} can't be added the structure set!", itr.Item2));
                            //MessageBox.Show(String.Format("Error! \n{0} can't be added to the structure set!", itr.Item2));
                            return true;
                        }
                    }
                    else
                    {
                        ProvideUIUpdate(0, String.Format("{0} is of DICOM type 'None'! ESAPI can't operate on DICOM type 'None'", itr.Item2));
                        //MessageBox.Show(String.Format("Error! \n{0} is of DICOM type 'None'! \nESAPI can't operate on DICOM type 'None'", itr.Item2));
                        return true;
                    }
                }
            }

            ProvideUIUpdate(0, "Removing any remaining tuning structures");
            //4-15-2022 
            //remove ALL tuning structures from any previous runs (structure id starts with 'TS_'). Be sure to exclude any requested TS structures from the config file as we just added them!
            List<Structure> tsStructs = selectedSS.Structures.Where(x => x.Id.Length > 2 && x.Id.ToLower().Substring(0, 3) == "ts_").ToList();
            calcItems = tsStructs.Count;
            counter = 0;
            foreach (Structure itr in tsStructs)
            {
                if (!structures.Where(x => x.Item2.ToLower() == itr.Id.ToLower()).Any() && selectedSS.CanRemoveStructure(itr))
                {
                    string id = itr.Id;
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Removing: {0}", id));
                    selectedSS.RemoveStructure(itr);
                }
            }
            ProvideUIUpdate(100, String.Format("Prior tuning structures successfully removed!"));

            return false;
        }

        public virtual List<Tuple<string,string,double>> convertHighToLowRes(List<Structure> highRes, List<Tuple<string, string, double>> highResSpareList, List<Tuple<string,string,double>> dataList)
        {
            int count = 0;
            foreach (Structure s in highRes)
            {
                int counter = 0;
                int calcItems = highRes.Count + 3;
                string id = s.Id;
                ProvideUIUpdate(String.Format("Converting: {0}", id));
                //get the high res structure mesh geometry
                MeshGeometry3D mesh = s.MeshGeometry;
                //get the start and stop image planes for this structure
                int startSlice = (int)((mesh.Bounds.Z - selectedSS.Image.Origin.z) / selectedSS.Image.ZRes);
                int stopSlice = (int)(((mesh.Bounds.Z + mesh.Bounds.SizeZ) - selectedSS.Image.Origin.z) / selectedSS.Image.ZRes) + 1;
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Number of slices to contour: {0}", stopSlice - startSlice));

                //create an Id for the low resolution struture that will be created. The name will be '_lowRes' appended to the current structure Id
                string newName = s.Id + "_lowRes";
                if (newName.Length > 16) newName = newName.Substring(0, 16);
                //add a new structure (default resolution by default)
                Structure lowRes = null;
                if (selectedSS.CanAddStructure("CONTROL", newName)) lowRes = selectedSS.AddStructure("CONTROL", newName);
                else
                {
                    MessageBox.Show(String.Format("Error! Cannot add new structure: {0}!\nCorrect this issue and try again!", newName.Substring(0, 16)));
                    return new List<Tuple<string, string, double>> { };
                }
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Added low-res structure: {0}", newName));

                //foreach slice that contains contours, get the contours, and determine if you need to add or subtract the contours on the given image plane for the new low resolution structure. You need to subtract contours if the points lie INSIDE the current structure contour.
                //We can sample three points (first, middle, and last points in array) to see if they are inside the current contour. If any of them are, subtract the set of contours from the image plane. Otherwise, add the contours to the image plane. NOTE: THIS LOGIC ASSUMES
                //THAT YOU DO NOT OBTAIN THE CUTOUT CONTOUR POINTS BEFORE THE OUTER CONTOUR POINTS (it seems that ESAPI generally passes the main structure contours first before the cutout contours, but more testing is needed)
                for (int slice = startSlice; slice < stopSlice; slice++)
                {
                    ProvideUIUpdate((int)(100 * ++counter / calcItems));
                    VVector[][] points = s.GetContoursOnImagePlane(slice);
                    for (int i = 0; i < points.GetLength(0); i++)
                    {
                        if (lowRes.IsPointInsideSegment(points[i][0]) || lowRes.IsPointInsideSegment(points[i][points[i].GetLength(0) - 1]) || lowRes.IsPointInsideSegment(points[i][(int)(points[i].GetLength(0) / 2)])) lowRes.SubtractContourOnImagePlane(points[i], slice);
                        else lowRes.AddContourOnImagePlane(points[i], slice);
                        //data += System.Environment.NewLine;
                    }
                }

                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Removing existing high-res structure from sparing list and replacing with low-res"));
                //get the index of the high resolution structure in the structure sparing list and repace this entry with the newly created low resolution structure
                int index = dataList.IndexOf(highResSpareList.ElementAt(count));
                dataList.RemoveAt(index);
                dataList.Insert(index, new Tuple<string, string, double>(newName, highResSpareList.ElementAt(count).Item2, highResSpareList.ElementAt(count).Item3));
                count++;
            }

            return dataList;
        }

        public virtual Structure AddTSStructures(Tuple<string, string> itr1)
        {
            Structure addedStructure = null;
            string dicomType = itr1.Item1;
            string structName = itr1.Item2;
            if (selectedSS.CanAddStructure(dicomType, structName))
            {
                addedStructure = selectedSS.AddStructure(dicomType, structName);
                addedStructures.Add(structName);
                optParameters.Add(Tuple.Create(structName, ""));
            }
            else MessageBox.Show(String.Format("Can't add {0} to the structure set!", structName));
            return addedStructure;
        }
    }
}
