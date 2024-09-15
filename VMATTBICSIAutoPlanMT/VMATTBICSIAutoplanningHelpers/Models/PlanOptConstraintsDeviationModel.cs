using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Interfaces;
using VMS.TPS.Common.Model.API;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class PlanOptConstraintsDeviationModel : IPlanQualityEvaluation
    {
        public Structure Structure { get; set; } = null;
        public DVHData DVHData { get; set; } = null;
        public double DoseDifferenceSquared { get; set; } = double.NaN;
        public double OptimizationCost { get; set; } = double.NaN;
        public double DoseConstraint { get; set; } = double.NaN;
        public int Prioirty { get; set; } = -1;

        public PlanOptConstraintsDeviationModel() { }

        public PlanOptConstraintsDeviationModel(Structure s, DVHData d, double constraint ,double doseDiff, double cost, int prioirty)
        {
            Structure = s;
            DVHData = d;
            DoseDifferenceSquared = doseDiff;
            OptimizationCost = cost;
            DoseConstraint = constraint;
            Prioirty = prioirty;
        }
    }
}
