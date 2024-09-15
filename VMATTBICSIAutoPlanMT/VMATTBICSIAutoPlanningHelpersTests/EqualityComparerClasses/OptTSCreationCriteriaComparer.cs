using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpersTests.EqualityComparerClasses
{
    public class OptTSCreationCriteriaComparer : IEqualityComparer<OptTSCreationCriteriaModel>
    {
        public string Print(OptTSCreationCriteriaModel x)
        {
            return $"{x.CreateForFinalOptimization} {x.DVHMetric} {x.Operator} {x.QueryValue} {x.QueryUnits} {x.Limit} {x.QueryResultUnits}";
        }

        public bool Equals(OptTSCreationCriteriaModel x, OptTSCreationCriteriaModel y)
        {
            if (x == null && y == null) return true;
            else if (x == null || y == null) return false;
            else if (object.ReferenceEquals(x, y)) return true;

            return x.CreateForFinalOptimization == y.CreateForFinalOptimization
                && x.DVHMetric == y.DVHMetric
                && x.Operator == y.Operator
                && ((double.IsNaN(x.QueryValue) && double.IsNaN(y.QueryValue)) || CalculationHelper.AreEqual(x.QueryValue, y.QueryValue))
                && x.QueryUnits == y.QueryUnits
                && ((double.IsNaN(x.Limit) && double.IsNaN(y.Limit)) || CalculationHelper.AreEqual(x.Limit, y.Limit))
                && x.QueryResultUnits == y.QueryResultUnits;
        }

        public bool Equals(IEnumerable<OptTSCreationCriteriaModel> x, IEnumerable<OptTSCreationCriteriaModel> y)
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

        public int GetHashCode(OptTSCreationCriteriaModel obj)
        {
            throw new NotImplementedException();
        }
    }
}
