using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.Interfaces
{
    public interface IPlanConstraint
    {
        string StructureId { get; set; }
        OptimizationObjectiveType ConstraintType { get; set; }
        double QueryDose { get; set; }
        Units QueryDoseUnits { get; set; }
        double QueryVolume { get; set; }
        Units QueryVolumeUnits { get; set; }
    }
}
