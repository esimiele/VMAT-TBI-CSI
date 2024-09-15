using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.Models;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpersTests.EqualityComparerClasses
{
    internal class IsocenterModelComparer : IEqualityComparer<IsocenterModel>
    {
        public string Print(IsocenterModel x)
        {
            return $"{x.IsocenterId} ({x.IsocenterPosition.x}, {x.IsocenterPosition.y}, {x.IsocenterPosition.z}) {x.NumberOfBeams}";
        }

        public bool Equals(IEnumerable<IsocenterModel> x, IEnumerable<IsocenterModel> y)
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
        public bool Equals(IsocenterModel x, IsocenterModel y)
        {
            if (x == null && y == null) return true;
            else if (x == null || y == null) return false;
            else if (object.ReferenceEquals(x, y)) return true;

            return string.Equals(x.IsocenterId, y.IsocenterId)
                && ArePositionsEqual(x.IsocenterPosition, y.IsocenterPosition)
                && x.NumberOfBeams == y.NumberOfBeams;
        }

        public int GetHashCode(IsocenterModel obj)
        {
            throw new NotImplementedException();
        }

        public bool ArePositionsEqual(VVector x, VVector y)
        {
            return CalculationHelper.AreEqual(x.x, y.x)
                && CalculationHelper.AreEqual(x.y, y.y)
                && CalculationHelper.AreEqual(x.z, y.z);
        }
    }
}
