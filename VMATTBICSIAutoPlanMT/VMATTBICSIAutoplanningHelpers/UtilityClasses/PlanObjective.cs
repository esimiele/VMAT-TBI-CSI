﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.UtilityClasses
{
    public class PlanObjective
    {
        //structure, constraint type, dose, relative volume, dose value presentation (unless otherwise specified)
        public string StructureId { get; set; } = string.Empty;
        public OptimizationObjectiveType ConstraintType { get; set; } = OptimizationObjectiveType.None;
        public double QueryDose { get; set; } = double.NaN;
        public Units QueryDoseUnits { get; set; } = Units.None;
        public double QueryVolume { get; set; } = double.NaN;
        public Units QueryVolumeUnits { get; set; } = Units.None;

        public PlanObjective(string structureId, OptimizationObjectiveType constraintType, double queryDose, Units queryDoseUnits, double queryVolume, Units queryVolumeUnits = Units.Percent)
        {
            StructureId = structureId;
            ConstraintType = constraintType;
            QueryDose = queryDose;
            QueryDoseUnits = queryDoseUnits;
            QueryVolume = queryVolume;
            QueryVolumeUnits = queryVolumeUnits;
        }
    }
}
