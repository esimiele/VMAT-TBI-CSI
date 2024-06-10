using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.Models;
using VMS.TPS.Common.Model.API;

namespace VMATTBICSIAutoPlanningHelpersTests.EqualityComparerClasses
{
    public class PlanTargetModelComparer : IEqualityComparer<PlanTargetsModel>
    {
        public bool Equals(PlanTargetsModel x, PlanTargetsModel y)
        {
            if (x == null && y == null) return true;
            else if (x == null || y == null) return false;
            else if (object.ReferenceEquals(x, y)) return true;
            TargetModelComparer comparer = new TargetModelComparer();

            return string.Equals(x.PlanId, y.PlanId)
                && comparer.Equals(x.Targets, y.Targets);
        }

        public int GetHashCode(PlanTargetsModel obj)
        {
            throw new NotImplementedException();
        }
    }

    public class TargetModelComparer : IEqualityComparer<TargetModel>
    {
        public bool Equals(IEnumerable<TargetModel> x, IEnumerable<TargetModel> y)
        {
            if (x == null && y == null) return true;
            else if (x == null || y == null) return false;
            else if (object.ReferenceEquals(x, y)) return true;

            List<bool> areEqual = new List<bool>();
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

        public bool Equals(TargetModel x, TargetModel y)
        {
            if (x == null && y == null) return true;
            else if (x == null || y == null) return false;
            else if (object.ReferenceEquals(x, y)) return true;

            return string.Equals(x.TargetId, y.TargetId)
                && CalculationHelper.AreEqual(x.TargetRxDose, y.TargetRxDose)
                && string.Equals(x.TsTargetId, y.TsTargetId);
        }

        public int GetHashCode(TargetModel obj)
        {
            throw new NotImplementedException();
        }
    }
}
