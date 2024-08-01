using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers;

namespace VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers.Tests
{
    [TestClass()]
    public class ExportFormatTypeHelperTests
    {
        [TestMethod()]
        public void GetExportFormatTypeTest()
        {
            string format = "dcm";
            Assert.AreEqual(ExportFormatTypeHelper.GetExportFormatType(format), ImgExportFormat.DICOM);

            format = "png";
            Assert.AreEqual(ExportFormatTypeHelper.GetExportFormatType(format), ImgExportFormat.PNG);

            format = "dcn";
            Assert.AreNotEqual(ExportFormatTypeHelper.GetExportFormatType(format), ImgExportFormat.DICOM);
        }
    }
}