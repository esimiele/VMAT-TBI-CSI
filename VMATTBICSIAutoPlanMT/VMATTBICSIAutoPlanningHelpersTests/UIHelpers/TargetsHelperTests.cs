using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using VMATTBICSIAutoPlanningHelpers.Models;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System.Linq;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers.Tests
{
    [TestClass()]
    public class TargetsHelperTests
    {
        [TestMethod()]
        public void GroupTargetsByPlanIdTest()
        {
            List<PlanTargetsModel> models = new List<PlanTargetsModel>();
            models.Add(new PlanTargetsModel("CSI-init", new TargetModel("2", 2400)));
            models.Add(new PlanTargetsModel("CSI-init", new TargetModel("1", 1200)));
            models.Add(new PlanTargetsModel("CSI-init", new TargetModel("3", 3600)));
            models.Add(new PlanTargetsModel("CSI-bst", new TargetModel("5", 6000)));
            models.Add(new PlanTargetsModel("CSI-bst", new TargetModel("4", 4800)));

            List<PlanTargetsModel> expected = new List<PlanTargetsModel>
            {
                new PlanTargetsModel("CSI-init", new List<TargetModel> {  new TargetModel("1", 1200), new TargetModel("2", 2400), new TargetModel("3", 3600)}),
                new PlanTargetsModel("CSI-bst", new List<TargetModel> {  new TargetModel("4", 4800), new TargetModel("5", 6000)})
            };
            List<PlanTargetsModel> result = TargetsHelper.GroupTargetsByPlanIdAndOrderByTargetRx(models);
            Console.WriteLine($"{expected.Count} | {result.Count}");
            Assert.AreEqual(expected.Count, result.Count);

            Console.WriteLine("expected");
            foreach(PlanTargetsModel itr in expected)
            {
                foreach(TargetModel tgt in itr.Targets)
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

            for(int i = 0; i < expected.Count; i++)
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