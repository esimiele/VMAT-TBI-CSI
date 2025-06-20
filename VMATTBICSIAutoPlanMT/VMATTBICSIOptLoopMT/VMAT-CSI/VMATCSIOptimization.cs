using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.UIHelpers;
using VMATTBICSIAutoPlanningHelpers.Models;
using VMATTBICSIAutoPlanningHelpers.DataContainers;

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
                if (PreliminaryChecksSSAndImage(_data.StructureSet, TargetsHelper.GetAllTargetIds(_data.Prescriptions).Any() ? TargetsHelper.GetAllTargetIds(_data.Prescriptions) : _data.NormalizationVolumes.Select(x => x.Value))) return true;
                if (PreliminaryChecksCouch(_data.StructureSet)) return true;
                if (PreliminaryChecksPlans(_data.Plans)) return true;
                if (RunOptimizationLoop(_data.Plans)) return true;
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
            overallCalcItems += _data.Plans.Count;
            int optLoopItems = 5 * _data.NumberOfIterations * _data.Plans.Count;
            if (_data.OneMoreOptimization) optLoopItems += 3;
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
            if (_data.OneMoreOptimization)
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
                    ProvideUIUpdate(OptimizationLoopUIHelper.PrintAdditionalPlanDoseInfo(_data.RequestedPlanMetrics, evalPlan, _data.NormalizationVolumes));
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
            List<RequestedOptimizationTSStructureModel> theList = new List<RequestedOptimizationTSStructureModel>
                        {
                            new TSCoolerStructureModel("TS_cooler101",107.0,101.0,100, 0.0)
                        };
            (bool fail, List<OptimizationConstraintModel> addedTSCoolerConstraint) = UpdateHeaterCoolerStructures(initialPlan, true, theList, false);
            if (fail)
            {
                //user killed operation while generating heater and cooler structures
                KillOptimizationLoop();
                return true;
            }
            ProvideUIUpdate(100 * ++percentComplete / calcItems);
            List<OptimizationConstraintModel> optParams = OptimizationSetupHelper.ReadConstraintsFromPlan(initialPlan);
            optParams.AddRange(addedTSCoolerConstraint);
            ProvideUIUpdate(100 * ++percentComplete / calcItems, OptimizationLoopUIHelper.PrintPlanOptimizationConstraints(initialPlan, optParams));

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
            Dictionary<string, string> plansTargets = TargetsHelper.GetHighestRxPlanTargetList(_data.Prescriptions);
            if(!plansTargets.Any())
            {
                ProvideUIUpdate("Prescriptions are missing! Using dictionary of plan normalization volumes as a surrogate");
                plansTargets = new Dictionary<string, string>(_data.NormalizationVolumes);
                if(!plansTargets.Any())
                {
                    ProvideUIUpdate("Error! plan normalization volume dictionary is empty! Cannot determine the appropriate target for each plan! Exiting!", true);
                    return true;
                }
            }
            int percentComplete = 0;
            int calcItems = 5 * plans.Count() * _data.NumberOfIterations;
            //first need to create a plan sum
            evalPlan = CreatePlanSum(_data.StructureSet, plans);
            if (evalPlan == null) return true;

            foreach (ExternalPlanSetup itr in plans) InitializeOptimizationConstriants(itr);

            ProvideUIUpdate("Starting optimization loop!");
            int count = 0;
            while(count < _data.NumberOfIterations)
            {
                bool oneMoreOptNextItr = _data.OneMoreOptimization && count + 1 == _data.NumberOfIterations;
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Iteration {count + 1}:");
                ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
                foreach (ExternalPlanSetup itr in plans)
                {
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Optimizing plan: {itr.Id}!");
                    if (OptimizePlan(_data.IsDemo, new OptimizationOptionsVMAT(OptimizationIntermediateDoseOption.NoIntermediateDose, ""), itr, _data.Application)) return true;
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, "Optimization finished! Calculating dose!");
                    if (CalculateDose(_data.IsDemo, itr, _data.Application)) return true;
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, "Dose calculated, normalizing plan!");
                    //normalize
                    if(NormalizePlan(itr, 
                                     TargetsHelper.GetTargetStructureForPlanType(_data.StructureSet, 
                                                                                 OptimizationLoopHelper.GetNormaliztionVolumeIdForPlan(itr.Id, _data.NormalizationVolumes), 
                                                                                 _data.UseFlash, 
                                                                                 _data.PlanType), 
                                     _data.TreatmentPercentage, 
                                     _data.TargetCoverageNormalization)) return true;
                    if (GetAbortStatus())
                    {
                        KillOptimizationLoop();
                        return true;
                    }
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, "Plan normalized!");
                    ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
                }
                
                if (BuildPlanSum(evalPlan, plans)) return true;
                ProvideUIUpdate(OptimizationLoopUIHelper.PrintAdditionalPlanDoseInfo(_data.RequestedPlanMetrics, evalPlan, _data.NormalizationVolumes));

                if (EvaluatePlanSumQuality(evalPlan, _data.PlanObjectives))
                {
                    ProvideUIUpdate($"All plan objectives met for plan sum: {evalPlan.Id}! Exiting!");
                    return false;
                }
                else
                {
                    ProvideUIUpdate("All plan objectives NOT met! Updating heater and cooler structures!");
                    (bool fail, List<OptimizationConstraintModel> updatedHeaterCoolerConstraints) = UpdateHeaterCoolerStructures(evalPlan, oneMoreOptNextItr, _data.RequestedOptimizationTSStructures);
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
                        List<OptimizationConstraintModel> optParams = OptimizationSetupHelper.ReadConstraintsFromPlan(itr);
                        ProvideUIUpdate($"Evaluating quality of plan: {itr.Id}!");
                        PlanEvaluationDataContainer e = EvaluatePlanSumComponentPlans(itr, optParams);
                        if (e.OptimizationKilledByUser) return true;

                        ProvideUIUpdate(OptimizationLoopUIHelper.PrintPlanOptimizationResultVsConstraints(itr, optParams, e.PlanDifferenceFromOptConstraints, e.TotalOptimizationCostOptConstraints));
                        ProvideUIUpdate(OptimizationLoopUIHelper.PrintAdditionalPlanDoseInfo(_data.RequestedPlanMetrics, itr, _data.NormalizationVolumes));

                        ProvideUIUpdate($"Scaling optimization parameters for heater cooler structures for plan: {itr.Id}!");
                        e.UpdatedOptimizationObjectives.AddRange(OptimizationLoopHelper.ScaleHeaterCoolerOptConstraints(itr.TotalDose.Dose, evalPlan.TotalDose.Dose, updatedHeaterCoolerConstraints));

                        if(oneMoreOptNextItr) e.UpdatedOptimizationObjectives = OptimizationLoopHelper.IncreaseOptConstraintPrioritiesForFinalOpt(e.UpdatedOptimizationObjectives);

                        ProvideUIUpdate(100 * ++percentComplete / calcItems, OptimizationLoopUIHelper.PrintPlanOptimizationConstraints(itr, e.UpdatedOptimizationObjectives));
                        UpdateConstraints(e.UpdatedOptimizationObjectives, itr);
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
        private PlanEvaluationDataContainer EvaluatePlanSumComponentPlans(ExternalPlanSetup plan, List<OptimizationConstraintModel> optParams)
        {
            PlanEvaluationDataContainer e = new PlanEvaluationDataContainer();
            List<PlanOptConstraintsDeviationModel> diffPlanOpt = EvaluateResultVsOptimizationConstraints(plan, optParams);
            e.TotalOptimizationCostOptConstraints = diffPlanOpt.Sum(x => x.OptimizationCost);
            e.PlanDifferenceFromOptConstraints = diffPlanOpt;
            e.UpdatedOptimizationObjectives = DetermineNewOptimizationObjectives(plan, e.PlanDifferenceFromOptConstraints, e.TotalOptimizationCostOptConstraints, optParams);
            return e;
        }

        /// <summary>
        /// Helper method to evaluate the plan quality of the sum plan agains the supplied planning objectives
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="planObj"></param>
        /// <returns></returns>
        private bool EvaluatePlanSumQuality(ExternalPlanSetup plan, List<PlanObjectiveModel> planObj)
        {
            UpdateUILabel($"Plan sum evaluation: {plan.Id}");
            ProvideUIUpdate($"Parsing optimization objectives from plan: {plan.Id}");
            List<OptimizationConstraintModel> optParams = OptimizationSetupHelper.ReadConstraintsFromPlan(plan);
            //get current optimization objectives from plan (we could use the optParams list, but we want the actual instances of the OptimizationObjective class so we can get the results from each objective)
            List<PlanObjectivesDeviationModel> diffPlanObj = EvaluateResultVsPlanObjectives(plan, planObj, optParams);
            //all constraints met, exiting
            if ( diffPlanObj.All(x => x.ObjectiveMet == true)) return true;
            return false;
        }
        #endregion
    }
}
