using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Interfaces;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class OptimizationConstraintModel : IPlanConstraint
    {
        public string StructureId { get; set; } = string.Empty;
        public OptimizationObjectiveType ConstraintType { get; set; } = OptimizationObjectiveType.None;
        public double QueryDose { get; set; } = double.NaN;
        public Units QueryDoseUnits { get; set; } = Units.None;
        public double QueryVolume { get; set; } = double.NaN;
        public Units QueryVolumeUnits { get; set; } = Units.None;
        public int Priority { get; set; } = -1;

        public OptimizationConstraintModel(string structureId, OptimizationObjectiveType constraintType, double queryDose, Units queryDoseUnits, double queryVolume, int priority, Units queryVolumeUnits = Units.Percent)
        {
            StructureId = structureId;
            ConstraintType = constraintType;
            QueryDose = queryDose;
            QueryDoseUnits = queryDoseUnits;
            QueryVolume = queryVolume;
            Priority = priority;
            QueryVolumeUnits = queryVolumeUnits;
        }
    }
}
