using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Threading;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Runtime.InteropServices;
using VMATTBICSIOptLoopMT.MTWorker;
using VMATTBICSIOptLoopMT.Prompts;
using VMATTBICSIOptLoopMT.PlanEvaluation;
using VMATTBICSIOptLoopMT.baseClasses;

namespace VMATTBICSIOptLoopMT
{
    public partial class progressWindow : Window
    {
        //flags to let the code know if the user hit the 'Abort' button, if the optimization loop is finished, and if the GUI can close safely (you don't want to close it if the background thread hasn't stopped working)
        public bool abortOpt;
        public bool isFinished;
        public bool canClose;
        //used to copy the instances of the background thread and the optimizationLoop class
        ESAPIworker slave;
        MTbase callerClass;
        //string to hold the patient MRN number
        string id = "";
        //path to where the log files should be written
        string logPath = "";

        //get instances of the stopwatch and dispatch timer to report how long the calculation takes at each reporting interval
        Stopwatch sw = new Stopwatch();
        DispatcherTimer dt = new DispatcherTimer();
        public string currentTime = "";

        public progressWindow()
        {
            InitializeComponent();
        }

        public void setCallerClass<T>(ESAPIworker e, T caller)
        {
            //Make all worker classes derive from MTbase. This simplifies the type casting as opposed to try and figure out the class at run time
            callerClass = caller as MTbase;
            slave = e;
            //initialize and start the stopwatch
            runTime.Text = "00:00:00";
            dt.Tick += new EventHandler(dt_tick);
            dt.Interval = new TimeSpan(0, 0, 1);
            try
            {
                doStuff();
            }
            catch (Exception except) { System.Windows.MessageBox.Show(except.Message); }
        }

        public void doStuff()
        {
            slave.DoWork(() =>
            {
                //get instance of current dispatcher (double check the use of this dispatcher vs the dispatcher held in ESAPIworker...)
                Dispatcher dispatch = Dispatcher;
                //asign the dispatcher and an instance of this class to the caller class (used to marshal updates back to this UI)
                callerClass.SetDispatcherAndUIInstance(dispatch, this);
                //start the stopwatch
                sw.Start();
                dt.Start();
                //start the tasks asynchronously
                if (callerClass.Run()) slave.isError = true;
                //stop the stopwatch
                sw.Stop();
                dt.Stop();
            });
        }

        private void Abort_Click(object sender, RoutedEventArgs e)
        {
            //the user wants to stop the optimization loop. Set the abortOpt flag to true. The optimization loop will stop when it reaches an appropriate point
            if (!isFinished)
            {
                string message = Environment.NewLine + Environment.NewLine + 
                    " Abort command received!" + Environment.NewLine + " The optimization loop will be stopped at the next available stopping point!" + Environment.NewLine + " Be patient!";
                update.Text += message +Environment.NewLine;
                abortOpt = true;
                abortStatus.Text = "Canceling";
                abortStatus.Background = Brushes.Yellow;
                updateLogFile(message);
            }
        }

        private void dt_tick(object sender, EventArgs e)
        {
            //increment the time on the progress window for each "tick", which is set to intervals of 1 second
            if (sw.IsRunning)
            {
                TimeSpan ts = sw.Elapsed;
                currentTime = String.Format("{0:00}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds);
                runTime.Text = currentTime;
            }
        }

        //public void doTheStuff()
        //{
        //            //really crank up the priority and lower the dose objective on the cooler on the last iteration of the optimization loop
        //            //this is basically here to avoid having to call op.updateConstraints a second time (if this batch of code was placed outside of the loop)
        //            if (d.oneMoreOpt && ((count + 1) == d.numOptimizations))
        //            {
        //                //go through the current list of optimization objects and add all of them to finalObj vector. ADD COMMENTS!
        //                List<Tuple<string, string, double, double, int>> finalObj = new List<Tuple<string, string, double, double, int>> { };
        //                foreach (Tuple<string, string, double, double, int> itr in e.updatedObj)
        //                {
        //                    //get maximum priority and assign it to the cooler structure to really push the hotspot down. Also lower dose objective
        //                    if (itr.Item1.ToLower().Contains("ts_cooler"))
        //                    {
        //                        finalObj.Add(new Tuple<string, string, double, double, int>(itr.Item1, itr.Item2, 0.98*itr.Item3, itr.Item4, Math.Max(itr.Item5, (int)(0.9*(double)e.updatedObj.Max(x => x.Item5)))));
        //                    }
        //                    else finalObj.Add(itr);
        //                }
        //                //set e.updatedObj to be equal to finalObj
        //                e.updatedObj = finalObj;
        //            }

        //            //print the updated optimization objectives to the user
        //            string newObj = System.Environment.NewLine;
        //            newObj += optObjHeader;
        //            foreach (Tuple<string, string, double, double, int> itr in e.updatedObj)
        //                newObj += String.Format(" {0, -15} | {1, -16} | {2,-10:N1} | {3,-10:N1} | {4,-8} |" + System.Environment.NewLine, itr.Item1, itr.Item2, itr.Item3, itr.Item4, itr.Item5);

        //            //update the optimization constraints in the plan
        //            op.updateConstraints(e.updatedObj, d.plan);
        //            Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * (++percentCompletion) / calcItems), newObj); }));
        //            if (abortOpt)
        //            {
        //                Dispatcher.BeginInvoke((Action)(() => { setAbortStatus(); }));
        //                return;
        //            }
        //            //increment the counter, update d.optParams so it is set to the initial optimization constraints at the BEGINNING of the optimization iteration, and save the changes to the plan
        //            count++;
        //            d.optParams = e.updatedObj;
        //            if(!demo) d.app.SaveModifications();
        //        }

        //        //option to run one additional optimization (can be requested on the main GUI)
        //        if (d.oneMoreOpt)
        //        {
        //            Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * (++percentCompletion) / calcItems), " Running one final optimization starting at MR3 to try and reduce global plan hotspots!"); }));
        //            Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format(" Elapsed time: {0}", currentTime)); }));
        //            //one final push to lower the global plan hotspot if the user asked for it
        //            if (demo) Thread.Sleep(3000);
        //            else
        //            {
        //                //run optimization using current dose as intermediate dose. This will start the optimization at MR3 or MR4 (depending on the configuration of Eclipse)
        //                try
        //                {
        //                    OptimizerResult optRes = d.plan.OptimizeVMAT(new OptimizationOptionsVMAT(OptimizationOption.ContinueOptimizationWithPlanDoseAsIntermediateDose, ""));
        //                    if (!optRes.Success) { Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format(" Error! Optimization failed!" + System.Environment.NewLine + " Try running the optimization manually Eclipse for more information!" + System.Environment.NewLine + System.Environment.NewLine + " Exiting!")); })); abortOpt = true; }
        //                    d.app.SaveModifications();
        //                }
        //                catch (Exception except) { Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format(" Error! Optimization failed because: {0}" + System.Environment.NewLine + System.Environment.NewLine + " Exiting!", except.Message)); })); abortOpt = true; }
        //            }

        //            if (abortOpt)
        //            {
        //                Dispatcher.BeginInvoke((Action)(() => { setAbortStatus(); }));
        //                return;
        //            }

        //            Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * (++percentCompletion) / calcItems), " Optimization finished! Calculating dose!"); }));
        //            Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format(" Elapsed time: {0}", currentTime)); }));
        //            if (demo) Thread.Sleep(3000);
        //            else
        //            {
        //                //calculate dose
        //                try
        //                {
        //                    CalculationResult calcRes = d.plan.CalculateDose();
        //                    if (!calcRes.Success) { Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format(" Error! Dose calculation failed!" + System.Environment.NewLine + " Try running the dose calculation manually Eclipse for more information!" + System.Environment.NewLine + System.Environment.NewLine + " Exiting!")); })); abortOpt = true; }
        //                    d.app.SaveModifications();
        //                }
        //                catch (Exception except) { Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format(" Error! Dose calculation failed because: {0}" + System.Environment.NewLine + System.Environment.NewLine + " Exiting!", except.Message)); })); abortOpt = true; }
        //            }

        //            if (abortOpt)
        //            {
        //                Dispatcher.BeginInvoke((Action)(() => { setAbortStatus(); }));
        //                return;
        //            }

        //            Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * (++percentCompletion) / calcItems), " Dose calculated, normalizing plan!"); }));
        //            Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format(" Elapsed time: {0}" + System.Environment.NewLine, currentTime)); }));
        //            //normalize
        //            op.normalizePlan(d.plan, d.relativeDose, d.targetVolCoverage, d.useFlash);

        //            //print useful info about target coverage and global dmax
        //            Structure target;
        //            if (d.useFlash) target = d.plan.StructureSet.Structures.First(x => x.Id.ToLower() == "ts_ptv_flash");
        //            //else target = d.plan.StructureSet.Structures.First(x => x.Id.ToLower() == "ts_ptv_vmat");
        //            else target = d.plan.StructureSet.Structures.First(x => x.Id.ToLower() == "ts_ptv_csi");
        //            string message = " Final plan infomation: " + System.Environment.NewLine +
        //                            String.Format(" Plan global Dmax = {0:0.0}%", 100 * (d.plan.Dose.DoseMax3D.Dose / d.plan.TotalDose.Dose)) + System.Environment.NewLine +
        //                            String.Format(" {0} Dmax = {1:0.0}%", target.Id, d.plan.GetDoseAtVolume(target, 0.0, VolumePresentation.Relative, DoseValuePresentation.Relative).Dose) + System.Environment.NewLine +
        //                            String.Format(" {0} Dmin = {1:0.0}%", target.Id, d.plan.GetDoseAtVolume(target, 100.0, VolumePresentation.Relative, DoseValuePresentation.Relative).Dose) + System.Environment.NewLine +
        //                            String.Format(" {0} V90% = {1:0.0}%", target.Id, d.plan.GetVolumeAtDose(target, new DoseValue(90.0, DoseValue.DoseUnit.Percent), VolumePresentation.Relative)) + System.Environment.NewLine +
        //                            String.Format(" {0} V110% = {1:0.0}%", target.Id, d.plan.GetVolumeAtDose(target, new DoseValue(110.0, DoseValue.DoseUnit.Percent), VolumePresentation.Relative)) + System.Environment.NewLine +
        //                            String.Format(" {0} V120% = {1:0.0}%", target.Id, d.plan.GetVolumeAtDose(target, new DoseValue(120.0, DoseValue.DoseUnit.Percent), VolumePresentation.Relative));
        //            Dispatcher.BeginInvoke((Action)(() => { provideUpdate(message); }));
        //        }

        //        if (d.useFlash)
        //        {
        //            Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * (++percentCompletion) / calcItems), String.Format(System.Environment.NewLine + " Removing flash, recalculating dose, and renormalizing to TS_PTV_VMAT!")); }));
        //            Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format(" Elapsed time: {0}", currentTime)); }));

        //            Structure bolus = d.plan.StructureSet.Structures.FirstOrDefault(x => x.Id.ToLower() == "bolus_flash");
        //            if (bolus == null)
        //            {
        //                //no structure named bolus_flash found. This is a problem. 
        //                Dispatcher.BeginInvoke((Action)(() => { provideUpdate(" No structure named 'BOLUS_FLASH' found in structure set! Exiting!"); }));
        //            }
        //            else
        //            {
        //                //reset dose calculation matrix for each plan in the current course. Sorry! You will have to recalculate dose to EVERY plan!
        //                string calcModel = d.plan.GetCalculationModel(CalculationType.PhotonVolumeDose);
        //                List<ExternalPlanSetup> plansWithCalcDose = new List<ExternalPlanSetup> { };
        //                foreach (ExternalPlanSetup p in d.plan.Course.ExternalPlanSetups)
        //                {
        //                    if (p.IsDoseValid && p.StructureSet == d.plan.StructureSet)
        //                    {
        //                        p.ClearCalculationModel(CalculationType.PhotonVolumeDose);
        //                        p.SetCalculationModel(CalculationType.PhotonVolumeDose, calcModel);
        //                        plansWithCalcDose.Add(p);
        //                    }
        //                }
        //                //reset the bolus dose to undefined
        //                bolus.ResetAssignedHU();
        //                //recalculate dose to all the plans that had previously had dose calculated in the current course
        //                if (demo) Thread.Sleep(3000);
        //                else
        //                {
        //                    foreach (ExternalPlanSetup p in plansWithCalcDose) { try { p.CalculateDose(); } catch (Exception except) { Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format(" Error! Dose calculation failed because: {0}", except.Message)); }));} }

        //                }
        //                Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * (++percentCompletion) / calcItems), " Dose calculated, normalizing plan!"); }));
        //                Dispatcher.BeginInvoke((Action)(() => { provideUpdate(String.Format(" Elapsed time: {0}", currentTime)); }));
        //                //"trick" the normalizePlan method into thinking we are not using flash. Therefore, it will normalize to TS_PTV_VMAT instead of TS_PTV_FLASH (i.e., set useFlash to false)
        //                op.normalizePlan(d.plan, d.relativeDose, d.targetVolCoverage, false);
        //            }
        //        }

        //        //optimization loop is finished, let user know, and save the changes to the plan
        //        isFinished = true;
        //        Dispatcher.BeginInvoke((Action)(() => { provideUpdate((int)(100 * (++percentCompletion) / calcItems), System.Environment.NewLine + " Finished!"); setAbortStatus(); }));
        //        if(!demo) d.app.SaveModifications();
        //    });
        //}

        //three overloaded methods to provide periodic updates on the progress of the optimization loop
        public void provideUpdate(int percentComplete, string message, bool fail)
        {
            if (fail) FailEvent();
            progress.Value = percentComplete;
            update.Text += message + Environment.NewLine;
            scroller.ScrollToBottom();
            updateLogFile(message);
        }

        public void provideUpdate(int percentComplete) 
        { 
            progress.Value = percentComplete; 
        }

        public void provideUpdate(string message, bool fail) 
        {
            if (fail) FailEvent();
            update.Text += message + Environment.NewLine; 
            scroller.ScrollToBottom(); 
            updateLogFile(message); 
        }

        private void FailEvent()
        {
            progress.Background = Brushes.Red;
            progress.Foreground = Brushes.Red;
            abortStatus.Text = "Failed!";
            abortStatus.Background = Brushes.Red;
            sw.Stop();
            dt.Stop();
            canClose = true;
        }

        private void updateLogFile(string output)
        {
            //this is here to check if the directory and file already exist. An alternative method would be to create a streamwriter in the constructor of this class, but because this program runs for several hours and I have no
            //control over the shared drive, there may be a situation where the streamwriter is created and wants to write to the file after a few hours and (for whatever reason) the directory/file is gone. In this case, it would likely
            //crash the program
            if (Directory.Exists(logPath))
            {
                output += Environment.NewLine;
                string fileName = logPath + id + ".txt";
                File.AppendAllText(fileName, output);
            }
        }

        //option to write the results to a text file in a user-specified location. A window will pop-up asking the user to navigate to their chosen directory and save the file with a custom name
        private void WriteResults_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Title = "Choose text file output",
                CheckPathExists = true,

                DefaultExt = "txt",
                Filter = "txt files (*.txt)|*.txt",
                FilterIndex = 2,
                RestoreDirectory = true,
            };

            if (saveFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string output = update.Text;
                string fileName = saveFileDialog1.FileName;
                File.WriteAllText(fileName, output);
                update.Text += String.Format(Environment.NewLine + " Output written to text file at: {0}" + Environment.NewLine, string.Concat(fileName));
            }
        }

        public void setAbortStatus()
        {
            if (abortOpt)
            {
                //the user requested to abort the optimization loop
                abortStatus.Text = "Aborted!";
                abortStatus.Background = Brushes.Red;
            }
            else
            {
                //the optimization loop finished successfully
                abortStatus.Text = "Finished!";
                abortStatus.Background = Brushes.LimeGreen;
            }
            //stop the clock and report the total run time. Also set the canClose flag to true to let the code know the background thread has finished working and it is safe to close
            sw.Stop();
            dt.Stop();
            canClose = true;
            provideUpdate(String.Format(" Total run time: {0}", currentTime), false);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            //extremely annoying message letting the user know that they cannot shut down the program until the optimization loop reaches a safe stopping point. The confirm window will keep popping up until 
            //the optimization loop reaches a safe stopping point. At that time, the user can close the application. If the user closes the progress window before that time, the background thread will still be working.
            //If the user forces the application to close, the timestamp within eclipse will still be there and it is not good to kill multithreaded applications in this way.
            //Basically, this code is an e-bomb, and will ensure the program can't be killed by the user until a safe stopping point has been reached (at least without the user of the task manager)
            while (!canClose)
            {
                if (!abortOpt)
                {
                    abortStatus.Text = "Canceling";
                    abortStatus.Background = Brushes.Yellow;
                    abortOpt = true;
                }
                confirmUI CUI = new confirmUI();
                CUI.message.Text = String.Format("I can't close until the optimization loop has stopped!"
                    + Environment.NewLine + "Please wait until the abort status says 'Aborted' or 'Finished' and then click 'Confirm'.");
                CUI.ShowDialog();
            }
        }
    }
}
