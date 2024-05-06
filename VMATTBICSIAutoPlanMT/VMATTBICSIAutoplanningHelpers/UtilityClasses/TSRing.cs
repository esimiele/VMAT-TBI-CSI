using EvilDICOM.RT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMATTBICSIAutoPlanningHelpers.UtilityClasses
{
    public class TSRing
    {
        public string TargetId { get; set; } = string.Empty;
        public double MarginFromTargetInCM { get; set; } = double.NaN;
        public double RingThicknessInCM {  get; set; } = double.NaN;
        public double DoseLevel { get; set; } = double.NaN; 

        public TSRing(string id, double margin, double thickness, double dose) 
        {
            TargetId = id;
            MarginFromTargetInCM = margin;
            RingThicknessInCM = thickness;
            DoseLevel = dose;
        }
        ////target to create ring from, margin, thickness, dose level (cGy)
        //private List<Tuple<string, double, double, double>> createRings = new List<Tuple<string, double, double, double>> { };
    }
}
