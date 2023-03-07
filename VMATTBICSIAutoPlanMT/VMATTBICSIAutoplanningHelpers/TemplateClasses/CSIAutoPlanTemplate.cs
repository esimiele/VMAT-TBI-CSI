using System;
using System.Collections.Generic;
using VMS.TPS.Common.Model.Types;

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
        // plan objectives (ONLY ONE PER TEMPLATE!)
        public List<Tuple<string, string, double, double, DoseValuePresentation>> planObj = new List<Tuple<string, string, double, double, DoseValuePresentation>> { };
        //requested items to be printed after each successful iteration of the optimization loop
        //structure id, constraint type, dose value (query type), units on dose (VOLUME WILL ALWAYS BE RELATIVE!)
        public List<Tuple<string, string, double, string>> planDoseInfo = new List<Tuple<string, string, double, string>> { };
        //requested cooler and heater structures to be added after each iteration of the optimization loop (IF CERTAIN CRITERIA ARE MET!)
        //structure id, low dose (%), high dose (%), Volume (%), priority, List of criteria that must be met for the requested TS structure to be added (all constraints are AND)
        //NOTE! THE LOW DOSE AND HIGH DOSE VALUES ARE USED FOR GENERATING HEATER STRUCTURES. 
        //FOR COOLER STRUCTURES, THE LOW DOSE VALUE IS USED TO CONVERT AN ISODOSE LEVEL TO STRUCTURE WHEREAS THE HIGH DOSE VALUE IS USED TO GENERATE THE OPTIMIZATION CONSTRAINT
        public List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> requestedTSstructures = new List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>>{};

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
