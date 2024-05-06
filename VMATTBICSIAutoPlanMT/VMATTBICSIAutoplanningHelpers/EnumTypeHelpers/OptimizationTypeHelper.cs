using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using OptimizationObjectiveType = VMATTBICSIAutoPlanningHelpers.Enums.OptimizationObjectiveType;

namespace VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers
{
    public static class OptimizationTypeHelper
    {
        /// <summary>
        /// Helper method to convert from Varian API enum to internal optimization objective type enum
        /// </summary>
        /// <param name="pt"></param>
        /// <returns></returns>
        public static OptimizationObjectiveType GetObjectiveType(OptimizationPointObjective pt)
        {
            if (pt.Operator == OptimizationObjectiveOperator.Upper) return OptimizationObjectiveType.Upper;
            else if (pt.Operator == OptimizationObjectiveOperator.Lower) return OptimizationObjectiveType.Lower;
            else if (pt.Operator == OptimizationObjectiveOperator.Exact) return OptimizationObjectiveType.Exact;
            else return OptimizationObjectiveType.None;
        }

        /// <summary>
        /// Helper method to convert from string representation of optimization objective type to internal optimization objective type enum
        /// </summary>
        /// <param name="op"></param>
        /// <returns></returns>
        public static OptimizationObjectiveType GetObjectiveType(string op)
        {
            if (string.Equals(op, "Upper")) return OptimizationObjectiveType.Upper;
            else if (string.Equals(op, "Lower")) return OptimizationObjectiveType.Lower;
            else if (string.Equals(op, "Mean")) return OptimizationObjectiveType.Mean;
            else if (string.Equals(op, "Exact")) return OptimizationObjectiveType.Exact;
            else return OptimizationObjectiveType.None;
        }

        /// <summary>
        /// Helper method to convert from the internal optimization objective type enum back to Varian API enum 
        /// </summary>
        /// <param name="op"></param>
        /// <returns></returns>
        public static OptimizationObjectiveOperator GetObjectiveOperator(OptimizationObjectiveType op)
        {
            if (op == OptimizationObjectiveType.Upper) return OptimizationObjectiveOperator.Upper;
            else if (op == OptimizationObjectiveType.Lower) return OptimizationObjectiveOperator.Lower;
            else if (op == OptimizationObjectiveType.Exact) return OptimizationObjectiveOperator.Exact;
            else return OptimizationObjectiveOperator.None;
        }
    }
}
