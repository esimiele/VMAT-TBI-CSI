using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpersTests.EqualityComparerClasses
{
    public class RingModelComparer : IEqualityComparer<TSRingStructureModel>
    {
        public bool Equals(TSRingStructureModel x, TSRingStructureModel y)
        {
            if (x == null && y == null) return true;
            else if (x == null || y == null) return false;
            else if (object.ReferenceEquals(x, y)) return true;

            return string.Equals(x.TargetId, y.TargetId)
                && CalculationHelper.AreEqual(x.MarginFromTargetInCM, y.MarginFromTargetInCM)
                && CalculationHelper.AreEqual(x.RingThicknessInCM, y.RingThicknessInCM)
                && CalculationHelper.AreEqual(x.DoseLevel, y.DoseLevel);
        }

        public int GetHashCode(TSRingStructureModel obj)
        {
            throw new NotImplementedException();
        }
    }
}
