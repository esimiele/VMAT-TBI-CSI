using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMATTBICSIAutoPlanningHelpers.UIHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using Telerik.JustMock;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.Enums;
using Telerik.JustMock.AutoMock.Ninject.Planning;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers.Tests
{
    [TestClass()]
    public class OptimizationSetupUIHelperTests
    {
        public IEnumerable<OptimizationObjective> GenerateTestPointObjectives(int numPointObj)
        {
            List<OptimizationObjective> testPointObj = new List<OptimizationObjective>();
            for (int i = 0; i < numPointObj; i++)
            {
                OptimizationPointObjective pt = Mock.Create<OptimizationPointObjective>();
                Mock.Arrange(() => pt.StructureId).Returns(i.ToString());
                Mock.Arrange(() => pt.Operator).Returns(i % 2 == 0 ? OptimizationObjectiveOperator.Upper : OptimizationObjectiveOperator.Lower);
                Mock.Arrange(() => pt.Dose).Returns(new DoseValue(i * 10, DoseValue.DoseUnit.cGy));
                Mock.Arrange(() => pt.Volume).Returns(i * 100);
                Mock.Arrange(() => pt.Priority).Returns(i);
                testPointObj.Add(pt);
            }
            return testPointObj;
        }

        public IEnumerable<OptimizationObjective> GenerateTestMeanObjectives(int numMeanObj)
        {
            List<OptimizationObjective> testMeanObj = new List<OptimizationObjective>();
            for (int i = 0; i < numMeanObj; i++)
            {
                OptimizationMeanDoseObjective pt = Mock.Create<OptimizationMeanDoseObjective>();
                Mock.Arrange(() => pt.StructureId).Returns(i.ToString());
                Mock.Arrange(() => pt.Dose).Returns(new DoseValue(i * 10, DoseValue.DoseUnit.cGy));
                Mock.Arrange(() => pt.Priority).Returns(i);
                testMeanObj.Add(pt);
            }
            return testMeanObj;
        }

        public List<Tuple<string, OptimizationObjectiveType, double, double, int>> BuildExpectedObjectiveList()
        {
            return new List<Tuple<string, OptimizationObjectiveType, double, double, int>>
            {
                Tuple.Create("0", OptimizationObjectiveType.Upper, 0.0, 0.0, 0),
                Tuple.Create("1", OptimizationObjectiveType.Lower, 10.0, 100.0, 1),
                Tuple.Create("2", OptimizationObjectiveType.Upper, 20.0, 200.0, 2),
                Tuple.Create("3", OptimizationObjectiveType.Lower, 30.0, 300.0, 3),
                Tuple.Create("4", OptimizationObjectiveType.Upper, 40.0, 400.0, 4),
                Tuple.Create("0", OptimizationObjectiveType.Mean, 0.0, 0.0, 0),
                Tuple.Create("1", OptimizationObjectiveType.Mean, 10.0, 0.0, 1),
                Tuple.Create("2", OptimizationObjectiveType.Mean, 20.0, 0.0, 2),
            };
        }

        [TestMethod()]
        public void ReadConstraintsFromPlanTest()
        {
            ExternalPlanSetup plan = Mock.Create<ExternalPlanSetup>();
            OptimizationSetup setup = Mock.Create<OptimizationSetup>();
            List<OptimizationObjective> testObj = new List<OptimizationObjective> { };
            testObj.AddRange(GenerateTestPointObjectives(5));
            testObj.AddRange(GenerateTestMeanObjectives(3));
            Mock.Arrange(() => setup.Objectives).Returns(testObj);
            Mock.Arrange(() => plan.OptimizationSetup).Returns(setup);

            List<Tuple<string, OptimizationObjectiveType, double, double, int>> expected = BuildExpectedObjectiveList();

            List<Tuple<string, OptimizationObjectiveType, double, double, int>> result = OptimizationSetupUIHelper.ReadConstraintsFromPlan(plan);
            foreach (Tuple<string, OptimizationObjectiveType, double, double, int> itr in result)
            {
                Console.WriteLine($"{itr.Item1}, {itr.Item2}, {itr.Item3}, {itr.Item4}, {itr.Item5}");
            }

            CollectionAssert.AreEqual(expected, result);
        }

        [TestMethod()]
        public void RescalePlanObjectivesToNewRxTest()
        {
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> initialList = BuildExpectedObjectiveList();
            double oldRx = 800.0;
            double newRx = 1200.0;
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> expected = new List<Tuple<string, OptimizationObjectiveType, double, double, int>>
            {
                Tuple.Create("0", OptimizationObjectiveType.Upper, 0.0, 0.0, 0),
                Tuple.Create("1", OptimizationObjectiveType.Lower, 15.0, 100.0, 1),
                Tuple.Create("2", OptimizationObjectiveType.Upper, 30.0, 200.0, 2),
                Tuple.Create("3", OptimizationObjectiveType.Lower, 45.0, 300.0, 3),
                Tuple.Create("4", OptimizationObjectiveType.Upper, 60.0, 400.0, 4),
                Tuple.Create("0", OptimizationObjectiveType.Mean, 0.0, 0.0, 0),
                Tuple.Create("1", OptimizationObjectiveType.Mean, 15.0, 0.0, 1),
                Tuple.Create("2", OptimizationObjectiveType.Mean, 30.0, 0.0, 2),
            };

            List<Tuple<string, OptimizationObjectiveType, double, double, int>> result = OptimizationSetupUIHelper.RescalePlanObjectivesToNewRx(initialList, oldRx, newRx);
            foreach (Tuple<string, OptimizationObjectiveType, double, double, int> itr in result)
            {
                Console.WriteLine($"{itr.Item1}, {itr.Item2}, {itr.Item3}, {itr.Item4}, {itr.Item5}");
            }

            CollectionAssert.AreEqual(expected, result);
        }
    }
}