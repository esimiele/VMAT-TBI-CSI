using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIOptLoopMT.PlanEvaluation;
using VMATTBICSIOptLoopMT.baseClasses;
using VMATTBICSIOptLoopMT.helpers;

namespace VMATTBICSIOptLoopMT.VMAT_CSI
{
    class VMATCSIOptimization : optimizationLoopBase
    {
        ExternalPlanSetup evalPlan = null;

        public VMATCSIOptimization(dataContainer _d)
        {
            _data = _d;
            InitializeLogPathAndName();
            CalculateNumberOfItemsToComplete();
        }

        public override bool Run()
        {
            try
            {
                SetAbortUIStatus("Runnning");
                PrintRunSetupInfo(_data.plans);
                //preliminary checks
                UpdateUILabel("Preliminary checks:");
                ProvideUIUpdate("Performing preliminary checks now:");
                if (PreliminaryChecksSSAndImage(_data.selectedSS, GetAllTargets(_data.prescriptions))) return true;
                if (PreliminaryChecksCouch(_data.selectedSS)) return true;
                if (_checkSupportStructures)
                {
                    if(CheckSupportStructures(_data.plans.First().Course.Patient.Courses.ToList(), _data.selectedSS)) return true;
                }
                if (PreliminaryChecksPlans(_data.plans)) return true;

                if (RunOptimizationLoop(_data.plans)) return true;
                OptimizationLoopFinished();
            }
            catch (Exception e) { ProvideUIUpdate(String.Format("{0}", e.Message), true); return true; }
            return false;
        }

        protected override void CalculateNumberOfItemsToComplete()
        {
            overallCalcItems = 3;
            overallCalcItems += _data.plans.Count;
            int optLoopItems = 5 * _data.numOptimizations * _data.plans.Count;
            if (_data.oneMoreOpt) optLoopItems += 3;
            overallCalcItems += optLoopItems;
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
            UpdateUILabel("Build plan sum:");
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
                ProvideUIUpdate((int)(100 * (i + 1) / totalSlices));
                if((i+1) % 10 == 0) ProvideUIUpdate(String.Format("Summing doses from slice: {0}", i + 1));
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

        #region optimization loop
        protected override bool ResolveRunOptions(List<ExternalPlanSetup> plans)
        {
            if (_data.oneMoreOpt)
            {
                UpdateUILabel("One more optimization:");
                if (RunOneMoreOptionizationToLowerHotspots(plans)) return true;
                if (BuildPlanSum(evalPlan, plans)) return true;
            }
            return false;
        }

        protected override bool RunSequentialPlansOptimizationLoop(List<ExternalPlanSetup> plans)
        {
            //a requirement for sequentional optimization
            List<Tuple<string, string>> plansTargets = GetPlanTargetList(_data.prescriptions);
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

            ProvideUIUpdate(" Starting optimization loop!");
            int count = 0;
            while(count < _data.numOptimizations)
            {
                ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format(" Iteration {0}:", count + 1));
                ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));

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
                    ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format(" Optimizing plan: {0}!", itr.Id));
                    if (OptimizePlan(_data.isDemo, new OptimizationOptionsVMAT(OptimizationIntermediateDoseOption.NoIntermediateDose, ""), itr, _data.app)) return true;
                    ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), " Optimization finished! Calculating dose!");
                    if (CalculateDose(_data.isDemo, itr, _data.app)) return true;
                    ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), " Dose calculated, normalizing plan!");
                    //normalize
                    normalizePlan(itr, GetTargetForPlan(_data.selectedSS, plansTargets.FirstOrDefault(x => x.Item1 == itr.Id).Item2, _data.useFlash), _data.relativeDose, _data.targetVolCoverage);
                    if (GetAbortStatus())
                    {
                        KillOptimizationLoop();
                        return true;
                    }
                    ProvideUIUpdate((int)(100 * (++percentComplete) / calcItems), String.Format(" Plan normalized!"));
                    ProvideUIUpdate(String.Format(" Elapsed time: {0}", GetElapsedTime()));
                }
                
                if (BuildPlanSum(evalPlan, plans)) return true;
                count++;
            }

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

    //public class Testing
    //{
    //    private ExternalPlanSetup _plan;
    //    private bool _demo;
    //    public Testing(bool d, ExternalPlanSetup p)
    //    {
    //        _demo = d;
    //        _plan = p;
    //    }

    //    public void Run(object o)
    //    {
    //        Testing t = (Testing)o;
    //        CalculateDose(t._demo, t._plan);
    //    }

    //    public bool CalculateDose(bool isDemo, ExternalPlanSetup plan)
    //    {
    //        if (isDemo) Thread.Sleep(3000);
    //        else
    //        {
    //            string id = plan.Id;
    //            MessageBox.Show(id);
    //            //try
    //            //{
    //            //    CalculationResult calcRes = plan.CalculateDose();
    //            //}
    //            //catch (Exception except)
    //            //{
    //            //    MessageBox.Show(except.Message);
    //            //    //PrintFailedMessage("Dose calculation", except.Message);
    //            //    return true;
    //            //}
    //        }
           
    //        return false;
    //    }
    //}
}
