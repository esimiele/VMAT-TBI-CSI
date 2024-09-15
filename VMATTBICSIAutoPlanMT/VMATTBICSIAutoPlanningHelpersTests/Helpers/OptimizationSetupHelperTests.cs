using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.PlanTemplateClasses;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Models;
using Telerik.JustMock;
using VMS.TPS.Common.Model.API;
using VMATTBICSIAutoPlanningHelpersTests.EqualityComparerClasses;
using System.Collections;

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    [TestClass()]
    public class OptimizationSetupHelperTests
    {
        //structure id, Rx dose, plan Id
        public List<Tuple<string, string, int, DoseValue, double>> CreateDummyPrescription()
        {
            //plan ID, target Id, numFx, dosePerFx, cumulative dose
            return new List<Tuple<string, string, int, DoseValue, double>>
            {
                Tuple.Create("CSI-init", "PTV_CSIMid", 20, new DoseValue(160.0, DoseValue.DoseUnit.cGy), 3200.0),
                Tuple.Create("CSI-init", "PTV_CSI", 20, new DoseValue(180.0, DoseValue.DoseUnit.cGy), 3600.0),
                Tuple.Create("CSI-bst", "PTV_Boost", 10, new DoseValue(180.0, DoseValue.DoseUnit.cGy), 5400.0),
            };
        }

        //plan id, highest Rx target for this plan id
        public Dictionary<string, string> CreateDummyCSIPlanTargetsList()
        {
            return new Dictionary<string, string>
            {
                {"initial", "PTV_CSI" },
                { "boost", "PTV_Boost" },
            };
        }

        public List<OptimizationConstraintModel> SetupDummyInitialOptObjList()
        {
            //rx = 3600
            return new List<OptimizationConstraintModel>
            {
                new OptimizationConstraintModel("PTV_CSI", OptimizationObjectiveType.Lower, 3600, Units.cGy, 0.0, 100),
                new OptimizationConstraintModel("PTV_CSI", OptimizationObjectiveType.Upper, 3672, Units.cGy, 0.0, 100),
                new OptimizationConstraintModel("Brainstem", OptimizationObjectiveType.Upper, 3650, Units.cGy, 0.0, 80),
                new OptimizationConstraintModel ("Brainstem_PRV", OptimizationObjectiveType.Upper, 3650, Units.cGy, 0.0, 60),
                new OptimizationConstraintModel ("OpticChiasm", OptimizationObjectiveType.Upper, 3420, Units.cGy, 0.0, 80),
                new OptimizationConstraintModel ("OpticChiasm_PRV", OptimizationObjectiveType.Upper, 3420, Units.cGy, 0.0, 60),
                new OptimizationConstraintModel ("TS_cooler107", OptimizationObjectiveType.Upper, 3672.0, Units.cGy, 0.0, 80)
            };
        }

        public List<OptimizationConstraintModel> SetupDummyBoostOptObjList()
        {
            //rx = 3600
            return new List<OptimizationConstraintModel>
            {
                new OptimizationConstraintModel ("PTV_Boost", OptimizationObjectiveType.Lower, 1800, Units.cGy, 0.0, 100),
                new OptimizationConstraintModel ("PTV_Boost", OptimizationObjectiveType.Upper, 1850, Units.cGy, 0.0, 100),
                new OptimizationConstraintModel ("Brainstem", OptimizationObjectiveType.Upper, 1760, Units.cGy, 0.0, 80),
                new OptimizationConstraintModel ("Brainstem_PRV", OptimizationObjectiveType.Upper, 1760, Units.cGy, 0.0, 60),
                new OptimizationConstraintModel ("OpticChiasm", OptimizationObjectiveType.Upper, 1650, Units.cGy, 0.0, 80),
                new OptimizationConstraintModel ("OpticChiasm_PRV", OptimizationObjectiveType.Upper, 1650, Units.cGy, 0.0, 60),
                new OptimizationConstraintModel ("TS_cooler107", OptimizationObjectiveType.Upper, 1836, Units.cGy, 0.0, 80)
            };
        }

        public CSIAutoPlanTemplate ConstructTestCSIAutoPlanTemplate()
        {
            CSIAutoPlanTemplate template = new CSIAutoPlanTemplate("test");
            template.InitialRxNumberOfFractions = 20;
            template.InitialRxDosePerFx = 180.0;
            template.BoostRxNumberOfFractions = 10;
            template.BoostRxDosePerFx = 180.0;
            template.InitialOptimizationConstraints = new List<OptimizationConstraintModel>(SetupDummyInitialOptObjList());
            template.BoostOptimizationConstraints = new List<OptimizationConstraintModel>(SetupDummyBoostOptObjList());
            return template;
        }

        public TBIAutoPlanTemplate ConstructTestTBIAutoPlanTemplate()
        {
            TBIAutoPlanTemplate template = new TBIAutoPlanTemplate("test");
            template.InitialRxNumberOfFractions = 20;
            template.InitialRxDosePerFx = 180.0;
            template.InitialOptimizationConstraints = new List<OptimizationConstraintModel>(SetupDummyInitialOptObjList());
            return template;
        }

        [TestMethod()]
        public void CreateOptimizationConstraintListCSITestWithPlanTargets()
        {
            Dictionary<string, string> testPlanTargets = CreateDummyCSIPlanTargetsList();
            CSIAutoPlanTemplate testTemplate = ConstructTestCSIAutoPlanTemplate();
            List<PlanOptimizationSetupModel> expected = new List<PlanOptimizationSetupModel>
            {
                new PlanOptimizationSetupModel("initial", SetupDummyInitialOptObjList()),
                new PlanOptimizationSetupModel("boost", SetupDummyBoostOptObjList())
            };

            List<PlanOptimizationSetupModel> result = OptimizationSetupHelper.CreateOptimizationConstraintList(testTemplate, testPlanTargets);
            OptimizationConstraintComparer comparer = new OptimizationConstraintComparer();

            //CollectionAssert.AreEqual(expected, result);
            int count = 0;
            foreach (PlanOptimizationSetupModel itr in result)
            {
                Assert.AreEqual(itr.PlanId, expected.ElementAt(count).PlanId);
                Assert.AreEqual(itr.OptimizationConstraints.Count(), expected.ElementAt(count).OptimizationConstraints.Count());
                for (int i = 0; i < itr.OptimizationConstraints.Count(); i++)
                {
                    Assert.IsTrue(comparer.Equals(itr.OptimizationConstraints.ElementAt(i), expected.ElementAt(count).OptimizationConstraints.ElementAt(i)));
                }
                count++;
            }
        }

        [TestMethod()]
        public void CreateOptimizationConstraintListCSITestNoPlanTargets()
        {
            CSIAutoPlanTemplate testTemplate = ConstructTestCSIAutoPlanTemplate();
            List<PlanOptimizationSetupModel> expected = new List<PlanOptimizationSetupModel>
            {
                new PlanOptimizationSetupModel("CSI-init", SetupDummyInitialOptObjList()),
                new PlanOptimizationSetupModel("CSI-bst", SetupDummyBoostOptObjList())
            };

            List<PlanOptimizationSetupModel> result = OptimizationSetupHelper.CreateOptimizationConstraintList(testTemplate, new Dictionary<string, string> { });
            OptimizationConstraintComparer comparer = new OptimizationConstraintComparer();

            int count = 0;
            foreach (PlanOptimizationSetupModel itr in result)
            {
                Assert.AreEqual(itr.PlanId, expected.ElementAt(count).PlanId);
                Assert.AreEqual(itr.OptimizationConstraints.Count(), expected.ElementAt(count).OptimizationConstraints.Count());
                for(int i = 0; i < itr.OptimizationConstraints.Count(); i++)
                {
                    Assert.IsTrue(comparer.Equals(itr.OptimizationConstraints.ElementAt(i), expected.ElementAt(count).OptimizationConstraints.ElementAt(i)));
                }
                count++;
            }
        }

        [TestMethod()]
        public void CreateOptimizationConstraintListTBITestNoPlanTargets()
        {
            TBIAutoPlanTemplate testTemplate = ConstructTestTBIAutoPlanTemplate();
            List<PlanOptimizationSetupModel> expected = new List<PlanOptimizationSetupModel>
            {
                new PlanOptimizationSetupModel("VMAT-TBI", SetupDummyInitialOptObjList()),
            };

            List<PlanOptimizationSetupModel> result = OptimizationSetupHelper.CreateOptimizationConstraintList(testTemplate, new Dictionary<string, string> { });
            OptimizationConstraintComparer comparer = new OptimizationConstraintComparer();

            //CollectionAssert.AreEqual(expected, result);
            int count = 0;
            foreach (PlanOptimizationSetupModel itr in result)
            {
                Assert.AreEqual(itr.PlanId, expected.ElementAt(count).PlanId);
                Assert.AreEqual(itr.OptimizationConstraints.Count(), expected.ElementAt(count).OptimizationConstraints.Count());
                for (int i = 0; i < itr.OptimizationConstraints.Count(); i++)
                {
                    Assert.IsTrue(comparer.Equals(itr.OptimizationConstraints.ElementAt(i), expected.ElementAt(count).OptimizationConstraints.ElementAt(i)));
                }
                count++;
            }
        }

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

        public List<OptimizationConstraintModel> BuildExpectedObjectiveList()
        {
            return new List<OptimizationConstraintModel>
            {
                new OptimizationConstraintModel("0", OptimizationObjectiveType.Upper, 0.0, Units.cGy, 0.0, 0),
                new OptimizationConstraintModel("1", OptimizationObjectiveType.Lower, 10.0, Units.cGy, 100.0, 1),
                new OptimizationConstraintModel("2", OptimizationObjectiveType.Upper, 20.0, Units.cGy, 200.0, 2),
                new OptimizationConstraintModel("3", OptimizationObjectiveType.Lower, 30.0, Units.cGy, 300.0, 3),
                new OptimizationConstraintModel("4", OptimizationObjectiveType.Upper, 40.0, Units.cGy, 400.0, 4),
                new OptimizationConstraintModel("0", OptimizationObjectiveType.Mean, 0.0, Units.cGy, 0.0, 0),
                new OptimizationConstraintModel("1", OptimizationObjectiveType.Mean, 10.0, Units.cGy, 0.0, 1),
                new OptimizationConstraintModel("2", OptimizationObjectiveType.Mean, 20.0, Units.cGy, 0.0, 2),
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

            List<OptimizationConstraintModel> expected = BuildExpectedObjectiveList();

            List<OptimizationConstraintModel> result = OptimizationSetupHelper.ReadConstraintsFromPlan(plan);
            OptimizationConstraintComparer comparer = new OptimizationConstraintComparer();
            int count = 0;
            foreach (OptimizationConstraintModel itr in result)
            {
                Console.WriteLine($"{itr.StructureId}, {itr.ConstraintType}, {itr.QueryDose}, {itr.QueryVolume}, {itr.Priority}");
                Assert.IsTrue(comparer.Equals(itr, expected.ElementAt(count)));
                count++;
            }
        }

        [TestMethod()]
        public void RescalePlanObjectivesToNewRxTest()
        {
            List<OptimizationConstraintModel> initialList = BuildExpectedObjectiveList();
            double oldRx = 800.0;
            double newRx = 1200.0;
            List<OptimizationConstraintModel> expected = new List<OptimizationConstraintModel>
            {
                new OptimizationConstraintModel("0", OptimizationObjectiveType.Upper, 0.0, Units.cGy, 0.0, 0),
                new OptimizationConstraintModel("1", OptimizationObjectiveType.Lower, 15.0, Units.cGy,100.0, 1),
                new OptimizationConstraintModel("2", OptimizationObjectiveType.Upper, 30.0, Units.cGy,200.0, 2),
                new OptimizationConstraintModel("3", OptimizationObjectiveType.Lower, 45.0, Units.cGy,300.0, 3),
                new OptimizationConstraintModel("4", OptimizationObjectiveType.Upper, 60.0, Units.cGy,400.0, 4),
                new OptimizationConstraintModel("0", OptimizationObjectiveType.Mean, 0.0, Units.cGy,0.0, 0),
                new OptimizationConstraintModel("1", OptimizationObjectiveType.Mean, 15.0, Units.cGy,0.0, 1),
                new OptimizationConstraintModel("2", OptimizationObjectiveType.Mean, 30.0, Units.cGy,0.0, 2),
            };

            List<OptimizationConstraintModel> result = OptimizationSetupHelper.RescalePlanObjectivesToNewRx(initialList, oldRx, newRx);
            OptimizationConstraintComparer comparer = new OptimizationConstraintComparer();
            int count = 0;
            foreach (OptimizationConstraintModel itr in result)
            {
                Console.WriteLine($"{itr.StructureId}, {itr.ConstraintType}, {itr.QueryDose}, {itr.QueryVolume}, {itr.Priority}");
                Assert.IsTrue(comparer.Equals(itr, expected.ElementAt(count)));
                count++;
            }
        }
    }
}