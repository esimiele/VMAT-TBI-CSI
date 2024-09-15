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
using System.Windows.Media.Media3D;
using Telerik.JustMock.AutoMock.Ninject.Planning.Targets;
using VMATTBICSIAutoPlanningHelpers.Delegates;

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
            for (int i = 0; i < 36; i++)
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
            for (int i = 0; i < expectedpts.Count(); i++)
            {
                Assert.AreEqual(expectedpts[i].x, outputpts[i].x, tolerance);
                Assert.AreEqual(expectedpts[i].y, outputpts[i].y, tolerance);
                Console.WriteLine($"({expectedpts[i].x}, {expectedpts[i].y}) | ({outputpts[i].x}, {outputpts[i].y})");
                //Console.WriteLine($"{CalculationHelper.AreEqual(expectedpts[i].x, outputpts[i].x, tolerance)}");
            }
        }

        public (Structure, VVector) MatLaxProjectionDistanceTestSetupStructure()
        {
            VVector iso = new VVector(0, 0, 0);
            Structure target = Mock.Create<Structure>();
            Mock.Arrange(() => target.Id).Returns("test");
            MeshGeometry3D geo = new MeshGeometry3D();
            //retangular grid from (20,10) to (-20,-10) with points every 5 mm in y and 10 mm in x
            geo.Positions = new Point3DCollection
            {
                new Point3D(20,10,0),
                new Point3D(20,5,0),
                new Point3D(20,-5,0),
                new Point3D(20,-10,0),
                new Point3D(10,10,0),
                new Point3D(10,5,0),
                new Point3D(10,-5,0),
                new Point3D(10,-10,0),
                new Point3D(0,10,0),
                new Point3D(0,5,0),
                new Point3D(0,-5,0),
                new Point3D(0,-10,0),
                new Point3D(-10,10,0),
                new Point3D(-10,5,0),
                new Point3D(-10,-5,0),
                new Point3D(-10,-10,0),
                new Point3D(-20,10,0),
                new Point3D(-20,5,0),
                new Point3D(-20,-5,0),
                new Point3D(-20,-10,0)
            };
            Mock.Arrange(() => target.MeshGeometry).Returns(geo);
            return (target, iso);
        }

        public (VVector[], VVector) MatLaxProjectionDistanceTestSetupVVector()
        {
            VVector iso = new VVector(0, 0, 0);
            //retangular grid from (20,10) to (-20,-10) with points every 5 mm in y and 10 mm in x
            VVector[] positions = new VVector[]
            {
                new VVector(20,10,0),
                new VVector(20,5,0),
                new VVector(20,-5,0),
                new VVector(20,-10,0),
                new VVector(10,10,0),
                new VVector(10,5,0),
                new VVector(10,-5,0),
                new VVector(10,-10,0),
                new VVector(0,10,0),
                new VVector(0,5,0),
                new VVector(0,-5,0),
                new VVector(0,-10,0),
                new VVector(-10,10,0),
                new VVector(-10,5,0),
                new VVector(-10,-5,0),
                new VVector(-10,-10,0),
                new VVector(-20,10,0),
                new VVector(-20,5,0),
                new VVector(-20,-5,0),
                new VVector(-20,-10,0)
            };
            return (positions, iso);
        }

        [TestMethod()]
        public void GetMaxLatProjectionDistanceTestStructure()
        {
            (Structure target, VVector isoPos) = MatLaxProjectionDistanceTestSetupStructure();
            double expectedDistance = 20.0;
            StringBuilder expectedMessage = new StringBuilder();
            expectedMessage.AppendLine($"Iso position: ({isoPos.x:0.0}, {isoPos.y:0.0}, {isoPos.z:0.0}) mm");
            expectedMessage.AppendLine($"Max lateral dimension: {expectedDistance:0.0} mm");

            (double resultDistance, StringBuilder resultMessage) = ContourHelper.GetMaxLatProjectionDistance(target, isoPos);

            Assert.AreEqual(expectedDistance, resultDistance);
            Assert.AreEqual(expectedMessage.ToString(), resultMessage.ToString());
        }

        [TestMethod()]
        public void GetMaxLatProjectionDistanceTestVVector()
        {
            (VVector[] positions, VVector isoPos) = MatLaxProjectionDistanceTestSetupVVector();
            double expectedDistance = 20.0;
            StringBuilder expectedMessage = new StringBuilder();
            expectedMessage.AppendLine($"Iso position: ({isoPos.x:0.0}, {isoPos.y:0.0}, {isoPos.z:0.0}) mm");
            expectedMessage.AppendLine($"Max lateral dimension: {expectedDistance:0.0} mm");

            (double resultDistance, StringBuilder resultMessage) = ContourHelper.GetMaxLatProjectionDistance(positions, isoPos);

            Assert.AreEqual(expectedDistance, resultDistance);
            Assert.AreEqual(expectedMessage.ToString(), resultMessage.ToString());
        }

        [TestMethod()]
        public void GetLateralBoundingBoxForStructureTest()
        {
            (Structure target, VVector isoPos) = MatLaxProjectionDistanceTestSetupStructure();
            VVector[] expectedPts = new VVector[]
            {
                new VVector(20,10,0),
                new VVector(20,0,0),
                new VVector(20,-10,0),
                new VVector(0,-10,0),
                new VVector(-20,-10,0),
                new VVector(-20,0,0),
                new VVector(-20,10,0),
                new VVector(0,10,0),
            };
            StringBuilder expectedMessage = new StringBuilder();
            expectedMessage.AppendLine($"Lateral bounding box for structure: test");
            expectedMessage.AppendLine($"Added margin: {0.0} cm");
            expectedMessage.AppendLine($" xMax: {expectedPts.Max(p => p.x)}");
            expectedMessage.AppendLine($" xMin: {expectedPts.Min(p => p.x)}");
            expectedMessage.AppendLine($" yMax: {expectedPts.Max(p => p.y)}");
            expectedMessage.AppendLine($" yMin: {expectedPts.Min(p => p.y)}");

            (VVector[] resultPts, StringBuilder resultMessage) = ContourHelper.GetLateralBoundingBoxForStructure(target, 0.0);
            CollectionAssert.AreEqual(expectedPts, resultPts);
            Assert.AreEqual(expectedMessage.ToString(), resultMessage.ToString());
        }

        [TestMethod()]
        public void GetAllContourPointsTest()
        {
            Structure structure1 = Mock.Create<Structure>();
            Mock.Arrange(() => structure1.Id).Returns("test1");
            VVector[][][] expectedPoints = new VVector[5][][];
            for(int i = 0; i < 5; i++)
            {
                VVector[][] contours = new VVector[][]
                {
                    new VVector[]
                    {
                        new VVector(20 - 5*i,10,i),
                        new VVector(20 - 5*i,5,i),
                        new VVector(20 - 5*i,-5,i),
                        new VVector(20 - 5*i,-10,i),
                    }
                };
                Mock.Arrange(() => structure1.GetContoursOnImagePlane(i)).Returns(contours);
                expectedPoints[i] = contours;
            };

            MeshGeometry3D geo = new MeshGeometry3D();
            //retangular grid from (20,10) to (-20,-10) with points every 5 mm in y and 10 mm in x
            geo.Positions = new Point3DCollection
            {
                new Point3D(20,10,0),
                new Point3D(20,5,0),
                new Point3D(20,-5,0),
                new Point3D(20,-10,0),
                new Point3D(10,10,1),
                new Point3D(10,5,1),
                new Point3D(10,-5,1),
                new Point3D(10,-10,1),
                new Point3D(0,10,2),
                new Point3D(0,5,2),
                new Point3D(0,-5,2),
                new Point3D(0,-10,2),
                new Point3D(-10,10,3),
                new Point3D(-10,5,3),
                new Point3D(-10,-5,3),
                new Point3D(-10,-10,3),
                new Point3D(-20,10,4),
                new Point3D(-20,5,4),
                new Point3D(-20,-5,4),
                new Point3D(-20,-10,4)
            };
            Mock.Arrange(() => structure1.MeshGeometry).Returns(geo);

            StructureSet ss = Mock.Create<StructureSet>();
            Image img = Mock.Create<Image>();
            Mock.Arrange(() => img.Origin).Returns(new VVector(0, 0, 0));
            Mock.Arrange(() => img.ZRes).Returns(1);
            Mock.Arrange(() => ss.Image).Returns(img);

            int startSlice = CalculationHelper.ComputeSlice(structure1.MeshGeometry.Positions.Min(p => p.Z), ss);
            int stopSlice = CalculationHelper.ComputeSlice(structure1.MeshGeometry.Positions.Max(p => p.Z), ss);

            Assert.AreEqual(startSlice, 0);
            Assert.AreEqual(stopSlice, 4);

            ProvideUIUpdateDelegate PUUD = ProvideUIUpdate;
            VVector[][][] resultPoints = ContourHelper.GetAllContourPoints(structure1, startSlice, stopSlice, PUUD);
            for(int i = 0; i < 5; i++)
            {
                CollectionAssert.AreEqual(expectedPoints[i], resultPoints[i]);
            }
        }

        public void ProvideUIUpdate(int progress, string msg = "", bool fail = false)
        {
            Console.WriteLine(progress);
        }
    }
}