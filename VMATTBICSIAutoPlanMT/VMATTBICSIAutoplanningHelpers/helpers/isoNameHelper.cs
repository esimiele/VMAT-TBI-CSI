using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMATTBICSIAutoplanningHelpers.helpers
{
    public class isoNameHelper
    {
        public List<string> getIsoNames(int numVMATIsos, int numIsos, bool isCSI = false)
        {
            List<string> isoNames = new List<string> { };

            if(!isCSI)
            {
                isoNames.Add("Head");
                if (numVMATIsos > 1 || numIsos > 1)
                {
                    if (numIsos > numVMATIsos)
                    {
                        if (numVMATIsos == 2) isoNames.Add("Pelvis");
                        else
                        {
                            isoNames.Add("Chest");
                            if (numVMATIsos == 3) isoNames.Add("Pelvis");
                            else if (numVMATIsos == 4) { isoNames.Add("Abdomen"); isoNames.Add("Pelvis"); }
                        }
                        isoNames.Add("AP / PA upper legs");
                        if (numIsos == numVMATIsos + 2) isoNames.Add("AP / PA lower legs");
                    }
                    else
                    {
                        if (numVMATIsos == 2) isoNames.Add("Pelvis");
                        else
                        {
                            isoNames.Add("Chest");
                            if (numVMATIsos == 3) isoNames.Add("Legs");
                            else if (numVMATIsos == 4) { isoNames.Add("Pelvis"); isoNames.Add("Legs"); }
                        }
                    }
                }
            }
            else
            {
                isoNames.Add("Brain");
                if(numVMATIsos > 1)
                {
                    if (numVMATIsos > 2) isoNames.Add("UpSpine");
                    isoNames.Add("LowSpine");
                }
            }
            return isoNames;
        }
    }
}
