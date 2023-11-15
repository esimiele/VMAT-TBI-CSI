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
using VMS.TPS.Common.Model;
using System.Windows;
using VMATTBICSIAutoPlanningHelpers.Tests.Helpers;

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    [TestClass()]
    public class PlanPrepHelperTests
    {
        [TestMethod()]
        public void ExtractBeamsPerIsoTestNoLatIsos()
        {
            ExternalPlanSetup plan = Mock.Create<ExternalPlanSetup>();
            List<Beam> testBeamSet = TBITestBeamBuilder.GenerateVMATTestBeamSet(1);
            Mock.Arrange(() => plan.Beams).Returns(testBeamSet);

            List<List<Beam>> result = PlanPrepHelper.ExtractBeamsPerIso(plan);
            List<List<Beam>> expected = TBITestBeamBuilder.GetExpectedBeamListGroupedByZPos(testBeamSet);
            int count = 0;
            foreach (List<Beam> itr in result)
            {
                CollectionAssert.AreEqual(itr, expected.ElementAt(count++));
            }
        }

        //[TestMethod()]
        //public void ExtractBeamsPerIsoTest2LatIsos()
        //{
        //    ExternalPlanSetup plan = Mock.Create<ExternalPlanSetup>();
        //    List<Beam> testBeamSet = TestBeamBuilder.GenerateTestBeamSet(2);
        //    Mock.Arrange(() => plan.Beams).Returns(testBeamSet);

        //    List<List<Beam>> result = PlanPrepHelper.ExtractBeamsPerIso(plan);
        //    List<List<Beam>> expected = TBITestBeamBuilder.GetExpectedBeamListGroupedByZPos(testBeamSet);
        //    int count = 0;
        //    foreach (List<Beam> itr in result)
        //    {
        //        CollectionAssert.AreEqual(itr, expected.ElementAt(count++));
        //    }
        //}

        //[TestMethod()]
        //public void ExtractBeamsPerIsoTest3LatIsos()
        //{
        //    ExternalPlanSetup plan = Mock.Create<ExternalPlanSetup>();
        //    List<Beam> testBeamSet = TBITestBeamBuilder.GenerateTestBeamSet(3);
        //    Mock.Arrange(() => plan.Beams).Returns(testBeamSet);

        //    List<List<Beam>> result = PlanPrepHelper.ExtractBeamsPerIso(plan);
        //    List<List<Beam>> expected = TBITestBeamBuilder.GetExpectedBeamListGroupedByZPos(testBeamSet);
        //    int count = 0;
        //    foreach (List<Beam> itr in result)
        //    {
        //        CollectionAssert.AreEqual(itr, expected.ElementAt(count++));
        //    }
        //}

        //[TestMethod()]
        //public void UpdateIsoListWithLateralIsosTestNoLatIsos()
        //{
        //    ExternalPlanSetup plan = Mock.Create<ExternalPlanSetup>();
        //    List<Beam> testBeamSet = TBITestBeamBuilder.GenerateTestBeamSet(1);
        //    Mock.Arrange(() => plan.Beams).Returns(testBeamSet);

        //    List<List<Beam>> beamsPerIso = PlanPrepHelper.ExtractBeamsPerIso(plan);
        //    //assume VMAT-only for no lat iso case
        //    List<string> isoNames = IsoNameHelper.GetTBIVMATIsoNames(beamsPerIso.Count, beamsPerIso.Count);
        //    (List<List<Beam>> updatedBeamsPerIso, List<string> updatedIsoNames) = PlanPrepHelper.UpdateIsoListWithLateralIsos(beamsPerIso, isoNames);
        //    List<List<Beam>> expectedBeamsList = TBITestBeamBuilder.GetExpectedBeamListGroupedByZAndXPos(testBeamSet);
        //    List<string> expectedIsoNames = TBITestBeamBuilder.GetExpectedIsoNameListGroupedByZAndXPos(testBeamSet.Count);
        //    int count = 0;
        //    foreach (List<Beam> itr in updatedBeamsPerIso)
        //    {
        //        CollectionAssert.AreEqual(itr, expectedBeamsList.ElementAt(count++));
        //    }
        //    CollectionAssert.AreEqual(updatedIsoNames, expectedIsoNames);
        //}

        //[TestMethod()]
        //public void UpdateIsoListWithLateralIsosTest2LatIsos()
        //{
        //    ExternalPlanSetup plan = Mock.Create<ExternalPlanSetup>();
        //    List<Beam> testBeamSet = TBITestBeamBuilder.GenerateTestBeamSet(2);
        //    Mock.Arrange(() => plan.Beams).Returns(testBeamSet);

        //    List<List<Beam>> beamsPerIso = PlanPrepHelper.ExtractBeamsPerIso(plan);
        //    //assume VMAT-only for no lat iso case
        //    List<string> isoNames = IsoNameHelper.GetTBIVMATIsoNames(beamsPerIso.Count, beamsPerIso.Count + 1);
        //    (List<List<Beam>> updatedBeamsPerIso, List<string> updatedIsoNames) = PlanPrepHelper.UpdateIsoListWithLateralIsos(beamsPerIso, isoNames);
        //    List<List<Beam>> expectedBeamsList = TBITestBeamBuilder.GetExpectedBeamListGroupedByZAndXPos(testBeamSet);
        //    List<string> expectedIsoNames = TBITestBeamBuilder.GetExpectedIsoNameListGroupedByZAndXPos(testBeamSet.Count);
        //    int count = 0;
        //    foreach (List<Beam> itr in updatedBeamsPerIso)
        //    {
        //        CollectionAssert.AreEqual(itr, expectedBeamsList.ElementAt(count++));
        //    }
        //    CollectionAssert.AreEqual(updatedIsoNames, expectedIsoNames);
        //}

        //[TestMethod()]
        //public void UpdateIsoListWithLateralIsosTest3LatIsos()
        //{
        //    ExternalPlanSetup plan = Mock.Create<ExternalPlanSetup>();
        //    List<Beam> testBeamSet = TBITestBeamBuilder.GenerateTestBeamSet(2);
        //    Mock.Arrange(() => plan.Beams).Returns(testBeamSet);

        //    List<List<Beam>> beamsPerIso = PlanPrepHelper.ExtractBeamsPerIso(plan);
        //    //assume VMAT-only for no lat iso case
        //    List<string> isoNames = IsoNameHelper.GetTBIVMATIsoNames(beamsPerIso.Count, beamsPerIso.Count + 1);
        //    (List<List<Beam>> updatedBeamsPerIso, List<string> updatedIsoNames) = PlanPrepHelper.UpdateIsoListWithLateralIsos(beamsPerIso, isoNames);
        //    List<List<Beam>> expectedBeamsList = TBITestBeamBuilder.GetExpectedBeamListGroupedByZAndXPos(testBeamSet);
        //    List<string> expectedIsoNames = TBITestBeamBuilder.GetExpectedIsoNameListGroupedByZAndXPos(testBeamSet.Count);
        //    int count = 0;
        //    foreach (List<Beam> itr in updatedBeamsPerIso)
        //    {
        //        CollectionAssert.AreEqual(itr, expectedBeamsList.ElementAt(count++));
        //    }
        //    CollectionAssert.AreEqual(updatedIsoNames, expectedIsoNames);
        //}

        [TestMethod()]
        public void ExtractIsoPositionsTest()
        {
            ExternalPlanSetup plan = Mock.Create<ExternalPlanSetup>();
            List<Beam> testBeamSet = TBITestBeamBuilder.GenerateVMATTestBeamSet(1);
            Mock.Arrange(() => plan.Beams).Returns(testBeamSet);
            StructureSet ss = Mock.Create<StructureSet>();
            Mock.Arrange(() => plan.StructureSet).Returns(ss);
            Image img = Mock.Create<Image>();
            Mock.Arrange(() => ss.Image).Returns(img);
            Mock.Arrange(() => img.Origin).Returns(new VVector());
            foreach (Beam b in testBeamSet)
            {
                Mock.Arrange(() => img.DicomToUser(b.IsocenterPosition, plan)).Returns(b.IsocenterPosition);
            }

            //first beam isocenter position in each group (grouping based on z position)
            List<Tuple<double, double, double>> expected = new List<Tuple<double, double, double>>
            {
                Tuple.Create(0.0,0.0,-10.0),
                Tuple.Create(0.0,0.0,-15.0),
                Tuple.Create(0.0,0.0,-25.0),
                Tuple.Create(0.0,0.0,-35.0)
            };

            List<Tuple<double, double, double>> result = PlanPrepHelper.ExtractIsoPositions(plan);
            CollectionAssert.AreEqual(expected, result);
        }

        [TestMethod()]
        public void CalculateShiftsTest()
        {
            //first beam isocenter position in each group (grouping based on z position)
            List<Tuple<double, double, double>> testIsoPositions = new List<Tuple<double, double, double>>
            {
                Tuple.Create(0.0,0.0,-10.0),
                Tuple.Create(0.0,0.0,-15.0),
                Tuple.Create(0.0,0.0,-25.0),
                Tuple.Create(0.0,0.0,-35.0)
            };

            List<Tuple<double, double, double>> expectedShiftsFromBBs = new List<Tuple<double, double, double>>
            {
                Tuple.Create(0.0,0.0,-1.0),
                Tuple.Create(0.0,0.0,-1.50),
                Tuple.Create(0.0,0.0,-2.50),
                Tuple.Create(0.0,0.0,-3.50)
            };

            List<Tuple<double, double, double>> expectedShiftsBetweenIsos = new List<Tuple<double, double, double>>
            {
                Tuple.Create(0.0,0.0,-1.0),
                Tuple.Create(0.0,0.0,-0.5),
                Tuple.Create(0.0,0.0,-1.0),
                Tuple.Create(0.0,0.0,-1.0)
            };

            (List<Tuple<double, double, double>> resultsShiftsFromBBs, List<Tuple<double, double, double>> resultsShiftsBetweenIsos) = PlanPrepHelper.CalculateShifts(testIsoPositions);
            CollectionAssert.AreEqual(resultsShiftsFromBBs, expectedShiftsFromBBs);
            CollectionAssert.AreEqual(resultsShiftsBetweenIsos, expectedShiftsBetweenIsos);
        }

        [TestMethod()]
        public void GetTBIShiftNoteTestNoAPPA()
        {
            //no couch structure, 4 isos, no lateral shifts
            ExternalPlanSetup plan = Mock.Create<ExternalPlanSetup>();
            List<Beam> testBeamSet = TBITestBeamBuilder.GenerateVMATTestBeamSet(1);
            Mock.Arrange(() => plan.Beams).Returns(testBeamSet);
            StructureSet ss = Mock.Create<StructureSet>();
            Mock.Arrange(() => plan.StructureSet).Returns(ss);
            Structure target = Mock.Create<Structure>();
            Mock.Arrange(() => target.Id).Returns("target");
            Mock.Arrange(() => ss.Structures).Returns(new List<Structure> { target });
            Image img = Mock.Create<Image>();
            Mock.Arrange(() => ss.Image).Returns(img);
            Mock.Arrange(() => img.Origin).Returns(new VVector());
            foreach (Beam b in testBeamSet)
            {
                Mock.Arrange(() => img.DicomToUser(b.IsocenterPosition, plan)).Returns(b.IsocenterPosition);
            }

            StringBuilder expected = new StringBuilder();
            expected.AppendLine("No couch surface structure found in plan!");
            expected.AppendLine("VMAT TBI setup per procedure. No Spinning Manny.");
            expected.AppendLine("Dosimetric shifts SUP to INF:");
            expected.AppendLine($"Head iso shift from CT REF:");
            expected.AppendLine($"Z = 1.0 cm INF");
            expected.AppendLine($"Chest iso shift from **Head ISO**:");
            expected.AppendLine($"Z = 0.5 cm INF");
            expected.AppendLine($"Pelvis iso shift from **Chest ISO**:");
            expected.AppendLine($"Z = 1.0 cm INF");
            expected.AppendLine($"Legs iso shift from **Pelvis ISO**:");
            expected.AppendLine($"Z = 1.0 cm INF");

            StringBuilder result = PlanPrepHelper.GetTBIShiftNote(plan, null);
            Console.WriteLine(result.ToString());
            StringAssert.Equals(expected.ToString(), result.ToString());
        }

        [TestMethod()]
        public void GetTBIShiftNoteTestWithAPPA()
        {
            //no couch structure, 4 isos, no lateral shifts
            ExternalPlanSetup vmatPlan = Mock.Create<ExternalPlanSetup>();
            List<Beam> testBeamSet = TBITestBeamBuilder.GenerateVMATTestBeamSet(1);
            Mock.Arrange(() => vmatPlan.Beams).Returns(testBeamSet);
            StructureSet ss = Mock.Create<StructureSet>();
            Mock.Arrange(() => vmatPlan.StructureSet).Returns(ss);
            Structure target = Mock.Create<Structure>();
            Mock.Arrange(() => target.Id).Returns("target");
            Mock.Arrange(() => ss.Structures).Returns(new List<Structure> { target });
            Image img = Mock.Create<Image>();
            Mock.Arrange(() => ss.Image).Returns(img);
            Mock.Arrange(() => img.Origin).Returns(new VVector());
            foreach (Beam b in testBeamSet)
            {
                Mock.Arrange(() => img.DicomToUser(b.IsocenterPosition, vmatPlan)).Returns(b.IsocenterPosition);
            }

            ExternalPlanSetup appaPlan = Mock.Create<ExternalPlanSetup>();
            List<Beam> appaBeams = TBITestBeamBuilder.GenerateAPPATestBeamSet(2);
            Mock.Arrange(() => appaPlan.Beams).Returns(appaBeams);
            StructureSet ss1 = Mock.Create<StructureSet>();
            Mock.Arrange(() => appaPlan.StructureSet).Returns(ss1);
            Image img1 = Mock.Create<Image>();
            Mock.Arrange(() => ss1.Image).Returns(img1);
            Mock.Arrange(() => img1.Origin).Returns(new VVector());
            foreach (Beam b in appaBeams)
            {
                Mock.Arrange(() => img1.DicomToUser(b.IsocenterPosition, appaPlan)).Returns(b.IsocenterPosition);
            }

            StringBuilder expected = new StringBuilder();
            expected.AppendLine("No couch surface structure found in plan!");
            expected.AppendLine("VMAT TBI setup per procedure. Please ensure the matchline on Spinning Manny and the bag matches");
            expected.AppendLine("Dosimetric shifts SUP to INF:");
            expected.AppendLine($"Head iso shift from CT REF:");
            expected.AppendLine($"Z = 1.0 cm INF");
            expected.AppendLine($"Chest iso shift from **Head ISO**:");
            expected.AppendLine($"Z = 0.5 cm INF");
            expected.AppendLine($"Abdomen iso shift from **Chest ISO**:");
            expected.AppendLine($"Z = 1.0 cm INF");
            expected.AppendLine($"Pelvis iso shift from **Pelvis ISO**:");
            expected.AppendLine($"Z = 1.0 cm INF");
            expected.AppendLine($"Rotate Spinning Manny, shift to opposite Couch Lat");
            expected.AppendLine($"Upper Leg iso - same Couch Lng as Pelvis iso");
            expected.AppendLine($"Lower Leg iso shift from **Upper Leg ISO**:");
            expected.AppendLine($"Z = 1.0 cm INF");

            StringBuilder result = PlanPrepHelper.GetTBIShiftNote(vmatPlan, appaPlan);
            Console.WriteLine(result.ToString());
            StringAssert.Equals(expected.ToString(), result.ToString());
        }

        [TestMethod()]
        public void GetCSIShiftNoteTest()
        {
            //no couch structure, 4 isos, no lateral shifts
            ExternalPlanSetup plan = Mock.Create<ExternalPlanSetup>();
            List<Beam> testBeamSet = CSITestBeamBuilder.GenerateTestBeamSet(3);
            Mock.Arrange(() => plan.Beams).Returns(testBeamSet);
            StructureSet ss = Mock.Create<StructureSet>();
            Mock.Arrange(() => plan.StructureSet).Returns(ss);
            Structure target = Mock.Create<Structure>();
            Mock.Arrange(() => target.Id).Returns("target");
            Mock.Arrange(() => ss.Structures).Returns(new List<Structure> { target });
            Image img = Mock.Create<Image>();
            Mock.Arrange(() => ss.Image).Returns(img);
            Mock.Arrange(() => img.Origin).Returns(new VVector());
            foreach (Beam b in testBeamSet)
            {
                Mock.Arrange(() => img.DicomToUser(b.IsocenterPosition, plan)).Returns(b.IsocenterPosition);
            }

            StringBuilder expected = new StringBuilder();
            expected.AppendLine("No couch surface structure found in plan!");
            expected.AppendLine("VMAT CSI setup per procedure.");
            expected.AppendLine("Dosimetric shifts SUP to INF:");
            expected.AppendLine($"Brain iso shift from CT REF:");
            expected.AppendLine($"Z = 1.0 cm INF");
            expected.AppendLine($"UpSpine iso shift from **Brain ISO**:");
            expected.AppendLine($"Z = 0.5 cm INF");
            expected.AppendLine($"LowSpine iso shift from **UpSpine ISO**:");
            expected.AppendLine($"Z = 1.0 cm INF");

            StringBuilder result = PlanPrepHelper.GetCSIShiftNote(plan);
            Console.WriteLine(result.ToString());
            StringAssert.Equals(expected.ToString(), result.ToString());
        }
    }
}