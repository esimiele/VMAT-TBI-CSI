using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    [TestClass()]
    public class IsoNameHelperTests
    {
        [TestMethod()]
        public void GetTBIVMATIsoNamesTestRunner()
        {
            //number of vmat isos, total number of isos, expected output
            List<Tuple<int, int, List<string>>> isoNumPairs = new List<Tuple<int, int, List<string>>>
            {
                Tuple.Create(2,2, new List < string > { "Head", "Pelvis"}),
                Tuple.Create(3,3, new List < string > { "Head", "Chest", "Legs" }),
                Tuple.Create(2,3, new List < string > { "Head", "Pelvis" }),
                Tuple.Create(4,6, new List < string > { "Head", "Chest", "Abdomen", "Pelvis" }),
            };

            foreach(Tuple<int, int, List<string>> itr in isoNumPairs)
            {
                GetTBIVMATIsoNamesTest(itr.Item1, itr.Item2, itr.Item3);
            }
        }

        public void GetTBIVMATIsoNamesTest(int numVMATisos, int numIsos, List<string> expected)
        {
            List<string> isoNames = IsoNameHelper.GetTBIVMATIsoNames(numVMATisos, numIsos);
            CollectionAssert.AreEqual(expected, isoNames);
        }
    }
}