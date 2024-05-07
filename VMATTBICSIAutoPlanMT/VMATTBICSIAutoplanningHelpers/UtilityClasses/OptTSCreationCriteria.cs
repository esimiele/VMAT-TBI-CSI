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
        public OptTSCreationCriteria(string structureId, DVHMetric dVHMetric, double queryVal, Units queryUnits, Units resultUnits) 
        {
            StructureId = structureId;
            DVHMetric = dVHMetric;
            QueryValue = queryVal;
            QueryUnits = queryUnits;
            QueryResultUnits = resultUnits;
        }

        public OptTSCreationCriteria(string structureId, DVHMetric dVHMetric, Units resultUnits)
        {
            StructureId = structureId;
            DVHMetric = dVHMetric;
            QueryResultUnits = resultUnits;
        }

        public bool CriteriaMet()
        {
            if(double.IsNaN(Limit) || double.IsNaN(QueryResult) || Operator == InequalityOperator.None) return false;
            if (Operator == InequalityOperator.Equal) return CalculationHelper.AreEqual(QueryResult, Limit);
            else if (Operator == InequalityOperator.GreaterThan) return QueryResult > Limit;
            else if (Operator == InequalityOperator.LessThan) return QueryResult < Limit;
            else if (Operator == InequalityOperator.GreaterThanOrEqualTo) return QueryResult >= Limit;
            else if (Operator == InequalityOperator.LessThanOrEqualTo) return QueryResult <= Limit;
            else return false;
        }
    }
}
