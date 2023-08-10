using System;
using VMS.TPS.Common.Model.API;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class CalculationHelper
    {
        /// <summary>
        /// Determine if x and y lengths are equivalent within tolerance (default value of 1 um)
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="tolerance"></param>
        /// <returns></returns>
        public static bool AreEqual(double x, double y, double tolerance = 0.001)
        {
            bool equal = false;
            double squareDiff = Math.Pow(x - y, 2);
            if (Math.Sqrt(squareDiff) <= tolerance) equal = true;
            return equal;
        }

        /// <summary>
        /// Compute mean of x and y (not included in Math library)
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static double ComputeAverage(double x, double y)
        {
            return (x + y) / 2;
        }

        /// <summary>
        /// Helper method to compute which CT slice a given z position is located
        /// </summary>
        /// <param name="z"></param>
        /// <param name="ss"></param>
        /// <returns></returns>
        public static int ComputeSlice(double z, StructureSet ss)
        {
            return (int)Math.Round((z - ss.Image.Origin.z) / ss.Image.ZRes);
        }
    }
}
