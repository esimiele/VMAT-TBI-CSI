using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Forms;

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
            return false;
        }

        public virtual bool createTSStructures()
        {
            return false;
        }

        public virtual bool createFlash()
        {
            return false;
        }

        public virtual bool RemoveOldTSStructures(List<Tuple<string, string>> structures)
        {
            ////remove existing TS structures if they exist and re-add them to the structure list
            //foreach (Tuple<string, string> itr in structures)
            //{
            //    Structure tmp = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == itr.Item2.ToLower());

            //    //4-15-2022 
            //    //if the human_body structure exists and is not null, it is likely this script has been run previously. As a precaution, copy the human_body structure onto the body (in case flash was requested
            //    //in the previous run of the script)
            //    if (itr.Item2.ToLower() == "human_body" && tmp != null) selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "body").SegmentVolume = tmp.Margin(0.0);

            //    //structure is present in selected structure set
            //    if (tmp != null)
            //    {
            //        //check to see if the dicom type is "none"
            //        if (!(tmp.DicomType == ""))
            //        {
            //            if (selectedSS.CanRemoveStructure(tmp)) selectedSS.RemoveStructure(tmp);
            //            else
            //            {
            //                MessageBox.Show(String.Format("Error! \n{0} can't be removed from the structure set!", tmp.Id));
            //                return true;
            //            }

            //            if (!selectedSS.CanAddStructure(itr.Item1, itr.Item2))
            //            {
            //                MessageBox.Show(String.Format("Error! \n{0} can't be added to the structure set!", itr.Item2));
            //                return true;
            //            }
            //        }
            //        else
            //        {
            //            MessageBox.Show(String.Format("Error! \n{0} is of DICOM type 'None'! \nESAPI can't operate on DICOM type 'None'", itr.Item2));
            //            return true;
            //        }
            //    }

            //    //Need to add the Human body, PTV_BODY, and TS_PTV_VMAT contours manually
            //    //if these structures were present, they should have been removed (regardless if they were contoured or not). 
            //    if (itr.Item2.ToLower().Contains("human") || itr.Item2.ToLower().Contains("ptv"))
            //    {
            //        if (selectedSS.CanAddStructure(itr.Item1, itr.Item2))
            //        {
            //            selectedSS.AddStructure(itr.Item1, itr.Item2);
            //            addedStructures.Add(itr.Item2);
            //        }
            //        else
            //        {
            //            MessageBox.Show(String.Format("Can't add {0} to the structure set!", itr.Item2));
            //            return true;
            //        }
            //    }
            //}

            ////4-15-2022 
            ////remove ALL tuning structures from any previous runs (structure id starts with 'TS_'). Be sure to exclude any requested TS structures from the config file as we just added them!
            //List<Structure> tsStructs = selectedSS.Structures.Where(x => x.Id.ToLower().Substring(0, 3) == "ts_").ToList();
            //foreach (Structure itr in tsStructs) if (!structures.Where(x => x.Item2.ToLower() == itr.Id.ToLower()).Any() && selectedSS.CanRemoveStructure(itr)) selectedSS.RemoveStructure(itr);

            return false;
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
