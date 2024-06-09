using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.Models;
using System.Windows.Input;
using Telerik.JustMock.AutoMock.Ninject.Planning.Targets;
using VMS.TPS.Common.Model.API;

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    [TestClass()]
    public class ConfigurationHelperTests
    {
        [TestMethod()]
        public void CropLineTestPass1()
        {
            string testLine = "create ring{PTV_CSI,1.5,2.0,600}";
            string expected = "PTV_CSI,1.5,2.0,600}";

            Assert.AreEqual(expected, ConfigurationHelper.CropLine(testLine, "{"));
        }

        [TestMethod()]
        public void CropLineTestPass2()
        {
            string testLine = "PTV_CSI,1.5,2.0,600}";
            string expected = "1.5,2.0,600}";

            Assert.AreEqual(expected, ConfigurationHelper.CropLine(testLine, ","));
        }

        [TestMethod()]
        public void CropLineTestFail()
        {
            string testLine = "create ring{PTV_CSI,1.5,2.0,600}";
            string expected = "PTV_CSI,1.5,2.0,600}";
            Assert.AreNotEqual(expected, ConfigurationHelper.CropLine(testLine, ","));
        }

        [TestMethod()]
        public void ParseJawPositionsTestPass()
        {
            string testJawPos = "add jaw position{-100.0,-100.0,100.0,100.0}";
            VRect<double> expected = new VRect<double>(-100.0, -100.0, 100.0, 100.0);
            (bool fail, VRect<double> result) = ConfigurationHelper.ParseJawPositions(testJawPos);
            Assert.AreEqual(expected, result);
        }

        [TestMethod()]
        public void ParseJawPositionsTestFail1()
        {
            string testJawPos = "add jaw position{-100.0,-100.0,100.0,100.0}";
            VRect<double> expected = new VRect<double>(-200.0, -100.0, 100.0, 100.0);
            (bool fail, VRect<double> result) = ConfigurationHelper.ParseJawPositions(testJawPos);
            Assert.AreNotEqual(expected, result);
        }

        [TestMethod()]
        public void ParseJawPositionsTestFail2()
        {
            string testJawPos = "add jaw position{-100.0,-100.0,100.0,100.0,100.0}";
            (bool fail, VRect<double> result) = ConfigurationHelper.ParseJawPositions(testJawPos);
            Assert.AreEqual(true, fail);
        }

        [TestMethod()]
        public void ParseCreateTSTestPass()
        {
            string testCreateTS = "create TS{CONTROL,TS_Eyes}";
            RequestedTSStructureModel expected = new RequestedTSStructureModel("CONTROL", "TS_Eyes");
            RequestedTSStructureModel result = ConfigurationHelper.ParseCreateTS(testCreateTS);
            Assert.AreEqual(expected, result);
        }

        [TestMethod()]
        public void ParseCreateTSTestFail1()
        {
            string testCreateTS = "create TS{PTV,TS_Eyes}";
            RequestedTSStructureModel expected = new RequestedTSStructureModel("CONTROL", "TS_Eyes");
            RequestedTSStructureModel result = ConfigurationHelper.ParseCreateTS(testCreateTS);
            Assert.AreNotEqual(expected, result);
        }

        [TestMethod()]
        public void ParseDaemonSettingsTest()
        {
            string dummyDaemon = "Aria DB daemon={VMSDBD,10.151.176.60,51402}";
            DaemonModel expected = new DaemonModel("VMSDBD", "10.151.176.60", 51402);
            Assert.AreEqual(expected, ConfigurationHelper.ParseDaemonSettings(dummyDaemon));
        }

        [TestMethod()]
        public void ParseCreateRingTest()
        {
            string dummyRing = "create ring{PTV_CSI,1.5,2.0,600}";
            TSRingStructureModel expected = new TSRingStructureModel("PTV_CSI", 1.5, 2.0, 600);
            Assert.AreEqual(expected, ConfigurationHelper.ParseCreateRing(dummyRing));
        }

        [TestMethod()]
        public void ParseTargetsTest()
        {
            string dummyTarget = "add target{PTV_CSI,1200,CSI-init}";
            PlanTargetsModel expected = new PlanTargetsModel("PTV_CSI", new TargetModel("CSI-init", 1200));
            Assert.AreEqual(expected, ConfigurationHelper.ParseTargets(dummyTarget));
        }

        [TestMethod()]
        public void ParseTSManipulationTest()
        {
            List<string> dummyTSManipulations = new List<string>
            {
                "add TS manipulation{Lenses,Crop from body,0.0}",
                "add TS manipulation{Lungs,Contour substructure,-1.0}",
                "add TS manipulation{Brainstem,Crop target from structure,0.0}",
                "add TS manipulation{Eyes,Contour overlap with target,0.0}",
                "add TS manipulation{skin,Crop target from structure,3.0}"
            };
            List<RequestedTSManipulationModel> expected = new List<RequestedTSManipulationModel>
            {
                new RequestedTSManipulationModel("Lenses", Enums.TSManipulationType.CropFromBody,0),
                new RequestedTSManipulationModel("Lungs", Enums.TSManipulationType.ContourSubStructure, -1),
                new RequestedTSManipulationModel("Brainstem", Enums.TSManipulationType.CropTargetFromStructure,0),
                new RequestedTSManipulationModel("Eyes", Enums.TSManipulationType.ContourOverlapWithTarget, 0),
                new RequestedTSManipulationModel("skin", Enums.TSManipulationType.CropTargetFromStructure, 3)
            };

            TSManipulationComparer comparer = new TSManipulationComparer();
            for (int i = 0; i < expected.Count; i++)
            {
                RequestedTSManipulationModel resultTMP = ConfigurationHelper.ParseTSManipulation(dummyTSManipulations.ElementAt(i));
                Console.WriteLine($"{comparer.Print(expected.ElementAt(i))} | {comparer.Print(resultTMP)}");
                Assert.IsTrue(comparer.Equals(expected.ElementAt(i), resultTMP));
            }
        }

        [TestMethod()]
        public void ParseOptimizationConstraintTest()
        {
            List<string> dummyConstraints = new List<string>
            {
                "add opt constraint{ PTV_Body,Lower,1200.0,100.0,100}",
                "add opt constraint{ PTV_Body,Upper,1212.0,0.0,100}",
                "add opt constraint{ PTV_Body,Lower,1202.0,98.0,100}",
                "add opt constraint{ Kidneys,Mean,750.0,0.0,80}",
                "add opt constraint{ Kidneys - 1.0cm,Mean,400.0,0.0,50}",
                "add opt constraint{ Lenses,Mean,1140.0,0.0,50}",
                "add opt constraint{ Lungs,Mean,600.0,0.0,90}",
                "add opt constraint{ Lungs - 1.0cm,Mean,300.0,0.0,80}",
                "add opt constraint{ Lungs - 2.0cm,Mean,200.0,0.0,70}",
                "add opt constraint{ Bowel,Upper,1205.0,0.0,50}"
            };

            List<OptimizationConstraintModel> expected = new List<OptimizationConstraintModel>
            {
                new OptimizationConstraintModel("PTV_Body", Enums.OptimizationObjectiveType.Lower, 1200, Enums.Units.cGy, 100.0, 100),
                new OptimizationConstraintModel("PTV_Body", Enums.OptimizationObjectiveType.Upper, 1212, Enums.Units.cGy, 0.0, 100),
                new OptimizationConstraintModel("PTV_Body", Enums.OptimizationObjectiveType.Lower, 1202, Enums.Units.cGy, 98.0, 100),
                new OptimizationConstraintModel("Kidneys", Enums.OptimizationObjectiveType.Mean, 750, Enums.Units.cGy, 0.0, 80),
                new OptimizationConstraintModel("Kidneys - 1.0cm", Enums.OptimizationObjectiveType.Mean, 400, Enums.Units.cGy, 0.0, 50),
                new OptimizationConstraintModel("Lenses", Enums.OptimizationObjectiveType.Mean, 1140, Enums.Units.cGy, 0.0, 50),
                new OptimizationConstraintModel("Lungs", Enums.OptimizationObjectiveType.Mean, 600, Enums.Units.cGy, 0.0, 90),
                new OptimizationConstraintModel("Lungs - 1.0cm", Enums.OptimizationObjectiveType.Mean, 300, Enums.Units.cGy, 0.0, 80),
                new OptimizationConstraintModel("Lungs - 2.0cm", Enums.OptimizationObjectiveType.Mean, 200, Enums.Units.cGy, 0.0, 70),
                new OptimizationConstraintModel("Bowel", Enums.OptimizationObjectiveType.Upper, 1205, Enums.Units.cGy, 0.0, 50),
            };

            OptimizationConstraintComparer comparer = new OptimizationConstraintComparer();
            for(int i = 0; i < expected.Count; i++)
            {
                OptimizationConstraintModel resultTMP = ConfigurationHelper.ParseOptimizationConstraint(dummyConstraints.ElementAt(i));
                Console.WriteLine($"{comparer.PrintConstraint(expected.ElementAt(i))} | {comparer.PrintConstraint(resultTMP)}");
                Assert.IsTrue(comparer.Equals(expected.ElementAt(i),resultTMP));
            }
            
        }


        //Unit testing a private method
        //[TestMethod]
        //public void IsLifeBeautiful_returns_true_when_your_name_is_God()
        //{
        //    God sut = new God();
        //    object[] parameters = { "God" };
        //    PrivateObject po = new PrivateObject(sut);

        //    var returnValue = po.Invoke("IsLifeBeautiful", parameters);

        //    Assert.IsTrue((bool)returnValue);
        //}
    }

    public class OptimizationConstraintComparer : IEqualityComparer<OptimizationConstraintModel>
    {
        public string PrintConstraint(OptimizationConstraintModel c)
        {
            return $"{c.StructureId} {c.ConstraintType} {c.QueryDose} {c.QueryDoseUnits} {c.QueryVolume} {c.QueryVolumeUnits} {c.Priority}";
        }

        public bool Equals(OptimizationConstraintModel x, OptimizationConstraintModel y)
        {
            if (x == null && y == null) return true;
            else if (x == null || y == null) return false;
            else if (object.ReferenceEquals(x, y)) return true;

            return string.Equals(x.StructureId, y.StructureId)
                && x.ConstraintType == y.ConstraintType
                && CalculationHelper.AreEqual(x.QueryDose, y.QueryDose)
                && x.QueryDoseUnits == y.QueryDoseUnits
                && CalculationHelper.AreEqual(x.QueryVolume, y.QueryVolume)
                && x.QueryVolumeUnits == y.QueryVolumeUnits
                && x.Priority == y.Priority;
        }

        public int GetHashCode(OptimizationConstraintModel obj)
        {
            throw new NotImplementedException();
        }
    }

    public class TSManipulationComparer : IEqualityComparer<RequestedTSManipulationModel>
    {
        public string Print(RequestedTSManipulationModel x)
        {
            return $"{x.StructureId} {x.ManipulationType} {x.MarginInCM}";
        }
        public bool Equals(RequestedTSManipulationModel x, RequestedTSManipulationModel y)
        {
            if (x == null && y == null) return true;
            else if (x == null || y == null) return false;
            else if (object.ReferenceEquals(x,y)) return true;

            return string.Equals(x.StructureId, y.StructureId)
                && x.ManipulationType == y.ManipulationType
                && CalculationHelper.AreEqual(x.MarginInCM, y.MarginInCM);
        }

        public int GetHashCode(RequestedTSManipulationModel obj)
        {
            throw new NotImplementedException();
        }
    }
}