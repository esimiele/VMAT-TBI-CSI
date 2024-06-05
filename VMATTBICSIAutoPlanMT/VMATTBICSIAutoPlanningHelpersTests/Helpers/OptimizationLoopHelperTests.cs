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
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    [TestClass()]
    public class OptimizationLoopHelperTests
    {
        [TestMethod()]
        public void CheckPlanHotspotTest()
        {
            ExternalPlanSetup plan = Mock.Create<ExternalPlanSetup>();
            PlanningItemDose dose = Mock.Create<PlanningItemDose>();
            DoseValue maxDose = new DoseValue(1500, DoseValue.DoseUnit.cGy);
            DoseValue totalDose = new DoseValue(1200, DoseValue.DoseUnit.cGy);
            Mock.Arrange(() => plan.Dose).Returns(dose);
            Mock.Arrange(() => plan.TotalDose).Returns(totalDose);
            Mock.Arrange(() => dose.DoseMax3D).Returns(maxDose);

            double threshold = 1.4;
            bool expected = false;
            double expectedDmax = 1500.0 / 1200.0;
            (bool result1, double dmax) = OptimizationLoopHelper.CheckPlanHotspot(plan, threshold);
            Assert.AreEqual(expectedDmax, dmax);
            Assert.AreEqual(expected, result1);
            threshold = 1.1;
            (bool result2, double dmax1) = OptimizationLoopHelper.CheckPlanHotspot(plan, threshold);
            Assert.AreNotEqual(expected, result2);
        }

        public List<OptimizationConstraintModel> SetupDummyOptObjList()
        {
            //rx = 3600
            return new List<OptimizationConstraintModel>
            {
                new OptimizationConstraintModel("TS_cooler120", OptimizationObjectiveType.Upper, 3888.0, Units.cGy, 0.0, 80),
                new OptimizationConstraintModel("TS_cooler110", OptimizationObjectiveType.Upper, 3888.0, Units.cGy, 0.0, 80),
                new OptimizationConstraintModel ("TS_cooler105", OptimizationObjectiveType.Upper, 3636.0, Units.cGy, 0.0, 70),
                new OptimizationConstraintModel ("TS_cooler107", OptimizationObjectiveType.Upper, 3672.0, Units.cGy, 0.0, 80)
            };
        }

        [TestMethod()]
        public void ScaleHeaterCoolerOptConstraintsTest()
        {
            List<OptimizationConstraintModel> dummyList = SetupDummyOptObjList();
            double planDose = 3600.0;
            double sumDose = 5400.0;
            List<OptimizationConstraintModel> expected = new List<OptimizationConstraintModel>
            {
                new OptimizationConstraintModel("TS_cooler120", OptimizationObjectiveType.Upper, 2592.0, Units.cGy,0.0, 80),
                new OptimizationConstraintModel("TS_cooler110", OptimizationObjectiveType.Upper, 2592.0, Units.cGy,0.0, 80),
                new OptimizationConstraintModel("TS_cooler105", OptimizationObjectiveType.Upper, 2424.0, Units.cGy,0.0, 70),
                new OptimizationConstraintModel("TS_cooler107", OptimizationObjectiveType.Upper, 2448.0, Units.cGy,0.0, 80)
            };
            List<OptimizationConstraintModel> result = OptimizationLoopHelper.ScaleHeaterCoolerOptConstraints(planDose, sumDose, dummyList);
            CollectionAssert.AreEqual(expected, result);
        }

        [TestMethod()]
        public void IncreaseOptConstraintPrioritiesForFinalOptTest()
        {
            List<OptimizationConstraintModel> dummyList = SetupDummyOptObjList();
            dummyList.Add(new OptimizationConstraintModel("TS_PTV_CSI", OptimizationObjectiveType.Lower, 3600.0, Units.cGy, 0.0, 120));
            List<OptimizationConstraintModel> expected = new List<OptimizationConstraintModel>
            {
                new OptimizationConstraintModel("TS_cooler120", OptimizationObjectiveType.Upper, 3810.24, Units.cGy,0.0, 108),
                new OptimizationConstraintModel("TS_cooler110", OptimizationObjectiveType.Upper, 3810.24, Units.cGy,0.0, 108),
                new OptimizationConstraintModel("TS_cooler105", OptimizationObjectiveType.Upper, 3563.28, Units.cGy,0.0, 108),
                new OptimizationConstraintModel("TS_cooler107", OptimizationObjectiveType.Upper, 3598.56, Units.cGy,0.0, 108),
                new OptimizationConstraintModel("TS_PTV_CSI", OptimizationObjectiveType.Lower, 3600.0, Units.cGy,0.0, 120)
            };
            List<OptimizationConstraintModel> result = OptimizationLoopHelper.IncreaseOptConstraintPrioritiesForFinalOpt(dummyList);
            int count = 0;
            foreach (OptimizationConstraintModel itr in result)
            {
                OptimizationConstraintModel exp = expected.ElementAtOrDefault(count++);
                //tolerance of 0.01 cGy (for some reason, just mock says the result and expected are different, but I can't see any difference in the output. Must be rounding)
                Assert.AreEqual(itr.QueryDose, exp.QueryDose, 0.01);
                Assert.AreEqual(itr.Priority, exp.Priority);
                Console.WriteLine($"{itr.StructureId}, {itr.ConstraintType}, {itr.QueryDose}, {itr.QueryVolume}, {itr.Priority} | {exp.StructureId}, {exp.ConstraintType}, {exp.QueryDose}, {exp.QueryVolume}, {exp.Priority} ");
            }
        }

        [TestMethod()]
        public void GetNormaliztionVolumeIdForPlanTest()
        {
            string planId = "testPlan4";
            Dictionary<string, string> testNormVolumes = new Dictionary<string, string>
            {
                { "testPlan1","testVol1" },
                { "testPlan2","testVol2" },
                { "testPlan3","testVol3" },
                { "testPlan4","testVol4" },
            };
            string expected = "testVol4";
            Assert.AreEqual(expected, OptimizationLoopHelper.GetNormaliztionVolumeIdForPlan(planId, testNormVolumes));
        }
    }
}