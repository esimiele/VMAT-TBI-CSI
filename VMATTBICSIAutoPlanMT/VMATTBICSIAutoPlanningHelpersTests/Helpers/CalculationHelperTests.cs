using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telerik.JustMock;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    [TestClass()]
    public class CalculationHelperTests
    {
        [TestMethod()]
        public void AreEqualFailTest()
        {
            bool expected = false;
            double x = 10;
            double y = 10.1;
            double tolerance = 0.001;
            Assert.AreEqual(expected, CalculationHelper.AreEqual(x, y, tolerance));
        }

        [TestMethod()]
        public void AreEqualPassTest()
        {
            bool expected = true;
            double x = 10;
            double y = 10.001;
            double tolerance = 0.001;
            Assert.AreEqual(expected, CalculationHelper.AreEqual(x, y, tolerance));
        }

        [TestMethod()]
        public void ComputeAverageFailTest()
        {
            double expected = 14.0;
            double x = 10;
            double y = 20;
            Assert.AreNotEqual(expected, CalculationHelper.ComputeAverage(x, y));
        }

        public void ComputeAveragePassTest()
        {
            double expected = 15.0;
            double x = 10;
            double y = 20;
            Assert.AreEqual(expected, CalculationHelper.ComputeAverage(x, y));
        }

        //untested
        [TestMethod()]
        public void ComputeSliceFailTest()
        {
            int expected = 50;
            double z = 100;
            StructureSet ss = Mock.Create<StructureSet>();
            Image theImage = Mock.Create<Image>();
            VVector origin = new VVector(0, 0, 2.0);
            Mock.Arrange(() => ss.Image).Returns(theImage);
            Mock.Arrange(() => theImage.Origin).Returns(origin);
            Assert.AreEqual(expected, CalculationHelper.ComputeSlice(z, ss));
        }
        
        //untested
        [TestMethod()]
        public void ComputeSlicePassTest()
        {
            int expected = 49;
            double z = 100;
            StructureSet ss = Mock.Create<StructureSet>();
            Image theImage = Mock.Create<Image>();
            VVector origin = new VVector(0, 0, 2.0);
            Mock.Arrange(() => ss.Image).Returns(theImage);
            Mock.Arrange(() => theImage.Origin).Returns(origin);
            Assert.AreNotEqual(expected, CalculationHelper.ComputeSlice(z, ss));
        }
    }
}