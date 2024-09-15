using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.Types;
using Telerik.JustMock;
using VMS.TPS.Common.Model.API;
using VMATTBICSIAutoPlanningHelpers.Models;
using VMATTBICSIAutoPlanningHelpersTests.EqualityComparerClasses;

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    [TestClass()]
    public class PlanObjectiveHelperTests
    {
        [TestMethod()]
        public void ConstructPlanObjectivesTest()
        {
            //structure id, objective type, dose (% or cGy), volume (%), dose value presentation
            List<PlanObjectiveModel> testPlanObj = new List<PlanObjectiveModel>
            {
                new PlanObjectiveModel("PTV_Boost", OptimizationObjectiveType.Lower, 100.0, Units.cGy, 95.0, Units.Percent),
                new PlanObjectiveModel("PTV_Boost", OptimizationObjectiveType.Upper, 110.0, Units.cGy, 0.0, Units.Percent),
                new PlanObjectiveModel("PTV_CSI", OptimizationObjectiveType.Lower, 3600.0, Units.cGy, 95.0, Units.Percent),
                new PlanObjectiveModel("PTV_CSI", OptimizationObjectiveType.Upper, 3750.0, Units.cGy, 0.0, Units.Percent),
                new PlanObjectiveModel("Brainstem", OptimizationObjectiveType.Upper, 104.0, Units.cGy, 0.0, Units.Percent),
                new PlanObjectiveModel("SpinalCord", OptimizationObjectiveType.Upper, 104.0, Units.cGy, 0.0, Units.Percent),
                new PlanObjectiveModel("OpticNrvs", OptimizationObjectiveType.Upper, 104.0, Units.cGy, 0.0, Units.Percent)
            };

            Dictionary<string, string> testTSTargets = new Dictionary<string, string>
            {
                {"PTV_Boost", "ts_PTV_Boost" },
                { "PTV_CSI", "ts_PTV_CSI" }
            };

            Structure tsPTVBoost = Mock.Create<Structure>();
            Mock.Arrange(() => tsPTVBoost.Id).Returns("ts_PTV_Boost");
            Structure tsPTVCSI = Mock.Create<Structure>();
            Mock.Arrange(() => tsPTVCSI.Id).Returns("ts_PTV_CSI");
            Structure brainstem = Mock.Create<Structure>();
            Mock.Arrange(() => brainstem.Id).Returns("Brainstem");
            Structure spinalcord = Mock.Create<Structure>();
            Mock.Arrange(() => spinalcord.Id).Returns("SpinalCord");
            Structure opticNrvs = Mock.Create<Structure>();
            Mock.Arrange(() => opticNrvs.Id).Returns("OpticNrvs");
            StructureSet ss = Mock.Create<StructureSet>();
            Mock.Arrange(() => ss.Structures).Returns(new List<Structure> { tsPTVBoost, tsPTVCSI, brainstem, spinalcord, opticNrvs });

            List<PlanObjectiveModel> expected = new List<PlanObjectiveModel>
            {
                new PlanObjectiveModel("ts_PTV_Boost", OptimizationObjectiveType.Lower, 100.0, Units.cGy, 95.0, Units.Percent),
                new PlanObjectiveModel("ts_PTV_Boost", OptimizationObjectiveType.Upper, 110.0, Units.cGy, 0.0, Units.Percent),
                new PlanObjectiveModel("ts_PTV_CSI", OptimizationObjectiveType.Lower, 3600.0, Units.cGy,95.0, Units.Percent),
                new PlanObjectiveModel("ts_PTV_CSI", OptimizationObjectiveType.Upper, 3750.0, Units.cGy,0.0, Units.Percent),
                new PlanObjectiveModel("Brainstem", OptimizationObjectiveType.Upper, 104.0, Units.cGy,0.0, Units.Percent),
                new PlanObjectiveModel("SpinalCord", OptimizationObjectiveType.Upper, 104.0, Units.cGy,0.0, Units.Percent),
                new PlanObjectiveModel("OpticNrvs", OptimizationObjectiveType.Upper, 104.0, Units.cGy, 0.0, Units.Percent)
            };

            List<PlanObjectiveModel> result = PlanObjectiveHelper.ConstructPlanObjectives(testPlanObj, ss, testTSTargets);
            PlanObjectiveModelComparer comparer = new PlanObjectiveModelComparer();

            Assert.AreEqual(expected.Count(), result.Count());
            int count = 0;
            foreach(PlanObjectiveModel itr in result)
            {
                Assert.IsTrue(comparer.Equals(itr, expected.ElementAt(count)));
                count++;
            }
        }
    }
}