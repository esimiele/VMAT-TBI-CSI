using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class FieldJunctionModel
    {
        public double OverlapCenterPositionZ { get; set; } = double.NaN;
        public int NumberOfCTSlices { get; set; } = -1;
        public int StartSlice { get; set; } = -1;
        public Structure JunctionStructure { get; set; } = null;

        public FieldJunctionModel(double center, int numSlice, int start) 
        {
            OverlapCenterPositionZ = center;
            NumberOfCTSlices = numSlice;
            StartSlice = start;
        }
    }
}
