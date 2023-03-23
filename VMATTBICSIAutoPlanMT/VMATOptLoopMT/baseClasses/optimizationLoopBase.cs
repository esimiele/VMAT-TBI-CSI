using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Threading;
using System.Diagnostics;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIOptLoopMT.PlanEvaluation;
using VMATTBICSIOptLoopMT.Helpers;
using VMATTBICSIAutoplanningHelpers.Helpers;
using VMATTBICSIAutoplanningHelpers.UIHelpers;
using VMATTBICSIAutoplanningHelpers.Prompts;

namespace VMATTBICSIOptLoopMT.baseClasses
{
    public class optimizationLoopBase : MTbase
    {
        protected dataContainer _data;
        protected bool _checkSupportStructures = false;
        protected int overallPercentCompletion = 0;
        protected int overallCalcItems = 1;

        protected void InitializeLogPathAndName()
        {
            logPath = _data.logFilePath + "\\optimization\\" + _data.id + "\\";
            fileName = logPath + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt";
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
        protected void PrintFailedMessage(string optorcalc, string reason = "")
        {
            if(string.IsNullOrEmpty(reason))
            {
                ProvideUIUpdate(String.Format(" Error! {0} failed!" + Environment.NewLine + " Try running the {0} manually Eclipse for more information!" + Environment.NewLine + Environment.NewLine + " Exiting!", optorcalc), true);
            }
            else
            {
                ProvideUIUpdate(String.Format(" Error! {0} failed because: {1}" + Environment.NewLine + Environment.NewLine + " Exiting!", optorcalc, reason), true);
            }
        }

        protected void PrintAdditionalPlanDoseInfo(List<Tuple<string,string,double,string>> requestedInfo, ExternalPlanSetup plan)
        {
            Structure structure;
            List<Structure> planStructures = _data.selectedSS.Structures.ToList();
            
            ProvideUIUpdate(String.Format(Environment.NewLine + " Additional infomation for plan: {0}" + Environment.NewLine, plan.Id));
            foreach(Tuple<string,string,double,string> itr in requestedInfo)
            {
                if(itr.Item1.Contains("<plan>"))
                {
                    if (itr.Item2 == "Dmax") ProvideUIUpdate(String.Format(" Plan global Dmax = {0:0.0}%", 100 * (plan.Dose.DoseMax3D.Dose / plan.TotalDose.Dose)));
                    else ProvideUIUpdate(String.Format("Cannot retrive metric ({0},{1},{2},{3})! Skipping!", itr.Item1, itr.Item2, itr.Item3, itr.Item4));
                }
                else
                {
                    structure = planStructures.FirstOrDefault(x => x.Id.ToLower() == itr.Item1.ToLower());
                    if(structure != null)
                    {
                        if(itr.Item2.Contains("max") || itr.Item2.Contains("min"))
                        {
                            ProvideUIUpdate(String.Format(" {0} {1} = {2:0.0}{3}", 
                                            structure.Id, 
                                            itr.Item2, 
                                            plan.GetDoseAtVolume(structure, itr.Item2 == "Dmax" ? 0.0 : 100.0, VolumePresentation.Relative, itr.Item4 == "Relative" ? DoseValuePresentation.Relative : DoseValuePresentation.Absolute).Dose, 
                                            itr.Item4 == "Relative" ? "%" : "cGy"));
                        }
                        else
                        {
                            if(itr.Item2 == "D")
                            {
                                //dose at specified volume requested
                                ProvideUIUpdate(String.Format(" {0} {1}{2}% = {3:0.0}{4}",
                                            structure.Id,
                                            itr.Item2,
                                            itr.Item3,
                                            plan.GetDoseAtVolume(structure, itr.Item3, VolumePresentation.Relative, itr.Item4 == "Relative" ? DoseValuePresentation.Relative : DoseValuePresentation.Absolute).Dose,
                                            itr.Item4 == "Relative" ? "%" : "cGy"));
                            }
                            else
                            {
                                //volume at specified dose requested
                                ProvideUIUpdate(String.Format(" {0} {1}{2}% = {3:0.0}{4}",
                                            structure.Id,
                                            itr.Item2,
                                            itr.Item3,
                                            plan.GetVolumeAtDose(structure, new DoseValue(itr.Item3, DoseValue.DoseUnit.Percent), itr.Item4 == "Relative" ? VolumePresentation.Relative : VolumePresentation.AbsoluteCm3),
                                            itr.Item4 == "Relative" ? "%" : "cc"));
                            }
                        }
                    }
                    else ProvideUIUpdate(String.Format("Cannot retrive metric ({0},{1},{2},{3})! Skipping!", itr.Item1, itr.Item2, itr.Item3, itr.Item4));

                }
            }
        }

        protected void PrintRunSetupInfo(List<ExternalPlanSetup> plans)
        {
            string optimizationLoopSetupInfo = String.Format(" ---------------------------------------------------------------------------------------------------------" + Environment.NewLine +
                                        " Date: {0}" + Environment.NewLine +
                                        " Plan type: {1}" + Environment.NewLine +
                                        " Optimization loop settings:" + Environment.NewLine +
                                        " Run coverage check: {2}" + Environment.NewLine +
                                        " Max number of optimizations: {3}" + Environment.NewLine +
                                        " Run additional optimization to lower hotspots: {4}" + Environment.NewLine +
                                        " Copy and save each optimized plan: {5}" + Environment.NewLine +
                                        " Plan normalization:" + Environment.NewLine,
                                        DateTime.Now, _data.planType, _data.runCoverageCheck, _data.numOptimizations, _data.oneMoreOpt, _data.copyAndSavePlanItr);

            foreach(ExternalPlanSetup itr in plans)
            {
                optimizationLoopSetupInfo += String.Format("     Plan: {0}, PTV V{1}cGy = {2:0.0}%" + Environment.NewLine, itr.Id, itr.TotalDose.Dose, _data.targetVolCoverage);
            }

            ProvideUIUpdate(optimizationLoopSetupInfo);
            PrintPlanObjectives();
            PrintRequestedTSStructures();
        }

        protected void PrintPlanObjectives()
        {
            string optPlanObjHeader = " Plan objectives:" + Environment.NewLine;
            optPlanObjHeader += " --------------------------------------------------------------------------" + Environment.NewLine;
            optPlanObjHeader += String.Format(" {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -9} |", "structure Id", "constraint type", "dose", "volume (%)", "dose type") + Environment.NewLine;
            optPlanObjHeader += " --------------------------------------------------------------------------";
            ProvideUIUpdate(optPlanObjHeader);

            foreach (Tuple<string, string, double, double, DoseValuePresentation> itr in _data.planObj)
            {
                //"structure Id", "constraint type", "dose (cGy or %)", "volume (%)", "Dose display (absolute or relative)"
                ProvideUIUpdate(String.Format(" {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-9} |", itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5));
            }
            ProvideUIUpdate(Environment.NewLine);
        }

        protected void PrintPlanOptimizationConstraints(string planId, List<Tuple<string, string, double, double, int>> constraints, int calcItems, ref int percentComplete)
        {
            ProvideUIUpdate(GetOptimizationObjectivesHeader(planId));
            foreach (Tuple<string, string, double, double, int> itr in constraints)
            {
                ProvideUIUpdate(String.Format(" {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5));
            }
            ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), " ");
        }

        protected void PrintRequestedTSStructures()
        {
            string optRequestTS = String.Format(" Requested tuning structures:") + Environment.NewLine;
            optRequestTS += " --------------------------------------------------------------------------" + Environment.NewLine;
            optRequestTS += String.Format(" {0, -15} | {1, -9} | {2, -10} | {3, -5} | {4, -8} | {5, -10} |", "structure Id", "low D (%)", "high D (%)", "V (%)", "priority", "constraint") + Environment.NewLine;
            optRequestTS += " --------------------------------------------------------------------------";
            ProvideUIUpdate(optRequestTS);

            foreach (Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>> itr in _data.requestedTSstructures)
            {
                string msg = String.Format(" {0, -15} | {1, -9:N1} | {2,-10:N1} | {3,-5:N1} | {4,-8} |", itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5);
                if (!itr.Item6.Any()) msg += String.Format(" {0,-10} |", "none");
                else
                {
                    int index = 0;
                    foreach (Tuple<string, double, string, double> itr1 in itr.Item6)
                    {
                        if (index == 0)
                        {
                            if (itr1.Item1.Contains("Dmax")) msg += String.Format(" {0,-10} |", String.Format("{0}{1}{2}%", itr1.Item1, itr1.Item3, itr1.Item4));
                            else if (itr1.Item1.Contains("V")) msg += String.Format(" {0,-10} |", String.Format("{0}{1}%{2}{3}%", itr1.Item1, itr1.Item2, itr1.Item3, itr1.Item4));
                            else msg += String.Format(" {0,-10} |", String.Format("{0}", itr1.Item1));
                        }
                        else
                        {
                            if (itr1.Item1.Contains("Dmax")) msg += String.Format(" {0,-59} | {1,-10} |", " ", String.Format("{0}{1}{2}%", itr1.Item1, itr1.Item3, itr1.Item4));
                            else if (itr1.Item1.Contains("V")) msg += String.Format(" {0,-59} | {1,-10} |", " ", String.Format("{0}{1}%{2}{3}%", itr1.Item1, itr1.Item2, itr1.Item3, itr1.Item4));
                            else msg += String.Format(" {0,-59} | {1,-10} |", " ", String.Format("{0}", itr1.Item1));
                        }
                        index++;
                        if (index < itr.Item6.Count) msg += Environment.NewLine;
                    }
                }
                ProvideUIUpdate(msg);
            }
        }

        protected string GetOptimizationObjectivesHeader(string planId)
        {
            string optObjHeader = Environment.NewLine + String.Format("Updated optimization constraints for plan: {0}", planId) + Environment.NewLine;
            optObjHeader += " -------------------------------------------------------------------------" + Environment.NewLine;
            optObjHeader += String.Format(" {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |" + Environment.NewLine, "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority");
            optObjHeader += " -------------------------------------------------------------------------";
            return optObjHeader;
        }

        protected string GetOptimizationResultsHeader(string planId)
        {
            string optResHeader = String.Format(" Results of optimization for plan: {0}" + Environment.NewLine, planId);
            optResHeader += " ---------------------------------------------------------------------------------------------------------" + Environment.NewLine;
            optResHeader += String.Format(" {0, -15} | {1, -16} | {2, -20} | {3, -16} | {4, -12} | {5, -9} |" + Environment.NewLine, "structure Id", "constraint type", "dose diff^2 (cGy^2)", "current priority", "cost", "cost (%)");
            optResHeader += " ---------------------------------------------------------------------------------------------------------";
            return optResHeader;
        }
        #endregion

        #region preliminary checks
        //only need to be done once per optimization loop
        protected bool PreliminaryChecksSSAndImage(StructureSet ss, List<string> targetIDs)
        {
            int percentComplete = 0;
            int calcItems = 2 + targetIDs.Count;

            //check if the user assigned the imaging device Id. If not, the optimization will crash with no error
            if (ss.Image.Series.ImagingDeviceId == "")
            {
                ProvideUIUpdate("Error! Did you forget to set the imaging device to 'Def_CTScanner'?", true);
                return true;
            }
            ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Imaging device Id: {0}", ss.Image.Series.ImagingDeviceId));

            //is the user origin inside the body?
            if (!ss.Image.HasUserOrigin || !(ss.Structures.FirstOrDefault(x => x.Id.ToLower() == "body").IsPointInsideSegment(ss.Image.UserOrigin)))
            {
                ProvideUIUpdate(String.Format("Did you forget to set the user origin?" + Environment.NewLine + "User origin is NOT inside body contour!" + Environment.NewLine + "Please fix and try again!"), true);
                return true;
            }
            ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("User origin assigned and located within body structure"));


            foreach (string itr in targetIDs)
            {
                if(!ss.Structures.Any(x => x.Id.ToLower() == itr.ToLower() && !x.IsEmpty))
                {
                    ProvideUIUpdate(String.Format("Error! Target: {0} is missing from structure set or empty! Please fix and try again!", itr), true);
                    return true;
                }
                else ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Target: {0} is in structure set and is not null", itr));
            }

            UpdateOverallProgress((int)(100 * (++overallPercentCompletion) / overallCalcItems));
            return false;
        }

        protected bool PreliminaryChecksCouch(StructureSet ss)
        {
            int percentComplete = 0;
            int calcItems = 2;

            //grab all couch structures including couch surface, rails, etc. Also grab the matchline and spinning manny couch (might not be present depending on the size of the patient)
            List<Structure> couchAndRails = ss.Structures.Where(x => x.Id.ToLower().Contains("couch") || x.Id.ToLower().Contains("rail")).ToList();
            ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Retrieved list of couch structures ({0} structures found)", couchAndRails.Count));

            //check to see if the couch and rail structures are present in the structure set. If not, let the user know as an FYI. At this point, the user can choose to stop the optimization loop and add the couch structures
            if (!couchAndRails.Any())
            {
                confirmUI CUI = new confirmUI();
                CUI.message.Text = String.Format("I didn't found any couch structures in the structure set!") + Environment.NewLine + Environment.NewLine + "Continue?!";
                CUI.ShowDialog();
                if (!CUI.confirm)
                {
                    ProvideUIUpdate("Quitting!", true);
                    return true;
                }
            }

            //now check if the couch and spinning manny structures are present on the first and last slices of the CT image
            if ((couchAndRails.Any() && couchAndRails.Where(x => !x.IsEmpty).Any()))
            {
                if(couchAndRails.Where(x => x.GetContoursOnImagePlane(0).Any()).Any() || couchAndRails.Where(x => x.GetContoursOnImagePlane(ss.Image.ZSize - 1).Any()).Any()) _checkSupportStructures = true;
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Checking if couch structures are on first or last slices of image", _checkSupportStructures));
            }
            else ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("No couch structures present --> nothing to check"));

            UpdateOverallProgress((int)(100 * (++overallPercentCompletion) / overallCalcItems));
            return false;
        }

        protected bool PreliminaryChecksSpinningManny(StructureSet ss)
        {
            int percentComplete = 0;
            int calcItems = 3;

            Structure spinningManny = ss.Structures.FirstOrDefault(x => x.Id.ToLower() == "spinmannysurface" || x.Id.ToLower() == "couchmannysurfac");
            if(spinningManny == null) ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Spinning Manny structure not found"));
            else ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Retrieved Spinning Manny structure"));

            Structure matchline = ss.Structures.FirstOrDefault(x => x.Id.ToLower() == "matchline");
            if (matchline == null) ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Matchline structure not found"));
            else ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Retrieved Matchline structure"));

            //check if there is a matchline contour. If so, is it empty?
            if (matchline != null && !matchline.IsEmpty)
            {
                //if a matchline contour is present and filled, does the spinning manny couch exist in the structure set? 
                //If not, let the user know so they can decide if they want to continue of stop the optimization loop
                if (spinningManny == null || spinningManny.IsEmpty)
                {
                    confirmUI CUI = new confirmUI();
                    CUI.message.Text = String.Format("I found a matchline, but no spinning manny couch or it's empty!") + Environment.NewLine + Environment.NewLine + "Continue?!";
                    CUI.ShowDialog();
                    if (!CUI.confirm) return true;
                }
            }

            if ((spinningManny != null && !spinningManny.IsEmpty))
            {
                if (spinningManny.GetContoursOnImagePlane(0).Any() || spinningManny.GetContoursOnImagePlane(ss.Image.ZSize - 1).Any()) _checkSupportStructures = true;
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Checking if Spinningy Manny structure is on first or last slices of image", _checkSupportStructures));
            }
            else ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("No Spinning Manny structure present --> nothing to check"));

            UpdateOverallProgress((int)(100 * (++overallPercentCompletion) / overallCalcItems));
            return false;
        }

        protected bool CheckSupportStructures(List<Course> courses, StructureSet ss)
        {
            int percentComplete = 0;
            int calcItems = 5;

            ProvideUIUpdate(String.Format("Support structures found on first and last slices of image!"));
            //couch structures found on first and last slices of CT image. Ask the user if they want to remove the contours for these structures on these image slices
            //We've found that eclipse will throw warning messages after each dose calculation if the couch structures are on the last slices of the CT image. The reason is because a beam could exit the support
            //structure (i.e., the couch) through the end of the couch thus exiting the CT image altogether. Eclipse warns that you are transporting radiation through a structure at the end of the CT image, which
            //defines the world volume (i.e., outside this volume, the radiation transport is killed)
            confirmUI CUI = new confirmUI();
            CUI.message.Text = String.Format("I found couch contours on the first or last slices of the CT image!") + Environment.NewLine + Environment.NewLine +
                                                "Do you want to remove them?!" + Environment.NewLine + "(The script will be less likely to throw warnings)";
            CUI.ShowDialog();
            ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems));
            //remove all applicable contours on the first and last CT slices
            if (CUI.confirm)
            {
                //If dose has been calculated for this plan, need to clear the dose in this and any and all plans that reference this structure set
                //check to see if this structure set is used in any other calculated plans
                string message = "The following plans have dose calculated and use the same structure set:" + Environment.NewLine;
                List<ExternalPlanSetup> otherPlans = new List<ExternalPlanSetup> { };
                foreach (Course c in courses)
                {
                    foreach (ExternalPlanSetup p in c.ExternalPlanSetups)
                    {
                        if (p.IsDoseValid && p.StructureSet == ss)
                        {
                            message += String.Format("Course: {0}, Plan: {1}", c.Id, p.Id) + Environment.NewLine;
                            otherPlans.Add(p);
                        }
                    }
                }
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Retrieved all plans that use this structure set that have dose calculated"));

                if (otherPlans.Count > 0)
                {
                    message += Environment.NewLine + "I need to reset the dose matrix, crop the structures, then re-calculate the dose." + Environment.NewLine + "Continue?!";
                    //8-15-2020 dumbass way around the whole "dose has been calculated, you can't change anything!" issue.
                    CUI = new confirmUI();
                    CUI.message.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
                    CUI.message.Text = message;
                    CUI.ShowDialog();
                    //the user dosen't want to continue
                    if (!CUI.confirm) return true;
                    else
                    {
                        List<ExternalPlanSetup> planRecalcList = new List<ExternalPlanSetup> { };
                        foreach (ExternalPlanSetup itr in otherPlans) if (!_data.plans.Where(x => x == itr).Any()) planRecalcList.Add(itr);
                        ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Revised plan list to exclude plans that will be optimized"));

                        //reset dose matrix for ALL plans
                        ResetDoseMatrix(otherPlans, percentComplete, calcItems);

                        //crop the couch structures
                        CropCouchStructures(ss, percentComplete, calcItems);

                        //only recalculate dose for all plans that are not currently up for optimization
                        ReCalculateDose(planRecalcList, percentComplete, calcItems);
                    }
                }
            }
            ProvideUIUpdate(100);
            UpdateOverallProgress((int)(100 * (++overallPercentCompletion) / overallCalcItems));
            return false;
        }

        private void ResetDoseMatrix(List<ExternalPlanSetup> plans, int percentComplete, int calcItems)
        {
            calcItems += plans.Count;
            foreach (ExternalPlanSetup itr in plans)
            {
                string calcModel = itr.GetCalculationModel(CalculationType.PhotonVolumeDose);
                itr.ClearCalculationModel(CalculationType.PhotonVolumeDose);
                itr.SetCalculationModel(CalculationType.PhotonVolumeDose, calcModel);
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Reset dose matrix for plan: {0}", itr.Id));
            }
        }

        private bool CropCouchStructures(StructureSet ss, int percentComplete, int calcItems)
        {
            List<Structure> couchStructures = ss.Structures.Where(x => x.Id.ToLower().Contains("couch") || x.Id.ToLower().Contains("rail") || x.Id.ToLower() == "spinmannysurface" || x.Id.ToLower() == "couchmannysurfac").ToList();
            calcItems += couchStructures.Count;
            foreach (Structure itr in couchStructures)
            {
                //check to ensure the structure is actually contoured (otherwise you will likely get an error if the structure is null)
                if (!itr.IsEmpty)
                {
                    itr.ClearAllContoursOnImagePlane(0);
                    itr.ClearAllContoursOnImagePlane(ss.Image.ZSize - 1);
                    ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Cropped structure: {0} from first and last slices of image", itr.Id));
                }
                else ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Structure: {0} is empty. Nothing to crop", itr.Id));
            }
            return false;
        }

        private void ReCalculateDose(List<ExternalPlanSetup> plans, int percentComplete, int calcItems)
        {
            calcItems += plans.Count;
            //recalculate dose for each plan that requires it
            foreach (ExternalPlanSetup itr in plans)
            {
                itr.CalculateDose();
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Calculated dose for plan: {0}", itr.Id));
            }
        }
        
        protected bool PreliminaryChecksPlans(List<ExternalPlanSetup> plans)
        {
            int percentComplete = 0;
            int calcItems = 5 * plans.Count;

            foreach(ExternalPlanSetup itr in plans)
            {
                if (itr.Beams.Count() == 0)
                {
                    ProvideUIUpdate("No beams present in the VMAT TBI plan!", true);
                    return true;
                }
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Beams present in plan: {0}", itr.Id));

                //check each beam to ensure the isoposition is rounded-off to the nearest 5mm
                calcItems += itr.Beams.Count();
                foreach (Beam b in itr.Beams)
                {
                    if (CheckIsocenterPositions(itr.StructureSet.Image.DicomToUser(b.IsocenterPosition, itr), b.Id)) return true;
                    ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Beam: {0} isocenter ok", b.Id));
                }

                //turn on jaw tracking if available
                try 
                { 
                    itr.OptimizationSetup.UseJawTracking = true;
                    ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Enabled jaw tracking for plan: {0}", itr.Id));
                }
                catch (Exception e) 
                { 
                    ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), $"{e.Message}\nCannot set jaw tracking for this machine! Jaw tracking will not be enabled!"); 
                }
                //set auto NTO priority to zero (i.e., shut it off)
                itr.OptimizationSetup.AddAutomaticNormalTissueObjective(0.0);
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Set automatic NTO priority to 0 for plan: {0}", itr.Id));

                //be sure to set the dose value presentation to absolute! This is important for plan evaluation in the evaluateAndUpdatePlan method below
                itr.DoseValuePresentation = DoseValuePresentation.Absolute;
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Set dose value presentation to absolute for plan: {0}", itr.Id));
            }
            UpdateOverallProgress((int)(100 * (++overallPercentCompletion) / overallCalcItems));
            return false;
        }

        private bool CheckIsocenterPositions(VVector pos, string beamId)
        {
            for (int i = 0; i < 3; i++)
            {
                //check that isocenter positions are rounded to the nearest 5 mm
                //ProvideUIUpdate(String.Format("i, pos[i], pos[i] % 1, beam id \n{0}, {1}, {2}, {3}", i, pos[i], Math.Abs(pos[i]) % 5, beamId));
                if (Math.Abs(pos[i]) % 5 > 1e-3)
                {
                    ProvideUIUpdate("Isocenter position is NOT rounded off!");
                    ProvideUIUpdate(String.Format("x, y, z, pos[i] % 1, beam id \n{0}, {1}, {2}, {3}, {4}", pos.x, pos.y, pos.z, Math.Abs(pos[i]) % 10, beamId), true);
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region optimization loop
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

        protected virtual bool ResolveRunOptions(List<ExternalPlanSetup> plans)
        {
            return true;
        }

        protected bool RunOneMoreOptionizationToLowerHotspots(List<ExternalPlanSetup> plans)
        {
            int percentComplete = 0;
            int calcItems = 3 * plans.Count;

            foreach (ExternalPlanSetup itr in plans)
            {
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format(" Running one final optimization to try and reduce global plan hotspots for plan: {0}!", itr.Id));
                ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));

                //one final push to lower the global plan hotspot if the user asked for it
                if (OptimizePlan(_data.isDemo, new OptimizationOptionsVMAT(OptimizationOption.ContinueOptimizationWithPlanDoseAsIntermediateDose, ""), itr, _data.app)) return true;
                UpdateOverallProgress((int)(100 * (++overallPercentCompletion) / overallCalcItems));

                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), " Optimization finished! Calculating dose!");
                ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));

                if (CalculateDose(_data.isDemo, itr, _data.app)) return true;
                UpdateOverallProgress((int)(100 * (++overallPercentCompletion) / overallCalcItems));

                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), " Dose calculated, normalizing plan!");
                ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));

                //normalize
                NormalizePlan(itr, new TargetsHelper().GetTargetForPlan(_data.selectedSS, null, _data.useFlash, _data.planType), _data.relativeDose, _data.targetVolCoverage);
                UpdateOverallProgress((int)(100 * (++overallPercentCompletion) / overallCalcItems));
                ProvideUIUpdate(String.Format(" {0} normalized!", itr.Id));

                //print requested additional info about the plan
                PrintAdditionalPlanDoseInfo(_data.requestedPlanDoseInfo, itr);
            }
            return false;
        }

        protected virtual bool RunOptimizationLoopInitialPlanOnly(ExternalPlanSetup plan)
        {
            int percentComplete = 0;
            int calcItems = 1 + 7 * _data.numOptimizations;

            //update the current optimization parameters for this iteration
            InitializeOptimizationConstriants(plan);

            if (_data.isDemo) Thread.Sleep(3000);
            else _data.app.SaveModifications();

            ProvideUIUpdate(" Starting optimization loop!");
            //counter to keep track of how many optimization iterations have been performed
            int count = 0;
            while (count < _data.numOptimizations)
            {
                bool isFinalOpt = (_data.oneMoreOpt && ((count + 1) == _data.numOptimizations));
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format(" Iteration {0}:", count + 1));
                ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));
                if (OptimizePlan(_data.isDemo, new OptimizationOptionsVMAT(OptimizationIntermediateDoseOption.NoIntermediateDose, ""), plan, _data.app)) return true;

                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), " Optimization finished! Calculating intermediate dose!");
                ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));
                if (CalculateDose(_data.isDemo, plan, _data.app)) return true;

                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), " Dose calculated! Continuing optimization!");
                ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));
                if (OptimizePlan(_data.isDemo, new OptimizationOptionsVMAT(OptimizationOption.ContinueOptimizationWithPlanDoseAsIntermediateDose, ""), plan, _data.app)) return true;

                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), " Optimization finished! Calculating dose!");
                ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));
                if (CalculateDose(_data.isDemo, plan, _data.app)) return true;

                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), " Dose calculated, normalizing plan!");
                ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));
                NormalizePlan(plan, new TargetsHelper().GetTargetForPlan(_data.selectedSS, "", _data.useFlash, _data.planType), _data.relativeDose, _data.targetVolCoverage);

                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), " Plan normalized! Evaluating plan quality and updating constraints!"); ;
                //evaluate the new plan for quality and make any adjustments to the optimization parameters
                EvalPlanStruct e = EvaluateAndUpdatePlan(plan, _data.planObj, isFinalOpt);
                
                if (e.wasKilled) return true;
                else if (e.allObjectivesMet)
                {
                    //updated optimization constraint list is empty, which means that all plan objectives have been met. 
                    //Let the user know and break the loop. Also set oneMoreOpt to false so that extra optimization is not performed
                    ProvideUIUpdate(String.Format(" All plan objectives have been met! Exiting!"), true);
                    _data.oneMoreOpt = false;
                    return false;
                }

                //did the user request to copy and save each plan iteration from the optimization loop?
                //the last two boolean evaluations check if the user requested one more optimization (always copy and save) or this is not the last loop iteration (used in the case where the user elected NOT to do one more optimization
                //but still wants to copy and save each plan). We don't want to copy and save the plan on the last loop iteration when oneMoreOpt is false because we will end up with two copies of
                //the same plan!
                if (!_data.isDemo && _data.copyAndSavePlanItr && (_data.oneMoreOpt || ((count + 1) != _data.numOptimizations))) CopyAndSavePlan(plan, count);

                PrintPlanOptimizationResultVsConstraints(plan, new OptimizationSetupUIHelper().ReadConstraintsFromPlan(plan), e.diffPlanOpt, e.totalCostPlanOpt);

                //really crank up the priority and lower the dose objective on the cooler on the last iteration of the optimization loop
                //this is basically here to avoid having to call op.updateConstraints a second time (if this batch of code was placed outside of the loop)
                if (isFinalOpt) e.updatedObj = IncreaseOptConstraintPrioritiesForFinalOpt(e.updatedObj);

                //print updated optimization constraints
                PrintPlanOptimizationConstraints(plan.Id, e.updatedObj, calcItems, ref percentComplete);

                //update the optimization constraints in the plan
                UpdateConstraints(e.updatedObj, plan);

                //increment the counter, update d.optParams so it is set to the initial optimization constraints at the BEGINNING of the optimization iteration, and save the changes to the plan
                count++;
            }
            return false;
        }

        protected List<Tuple<string, string, double, double, int>> IncreaseOptConstraintPrioritiesForFinalOpt(List<Tuple<string, string, double, double, int>> updatedOpt)
        {
            //go through the current list of optimization objects and add all of them to finalObj vector. ADD COMMENTS!
            List<Tuple<string, string, double, double, int>> finalObj = new List<Tuple<string, string, double, double, int>> { };
            double maxPriority = (double)updatedOpt.Max(x => x.Item5); 
            foreach (Tuple<string, string, double, double, int> itr in updatedOpt)
            {
                //get maximum priority and assign it to the cooler structure to really push the hotspot down. Also lower dose objective
                if (itr.Item1.ToLower().Contains("ts_cooler"))
                {
                    finalObj.Add(new Tuple<string, string, double, double, int>(itr.Item1, itr.Item2, 0.98 * itr.Item3, itr.Item4, Math.Max(itr.Item5, (int)(0.9 * maxPriority))));
                }
                else finalObj.Add(itr);
            }
            return finalObj;
        }

        protected bool PrintPlanOptimizationResultVsConstraints(ExternalPlanSetup plan, List<Tuple<string, string, double, double, int>> optParams, List<Tuple<Structure, DVHData, double, double, double, int>> diffPlanOpt, double totalCostPlanOpt)
        {
            //print the results of the quality check for this optimization
            ProvideUIUpdate(GetOptimizationResultsHeader(plan.Id));
            int index = 0;
            //structure, dvh data, current dose obj, dose diff^2, cost, current priority, priority difference
            foreach (Tuple<Structure, DVHData, double, double, double, int> itr in diffPlanOpt)
            {
                //"structure Id", "constraint type", "dose diff^2 (cGy^2)", "current priority", "cost", "cost (%)"
                ProvideUIUpdate(String.Format(" {0, -15} | {1, -16} | {2, -20:N1} | {3, -16} | {4, -12:N1} | {5, -9:N1} |",
                                                itr.Item1.Id, optParams.ElementAt(index).Item2, itr.Item4, itr.Item6, itr.Item5, 100 * itr.Item5 / totalCostPlanOpt));
                index++;
            }

            PrintAdditionalPlanDoseInfo(_data.requestedPlanDoseInfo, plan); return false;
        }

        protected virtual bool RunSequentialPlansOptimizationLoop(List<ExternalPlanSetup> plans)
        {
            return false;
        }
        #endregion

        #region helper functions during optimization
        protected bool OptimizePlan(bool isDemo, OptimizationOptionsVMAT options, ExternalPlanSetup plan, VMS.TPS.Common.Model.API.Application app)
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
            UpdateOverallProgress((int)(100 * (++overallPercentCompletion) / overallCalcItems));
            //check if user wants to stop
            if (GetAbortStatus())
            {
                KillOptimizationLoop();
                return true;
            }
            return false;
        }

        public bool CalculateDose(bool isDemo, ExternalPlanSetup plan, VMS.TPS.Common.Model.API.Application app)
        {
            UpdateUILabel("Dose calculation:");
            if (isDemo) Thread.Sleep(3000);
            else
            {
                try
                {
                    CalculationResult calcRes = plan.CalculateDose();
                    if (!calcRes.Success)
                    {
                        PrintFailedMessage("Dose calculation");
                        return true;
                    }
                }
                catch (Exception except)
                {
                    PrintFailedMessage("Dose calculation", except.Message);
                    return true;
                }
                app.SaveModifications();
            }
            UpdateOverallProgress((int)(100 * (++overallPercentCompletion) / overallCalcItems));
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
            List<Tuple<string, string, double, double, int>> originalOptObj = new OptimizationSetupUIHelper().ReadConstraintsFromPlan(plan);
            int calcItems = originalOptObj.Count();
            List<Tuple<string, string, double, double, int>> optObj = new List<Tuple<string, string, double, double, int>> { };
            int priority;
            
            UpdateUILabel("Initialize constraints:");
            ProvideUIUpdate(GetOptimizationObjectivesHeader(plan.Id));
            foreach (Tuple<string, string, double, double, int> opt in originalOptObj)
            {
                //leave the PTV priorities at their original values (i.e., 100)
                if (opt.Item1.ToLower().Contains("ptv") || opt.Item1.ToLower().Contains("ts_jnx")) priority = opt.Item5;
                //start OAR structure priorities at 2/3 of the values the user specified so there is some wiggle room for adjustment
                else priority = (int)Math.Ceiling(((double)opt.Item5 * 2) / 3);
                optObj.Add(Tuple.Create(opt.Item1, opt.Item2, opt.Item3, opt.Item4, priority));
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format(" {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", opt.Item1, opt.Item2, opt.Item3, opt.Item4, priority));
            }
            ProvideUIUpdate(" ");
            UpdateConstraints(optObj, plan);

            UpdateOverallProgress((int)(100 * (++overallPercentCompletion) / overallCalcItems));
            return false;
        }

        protected bool UpdateConstraints(List<Tuple<string, string, double, double, int>> obj, ExternalPlanSetup plan)
        {
            int percentComplete = 0;
            int calcItems = plan.OptimizationSetup.Objectives.Count() + obj.Count();
            UpdateUILabel("Remove existing constraints:");
            //remove all existing optimization constraints
            foreach (OptimizationObjective o in plan.OptimizationSetup.Objectives)
            {
                plan.OptimizationSetup.RemoveObjective(o);
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems));
            }

            UpdateUILabel("Assign updated constraints:");
            //assign the new optimization constraints (passed as an argument to this method)
            foreach (Tuple<string, string, double, double, int> opt in obj)
            {
                if (opt.Item2.ToLower() == "upper") plan.OptimizationSetup.AddPointObjective(plan.StructureSet.Structures.First(x => x.Id == opt.Item1), OptimizationObjectiveOperator.Upper, new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), opt.Item4, opt.Item5);
                else if (opt.Item2.ToLower() == "lower") plan.OptimizationSetup.AddPointObjective(plan.StructureSet.Structures.First(x => x.Id == opt.Item1), OptimizationObjectiveOperator.Lower, new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), opt.Item4, opt.Item5);
                else if (opt.Item2.ToLower() == "mean") plan.OptimizationSetup.AddMeanDoseObjective(plan.StructureSet.Structures.First(x => x.Id == opt.Item1), new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), opt.Item5);
                else if (opt.Item2.ToLower() == "exact") ProvideUIUpdate("Warning! Script not setup to handle exact dose constraints! Skipping");
                else ProvideUIUpdate("Constraint type not recognized! Skipping!");
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems));
            }
            UpdateOverallProgress((int)(100 * (++overallPercentCompletion) / overallCalcItems));
            return false;
        }
        #endregion

        #region normalization
        public bool NormalizePlan(ExternalPlanSetup plan, Structure target, double relativeDose, double targetVolCoverage)
        {
            UpdateUILabel("Normalization:");
            //in demo mode, dose might not be calculated for the plan
            if (!plan.IsDoseValid) return true;
            if (target == null || target.IsEmpty) return true;
            //how to normalize a plan in the ESAPI workspace:
            //reference: https://github.com/VarianAPIs/Varian-Code-Samples/blob/master/webinars%20%26%20workshops/Research%20Symposium%202015/Eclipse%20Scripting%20API/Projects/AutomatedPlanningDemo/PlanGeneration.cs
            plan.PlanNormalizationValue = 100.0;
            //absolute dose
            double RxDose = plan.TotalDose.Dose;
            //construct a DoseValue from RxDose
            DoseValue dv = new DoseValue(relativeDose * RxDose / 100, DoseValue.DoseUnit.cGy);
            //get current coverage of the RxDose
            double coverage = plan.GetVolumeAtDose(target, dv, VolumePresentation.Relative);

            //if the current coverage doesn't equal the desired coverage, then renormalize the plan
            if (coverage != targetVolCoverage)
            {
                //get the dose that does cover the targetVolCoverage of the target volume and scale the dose distribution by the ratio of that dose to the relative prescription dose
                dv = plan.GetDoseAtVolume(target, targetVolCoverage, VolumePresentation.Relative, DoseValuePresentation.Absolute);
                double normValue = 100.0 * dv.Dose / (relativeDose * RxDose / 100);
                if (normValue < 0.01 || normValue > 10000.0)
                {
                    ProvideUIUpdate(String.Format("Calculated plan normalization value ({0}%) is outside of acceptable range: 0.01% - 10000.0%! Exiting", normValue), true);
                    return true;
                }
                plan.PlanNormalizationValue = normValue;
            }
            UpdateOverallProgress((int)(100 * (++overallPercentCompletion) / overallCalcItems));
            return false;
        }
        #endregion

        #region plan evaluation
        protected EvalPlanStruct EvaluateAndUpdatePlan(ExternalPlanSetup plan, List<Tuple<string, string, double, double, DoseValuePresentation>> planObj, bool finalOptimization)
        {
            UpdateUILabel(String.Format("Plan evaluation: {0}", plan.Id));
            ProvideUIUpdate(Environment.NewLine + "Constructed evaluation data struct!");
            //create a new data structure to hold the results of the plan quality evaluation
            EvalPlanStruct e = new EvalPlanStruct();
            e.Construct();

            ProvideUIUpdate(String.Format("Parsing optimization objectives from plan: {0}", plan.Id));
            List<Tuple<string, string, double, double, int>> optParams = new OptimizationSetupUIHelper().ReadConstraintsFromPlan(plan);
            //get current optimization objectives from plan (we could use the optParams list, but we want the actual instances of the OptimizationObjective class so we can get the results from each objective)
            (int, int, double, List<Tuple<Structure, DVHData, double, double>>) planObjectiveEvaluation = EvaluateResultVsPlanObjectives(plan, planObj, optParams);

            if (GetAbortStatus())
            {
                KillOptimizationLoop();
                e.wasKilled = true;
                return e;
            }

            e.diffPlanObj = planObjectiveEvaluation.Item4;
            e.totalCostPlanObj = planObjectiveEvaluation.Item3;
            //all constraints met, exiting
            if (planObjectiveEvaluation.Item1 == planObjectiveEvaluation.Item2)
            {
                e.allObjectivesMet = true;
                return e;
            }
            ProvideUIUpdate("All plan objectives NOT met! Adjusting optimization parameters!");

            (double, List<Tuple<Structure, DVHData, double, double, double, int>>) optimizationObjectiveEvaluation = EvaluateResultVsOptimizationConstraints(plan, optParams);
            e.totalCostPlanOpt = optimizationObjectiveEvaluation.Item1;
            e.diffPlanOpt = optimizationObjectiveEvaluation.Item2;

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

            (bool, List<Tuple<string, string, double, double, int>>) result = UpdateHeaterCoolerStructures(plan, finalOptimization);

            //did the user abort the program while updating the heater and cooler structures
            if(result.Item1)
            {
                //user killed operation while generating heater and cooler structures
                KillOptimizationLoop();
                e.wasKilled = true;
                return e;
            }
            e.updatedObj.AddRange(result.Item2);
            
            UpdateOverallProgress((int)(100 * (++overallPercentCompletion) / overallCalcItems));
            return e;
        }

        protected (int, int, double, List<Tuple<Structure, DVHData, double, double>>) EvaluateResultVsPlanObjectives(ExternalPlanSetup plan, List<Tuple<string, string, double, double, DoseValuePresentation>> planObj, List<Tuple<string,string,double,double,int>> optParams)
        {
            ProvideUIUpdate("Evluating optimization result vs plan objectives");
            int percentComplete = 0;
            int calcItems = 1 + planObj.Count();
            //counter to record the number of plan objective met
            int numPass = 0;
            int numComparisons = 0;
            double totalCostPlanObj = 0;
            List<Tuple<Structure, DVHData, double, double>> differenceFromPlanObj = new List<Tuple<Structure, DVHData, double, double>> { };
            

            //loop through all the plan objectives for this case and compare the actual dose to the dose in the plan objective. If we met the constraint, increment numPass. At the end of the loop, if numPass == the number of plan objectives
            //then we have achieved the desired plan quality and can stop the optimization loop
            //string message = "";
            foreach (Tuple<string, string, double, double, DoseValuePresentation> itr in planObj)
            {
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems));
                //used to account for the case where there is a template plan objective that is not included in the current case (e.g., testes are not always spared)
                if (plan.StructureSet.Structures.Where(x => x.Id.ToLower() == itr.Item1.ToLower()).Any() && !plan.StructureSet.Structures.First(x => x.Id.ToLower() == itr.Item1.ToLower()).IsEmpty)
                {
                    //similar to code to the foreach loop used to cycle through the optimization parameters
                    Structure s = plan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToLower() == (itr.Item1.ToLower() + "_lowres"));
                    if (s == null) s = plan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToLower() == itr.Item1.ToLower());
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
                    IEnumerable<Tuple<string, string, double, double, int>> copyOpt = (from p in optParams
                                                                                        where p.Item1.ToLower() == s.Id.ToLower()
                                                                                        where p.Item2.ToLower() == itr.Item2.ToLower()
                                                                                        where p.Item4 == itr.Item4
                                                                                        select p);
                    //IEnumerable<Tuple<string, string, double, double, int>> copyOpt = optParams.Where(x => x.Item1.ToLower() == s.Id.ToLower() && x.Item2.ToLower() == itr.Item2.ToLower());

                    //foreach(Tuple<string, string, double, double, int> t in copyOpt)
                    //{
                    //    ProvideUIUpdate(String.Format("({0},{1},{2},{3},{4})", t.Item1, t.Item2, t.Item3, t.Item4, t.Item5));
                    //}

                    //If the appropriate constraint was found, calculate the cost as the (dose diff)^2 * priority 
                    if (copyOpt.Any())
                    {
                        optPriority = copyOpt.First().Item5;
                        ProvideUIUpdate(String.Format("Corresponding optimization objective found for plan objective: ({0},{1},{2},{3},{4})", itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5.ToString()));
                        //increment the number of comparisons since an optimization constraint was found
                        numComparisons++;
                    }
                    //if no exact constraint was found, leave the priority at zero (per Nataliya's instructions)
                    else ProvideUIUpdate(String.Format("No corresponding optimization objective found for plan objective: ({0},{1},{2},{3},{4})", itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5.ToString()));

                    diff = GetDifferenceFromGoal(plan, itr, s, dvh);
                    if (diff <= 0.0)
                    {
                        //objective was met. Increment the counter for the number of objecives met
                        ProvideUIUpdate(String.Format("Objective met for: ({0},{1},{2},{3},{4})", itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5.ToString()));
                        numPass++;
                    }
                    else
                    {
                        cost = diff * diff * optPriority;
                        ProvideUIUpdate(String.Format("Objective NOT met for: ({0},{1},{2},{3},{4})", itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5.ToString()));
                    }

                    //add this comparison to the list and increment the running total of the cost for the plan objectives
                    differenceFromPlanObj.Add(Tuple.Create(s, dvh, diff * diff, cost));
                    totalCostPlanObj += cost;
                }
            }
            ProvideUIUpdate(100, String.Format("Elapsed time: {0}", GetElapsedTime()));
            return (numComparisons, numPass, totalCostPlanObj, differenceFromPlanObj);
        }

        protected (double, List<Tuple<Structure, DVHData, double, double, double, int>>) EvaluateResultVsOptimizationConstraints(ExternalPlanSetup plan, List<Tuple<string, string, double, double, int>> optParams)
        {
            ProvideUIUpdate("Evaluating optimization result vs optimization constraints:");
            //since we didn't meet all of the plan objectives, we now need to evaluate how well the plan compared to the desired plan objectives
            List<Tuple<Structure, DVHData, double, double, double, int>> differenceFromOptConstraints = new List<Tuple<Structure, DVHData, double, double, double, int>> { };
            //double to hold the total cost of the optimization
            double totalCostPlanOpt = 0;
            int percentComplete = 0;
            int calcItems = 1 + optParams.Count();
            foreach (Tuple<string, string, double, double, int> itr in optParams)
            {
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems));
                //get the structure for each optimization object in optParams and its associated DVH
                Structure s = _data.selectedSS.Structures.First(x => x.Id.ToLower() == itr.Item1.ToLower());
                //dose representation in optimization objectives is always absolute!
                DVHData dvh = plan.GetDVHCumulativeData(s, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);
                double diff = GetDifferenceFromGoal(plan, itr, s, dvh);
                //calculate the cost for this constraint as the dose difference squared times the constraint priority
                double cost = diff * diff * itr.Item5;

                //add the results to the diffPlanOpt list
                //structure, dvh data, current dose obj, dose diff^2, cost, current priority
                differenceFromOptConstraints.Add(Tuple.Create(s, dvh, itr.Item3, diff * diff, cost, itr.Item5));
                //add the cost for this constraint to the running total
                totalCostPlanOpt += cost;
            }
            ProvideUIUpdate(100, String.Format("Elapsed time: {0}", GetElapsedTime()));
            //save the total cost from this optimization
            return (totalCostPlanOpt, differenceFromOptConstraints);
        }

        private double GetDifferenceFromGoal<T>(ExternalPlanSetup plan, Tuple<string, string, double, double, T> goal, Structure s, DVHData dvh)
        {
            //generic type function to accept both optimization constraint and plan objective tuples as arguments
            double diff = 0.0;
            //calculate the dose difference between the actual plan dose and the optimization dose constraint (separate based on constraint type). If the difference is less than 0, truncate the dose difference to 0
            if (goal.Item2.ToLower() == "upper")
            {
                //diff = plan.GetDoseAtVolume(s, opt.Item4, VolumePresentation.Relative, DoseValuePresentation.Absolute).Dose - currentDose;
                diff = plan.GetDoseAtVolume(s, goal.Item4, VolumePresentation.Relative, DoseValuePresentation.Absolute).Dose - goal.Item3;
            }
            else if (goal.Item2.ToLower() == "lower")
            {
                //diff = currentDose - plan.GetDoseAtVolume(s, opt.Item4, VolumePresentation.Relative, DoseValuePresentation.Absolute).Dose;
                diff = goal.Item3 - plan.GetDoseAtVolume(s, goal.Item4, VolumePresentation.Relative, DoseValuePresentation.Absolute).Dose;
            }
            else if (goal.Item2.ToLower() == "mean")
            {
                //diff = dvh.MeanDose.Dose - currentDose;
                diff = dvh.MeanDose.Dose - goal.Item3;
            }
            if (diff <= 0.0) diff = 0.0;
            return diff;
        }

        protected virtual List<Tuple<string, string, double, double, int>> DetermineNewOptimizationObjectives(ExternalPlanSetup plan, List<Tuple<Structure, DVHData, double, double, double, int>> diffPlanOpt, double totalCostOptimizationConstraints, List<Tuple<string, string, double, double, int>> optParams)
        {
            ProvideUIUpdate("Determining new optimization objectives for next iteration");
            //not all plan objectives were met and now we need to do some investigative work to find out what failed and by how much
            //update optimization parameters based on how each of the structures contained in diffPlanOpt performed
            List<Tuple<string, string, double, double, int>> updatedOptimizationConstraints = new List<Tuple<string, string, double, double, int>> { };
            int percentComplete = 0;
            int calcItems = 1 + diffPlanOpt.Count();
            int count = 0;
            foreach (Tuple<Structure, DVHData, double, double, double, int> itr in diffPlanOpt)
            {
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems));
                double relative_cost = 0.0;
                //assign new objective dose and priority to the current dose and priority
                double newDose = itr.Item3;
                int newPriority = itr.Item6;
                //check to see if objective was met (i.e., was the cost > 0.). If objective was met, adjust nothing and copy the current optimization objective for this structure onto the updatedObj vector
                if (itr.Item5 > 0.0)
                {
                    //objective was not met. Determine what to adjust based on OPTIMIZATION OBJECTIVE parameters (not plan objective parameters)
                    relative_cost = itr.Item5 / totalCostOptimizationConstraints;

                    //do NOT adjust ptv dose constraints, only priorities (the ptv structures are going to have the highest relative cost of all the structures due to the difficulty in covering the entire PTV with 100% of the dose and keeing dMax < 5%)
                    //If we starting adjusting the dose for these constraints, they would quickly escalate out of control, therefore, only adjust their priorities by a small amount
                    if (!itr.Item1.Id.ToLower().Contains("ptv") && !itr.Item1.Id.ToLower().Contains("ts_ring") && (relative_cost >= _data.threshold))
                    {
                        //OAR objective is greater than threshold, adjust dose. Evaluate difference between current actual dose and current optimization parameter setting. Adjust new objective dose by dose difference weighted by the relative cost
                        //=> don't push the dose too low, otherwise the constraints won't make sense. Currently, the lowest dose limit is 10% of the Rx dose (set by adjusting lowDoseLimit)
                        //this equation was (more or less) determined empirically
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
        protected virtual (bool, List<Tuple<string, string, double, double, int>>) UpdateHeaterCoolerStructures(ExternalPlanSetup plan, bool isFinalOptimization)
        {
            UpdateUILabel("Update TS heaters & coolers:");
            bool wasKilled = false;
            ProvideUIUpdate("Updating heater and cooler tuning structures for next iteration");
            int percentComplete = 0;
            int calcItems = 2 +_data.requestedTSstructures.Count();
            //update cooler and heater structures for optimization
            //first remove existing structures
            RemoveCoolHeatStructures(plan);

            //list to hold info related to optimization constraints for any added heater and cooler structures
            List<Tuple<string, string, double, double, int>> addedTSstructures = new List<Tuple<string, string, double, double, int>> { };
            //now create new cooler and heating structures
            ProvideUIUpdate(String.Format("Retrieving target structure for plan: {0}", plan.Id));
            List<Tuple<string, string>> plansTargets = new TargetsHelper().GetPlanTargetList(_data.prescriptions);
            if (!plansTargets.Any())
            {
                ProvideUIUpdate(String.Format("Error! Could not retrieve list of plans and associated targets! Exiting"));
                wasKilled = true;
                return (wasKilled, addedTSstructures);
            }
            string targetId = "";
            if (plansTargets.Where(x => x.Item1 == plan.Id).Any()) targetId = plansTargets.FirstOrDefault(x => x.Item1 == plan.Id).Item2;
            Structure target = new TargetsHelper().GetTargetForPlan(_data.selectedSS, targetId, _data.useFlash, _data.planType);
            if (target == null)
            {
                ProvideUIUpdate(String.Format("Error! Target structure not found for plan: {0}! Exiting!", plan.Id));
                wasKilled = true;
                return (wasKilled, addedTSstructures);
            }

            foreach (Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>> itr in _data.requestedTSstructures)
            {
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems));
                Tuple<string, string, double, double, int> TSstructure = null;
                //does it have constraints that need to be met before adding the TS structure?
                if (AllHeaterCoolerTSConstraintsMet(plan, target, itr.Item6, isFinalOptimization))
                {
                    ProvideUIUpdate(String.Format("All conditions met for: {0}! Adding to structure set!", itr.Item1));
                    //cooler
                    if (itr.Item1.Contains("cooler")) TSstructure = GenerateCooler(plan, itr.Item2 / 100, itr.Item3 / 100, itr.Item4, itr.Item1, itr.Item5);
                    //heater
                    else TSstructure = GenerateHeater(plan, target, itr.Item2 / 100, itr.Item3 / 100, itr.Item4, itr.Item1, itr.Item5);
                    if (TSstructure != null) addedTSstructures.Add(TSstructure);
                }
                else ProvideUIUpdate(String.Format("All conditions NOT met for: {0}! Skipping!", itr.Item1));
                if(GetAbortStatus())
                {
                    wasKilled = true;
                    return (wasKilled, addedTSstructures);
                }
            }
            ProvideUIUpdate(100, String.Format("Elapsed time: {0}", GetElapsedTime()));
            return (wasKilled, addedTSstructures);
        }

        private bool AllHeaterCoolerTSConstraintsMet(ExternalPlanSetup plan, Structure target, List<Tuple<string, double, string, double>> constraints, bool isFinalOptimization)
        {
            if (constraints.Any())
            {
                //if any conditions were requested for a particular heater or cooler structure, ensure all of the conditions were met prior to adding the heater/cooler structure
                foreach (Tuple<string, double, string, double> itr in constraints)
                {
                    if (itr.Item1.Contains("Dmax"))
                    {
                        //dmax constraint
                        if (plan.Dose.DoseMax3D.Dose / plan.TotalDose.Dose <= itr.Item4 / 100) return false;
                    }
                    else if (itr.Item1.Contains("V"))
                    {
                        //volume constraint
                        if (itr.Item3 == ">")
                        {
                            if (plan.GetVolumeAtDose(target, new DoseValue(itr.Item2, DoseValue.DoseUnit.Percent), VolumePresentation.Relative) <= itr.Item4) return false;
                        }
                        else
                        {
                            if (plan.GetVolumeAtDose(target, new DoseValue(itr.Item2, DoseValue.DoseUnit.Percent), VolumePresentation.Relative) >= itr.Item4) return false;
                        }
                    }
                    else if (!isFinalOptimization) return false;
                }
            }
            return true;
        }

        protected void RemoveCoolHeatStructures(ExternalPlanSetup plan)
        {
            ProvideUIUpdate("Removing existing heater and cooler structures");
            StructureSet ss = _data.selectedSS;
            List<Structure> coolerHeater = ss.Structures.Where(x => x.Id.ToLower().Contains("ts_cooler") || x.Id.ToLower().Contains("ts_heater")).ToList();
            int percentComplete = 0;
            int calcItems = coolerHeater.Count();
            foreach (Structure itr in coolerHeater)
            {
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format("Removing structure: {0}", itr.Id));
                if (ss.CanRemoveStructure(itr)) ss.RemoveStructure(itr);
                else ProvideUIUpdate(String.Format("Warning! Cannot remove {0} from the structure set! Skipping!", itr.Id));
            }
        }

        protected Tuple<string, string, double, double, int> GenerateCooler(ExternalPlanSetup plan, double doseLevel, double requestedDoseConstraint, double volume, string name, int priority)
        {
            ProvideUIUpdate(String.Format("Generating cooler structure: {0} now", name));
            //create an empty optiization objective
            Tuple<string, string, double, double, int> cooler = null;
            StructureSet ss = plan.StructureSet;
            //grab the relevant dose, dose leve, priority, etc. parameters
            PlanningItemDose d = plan.Dose;
            DoseValue dv = new DoseValue(doseLevel * plan.TotalDose.Dose, DoseValue.DoseUnit.cGy);
            if (ss.CanAddStructure("CONTROL", name))
            {
                //add the cooler structure to the structure list and convert the doseLevel isodose volume to a structure. Add this new structure to the list with a max dose objective of Rx * 105% and give it a priority of 80
                Structure coolerStructure = ss.AddStructure("CONTROL", name);
                coolerStructure.ConvertDoseLevelToStructure(d, dv);
                if(coolerStructure.IsEmpty)
                {
                    ProvideUIUpdate(String.Format("Warning! Cooler structure: {0} is empty! Removing and skipping!", name));
                    if (ss.CanRemoveStructure(coolerStructure)) ss.RemoveStructure(coolerStructure);
                }
                else cooler = Tuple.Create(name, "Upper", requestedDoseConstraint * plan.TotalDose.Dose, volume, priority);
            }
            return cooler;
        }

        protected Tuple<string, string, double, double, int> GenerateHeater(ExternalPlanSetup plan, Structure target, double doseLevelLow, double doseLevelHigh, double volume, string name, int priority)
        {
            ProvideUIUpdate(String.Format("Generating heater structure: {0} now", name));
            //similar to the generateCooler method
            Tuple<string, string, double, double, int> heater = null;
            StructureSet ss = plan.StructureSet;
            PlanningItemDose d = plan.Dose;
            DoseValue dv = new DoseValue(doseLevelLow * plan.TotalDose.Dose, DoseValue.DoseUnit.cGy);
            if (ss.CanAddStructure("CONTROL", name))
            {
                //segment lower isodose volume
                Structure heaterStructure = ss.AddStructure("CONTROL", name);
                heaterStructure.ConvertDoseLevelToStructure(d, dv);
                //segment higher isodose volume
                Structure dummy = ss.AddStructure("CONTROL", "dummy");
                dummy.ConvertDoseLevelToStructure(d, new DoseValue(doseLevelHigh * plan.TotalDose.Dose, DoseValue.DoseUnit.cGy));
                //subtract the higher isodose volume from the heater structure and assign it to the heater structure. 
                //This is the heater structure that will be used for optimization. Create a new optimization objective for this tunning structure
                heaterStructure.SegmentVolume = heaterStructure.Sub(dummy.SegmentVolume.Margin(0.0));
                //clean up
                ss.RemoveStructure(dummy);
                //only keep the overlapping regions of the heater structure with the taget structure
                heaterStructure.SegmentVolume = heaterStructure.And(target.Margin(0.0));
                if (heaterStructure.IsEmpty)
                {
                    ProvideUIUpdate(String.Format("Warning! Heater structure: {0} is empty! Removing and skipping!", name));
                    if (ss.CanRemoveStructure(heaterStructure)) ss.RemoveStructure(heaterStructure);
                }
                else
                {
                    //heaters generally need to increase the dose to regions of the target NOT receiving the Rx dose --> always set the dose objective to the Rx dose
                    heater = Tuple.Create(name, "Lower", plan.TotalDose.Dose, volume, priority);
                }
            }
            return heater;
        }
        #endregion
    }
}
