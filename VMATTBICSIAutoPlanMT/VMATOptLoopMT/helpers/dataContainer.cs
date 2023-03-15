using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIOptLoopMT.helpers
{
    //data structure to hold all the relevant information
    public struct dataContainer
    {
        //data members
        public Application app;
        public List<ExternalPlanSetup> plans;
        public StructureSet selectedSS;
        public string id;
        public int numOptimizations;
        public double targetVolCoverage;
        public double relativeDose;
        public bool runCoverageCheck;
        public bool oneMoreOpt;
        public bool copyAndSavePlanItr;
        public bool useFlash;
        public List<Tuple<string, string, int, DoseValue, double>> prescriptions;
        public List<Tuple<string, string, double, double, int>> optParams;
        public List<Tuple<string, string, double, double, DoseValuePresentation>> planObj;
        public List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> requestedTSstructures;
        public List<Tuple<string, string, double, string>> requestedPlanDoseInfo;
        public double threshold;
        public double lowDoseLimit;
        public bool isDemo;
        public string logFilePath;
        //simple method to automatically assign/initialize the above data members
        public void construct(List<ExternalPlanSetup> p, 
                              List<Tuple<string, string, int, DoseValue, double>> presc,
                              List<Tuple<string, string, double, double, int>> param, 
                              List<Tuple<string, string, double, double, DoseValuePresentation>> objectives, 
                              List<Tuple<string, double, double, double, int, 
                              List<Tuple<string, double, string, double>>>> RTS,
                              List<Tuple<string,string,double,string>> info,
                              double targetNorm, 
                              int numOpt, 
                              bool coverMe, 
                              bool unoMas, 
                              bool copyAndSave, 
                              bool flash, 
                              double thres, 
                              double lowDose, 
                              bool demo, 
                              string logPath, 
                              Application a)
        {
            plans = new List<ExternalPlanSetup>(p);
            selectedSS = plans.First().StructureSet;
            id = plans.First().Course.Patient.Id;
            numOptimizations = numOpt;

            //log file path
            logFilePath = logPath;
            //run the optimization loop as a demo
            isDemo = demo;
            //what percentage of the target volume should recieve the prescription dose
            targetVolCoverage = targetNorm;
            //dose relative to the prescription dose expressed as a percent (used for normalization)
            relativeDose = 100.0;
            //threshold to determine if the dose or the priority should be adjusted for an optimization constraint. This threshold indicates the relative cost, above which, the dose will be decreased for an optimization constraint.
            //Below the threshold, the priority will be increased for an optimization constraint. 
            threshold = thres;
            //the low dose limit is used to prevent the algorithm (below) from pushing the dose constraints too low. Basically, if the proposed new dose (i.e., calculated dose from the previous optimization minus the proposed dose decrement)
            //is greater than the prescription dose times the lowDoseLimit, the change is accepted and the dose constraint is modified. Otherwise, the optimization constraint is NOT altered
            lowDoseLimit = lowDose;
            //copy additional optimization loop parameters
            runCoverageCheck = coverMe;
            oneMoreOpt = unoMas;
            copyAndSavePlanItr = copyAndSave;
            useFlash = flash;
            app = a;

            prescriptions = presc;
            optParams = param;
            planObj = objectives;
            requestedTSstructures = RTS;
            requestedPlanDoseInfo = info;
        }
    }
}
