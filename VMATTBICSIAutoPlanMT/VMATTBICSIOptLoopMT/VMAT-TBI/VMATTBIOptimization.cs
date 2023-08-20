using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.UIHelpers;
using VMATTBICSIAutoPlanningHelpers.Structs;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Prompts;
using System.Text;

namespace VMATTBICSIOptLoopMT.VMAT_TBI
{
    class VMATTBIOptimization : OptimizationLoopBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="_d"></param>
        public VMATTBIOptimization(OptDataContainer _d)
        {
            _data = _d;
            InitializeLogPathAndName();
            CalculateNumberOfItemsToComplete();
        }

        /// <summary>
        /// Primary run control
        /// </summary>
        /// <returns></returns>
        public override bool Run()
        {
            try
            {
                SetAbortUIStatus("Runnning");
                PrintRunSetupInfo();
                //preliminary checks
                if (PreliminaryChecksSSAndImage(_data.selectedSS, TargetsHelper.GetAllTargetIds(_data.prescriptions))) return true;
                if (PreliminaryChecksCouch(_data.selectedSS)) return true;
                if (PreliminaryChecksSpinningManny(_data.selectedSS)) return true;
                if (_data.useFlash && PreliminaryChecksBolusOverlapWithSupport()) return true;
                if (PreliminaryChecksPlans(_data.plans)) return true;

                if (_data.isDemo || !_data.runCoverageCheck) ProvideUIUpdate(" Skipping coverage check! Moving on to optimization loop!");
                else
                {
                    foreach (ExternalPlanSetup itr in _data.plans)
                    {
                        if (RunCoverageCheck(itr, _data.relativeDose, _data.targetVolCoverage, _data.useFlash)) return true;
                        ProvideUIUpdate(String.Format(" Coverage check for plan {0} completed!",itr.Id));
                    }
                }
                ProvideUIUpdate(String.Format(" Commencing optimization loop!"));
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
            overallCalcItems = 4;
            overallCalcItems += _data.plans.Count;
            if (_data.runCoverageCheck) overallCalcItems += 4 * _data.plans.Count;
            int optLoopItems = 6 * _data.numOptimizations * _data.plans.Count;
            if (_data.oneMoreOpt) optLoopItems += 3;
            overallCalcItems += optLoopItems;
        }

        #region preliminary checks specific to TBI
        /// <summary>
        /// Preliminary checks 
        /// </summary>
        /// <param name="ss"></param>
        /// <returns></returns>
        private bool PreliminaryChecksSpinningManny(StructureSet ss)
        {
            int percentComplete = 0;
            int calcItems = 3;

            //check if there is a matchline contour. If so, is it empty?
            if (StructureTuningHelper.DoesStructureExistInSS("Matchline", ss, true))
            {
                ProvideUIUpdate(100 * ++percentComplete / calcItems, "Matchline structure present in structure set and is NOT empty");
                //if a matchline contour is present and filled, does the spinning manny couch exist in the structure set? 
                //If not, let the user know so they can decide if they want to continue of stop the optimization loop
                if (!StructureTuningHelper.DoesStructureExistInSS(new List<string> { "spinmannysurface", "couchmannysurfac" }, ss, true))
                {
                    ConfirmPrompt CP = new ConfirmPrompt("I found a matchline, but no spinning manny couch or it's empty!" + Environment.NewLine + Environment.NewLine + "Continue?!");
                    CP.ShowDialog();
                    if (!CP.GetSelection())
                    {
                        KillOptimizationLoop();
                        return true;
                    }
                }
            }
            else ProvideUIUpdate(100 * ++percentComplete / calcItems, "Matchline structure not found");

            Structure spinningManny = ss.Structures.FirstOrDefault(x => x.Id.ToLower() == "spinmannysurface" || x.Id.ToLower() == "couchmannysurfac");
            if (spinningManny == null) ProvideUIUpdate(100 * ++percentComplete / calcItems, "Spinning Manny structure not found");
            else ProvideUIUpdate(100 * ++percentComplete / calcItems, "Retrieved Spinning Manny structure");

            if (spinningManny != null && !spinningManny.IsEmpty)
            {
                if (spinningManny.GetContoursOnImagePlane(0).Any() || spinningManny.GetContoursOnImagePlane(ss.Image.ZSize - 1).Any()) _checkSupportStructures = true;
                ProvideUIUpdate(100 * ++percentComplete / calcItems, "Checking if Spinningy Manny structure is on first or last slices of image");
            }
            else ProvideUIUpdate(100 * ++percentComplete / calcItems, "No Spinning Manny structure present --> nothing to check");

            UpdateOverallProgress(100 * ++overallPercentCompletion / overallCalcItems);
            return false;
        }

        /// <summary>
        /// Preliminary checks to ensure the bolus_flash structure does not overlap with any of the support structures. If so, optimization will not be permitted. 
        /// Pre-emptively crop the bolus flash structure from the couch structures
        /// </summary>
        /// <returns></returns>
        private bool PreliminaryChecksBolusOverlapWithSupport()
        {
            ProvideUIUpdate(0,"Cropping bolus_flash from any support structures");
            int percentComplete = 0;
            int calcItems = 1;

            List<Structure> supports = _data.selectedSS.Structures.Where(x => x.DicomType.ToLower().Contains("support")).ToList();
            calcItems += supports.Count;

            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved list of support structures");
            if (supports.Any())
            {
                Structure bolus = StructureTuningHelper.GetStructureFromId("bolus_flash", _data.selectedSS);
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved virtual bolus structure");
                if (bolus == null || bolus.IsEmpty)
                {
                    ProvideUIUpdate($"Error! Could not retrieve bolus structure! Exiting!", true);
                    return true;
                }
                (List<ExternalPlanSetup> otherPlans, StringBuilder planIdList) = OptimizationLoopHelper.GetOtherPlansWithSameSSWithCalculatedDose(_data.plans.First().Course.Patient.Courses.ToList(), _data.selectedSS);
                calcItems += otherPlans.Count;
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved list of plans that use structure set: {_data.selectedSS.Id} and have dose calculated");

                List<ExternalPlanSetup> planRecalcList = new List<ExternalPlanSetup> { };
                if (otherPlans.Any())
                {
                    ProvideUIUpdate("The following plans have dose calculated and use the same structure set:");
                    ProvideUIUpdate(planIdList.ToString());

                    foreach (ExternalPlanSetup itr in otherPlans) if (!_data.plans.Any(x => x == itr)) planRecalcList.Add(itr);
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, "Revised plan list to exclude plans that will be optimized");
                    calcItems += planRecalcList.Count;

                    //reset dose matrix for ALL plans
                    ResetDoseMatrix(otherPlans, percentComplete, calcItems);
                }
                foreach (Structure itr in supports)
                {
                    //crop the couch structures
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Cropping {bolus.Id} from support structure: {itr.Id}");
                    ContourHelper.CropStructureFromStructure(bolus, itr, 0.0);
                }
                //only recalculate dose for all plans that are not currently up for optimization
                if (otherPlans.Any()) ReCalculateDose(planRecalcList, percentComplete, calcItems);
                ProvideUIUpdate(100, "Finished cropping bolus from support structures");
            }
            else
            {
                ProvideUIUpdate(100, "No support structures in the structure set! Skipping");
            }
            return false;
        }
        #endregion

        #region coverage check
        /// <summary>
        /// Helper method to run the coverage check for the supplied plan
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="relativeDose"></param>
        /// <param name="targetVolCoverage"></param>
        /// <param name="useFlash"></param>
        /// <returns></returns>
        private bool RunCoverageCheck(ExternalPlanSetup plan, double relativeDose, double targetVolCoverage, bool useFlash)
        {
            ProvideUIUpdate("Running coverage check..." + Environment.NewLine);
            //zero all optimization objectives except those in the target
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> optParams = OptimizationSetupUIHelper.ReadConstraintsFromPlan(plan);
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> targetOnlyObj = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };

            ProvideUIUpdate(OptimizationLoopUIHelper.GetOptimizationObjectivesHeader(plan.Id));
            int percentCompletion = 0;
            int calcItems = 5;
            foreach (Tuple<string, OptimizationObjectiveType, double, double, int> opt in optParams)
            {
                int priority = 0;
                if (opt.Item1.ToLower().Contains("ptv") || opt.Item1.ToLower().Contains("ts_jnx")) priority = opt.Item5;
                targetOnlyObj.Add(Tuple.Create(opt.Item1, opt.Item2, opt.Item3, opt.Item4, priority));
                //record the optimization constraints for each structure after zero-ing the priorities. This information will be reported to the user in a progress update
                ProvideUIUpdate(String.Format("{0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", opt.Item1, opt.Item2.ToString(), opt.Item3, opt.Item4, priority));
            }
            //update the constraints and provide an update to the user
            UpdateConstraints(targetOnlyObj, plan);
            ProvideUIUpdate(100 * ++percentCompletion / calcItems);

            //run one optimization with NO intermediate dose.
            if (OptimizePlan(_data.isDemo, new OptimizationOptionsVMAT(OptimizationIntermediateDoseOption.NoIntermediateDose, ""), plan, _data.app)) return true;

            ProvideUIUpdate(100 * ++percentCompletion / calcItems, "Optimization finished on coverage check! Calculating dose!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");

            //calculate dose (using AAA algorithm)
            if (CalculateDose(_data.isDemo, plan, _data.app)) return true;

            ProvideUIUpdate(100 * ++percentCompletion / calcItems, "Dose calculated for coverage check, normalizing plan!");

            //normalize plan
            NormalizePlan(plan, TargetsHelper.GetTargetStructureForPlanType(_data.selectedSS, OptimizationLoopHelper.GetNormaliztionVolumeIdForPlan(plan.Id, _data.normalizationVolumes), useFlash, _data.planType), relativeDose, targetVolCoverage);
            if (GetAbortStatus())
            {
                KillOptimizationLoop();
                return true;
            }

            ProvideUIUpdate(100 * ++percentCompletion / calcItems, "Plan normalized!");

            //print useful info about target coverage and global dmax
            ProvideUIUpdate(OptimizationLoopUIHelper.PrintAdditionalPlanDoseInfo(_data.requestedPlanDoseInfo, plan, _data.normalizationVolumes));

            //calculate global Dmax expressed as a percent of the prescription dose (if dose has been calculated)
            if (plan.IsDoseValid && ((plan.Dose.DoseMax3D.Dose / plan.TotalDose.Dose) > 1.40))
            {
                ProvideUIUpdate(Environment.NewLine +
                                $"I'm having trouble covering the target with the Rx Dose! Hot spot = {100 * (plan.Dose.DoseMax3D.Dose / plan.TotalDose.Dose):0.0}%" +
                                Environment.NewLine + "Consider stopping the optimization and checking the beam arrangement!");
            }
            return false;
        }
        #endregion

        #region optimization loop
        /// <summary>
        /// Overridden method to handle any remaining run options once the maximum number of iterations has been reached in the optimization loop
        /// </summary>
        /// <param name="plans"></param>
        /// <returns></returns>
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
            return false;
        }

        /// <summary>
        /// Helper method to remove the virtual bolus structure from the structure set, recalculate the dose, and renormalize to the original PTV without flash
        /// </summary>
        /// <param name="plans"></param>
        /// <returns></returns>
        private bool RemoveFlashAndRecalc(List<ExternalPlanSetup> plans)
        {
            ProvideUIUpdate(100 * ++overallPercentCompletion / overallCalcItems, Environment.NewLine + "Removing flash, recalculating dose, and renormalizing to TS_PTV_VMAT!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");

            Structure bolus = StructureTuningHelper.GetStructureFromId("bolus_flash", _data.selectedSS);;
            if (bolus == null)
            {
                //no structure named bolus_flash found. This is a problem. 
                ProvideUIUpdate("No structure named 'BOLUS_FLASH' found in structure set! Exiting!", true);
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
                    ProvideUIUpdate(100 * ++overallPercentCompletion / overallCalcItems, "Dose calculated, normalizing plan!");
                    ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
                    if(plans.Any(x => x == itr))
                    {
                        //force the plan to normalize to TS_PTV_VMAT after removing flash
                        NormalizePlan(itr, TargetsHelper.GetTargetStructureForPlanType(_data.selectedSS, "", false, _data.planType), _data.relativeDose, _data.targetVolCoverage);
                        ProvideUIUpdate(100 * ++overallPercentCompletion / overallCalcItems, "Plan normalized!");
                    }
                    else
                    {
                        ProvideUIUpdate(100 * ++overallPercentCompletion / overallCalcItems, $"Plan: {itr.Id} is not contained in the plan list! Skipping normalization!");
                    }
                }
            }
            return false;
        }
        #endregion
    }
}
