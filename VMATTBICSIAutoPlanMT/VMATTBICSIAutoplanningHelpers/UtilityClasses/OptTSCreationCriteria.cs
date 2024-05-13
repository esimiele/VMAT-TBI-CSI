using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Helpers;

namespace VMATTBICSIAutoPlanningHelpers.UtilityClasses
{
    public class OptTSCreationCriteria : RequestedPlanMetric
    {
        public bool CreateForFinalOptimization { get; set; } = false;
        public double Limit { get; set; } = double.NaN;
        public InequalityOperator Operator { get; set; } = InequalityOperator.None;
        public double QueryResult { get; set; } = double.NaN;
        public OptTSCreationCriteria(bool finalOpt) 
        {
            CreateForFinalOptimization = finalOpt;
        }

        public OptTSCreationCriteria(DVHMetric dVHMetric, double queryVal, Units queryUnits, InequalityOperator op, double lim, Units resultUnits) 
        {
            DVHMetric = dVHMetric;
            QueryValue = queryVal;
            QueryUnits = queryUnits;
            QueryResultUnits = resultUnits;
            Limit = lim;
            QueryResultUnits = resultUnits;
        }

        public OptTSCreationCriteria(DVHMetric dVHMetric, InequalityOperator op, double lim, Units resultUnits)
        {
            DVHMetric = dVHMetric;
            Operator = op;
            Limit = lim;
            QueryResultUnits = resultUnits;
        }

        public bool CriteriaMet(bool isFinalOpt)
        {
            if(double.IsNaN(Limit) || double.IsNaN(QueryResult) || Operator == InequalityOperator.None) return false;
            if(CreateForFinalOptimization && isFinalOpt) return true;
            if (Operator == InequalityOperator.Equal) return CalculationHelper.AreEqual(QueryResult, Limit);
            else if (Operator == InequalityOperator.GreaterThan) return QueryResult > Limit;
            else if (Operator == InequalityOperator.LessThan) return QueryResult < Limit;
            else if (Operator == InequalityOperator.GreaterThanOrEqualTo) return QueryResult >= Limit;
            else if (Operator == InequalityOperator.LessThanOrEqualTo) return QueryResult <= Limit;
            else return false;
        }
    }
}
