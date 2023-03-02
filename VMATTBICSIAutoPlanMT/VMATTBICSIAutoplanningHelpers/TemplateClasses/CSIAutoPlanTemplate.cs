using System;
using System.Collections.Generic;

namespace VMATTBICSIAutoplanningHelpers.TemplateClasses
{
    public class CSIAutoPlanTemplate
    {
        public string templateName { get; set; }
        public double initialRxDosePerFx = 0.1;
        public int initialRxNumFx = 1;
        public double boostRxDosePerFx = 0.1;
        public int boostRxNumFx = 1;

        //structure ID, Rx dose, plan Id
        public List<Tuple<string, double, string>> targets = new List<Tuple<string, double, string>> { };
        //structure ID, sparing type, margin
        public List<Tuple<string, string, double>> spareStructures = new List<Tuple<string, string, double>> { };
        //structure, constraint type, dose cGy, volume %, priority
        public List<Tuple<string, string, double, double, int>> init_constraints = new List<Tuple<string, string, double, double, int>> { };
        public List<Tuple<string, string, double, double, int>> bst_constraints = new List<Tuple<string, string, double, double, int>> { };
        //DICOM type, structure ID
        public List<Tuple<string, string>> TS_structures = new List<Tuple<string, string>> { };

        public CSIAutoPlanTemplate()
        {
        }

        public CSIAutoPlanTemplate(int count)
        {
            templateName = String.Format("Template: {0}", count);
        }

        public CSIAutoPlanTemplate(string name)
        {
            templateName = name;
        }
    }
}
