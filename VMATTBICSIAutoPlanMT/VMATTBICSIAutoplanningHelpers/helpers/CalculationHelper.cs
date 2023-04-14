using System;

namespace VMATTBICSIAutoplanningHelpers.Helpers
{
    public class CalculationHelper
    {
        //tolerance of 1um
        public bool AreEqual(double x, double y, double tolerance = 0.001)
        {
            bool equal = false;
            double squareDiff = Math.Pow(x - y, 2);
            if (Math.Sqrt(squareDiff) <= tolerance) equal = true;
            return equal;
        }
    }
}
