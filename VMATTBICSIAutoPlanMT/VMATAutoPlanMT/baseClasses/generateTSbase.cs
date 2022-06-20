﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Forms;
using System.Windows.Media.Media3D;

namespace VMATAutoPlanMT
{
    public class generateTSbase
    {
        public StructureSet selectedSS;
        public List<string> addedStructures = new List<string> { };
        public List<Tuple<string, string>> optParameters = new List<Tuple<string, string>> { };
        public bool useFlash = false;
        public List<string> isoNames = new List<string> { };

        public generateTSbase()
        {

        }

        public virtual bool generateStructures()
        {
            isoNames.Clear();
            if (preliminaryChecks()) return true;
            if (createTSStructures()) return true;
            if (useFlash) if (createFlash()) return true;
            MessageBox.Show("Structures generated successfully!\nPlease proceed to the beam placement tab!");
            return false;
        }

        public virtual bool preliminaryChecks()
        {
            //specific to each case (TBI or CSI)
            return false;
        }

        public bool isUOriginInside()
        {
            if (!selectedSS.Image.HasUserOrigin || !(selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "body").IsPointInsideSegment(selectedSS.Image.UserOrigin)))
            {
                MessageBox.Show("Did you forget to set the user origin? \nUser origin is NOT inside body contour! \nPlease fix and try again!");
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

        public virtual bool RemoveOldTSStructures(List<Tuple<string, string>> structures)
        {
            //remove existing TS structures if they exist and re-add them to the structure list
            foreach (Tuple<string, string> itr in structures)
            {
                Structure tmp = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == itr.Item2.ToLower());

                //structure is present in selected structure set
                if (tmp != null)
                {
                    //check to see if the dicom type is "none"
                    if (!(tmp.DicomType == ""))
                    {
                        if (selectedSS.CanRemoveStructure(tmp)) selectedSS.RemoveStructure(tmp);
                        else
                        {
                            MessageBox.Show(String.Format("Error! \n{0} can't be removed from the structure set!", tmp.Id));
                            return true;
                        }

                        if (!selectedSS.CanAddStructure(itr.Item1, itr.Item2))
                        {
                            MessageBox.Show(String.Format("Error! \n{0} can't be added to the structure set!", itr.Item2));
                            return true;
                        }
                    }
                    else
                    {
                        MessageBox.Show(String.Format("Error! \n{0} is of DICOM type 'None'! \nESAPI can't operate on DICOM type 'None'", itr.Item2));
                        return true;
                    }
                }
            }

            //4-15-2022 
            //remove ALL tuning structures from any previous runs (structure id starts with 'TS_'). Be sure to exclude any requested TS structures from the config file as we just added them!
            List<Structure> tsStructs = selectedSS.Structures.Where(x => x.Id.ToLower().Substring(0, 3) == "ts_").ToList();
            foreach (Structure itr in tsStructs) if (!structures.Where(x => x.Item2.ToLower() == itr.Id.ToLower()).Any() && selectedSS.CanRemoveStructure(itr)) selectedSS.RemoveStructure(itr);

            return false;
        }

        public virtual List<Tuple<string,string,double>> convertHighToLowRes(List<Structure> highRes, List<Tuple<string, string, double>> highResSpareList, List<Tuple<string,string,double>> dataList)
        {
            int count = 0;
            foreach (Structure s in highRes)
            {
                //get the high res structure mesh geometry
                MeshGeometry3D mesh = s.MeshGeometry;
                //get the start and stop image planes for this structure
                int startSlice = (int)((mesh.Bounds.Z - selectedSS.Image.Origin.z) / selectedSS.Image.ZRes);
                int stopSlice = (int)(((mesh.Bounds.Z + mesh.Bounds.SizeZ) - selectedSS.Image.Origin.z) / selectedSS.Image.ZRes) + 1;
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

                //foreach slice that contains contours, get the contours, and determine if you need to add or subtract the contours on the given image plane for the new low resolution structure. You need to subtract contours if the points lie INSIDE the current structure contour.
                //We can sample three points (first, middle, and last points in array) to see if they are inside the current contour. If any of them are, subtract the set of contours from the image plane. Otherwise, add the contours to the image plane. NOTE: THIS LOGIC ASSUMES
                //THAT YOU DO NOT OBTAIN THE CUTOUT CONTOUR POINTS BEFORE THE OUTER CONTOUR POINTS (it seems that ESAPI generally passes the main structure contours first before the cutout contours, but more testing is needed)
                for (int slice = startSlice; slice < stopSlice; slice++)
                {
                    VVector[][] points = s.GetContoursOnImagePlane(slice);
                    for (int i = 0; i < points.GetLength(0); i++)
                    {
                        if (lowRes.IsPointInsideSegment(points[i][0]) || lowRes.IsPointInsideSegment(points[i][points[i].GetLength(0) - 1]) || lowRes.IsPointInsideSegment(points[i][(int)(points[i].GetLength(0) / 2)])) lowRes.SubtractContourOnImagePlane(points[i], slice);
                        else lowRes.AddContourOnImagePlane(points[i], slice);
                        //data += System.Environment.NewLine;
                    }
                }

                //get the index of the high resolution structure in the structure sparing list and repace this entry with the newly created low resolution structure
                int index = dataList.IndexOf(highResSpareList.ElementAt(count));
                dataList.RemoveAt(index);
                dataList.Insert(index, new Tuple<string, string, double>(newName, highResSpareList.ElementAt(count).Item2, highResSpareList.ElementAt(count).Item3));
                count++;
            }

            return dataList;
        }

        public virtual void AddTSStructures(Tuple<string, string> itr1)
        {
            string dicomType = itr1.Item1;
            string structName = itr1.Item2;
            if (selectedSS.CanAddStructure(dicomType, structName))
            {
                selectedSS.AddStructure(dicomType, structName);
                addedStructures.Add(structName);
                optParameters.Add(Tuple.Create(structName, "Mean Dose < Rx Dose"));
            }
            else MessageBox.Show(String.Format("Can't add {0} to the structure set!", structName));
        }
    }
}
