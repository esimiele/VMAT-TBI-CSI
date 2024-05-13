using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers
{
    public static class InequalityOperatorHelper
    {
        public static InequalityOperator GetInequalityOperator(string op)
        {
            op = op.Trim();
            if (op == "<") return InequalityOperator.LessThan;
            else if (op == ">") return InequalityOperator.GreaterThan;
            else if (op == "<=") return InequalityOperator.LessThanOrEqualTo;
            else if (op == ">=") return InequalityOperator.GreaterThanOrEqualTo;
            else if (op == "=") return InequalityOperator.Equal;
            else return InequalityOperator.None;
        }
    }
}
