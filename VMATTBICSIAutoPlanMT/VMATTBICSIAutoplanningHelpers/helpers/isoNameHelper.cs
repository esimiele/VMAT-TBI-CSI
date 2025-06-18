using System.Collections.Generic;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class IsoNameHelper
    {
        /// <summary>
        /// Simple method to specify the isocenter names for VMAT CSI
        /// </summary>
        /// <param name="numVMATIsos"></param>
        /// <returns></returns>
        public static List<IsocenterModel> GetCSIIsoNames(int numVMATIsos)
        {
            List<IsocenterModel> isoNames = new List<IsocenterModel>
            {
                new IsocenterModel("Brain")
            };
            if(numVMATIsos > 1)
            {
                if (numVMATIsos > 2) isoNames.Add(new IsocenterModel("UpSpine"));
                isoNames.Add(new IsocenterModel("LowSpine"));
            }
            return isoNames;
        }

        /// <summary>
        /// Helper method to specify the VMAT isocenter names based on the supplied number of vmat isos and total number of isos
        /// </summary>
        /// <param name="numVMATIsos"></param>
        /// <param name="numIsos"></param>
        /// <returns></returns>
        public static List<IsocenterModel> GetTBIVMATIsoNames(int numVMATIsos, int numIsos)
        {
            List<IsocenterModel> isoNames = new List<IsocenterModel>
            {
                new IsocenterModel("Head")
            };
            if (numVMATIsos > 1 || numIsos > 1)
            {
                if (numIsos > numVMATIsos)
                {
                    if (numVMATIsos == 2) isoNames.Add(new IsocenterModel("Pelvis"));
                    else
                    {
                        isoNames.Add(new IsocenterModel("Chest"));
                        if (numVMATIsos == 3) isoNames.Add(new IsocenterModel("Pelvis"));
                        else if (numVMATIsos == 4)
                        {
                            isoNames.Add(new IsocenterModel("Abdomen"));
                            isoNames.Add(new IsocenterModel("Pelvis"));
                        }
                    }
                }
                else
                {
                    if (numVMATIsos == 2) isoNames.Add(new IsocenterModel("Pelvis"));
                    else
                    {
                        isoNames.Add(new IsocenterModel("Chest"));
                        if (numVMATIsos == 3) isoNames.Add(new IsocenterModel("Legs"));
                        else if (numVMATIsos == 4)
                        {
                            isoNames.Add(new IsocenterModel("Pelvis"));
                            isoNames.Add(new IsocenterModel("Legs"));
                        }
                    }
                }
            }
            return isoNames;
        }

        /// <summary>
        /// Helper method to specify the AP/PA isocenter names based on the supplied number of vmat isos and total number of isos
        /// </summary>
        /// <param name="numVMATIsos"></param>
        /// <param name="numIsos"></param>
        /// <returns></returns>
        public static List<IsocenterModel> GetTBIAPPAIsoNames(int numVMATIsos, int numIsos)
        {
            List<IsocenterModel> isoNames = new List<IsocenterModel>
            {
                new IsocenterModel("upper legs"),
            };
            if (numIsos == numVMATIsos + 2) isoNames.Add(new IsocenterModel("lower legs"));
            return isoNames;
        }
    }
}
