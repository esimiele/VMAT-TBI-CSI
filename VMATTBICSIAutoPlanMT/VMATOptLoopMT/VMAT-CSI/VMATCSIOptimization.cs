using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIOptLoopMT.PlanEvaluation;
using VMATTBICSIOptLoopMT.baseClasses;
using VMATTBICSIOptLoopMT.helpers;
using VMATTBICSIAutoplanningHelpers.MTWorker;
using System.Windows;
using System.IO;
using System.Runtime.CompilerServices;
using System.Diagnostics;

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

        #region plan sum
        private ExternalPlanSetup CreatePlanSum(StructureSet ss, List<ExternalPlanSetup> thePlans)
        {
            //create evaluation plan
            ExternalPlanSetup evalPlan = null;
            ExternalPlanSetup initialPlan = thePlans.First();
            if (initialPlan.Course.CanAddPlanSetup(ss))
            {
                evalPlan = initialPlan.Course.AddExternalPlanSetup(ss);
                if(!initialPlan.Course.ExternalPlanSetups.Any(x => x.Id == "Eval Plan")) evalPlan.Id = "Eval Plan";
                else evalPlan.Id = "Eval Plan1";
                ProvideUIUpdate(String.Format("Successfully created evaluation plan (plan sum): {0}", evalPlan.Id));
                ProvideUIUpdate(String.Format("Assigning prescription to plan sum: {0}", evalPlan.Id));
                int totalFx = 0;
                foreach (ExternalPlanSetup itr in thePlans) totalFx += (int)itr.NumberOfFractions;
                //assumes dose per fraction is the same between the initial and boost plans
                evalPlan.SetPrescription(totalFx, initialPlan.DosePerFraction, 1.0);
                evalPlan.DoseValuePresentation = DoseValuePresentation.Absolute;
                ProvideUIUpdate(String.Format("Prescription:"));
                ProvideUIUpdate(String.Format("    Dose per fraction: {0} cGy/fx", evalPlan.DosePerFraction.Dose));
                ProvideUIUpdate(String.Format("    Number of fractions: {0}", evalPlan.NumberOfFractions));
                ProvideUIUpdate(String.Format("    Total dose: {0} cGy", evalPlan.TotalDose.Dose));
            }
            else
            {
                ProvideUIUpdate("Error! Could not create plan sum!", true);
            }
            return evalPlan;
        }

        private bool BuildPlanSum(ExternalPlanSetup evalPlan, List<ExternalPlanSetup> thePlans)
        {
            //grab the initial and boost plans
            ExternalPlanSetup initialPlan = thePlans.First();
            ExternalPlanSetup boostPlan = thePlans.Last();
            ProvideUIUpdate(String.Format("Building plan sum from: {0} and {1}!", initialPlan.Id, boostPlan.Id));
            int zSize = initialPlan.Dose.ZSize;
            int[][,] summedDoses = CreateSummedDoseArray(zSize, evalPlan, initialPlan, boostPlan);
            AssignSummedDoseToEvalPlan(evalPlan, summedDoses, zSize);
            return false;
        }

        private int[][,] CreateSummedDoseArray(int totalSlices, ExternalPlanSetup sum, ExternalPlanSetup initialPlan, ExternalPlanSetup boostPlan)
        {
            ProvideUIUpdate("Summing the dose distributions from initial and boost plans");
            int xSize = initialPlan.Dose.XSize;
            int ySize = initialPlan.Dose.YSize;
            double initialScaleFactor = 100 * initialPlan.TotalDose.Dose / (sum.TotalDose.Dose * initialPlan.PlanNormalizationValue);
            double boostScaleFactor = 100 * boostPlan.TotalDose.Dose / (sum.TotalDose.Dose * boostPlan.PlanNormalizationValue);
            int[][,] sumArray = new int[totalSlices][,];
            for (int i = 0; i < totalSlices; i++)
            {
                ProvideUIUpdate((int)(100 * (i + 1) / totalSlices), String.Format("Summing doses from slice: {0}", i));
                //need to initialize jagged array before using
                sumArray[i] = new int[xSize, ySize];
                //get dose arrays from initial and boost plans (better to use more memory and initialize two arrays rather than putting this in a loop to limit the
                //number of times we iterate over the entire image slices
                int[,] array1 = GetDoseArray(sum.CopyEvaluationDose(initialPlan.Dose), i);
                int[,] array2 = GetDoseArray(sum.CopyEvaluationDose(boostPlan.Dose), i);
                for (int j = 0; j < xSize; j++)
                {
                    //fancy linq methods to sum entire rows at once
                    //int[] array1row = Enumerable.Range(0, array1.GetLength(1)).Select(x => array1[j, x]).ToArray();
                    //int[] array2row = Enumerable.Range(0, array2.GetLength(1)).Select(x => array2[j, x]).ToArray();
                    //int[] sum = array1row.Zip(array2row, (x, y) => x + y).ToArray();
                    for (int k = 0; k < ySize; k++)
                    {
                        sumArray[i][j, k] = (int)(array1[j, k] * initialScaleFactor) + (int)(array2[j, k] * boostScaleFactor);
                    }
                }
            }
            ProvideUIUpdate("Finished summing the dose distributions.");
            return sumArray;
        }

        private int[,] GetDoseArray(EvaluationDose e, int slice)
        {
            int[,] buffer = new int[e.XSize, e.YSize];
            e.GetVoxels(slice, buffer);
            return buffer;
        }

        private bool AssignSummedDoseToEvalPlan(ExternalPlanSetup evalPlan, int[][,] summedDoses, int totalSlices)
        {
            //testing 3/9/23
            //max dose and structure DVHs are within 0.1% between evaluation plan and true plan sum
            //this needs to be done outside the above loop because as soon as we call createevaluationdose, it will wipe anything we have assigned to the eval plan thus far
            ProvideUIUpdate(String.Format("Assigning summed doses to eval plan: {0}", evalPlan.Id));
            EvaluationDose summed = evalPlan.CreateEvaluationDose();
            if (summed == null)
            {
                ProvideUIUpdate("summed is null", true);
                return true;
            }
            try
            {
                for (int i = 0; i < totalSlices; i++)
                {
                    summed.SetVoxels(i, summedDoses[i]);
                }
            }
            catch (Exception e) 
            { 
                ProvideUIUpdate(e.Message, true); 
                return true; 
            }
            ProvideUIUpdate(String.Format("Finished assigning summed doses to eval plan: {0}", evalPlan.Id));
            return false;
        }
        #endregion

        protected override bool RunOptimizationLoop()
        {
            //need to determine if we only need to optimize one plan (or an initial and boost plan)
            if (_data.plans.Count == 1)
            {
                if (RunOptimizationLoopInitialPlanOnly()) return true;
            }
            else
            {
                if(RunSequentialPlansOptimizationLoop()) return true;
            }
            _data.app.SaveModifications();
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

            ProvideUIUpdate(100, Environment.NewLine + " Finished!");
            if (!_data.isDemo && _data.oneMoreOpt) _data.app.SaveModifications();
            return false;
        }
        #endregion

        #region sequential plans optimization loop
        private bool RunSequentialPlansOptimizationLoop()
        {
            List<Tuple<string, string>> plansTargets = GetPlanTargetList(_data.prescriptions);
            if(!plansTargets.Any())
            {
                ProvideUIUpdate("Error! Prescriptions are missing! Cannot determine the appropriate target for each plan! Exiting!", true);
                return true;
            }
            int percentCompletion = 0;
            int calcItems = 100;
            //first need to create a plan sum
            ExternalPlanSetup evalPlan = CreatePlanSum(_data.selectedSS, _data.plans);
            if (evalPlan == null) return true;

            ProvideUIUpdate(" Starting optimization loop!");
            int count = 0;
            while(count < _data.numOptimizations)
            {
                ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), String.Format(" Iteration {0}:", count + 1));
                ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));

                foreach (ExternalPlanSetup itr in _data.plans)
                {
                    //string exeName = "ParallelTest";
                    //string path = AppExePath(exeName);
                    //if (!string.IsNullOrEmpty(path))
                    //{
                    //    ProcessStartInfo p = new ProcessStartInfo(path);
                    //    p.Arguments = String.Format("{0} {1}", _data.id, itr.UID);
                    //    Process.Start(p);
                    //}
                    //else ProvideUIUpdate("Executable path was empty");
                    //Testing t = new Testing(_data.isDemo, itr);
                    //Thread worker = new Thread(t.Run);
                    //worker.Start(t);
                    ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), String.Format(" Optimizing plan: {0}!", itr.Id));
                    if (OptimizePlan(_data.isDemo, new OptimizationOptionsVMAT(OptimizationIntermediateDoseOption.NoIntermediateDose, ""), itr, _data.app)) return true;
                    ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), " Optimization finished! Calculating dose!");
                    if (CalculateDose(_data.isDemo, itr, _data.app)) return true;
                    ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), " Dose calculated, normalizing plan!");
                    //normalize
                    normalizePlan(itr, GetTargetForPlan(_data.selectedSS, plansTargets.FirstOrDefault(x => x.Item1 == itr.Id).Item2, _data.useFlash), _data.relativeDose, _data.targetVolCoverage);
                    if (GetAbortStatus())
                    {
                        KillOptimizationLoop();
                        return true;
                    }
                    ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), String.Format(" Plan normalized!"));
                    ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));
                }
                
                if (BuildPlanSum(evalPlan, _data.plans)) return true;
                count++;
            }

            return false;
        }

        private string AppExePath(string exeName)
        {
            return FirstExePathIn(Path.GetDirectoryName(GetSourceFilePath()), exeName);
        }

        private string FirstExePathIn(string dir, string exeName)
        {
            return Directory.GetFiles(dir, "*.exe").FirstOrDefault(x => x.Contains(exeName));
        }

        private string GetSourceFilePath([CallerFilePath] string sourceFilePath = "")
        {
            return @"\\enterprise.stanfordmed.org\depts\RadiationTherapy\Public\Users\ESimiele\Research\VMAT-TBI-CSI\bin\";
        }

        #endregion
    }

    public class Testing
    {
        private ExternalPlanSetup _plan;
        private bool _demo;
        public Testing(bool d, ExternalPlanSetup p)
        {
            _demo = d;
            _plan = p;
        }

        public void Run(object o)
        {
            Testing t = (Testing)o;
            CalculateDose(t._demo, t._plan);
        }

        public bool CalculateDose(bool isDemo, ExternalPlanSetup plan)
        {
            if (isDemo) Thread.Sleep(3000);
            else
            {
                string id = plan.Id;
                MessageBox.Show(id);
                //try
                //{
                //    CalculationResult calcRes = plan.CalculateDose();
                //}
                //catch (Exception except)
                //{
                //    MessageBox.Show(except.Message);
                //    //PrintFailedMessage("Dose calculation", except.Message);
                //    return true;
                //}
            }
           
            return false;
        }
    }
}
