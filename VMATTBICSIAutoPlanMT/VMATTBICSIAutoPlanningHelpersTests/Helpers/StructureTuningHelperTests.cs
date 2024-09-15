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
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    [TestClass()]
    public class StructureTuningHelperTests
    {
        [TestMethod()]
        public void AddTemplateSpecificStructureManipulationsTest()
        {
            List<RequestedTSManipulationModel> templateManipulationList = new List<RequestedTSManipulationModel>
            {
                new RequestedTSManipulationModel("Ovaries", TSManipulationType.CropTargetFromStructure, 0.1),
                new RequestedTSManipulationModel("Testes", TSManipulationType.CropTargetFromStructure, 0.1),
                new RequestedTSManipulationModel("Liver", TSManipulationType.CropTargetFromStructure, 0.0),
                new RequestedTSManipulationModel("Lungs", TSManipulationType.ContourSubStructure, -1.0),
                new RequestedTSManipulationModel("Lungs", TSManipulationType.ContourSubStructure, -1.5),
                new RequestedTSManipulationModel("Liver", TSManipulationType.ContourSubStructure, -1.0),
                new RequestedTSManipulationModel("Liver", TSManipulationType.ContourSubStructure, -2.0),
            };
            List<RequestedTSManipulationModel> defaultManipulationList = new List<RequestedTSManipulationModel> { };

            string sex = "Female";
            List<RequestedTSManipulationModel> expected = new List<RequestedTSManipulationModel>
            {
                new RequestedTSManipulationModel("Ovaries", TSManipulationType.CropTargetFromStructure, 0.1),
                new RequestedTSManipulationModel("Liver", TSManipulationType.CropTargetFromStructure, 0.0),
                new RequestedTSManipulationModel("Lungs", TSManipulationType.ContourSubStructure, -1.0),
                new RequestedTSManipulationModel("Lungs", TSManipulationType.ContourSubStructure, -1.5),
                new RequestedTSManipulationModel("Liver", TSManipulationType.ContourSubStructure, -1.0),
                new RequestedTSManipulationModel("Liver", TSManipulationType.ContourSubStructure, -2.0),
            };

            List<RequestedTSManipulationModel> result = StructureTuningHelper.AddTemplateSpecificStructureManipulations(templateManipulationList,
                                                                                                                  defaultManipulationList,
                                                                                                                  sex);

            Assert.AreEqual(expected.Count(), result.Count());
            int count = 0;
            foreach(RequestedTSManipulationModel itr in result)
            {
                Assert.AreEqual(itr.StructureId, expected.ElementAt(count).StructureId);
                Assert.AreEqual(itr.ManipulationType, expected.ElementAt(count).ManipulationType);
                Assert.AreEqual(itr.MarginInCM, expected.ElementAt(count).MarginInCM);
                count++;
            }
        }

        [TestMethod()]
        public void CheckStructuresToUnionTest()
        {
            StructureSet ss = BuildTestStructureSet();

            List<UnionStructureModel> expected = new List<UnionStructureModel>
            {
                new UnionStructureModel(ss.Structures.First(x => x.Id == "Lens_L"), ss.Structures.First(x => x.Id == "Lens_R"), "lenses"),
                new UnionStructureModel(ss.Structures.First(x => x.Id == "Lung_L"), ss.Structures.First(x => x.Id == "Lung_R"), "lungs"),
                new UnionStructureModel(ss.Structures.First(x => x.Id == "Kidney_L"), ss.Structures.First(x => x.Id == "Kidney_R"), "kidneys"),
            };

            List<UnionStructureModel> result = StructureTuningHelper.CheckStructuresToUnion(ss);
            int count = 0;
            foreach (UnionStructureModel itr in result)
            {
                Console.WriteLine($"{itr.Structure_Left.Id}, {itr.Structure_Right.Id}, {itr.ProposedUnionStructureId}");
                Assert.AreEqual(itr.Structure_Left, expected.ElementAt(count).Structure_Left);
                Assert.AreEqual(itr.Structure_Right, expected.ElementAt(count).Structure_Right);
                Assert.AreEqual(itr.ProposedUnionStructureId, expected.ElementAt(count).ProposedUnionStructureId);
                count++;
            }
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