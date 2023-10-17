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
        public List<Tuple<string,string>> CreateDummyCSIPlanTargetsList()
        {
            return new List<Tuple<string, string>>
            {
                Tuple.Create("initial", "PTV_CSI"),
                Tuple.Create("boost", "PTV_Boost"),
            };
        }

        public List<Tuple<string, OptimizationObjectiveType, double, double, int>> SetupDummyInitialOptObjList()
        {
            //rx = 3600
            return new List<Tuple<string, OptimizationObjectiveType, double, double, int>>
            {
                new Tuple<string, OptimizationObjectiveType, double, double, int>("PTV_CSI", OptimizationObjectiveType.Lower, 3600, 0.0, 100),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("PTV_CSI", OptimizationObjectiveType.Upper, 3672, 0.0, 100),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("Brainstem", OptimizationObjectiveType.Upper, 3650, 0.0, 80),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("Brainstem_PRV", OptimizationObjectiveType.Upper, 3650, 0.0, 60),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("OpticChiasm", OptimizationObjectiveType.Upper, 3420, 0.0, 80),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("OpticChiasm_PRV", OptimizationObjectiveType.Upper, 3420, 0.0, 60),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("TS_cooler107", OptimizationObjectiveType.Upper, 3672.0, 0.0, 80)
            };
        }

        public List<Tuple<string, OptimizationObjectiveType, double, double, int>> SetupDummyBoostOptObjList()
        {
            //rx = 3600
            return new List<Tuple<string, OptimizationObjectiveType, double, double, int>>
            {
                new Tuple<string, OptimizationObjectiveType, double, double, int>("PTV_Boost", OptimizationObjectiveType.Lower, 1800, 0.0, 100),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("PTV_Boost", OptimizationObjectiveType.Upper, 1850, 0.0, 100),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("Brainstem", OptimizationObjectiveType.Upper, 1760, 0.0, 80),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("Brainstem_PRV", OptimizationObjectiveType.Upper, 1760, 0.0, 60),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("OpticChiasm", OptimizationObjectiveType.Upper, 1650, 0.0, 80),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("OpticChiasm_PRV", OptimizationObjectiveType.Upper, 1650, 0.0, 60),
                new Tuple<string, OptimizationObjectiveType, double, double, int>("TS_cooler107", OptimizationObjectiveType.Upper, 1836, 0.0, 80)
            };
        }

        public CSIAutoPlanTemplate ConstructTestCSIAutoPlanTemplate()
        {
            CSIAutoPlanTemplate template = new CSIAutoPlanTemplate("test");
            template.SetInitialRxNumFx(20);
            template.SetInitRxDosePerFx(180.0);
            template.SetBoostRxNumFx(10);
            template.SetBoostRxDosePerFx(180.0);
            template.SetInitOptimizationConstraints(SetupDummyInitialOptObjList());
            template.SetBoostOptimizationConstraints(SetupDummyBoostOptObjList());
            return template;
        }

        public TBIAutoPlanTemplate ConstructTestTBIAutoPlanTemplate()
        {
            TBIAutoPlanTemplate template = new TBIAutoPlanTemplate("test");
            template.SetInitialRxNumFx(20);
            template.SetInitRxDosePerFx(180.0);
            template.SetInitOptimizationConstraints(SetupDummyInitialOptObjList());
            return template;
        }

        [TestMethod()]
        public void CreateOptimizationConstraintListCSITestWithPlanTargets()
        {
            List<Tuple<string, string>> testPlanTargets = CreateDummyCSIPlanTargetsList();
            CSIAutoPlanTemplate testTemplate = ConstructTestCSIAutoPlanTemplate();
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> expected = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>
            {
                Tuple.Create("initial", SetupDummyInitialOptObjList()),
                Tuple.Create("boost", SetupDummyBoostOptObjList())
            };

            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> result = OptimizationSetupHelper.CreateOptimizationConstraintList(testTemplate, testPlanTargets);

            //CollectionAssert.AreEqual(expected, result);
            int count = 0;
            foreach (Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>> itr in result)
            {
                Assert.AreEqual(itr.Item1, expected.ElementAt(count).Item1);
                CollectionAssert.AreEqual(itr.Item2, expected.ElementAt(count).Item2);
                count++;
            }

            expected = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>
            {
                Tuple.Create("initial", SetupDummyInitialOptObjList()),
                Tuple.Create("boost", SetupDummyBoostOptObjList())
            };
        }

        [TestMethod()]
        public void CreateOptimizationConstraintListCSITestNoPlanTargets()
        {
            CSIAutoPlanTemplate testTemplate = ConstructTestCSIAutoPlanTemplate();
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> expected = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>
            {
                Tuple.Create("CSI-init", SetupDummyInitialOptObjList()),
                Tuple.Create("CSI-bst", SetupDummyBoostOptObjList())
            };

            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> result = OptimizationSetupHelper.CreateOptimizationConstraintList(testTemplate, new List<Tuple<string, string>> { });

            //CollectionAssert.AreEqual(expected, result);
            int count = 0;
            foreach (Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>> itr in result)
            {
                Assert.AreEqual(itr.Item1, expected.ElementAt(count).Item1);
                CollectionAssert.AreEqual(itr.Item2, expected.ElementAt(count).Item2);
                count++;
            }
        }

        [TestMethod()]
        public void CreateOptimizationConstraintListTBITestNoPlanTargets()
        {
            TBIAutoPlanTemplate testTemplate = ConstructTestTBIAutoPlanTemplate();
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> expected = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>
            {
                Tuple.Create("VMAT-TBI", SetupDummyInitialOptObjList()),
            };

            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> result = OptimizationSetupHelper.CreateOptimizationConstraintList(testTemplate, new List<Tuple<string, string>> { });

            //CollectionAssert.AreEqual(expected, result);
            int count = 0;
            foreach (Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>> itr in result)
            {
                Assert.AreEqual(itr.Item1, expected.ElementAt(count).Item1);
                CollectionAssert.AreEqual(itr.Item2, expected.ElementAt(count).Item2);
                count++;
            }
        }
    }
}