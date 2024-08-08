using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Interfaces;
using VMS.TPS.Common.Model.API;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class PlanObjectivesDeviationModel : IPlanQualityEvaluation
    {
        public Structure Structure { get; set; } = null;
        public DVHData DVHData { get; set; } = null;
        public double DoseDifferenceSquared { get; set; } = double.NaN;
        public bool ObjectiveMet { get; set; } = false;

        public PlanObjectivesDeviationModel() { }
        public PlanObjectivesDeviationModel(Structure structure, DVHData dVHData, double doseDifferenceSquared, bool met)
        {
            Structure = structure;
            DVHData = dVHData;
            DoseDifferenceSquared = doseDifferenceSquared;
            ObjectiveMet = met;
        }
    }
}
