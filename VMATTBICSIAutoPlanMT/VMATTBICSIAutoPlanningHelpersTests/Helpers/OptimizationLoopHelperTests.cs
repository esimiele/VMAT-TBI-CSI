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
using VMATTBICSIAutoPlanningHelpersTests.EqualityComparerClasses;

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    [TestClass()]
    public class OptimizationLoopHelperTests
    {
        [TestMethod()]
        public void GetOtherPlansWithSameSSWithCalculatedDoseTest()
        {
            Course c1 = Mock.Create<Course>();
            Course c2 = Mock.Create<Course>();
            Course c3 = Mock.Create<Course>();

            Mock.Arrange(() => c1.Id).Returns("c1");
            Mock.Arrange(() => c2.Id).Returns("c2");
            Mock.Arrange(() => c3.Id).Returns("c3");

            ExternalPlanSetup p1 = Mock.Create<ExternalPlanSetup>();
            ExternalPlanSetup p2 = Mock.Create<ExternalPlanSetup>();
            ExternalPlanSetup p3 = Mock.Create<ExternalPlanSetup>();
            ExternalPlanSetup p4 = Mock.Create<ExternalPlanSetup>();
            ExternalPlanSetup p5 = Mock.Create<ExternalPlanSetup>();
            ExternalPlanSetup p6 = Mock.Create<ExternalPlanSetup>();

            StructureSet ss = Mock.Create<StructureSet>();
            Mock.Arrange(() => ss.UID).Returns("1");
            StructureSet ss1 = Mock.Create<StructureSet>();
            Mock.Arrange(() => ss.UID).Returns("2");
            StructureSet ss2 = Mock.Create<StructureSet>();
            Mock.Arrange(() => ss.UID).Returns("3");
            StructureSet ss3 = Mock.Create<StructureSet>();
            Mock.Arrange(() => ss.UID).Returns("4");

            Mock.Arrange(() => p1.StructureSet).Returns(ss);
            Mock.Arrange(() => p2.StructureSet).Returns(ss1);
            Mock.Arrange(() => p3.StructureSet).Returns(ss);
            Mock.Arrange(() => p4.StructureSet).Returns(ss3);
            Mock.Arrange(() => p5.StructureSet).Returns(ss2);
            Mock.Arrange(() => p6.StructureSet).Returns(ss1);

            Beam b = Mock.Create<Beam>();

            Mock.Arrange(() => p1.Beams).Returns(new List<Beam> { b });
            Mock.Arrange(() => p2.Beams).Returns(new List<Beam> { b });
            Mock.Arrange(() => p3.Beams).Returns(new List<Beam> { b });
            Mock.Arrange(() => p4.Beams).Returns(new List<Beam> { b });
            Mock.Arrange(() => p5.Beams).Returns(new List<Beam> { b });
            Mock.Arrange(() => p6.Beams).Returns(new List<Beam> { b });

            Mock.Arrange(() => p1.Id).Returns("1");
            Mock.Arrange(() => p2.Id).Returns("2");
            Mock.Arrange(() => p3.Id).Returns("3");
            Mock.Arrange(() => p4.Id).Returns("4");
            Mock.Arrange(() => p5.Id).Returns("5");
            Mock.Arrange(() => p6.Id).Returns("6");

            Mock.Arrange(() => p1.Course).Returns(c1);
            Mock.Arrange(() => p2.Course).Returns(c1);
            Mock.Arrange(() => p3.Course).Returns(c2);
            Mock.Arrange(() => p4.Course).Returns(c2);
            Mock.Arrange(() => p5.Course).Returns(c3);
            Mock.Arrange(() => p6.Course).Returns(c3);

            Mock.Arrange(() => p1.IsDoseValid).Returns(true);
            Mock.Arrange(() => p2.IsDoseValid).Returns(false);
            Mock.Arrange(() => p3.IsDoseValid).Returns(true);
            Mock.Arrange(() => p4.IsDoseValid).Returns(true);
            Mock.Arrange(() => p5.IsDoseValid).Returns(false);
            Mock.Arrange(() => p6.IsDoseValid).Returns(false);

            Mock.Arrange(() => c1.ExternalPlanSetups).Returns(new List<ExternalPlanSetup> { p1, p2 });
            Mock.Arrange(() => c2.ExternalPlanSetups).Returns(new List<ExternalPlanSetup> { p3, p4 });
            Mock.Arrange(() => c3.ExternalPlanSetups).Returns(new List<ExternalPlanSetup> { p5, p6 });
            List<Course> courses = new List<Course> { c1, c2, c3};

            List<ExternalPlanSetup> expected = new List<ExternalPlanSetup>{p1, p3};

            (IEnumerable<ExternalPlanSetup>, StringBuilder) result = OptimizationLoopHelper.GetOtherPlansWithSameSSWithCalculatedDose(courses, ss);
            Console.WriteLine(result.Item2.ToString());
            Console.WriteLine($"{expected.Count} | {result.Item1.Count()}");
            CollectionAssert.AreEqual(expected, result.Item1.ToList());
        }

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

            OptimizationConstraintComparer comparer = new OptimizationConstraintComparer();
            for (int i = 0; i < expected.Count; i++)
            {
                Console.WriteLine($"{comparer.Print(expected.ElementAt(i))} | {comparer.Print(result.ElementAt(i))}");
                Assert.IsTrue(comparer.Equals(expected.ElementAt(i), result.ElementAt(i)));
            }
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