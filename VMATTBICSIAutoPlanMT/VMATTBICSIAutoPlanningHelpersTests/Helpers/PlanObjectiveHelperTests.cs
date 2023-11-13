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

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    [TestClass()]
    public class PlanObjectiveHelperTests
    {
        [TestMethod()]
        public void ConstructPlanObjectivesTest()
        {
            //structure id, objective type, dose (% or cGy), volume (%), dose value presentation
            List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> testPlanObj = new List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>>
            {
                Tuple.Create("PTV_Boost", OptimizationObjectiveType.Lower, 100.0, 95.0, DoseValuePresentation.Relative),
                Tuple.Create("PTV_Boost", OptimizationObjectiveType.Upper, 110.0, 0.0, DoseValuePresentation.Relative),
                Tuple.Create("PTV_CSI", OptimizationObjectiveType.Lower, 3600.0, 95.0, DoseValuePresentation.Absolute),
                Tuple.Create("PTV_CSI", OptimizationObjectiveType.Upper, 3750.0, 0.0, DoseValuePresentation.Absolute),
                Tuple.Create("Brainstem", OptimizationObjectiveType.Upper, 104.0, 0.0, DoseValuePresentation.Relative),
                Tuple.Create("SpinalCord", OptimizationObjectiveType.Upper, 104.0, 0.0, DoseValuePresentation.Relative),
                Tuple.Create("OpticNrvs", OptimizationObjectiveType.Upper, 104.0, 0.0, DoseValuePresentation.Relative)
            };

            List<Tuple<string, string>> testTSTargets = new List<Tuple<string, string>>
            {
                Tuple.Create("PTV_Boost", "ts_PTV_Boost"),
                Tuple.Create("PTV_CSI", "ts_PTV_CSI"),
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

            List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> expected = new List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>>
            {
                Tuple.Create("ts_PTV_Boost", OptimizationObjectiveType.Lower, 100.0, 95.0, DoseValuePresentation.Relative),
                Tuple.Create("ts_PTV_Boost", OptimizationObjectiveType.Upper, 110.0, 0.0, DoseValuePresentation.Relative),
                Tuple.Create("ts_PTV_CSI", OptimizationObjectiveType.Lower, 3600.0, 95.0, DoseValuePresentation.Absolute),
                Tuple.Create("ts_PTV_CSI", OptimizationObjectiveType.Upper, 3750.0, 0.0, DoseValuePresentation.Absolute),
                Tuple.Create("Brainstem", OptimizationObjectiveType.Upper, 104.0, 0.0, DoseValuePresentation.Relative),
                Tuple.Create("SpinalCord", OptimizationObjectiveType.Upper, 104.0, 0.0, DoseValuePresentation.Relative),
                Tuple.Create("OpticNrvs", OptimizationObjectiveType.Upper, 104.0, 0.0, DoseValuePresentation.Relative)
            };

            List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> result = PlanObjectiveHelper.ConstructPlanObjectives(testPlanObj, ss, testTSTargets);

            CollectionAssert.AreEqual(expected, result);
        }
    }
}