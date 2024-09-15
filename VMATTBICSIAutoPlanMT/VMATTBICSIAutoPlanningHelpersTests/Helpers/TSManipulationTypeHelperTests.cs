using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers;

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    [TestClass()]
    public class TSManipulationTypeHelperTests
    {
        [TestMethod()]
        public void GetTSManipulationTypeTest()
        {
            TSManipulationType expected = TSManipulationType.CropTargetFromStructure;
            TSManipulationType result = TSManipulationTypeHelper.GetTSManipulationType("Crop target from structure");
            Assert.AreEqual(expected, result);

            expected = TSManipulationType.CropFromBody;
            result = TSManipulationTypeHelper.GetTSManipulationType("Crop from body");
            Assert.AreEqual(expected, result);

            expected = TSManipulationType.None;
            result = TSManipulationTypeHelper.GetTSManipulationType("something");
            Assert.AreEqual(expected, result);
        }
    }
}