using System.Collections.Generic;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class IsoNameHelper
    {
        public static List<string> GetCSIIsoNames(int numVMATIsos)
        {
            List<string> isoNames = new List<string> { };
            isoNames.Add("Brain");
            if(numVMATIsos > 1)
            {
                if (numVMATIsos > 2) isoNames.Add("UpSpine");
                isoNames.Add("LowSpine");
            }
            return isoNames;
        }

        public static List<string> GetTBIVMATIsoNames(int numVMATIsos, int numIsos)
        {
            List<string> isoNames = new List<string> { };
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
                        else if (numVMATIsos == 4)
                        {
                            isoNames.Add("Abdomen");
                            isoNames.Add("Pelvis");
                        }
                    }
                }
                else
                {
                    if (numVMATIsos == 2) isoNames.Add("Pelvis");
                    else
                    {
                        isoNames.Add("Chest");
                        if (numVMATIsos == 3) isoNames.Add("Legs");
                        else if (numVMATIsos == 4)
                        {
                            isoNames.Add("Pelvis");
                            isoNames.Add("Legs");
                        }
                    }
                }
            }
            return isoNames;
        }

        public static List<string> GetTBIAPPAIsoNames(int numVMATIsos, int numIsos)
        {
            List<string> isoNames = new List<string> { };
            isoNames.Add("AP / PA upper legs");
            if (numIsos == numVMATIsos + 2) isoNames.Add("AP / PA lower legs");
            return isoNames;
        }
    }
}
