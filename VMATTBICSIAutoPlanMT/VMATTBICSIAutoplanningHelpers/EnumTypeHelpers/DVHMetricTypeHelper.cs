using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers
{
    public static class DVHMetricTypeHelper
    {
        public static DVHMetric GetDVHMetricType(string metricType)
        {
            if (string.Equals(metricType, "dmax", StringComparison.OrdinalIgnoreCase)) return DVHMetric.Dmax;
            else if (string.Equals(metricType, "dmin", StringComparison.OrdinalIgnoreCase)) return DVHMetric.Dmin;
            else if (string.Equals(metricType, "doseatvolume", StringComparison.OrdinalIgnoreCase)) return DVHMetric.DoseAtVolume;
            else if (string.Equals(metricType, "volumeatdose", StringComparison.OrdinalIgnoreCase)) return DVHMetric.VolumeAtDose;
            else return DVHMetric.None;
        }
    }
}
