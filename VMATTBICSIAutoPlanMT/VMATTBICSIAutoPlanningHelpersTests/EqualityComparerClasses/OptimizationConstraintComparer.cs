using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpersTests.EqualityComparerClasses
{
    public class OptimizationConstraintComparer : IEqualityComparer<OptimizationConstraintModel>
    {
        public string Print(OptimizationConstraintModel c)
        {
            return $"{c.StructureId} {c.ConstraintType} {c.QueryDose} {c.QueryDoseUnits} {c.QueryVolume} {c.QueryVolumeUnits} {c.Priority}";
        }

        public bool Equals(OptimizationConstraintModel x, OptimizationConstraintModel y)
        {
            if (x == null && y == null) return true;
            else if (x == null || y == null) return false;
            else if (object.ReferenceEquals(x, y)) return true;

            return string.Equals(x.StructureId, y.StructureId)
                && x.ConstraintType == y.ConstraintType
                && CalculationHelper.AreEqual(x.QueryDose, y.QueryDose)
                && x.QueryDoseUnits == y.QueryDoseUnits
                && CalculationHelper.AreEqual(x.QueryVolume, y.QueryVolume)
                && x.QueryVolumeUnits == y.QueryVolumeUnits
                && x.Priority == y.Priority;
        }

        public bool Equals(IEnumerable<OptimizationConstraintModel> x, IEnumerable<OptimizationConstraintModel> y)
        {
            if (x == null && y == null) return true;
            else if (x == null || y == null) return false;
            else if (object.ReferenceEquals(x, y)) return true;
            List<bool> areEqual = new List<bool> { };
            if (x.Count() == y.Count())
            {
                for (int i = 0; i < x.Count(); i++)
                {
                    areEqual.Add(Equals(x.ElementAt(i), y.ElementAt(i)));
                }
                return areEqual.All(a => a == true);
            }
            else return false;
        }

        public int GetHashCode(OptimizationConstraintModel obj)
        {
            throw new NotImplementedException();
        }
    }
}
