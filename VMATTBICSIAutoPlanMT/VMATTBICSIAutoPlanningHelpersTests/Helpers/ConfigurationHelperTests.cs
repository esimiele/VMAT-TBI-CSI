using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.Types;

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
            Tuple<string, string> expected = Tuple.Create("CONTROL", "TS_Eyes");
            Tuple<string,string> result = ConfigurationHelper.ParseCreateTS(testCreateTS);
            Assert.AreEqual(expected, result);
        }

        [TestMethod()]
        public void ParseCreateTSTestFail1()
        {
            string testCreateTS = "create TS{PTV,TS_Eyes}";
            Tuple<string, string> expected = Tuple.Create("CONTROL", "TS_Eyes");
            Tuple<string, string> result = ConfigurationHelper.ParseCreateTS(testCreateTS);
            Assert.AreNotEqual(expected, result);
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
}