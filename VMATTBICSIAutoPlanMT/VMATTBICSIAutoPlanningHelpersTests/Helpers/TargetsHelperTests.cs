using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    [TestClass()]
    public class TargetsHelperTests
    {
        public List<PrescriptionModel> CreateDummyPrescription()
        {
            //plan ID, target Id, numFx, dosePerFx, cumulative dose
            return new List<PrescriptionModel>
            {
                new PrescriptionModel("CSI-init", "PTV_CSIMid", 20, new DoseValue(160.0, DoseValue.DoseUnit.cGy), 3200.0),
                new PrescriptionModel("CSI-init", "PTV_CSI", 20, new DoseValue(180.0, DoseValue.DoseUnit.cGy), 3600.0),
                new PrescriptionModel("CSI-bst", "PTV_Boost", 10, new DoseValue(180.0, DoseValue.DoseUnit.cGy), 5400.0),
            };
        }

        [TestMethod()]
        public void GetHighestRxPlanTargetListTestRx()
        {
            List<PrescriptionModel> testRx = CreateDummyPrescription();
            Dictionary<string, string> expected = new Dictionary<string, string>
            {
                { "CSI-init", "PTV_CSI" },
                { "CSI-bst", "PTV_Boost" }
            };
            CollectionAssert.AreEqual(expected, TargetsHelper.GetHighestRxPlanTargetList(testRx));
        }

        [TestMethod()]
        public void GroupTargetsByPlanIdTest()
        {
            List<PlanTargetsModel> models = new List<PlanTargetsModel>();
            models.Add(new PlanTargetsModel("CSI-init", new TargetModel("2", 2400)));
            models.Add(new PlanTargetsModel("CSI-bst", new TargetModel("4", 4800)));
            models.Add(new PlanTargetsModel("CSI-init", new TargetModel("3", 3600)));
            models.Add(new PlanTargetsModel("CSI-bst", new TargetModel("5", 6000)));
            models.Add(new PlanTargetsModel("CSI-init", new TargetModel("1", 1200)));

            List<PlanTargetsModel> expected = new List<PlanTargetsModel>
        {
            new PlanTargetsModel("CSI-init", new List<TargetModel> {  new TargetModel("1", 1200), new TargetModel("2", 2400), new TargetModel("3", 3600)}),
            new PlanTargetsModel("CSI-bst", new List<TargetModel> {  new TargetModel("4", 4800), new TargetModel("5", 6000)})
        };
            List<PlanTargetsModel> result = TargetsHelper.GroupTargetsByPlanIdAndOrderByTargetRx(models);
            Console.WriteLine($"{expected.Count} | {result.Count}");
            Assert.AreEqual(expected.Count, result.Count);

            Console.WriteLine("expected");
            foreach (PlanTargetsModel itr in expected)
            {
                foreach (TargetModel tgt in itr.Targets)
                {
                    Console.WriteLine($"{itr.PlanId} | {tgt.TargetId} | {tgt.TargetRxDose}");
                }
            }
            Console.WriteLine("result");
            foreach (PlanTargetsModel itr in result)
            {
                foreach (TargetModel tgt in itr.Targets)
                {
                    Console.WriteLine($"{itr.PlanId} | {tgt.TargetId} | {tgt.TargetRxDose}");
                }
            }

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected.ElementAt(i).PlanId, result.ElementAt(i).PlanId);
                CollectionAssert.AreEqual(expected.ElementAt(i).Targets, result.ElementAt(i).Targets, new TargetModelComparer());
            }
        }

        [TestMethod()]
        public void GroupPrescriptionsByPlanIdTest()
        {
            List<PrescriptionModel> models = new List<PrescriptionModel>();
            models.Add(new PrescriptionModel("CSI-init", "2", 6, new DoseValue(400, DoseValue.DoseUnit.cGy), 2400));
            models.Add(new PrescriptionModel("CSI-bst", "4", 7, new DoseValue(200, DoseValue.DoseUnit.cGy), 5000));
            models.Add(new PrescriptionModel("CSI-init", "1", 6, new DoseValue(200, DoseValue.DoseUnit.cGy), 1200));
            models.Add(new PrescriptionModel("CSI-bst", "5", 7, new DoseValue(300, DoseValue.DoseUnit.cGy), 5700));
            models.Add(new PrescriptionModel("CSI-init", "3", 6, new DoseValue(600, DoseValue.DoseUnit.cGy), 3600));

            List<PlanTargetsModel> expected = new List<PlanTargetsModel>
        {
            new PlanTargetsModel("CSI-init", new List<TargetModel> {  new TargetModel("1", 1200), new TargetModel("2", 2400), new TargetModel("3", 3600)}),
            new PlanTargetsModel("CSI-bst", new List<TargetModel> {  new TargetModel("4", 5000), new TargetModel("5", 5700)})
        };
            List<PlanTargetsModel> result = TargetsHelper.GroupPrescriptionsByPlanIdAndOrderByTargetRx(models);
            Console.WriteLine($"{expected.Count} | {result.Count}");
            Assert.AreEqual(expected.Count, result.Count);

            Console.WriteLine("expected");
            foreach (PlanTargetsModel itr in expected)
            {
                foreach (TargetModel tgt in itr.Targets)
                {
                    Console.WriteLine($"{itr.PlanId} | {tgt.TargetId} | {tgt.TargetRxDose}");
                }
            }
            Console.WriteLine("result");
            foreach (PlanTargetsModel itr in result)
            {
                foreach (TargetModel tgt in itr.Targets)
                {
                    Console.WriteLine($"{itr.PlanId} | {tgt.TargetId} | {tgt.TargetRxDose}");
                }
            }

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected.ElementAt(i).PlanId, result.ElementAt(i).PlanId);
                CollectionAssert.AreEqual(expected.ElementAt(i).Targets, result.ElementAt(i).Targets, new TargetModelComparer());
            }
        }
    }

    public class TargetModelComparer : Comparer<TargetModel>
    {
        public override int Compare(TargetModel x, TargetModel y)
        {
            // compare the two mountains
            // for the purpose of this tests they are considered equal when their identifiers (names) match
            int idCompare = x.TargetId.CompareTo(y.TargetId);
            int rxCompare = x.TargetRxDose.CompareTo(y.TargetRxDose);
            return Math.Max(Math.Abs(idCompare), Math.Abs(rxCompare));
        }
    }
}