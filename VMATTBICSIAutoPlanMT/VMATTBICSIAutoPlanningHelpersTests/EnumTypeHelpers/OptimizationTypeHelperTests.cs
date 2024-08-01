using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using Telerik.JustMock;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers;

namespace VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers.Tests
{
    [TestClass()]
    public class OptimizationTypeHelperTests
    {
        [TestMethod()]
        public void GetObjectiveTypeTest()
        {
            OptimizationPointObjective testObj = Mock.Create<OptimizationPointObjective>();
            Mock.Arrange(() => testObj.Operator).Returns(OptimizationObjectiveOperator.Lower);

            OptimizationObjectiveType expected = OptimizationObjectiveType.Lower;
            Assert.AreEqual(expected, OptimizationTypeHelper.GetObjectiveType(testObj));
        }

        [TestMethod()]
        public void GetObjectiveTypeTestString()
        {
            OptimizationObjectiveType expected = OptimizationObjectiveType.Lower;
            Assert.AreEqual(expected, OptimizationTypeHelper.GetObjectiveType("Lower"));
        }

        [TestMethod()]
        public void GetObjectiveOperatorTest()
        {
            OptimizationObjectiveOperator expected = OptimizationObjectiveOperator.Upper;
            Assert.AreEqual(expected, OptimizationTypeHelper.GetObjectiveOperator(OptimizationObjectiveType.Upper));
        }
    }
}