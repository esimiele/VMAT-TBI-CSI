using EvilDICOM.RT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class TSRingStructureModel
    {
        public string TargetId { get; set; } = string.Empty;
        public string RingId { get; set; } = string.Empty;
        public double MarginFromTargetInCM { get; set; } = double.NaN;
        public double RingThicknessInCM {  get; set; } = double.NaN;
        public double DoseLevel { get; set; } = double.NaN; 

        public TSRingStructureModel(string id, double margin, double thickness, double dose) 
        {
            TargetId = id;
            MarginFromTargetInCM = margin;
            RingThicknessInCM = thickness;
            DoseLevel = dose;
        }

        public TSRingStructureModel(TSRingStructureModel r)
        {
            TargetId = r.TargetId;
            RingId = r.RingId;
            MarginFromTargetInCM = r.MarginFromTargetInCM;
            RingThicknessInCM = r.RingThicknessInCM;
            DoseLevel = r.DoseLevel;
        }
    }
}
