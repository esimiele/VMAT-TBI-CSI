using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class TargetModel
    {
        public string TargetId { get; set; } = string.Empty;
        public double TargetRxDose { get; set; } = double.NaN;
        public string TsTargetId { get; set; } = string.Empty;

        public TargetModel(string target, double dose, string ts)
        {
            TargetId = target;
            TargetRxDose = dose;
            TsTargetId = ts;
        }

        public TargetModel(string target, double dose)
        {
            TargetId = target;
            TargetRxDose = dose;
        }
    }
}
