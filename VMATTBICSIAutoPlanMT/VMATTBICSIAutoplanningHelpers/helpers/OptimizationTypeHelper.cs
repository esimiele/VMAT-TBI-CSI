using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using OptimizationObjectiveType = VMATTBICSIAutoplanningHelpers.Enums.OptimizationObjectiveType;

namespace VMATTBICSIAutoplanningHelpers.Helpers
{
    //placing helper method here to assist with conversion to own enum type
    public static class OptimizationTypeHelper
    {
        //convert from Varian API enum to internal optimization objective type enum
        public static OptimizationObjectiveType GetObjectiveType(OptimizationPointObjective pt)
        {
            if (pt.Operator == OptimizationObjectiveOperator.Upper) return OptimizationObjectiveType.Upper;
            else if (pt.Operator == OptimizationObjectiveOperator.Lower) return OptimizationObjectiveType.Lower;
            else if (pt.Operator == OptimizationObjectiveOperator.Exact) return OptimizationObjectiveType.Exact;
            else return OptimizationObjectiveType.None;
        }

        //convert from string representation of optimization objective type to internal optimization objective type enum
        public static OptimizationObjectiveType GetObjectiveType(string op)
        {
            if (op == "Upper") return OptimizationObjectiveType.Upper;
            else if (op == "Lower") return OptimizationObjectiveType.Lower;
            else if (op == "Mean") return OptimizationObjectiveType.Mean;
            else if (op == "Exact") return OptimizationObjectiveType.Exact;
            else return OptimizationObjectiveType.None;
        }

        //convert back to Varian API enum for optimization objective type enum
        public static OptimizationObjectiveOperator GetObjectiveOperator(OptimizationObjectiveType op)
        {
            if (op == OptimizationObjectiveType.Upper) return OptimizationObjectiveOperator.Upper;
            else if (op == OptimizationObjectiveType.Lower) return OptimizationObjectiveOperator.Lower;
            else if (op == OptimizationObjectiveType.Exact) return OptimizationObjectiveOperator.Exact;
            else return OptimizationObjectiveOperator.None;
        }
    }
}
