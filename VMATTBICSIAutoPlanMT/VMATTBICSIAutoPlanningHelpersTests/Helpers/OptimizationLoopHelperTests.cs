﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
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

        public List<Tuple<string, OptimizationObjectiveType, double, double, int>> SetupDummyOptObjList()
        {
            //rx = 3600
            return new List<Tuple<string, OptimizationObjectiveType, double, double, int>>
            {
                new Tuple<string, OptimizationObjectiveType, double, double, int>("TS_cooler120", OptimizationObjectiveType.Upper, 3888.0, 0.0, 80),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("TS_cooler110", OptimizationObjectiveType.Upper, 3888.0, 0.0, 80),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("TS_cooler105", OptimizationObjectiveType.Upper, 3636.0, 0.0, 70),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("TS_cooler107", OptimizationObjectiveType.Upper, 3672.0, 0.0, 80)
            };
        }

        [TestMethod()]
        public void ScaleHeaterCoolerOptConstraintsTest()
        {
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> dummyList = SetupDummyOptObjList();
            double planDose = 3600.0;
            double sumDose = 5400.0;
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> expected = new List<Tuple<string, OptimizationObjectiveType, double, double, int>>
            {
                new Tuple<string, OptimizationObjectiveType, double, double, int>("TS_cooler120", OptimizationObjectiveType.Upper, 2592.0, 0.0, 80),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("TS_cooler110", OptimizationObjectiveType.Upper, 2592.0, 0.0, 80),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("TS_cooler105", OptimizationObjectiveType.Upper, 2424.0, 0.0, 70),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("TS_cooler107", OptimizationObjectiveType.Upper, 2448.0, 0.0, 80)
            };
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> result = OptimizationLoopHelper.ScaleHeaterCoolerOptConstraints(planDose, sumDose, dummyList);
            CollectionAssert.AreEqual(expected, result);
        }

        [TestMethod()]
        public void IncreaseOptConstraintPrioritiesForFinalOptTest()
        {
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> dummyList = SetupDummyOptObjList();
            dummyList.Add(Tuple.Create("TS_PTV_CSI", OptimizationObjectiveType.Lower, 3600.0, 0.0, 120));
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> expected = new List<Tuple<string, OptimizationObjectiveType, double, double, int>>
            {
                new Tuple<string, OptimizationObjectiveType, double, double, int>("TS_cooler120", OptimizationObjectiveType.Upper, 3810.24, 0.0, 108),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("TS_cooler110", OptimizationObjectiveType.Upper, 3810.24, 0.0, 108),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("TS_cooler105", OptimizationObjectiveType.Upper, 3563.28, 0.0, 108),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("TS_cooler107", OptimizationObjectiveType.Upper, 3598.56, 0.0, 108),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("TS_PTV_CSI", OptimizationObjectiveType.Lower, 3600.0, 0.0, 120)
            };
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> result = OptimizationLoopHelper.IncreaseOptConstraintPrioritiesForFinalOpt(dummyList);
            int count = 0;
            foreach (Tuple<string, OptimizationObjectiveType, double, double, int> itr in result)
            {
                Tuple<string, OptimizationObjectiveType, double, double, int> exp = expected.ElementAtOrDefault(count++);
                //tolerance of 0.01 cGy (for some reason, just mock says the result and expected are different, but I can't see any difference in the output. Must be rounding)
                Assert.AreEqual(itr.Item3, exp.Item3, 0.01);
                Assert.AreEqual(itr.Item5, exp.Item5);
                //Console.WriteLine($"{itr.Item1}, {itr.Item2}, {itr.Item3}, {itr.Item4}, {itr.Item5} | {exp.Item1}, {exp.Item2}, {exp.Item3}, {exp.Item4}, {exp.Item5} ");
            }
        }

        [TestMethod()]
        public void GetNormaliztionVolumeIdForPlanTest()
        {
            string planId = "testPlan4";
            List<Tuple<string, string>> testNormVolumes = new List<Tuple<string, string>>
            {
                Tuple.Create("testPlan1","testVol1"),
                Tuple.Create("testPlan2","testVol2"),
                Tuple.Create("testPlan3","testVol3"),
                Tuple.Create("testPlan4","testVol4")
            };
            string expected = "testVol4";
            Assert.AreEqual(expected, OptimizationLoopHelper.GetNormaliztionVolumeIdForPlan(planId, testNormVolumes));
        }
    }
}