using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIOptLoopMT.baseClasses;
using VMATTBICSIOptLoopMT.helpers;
using VMATTBICSIAutoplanningHelpers.Helpers;

namespace VMATTBICSIOptLoopMT.VMAT_TBI
{
    class VMATTBIOptimization : optimizationLoopBase
    {
        public VMATTBIOptimization(dataContainer _d)
        {
            _data = _d;
            InitializeLogPathAndName();
        }

        public override bool Run()
        {
            try
            {
                SetAbortUIStatus("Runnning");
                PrintRunSetupInfo(_data.plans);
                //preliminary checks
                if (PreliminaryChecksSSAndImage(_data.selectedSS, new TargetsHelper().GetAllTargets(_data.prescriptions))) return true;
                if (PreliminaryChecksCouch(_data.selectedSS)) return true;
                if (PreliminaryChecksSpinningManny(_data.selectedSS)) return true;
                if (_checkSupportStructures)
                {
                    if (CheckSupportStructures(_data.plans.First().Course.Patient.Courses.ToList(), _data.selectedSS)) return true;
                }
                if (PreliminaryChecksPlans(_data.plans)) return true;

                if (_data.isDemo || !_data.runCoverageCheck) ProvideUIUpdate(" Skipping coverage check! Moving on to optimization loop!");
                else
                {
                    foreach (ExternalPlanSetup itr in _data.plans)
                    {
                        if (RunCoverageCheck(_data.optParams, itr, _data.relativeDose, _data.targetVolCoverage, _data.useFlash)) return true;
                        ProvideUIUpdate(String.Format(" Coverage check for plan {0} completed!",itr.Id));
                    }
                }
                ProvideUIUpdate(String.Format(" Commencing optimization loop!"));
                if (RunOptimizationLoop(_data.plans)) return true;
                OptimizationLoopFinished();
            }
            catch (Exception e) { ProvideUIUpdate(String.Format("{0}", e.Message), true); return true; }
            return false;
        }

        protected void CalculateNumberOfItemsToComplete()
        {
            overallCalcItems = 4;
            overallCalcItems += _data.plans.Count;
            if (_data.runCoverageCheck) overallCalcItems += 4 * _data.plans.Count;
            int optLoopItems = 6 * _data.numOptimizations * _data.plans.Count;
            if (_data.oneMoreOpt) optLoopItems += 3;
            overallCalcItems += optLoopItems;
        }

        #region coverage check
        private bool RunCoverageCheck(List<Tuple<string, string, double, double, int>> optParams, ExternalPlanSetup plan, double relativeDose, double targetVolCoverage, bool useFlash)
        {
            ProvideUIUpdate(" Running coverage check..." + Environment.NewLine);
            //zero all optimization objectives except those in the target
            List<Tuple<string, string, double, double, int>> targetOnlyObj = new List<Tuple<string, string, double, double, int>> { };

            ProvideUIUpdate(GetOptimizationObjectivesHeader());

            int percentCompletion = 0;
            int calcItems = 5;

            foreach (Tuple<string, string, double, double, int> opt in optParams)
            {
                int priority = 0;
                if (opt.Item1.ToLower().Contains("ptv") || opt.Item1.ToLower().Contains("ts_jnx")) priority = opt.Item5;
                targetOnlyObj.Add(Tuple.Create(opt.Item1, opt.Item2, opt.Item3, opt.Item4, priority));
                //record the optimization constraints for each structure after zero-ing the priorities. This information will be reported to the user in a progress update
                ProvideUIUpdate(String.Format(" {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", opt.Item1, opt.Item2, opt.Item3, opt.Item4, priority));
            }
            //update the constraints and provide an update to the user
            UpdateConstraints(targetOnlyObj, plan);
            ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems));

            //run one optimization with NO intermediate dose.
            if (OptimizePlan(_data.isDemo, new OptimizationOptionsVMAT(OptimizationIntermediateDoseOption.NoIntermediateDose, ""), plan, _data.app)) return true;

            ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), " Optimization finished on coverage check! Calculating dose!");
            ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));

            //calculate dose (using AAA algorithm)
            if (CalculateDose(_data.isDemo, plan, _data.app)) return true;

            ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), " Dose calculated for coverage check, normalizing plan!");

            //normalize plan
            normalizePlan(plan, new TargetsHelper().GetTargetForPlan(_data.selectedSS, "", useFlash), relativeDose, targetVolCoverage);
            if (GetAbortStatus())
            {
                KillOptimizationLoop();
                return true;
            }

            ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), " Plan normalized!");

            //print useful info about target coverage and global dmax
            PrintAdditionalPlanDoseInfo(_data.requestedPlanDoseInfo, plan);

            //calculate global Dmax expressed as a percent of the prescription dose (if dose has been calculated)
            if (plan.IsDoseValid && ((plan.Dose.DoseMax3D.Dose / plan.TotalDose.Dose) > 1.40))
            {
                ProvideUIUpdate(Environment.NewLine +
                                String.Format(" I'm having trouble covering the target with the Rx Dose! Hot spot = {0:0.0}%", 100 * (plan.Dose.DoseMax3D.Dose / plan.TotalDose.Dose)) +
                                Environment.NewLine + " Consider stopping the optimization and checking the beam arrangement!");
            }
            return false;
        }
        #endregion

        #region optimization loop
        protected override bool ResolveRunOptions(List<ExternalPlanSetup> plans)
        {
            if (_data.oneMoreOpt)
            {
                if (RunOneMoreOptionizationToLowerHotspots(plans)) return true;
            }
            if (_data.useFlash)
            {
                if (RemoveFlashAndRecalc(plans)) return true;
            }
            ProvideUIUpdate(100, Environment.NewLine + " Finished!");
            return false;
        }

        private bool RemoveFlashAndRecalc(List<ExternalPlanSetup> plans)
        {
            ProvideUIUpdate((int)(100 * (++overallPercentCompletion) / overallCalcItems), String.Format(Environment.NewLine + " Removing flash, recalculating dose, and renormalizing to TS_PTV_VMAT!"));
            ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));

            Structure bolus = _data.selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "bolus_flash");
            if (bolus == null)
            {
                //no structure named bolus_flash found. This is a problem. 
                ProvideUIUpdate(" No structure named 'BOLUS_FLASH' found in structure set! Exiting!", true);
                return true;
            }
            else
            {
                //reset dose calculation matrix for each plan in the current course. Sorry! You will have to recalculate dose to EVERY plan!
                string calcModel = _data.plans.First().GetCalculationModel(CalculationType.PhotonVolumeDose);
                List<ExternalPlanSetup> plansWithCalcDose = new List<ExternalPlanSetup> { };
                foreach (ExternalPlanSetup itr in plans.First().Course.ExternalPlanSetups)
                {
                    if (itr.IsDoseValid && itr.StructureSet == _data.selectedSS)
                    {
                        itr.ClearCalculationModel(CalculationType.PhotonVolumeDose);
                        itr.SetCalculationModel(CalculationType.PhotonVolumeDose, calcModel);
                        plansWithCalcDose.Add(itr);
                    }
                }
                //reset the bolus dose to undefined
                bolus.ResetAssignedHU();

                //recalculate dose to all the plans that had previously had dose calculated in the current course
                foreach (ExternalPlanSetup itr in plansWithCalcDose)
                {
                    CalculateDose(_data.isDemo, itr, _data.app);
                    ProvideUIUpdate((int)(100 * (++overallPercentCompletion) / overallCalcItems), " Dose calculated, normalizing plan!");
                    ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));
                    normalizePlan(itr, new TargetsHelper().GetTargetForPlan(_data.selectedSS, "", false), _data.relativeDose, _data.targetVolCoverage);
                    ProvideUIUpdate((int)(100 * (++overallPercentCompletion) / overallCalcItems), " Plan normalized!");
                }
            }
            return false;
        }
        #endregion
    }
}
