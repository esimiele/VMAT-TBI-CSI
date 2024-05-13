using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using PlanType = VMATTBICSIAutoPlanningHelpers.Enums.PlanType;
using OptimizationObjectiveType = VMATTBICSIAutoPlanningHelpers.Enums.OptimizationObjectiveType;
using VMATTBICSIAutoPlanningHelpers.UtilityClasses;

namespace VMATTBICSIAutoPlanningHelpers.Structs
{
    /// <summary>
    /// Data structure to hold all the relevant optimization loop configuration information
    /// </summary>
    public struct OptDataContainer
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
        public List<Prescription> prescriptions;
        public Dictionary<string, string> normalizationVolumes;
        public List<PlanObjective> planObj;
        //public List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> requestedTSstructures;
        public List<RequestedOptimizationTSStructure> requestedTSStructures;
        public List<RequestedPlanMetric> requestedPlanMetrics;
        public double threshold;
        public double lowDoseLimit;
        public bool isDemo;
        public string logFilePath;
        public PlanType planType;

        //simple method to automatically assign/initialize the above data members
        public void Construct(List<ExternalPlanSetup> p, 
                              List<Prescription> presc,
                              Dictionary<string,string> normVols,
                              List<PlanObjective> objectives, 
                              List<RequestedOptimizationTSStructure> RTS,
                              List<RequestedPlanMetric> info,
                              PlanType type,
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
            planType = type;
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
            normalizationVolumes = normVols;
            planObj = objectives;
            requestedTSStructures = RTS;
            requestedPlanMetrics = info;
        }
    }
}
