using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Threading;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIOptLoopMT.PlanEvaluation;
using VMATTBICSIOptLoopMT.baseClasses;
using VMATTBICSIOptLoopMT.helpers;
using VMATTBICSIAutoplanningHelpers.Prompts;

namespace VMATTBICSIOptLoopMT.baseClasses
{
    public class optimizationLoopBase : MTbase
    {
        private dataContainer _data;

        public optimizationLoopBase(dataContainer _d)
        {
            _data = _d;
        }

        public override bool Run()
        {
            try
            {
                SetAbortUIStatus("Runnning");
                PrintRunSetupInfo();
                if (preliminaryChecks(_data.plan)) return true;
                
                if (_data.isDemo || !_data.runCoverageCheck) ProvideUIUpdate(" Skipping coverage check! Moving on to optimization loop!");
                else
                {
                    if (RunCoverageCheck(_data.optParams, _data.plan, _data.relativeDose, _data.targetVolCoverage, _data.useFlash)) return true;
                    ProvideUIUpdate(" Coverage check completed! Commencing optimization loop!");
                }
                if (_data.numOptimizations < 1)
                {
                    UpdateConstraints(_data.optParams, _data.plan);
                    ProvideUIUpdate(100);
                }
                else
                {
                    if(RunOptimizationLoop()) return true;
                }
                OptimizationLoopFinished();
            }
            catch (Exception e) { ProvideUIUpdate(String.Format("{0}", e.Message), true); return true; }
            return false;
        }

        private void PrintFailedMessage(string optorcalc, string reason = "")
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

        private void PrintAdditionalPlanDoseInfo(List<Tuple<string,string,double,string>> requestedInfo, ExternalPlanSetup plan)
        {
            Structure structure;
            List<Structure> planStructures = plan.StructureSet.Structures.ToList();
            
            string message = " Additional plan infomation: " + Environment.NewLine;
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

        #region print run setup and format headers
        private void PrintRunSetupInfo()
        {
            string optimizationLoopSetupInfo = String.Format(" ---------------------------------------------------------------------------------------------------------" + Environment.NewLine +
                                        " Date: {0}" + Environment.NewLine +
                                        " Optimization parameters:" + Environment.NewLine +
                                        " Run coverage check: {1}" + Environment.NewLine +
                                        " Max number of optimizations: {2}" + Environment.NewLine +
                                        " Run additional optimization to lower hotspots: {3}" + Environment.NewLine +
                                        " Copy and save each optimized plan: {4}" + Environment.NewLine +
                                        " Plan normalization: PTV V{5}cGy = {6:0.0}%" + Environment.NewLine,
                                        DateTime.Now, _data.runCoverageCheck, _data.numOptimizations, _data.oneMoreOpt, _data.copyAndSavePlanItr, _data.plan.TotalDose.Dose, _data.targetVolCoverage);

            ProvideUIUpdate(optimizationLoopSetupInfo);
            PrintPlanObjectives();
            PrintRequestedTSStructures();
        }

        private void PrintPlanObjectives()
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

        private void PrintRequestedTSStructures()
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

        private string GetOptimizationObjectivesHeader()
        {
            string optObjHeader = " Updated optimization constraints:" + Environment.NewLine;
            optObjHeader += " -------------------------------------------------------------------------" + Environment.NewLine;
            optObjHeader += String.Format(" {0, -15} | {1, -16} | {2, -10} | {3, -10} | {4, -8} |" + Environment.NewLine, "structure Id", "constraint type", "dose (cGy)", "volume (%)", "priority");
            optObjHeader += " -------------------------------------------------------------------------";
            return optObjHeader;
        }

        private string GetOptimizationResultsHeader()
        {
            string optResHeader = " Results of optimization:" + Environment.NewLine;
            optResHeader += " ---------------------------------------------------------------------------------------------------------" + Environment.NewLine;
            optResHeader += String.Format(" {0, -15} | {1, -16} | {2, -20} | {3, -16} | {4, -12} | {5, -9} |" + Environment.NewLine, "structure Id", "constraint type", "dose diff^2 (cGy^2)", "current priority", "cost", "cost (%)");
            optResHeader += " ---------------------------------------------------------------------------------------------------------";
            return optResHeader;
        }
        #endregion

        #region preliminary checks
        public bool preliminaryChecks(ExternalPlanSetup plan)
        {
            //check if the user assigned the imaging device Id. If not, the optimization will crash with no error
            if (plan.StructureSet.Image.Series.ImagingDeviceId == "")
            {
                MessageBox.Show("Warning! Did you forget to set the imaging device to 'Def_CTScanner'?");
                return true;
            }

            //is the user origin inside the body?
            if (!plan.StructureSet.Image.HasUserOrigin || !(plan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToLower() == "body").IsPointInsideSegment(plan.StructureSet.Image.UserOrigin)))
            {
                MessageBox.Show("Did you forget to set the user origin? \nUser origin is NOT inside body contour! \nPlease fix and try again!");
                return true;
            }

            //are there beams in the plan?
            if (plan.Beams.Count() == 0)
            {
                MessageBox.Show("No beams present in the VMAT TBI plan!");
                return true;
            }

            //check each beam to ensure its z-position is rounded-off to the nearest integer. Ensure the x,y positions are both 0 within +/- 1e-3 cm (need small tolerance due to possible rounding errors in eclipse)
            foreach (Beam b in plan.Beams)
            {
                VVector pos = b.IsocenterPosition;
                pos = plan.StructureSet.Image.DicomToUser(pos, plan);
                //the first argument checks if the rounding error is JUST OVER the nearest integer and the second argument checks if the rounding error is JUST UNDER the nearest integer
                if (pos.x % 1 > 1e-3 && Math.Abs((pos.x % 1) - 1) > 1e-3)
                {
                    MessageBox.Show(String.Format("x, y, z, z % 1, abs((z % 1) - 1), beam id \n{0}, {1}, {2}, {3}, {4}, {5}", pos.x, pos.y, pos.z, pos.z % 1, Math.Abs((pos.z % 1) - 1), b.Id));
                    MessageBox.Show("X position of isocenters are NOT rounded off! Exiting!");
                    return true;
                }
                if (pos.y % 1 > 1e-3 && Math.Abs((pos.y % 1) - 1) > 1e-3)
                {
                    MessageBox.Show(String.Format("x, y, z, z % 1, abs((z % 1) - 1), beam id \n{0}, {1}, {2}, {3}, {4}, {5}", pos.x, pos.y, pos.z, pos.z % 1, Math.Abs((pos.z % 1) - 1), b.Id));
                    MessageBox.Show("Y position of isocenters are NOT rounded off! Exiting!");
                    return true;
                }
                //if pos.z % 1 is not zero, check to see if rounded difference between pos.z % 1 and 1 is > 1 nm (basically check for an internal rounding error in the program). If so, throw an error.
                if (pos.z % 1 > 1e-3 && Math.Abs((pos.z % 1) - 1) > 1e-3)
                {
                    MessageBox.Show(String.Format("x, y, z, z % 1, abs((z % 1) - 1), beam id \n{0}, {1}, {2}, {3}, {4}, {5}", pos.x, pos.y, pos.z, pos.z % 1, Math.Abs((pos.z % 1)-1), b.Id));
                    MessageBox.Show("Z positions of isocenters are NOT rounded-off! Exiting!");
                    return true;
                }
            }

            //ensure the ptv structures are NOT null
            //if (plan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToLower() == "ptv_body") == null || plan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToLower() == "ts_ptv_vmat") == null)
            //{
            //    MessageBox.Show("Error! Target structure(s) not found!");
            //    return true;
            //}
            if (plan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToLower() == "ts_ptv_csi") == null)
            {
                MessageBox.Show("Error! Target structure(s) not found!");
                return true;
            }

            //grab all couch structures including couch surface, rails, etc. Also grab the matchline and spinning manny couch (might not be present depending on the size of the patient)
            IEnumerable<Structure> couch = plan.StructureSet.Structures.Where(x => x.Id.ToLower().Contains("couch"));
            IEnumerable<Structure> rails = plan.StructureSet.Structures.Where(x => x.Id.ToLower().Contains("rail"));
            Structure matchline = plan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToLower() == "matchline");
            Structure spinningManny = plan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToLower() == "spinmannysurface");
            if(spinningManny == null) spinningManny = plan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToLower() == "couchmannysurfac");

            //check to see if the couch and rail structures are present in the structure set. If not, let the user know as an FYI. At this point, the user can choose to stop the optimization loop and add the couch structures
            if (!couch.Any() || !rails.Any())
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

            //check if there is a matchline contour. If so, is it empty?
            if (matchline != null && !matchline.IsEmpty)
            {
                //if a matchline contour is present and filled, does the spinning manny couch exist in the structure set? If not, let the user know so they can decide if they want to continue of stop the optimization loop
                if (spinningManny == null || spinningManny.IsEmpty)
                {
                    confirmUI CUI = new confirmUI();
                    CUI.message.Text = String.Format("I found a matchline, but no spinning manny couch or it's empty!") + Environment.NewLine + Environment.NewLine + "Continue?!";
                    CUI.ShowDialog();
                    if (!CUI.confirm) return true;
                }
            }

            //now check if the couch and spinning manny structures are present on the first and last slices of the CT image
            bool checkSupportStruct = false;
            if ((couch.Any() && couch.Where(x => !x.IsEmpty).Any()) && (couch.Where(x => x.GetContoursOnImagePlane(0).Any()).Any() || couch.Where(x => x.GetContoursOnImagePlane(plan.StructureSet.Image.ZSize - 1).Any()).Any())) checkSupportStruct = true;
            if (!checkSupportStruct && (spinningManny != null && !spinningManny.IsEmpty) && (spinningManny.GetContoursOnImagePlane(0).Any() || spinningManny.GetContoursOnImagePlane(plan.StructureSet.Image.ZSize - 1).Any())) checkSupportStruct = true;
            if (checkSupportStruct)
            {
                //couch structures found on first and last slices of CT image. Ask the user if they want to remove the contours for these structures on these image slices
                //We've found that eclipse will throw warning messages after each dose calculation if the couch structures are on the last slices of the CT image. The reason is because a beam could exit the support
                //structure (i.e., the couch) through the end of the couch thus exiting the CT image altogether. Eclipse warns that you are transporting radiation through a structure at the end of the CT image, which
                //defines the world volume (i.e., outside this volume, the radiation transport is killed)
                confirmUI CUI = new confirmUI();
                CUI.message.Text = String.Format("I found couch contours on the first or last slices of the CT image!") + Environment.NewLine + Environment.NewLine + 
                                                 "Do you want to remove them?!" + Environment.NewLine + "(The script will be less likely to throw warnings)";
                CUI.ShowDialog();
                //remove all applicable contours on the first and last CT slices
                if (CUI.confirm)
                {
                    bool recalcDose = false;

                    //If dose has been calculated for this plan, need to clear the dose in this and any and all plans that reference this structure set
                    StructureSet ss = plan.StructureSet;
                    //check to see if this structure set is used in any other calculated plans
                    string message = "The following plans have dose calculated and use the same structure set:" + System.Environment.NewLine;
                    List<ExternalPlanSetup> otherPlans = new List<ExternalPlanSetup> { };
                    foreach (Course c in plan.Course.Patient.Courses)
                    {
                        foreach (ExternalPlanSetup p in c.ExternalPlanSetups)
                        {
                            if (p.IsDoseValid && p.StructureSet == ss)
                            {
                                message += String.Format("Course: {0}, Plan: {1}", c.Id, p.Id) + System.Environment.NewLine;
                                otherPlans.Add(p);
                            }
                        }
                    }

                    if (plan.IsDoseValid || otherPlans.Count > 0)
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
                            //reset the calculation models for all plans that have calculated dose and share this structure set (inherently wipes the dose)
                            if (otherPlans.Where(x => x != plan).Any()) recalcDose = true;
                            foreach (ExternalPlanSetup p in otherPlans)
                            {
                                string calcModel = p.GetCalculationModel(CalculationType.PhotonVolumeDose);
                                p.ClearCalculationModel(CalculationType.PhotonVolumeDose);
                                p.SetCalculationModel(CalculationType.PhotonVolumeDose, calcModel);
                            }
                        }
                    }
                    foreach (Structure s in couch)
                    {
                        //check to ensure the structure is actually contoured (otherwise you will likely get an error if the structure is null)
                        if (!s.IsEmpty)
                        {
                            s.ClearAllContoursOnImagePlane(0);
                            s.ClearAllContoursOnImagePlane(plan.StructureSet.Image.ZSize - 1);
                        }
                    }
                    foreach (Structure s in rails)
                    {
                        if (!s.IsEmpty)
                        {
                            s.ClearAllContoursOnImagePlane(0);
                            s.ClearAllContoursOnImagePlane(plan.StructureSet.Image.ZSize - 1);
                        }
                    }
                    if (spinningManny != null && !spinningManny.IsEmpty)
                    {
                        spinningManny.ClearAllContoursOnImagePlane(0);
                        spinningManny.ClearAllContoursOnImagePlane(plan.StructureSet.Image.ZSize - 1);
                    }
                    if(recalcDose)
                    {
                        //recalculate dose for each plan that requires it for the current course only!
                        foreach (ExternalPlanSetup p in otherPlans) if (p != plan && p.Course == plan.Course)
                        {
                            p.CalculateDose();    
                        }
                    }
                }
            }

            //turn on jaw tracking
            try { plan.OptimizationSetup.UseJawTracking = true; }
            catch (Exception e) { MessageBox.Show($"{e.Message}\nCannot set jaw tracking for this machine! Jaw tracking will not be enabled!"); }
            //set auto NTO priority to zero (i.e., shut it off)
            plan.OptimizationSetup.AddAutomaticNormalTissueObjective(0.0);
            //be sure to set the dose value presentation to absolute! This is important for plan evaluation in the evaluateAndUpdatePlan method below
            plan.DoseValuePresentation = DoseValuePresentation.Absolute;


            if (GetAbortStatus())
            {
                KillOptimizationLoop();
                return true;
            }
            return false;
        }
        #endregion

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
            if(CalculateDose(_data.isDemo, plan, _data.app)) return true;
            
            ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), " Dose calculated for coverage check, normalizing plan!");

            //normalize plan
            normalizePlan(plan, relativeDose, targetVolCoverage, useFlash);
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
        private bool RunOptimizationLoop()
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
                string optResults = Environment.NewLine + GetOptimizationResultsHeader();
                int index = 0;
                //structure, dvh data, current dose obj, dose diff^2, cost, current priority, priority difference
                foreach (Tuple<Structure, DVHData, double, double, double, int> itr in e.diffPlanOpt)
                {
                    string id = "";
                    //grab the structure id from the optParams list (better to work with string literals rather than trying to access the structure id through the structure object instance in the diffPlanOpt data structure)
                    id = _data.optParams.ElementAt(index).Item1;
                    //"structure Id", "constraint type", "dose diff^2 (cGy^2)", "current priority", "cost", "cost (%)"
                    optResults += String.Format(" {0, -15} | {1, -16} | {2, -20:N1} | {3, -16} | {4, -12:N1} | {5, -9:N1} |" + Environment.NewLine, id, _data.optParams.ElementAt(index).Item2, itr.Item4, itr.Item6, itr.Item5, 100 * itr.Item5 / e.totalCostPlanOpt);
                    index++;
                }
                ProvideUIUpdate(optResults);

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
                string newObj = Environment.NewLine + GetOptimizationObjectivesHeader();
                foreach (Tuple<string, string, double, double, int> itr in e.updatedObj)
                    newObj += String.Format(" {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |" + Environment.NewLine, itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5);
                ProvideUIUpdate((int)(100 * (++percentCompletion) / calcItems), newObj);

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
                    normalizePlan(_data.plan, _data.relativeDose, _data.targetVolCoverage, false);
                }
            }
            ProvideUIUpdate(100, Environment.NewLine + " Finished!");
            if (!_data.isDemo && (_data.oneMoreOpt || _data.useFlash)) _data.app.SaveModifications();
            return false;
        }
        #endregion

        #region helper functions
        private bool OptimizePlan(bool isDemo, OptimizationOptionsVMAT options, ExternalPlanSetup plan, VMS.TPS.Common.Model.API.Application app)
        {
            if(isDemo) Thread.Sleep(3000);
            else
            {
                //optimize with intermediate dose (AAA algorithm).
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
            //check if user wants to stop
            if (GetAbortStatus())
            {
                KillOptimizationLoop();
                return true;
            }
            return false;
        }

        private bool CalculateDose(bool isDemo, ExternalPlanSetup plan, VMS.TPS.Common.Model.API.Application app)
        {
            if (isDemo) Thread.Sleep(3000);
            else
            {
                //calculate dose
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
            //check if user wants to stop
            if (GetAbortStatus())
            {
                KillOptimizationLoop();
                return true;
            }
            return false;
        }

        private bool CopyAndSavePlan(ExternalPlanSetup plan, int count)
        {
            Course c = plan.Course;
            //this copies the plan and the dose!
            ExternalPlanSetup newPlan = (ExternalPlanSetup)c.CopyPlanSetup(plan);
            newPlan.Id = String.Format("opt itr {0}", count + 1);
            return false;
        }

        private List<Tuple<string, string, double, double, int>> InitializeOptimizationConstriants(List<Tuple<string, string, double, double, int>> originalOptObj)
        {
            //coverage check passed, now set some initial optimization parameters for each structure in the initial list
            List<Tuple<string, string, double, double, int>> optObj = new List<Tuple<string, string, double, double, int>> { };
            int priority = 0;
            ProvideUIUpdate(Environment.NewLine + GetOptimizationObjectivesHeader());
            foreach (Tuple<string, string, double, double, int> opt in originalOptObj)
            {
                //leave the PTV priorities at their original values (i.e., 100)
                if (opt.Item1.ToLower().Contains("ptv") || opt.Item1.ToLower().Contains("ts_jnx")) priority = opt.Item5;
                //start OAR structure priorities at 2/3 of the values the user specified so there is some wiggle room for adjustment
                else priority = (int)Math.Ceiling(((double)opt.Item5 * 2) / 3);
                optObj.Add(Tuple.Create(opt.Item1, opt.Item2, opt.Item3, opt.Item4, priority));
                ProvideUIUpdate(String.Format(" {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |", opt.Item1, opt.Item2, opt.Item3, opt.Item4, priority));
            }
            return optObj;
        }
        #endregion

        //**********************************************************************************************************************************************************************************************************************************************************************************************************************************************************************
        // ADJUST THIS CODE IF YOU WANT TO CHANGE HOW THE PROGRAM ADJUSTS THE OPTIMIZATION CONSTRAINTS AFTER EACH ITERATION
        //**********************************************************************************************************************************************************************************************************************************************************************************************************************************************************************
        public evalPlanStruct evaluateAndUpdatePlan(ExternalPlanSetup plan, List<Tuple<string, string, double, double, int>> optParams, List<Tuple<string, string, double, double, DoseValuePresentation>> planObj, 
                                                List<Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>>> requestedTSstructures, double threshold, double lowDoseLimit, bool finalOptimization)
        {
            //create a new data structure to hold the results of the plan quality evaluation
            evalPlanStruct e = new evalPlanStruct();
            e.construct();

            //get current optimization objectives from plan (we could use the optParams list, but we want the actual instances of the OptimizationObjective class so we can get the results from each objective)
            IEnumerable<OptimizationObjective> currentObj = plan.OptimizationSetup.Objectives;

            //counter to record the number of plan objective met
            int numPass = 0;
            int numComparisons = 0;
            double totalCostPlanObj = 0;
            //loop through all the plan objectives for this case and compare the actual dose to the dose in the plan objective. If we met the constraint, increment numPass. At the end of the loop, if numPass == the number of plan objectives
            //then we have achieved the desired plan quality and can stop the optimization loop
            //string message = "";
            foreach (Tuple<string, string, double, double, DoseValuePresentation> itr in planObj)
            {
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
                    if (optParams.FirstOrDefault(x => x.Item1.ToLower() == (itr.Item1.ToLower() + "_lowres")) != null || optParams.FirstOrDefault(x => x.Item1.ToLower() == itr.Item1.ToLower()) != null)
                    {
                        //If so, do a three-way comparison to find the correct optimization objective for this plan objective (compare based structureId, constraint type, and constraint volume). These three objectives will remain constant
                        //throughout the optimization process whereas the dose constraint will vary
                        IEnumerable<Tuple<string, string, double, double, int>> copyOpt = from p in optParams
                                                                                          where p.Item1.ToLower() == (itr.Item1.ToLower() + "_lowres")
                                                                                          where p.Item2.ToLower() == (itr.Item2.ToLower() + "_lowres")
                                                                                          where p.Item4 == itr.Item4
                                                                                          select p;

                        if (copyOpt.ElementAtOrDefault(0) == null) copyOpt = from p in optParams
                                                                             where p.Item1.ToLower() == itr.Item1.ToLower()
                                                                             where p.Item2.ToLower() == itr.Item2.ToLower()
                                                                             where p.Item4 == itr.Item4
                                                                             select p;

                        //If the appropriate constraint was found, calculate the cost as the (dose diff)^2 * priority. Also 
                        if (copyOpt.ElementAtOrDefault(0) != null) optPriority = copyOpt.ElementAtOrDefault(0).Item5;
                        //if no exact constraint was found, leave the priority at zero (per Nataliya's instructions)
                        //increment the number of comparisons since an optimization constraint was found
                        numComparisons++;
                    }
                    //else MessageBox.Show(itr.Item1);

                    //similar code as above
                    if (itr.Item2.ToLower() == "upper")
                    {
                        diff = plan.GetDoseAtVolume(s, itr.Item4, VolumePresentation.Relative, itr.Item5).Dose - itr.Item3;
                        //if (plan.GetDoseAtVolume(struRes.Item1, itr.Item4, VolumePresentation.Relative, itr.Item5).Dose <= itr.Item3) numPass++;
                    }
                    else if (itr.Item2.ToLower() == "lower")
                    {
                        diff = itr.Item3 - plan.GetDoseAtVolume(s, itr.Item4, VolumePresentation.Relative, itr.Item5).Dose;
                        //if (plan.GetDoseAtVolume(struRes.Item1, itr.Item4, VolumePresentation.Relative, itr.Item5).Dose >= itr.Item3) numPass++;
                    }
                    else if (itr.Item2.ToLower() == "mean")
                    {
                        diff = dvh.MeanDose.Dose - itr.Item3;
                        //if (struRes.Item2.MeanDose.Dose <= itr.Item3) numPass++;
                    }

                    if (diff <= 0.0)
                    {
                        //objective was met. Increment the counter for the number of objecives met
                        numPass++;
                        diff = 0.0;
                    }
                    else cost = diff * diff * optPriority;

                    //add this comparison to the list and increment the running total of the cost for the plan objectives
                    //message += String.Format("{0}, {1}, {2}, {3}, {4}", s.Id, itr.Item2, itr.Item3, diff, cost) + System.Environment.NewLine;
                    e.diffPlanObj.Add(Tuple.Create(s, dvh, diff * diff, cost));
                    totalCostPlanObj += cost;
                }
                //else message += String.Format("No structure found for: {0}", itr.Item1) + System.Environment.NewLine;
            }
            //message += String.Format("{0}, {1}", numPass, numComparisons);

            //MessageBox.Show(message);
            e.totalCostPlanObj = totalCostPlanObj;
            if (numPass == numComparisons) return e; //all constraints met, exiting

            //since we didn't meet all of the plan objectives, we now need to evaluate how well the plan compared to the desired plan objectives
            //double to hold the total cost of the optimization
            double totalCostPlanOpt = 0;
            foreach (Tuple<string, string, double, double, int> opt in optParams)
            {
                //get the structure for each optimization object in optParams and its associated DVH
                Structure s = plan.StructureSet.Structures.First(x => x.Id.ToLower() == opt.Item1.ToLower());
                //dose representation in optimization objectives is always absolute!
                DVHData dvh = plan.GetDVHCumulativeData(s, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);
                double diff = 0.0;

                //calculate the dose difference between the actual plan dose and the optimization dose constraint (separate based on constraint type). If the difference is less than 0, truncate the dose difference to 0
                if (opt.Item2.ToLower() == "upper")
                {
                    //diff = plan.GetDoseAtVolume(s, opt.Item4, VolumePresentation.Relative, DoseValuePresentation.Absolute).Dose - currentDose;
                    diff = plan.GetDoseAtVolume(s, opt.Item4, VolumePresentation.Relative, DoseValuePresentation.Absolute).Dose - opt.Item3;
                }
                else if (opt.Item2.ToLower() == "lower")
                {
                    //diff = currentDose - plan.GetDoseAtVolume(s, opt.Item4, VolumePresentation.Relative, DoseValuePresentation.Absolute).Dose;
                    diff = opt.Item3 - plan.GetDoseAtVolume(s, opt.Item4, VolumePresentation.Relative, DoseValuePresentation.Absolute).Dose;
                }
                else if (opt.Item2.ToLower() == "mean")
                {
                    //diff = dvh.MeanDose.Dose - currentDose;
                    diff = dvh.MeanDose.Dose - opt.Item3;
                }
                if (diff <= 0.0) diff = 0.0;

                //calculate the cost for this constraint as the dose difference squared times the constraint priority
                double cost = diff * diff * opt.Item5;

                //add the results to the diffPlanOpt list
                //structure, dvh data, current dose obj, dose diff^2, cost, current priority
                e.diffPlanOpt.Add(Tuple.Create(s, dvh, opt.Item3, diff * diff, cost, opt.Item5));
                //add the cost for this constraint to the running total
                totalCostPlanOpt += cost;
            }
            //save the total cost from this optimization
            e.totalCostPlanOpt = totalCostPlanOpt;

            //not all plan objectives were met and now we need to do some investigative work to find out what failed and by how much
            //update optimization parameters based on how each of the structures contained in diffPlanOpt performed
            //string output = "";
            int count = 0;
            foreach (Tuple<Structure, DVHData, double, double, double, int> itr in e.diffPlanOpt)
            {
                //placeholders
                double relative_cost = 0.0;
                //assign new objective dose and priority to the current dose and priority
                double newDose = itr.Item3;
                int newPriority = itr.Item6;
                //check to see if objective was met (i.e., was the cost > 0.). If objective was met, adjust nothing and copy the current optimization objective for this structure onto the updatedObj vector
                if (itr.Item5 > 0.0)
                {
                    //objective was not met. Determine what to adjust based on OPTIMIZATION OBJECTIVE parameters (not plan objective parameters)
                    relative_cost = itr.Item5 / totalCostPlanOpt;

                    //do NOT adjust ptv dose constraints, only priorities (the ptv structures are going to have the highest relative cost of all the structures due to the difficulty in covering the entire PTV with 100% of the dose and keeing dMax < 5%)
                    //If we starting adjusting the dose for these constraints, they would quickly escalate out of control, therefore, only adjust their priorities by a small amount
                    if (!itr.Item1.Id.ToLower().Contains("ptv") && !itr.Item1.Id.ToLower().Contains("ts_ring") && (relative_cost >= threshold))
                    {
                        //OAR objective is greater than threshold, adjust dose. Evaluate difference between current actual dose and current optimization parameter setting. Adjust new objective dose by dose difference weighted by the relative cost
                        //=> don't push the dose too low, otherwise the constraints won't make sense. Currently, the lowest dose limit is 10% of the Rx dose (set by adjusting lowDoseLimit)
                        //this equation was (more or less) determined empirically
                        if ((newDose - (Math.Sqrt(itr.Item4) * relative_cost * 2)) >= plan.TotalDose.Dose * lowDoseLimit) newDose -= (Math.Sqrt(itr.Item4) * relative_cost * 2);
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
                if(!optParams.ElementAt(count).Item1.ToLower().Contains("ts_heater") && !optParams.ElementAt(count).Item1.ToLower().Contains("ts_cooler"))
                    e.updatedObj.Add(Tuple.Create(optParams.ElementAt(count).Item1, optParams.ElementAt(count).Item2, newDose, optParams.ElementAt(count).Item4, newPriority));
                // output += String.Format("{0}, {1}, {2}, {3}, {4}, {5}\n", optParams.ElementAt(count).Item1, optParams.ElementAt(count).Item2, newDose, optParams.ElementAt(count).Item4, newPriority, relative_cost);
                count++;
            }
            //MessageBox.Show(output);

            //update cooler and heater structures for optimization
            //first remove existing structures
            removeCoolHeatStructures(plan);
            
            //now create new cooler and heating structures
            Structure target = null;
            if (plan.OptimizationSetup.Objectives.Where(x => x.StructureId.ToLower() == "ts_ptv_flash").Any()) target = plan.StructureSet.Structures.First(x => x.Id.ToLower() == "ts_ptv_flash");
            //else target = plan.StructureSet.Structures.First(x => x.Id.ToLower() == "ts_ptv_vmat");
            else target = plan.StructureSet.Structures.First(x => x.Id.ToLower() == "ts_ptv_csi");
            foreach (Tuple<string, double, double, double, int, List<Tuple<string, double, string, double>>> itr in requestedTSstructures)
            {
                bool addTSstruct = true;
                Tuple<string, string, double, double, int> TSstructure = null;
                //does it have constraints that need to be met before adding the TS structure?
                if (itr.Item6.Any())
                {
                    foreach (Tuple<string, double, string, double> itr1 in itr.Item6)
                    {
                        if (itr1.Item1.Contains("Dmax"))
                        {
                            //dmax constraint
                            if (plan.Dose.DoseMax3D.Dose / plan.TotalDose.Dose <= itr1.Item4 / 100) { addTSstruct = false; break; }
                        }
                        else if (itr1.Item1.Contains("V"))
                        {
                            //volume constraint
                            if (itr1.Item3 == ">") { if (plan.GetVolumeAtDose(target, new DoseValue(itr1.Item2, DoseValue.DoseUnit.Percent), VolumePresentation.Relative) <= itr1.Item4) { addTSstruct = false; break; } }
                            else { if (plan.GetVolumeAtDose(target, new DoseValue(itr1.Item2, DoseValue.DoseUnit.Percent), VolumePresentation.Relative) >= itr1.Item4) { addTSstruct = false; break; } }
                        }
                        else if(!finalOptimization) { addTSstruct = false; break; }
                    }
                }
                if (addTSstruct)
                {
                    //cooler
                    if (itr.Item1.Contains("cooler")) TSstructure = generateCooler(plan, itr.Item2 / 100, itr.Item3 / 100, itr.Item4, itr.Item1, itr.Item5);
                    //heater
                    else TSstructure = generateHeater(plan, target, itr.Item2 / 100, itr.Item3 / 100, itr.Item4, itr.Item1, itr.Item5); 
                    if (TSstructure != null) 
                    { 
                        e.updatedObj.Add(TSstructure); 
                        e.numAddedStructs++; 
                    }
                }
            }
            //return the entire data structure
            return e;
        }

        private void removeCoolHeatStructures(ExternalPlanSetup plan)
        {
            StructureSet ss = plan.StructureSet;
            foreach(Structure itr in ss.Structures.Where(x => x.Id.ToLower().Contains("ts_cooler") || x.Id.ToLower().Contains("ts_heater")))
            {
                if (ss.CanRemoveStructure(itr)) ss.RemoveStructure(itr);
                else ProvideUIUpdate(String.Format("Warning! Cannot remove {0} from the structure set! Skipping!", itr.Id));
            }
        }
        //**********************************************************************************************************************************************************************************************************************************************************************************************************************************************************************

        #region generate heaters and coolers
        private Tuple<string, string, double, double, int> generateCooler(ExternalPlanSetup plan, double doseLevel, double requestedDoseConstraint, double volume, string name, int priority)
        {
            //create an empty optiization objective
            Tuple<string, string, double, double, int> cooler = null;
            StructureSet s = plan.StructureSet;
            //grab the relevant dose, dose leve, priority, etc. parameters
            PlanningItemDose d = plan.Dose;
            DoseValue dv = new DoseValue(doseLevel * plan.TotalDose.Dose, DoseValue.DoseUnit.cGy);
            if (s.CanAddStructure("CONTROL", name))
            {
                //add the cooler structure to the structure list and convert the doseLevel isodose volume to a structure. Add this new structure to the list with a max dose objective of Rx * 105% and give it a priority of 80
                Structure coolerStructure = s.AddStructure("CONTROL", name);
                coolerStructure.ConvertDoseLevelToStructure(d, dv);
                cooler = Tuple.Create(name, "Upper", requestedDoseConstraint * plan.TotalDose.Dose, volume, priority);
            }
            return cooler;
        }

        private Tuple<string, string, double, double, int> generateHeater(ExternalPlanSetup plan, Structure target, double doseLevelLow, double doseLevelHigh, double volume, string name, int priority)
        {
            //similar to the generateCooler method
            Tuple<string, string, double, double, int> heater = null;
            Structure heaterStructure = null;
            StructureSet s = plan.StructureSet;
            PlanningItemDose d = plan.Dose;
            DoseValue dv = new DoseValue(doseLevelLow * plan.TotalDose.Dose, DoseValue.DoseUnit.cGy);
            if (s.CanAddStructure("CONTROL", name))
            {
                //segment lower isodose volume
                heaterStructure = s.AddStructure("CONTROL", name);
                heaterStructure.ConvertDoseLevelToStructure(d, dv);
                //segment higher isodose volume
                Structure dummy = s.AddStructure("CONTROL", "dummy");
                //dummy.ConvertDoseLevelToStructure(d, plan.TotalDose);
                dummy.ConvertDoseLevelToStructure(d, new DoseValue(doseLevelHigh * plan.TotalDose.Dose, DoseValue.DoseUnit.cGy));
                //boolean the lower isodose volume with ts_PTV_VMAT (we only want the lower isodose volumes that overlap with the target) onto the heater structure
                heaterStructure.SegmentVolume = heaterStructure.And(target.SegmentVolume.Margin(0.0));
                //subtract the higher isodose volume from the heater structure and assign it to the heater structure. This is the heater structure that will be used for optimization. Create a new optimization objective for this tunning structure
                heaterStructure.SegmentVolume = heaterStructure.Sub(dummy.SegmentVolume.Margin(0.0));
                //heaters generally need to increase the dose to regions of the target NOT receiving the Rx dose --> always set the dose objective to the Rx dose
                heater = Tuple.Create(name, "Lower", plan.TotalDose.Dose, volume, priority);
                //clean up
                s.RemoveStructure(dummy);
                //only keep the overlapping regions of the heater structure with the taget structure
                heaterStructure.SegmentVolume = heaterStructure.And(target.Margin(0.0));
            }
            return heater;
        }
        #endregion

        private void UpdateConstraints(List<Tuple<string, string, double, double, int>> obj, ExternalPlanSetup plan)
        {
            //remove all existing optimization constraints
            foreach (OptimizationObjective o in plan.OptimizationSetup.Objectives) plan.OptimizationSetup.RemoveObjective(o);
            //assign the new optimization constraints (passed as an argument to this method)
            foreach (Tuple<string, string, double, double, int> opt in obj)
            {
                if (opt.Item2.ToLower() == "upper") plan.OptimizationSetup.AddPointObjective(plan.StructureSet.Structures.First(x => x.Id == opt.Item1), OptimizationObjectiveOperator.Upper, new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), opt.Item4, opt.Item5);
                else if (opt.Item2.ToLower() == "lower") plan.OptimizationSetup.AddPointObjective(plan.StructureSet.Structures.First(x => x.Id == opt.Item1), OptimizationObjectiveOperator.Lower, new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), opt.Item4, opt.Item5);
                else if (opt.Item2.ToLower() == "mean") plan.OptimizationSetup.AddMeanDoseObjective(plan.StructureSet.Structures.First(x => x.Id == opt.Item1), new DoseValue(opt.Item3, DoseValue.DoseUnit.cGy), opt.Item5);
                else if (opt.Item2.ToLower() == "exact") MessageBox.Show("Script not setup to handle exact dose constraints!");
                else MessageBox.Show("Constraint type not recognized!");
            }
        }

        public void normalizePlan(ExternalPlanSetup plan, double relativeDose, double targetVolCoverage, bool useFlash)
        {
            //in demo mode, dose might not be calculated for the plan
            if (!plan.IsDoseValid) return;
            //how to normalize a plan in the ESAPI workspace:
            //reference: https://github.com/VarianAPIs/Varian-Code-Samples/blob/master/webinars%20%26%20workshops/Research%20Symposium%202015/Eclipse%20Scripting%20API/Projects/AutomatedPlanningDemo/PlanGeneration.cs
            Structure target;
            //if (!useFlash) target = plan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToLower() == "ts_ptv_vmat");
            if (!useFlash) target = plan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToLower() == "ts_ptv_csi");
            else target = plan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToLower() == "ts_ptv_flash");
            plan.PlanNormalizationValue = 100.0;
            //absolute dose
            double RxDose = plan.TotalDose.Dose;
            //construct a DoseValue from RxDose
            DoseValue dv = new DoseValue(relativeDose * RxDose / 100, DoseValue.DoseUnit.cGy);
            //get current coverage of the RxDose
            double coverage = plan.GetVolumeAtDose(target, dv, VolumePresentation.Relative);
            //MessageBox.Show(String.Format("{0}, {1}", dv, coverage));
            
            //if the current coverage doesn't equal the desired coverage, then renormalize the plan
            if (coverage != targetVolCoverage)
            {
                //get the dose that does cover the targetVolCoverage of the target volume and scale the dose distribution by the ratio of that dose to the relative prescription dose
                dv = plan.GetDoseAtVolume(target, targetVolCoverage, VolumePresentation.Relative, DoseValuePresentation.Absolute);
                plan.PlanNormalizationValue = 100.0 * dv.Dose / (relativeDose * RxDose / 100);
                //MessageBox.Show(String.Format("{0}, {1}, {2}", dv, plan.PlanNormalizationValue, plan.Dose.DoseMax3D.Dose));
            }
        }
    }
}
