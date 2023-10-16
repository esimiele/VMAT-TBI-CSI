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

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    /// <summary>
    /// Only contains a sampling of tests for the Contour helper methods. Primarily the contouroverlap, cropstructurefromstructure, etc. methods since these all use (basically) the same logic with
    /// minor variations. Tests were written for the more involved helper methods
    /// </summary>
    [TestClass()]
    public class ContourHelperTests
    {
        public (Structure, StructureSet, double) SetupCropFromBodyTest()
        {
            Structure theStructure = Mock.Create<Structure>();
            Mock.Arrange(() => theStructure.Id).Returns("test");
            SegmentVolume structSegVol = Mock.Create<SegmentVolume>();
            Mock.Arrange(() => theStructure.SegmentVolume).Returns(structSegVol);

            Structure body = Mock.Create<Structure>();
            Mock.Arrange(() => body.Id).Returns("body");
            SegmentVolume bodySegVol = Mock.Create<SegmentVolume>();
            Mock.Arrange(() => body.SegmentVolume).Returns(bodySegVol);
            Mock.Arrange(() => structSegVol.And(bodySegVol)).DoNothing();

            StructureSet ss = Mock.Create<StructureSet>();
            Mock.Arrange(() => ss.Structures).Returns(new List<Structure> { theStructure, body });

            //in cm
            double margin = 0.3;
            return (theStructure, ss, margin);
        }

        public (Structure, Structure, double) GeneralContourHelperSetup()
        {
            Structure structure1 = Mock.Create<Structure>();
            Mock.Arrange(() => structure1.Id).Returns("test1");
            SegmentVolume structSegVol1 = Mock.Create<SegmentVolume>();
            Mock.Arrange(() => structure1.SegmentVolume).Returns(structSegVol1);

            Structure structure2 = Mock.Create<Structure>();
            Mock.Arrange(() => structure2.Id).Returns("test2");
            SegmentVolume structSegVol2 = Mock.Create<SegmentVolume>();
            Mock.Arrange(() => structure2.SegmentVolume).Returns(structSegVol2);

            Mock.Arrange(() => structSegVol1.And(structSegVol2)).DoNothing();
            Mock.Arrange(() => structSegVol1.Or(structSegVol2)).DoNothing();
            Mock.Arrange(() => structSegVol1.Sub(structSegVol2)).DoNothing();
            Mock.Arrange(() => structSegVol1.Xor(structSegVol2)).DoNothing();

            //in cm
            double margin = 0.5;
            return (structure1, structure2, margin);
        }

        [TestMethod()]
        public void CropStructureFromBodyTest()
        {
            (Structure testStruct, StructureSet ss, double margin) = SetupCropFromBodyTest();
            bool expected = false;
            (bool result, StringBuilder sb) = ContourHelper.CropStructureFromBody(testStruct, ss, margin);
            Assert.AreEqual(expected, result);
        }

        [TestMethod()]
        public void CropStructureFromStructureTest()
        {
            (Structure s1, Structure s2, double margin) = GeneralContourHelperSetup();
            bool expected = false;
            (bool result, StringBuilder sb) = ContourHelper.CropStructureFromStructure(s1, s2, margin);
            Assert.AreEqual(expected, result);
        }

        [TestMethod()]
        public void ContourOverlapTest()
        {
            (Structure s1, Structure s2, double margin) = GeneralContourHelperSetup();
            bool expected = false;
            (bool result, StringBuilder sb) = ContourHelper.ContourOverlap(s1, s2, margin);
            Assert.AreEqual(expected, result);
        }

        [TestMethod()]
        public void ContourUnionTest()
        {
            (Structure s1, Structure s2, double margin) = GeneralContourHelperSetup();
            bool expected = false;
            (bool result, StringBuilder sb) = ContourHelper.ContourUnion(s1, s2, margin);
            Assert.AreEqual(expected, result);
        }

        public VVector[] GenerateCircleContourPoints(double addedMargin)
        {
            //radius in mm
            double radius = 100.0 + addedMargin;
            VVector[] result = new VVector[36];
            for(int i = 0; i < 36; i++)
            {
                result[i] = new VVector(radius * Math.Cos(i * 10), radius * Math.Sin(i * 10), 0.0);
            }
            return result;
        }

        [TestMethod()]
        public void GenerateContourPointsTest()
        {
            VVector[] testpts = GenerateCircleContourPoints(0.0);
            double margin = 10.0;
            VVector[] expectedpts = GenerateCircleContourPoints(margin);
            VVector[] outputpts = ContourHelper.GenerateContourPoints(testpts, margin);
            //50 micron tolorerance due to rounding errors between the setup and test methods (tolerance doesn't need to be super tight for this method)
            double tolerance = 0.05;
            List<bool> expected = new List<bool> { };
            List<bool> result = new List<bool> { };
            for(int i = 0; i < expectedpts.Count(); i++)
            {
                expected.Add(true);
                result.Add(CalculationHelper.AreEqual(expectedpts[i].x, outputpts[i].x, tolerance) && CalculationHelper.AreEqual(expectedpts[i].y, outputpts[i].y, tolerance));
                //Console.WriteLine($"({expectedpts[i].x}, {expectedpts[i].y}) | ({outputpts[i].x}, {outputpts[i].y})");
                //Console.WriteLine($"{CalculationHelper.AreEqual(expectedpts[i].x, outputpts[i].x, tolerance)}");
            }
            CollectionAssert.AreEqual(expected, result);
        }
    }
}