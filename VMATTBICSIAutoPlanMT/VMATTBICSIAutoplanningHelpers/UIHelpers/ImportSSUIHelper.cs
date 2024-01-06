using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class ImportSSUIHelper
    {
        public static void PrintImportSSInfo()
        {
            string message = "Launch the import listener script to try and import the auto-contoured structure set." + Environment.NewLine;
            message += "If the import listener does not find the structure set within the first 30 seconds, the structure set likely does not exist!";
            MessageBox.Show(message);
        }
    }
}
