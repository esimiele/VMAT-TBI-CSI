using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    [TestClass()]
    public class ExportFormatTypeHelperTests
    {
        [TestMethod()]
        public void GetExportFormatTypeTestPass()
        {
            string format = "dcm";
            Assert.AreEqual(ExportFormatTypeHelper.GetExportFormatType(format), ImgExportFormat.DICOM);
        }

        [TestMethod()]
        public void GetExportFormatTypeTestPassPNG()
        {
            string format = "png";
            Assert.AreEqual(ExportFormatTypeHelper.GetExportFormatType(format), ImgExportFormat.PNG);
        }

        [TestMethod()]
        public void GetExportFormatTypeTestFail()
        {
            string format = "dcn";
            Assert.AreNotEqual(ExportFormatTypeHelper.GetExportFormatType(format), ImgExportFormat.DICOM);
        }
    }
}