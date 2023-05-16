using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSManipulationType = VMATTBICSIAutoPlanningHelpers.Enums.TSManipulationType;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class TSManipulationTypeHelper
    {
        public static TSManipulationType GetTSManipulationType(string manipulation)
        {
            if (string.Equals(manipulation, "Crop target from structure")) return TSManipulationType.CropTargetFromStructure;
            else if (string.Equals(manipulation, "Contour overlap with target")) return TSManipulationType.ContourOverlapWithTarget;
            else if (string.Equals(manipulation, "Crop from body")) return TSManipulationType.CropFromBody;
            else return TSManipulationType.None;
        }
    }
}
