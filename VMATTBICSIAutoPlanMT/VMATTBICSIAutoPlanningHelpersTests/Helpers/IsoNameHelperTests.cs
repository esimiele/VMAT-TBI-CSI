using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    [TestClass()]
    public class IsoNameHelperTests
    {
        [TestMethod()]
        public void GetTBIVMATIsoNamesTestRunner()
        {
            //number of vmat isos, total number of isos, expected output
            List<Tuple<int, int, List<IsocenterModel>>> isoNumPairs = new List<Tuple<int, int, List<IsocenterModel>>>
            {
                Tuple.Create(2,2, new List < IsocenterModel > { new IsocenterModel("Head"), new IsocenterModel("Pelvis")}),
                Tuple.Create(3,3, new List < IsocenterModel > { new IsocenterModel("Head"), new IsocenterModel("Chest"), new IsocenterModel("Legs") }),
                Tuple.Create(2,3, new List < IsocenterModel > { new IsocenterModel("Head"), new IsocenterModel("Pelvis") }),
                Tuple.Create(4,6, new List < IsocenterModel > { new IsocenterModel("Head"), new IsocenterModel("Chest"), new IsocenterModel("Abdomen"), new IsocenterModel("Pelvis") }),
            };

            foreach(Tuple<int, int, List<IsocenterModel>> itr in isoNumPairs)
            {
                GetTBIVMATIsoNamesTest(itr.Item1, itr.Item2, itr.Item3);
            }
        }

        public void GetTBIVMATIsoNamesTest(int numVMATisos, int numIsos, List<IsocenterModel> expected)
        {
            List<IsocenterModel> isoNames = IsoNameHelper.GetTBIVMATIsoNames(numVMATisos, numIsos);
            CollectionAssert.AreEqual(expected, isoNames);
        }
    }
}