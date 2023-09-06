using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.Structs;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.UIHelpers;
using VMATTBICSIAutoPlanningHelpers.Prompts;
using OptimizationProgressWindow;
using System.Text;

namespace VMATTBICSIAutoPlanningHelpers.BaseClasses
{
    public class OptimizationLoopBase : OptimizationMTbase
    {
        protected OptDataContainer _data;
        protected bool _checkSupportStructures = false;
        protected int overallPercentCompletion = 0;
        protected int overallCalcItems = 1;

        /// <summary>
        /// Simple method to initialize the log file path, the log file name, and the file name for the temporary errors and warnings file
        /// </summary>
        protected void InitializeLogPathAndName()
        {
            logPath = _data.logFilePath + "\\optimization\\" + _data.id + "\\";
            string currentDateTime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            fileName = logPath + currentDateTime + ".txt";
            fileNameErrorsWarnings = logPath + currentDateTime + "-EWs" + ".txt";
            /*
             * prelimiary check
             * coverage check ?
             * num opt
             * per opt TBI --> opt, dose calc, opt, dose calc, norm, eval, update
             * per opt CSI initial --> opt, dose calc, norm, eval, update
             * per opt CSI sequential --> (opt, dose calc, norm, eval, update) x 2
             * additional opt
             */
        }

        #region print run setup, failed message, plan dose info, etc.
        /// <summary>
        /// Helper method to print that either optimization or dose calculation failed and the reason why it failed
        /// </summary>
        /// <param name="optorcalc"></param>
        /// <param name="reason"></param>
        protected void PrintFailedMessage(string optorcalc, string reason = "")
        {
            if(string.IsNullOrEmpty(reason))
            {
                ProvideUIUpdate($"Error! {optorcalc} failed!" + Environment.NewLine + " Try running the {0} manually Eclipse for more information!" + Environment.NewLine + Environment.NewLine + " Exiting!", true);
            }
            else
            {
                ProvideUIUpdate($"Error! {optorcalc} failed because: {reason}" + Environment.NewLine + Environment.NewLine + " Exiting!", true);
            }
        }

        /// <summary>
        /// Simple method to print all of the relevant optimization loop run setup information to the user
        /// </summary>
        protected void PrintRunSetupInfo()
        {
            ProvideUIUpdate(OptimizationLoopUIHelper.GetRunSetupInfoHeader(_data.plans, _data.planType, _data.runCoverageCheck, _data.numOptimizations, _data.oneMoreOpt, _data.copyAndSavePlanItr, _data.targetVolCoverage));
            ProvideUIUpdate(OptimizationLoopUIHelper.PrintPlanObjectives(_data.planObj));
            ProvideUIUpdate(OptimizationLoopUIHelper.PrintRequestedTSStructures(_data.requestedTSstructures));
        }
        #endregion

        #region preliminary checks
        /// <summary>
        /// Helper method to check the attributes of the structure set, image, and integrity of the targets that will be used for optimization
        /// </summary>
        /// <param name="ss"></param>
        /// <param name="targetIDs"></param>
        /// <returns></returns>
        protected bool PreliminaryChecksSSAndImage(StructureSet ss, List<string> targetIDs)
        {
            int percentComplete = 0;
            int calcItems = 2 + targetIDs.Count;

            //check if the user assigned the imaging device Id. If not, the optimization will crash with no error
            if (string.IsNullOrEmpty(ss.Image.Series.ImagingDeviceId))
            {
                ProvideUIUpdate("Error! Did you forget to set the imaging device to 'Def_CTScanner'?", true);
                return true;
            }
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Imaging device Id: {ss.Image.Series.ImagingDeviceId}");

            //is the user origin set, does the body exist, and is the user origin inside the body
            if (!ss.Image.HasUserOrigin || !StructureTuningHelper.DoesStructureExistInSS("Body", ss, true) || !StructureTuningHelper.GetStructureFromId("Body", ss).IsPointInsideSegment(ss.Image.UserOrigin))
            {
                ProvideUIUpdate("Did you forget to set the user origin?" + Environment.NewLine + "User origin is NOT inside body contour!" + Environment.NewLine + "Please fix and try again!", true);
                return true;
            }
            ProvideUIUpdate(100 * ++percentComplete / calcItems, "User origin assigned and located within body structure");

            foreach (string itr in targetIDs)
            {
                if(!StructureTuningHelper.DoesStructureExistInSS(itr, ss, true))
                {
                    ProvideUIUpdate($"Error! Target: {itr} is missing from structure set or empty! Please fix and try again!", true);
                    return true;
                }
                else ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Target: {itr} is in structure set and is not null");
            }

            UpdateOverallProgress(100 * ++overallPercentCompletion / overallCalcItems);
            return false;
        }

        /// <summary>
        /// Preliminary checks for the couch structures if they exist. Primarily if support structure contours exist on the first and last slices of the CT image
        /// </summary>
        /// <param name="ss"></param>
        /// <returns></returns>
        protected bool PreliminaryChecksCouch(StructureSet ss)
        {
            int percentComplete = 0;
            int calcItems = 2;

            //grab all couch structures including couch surface, rails, etc. Also grab the matchline and spinning manny couch (might not be present depending on the size of the patient)
            List<Structure> couchAndRails = ss.Structures.Where(x => x.Id.ToLower().Contains("couch") || x.Id.ToLower().Contains("rail")).ToList();
            ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Retrieved list of couch structures ({couchAndRails.Count} structures found)");

            //check to see if the couch and rail structures are present in the structure set. If not, let the user know as an FYI. At this point, the user can choose to stop the optimization loop and add the couch structures
            if (!couchAndRails.Any())
            {
                ConfirmPrompt CP = new ConfirmPrompt("I didn't found any couch structures in the structure set!" + Environment.NewLine + Environment.NewLine + "Continue?!");
                CP.ShowDialog();
                if (!CP.GetSelection())
                {
                    ProvideUIUpdate("Quitting!", true);
                    return true;
                }
            }

            //now check if the couch and spinning manny structures are present on the first and last slices of the CT image
            if (couchAndRails.Any() && couchAndRails.Any(x => !x.IsEmpty))
            {
                if(couchAndRails.Any(x => x.GetContoursOnImagePlane(0).Any()) || couchAndRails.Any(x => x.GetContoursOnImagePlane(ss.Image.ZSize - 1).Any())) _checkSupportStructures = true;
                ProvideUIUpdate(100 * ++percentComplete / calcItems, "Checking if couch structures are on first or last slices of image");
            }
            else ProvideUIUpdate(100 * ++percentComplete / calcItems, "No couch structures present --> nothing to check");

            UpdateOverallProgress(100 * ++overallPercentCompletion / overallCalcItems);
            return false;
        }

        /// <summary>
        /// Helper method to crop the couch structures from the first and last slices of the CT image prior to starting the optimization loop
        /// </summary>
        /// <param name="courses"></param>
        /// <param name="ss"></param>
        /// <returns></returns>
        protected bool CheckSupportStructures(List<Course> courses, StructureSet ss)
        {
            int percentComplete = 0;
            int calcItems = 5;

            ProvideUIUpdate("Support structures found on first and last slices of image!");
            //couch structures found on first and last slices of CT image. Ask the user if they want to remove the contours for these structures on these image slices
            //We've found that eclipse will throw warning messages after each dose calculation if the couch structures are on the last slices of the CT image. The reason is because a beam could exit the support
            //structure (i.e., the couch) through the end of the couch thus exiting the CT image altogether. Eclipse warns that you are transporting radiation through a structure at the end of the CT image, which
            //defines the world volume (i.e., outside this volume, the radiation transport is killed)
            ConfirmPrompt CP = new ConfirmPrompt("I found couch contours on the first or last slices of the CT image!" + Environment.NewLine + Environment.NewLine +
                                                 "Do you want to remove them?!" + Environment.NewLine + "(The script will be less likely to throw warnings)");
            CP.ShowDialog();
            ProvideUIUpdate(100 * ++percentComplete / calcItems);
            //remove all applicable contours on the first and last CT slices
            if (CP.GetSelection())
            {
                //If dose has been calculated for this plan, need to clear the dose in this and any and all plans that reference this structure set
                //check to see if this structure set is used in any other calculated plans
                
                ProvideUIUpdate(100 * ++percentComplete / calcItems, "Retrieved all plans that use this structure set that have dose calculated");

                (List<ExternalPlanSetup> otherPlans, StringBuilder planIdList) = OptimizationLoopHelper.GetOtherPlansWithSameSSWithCalculatedDose(courses, ss);
                if (otherPlans.Any())
                {
                    string message = "The following plans have dose calculated and use the same structure set:" + Environment.NewLine;
                    message += planIdList.ToString();
                    message += Environment.NewLine + "I need to reset the dose matrix, crop the structures, then re-calculate the dose." + Environment.NewLine + "Continue?!";
                    CP = new ConfirmPrompt(message);
                    CP.ShowDialog();
                    //the user dosen't want to continue
                    if (CP.GetSelection())
                    {
                        List<ExternalPlanSetup> planRecalcList = new List<ExternalPlanSetup> { };
                        foreach (ExternalPlanSetup itr in otherPlans)
                        {
                            if (!_data.plans.Any(x => x == itr)) planRecalcList.Add(itr);
                        }
                        ProvideUIUpdate(100 * ++percentComplete / calcItems, "Revised plan list to exclude plans that will be optimized");

                        //reset dose matrix for ALL plans
                        ResetDoseMatrix(otherPlans, percentComplete, calcItems);

                        //crop the couch structures
                        CropCouchStructures(ss, percentComplete, calcItems);

                        //only recalculate dose for all plans that are not currently up for optimization
                        ReCalculateDose(planRecalcList, percentComplete, calcItems);
                    }
                    else
                    {
                        KillOptimizationLoop();
                        return true;
                    }
                }
                else CropCouchStructures(ss, percentComplete, calcItems);
            }
            ProvideUIUpdate(100);
            UpdateOverallProgress(100 * ++overallPercentCompletion / overallCalcItems);
            return false;
        }

        /// <summary>
        /// Helper method to reset the dose calculation matrix for the supplied list of plans
        /// </summary>
        /// <param name="plans"></param>
        /// <param name="percentComplete"></param>
        /// <param name="calcItems"></param>
        protected void ResetDoseMatrix(List<ExternalPlanSetup> plans, int percentComplete, int calcItems)
        {
            calcItems += plans.Count;
            foreach (ExternalPlanSetup itr in plans)
            {
                string calcModel = itr.GetCalculationModel(CalculationType.PhotonVolumeDose);
                itr.ClearCalculationModel(CalculationType.PhotonVolumeDose);
                itr.SetCalculationModel(CalculationType.PhotonVolumeDose, calcModel);
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Reset dose matrix for plan: {itr.Id}");
            }
        }

        /// <summary>
        /// Helper method to crop the couch structures on the first and last slices of the CT image (helps reduce warning/error messages
        /// that halt the optimization loop until close)
        /// </summary>
        /// <param name="ss"></param>
        /// <param name="percentComplete"></param>
        /// <param name="calcItems"></param>
        /// <returns></returns>
        private bool CropCouchStructures(StructureSet ss, int percentComplete, int calcItems)
        {
            List<Structure> couchStructures = ss.Structures.Where(x => x.Id.ToLower().Contains("couch") || 
                                                                  x.Id.ToLower().Contains("rail") || 
                                                                  string.Equals(x.Id.ToLower(), "spinmannysurface") || 
                                                                  string.Equals(x.Id.ToLower(), "couchmannysurfac")).ToList();
            calcItems += couchStructures.Count;
            foreach (Structure itr in couchStructures)
            {
                //check to ensure the structure is actually contoured (otherwise you will likely get an error if the structure is null)
                if (!itr.IsEmpty)
                {
                    itr.ClearAllContoursOnImagePlane(0);
                    itr.ClearAllContoursOnImagePlane(ss.Image.ZSize - 1);
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Cropped structure: {itr.Id} from first and last slices of image");
                }
                else ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Structure: {itr.Id} is empty. Nothing to crop");
            }
            return false;
        }

        /// <summary>
        /// Simple helper method to recalculate dose for the supplied list of plans
        /// </summary>
        /// <param name="plans"></param>
        /// <param name="percentComplete"></param>
        /// <param name="calcItems"></param>
        protected void ReCalculateDose(List<ExternalPlanSetup> plans, int percentComplete, int calcItems)
        {
            calcItems += plans.Count;
            //recalculate dose for each plan that requires it
            foreach (ExternalPlanSetup itr in plans)
            {
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Re calculating dose for plan: {itr.Id}");
                itr.CalculateDose();
            }
        }
        
        /// <summary>
        /// Preliminary checks that should be performed for each of the supplied plans. In addition set some basic configuration settings for each
        /// of the supplied plans
        /// </summary>
        /// <param name="plans"></param>
        /// <returns></returns>
        protected bool PreliminaryChecksPlans(List<ExternalPlanSetup> plans)
        {
            int percentComplete = 0;
            int calcItems = 5 * plans.Count;

            foreach(ExternalPlanSetup itr in plans)
            {
                if (itr.Beams.Count() == 0)
                {
                    ProvideUIUpdate($"No beams present in plan: {itr.Id}!", true);
                    return true;
                }
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Beams present in plan: {itr.Id}");

                //check each beam to ensure the isoposition is rounded-off to the nearest 5mm
                calcItems += itr.Beams.Count();
                foreach (Beam b in itr.Beams)
                {
                    if (CheckIsocenterPositions(itr.StructureSet.Image.DicomToUser(b.IsocenterPosition, itr), 5.0, b.Id)) return true;
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Beam: {b.Id} isocenter ok");
                }

                //turn on jaw tracking if available
                try 
                { 
                    itr.OptimizationSetup.UseJawTracking = true;
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Enabled jaw tracking for plan: {itr.Id}");
                }
                catch (Exception e) 
                { 
                    ProvideUIUpdate(100 * ++percentComplete / calcItems, $"{e.Message}\nCannot set jaw tracking for this machine! Jaw tracking will not be enabled!"); 
                }

                //set auto NTO priority to zero (i.e., shut it off)
                itr.OptimizationSetup.AddAutomaticNormalTissueObjective(0.0);
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Set automatic NTO priority to 0 for plan: {itr.Id}");

                //be sure to set the dose value presentation to absolute! This is important for plan evaluation in the evaluateAndUpdatePlan method below
                itr.DoseValuePresentation = DoseValuePresentation.Absolute;
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Set dose value presentation to absolute for plan: {itr.Id}");
            }
            UpdateOverallProgress(100 * ++overallPercentCompletion / overallCalcItems);
            return false;
        }

        /// <summary>
        /// Simple helper method to ensure the all isocenters are rounded appropriately (rounded to the supplied nearestRoundedValue)
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="beamId"></param>
        /// <returns></returns>
        private bool CheckIsocenterPositions(VVector pos, double nearestRoundedValue, string beamId)
        {
            for (int i = 0; i < 3; i++)
            {
                //check that isocenter positions are rounded to the nearest 5 mm
                //calculate the modulus of the iso position and 5 mm
                double mod = Math.Abs(pos[i]) % nearestRoundedValue;
                if(mod >= (nearestRoundedValue / 2))
                {
                    //mod is > 2.5 --> check to see the 5 - mod isn't 4.99999999999999 
                    mod = nearestRoundedValue - mod;
                }
                if (mod > 1e-3)
                {
                    ProvideUIUpdate("Isocenter position is NOT rounded off!");
                    ProvideUIUpdate($"x, y, z, pos[i] % {nearestRoundedValue:0.0}, beam id \n{pos.x:0.0}, {pos.y:0.0}, {pos.z:0.0}, {Math.Abs(pos[i]) % nearestRoundedValue:0.0}, {beamId}", true);
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region optimization loop
        /// <summary>
        /// Main controller for controlling the flow of the optimization loop for initial only and sequential boost cases
        /// </summary>
        /// <param name="plans"></param>
        /// <returns></returns>
        protected virtual bool RunOptimizationLoop(List<ExternalPlanSetup> plans)
        {
            UpdateUILabel("Optimization Loop:");
            //need to determine if we only need to optimize one plan (or an initial and boost plan)
            if (plans.Count == 1)
            {
                if (RunOptimizationLoopInitialPlanOnly(plans.First())) return true;
            }
            else
            {
                if (RunSequentialPlansOptimizationLoop(plans)) return true;
            }
            if (ResolveRunOptions(plans)) return true;
            if (!_data.isDemo) _data.app.SaveModifications();
            return false;
        }

        /// <summary>
        /// Virtual method to be overriden in the child classes to determine how to resolve final run options (specific to TBI/CSI plan types)
        /// </summary>
        /// <param name="plans"></param>
        /// <returns></returns>
        protected virtual bool ResolveRunOptions(List<ExternalPlanSetup> plans)
        {
            return true;
        }

        /// <summary>
        /// Helper method to run one more optimization for each of the supplied plans in an attempt to lower the hotspots in the plan
        /// </summary>
        /// <param name="plans"></param>
        /// <returns></returns>
        protected bool RunOneMoreOptionizationToLowerHotspots(List<ExternalPlanSetup> plans)
        {
            int percentComplete = 0;
            int calcItems = 3 * plans.Count;

            foreach (ExternalPlanSetup itr in plans)
            {
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Running one final optimization to try and reduce global plan hotspots for plan: {itr.Id}!");
                ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");

                //one final push to lower the global plan hotspot if the user asked for it
                if (OptimizePlan(_data.isDemo, new OptimizationOptionsVMAT(OptimizationOption.ContinueOptimizationWithPlanDoseAsIntermediateDose, ""), itr, _data.app)) return true;
                UpdateOverallProgress(100 * ++overallPercentCompletion / overallCalcItems);
                ProvideUIUpdate(100 * ++percentComplete / calcItems, "Optimization finished! Calculating dose!");
                ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");

                if (CalculateDose(_data.isDemo, itr, _data.app)) return true;
                UpdateOverallProgress(100 * ++overallPercentCompletion / overallCalcItems);
                ProvideUIUpdate(100 * ++percentComplete / calcItems, "Dose calculated, normalizing plan!");
                ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");

                //normalize
                if(NormalizePlan(itr, TargetsHelper.GetTargetStructureForPlanType(_data.selectedSS, OptimizationLoopHelper.GetNormaliztionVolumeIdForPlan(itr.Id, _data.normalizationVolumes), _data.useFlash, _data.planType), _data.relativeDose, _data.targetVolCoverage)) return true;
                UpdateOverallProgress(100 * ++overallPercentCompletion / overallCalcItems);
                ProvideUIUpdate($"{itr.Id} normalized!");

                //print requested additional info about the plan
                ProvideUIUpdate(OptimizationLoopUIHelper.PrintAdditionalPlanDoseInfo(_data.requestedPlanDoseInfo, itr, _data.normalizationVolumes));
            }
            return false;
        }

        /// <summary>
        /// Method to control the flow of the optimization loop for initial-only plan cases (TBI and CSI-initial)
        /// </summary>
        /// <param name="plan"></param>
        /// <returns></returns>
        protected virtual bool RunOptimizationLoopInitialPlanOnly(ExternalPlanSetup plan)
        {
            int percentComplete = 0;
            int calcItems = 1 + 7 * _data.numOptimizations;

            //update the current optimization parameters for this iteration
            InitializeOptimizationConstriants(plan);

            if (_data.isDemo) Thread.Sleep(3000);
            else _data.app.SaveModifications();

            ProvideUIUpdate("Starting optimization loop!");
            //counter to keep track of how many optimization iterations have been performed
            int count = 0;
            while (count < _data.numOptimizations)
            {
                bool isFinalOpt = (_data.oneMoreOpt && ((count + 1) == _data.numOptimizations));
                ProvideUIUpdate(100 * (++percentComplete) / calcItems, $"Iteration {count + 1}:");
                ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");

                if (OptimizePlan(_data.isDemo, new OptimizationOptionsVMAT(OptimizationIntermediateDoseOption.NoIntermediateDose, ""), plan, _data.app)) return true;
                ProvideUIUpdate(100 * (++percentComplete) / calcItems, "Optimization finished! Calculating intermediate dose!");
                ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");

                if (CalculateDose(_data.isDemo, plan, _data.app)) return true;
                ProvideUIUpdate(100 * (++percentComplete) / calcItems, "Dose calculated! Continuing optimization!");
                ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");

                if (OptimizePlan(_data.isDemo, new OptimizationOptionsVMAT(OptimizationOption.ContinueOptimizationWithPlanDoseAsIntermediateDose, ""), plan, _data.app)) return true;
                ProvideUIUpdate(100 * (++percentComplete) / calcItems, "Optimization finished! Calculating dose!");
                ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");

                if (CalculateDose(_data.isDemo, plan, _data.app)) return true;
                ProvideUIUpdate(100 * (++percentComplete) / calcItems, "Dose calculated, normalizing plan!");
                ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");

                if (NormalizePlan(plan, TargetsHelper.GetTargetStructureForPlanType(_data.selectedSS, OptimizationLoopHelper.GetNormaliztionVolumeIdForPlan(plan.Id, _data.normalizationVolumes), _data.useFlash, _data.planType), _data.relativeDose, _data.targetVolCoverage)) return true;
                ProvideUIUpdate(100 * (++percentComplete) / calcItems, "Plan normalized! Evaluating plan quality and updating constraints!");

                //evaluate the new plan for quality and make any adjustments to the optimization parameters
                EvalPlanStruct e = EvaluateAndUpdatePlan(plan, _data.planObj, isFinalOpt);
                if (e.wasKilled) return true;
                else if (e.allObjectivesMet)
                {
                    //updated optimization constraint list is empty, which means that all plan objectives have been met. 
                    //Let the user know and break the loop. Also set oneMoreOpt to false so that extra optimization is not performed
                    ProvideUIUpdate("All plan objectives have been met! Exiting!", true);
                    _data.oneMoreOpt = false;
                    return false;
                }

                //did the user request to copy and save each plan iteration from the optimization loop?
                //the last two boolean evaluations check if the user requested one more optimization (always copy and save) or this is not the last loop iteration (used in the case where the user elected NOT to do one more optimization
                //but still wants to copy and save each plan). We don't want to copy and save the plan on the last loop iteration when oneMoreOpt is false because we will end up with two copies of
                //the same plan!
                if (!_data.isDemo && _data.copyAndSavePlanItr && (_data.oneMoreOpt || ((count + 1) != _data.numOptimizations))) CopyAndSavePlan(plan, count);

                ProvideUIUpdate(OptimizationLoopUIHelper.PrintPlanOptimizationResultVsConstraints(plan, OptimizationSetupUIHelper.ReadConstraintsFromPlan(plan), e.diffPlanOpt, e.totalCostPlanOpt));
                ProvideUIUpdate(OptimizationLoopUIHelper.PrintAdditionalPlanDoseInfo(_data.requestedPlanDoseInfo, plan, _data.normalizationVolumes));

                //really crank up the priority and lower the dose objective on the cooler on the last iteration of the optimization loop
                //this is basically here to avoid having to call op.updateConstraints a second time (if this batch of code was placed outside of the loop)
                if (isFinalOpt) e.updatedObj = OptimizationLoopHelper.IncreaseOptConstraintPrioritiesForFinalOpt(e.updatedObj);

                //print updated optimization constraints
                ProvideUIUpdate(100 * ++percentComplete / calcItems, OptimizationLoopUIHelper.PrintPlanOptimizationConstraints(plan.Id, e.updatedObj));

                //update the optimization constraints in the plan
                UpdateConstraints(e.updatedObj, plan);

                //increment the counter, update d.optParams so it is set to the initial optimization constraints at the BEGINNING of the optimization iteration, and save the changes to the plan
                count++;
            }
            return false;
        }

        /// <summary>
        /// Virtual method for running sequential optimization to the supplied list of plans
        /// </summary>
        /// <param name="plans"></param>
        /// <returns></returns>
        protected virtual bool RunSequentialPlansOptimizationLoop(List<ExternalPlanSetup> plans)
        {
            return false;
        }
        #endregion

        #region helper functions during optimization
        protected bool OptimizePlan(bool isDemo, OptimizationOptionsVMAT options, ExternalPlanSetup plan, Application app)
        {
            UpdateUILabel("Optimization:");
            if (isDemo) Thread.Sleep(3000);
            else
            {
                try
                {
                    OptimizerResult optRes = plan.OptimizeVMAT(options);
                    if (!optRes.Success)
                    {
                        PrintFailedMessage("Optimization");
                        return true;
                    }
                }
                catch (Exception except)
                {
                    PrintFailedMessage("Optimization", except.Message);
                    return true;
                }
                app.SaveModifications();
            }
            UpdateOverallProgress(100 * ++overallPercentCompletion / overallCalcItems);
            //check if user wants to stop
            if (GetAbortStatus())
            {
                KillOptimizationLoop();
                return true;
            }
            return false;
        }

        public bool CalculateDose(bool isDemo, ExternalPlanSetup plan, Application app)
        {
            UpdateUILabel("Dose calculation:");
            if (isDemo) Thread.Sleep(3000);
            else
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                WinUtilitiesModified.LaunchWindowsClosingThread(cts.Token, fileNameErrorsWarnings);
                try
                {
                    CalculationResult calcRes = plan.CalculateDose();
                    if (!calcRes.Success)
                    {
                        cts.Cancel();
                        PrintFailedMessage("Dose calculation");
                        return true;
                    }
                }
                catch (Exception except)
                {
                    cts.Cancel();
                    PrintFailedMessage("Dose calculation", except.Message);
                    return true;
                }
                app.SaveModifications();
                cts.Cancel();
            }
            UpdateOverallProgress(100 * ++overallPercentCompletion / overallCalcItems);
            //check if user wants to stop
            if (GetAbortStatus())
            {
                KillOptimizationLoop();
                return true;
            }
            return false;
        }

        protected bool CopyAndSavePlan(ExternalPlanSetup plan, int count)
        {
            UpdateUILabel("Copy and save plan:");
            Course c = plan.Course;
            //this copies the plan and the dose!
            ExternalPlanSetup newPlan = (ExternalPlanSetup)c.CopyPlanSetup(plan);
            string newPlanId = String.Format("opt itr {0}{1}", plan.Id, count + 1);
            if (newPlanId.Length > 16) newPlanId = newPlanId.Substring(0, 16);
            newPlan.Id = newPlanId;
            ProvideUIUpdate(String.Format("Copying plan: {0} and saving as: {1}", plan.Id, newPlan.Id));
            return false;
        }

        protected virtual bool InitializeOptimizationConstriants(ExternalPlanSetup plan)
        {
            int percentComplete = 0;
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> originalOptObj = OptimizationSetupUIHelper.ReadConstraintsFromPlan(plan);
            int calcItems = originalOptObj.Count();
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> optObj = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
            int priority;
            
            UpdateUILabel("Initialize constraints:");
            ProvideUIUpdate(OptimizationLoopUIHelper.GetOptimizationObjectivesHeader(plan.Id));
            foreach (Tuple<string, OptimizationObjectiveType, double, double, int> opt in originalOptObj)
            {
                //leave the PTV priorities at their original values (i.e., 100)
                if (opt.Item1.ToLower().Contains("ptv") || opt.Item1.ToLower().Contains("ts_jnx")) priority = opt.Item5;
                //start OAR structure priorities at 2/3 of the values the user specified so there is some wiggle room for adjustment
                else priority = (int)Math.Ceiling(((double)opt.Item5 * 2) / 3);
                optObj.Add(Tuple.Create(opt.Item1, opt.Item2, opt.Item3, opt.Item4, priority));
                ProvideUIUpdate(100 * ++percentComplete / calcItems, String.Format("{0, -16} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", opt.Item1, opt.Item2.ToString(), opt.Item3, opt.Item4, priority));
            }
            ProvideUIUpdate(" ");
            UpdateConstraints(optObj, plan);

            UpdateOverallProgress(100 * ++overallPercentCompletion / overallCalcItems);
            return false;
        }

        protected bool UpdateConstraints(List<Tuple<string, OptimizationObjectiveType, double, double, int>> obj, ExternalPlanSetup plan)
        {
            int percentComplete = 0;
            int calcItems = plan.OptimizationSetup.Objectives.Count() + obj.Count();
            UpdateUILabel("Remove existing constraints:");
            //remove all existing optimization constraints
            foreach (OptimizationObjective o in plan.OptimizationSetup.Objectives)
            {
                plan.OptimizationSetup.RemoveObjective(o);
                ProvideUIUpdate(100 * ++percentComplete / calcItems);
            }

            UpdateUILabel("Assign updated constraints:");
            //assign the new optimization constraints (passed as an argument to this method)
            foreach (Tuple<string, OptimizationObjectiveType, double, double, int> opt in obj)
            {
                if (opt.Item2 != OptimizationObjectiveType.Mean) plan.OptimizationSetup.AddPointObjective(StructureTuningHelper.GetStructureFromId(opt.Item1, plan.StructureSet), OptimizationTypeHelper.GetObjectiveOperator(opt.Item2), new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), opt.Item4, opt.Item5);
                else plan.OptimizationSetup.AddMeanDoseObjective(StructureTuningHelper.GetStructureFromId(opt.Item1, plan.StructureSet), new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), opt.Item5);
                ProvideUIUpdate(100 * ++percentComplete / calcItems);
            }
            UpdateOverallProgress(100 * ++overallPercentCompletion / overallCalcItems);
            return false;
        }
        #endregion

        #region normalization
        /// <summary>
        /// Helper utility method to normalize the supplied plan to achieve the requested target coverage to the supplied target
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="target"></param>
        /// <param name="relativeDose"></param>
        /// <param name="targetVolCoverage"></param>
        /// <returns></returns>
        protected bool NormalizePlan(ExternalPlanSetup plan, Structure target, double relativeDose, double targetVolCoverage)
        {
            UpdateUILabel("Normalization:");
            //in demo mode, dose might not be calculated for the plan
            if (!plan.IsDoseValid)
            {
                ProvideUIUpdate($"Error! Dose for plan {plan.Id} is NOT valid! Cannot normalize! Exiting!", true);
                return true;
            }
            if (target == null || target.IsEmpty)
            {
                ProvideUIUpdate($"Error! Target/normalization structure for plan {plan.Id} is NOT null or empty! Cannot normalize! Exiting!", true);
                return true;
            }
            //how to normalize a plan in the ESAPI workspace:
            //reference: https://github.com/VarianAPIs/Varian-Code-Samples/blob/master/webinars%20%26%20workshops/Research%20Symposium%202015/Eclipse%20Scripting%20API/Projects/AutomatedPlanningDemo/PlanGeneration.cs
            plan.PlanNormalizationValue = 100.0;
            //absolute dose
            double RxDose = plan.TotalDose.Dose;
            //construct a DoseValue from RxDose
            DoseValue dv = new DoseValue(relativeDose * RxDose / 100, DoseValue.DoseUnit.cGy);
            //get current coverage of the RxDose
            double coverage = plan.GetVolumeAtDose(target, dv, VolumePresentation.Relative);

            ProvideUIUpdate($"{target.Id} V{relativeDose}% = {coverage:0.0}%");
            //if the current coverage doesn't equal the desired coverage, then renormalize the plan
            if (coverage != targetVolCoverage)
            {
                ProvideUIUpdate($"Renormalizing plan: {plan.Id} to acheive {target.Id} V{relativeDose}% >= {targetVolCoverage}");
                //get the dose that does cover the targetVolCoverage of the target volume and scale the dose distribution by the ratio of that dose to the relative prescription dose
                dv = plan.GetDoseAtVolume(target, targetVolCoverage, VolumePresentation.Relative, DoseValuePresentation.Absolute);
                double normValue = 100.0 * dv.Dose / (relativeDose * RxDose / 100);
                if (normValue < 0.01 || normValue > 10000.0)
                {
                    ProvideUIUpdate($"Calculated plan normalization value ({normValue}%) is outside of acceptable range: 0.01% - 10000.0%! Exiting", true);
                    return true;
                }
                plan.PlanNormalizationValue = normValue;
                ProvideUIUpdate($"{plan.Id} normalized. Normalization value = {normValue:0.0}%");
            }
            UpdateOverallProgress(100 * ++overallPercentCompletion / overallCalcItems);
            return false;
        }
        #endregion

        #region plan evaluation
        /// <summary>
        /// Helper method to control the flow of evaluating the plan quality of the supplied plan and updating the optimization constraints
        /// assigned to the plan
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="planObj"></param>
        /// <param name="finalOptimization"></param>
        /// <returns></returns>
        protected EvalPlanStruct EvaluateAndUpdatePlan(ExternalPlanSetup plan, 
                                                       List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> planObj, 
                                                       bool finalOptimization)
        {
            UpdateUILabel($"Plan evaluation: {plan.Id}");
            ProvideUIUpdate(Environment.NewLine + "Constructed evaluation data struct!");
            //create a new data structure to hold the results of the plan quality evaluation
            EvalPlanStruct e = new EvalPlanStruct();
            e.Construct();

            ProvideUIUpdate($"Parsing optimization objectives from plan: {plan.Id}");
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> optParams = OptimizationSetupUIHelper.ReadConstraintsFromPlan(plan);
            //get current optimization objectives from plan (we could use the optParams list, but we want the actual instances of the OptimizationObjective class so we can get the results from each objective)
            (int numComparison, int numPass, double totalCostPlanObj, List<Tuple<Structure, DVHData, double, double>> differenceFromPlanObj) = EvaluateResultVsPlanObjectives(plan, planObj, optParams);
            if (GetAbortStatus())
            {
                KillOptimizationLoop();
                e.wasKilled = true;
                return e;
            }

            e.diffPlanObj = differenceFromPlanObj;
            e.totalCostPlanObj = totalCostPlanObj;
            //all constraints met, exiting
            if (numComparison == numPass)
            {
                e.allObjectivesMet = true;
                return e;
            }
            ProvideUIUpdate("All plan objectives NOT met! Adjusting optimization parameters!");

            (double totalCostPlanOpt, List<Tuple<Structure, DVHData, double, double, double, int>> differenceFromOptConstraints) = EvaluateResultVsOptimizationConstraints(plan, optParams);
            e.totalCostPlanOpt = totalCostPlanOpt;
            e.diffPlanOpt = differenceFromOptConstraints;
            if (GetAbortStatus())
            {
                KillOptimizationLoop();
                e.wasKilled = true; 
                return e;
            }

            e.updatedObj = DetermineNewOptimizationObjectives(plan, e.diffPlanOpt, e.totalCostPlanOpt, optParams);
            if (GetAbortStatus())
            {
                KillOptimizationLoop();
                e.wasKilled = true; 
                return e;
            }

            (bool wasKilled, List<Tuple<string, OptimizationObjectiveType, double, double, int>> updatedOptConstraints) = UpdateHeaterCoolerStructures(plan, finalOptimization, _data.requestedTSstructures);

            //did the user abort the program while updating the heater and cooler structures
            if(wasKilled)
            {
                //user killed operation while generating heater and cooler structures
                KillOptimizationLoop();
                e.wasKilled = true;
                return e;
            }
            e.updatedObj.AddRange(updatedOptConstraints);
            
            UpdateOverallProgress(100 * ++overallPercentCompletion / overallCalcItems);
            return e;
        }

        /// <summary>
        /// Helper method to evaluate the plan quality of the supplied plan versus the supplied planning objectives
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="planObj"></param>
        /// <param name="optParams"></param>
        /// <returns></returns>
        protected (int, int, double, List<Tuple<Structure, DVHData, double, double>>) EvaluateResultVsPlanObjectives(ExternalPlanSetup plan, 
                                                                                                                     List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> planObj, 
                                                                                                                     List<Tuple<string, OptimizationObjectiveType, double,double,int>> optParams)
        {
            ProvideUIUpdate("Evluating optimization result vs plan objectives");
            int percentComplete = 0;
            int calcItems = 1 + planObj.Count();
            //counter to record the number of plan objective met
            int numPass = 0;
            int numComparisons = 0;
            double totalCostPlanObj = 0;
            List<Tuple<Structure, DVHData, double, double>> differenceFromPlanObj = new List<Tuple<Structure, DVHData, double, double>> { };
            
            //loop through all the plan objectives for this case and compare the actual dose to the dose in the plan objective.
            //If we met the constraint, increment numPass. At the end of the loop, if numPass == the number of plan objectives
            //then we have achieved the desired plan quality and can stop the optimization loop
            foreach (Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation> itr in planObj)
            {
                ProvideUIUpdate(100 * ++percentComplete / calcItems);
                //used to account for the case where there is a template plan objective that is not included in the current case (e.g., testes are not always spared)
                if (StructureTuningHelper.DoesStructureExistInSS(itr.Item1, plan.StructureSet, true))
                {
                    //similar to code to the foreach loop used to cycle through the optimization parameters
                    Structure s = StructureTuningHelper.GetStructureFromId(itr.Item1, plan.StructureSet);;
                    //this statement is difference from the dvh statement in the previous foreach loop because the dose is always expressed as an absolute value in the optimization objectives, but can be either relative or absolute in the plan objectives
                    //(itr.Item5 is the dose representation for this objective)
                    DVHData dvh = plan.GetDVHCumulativeData(s, itr.Item5, VolumePresentation.Relative, 0.1);
                    double diff = 0.0;
                    double cost = 0.0;
                    int optPriority = 0;

                    //NOTE: THERE MAY BE CASES WHERE A STRUCTURE MIGHT HAVE A PLAN OBJECTIVE, BUT NOT AN OPTIMIZATION OBJECTIVE(e.g., ovaries). Check if the structure of interest also has an optimization objective. If so, this indicates the user actually wanted to spare this
                    //structure for this plan and we should increment the number of comparisons counter. In addition, we need to copy the objective priority from the optimization objective if there is one
                    //If so, do a three-way comparison to find the correct optimization objective for this plan objective (compare based structureId, constraint type, and constraint volume). These three objectives will remain constant
                    //throughout the optimization process whereas the dose constraint will vary
                    List<Tuple<string, OptimizationObjectiveType, double, double, int>> copyOpt = (from p in optParams
                                                                                             where p.Item1.ToLower() == s.Id.ToLower()
                                                                                             where p.Item2 == itr.Item2
                                                                                             where p.Item4 == itr.Item4
                                                                                             select p).ToList();

                    //If the appropriate constraint was found, calculate the cost as the (dose diff)^2 * priority 
                    if (copyOpt.Any())
                    {
                        optPriority = copyOpt.First().Item5;
                        //ProvideUIUpdate(String.Format("Corresponding optimization objective found for plan objective: ({0},{1},{2},{3},{4})", itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5.ToString()));
                        //increment the number of comparisons since an optimization constraint was found
                        numComparisons++;
                    }
                    //if no exact constraint was found, leave the priority at zero (per Nataliya's instructions)

                    diff = PlanEvaluationHelper.GetDifferenceFromGoal(plan, itr, s, dvh);
                    if (diff <= 0.0)
                    {
                        //objective was met. Increment the counter for the number of objecives met
                        ProvideUIUpdate($"Plan objective met for: ({itr.Item1},{itr.Item2},{itr.Item3},{itr.Item4},{itr.Item5})");
                        numPass++;
                    }
                    else
                    {
                        cost = diff * diff * optPriority;
                        ProvideUIUpdate($"Plan objective NOT met for: ({itr.Item1},{itr.Item2},{itr.Item3},{itr.Item4},{itr.Item5})");
                    }

                    //add this comparison to the list and increment the running total of the cost for the plan objectives
                    differenceFromPlanObj.Add(Tuple.Create(s, dvh, diff * diff, cost));
                    totalCostPlanObj += cost;
                }
            }
            ProvideUIUpdate(100, $"Elapsed time: {GetElapsedTime()}");
            return (numComparisons, numPass, totalCostPlanObj, differenceFromPlanObj);
        }

        /// <summary>
        /// Helper method to evaluate the plan quality for the supplied plan and calculate the differences between the optimization dose objectives and the achieved doses
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="optParams"></param>
        /// <returns></returns>
        protected (double, List<Tuple<Structure, DVHData, double, double, double, int>>) EvaluateResultVsOptimizationConstraints(ExternalPlanSetup plan, 
                                                                                                                                 List<Tuple<string, OptimizationObjectiveType, double, double, int>> optParams)
        {
            ProvideUIUpdate("Evaluating optimization result vs optimization constraints:");
            //since we didn't meet all of the plan objectives, we now need to evaluate how well the plan compared to the desired plan objectives
            List<Tuple<Structure, DVHData, double, double, double, int>> differenceFromOptConstraints = new List<Tuple<Structure, DVHData, double, double, double, int>> { };
            //double to hold the total cost of the optimization
            double totalCostPlanOpt = 0;
            int percentComplete = 0;
            int calcItems = 1 + optParams.Count();
            foreach (Tuple<string, OptimizationObjectiveType, double, double, int> itr in optParams)
            {
                ProvideUIUpdate(100 * ++percentComplete / calcItems);
                //get the structure for each optimization object in optParams and its associated DVH
                Structure s = StructureTuningHelper.GetStructureFromId(itr.Item1, _data.selectedSS);
                //dose representation in optimization objectives is always absolute!
                DVHData dvh = plan.GetDVHCumulativeData(s, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);
                double diff = PlanEvaluationHelper.GetDifferenceFromGoal(plan, itr, s, dvh);

                //calculate the cost for this constraint as the dose difference squared times the constraint priority
                double cost = diff * diff * itr.Item5;

                //structure, dvh data, current dose obj, dose diff^2, cost, current priority
                differenceFromOptConstraints.Add(Tuple.Create(s, dvh, itr.Item3, diff * diff, cost, itr.Item5));
                //add the cost for this constraint to the running total
                totalCostPlanOpt += cost;
            }
            ProvideUIUpdate(100, $"Elapsed time: {GetElapsedTime()}");
            //save the total cost from this optimization
            return (totalCostPlanOpt, differenceFromOptConstraints);
        }

        /// <summary>
        /// Helper method to take the calculated plan quality metrics (i.e., diffPlanOpt list) and determine new optimization constraints for the supplied plan
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="diffPlanOpt"></param>
        /// <param name="totalCostOptimizationConstraints"></param>
        /// <param name="optParams"></param>
        /// <returns></returns>
        protected virtual List<Tuple<string, OptimizationObjectiveType, double, double, int>> DetermineNewOptimizationObjectives(ExternalPlanSetup plan, 
                                                                                                                                 List<Tuple<Structure, DVHData, double, double, double, int>> diffPlanOpt, 
                                                                                                                                 double totalCostOptimizationConstraints, 
                                                                                                                                 List<Tuple<string, OptimizationObjectiveType, double, double, int>> optParams)
        {
            ProvideUIUpdate("Determining new optimization objectives for next iteration");
            //not all plan objectives were met and now we need to do some investigative work to find out what failed and by how much
            //update optimization parameters based on how each of the structures contained in diffPlanOpt performed
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> updatedOptimizationConstraints = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
            int percentComplete = 0;
            int calcItems = 1 + diffPlanOpt.Count();
            int count = 0;
            foreach (Tuple<Structure, DVHData, double, double, double, int> itr in diffPlanOpt)
            {
                ProvideUIUpdate(100 * ++percentComplete / calcItems);
                double relative_cost = 0.0;
                //assign new objective dose and priority to the current dose and priority
                double newDose = itr.Item3;
                int newPriority = itr.Item6;
                //check to see if objective was met (i.e., was the cost > 0.). If objective was met, adjust nothing and copy the current optimization objective for this structure onto the updatedObj vector
                if (itr.Item5 > 0.0)
                {
                    //objective was not met. Determine what to adjust based on OPTIMIZATION OBJECTIVE parameters (not plan objective parameters)
                    relative_cost = itr.Item5 / totalCostOptimizationConstraints;

                    //do NOT adjust ptv dose constraints, only priorities (the ptv structures are going to have the highest relative cost of all the structures due to the difficulty in covering the entire PTV with 100% of the dose and keeing dMax low)
                    //If we starting adjusting the dose for these constraints, they would quickly escalate out of control, therefore, only adjust their priorities by a small amount
                    if (!itr.Item1.Id.ToLower().Contains("ptv") && !itr.Item1.Id.ToLower().Contains("ts_ring") && (relative_cost >= _data.threshold))
                    {
                        //OAR objective is greater than threshold, adjust dose. Evaluate difference between current actual dose and current optimization parameter setting. Adjust new objective dose by dose difference weighted by the relative cost
                        //=> don't push the dose too low, otherwise the constraints won't make sense. Currently, the lowest dose limit is 10% of the Rx dose (set by adjusting lowDoseLimit)
                        //this equation was (more or less) determined empirically:
                        // current dose obj - sqrt(dose diff from current obj) * relative cost * 2
                        if ((newDose - (Math.Sqrt(itr.Item4) * relative_cost * 2)) >= plan.TotalDose.Dose * _data.lowDoseLimit) newDose -= (Math.Sqrt(itr.Item4) * relative_cost * 2);
                        //else do nothing. This can be changed later to increase the priority instead of doing nothing
                    }
                    else
                    {
                        //OAR objective was less than threshold (or it was a ptv objective), adjust priority
                        //increase OAR objective priority by 100 times the relative cost of this objective
                        //increase PTV objective by 10 times the relative cost (need to have a much lower scaling factor, otherwise it will increase too rapidly)
                        double increase = 100 * relative_cost;
                        if (itr.Item1.Id.ToLower().Contains("ptv") || itr.Item1.Id.ToLower().Contains("ts_ring")) increase /= 10;
                        newPriority += (int)Math.Ceiling(increase);
                    }
                }

                //do NOT update the cooler and heater structure objectives (these will be removed, re-contoured, and re-assigned optimization objectives in the below statements)
                if (!optParams.ElementAt(count).Item1.ToLower().Contains("ts_heater") && !optParams.ElementAt(count).Item1.ToLower().Contains("ts_cooler"))
                {
                    updatedOptimizationConstraints.Add(Tuple.Create(optParams.ElementAt(count).Item1, optParams.ElementAt(count).Item2, newDose, optParams.ElementAt(count).Item4, newPriority));
                }
                count++;
            }
            ProvideUIUpdate(100, String.Format("Elapsed time: {0}", GetElapsedTime()));
            return updatedOptimizationConstraints;
        }
        #endregion

        #region heaters and cooler structure generation removal
        /// <summary>
        /// Helper method to update the status of the heater and cooler structures based on the results of the prior optimization loop
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="isFinalOptimization"></param>
        /// <param name="requestedTSStructures"></param>
        /// <param name="removeExistingHeaterCoolerStructures"></param>
        /// <returns></returns>
        protected virtual (bool, List<Tuple<string, OptimizationObjectiveType, double, double, int>>) UpdateHeaterCoolerStructures(ExternalPlanSetup plan, 
                                                                                                                                   bool isFinalOptimization, 
                                                                                                                                   List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> requestedTSStructures, 
                                                                                                                                   bool removeExistingHeaterCoolerStructures = true)
        {
            UpdateUILabel("Update TS heaters & coolers:");
            bool wasKilled = false;
            ProvideUIUpdate("Updating heater and cooler tuning structures for next iteration");
            int percentComplete = 0;
            int calcItems = 2 +_data.requestedTSstructures.Count();
            //first remove existing structures
            if(removeExistingHeaterCoolerStructures) RemoveCoolHeatStructures(plan);

            //list to hold info related to optimization constraints for any added heater and cooler structures
            List<Tuple<string, OptimizationObjectiveType, double, double, int>> heaterCoolerOptConstraints = OptimizationSetupUIHelper.ReadConstraintsFromPlan(plan).Where(x => x.Item1.ToLower().Contains("cooler") || x.Item1.ToLower().Contains("heater")).ToList();
            //now create new cooler and heating structures
            ProvideUIUpdate($"Retrieving target structure for plan: {plan.Id}");
            List<Tuple<string, string>> plansTargets = TargetsHelper.GetHighestRxPlanTargetList(_data.prescriptions);
            if (!plansTargets.Any())
            {
                ProvideUIUpdate("Error! Could not retrieve list of plans and associated targets! Exiting", true);
                wasKilled = true;
                return (wasKilled, heaterCoolerOptConstraints);
            }

            string targetId = "";
            if (plansTargets.Any(x => string.Equals(x.Item1, plan.Id))) targetId = plansTargets.First(x => string.Equals(x.Item1, plan.Id)).Item2;

            Structure target = TargetsHelper.GetTargetStructureForPlanType(_data.selectedSS, targetId, _data.useFlash, _data.planType);
            ProvideUIUpdate($"Retrieved target: {target.Id} for plan: {plan.Id} to evaluate requested heater/cooler structures");
            if (target == null || target.IsEmpty)
            {
                ProvideUIUpdate($"Error! Target structure not found or is empty for plan: {plan.Id}! Exiting!", true);
                wasKilled = true;
                return (wasKilled, heaterCoolerOptConstraints);
            }

            //iterate through the list of requested optimization tuning structures
            foreach (Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>> itr in requestedTSStructures)
            {
                ProvideUIUpdate(100 * ++percentComplete / calcItems);
                Tuple<string, OptimizationObjectiveType, double, double, int> TSstructure = null;
                //does it have constraints that need to be met before adding the TS structure?
                if (TSHeaterCoolerHelper.AllHeaterCoolerTSConstraintsMet(plan, target, itr.Item6, isFinalOptimization))
                {
                    ProvideUIUpdate($"All conditions met for: {itr.Item1}! Adding to structure set!");
                    if (itr.Item1.Contains("cooler"))
                    {
                        //cooler
                        (string update, Tuple<string, OptimizationObjectiveType, double, double, int> cooler) = TSHeaterCoolerHelper.GenerateCooler(plan, itr.Item2 / 100, itr.Item3 / 100, itr.Item4, itr.Item1, itr.Item5);
                        ProvideUIUpdate(update);
                        TSstructure = cooler;
                    }
                    else
                    {
                        //heater
                        (string update, Tuple<string, OptimizationObjectiveType, double, double, int> heater) = TSHeaterCoolerHelper.GenerateHeater(plan, target, itr.Item2 / 100, itr.Item3 / 100, itr.Item4, itr.Item1, itr.Item5);
                        ProvideUIUpdate(update);
                        TSstructure = heater;
                    }
                    if (TSstructure != null) heaterCoolerOptConstraints.Add(TSstructure);
                }
                else ProvideUIUpdate($"All conditions NOT met for: {itr.Item1}! Skipping!");
                if(GetAbortStatus())
                {
                    wasKilled = true;
                    return (wasKilled, heaterCoolerOptConstraints);
                }
            }
            ProvideUIUpdate(100, $"Elapsed time: {GetElapsedTime()}");
            return (wasKilled, heaterCoolerOptConstraints);
        }

        /// <summary>
        /// Helper method to retrieve all the heater and cooler structures in the structure set, and remove them
        /// </summary>
        /// <param name="plan"></param>
        protected void RemoveCoolHeatStructures(ExternalPlanSetup plan)
        {
            ProvideUIUpdate("Removing existing heater and cooler structures");
            StructureSet ss = plan.StructureSet;
            List<Structure> coolerHeater = ss.Structures.Where(x => x.Id.ToLower().Contains("ts_cooler") || x.Id.ToLower().Contains("ts_heater")).ToList();
            int percentComplete = 0;
            int calcItems = coolerHeater.Count();
            foreach (Structure itr in coolerHeater)
            {
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Removing structure: {itr.Id}");
                if (ss.CanRemoveStructure(itr)) ss.RemoveStructure(itr);
                else ProvideUIUpdate($"Warning! Cannot remove {itr.Id} from the structure set! Skipping!");
            }
        }
        #endregion
    }
}
