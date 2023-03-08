using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIOptLoopMT.PlanEvaluation;
using VMATTBICSIOptLoopMT.baseClasses;
using VMATTBICSIOptLoopMT.helpers;

namespace VMATTBICSIOptLoopMT.VMAT_CSI
{
    class VMATCSIOptimization : optimizationLoopBase
    {
        public VMATCSIOptimization(dataContainer _d)
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
                //preliminary checks
                PrintRunSetupInfo();
                if (PreliminaryChecksSSAndImage(_data.selectedSS, new List<string> { })) return true;
                if (PreliminaryChecksCouch(_data.selectedSS)) return true;
                if (_checkSupportStructures)
                {
                    if(CheckSupportStructures(_data.plans.First().Course.Patient.Courses.ToList(), _data.selectedSS)) return true;
                }
                foreach(ExternalPlanSetup itr in _data.plans) if (PreliminaryChecksPlans(itr)) return true;

                if (RunOptimizationLoop()) return true;
                OptimizationLoopFinished();
            }
            catch (Exception e) { ProvideUIUpdate(String.Format("{0}", e.Message), true); return true; }
            return false;
        }

        private int[,,] GetArray(EvaluationDose e)
        {
            int[,,] array = new int[e.XSize, e.YSize, e.ZSize];
            int totalslices = e.ZSize;
            for(int i = 0; i < totalslices; i++)
            {
                int[,] tmpArray = new int[e.XSize, e.YSize];
                e.GetVoxels(i, tmpArray);
                for (int j = 0; j < e.XSize; j++)
                {
                    //int[] array1row = Enumerable.Range(0, array1.GetLength(1)).Select(x => array1[j, x]).ToArray();
                    //int[] array2row = Enumerable.Range(0, array2.GetLength(1)).Select(x => array2[j, x]).ToArray();
                    //int[] sum = array1row.Zip(array2row, (x, y) => x + y).ToArray();
                    for (int k = 0; k < e.YSize; k++)
                    {
                        //sumArray[j, k] = sum[k];
                        array[j, k, i] = tmpArray[j, k];
                    }

                }
            }
            return array;
        }

        protected override bool RunOptimizationLoop()
        {
            //need to determine if we only need to optimize one plan (or an initial and boost plan)
            //if (_data.plans.Count == 1) if(RunOptimizationLoopInitialPlanOnly()) return true;
            //else
            //{
            //create evaluation plan
            ExternalPlanSetup evalPlan = _data.plan.Course.AddExternalPlanSetup(_data.selectedSS);
            evalPlan.Id = "Eval Plan";
            evalPlan.CopyEvaluationDose(_data.plans.First().Dose);
            EvaluationDose e1 = evalPlan.DoseAsEvaluationDose;
            int[,,] array1 = GetArray(e1);
            ProvideUIUpdate(String.Format("Retrieved eval doses for initial"));

            //if (e1 == null)
            //{
            //    ProvideUIUpdate("e1 is null", true);
            //    return true;
            //}
            evalPlan.CopyEvaluationDose(_data.plans.Last().Dose);
            EvaluationDose e2 = evalPlan.DoseAsEvaluationDose;
            int[,,] array2 = GetArray(e2);
            ProvideUIUpdate(String.Format("Retrieved eval doses for boost"));

            //if (e2 == null)
            //{
            //    ProvideUIUpdate("e2 is null", true);
            //    return true;
            //}
            EvaluationDose summed = evalPlan.CreateEvaluationDose();
            if (summed == null)
            {
                ProvideUIUpdate("summed is null", true);
                return true;
            }
            int totalslices = e1.ZSize;
            for (int i = 0; i < totalslices; i++)
            {
                ProvideUIUpdate((int)(100 * (i + 1) / totalslices), String.Format("Summing eval doses from slice: {0}", i));
                int[,] sumArray = new int[e1.XSize, e1.YSize];
                //int[,] array1 = new int[e1.XSize, e1.YSize];
                //int[,] array2 = new int[e1.XSize,e1.YSize];
                //e1.GetVoxels(i, array1);
                //e2.GetVoxels(i, array2);
                for(int j = 0; j < e1.XSize; j++)
                {
                    //int[] array1row = Enumerable.Range(0, array1.GetLength(1)).Select(x => array1[j, x]).ToArray();
                    //int[] array2row = Enumerable.Range(0, array2.GetLength(1)).Select(x => array2[j, x]).ToArray();
                    //int[] sum = array1row.Zip(array2row, (x, y) => x + y).ToArray();
                    for (int k = 0; k < e1.YSize; k++)
                    {
                        //sumArray[j, k] = sum[k];
                        sumArray[j, k] = array1[j,k,i] + array2[j,k,i];
                    }

                }
                try
                {
                    summed.SetVoxels(i, sumArray);
                }
                catch (Exception e) { ProvideUIUpdate(e.Message, true); return true; }
            }
            ProvideUIUpdate(String.Format("{0}", summed.DoseMax3D));
            ProvideUIUpdate(String.Format("{0}", evalPlan.Dose.DoseMax3D));
            double maxDose = (evalPlan.Dose.DoseMax3D.Dose * _data.plan.TotalDose.Dose) / 100;
            ProvideUIUpdate(String.Format("{0}", maxDose));
            ProvideUIUpdate(String.Format("{0}", _data.plan.Course.PlanSetups.First().Dose.DoseMax3D.Dose / maxDose));
            ProvideUIUpdate(String.Format(" Brain V5000cGy {0:0.0}%", evalPlan.GetVolumeAtDose(_data.selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == "brain"),new DoseValue(100*(5000.0/1200.0), DoseValue.DoseUnit.Percent),VolumePresentation.Relative)));
            _data.app.SaveModifications();
            //if (e1 == null) ProvideUIUpdate("Eval dose is null", true);
            //else ProvideUIUpdate($"{e1.Id} is not null");
            //EvaluationDose e2 = _data.plans.Last().DoseAsEvaluationDose;
            //}
            return false;
        }

        #region initial plan only optimization loop
        private bool RunOptimizationLoopInitialPlanOnly()
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
                normalizePlan(_data.plan, _data.relativeDose, _data.targetVolCoverage, _data.useFlash);
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
                normalizePlan(_data.plan, _data.relativeDose, _data.targetVolCoverage, _data.useFlash);

                //print requested additional info about the plan
                PrintAdditionalPlanDoseInfo(_data.requestedPlanDoseInfo, _data.plan);
            }

            ProvideUIUpdate(100, Environment.NewLine + " Finished!");
            if (!_data.isDemo && _data.oneMoreOpt) _data.app.SaveModifications();
            return false;
        }
        #endregion

        #region sequential plans optimization loop
        private bool RunSequentialPlansOptimizationLoop()
        {
            return false;
        }
        #endregion
    }
}
