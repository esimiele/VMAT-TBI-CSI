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

        public List<OptimizationConstraint> SetupDummyInitialOptObjList()
        {
            //rx = 3600
            return new List<OptimizationConstraint>
            {
                new OptimizationConstraint("PTV_CSI", OptimizationObjectiveType.Lower, 3600, Units.cGy, 0.0, 100),
                new OptimizationConstraint("PTV_CSI", OptimizationObjectiveType.Upper, 3672, Units.cGy, 0.0, 100),
                new OptimizationConstraint("Brainstem", OptimizationObjectiveType.Upper, 3650, Units.cGy, 0.0, 80),
                new OptimizationConstraint ("Brainstem_PRV", OptimizationObjectiveType.Upper, 3650, Units.cGy, 0.0, 60),
                new OptimizationConstraint ("OpticChiasm", OptimizationObjectiveType.Upper, 3420, Units.cGy, 0.0, 80),
                new OptimizationConstraint ("OpticChiasm_PRV", OptimizationObjectiveType.Upper, 3420, Units.cGy, 0.0, 60),
                new OptimizationConstraint ("TS_cooler107", OptimizationObjectiveType.Upper, 3672.0, Units.cGy, 0.0, 80)
            };
        }

        public List<OptimizationConstraint> SetupDummyBoostOptObjList()
        {
            //rx = 3600
            return new List<OptimizationConstraint>
            {
                new OptimizationConstraint ("PTV_Boost", OptimizationObjectiveType.Lower, 1800, Units.cGy, 0.0, 100),
                new OptimizationConstraint ("PTV_Boost", OptimizationObjectiveType.Upper, 1850, Units.cGy, 0.0, 100),
                new OptimizationConstraint ("Brainstem", OptimizationObjectiveType.Upper, 1760, Units.cGy, 0.0, 80),
                new OptimizationConstraint ("Brainstem_PRV", OptimizationObjectiveType.Upper, 1760, Units.cGy, 0.0, 60),
                new OptimizationConstraint ("OpticChiasm", OptimizationObjectiveType.Upper, 1650, Units.cGy, 0.0, 80),
                new OptimizationConstraint ("OpticChiasm_PRV", OptimizationObjectiveType.Upper, 1650, Units.cGy, 0.0, 60),
                new OptimizationConstraint ("TS_cooler107", OptimizationObjectiveType.Upper, 1836, Units.cGy, 0.0, 80)
            };
        }

        public CSIAutoPlanTemplate ConstructTestCSIAutoPlanTemplate()
        {
            CSIAutoPlanTemplate template = new CSIAutoPlanTemplate("test");
            template.InitialRxNumberOfFractions = 20;
            template.InitialRxDosePerFx = 180.0;
            template.BoostRxNumberOfFractions = 10;
            template.BoostRxDosePerFx = 180.0;
            template.InitialOptimizationConstraints = new List<OptimizationConstraint>(SetupDummyInitialOptObjList());
            template.BoostOptimizationConstraints = new List<OptimizationConstraint>(SetupDummyBoostOptObjList());
            return template;
        }

        public TBIAutoPlanTemplate ConstructTestTBIAutoPlanTemplate()
        {
            TBIAutoPlanTemplate template = new TBIAutoPlanTemplate("test");
            template.InitialRxNumberOfFractions = 20;
            template.InitialRxDosePerFx = 180.0;
            template.InitialOptimizationConstraints = new List<OptimizationConstraint>(SetupDummyInitialOptObjList());
            return template;
        }

        [TestMethod()]
        public void CreateOptimizationConstraintListCSITestWithPlanTargets()
        {
            Dictionary<string, string> testPlanTargets = CreateDummyCSIPlanTargetsList();
            CSIAutoPlanTemplate testTemplate = ConstructTestCSIAutoPlanTemplate();
            List<PlanOptimizationSetup> expected = new List<PlanOptimizationSetup>
            {
                new PlanOptimizationSetup("initial", SetupDummyInitialOptObjList()),
                new PlanOptimizationSetup("boost", SetupDummyBoostOptObjList())
            };

            List<PlanOptimizationSetup> result = OptimizationSetupHelper.CreateOptimizationConstraintList(testTemplate, testPlanTargets);

            //CollectionAssert.AreEqual(expected, result);
            int count = 0;
            foreach (PlanOptimizationSetup itr in result)
            {
                Assert.AreEqual(itr.PlanId, expected.ElementAt(count).PlanId);
                CollectionAssert.AreEqual(itr.OptimizationConstraints, expected.ElementAt(count).OptimizationConstraints);
                count++;
            }

            expected = new List<PlanOptimizationSetup>
            {
                new PlanOptimizationSetup("initial", SetupDummyInitialOptObjList()),
                new PlanOptimizationSetup("boost", SetupDummyBoostOptObjList())
            };
        }

        [TestMethod()]
        public void CreateOptimizationConstraintListCSITestNoPlanTargets()
        {
            CSIAutoPlanTemplate testTemplate = ConstructTestCSIAutoPlanTemplate();
            List<PlanOptimizationSetup> expected = new List<PlanOptimizationSetup>
            {
                new PlanOptimizationSetup("CSI-init", SetupDummyInitialOptObjList()),
                new PlanOptimizationSetup("CSI-bst", SetupDummyBoostOptObjList())
            };

            List<PlanOptimizationSetup> result = OptimizationSetupHelper.CreateOptimizationConstraintList(testTemplate, new Dictionary<string, string> { });

            //CollectionAssert.AreEqual(expected, result);
            int count = 0;
            foreach (PlanOptimizationSetup itr in result)
            {
                Assert.AreEqual(itr.PlanId, expected.ElementAt(count).PlanId);
                CollectionAssert.AreEqual(itr.OptimizationConstraints, expected.ElementAt(count).OptimizationConstraints);
                count++;
            }
        }

        [TestMethod()]
        public void CreateOptimizationConstraintListTBITestNoPlanTargets()
        {
            TBIAutoPlanTemplate testTemplate = ConstructTestTBIAutoPlanTemplate();
            List<PlanOptimizationSetup> expected = new List<PlanOptimizationSetup>
            {
                new PlanOptimizationSetup("VMAT-TBI", SetupDummyInitialOptObjList()),
            };

            List<PlanOptimizationSetup> result = OptimizationSetupHelper.CreateOptimizationConstraintList(testTemplate, new Dictionary<string, string> { });

            //CollectionAssert.AreEqual(expected, result);
            int count = 0;
            foreach (PlanOptimizationSetup itr in result)
            {
                Assert.AreEqual(itr.PlanId, expected.ElementAt(count).PlanId);
                CollectionAssert.AreEqual(itr.OptimizationConstraints, expected.ElementAt(count).OptimizationConstraints);
                count++;
            }
        }
    }
}