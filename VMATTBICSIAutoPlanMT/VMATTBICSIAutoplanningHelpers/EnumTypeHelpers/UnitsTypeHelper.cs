using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers
{
    public static class UnitsTypeHelper
    {
        public static Units GetUnitsType(string unit)
        {
            if (string.Equals(unit, "%") || string.Equals(unit, "relative", StringComparison.OrdinalIgnoreCase)) return Units.Percent;
            else if (string.Equals(unit, "cgy", StringComparison.OrdinalIgnoreCase)) return Units.cGy;
            else if (string.Equals(unit, "gy", StringComparison.OrdinalIgnoreCase)) return Units.Gy;
            else if (string.Equals(unit, "cc", StringComparison.OrdinalIgnoreCase)) return Units.cc;
            else return Units.None;
        }

        public static DoseValue.DoseUnit GetDoseUnit(Units units)
        {
            if (units == Units.Percent) return DoseValue.DoseUnit.Percent;
            else if (units == Units.cGy) return DoseValue.DoseUnit.cGy;
            else if (units == Units.Gy) return DoseValue.DoseUnit.Gy;
            else return DoseValue.DoseUnit.Unknown;
        }
    }
}
