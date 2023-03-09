using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIOptLoopMT.baseClasses;
using VMATTBICSIOptLoopMT.helpers;
using VMATTBICSIOptLoopMT.PlanEvaluation;

namespace VMATTBICSIOptLoopMT.VMAT_TBI
{
    class VMATTBIOptimization : optimizationLoopBase
    {
        public VMATTBIOptimization(dataContainer _d)
        {
            _data = _d;
            logPath = _data.logFilePath + "\\optimization\\";
            fileName = logPath + _data.id + ".txt";
        }

        public override bool Run()
        {
            try
            {
                SetAbortUIStatus("Runnning");
                PrintRunSetupInfo();
                //preliminary checks
                PrintRunSetupInfo();
                if (PreliminaryChecksSSAndImage(_data.selectedSS, new List<string> { })) return true;
                if (PreliminaryChecksCouch(_data.selectedSS)) return true;
                if (PreliminaryChecksSpinningManny(_data.selectedSS)) return true;
                if (_checkSupportStructures)
                {
                    if (CheckSupportStructures(_data.plans.First().Course.Patient.Courses.ToList(), _data.selectedSS)) return true;
                }
                foreach (ExternalPlanSetup itr in _data.plans) if (PreliminaryChecksPlans(itr)) return true;

                if (_data.isDemo || !_data.runCoverageCheck) ProvideUIUpdate(" Skipping coverage check! Moving on to optimization loop!");
                else
                {
                    if (RunCoverageCheck(_data.optParams, _data.plan, _data.relativeDose, _data.targetVolCoverage, _data.useFlash)) return true;
                    ProvideUIUpdate(" Coverage check completed! Commencing optimization loop!");
                }
                if (RunOptimizationLoop()) return true;
                OptimizationLoopFinished();
            }
            catch (Exception e) { ProvideUIUpdate(String.Format("{0}", e.Message), true); return true; }
            return false;
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
            normalizePlan(plan, GetTargetForPlan(_data.selectedSS, "", useFlash), relativeDose, targetVolCoverage);
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
        protected override bool RunOptimizationLoop()
        {
            int percentCompletion = 0;
            int calcItems = 100;

            //update the current optimization parameters for this iteration
            _data.optParams = InitializeOptimizationConstriants(_data.optParams);
            //reset the objectives and inform the user of the current optimization parameters
            UpdateConstraints(_data.optParams, _data.plan);

            if (_data.isDemo) Thread.Sleep(3000);
            else _data.app.SaveModifications();

            ProvideUIUpdate(" Starting optimization loop!");
            //counter to keep track of how many optimization iterations have been performed
            int count = 0;
            while (count < _data.numOptimizations)
            {
                ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), String.Format(" Iteration {0}:", count + 1));
                ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));
                if (OptimizePlan(_data.isDemo, new OptimizationOptionsVMAT(OptimizationIntermediateDoseOption.NoIntermediateDose, ""), _data.plan, _data.app)) return true;

                ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), " Optimization finished! Calculating intermediate dose!");
                ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));
                if (CalculateDose(_data.isDemo, _data.plan, _data.app)) return true;

                ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), " Dose calculated! Continuing optimization!");
                ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));
                if (OptimizePlan(_data.isDemo, new OptimizationOptionsVMAT(OptimizationOption.ContinueOptimizationWithPlanDoseAsIntermediateDose, ""), _data.plan, _data.app)) return true;

                ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), " Optimization finished! Calculating dose!");
                ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));
                if (CalculateDose(_data.isDemo, _data.plan, _data.app)) return true;

                ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), " Dose calculated, normalizing plan!");
                ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));

                //normalize
                normalizePlan(_data.plan, GetTargetForPlan(_data.selectedSS, "", _data.useFlash), _data.relativeDose, _data.targetVolCoverage);
                if (GetAbortStatus())
                {
                    KillOptimizationLoop();
                    return true;
                }

                ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), " Plan normalized! Evaluating plan quality and updating constraints!"); ;
                //evaluate the new plan for quality and make any adjustments to the optimization parameters
                evalPlanStruct e = evaluateAndUpdatePlan(_data.plan, _data.optParams, _data.planObj, _data.requestedTSstructures, _data.threshold, _data.lowDoseLimit, (_data.oneMoreOpt && ((count + 1) == _data.numOptimizations)));

                //updated optimization constraint list is empty, which means that all plan objectives have been met. Let the user know and break the loop. Also set oneMoreOpt to false so that extra optimization is not performed
                if (!e.updatedObj.Any())
                {
                    ProvideUIUpdate(String.Format(" All plan objectives have been met! Exiting!"), true);
                    _data.oneMoreOpt = false;
                    break;
                }

                //did the user request to copy and save each plan iteration from the optimization loop?
                //the last two boolean evaluations check if the user requested one more optimization (always copy and save) or this is not the last loop iteration (used in the case where the user elected NOT to do one more optimization
                //but still wants to copy and save each plan). We don't want to copy and save the plan on the last loop iteration when oneMoreOpt is false because we will end up with two copies of
                //the same plan!
                if (!_data.isDemo && _data.copyAndSavePlanItr && (_data.oneMoreOpt || ((count + 1) != _data.numOptimizations))) CopyAndSavePlan(_data.plan, count);

                //print the results of the quality check for this optimization
                ProvideUIUpdate(Environment.NewLine + GetOptimizationResultsHeader());
                int index = 0;
                //structure, dvh data, current dose obj, dose diff^2, cost, current priority, priority difference
                foreach (Tuple<Structure, DVHData, double, double, double, int> itr in e.diffPlanOpt)
                {
                    string id = "";
                    //grab the structure id from the optParams list (better to work with string literals rather than trying to access the structure id through the structure object instance in the diffPlanOpt data structure)
                    id = _data.optParams.ElementAt(index).Item1;
                    //"structure Id", "constraint type", "dose diff^2 (cGy^2)", "current priority", "cost", "cost (%)"
                    ProvideUIUpdate(String.Format(" {0, -15} | {1, -16} | {2, -20:N1} | {3, -16} | {4, -12:N1} | {5, -9:N1} |", id, _data.optParams.ElementAt(index).Item2, itr.Item4, itr.Item6, itr.Item5, 100 * itr.Item5 / e.totalCostPlanOpt));
                    index++;
                }

                PrintAdditionalPlanDoseInfo(_data.requestedPlanDoseInfo, _data.plan);

                //really crank up the priority and lower the dose objective on the cooler on the last iteration of the optimization loop
                //this is basically here to avoid having to call op.updateConstraints a second time (if this batch of code was placed outside of the loop)
                if (_data.oneMoreOpt && ((count + 1) == _data.numOptimizations))
                {
                    //go through the current list of optimization objects and add all of them to finalObj vector. ADD COMMENTS!
                    List<Tuple<string, string, double, double, int>> finalObj = new List<Tuple<string, string, double, double, int>> { };
                    foreach (Tuple<string, string, double, double, int> itr in e.updatedObj)
                    {
                        //get maximum priority and assign it to the cooler structure to really push the hotspot down. Also lower dose objective
                        if (itr.Item1.ToLower().Contains("ts_cooler"))
                        {
                            finalObj.Add(new Tuple<string, string, double, double, int>(itr.Item1, itr.Item2, 0.98 * itr.Item3, itr.Item4, Math.Max(itr.Item5, (int)(0.9 * (double)e.updatedObj.Max(x => x.Item5)))));
                        }
                        else finalObj.Add(itr);
                    }
                    //set e.updatedObj to be equal to finalObj
                    e.updatedObj = finalObj;
                }

                //print the updated optimization objectives to the user
                ProvideUIUpdate(Environment.NewLine + GetOptimizationObjectivesHeader());
                foreach (Tuple<string, string, double, double, int> itr in e.updatedObj)
                    ProvideUIUpdate(String.Format(" {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5));
                ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems));

                //update the optimization constraints in the plan
                UpdateConstraints(e.updatedObj, _data.plan);

                //increment the counter, update d.optParams so it is set to the initial optimization constraints at the BEGINNING of the optimization iteration, and save the changes to the plan
                count++;
                _data.optParams = e.updatedObj;
                if (!_data.isDemo) _data.app.SaveModifications();
            }

            //option to run one additional optimization (can be requested on the main GUI)
            if (_data.oneMoreOpt)
            {
                ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), " Running one final optimization starting at MR3 to try and reduce global plan hotspots!");
                ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));

                //one final push to lower the global plan hotspot if the user asked for it
                if (OptimizePlan(_data.isDemo, new OptimizationOptionsVMAT(OptimizationOption.ContinueOptimizationWithPlanDoseAsIntermediateDose, ""), _data.plan, _data.app)) return true;

                ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), " Optimization finished! Calculating dose!");
                ProvideUIUpdate(String.Format(" Elapsed time: {0}" + Environment.NewLine, GetElapsedTime()));
                if (CalculateDose(_data.isDemo, _data.plan, _data.app)) return true;

                ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), " Dose calculated, normalizing plan!");
                ProvideUIUpdate(String.Format(" Elapsed time: {0}" + Environment.NewLine, GetElapsedTime()));

                //normalize
                normalizePlan(_data.plan, GetTargetForPlan(_data.selectedSS, "", _data.useFlash), _data.relativeDose, _data.targetVolCoverage);

                //print requested additional info about the plan
                PrintAdditionalPlanDoseInfo(_data.requestedPlanDoseInfo, _data.plan);
            }

            if (_data.useFlash)
            {
                ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), String.Format(Environment.NewLine + " Removing flash, recalculating dose, and renormalizing to TS_PTV_VMAT!"));
                ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));

                Structure bolus = _data.plan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToLower() == "bolus_flash");
                if (bolus == null)
                {
                    //no structure named bolus_flash found. This is a problem. 
                    ProvideUIUpdate(" No structure named 'BOLUS_FLASH' found in structure set! Exiting!");
                }
                else
                {
                    //reset dose calculation matrix for each plan in the current course. Sorry! You will have to recalculate dose to EVERY plan!
                    string calcModel = _data.plan.GetCalculationModel(CalculationType.PhotonVolumeDose);
                    List<ExternalPlanSetup> plansWithCalcDose = new List<ExternalPlanSetup> { };
                    foreach (ExternalPlanSetup p in _data.plan.Course.ExternalPlanSetups)
                    {
                        if (p.IsDoseValid && p.StructureSet == _data.plan.StructureSet)
                        {
                            p.ClearCalculationModel(CalculationType.PhotonVolumeDose);
                            p.SetCalculationModel(CalculationType.PhotonVolumeDose, calcModel);
                            plansWithCalcDose.Add(p);
                        }
                    }
                    //reset the bolus dose to undefined
                    bolus.ResetAssignedHU();
                    //recalculate dose to all the plans that had previously had dose calculated in the current course
                    foreach (ExternalPlanSetup p in plansWithCalcDose) CalculateDose(_data.isDemo, _data.plan, _data.app);

                    ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), " Dose calculated, normalizing plan!");
                    ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));
                    //"trick" the normalizePlan method into thinking we are not using flash. Therefore, it will normalize to TS_PTV_VMAT instead of TS_PTV_FLASH (i.e., set useFlash to false)
                    normalizePlan(_data.plan, GetTargetForPlan(_data.selectedSS, "", false), _data.relativeDose, _data.targetVolCoverage);
                }
            }
            ProvideUIUpdate(100, Environment.NewLine + " Finished!");
            if (!_data.isDemo && (_data.oneMoreOpt || _data.useFlash)) _data.app.SaveModifications();
            return false;
        }
        #endregion
    }
}
