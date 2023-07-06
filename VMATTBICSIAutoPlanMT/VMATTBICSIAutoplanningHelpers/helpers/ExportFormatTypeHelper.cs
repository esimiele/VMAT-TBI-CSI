using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class ExportFormatTypeHelper
    {
        public static ImgExportFormat GetExportFormatType(string type)
        {
            if (string.Equals(type, "dcm")) return ImgExportFormat.DICOM;
            else return ImgExportFormat.PNG;
        }
    }
}
