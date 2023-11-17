using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMS.TPS.Common.Model.API;
using Telerik.JustMock;

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    [TestClass()]
    public class StructureTuningHelperTests
    {
        [TestMethod()]
        public void AddTemplateSpecificStructureManipulationsTest()
        {
            List<Tuple<string, TSManipulationType, double>> templateManipulationList = new List<Tuple<string, TSManipulationType, double>>
            {
                Tuple.Create("Ovaries", TSManipulationType.CropTargetFromStructure, 0.1),
                Tuple.Create("Testes", TSManipulationType.CropTargetFromStructure, 0.1),
                Tuple.Create("Liver", TSManipulationType.CropTargetFromStructure, 0.0),
                Tuple.Create("Lungs", TSManipulationType.ContourSubStructure, -1.0),
                Tuple.Create("Lungs", TSManipulationType.ContourSubStructure, -1.5),
                Tuple.Create("Liver", TSManipulationType.ContourSubStructure, -1.0),
                Tuple.Create("Liver", TSManipulationType.ContourSubStructure, -2.0),
            };
            List<Tuple<string, TSManipulationType, double>> defaultManipulationList = new List<Tuple<string, TSManipulationType, double>> { };

            string sex = "Female";
            List<Tuple<string, TSManipulationType, double>> expected = new List<Tuple<string, TSManipulationType, double>>
            {
                Tuple.Create("Ovaries", TSManipulationType.CropTargetFromStructure, 0.1),
                Tuple.Create("Liver", TSManipulationType.CropTargetFromStructure, 0.0),
                Tuple.Create("Lungs", TSManipulationType.ContourSubStructure, -1.0),
                Tuple.Create("Lungs", TSManipulationType.ContourSubStructure, -1.5),
                Tuple.Create("Liver", TSManipulationType.ContourSubStructure, -1.0),
                Tuple.Create("Liver", TSManipulationType.ContourSubStructure, -2.0),
            };

            List<Tuple<string, TSManipulationType, double>> result = StructureTuningHelper.AddTemplateSpecificStructureManipulations(templateManipulationList,
                                                                                                                                     defaultManipulationList,
                                                                                                                                     sex);

            CollectionAssert.AreEqual(expected, result);
        }

        [TestMethod()]
        public void CheckStructuresToUnionTest()
        {
            StructureSet ss = BuildTestStructureSet();

            List<Tuple<Structure, Structure, string>> expected = new List<Tuple<Structure, Structure, string>>
            {
                Tuple.Create(ss.Structures.First(x => x.Id == "Lens_L"), ss.Structures.First(x => x.Id == "Lens_R"), "lenses"),
                Tuple.Create(ss.Structures.First(x => x.Id == "Lung_L"), ss.Structures.First(x => x.Id == "Lung_R"), "lungs"),
                Tuple.Create(ss.Structures.First(x => x.Id == "Kidney_L"), ss.Structures.First(x => x.Id == "Kidney_R"), "kidneys"),
            };

            List<Tuple<Structure, Structure, string>> result = StructureTuningHelper.CheckStructuresToUnion(ss);
            foreach (Tuple<Structure, Structure, string> itr in result)
            {
                Console.WriteLine($"{itr.Item1.Id}, {itr.Item2.Id}, {itr.Item3}");
            }
            CollectionAssert.AreEqual(expected, result);
        }

        public StructureSet BuildTestStructureSet()
        {
            StructureSet ss = Mock.Create<StructureSet>();
            Structure lensL = Mock.Create<Structure>();
            Mock.Arrange(() => lensL.Id).Returns("Lens_L");
            Structure lensR = Mock.Create<Structure>();
            Mock.Arrange(() => lensR.Id).Returns("Lens_R");
            Structure liver = Mock.Create<Structure>();
            Mock.Arrange(() => liver.Id).Returns("liver");
            Structure lungL = Mock.Create<Structure>();
            Mock.Arrange(() => lungL.Id).Returns("Lung_L");
            Structure lungR = Mock.Create<Structure>();
            Mock.Arrange(() => lungR.Id).Returns("Lung_R");
            Structure bowel = Mock.Create<Structure>();
            Mock.Arrange(() => bowel.Id).Returns("bowel");
            Structure kidneyL = Mock.Create<Structure>();
            Mock.Arrange(() => kidneyL.Id).Returns("Kidney_L");
            Structure kidneyR = Mock.Create<Structure>();
            Mock.Arrange(() => kidneyR.Id).Returns("Kidney_R");
            Mock.Arrange(() => ss.Structures).Returns(new List<Structure> { lensL, lensR, liver, lungL, lungR, bowel, kidneyL, kidneyR });
            return ss;
        }

        [TestMethod()]
        public void GetStructureFromIdTest()
        {
            StructureSet ss = BuildTestStructureSet();

            Structure expected = ss.Structures.First(x => x.Id == "Kidney_R");
            Structure result = StructureTuningHelper.GetStructureFromId("Kidney_R", ss, false);
            Assert.AreEqual(expected, result);

            Structure wrongStructure = StructureTuningHelper.GetStructureFromId("Kidney_L", ss, false);
            Assert.AreNotEqual(expected, wrongStructure);
        }

        [TestMethod()]
        public void DoesStructureExistInSSTest()
        {
            StructureSet ss = BuildTestStructureSet();
            bool expected = false;
            bool result = StructureTuningHelper.DoesStructureExistInSS("brain", ss, false);
            Assert.AreEqual(expected, result);

            expected = true;
            result = StructureTuningHelper.DoesStructureExistInSS("Lung_R", ss, false);
            Assert.AreEqual(expected, result);

            expected = false;
            result = StructureTuningHelper.DoesStructureExistInSS(new List<string> { "testes, brain, bones, stomach" }, ss, false);
            Assert.AreEqual(expected, result);

            expected = true;
            result = StructureTuningHelper.DoesStructureExistInSS(new List<string> { "testes, brain, bones, stomach", "Liver" }, ss, false);
            Assert.AreEqual(expected, result);
        }
    }
}