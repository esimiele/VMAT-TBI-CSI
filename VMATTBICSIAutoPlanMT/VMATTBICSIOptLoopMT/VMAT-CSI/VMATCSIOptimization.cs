using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.Structs;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.UIHelpers;
using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIOptLoopMT.VMAT_CSI
{
    class VMATCSIOptimization : OptimizationLoopBase
    {
        ExternalPlanSetup evalPlan = null;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="_d"></param>
        public VMATCSIOptimization(OptDataContainer _d)
        {
            _data = _d;
            InitializeLogPathAndName();
            CalculateNumberOfItemsToComplete();
        }

        /// <summary>
        /// Run control
        /// </summary>
        /// <returns></returns>
        public override bool Run()
        {
            try
            {
                SetAbortUIStatus("Runnning");
                PrintRunSetupInfo();
                //preliminary checks
                UpdateUILabel("Preliminary checks:");
                ProvideUIUpdate("Performing preliminary checks now:");
                if (PreliminaryChecksSSAndImage(_data.selectedSS, TargetsHelper.GetAllTargetIds(_data.prescriptions))) return true;
                if (PreliminaryChecksCouch(_data.selectedSS)) return true;
                if (PreliminaryChecksPlans(_data.plans)) return true;
                if (RunOptimizationLoop(_data.plans)) return true;
                OptimizationLoopFinished();
            }
            catch (Exception e)
            {
                ProvideUIUpdate($"{e.Message}", true);
                return true; 
            }
            return false;
        }

        /// <summary>
        /// Helper method to calculate the total number of items to complete during this optimization loop run
        /// </summary>
        protected void CalculateNumberOfItemsToComplete()
        {
            overallCalcItems = 3;
            overallCalcItems += _data.plans.Count;
            int optLoopItems = 5 * _data.numOptimizations * _data.plans.Count;
            if (_data.oneMoreOpt) optLoopItems += 3;
            overallCalcItems += optLoopItems;
        }

        #region plan sum
        /// <summary>
        /// Helper method to create an empty plan that will be used to store the summed dose from the initial and boost plans
        /// </summary>
        /// <param name="ss"></param>
        /// <param name="thePlans"></param>
        /// <returns></returns>
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
                ProvideUIUpdate($"Successfully created evaluation plan (plan sum): {evalPlan.Id}");
                ProvideUIUpdate($"Assigning prescription to plan sum: {evalPlan.Id}");
                int totalFx = 0;
                foreach (ExternalPlanSetup itr in thePlans) totalFx += (int)itr.NumberOfFractions;
                //assumes dose per fraction is the same between the initial and boost plans
                evalPlan.SetPrescription(totalFx, initialPlan.DosePerFraction, 1.0);
                evalPlan.DoseValuePresentation = DoseValuePresentation.Absolute;
                ProvideUIUpdate("Prescription:");
                ProvideUIUpdate($"    Dose per fraction: {evalPlan.DosePerFraction.Dose} cGy/fx");
                ProvideUIUpdate($"    Number of fractions: {evalPlan.NumberOfFractions}");
                ProvideUIUpdate($"    Total dose: {evalPlan.TotalDose.Dose} cGy");
            }
            else
            {
                ProvideUIUpdate("Error! Could not create plan sum!", true);
            }
            return evalPlan;
        }

        /// <summary>
        /// Utility method to build a plan sum using the supplied list of plans
        /// </summary>
        /// <param name="evalPlan"></param>
        /// <param name="thePlans"></param>
        /// <returns></returns>
        private bool BuildPlanSum(ExternalPlanSetup evalPlan, List<ExternalPlanSetup> thePlans)
        {
            UpdateUILabel("Build plan sum:");
            //grab the initial and boost plans
            ExternalPlanSetup initialPlan = thePlans.First();
            ExternalPlanSetup boostPlan = thePlans.Last();
            ProvideUIUpdate($"Building plan sum from: {initialPlan.Id} and {boostPlan.Id}!");
            int zSize = initialPlan.Dose.ZSize;
            int[][,] summedDoses = CreateSummedDoseArray(zSize, evalPlan, initialPlan, boostPlan);
            AssignSummedDoseToEvalPlan(evalPlan, summedDoses, zSize);
            return false;
        }

        /// <summary>
        /// Helper method to build an nested array of dose values for all slices of the dose matrix
        /// </summary>
        /// <param name="totalSlices"></param>
        /// <param name="sum"></param>
        /// <param name="initialPlan"></param>
        /// <param name="boostPlan"></param>
        /// <returns></returns>
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
                ProvideUIUpdate(100 * (i + 1) / totalSlices);
                //need to initialize jagged array before using
                sumArray[i] = new int[xSize, ySize];
                //get dose arrays from initial and boost plans (better to use more memory and initialize two arrays rather than putting this in a loop to limit the
                //number of times we iterate over the entire image slices
                int[,] array1 = GetDoseArray(sum.CopyEvaluationDose(initialPlan.Dose), i);
                int[,] array2 = GetDoseArray(sum.CopyEvaluationDose(boostPlan.Dose), i);
                for (int j = 0; j < xSize; j++)
                {
                    for (int k = 0; k < ySize; k++)
                    {
                        sumArray[i][j, k] = (int)(array1[j, k] * initialScaleFactor) + (int)(array2[j, k] * boostScaleFactor);
                    }
                }
            }
            ProvideUIUpdate("Finished summing the dose distributions.");
            return sumArray;
        }

        /// <summary>
        /// Simple method to retrieve the 2D dose array for the specified slice of the dose matrix
        /// </summary>
        /// <param name="e"></param>
        /// <param name="slice"></param>
        /// <returns></returns>
        private int[,] GetDoseArray(EvaluationDose e, int slice)
        {
            int[,] buffer = new int[e.XSize, e.YSize];
            e.GetVoxels(slice, buffer);
            return buffer;
        }

        /// <summary>
        /// Helper method to take the nested array of dose values and assigned them to the plan sum
        /// </summary>
        /// <param name="evalPlan"></param>
        /// <param name="summedDoses"></param>
        /// <param name="totalSlices"></param>
        /// <returns></returns>
        private bool AssignSummedDoseToEvalPlan(ExternalPlanSetup evalPlan, int[][,] summedDoses, int totalSlices)
        {
            //max dose and structure DVHs are within 0.1% between evaluation plan and true plan sum
            //this needs to be done outside the above loop because as soon as we call createevaluationdose, it will wipe anything we have assigned to the eval plan thus far
            ProvideUIUpdate($"Assigning summed doses to eval plan: {evalPlan.Id}");
            EvaluationDose summed = evalPlan.CreateEvaluationDose();
            if (summed == null)
            {
                ProvideUIUpdate("Eval plan summed dose distribution is null", true);
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
            ProvideUIUpdate($"Finished assigning summed doses to eval plan: {evalPlan.Id}");
            return false;
        }
        #endregion

        #region optimization loop
        /// <summary>
        /// Overridden method of resolving the final run options once the maximum number of iterations has been reached
        /// </summary>
        /// <param name="plans"></param>
        /// <returns></returns>
        protected override bool ResolveRunOptions(List<ExternalPlanSetup> plans)
        {
            if (_data.oneMoreOpt)
            {
                UpdateUILabel("One more optimization:");
                if (RunOneMoreOptionizationToLowerHotspots(plans)) return true;

                if (plans.Count() > 1)
                {
                    ExternalPlanSetup initialPlan = plans.First();
                    (bool needsAdditionalOpt, double dmax) = OptimizationLoopHelper.CheckPlanHotspot(initialPlan, 1.10);
                    if (needsAdditionalOpt)
                    {
                        if (AttemptToLowerInitPlanDmax(initialPlan, dmax)) return true;
                    }
                    else ProvideUIUpdate($"Initial plan ({plans.First().Id}) Dmax is {dmax * 100:0.0}%");

                    UpdateUILabel("Create plan sum:");
                    if (BuildPlanSum(evalPlan, plans)) return true;
                    ProvideUIUpdate(OptimizationLoopUIHelper.PrintAdditionalPlanDoseInfo(_data.requestedPlanDoseInfo, evalPlan, _data.normalizationVolumes));
                }
            }
            return false;
        }

        /// <summary>
        /// Method to run the initial CSI plan through one final optimization to try and lower hotspots in an attempt to cool the composite plan down
        /// </summary>
        /// <param name="initialPlan"></param>
        /// <param name="dmax"></param>
        /// <returns></returns>
        private bool AttemptToLowerInitPlanDmax(ExternalPlanSetup initialPlan, double dmax)
        {
            ProvideUIUpdate($"Initial plan ({initialPlan.Id}) Dmax is {dmax * 100:0.0}%!");
            ProvideUIUpdate($"Running one additional optimization for {initialPlan.Id} to lower Dmax!");

            int percentComplete = 0;
            int calcItems = 4;
            List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> theList = new List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>>
                        {
                            new Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>("TS_cooler101",107.0,101.0,0.0,100, new List<Tuple<string, double, string, double>>{ })
                        };
            (bool fail, List<Tuple<string, OptimizationObjectiveType, double, double, int>> addedTSCoolerConstraint) = UpdateHeaterCoolerStructures(initialPlan, true, theList, false);
            if (fail)
            {
                //user killed operation while generating heater and cooler structures
                KillOptimizationLoop();
                return true;
            }
            ProvideUIUpdate(100 * ++percentComplete / calcItems);
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> optParams = OptimizationSetupUIHelper.ReadConstraintsFromPlan(initialPlan);
            optParams.AddRange(addedTSCoolerConstraint);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, OptimizationLoopUIHelper.PrintPlanOptimizationConstraints(initialPlan.Id, optParams));

            UpdateConstraints(optParams, initialPlan);
            ProvideUIUpdate(100 * ++percentComplete / calcItems);

            ////set MR restart level option for the photon optimization
            //string optimizationModel = initialPlan.GetCalculationModel(CalculationType.PhotonVMATOptimization);
            ////set MR restart level option for the photon optimization
            //if (!initialPlan.SetCalculationOption(optimizationModel, "MRLevelAtRestart", "MR4"))
            //{
            //    ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"Warning! VMAT/MRLevelAtRestart option not found for {optimizationModel}");
            //}
            //else ProvideUIUpdate((int)(100 * ++percentComplete / calcItems), $"MR restart level set to MR4");

            if(RunOneMoreOptionizationToLowerHotspots(new List<ExternalPlanSetup> { initialPlan })) return true;
            return false;
        }

        /// <summary>
        /// Control method for directing the flow of sequential optimization
        /// </summary>
        /// <param name="plans"></param>
        /// <returns></returns>
        protected override bool RunSequentialPlansOptimizationLoop(List<ExternalPlanSetup> plans)
        {
            //a requirement for sequentional optimization
            List<Tuple<string, string>> plansTargets = TargetsHelper.GetHighestRxPlanTargetList(_data.prescriptions);
            if(!plansTargets.Any())
            {
                ProvideUIUpdate("Error! Prescriptions are missing! Cannot determine the appropriate target for each plan! Exiting!", true);
                return true;
            }
            int percentComplete = 0;
            int calcItems = 5 * plans.Count() * _data.numOptimizations;
            //first need to create a plan sum
            evalPlan = CreatePlanSum(_data.selectedSS, plans);
            if (evalPlan == null) return true;

            foreach (ExternalPlanSetup itr in plans) InitializeOptimizationConstriants(itr);

            ProvideUIUpdate("Starting optimization loop!");
            int count = 0;
            while(count < _data.numOptimizations)
            {
                bool oneMoreOptNextItr = _data.oneMoreOpt && count + 1 == _data.numOptimizations;
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Iteration {count + 1}:");
                ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
                foreach (ExternalPlanSetup itr in plans)
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
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Optimizing plan: {itr.Id}!");
                    if (OptimizePlan(_data.isDemo, new OptimizationOptionsVMAT(OptimizationIntermediateDoseOption.NoIntermediateDose, ""), itr, _data.app)) return true;
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, "Optimization finished! Calculating dose!");
                    if (CalculateDose(_data.isDemo, itr, _data.app)) return true;
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, "Dose calculated, normalizing plan!");
                    //normalize
                    if(NormalizePlan(itr, 
                                     TargetsHelper.GetTargetStructureForPlanType(_data.selectedSS, 
                                                                                 OptimizationLoopHelper.GetNormaliztionVolumeIdForPlan(itr.Id, _data.normalizationVolumes), 
                                                                                 _data.useFlash, 
                                                                                 _data.planType), 
                                     _data.relativeDose, 
                                     _data.targetVolCoverage)) return true;
                    if (GetAbortStatus())
                    {
                        KillOptimizationLoop();
                        return true;
                    }
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, "Plan normalized!");
                    ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
                }
                
                if (BuildPlanSum(evalPlan, plans)) return true;
                ProvideUIUpdate(OptimizationLoopUIHelper.PrintAdditionalPlanDoseInfo(_data.requestedPlanDoseInfo, evalPlan, _data.normalizationVolumes));

                if (EvaluatePlanSumQuality(evalPlan, _data.planObj))
                {
                    ProvideUIUpdate($"All plan objectives met for plan sum: {evalPlan.Id}! Exiting!");
                    return false;
                }
                else
                {
                    ProvideUIUpdate("All plan objectives NOT met! Updating heater and cooler structures!");
                    (bool fail, List<Tuple<string, OptimizationObjectiveType, double, double, int>> updatedHeaterCoolerConstraints) = UpdateHeaterCoolerStructures(evalPlan, oneMoreOptNextItr, _data.requestedTSstructures);
                    //did the user abort the program while updating the heater and cooler structures
                    if (fail)
                    {
                        //user killed operation while generating heater and cooler structures
                        KillOptimizationLoop();
                        return true;
                    }
                    foreach (ExternalPlanSetup itr in plans)
                    {
                        ProvideUIUpdate($"Adjusting optimization parameters for plan: {itr.Id}!");
                        List<Tuple<string, OptimizationObjectiveType, double, double, int>> optParams = OptimizationSetupUIHelper.ReadConstraintsFromPlan(itr);
                        ProvideUIUpdate($"Evaluating quality of plan: {itr.Id}!");
                        EvalPlanStruct e = EvaluatePlanSumComponentPlans(itr, optParams);
                        if (e.wasKilled) return true;

                        ProvideUIUpdate(OptimizationLoopUIHelper.PrintPlanOptimizationResultVsConstraints(itr, optParams, e.diffPlanOpt, e.totalCostPlanOpt));
                        ProvideUIUpdate(OptimizationLoopUIHelper.PrintAdditionalPlanDoseInfo(_data.requestedPlanDoseInfo, itr, _data.normalizationVolumes));

                        ProvideUIUpdate($"Scaling optimization parameters for heater cooler structures for plan: {itr.Id}!");
                        e.updatedObj.AddRange(OptimizationLoopHelper.ScaleHeaterCoolerOptConstraints(itr.TotalDose.Dose, evalPlan.TotalDose.Dose, updatedHeaterCoolerConstraints));

                        if(oneMoreOptNextItr) e.updatedObj = OptimizationLoopHelper.IncreaseOptConstraintPrioritiesForFinalOpt(e.updatedObj);

                        ProvideUIUpdate(100 * ++percentComplete / calcItems, OptimizationLoopUIHelper.PrintPlanOptimizationConstraints(itr.Id, e.updatedObj));
                        UpdateConstraints(e.updatedObj, itr);
                    }
                }
                count++;
            }
            return false;
        }

        /// <summary>
        /// Helper method to evaluate the plan quality of the supplied plan against the supplied optimization constraints
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="optParams"></param>
        /// <returns></returns>
        private EvalPlanStruct EvaluatePlanSumComponentPlans(ExternalPlanSetup plan, List<Tuple<string, OptimizationObjectiveType, double, double, int>> optParams)
        {
            EvalPlanStruct e = new EvalPlanStruct();
            e.Construct(); 
            (double totalCostPlanOpt, List<Tuple<Structure, DVHData, double, double, double, int>> diffPlanOpt) = EvaluateResultVsOptimizationConstraints(plan, optParams);
            e.totalCostPlanOpt = totalCostPlanOpt;
            e.diffPlanOpt = diffPlanOpt;
            e.updatedObj = DetermineNewOptimizationObjectives(plan, e.diffPlanOpt, e.totalCostPlanOpt, optParams);
            return e;
        }

        /// <summary>
        /// Helper method to evaluate the plan quality of the sum plan agains the supplied planning objectives
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="planObj"></param>
        /// <returns></returns>
        private bool EvaluatePlanSumQuality(ExternalPlanSetup plan, List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> planObj)
        {
            UpdateUILabel($"Plan sum evaluation: {plan.Id}");
            ProvideUIUpdate($"Parsing optimization objectives from plan: {plan.Id}");
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> optParams = OptimizationSetupUIHelper.ReadConstraintsFromPlan(plan);
            //get current optimization objectives from plan (we could use the optParams list, but we want the actual instances of the OptimizationObjective class so we can get the results from each objective)
            (int, int, double, List<Tuple<Structure, DVHData, double, double>>) planObjectiveEvaluation = EvaluateResultVsPlanObjectives(plan, planObj, optParams);
            //all constraints met, exiting
            if (planObjectiveEvaluation.Item1 == planObjectiveEvaluation.Item2) return true;
            return false;
        }

        //private string AppExePath(string exeName)
        //{
        //    return FirstExePathIn(Path.GetDirectoryName(GetSourceFilePath()), exeName);
        //}

        //private string FirstExePathIn(string dir, string exeName)
        //{
        //    return Directory.GetFiles(dir, "*.exe").FirstOrDefault(x => x.Contains(exeName));
        //}

        //private string GetSourceFilePath([CallerFilePath] string sourceFilePath = "")
        //{
        //    return @"\\enterprise.stanfordmed.org\depts\RadiationTherapy\Public\Users\ESimiele\Research\VMAT-TBI-CSI\bin\";
        //}

        #endregion
    }
}
