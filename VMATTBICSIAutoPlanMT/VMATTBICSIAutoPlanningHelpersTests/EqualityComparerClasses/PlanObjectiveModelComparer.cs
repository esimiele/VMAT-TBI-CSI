using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpersTests.EqualityComparerClasses
{
    public class PlanObjectiveModelComparer : IEqualityComparer<PlanObjectiveModel>
    {
        public string Print(PlanObjectiveModel x)
        {
            return $"{x.StructureId} {x.ConstraintType} {x.QueryDose} {x.QueryDoseUnits} {x.QueryVolume} {x.QueryVolumeUnits}";
        }

        public bool Equals(PlanObjectiveModel x, PlanObjectiveModel y)
        {
            if (x == null && y == null) return true;
            else if (x == null || y == null) return false;
            else if (object.ReferenceEquals(x, y)) return true;

            return string.Equals(x.StructureId, y.StructureId)
                && x.ConstraintType == y.ConstraintType
                && CalculationHelper.AreEqual(x.QueryDose, y.QueryDose)
                && x.QueryDoseUnits == y.QueryDoseUnits
                && x.QueryVolume == y.QueryVolume
                && x.QueryVolumeUnits == y.QueryVolumeUnits;
        }

        public int GetHashCode(PlanObjectiveModel obj)
        {
            throw new NotImplementedException();
        }
    }
}
