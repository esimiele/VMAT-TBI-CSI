using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;

namespace VMATTBICSIAutoPlanningHelpers.Interfaces
{
    public interface IPlanQualityEvaluation
    { 
        Structure Structure { get; set; }
        DVHData DVHData { get; set; }
        double DoseDifferenceSquared { get; set; }
    }
}
