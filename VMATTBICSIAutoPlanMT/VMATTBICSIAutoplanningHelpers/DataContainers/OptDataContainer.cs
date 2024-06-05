using System.Collections.Generic;
using VMS.TPS.Common.Model.API;
using PlanType = VMATTBICSIAutoPlanningHelpers.Enums.PlanType;
using VMATTBICSIAutoPlanningHelpers.Models;
using System.Linq;

namespace VMATTBICSIAutoPlanningHelpers.DataContainers
{
    public class OptDataContainer
    {
        public Application Application { get; set; } = null;
        public List<ExternalPlanSetup> Plans { get; set; } = new List<ExternalPlanSetup> { };
        public StructureSet StructureSet { get; set; } = null;
        public string MRN { get; set; } = string.Empty;
        public int NumberOfIterations { get; set; } = -1;
        public double TargetCoverageNormalization { get; set; } = double.NaN;
        public double TreatmentPercentage { get; set; } = 100.0;
        public bool RunCoverageCheck { get; set; } = false;
        public bool OneMoreOptimization { get; set; } = false;
        public bool CopyAndSaveEachOptimizedPlan { get; set; } = false;
        public bool UseFlash { get; set; } = false;
        public List<PrescriptionModel> Prescriptions { get; set; } = new List<PrescriptionModel> { };
        public Dictionary<string, string> NormalizationVolumes { get; set; } = new Dictionary<string, string> { };
        public List<PlanObjectiveModel> PlanObjectives { get; set; } = new List<PlanObjectiveModel> { };
        public List<RequestedOptimizationTSStructureModel> RequestedOptimizationTSStructures { get; set; } = new List<RequestedOptimizationTSStructureModel> { };
        public List<RequestedPlanMetricModel> RequestedPlanMetrics { get; set; } = new List<RequestedPlanMetricModel> { };
        public double DecisionThreshold { get; set; } = double.NaN;
        public double LowDoseLimit { get; set; } = double.NaN;
        public bool IsDemo { get; set; } = false;
        public string LogFilePath { get; set; } = string.Empty;
        public PlanType PlanType { get; set; } = PlanType.VMAT_TBI;

        public OptDataContainer(List<ExternalPlanSetup> p,
                                List<PrescriptionModel> presc,
                                Dictionary<string, string> normVols,
                                List<PlanObjectiveModel> objectives,
                                List<RequestedOptimizationTSStructureModel> RTS,
                                List<RequestedPlanMetricModel> info,
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
            Plans = new List<ExternalPlanSetup>(p);
            StructureSet = Plans.First().StructureSet;
            MRN = StructureSet.Patient.Id;
            PlanType = type;
            NumberOfIterations = numOpt;

            //log file path
            LogFilePath = logPath;
            //run the optimization loop as a demo
            IsDemo = demo;
            //what percentage of the target volume should recieve the prescription dose
            TargetCoverageNormalization = targetNorm;
            //threshold to determine if the dose or the priority should be adjusted for an optimization constraint. This threshold indicates the relative cost, above which, the dose will be decreased for an optimization constraint.
            //Below the threshold, the priority will be increased for an optimization constraint. 
            DecisionThreshold = thres;
            //the low dose limit is used to prevent the algorithm (below) from pushing the dose constraints too low. Basically, if the proposed new dose (i.e., calculated dose from the previous optimization minus the proposed dose decrement)
            //is greater than the prescription dose times the lowDoseLimit, the change is accepted and the dose constraint is modified. Otherwise, the optimization constraint is NOT altered
            LowDoseLimit = lowDose;
            //copy additional optimization loop parameters
            RunCoverageCheck = coverMe;
            OneMoreOptimization = unoMas;
            CopyAndSaveEachOptimizedPlan = copyAndSave;
            UseFlash = flash;
            Application = a;

            Prescriptions = presc;
            NormalizationVolumes = normVols;
            PlanObjectives = objectives;
            RequestedOptimizationTSStructures = RTS;
            RequestedPlanMetrics = info;
        }
    }
}
