﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMATTBICSIAutoPlanningHelpers.UtilityClasses
{
    public class RequestedTSStructure
    {
        public string DICOMType { get; set; } = string.Empty;
        public string StructureId { get; set; } = string.Empty;

        public RequestedTSStructure(string dICOMType, string structureId)
        {
            DICOMType = dICOMType;
            StructureId = structureId;
        }
    }
}