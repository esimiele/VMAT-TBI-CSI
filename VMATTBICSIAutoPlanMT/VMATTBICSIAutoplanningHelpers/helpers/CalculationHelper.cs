using System;
using VMS.TPS.Common.Model.API;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class CalculationHelper
    {
        //tolerance of 1um
        public static bool AreEqual(double x, double y, double tolerance = 0.001)
        {
            bool equal = false;
            double squareDiff = Math.Pow(x - y, 2);
            if (Math.Sqrt(squareDiff) <= tolerance) equal = true;
            return equal;
        }

        public static double ComputeAverage(double x, double y)
        {
            return (x + y) / 2;
        }

        public static int ComputeInfSlice(double x, StructureSet ss)
        {
            return (int)((x - ss.Image.Origin.z) / ss.Image.ZRes);
        }

        public static int ComputeSupSlice(double x, StructureSet ss)
        {
            return (int)((x - ss.Image.Origin.z) / ss.Image.ZRes) + 1;
        }
    }
}
