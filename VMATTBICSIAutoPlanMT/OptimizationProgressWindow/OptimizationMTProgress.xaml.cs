using System;
using System.Windows;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace OptimizationProgressWindow
{
    public partial class OptimizationMTProgress : Window
    {
        #region Get methods
        //flags to let the code know if the user hit the 'Abort' button, if the optimization loop is finished, 
        //and if the GUI can close safely (you don't want to close it if the background thread hasn't stopped working)
        public bool GetAbortStatus() { return abortOpt; }
        public void SetFinishStatus(bool status) { isFinished = status; }
        public string GetElapsedTime() { return $"{sw.Elapsed.Hours:00}:{sw.Elapsed.Minutes:00}:{sw.Elapsed.Seconds:00}"; }
        #endregion

        private bool abortOpt;
        private bool isFinished;
        private bool canClose;
        //used to copy the instances of the background thread and the optimizationLoop class
        private ESAPIWorker slave;
        private OptimizationMTbase callerClass;
        //path to where the log files should be written
        private string logPath = "";
        //filename
        private string fileName = "";
        // errors and warnings log
        private string fileNameErrorsWarnings = "";
        //get instances of the stopwatch and dispatch timer to report how long the calculation takes at each reporting interval
        private Stopwatch sw = new Stopwatch();
        private DispatcherTimer dt = new DispatcherTimer();

        /// <summary>
        /// Constructor
        /// </summary>
        public OptimizationMTProgress()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Method to initialize the calling class instance, UI thread, and stopwatch. Launch the optimization loop on the main thread
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="e"></param>
        /// <param name="caller"></param>
        public void SetCallerClass<T>(ESAPIWorker e, T caller)
        {
            //Make all worker classes derive from MTbase. This simplifies the type casting as opposed to try and figure out the class at run time
            callerClass = caller as OptimizationMTbase;
            slave = e;
            //initialize and start the stopwatch
            dt.Tick += new EventHandler(Dt_tick);
            dt.Interval = new TimeSpan(0, 0, 1);
            DoStuff();
        }

        /// <summary>
        /// Method to initialize the dispatcher and this class instance on the main thread to be able to marshal updates back to the UI thread.
        /// Launch the optimization loop (call the Run method)
        /// </summary>
        public void DoStuff()
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

        /// <summary>
        /// Tick event. Called every tick interval on the stopwatch
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Dt_tick(object sender, EventArgs e)
        {
            //increment the time on the progress window for each "tick", which is set to intervals of 1 second
            if (sw.IsRunning)
            {
                TimeSpan ts = sw.Elapsed;
                runTime.Text = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
            }
        }

        #region update UI methods
        /// <summary>
        /// Update the current task category label information
        /// </summary>
        /// <param name="message"></param>
        public void UpdateLabel(string message)
        {
            taskLabel.Text = message;
        }

        /// <summary>
        /// Simple method to update the text block in the UI and progress bar with information from the main thread running the optimization loop
        /// </summary>
        /// <param name="percentComplete"></param>
        /// <param name="message"></param>
        /// <param name="fail"></param>
        //two overloaded methods to provide periodic updates on the progress of the optimization loop
        public void ProvideUpdate(int percentComplete, string message, bool fail)
        {
            if (fail) FailEvent();
            taskProgress.Value = percentComplete;
            if(!string.IsNullOrEmpty(message))
            {
                update.Text += message + Environment.NewLine;
                scroller.ScrollToBottom();
                UpdateLogFile(message);
            }
        }

        /// <summary>
        /// Overloaded method to update the text block in the UI
        /// </summary>
        /// <param name="message"></param>
        /// <param name="fail"></param>
        public void ProvideUpdate(string message, bool fail) 
        {
            if (fail) FailEvent();
            update.Text += message + Environment.NewLine; 
            scroller.ScrollToBottom(); 
            UpdateLogFile(message); 
        }

        /// <summary>
        /// Update the overall optimization loop progress bar in the UI
        /// </summary>
        /// <param name="percentComplete"></param>
        public void UpdateOverallProgress(int percentComplete)
        {
            overallProgress.Value = percentComplete;
        }

        /// <summary>
        /// Method to help handle the case where the optimization loop ran into a problem and the UI needs to indicate that the optimization
        /// loop failed
        /// </summary>
        private void FailEvent()
        {
            taskProgress.Background = Brushes.Red;
            taskProgress.Foreground = Brushes.Red;
            overallProgress.Background = Brushes.Red;
            overallProgress.Foreground = Brushes.Red;
            abortStatus.Text = "Failed!";
            abortStatus.Background = Brushes.Red;
            sw.Stop();
            dt.Stop();
            canClose = true;
        }
        #endregion

        #region logging
        /// <summary>
        /// Helper method to initialize the log file that will be written during the optimization loop. Copies file path and full file names
        /// </summary>
        /// <param name="path"></param>
        /// <param name="name"></param>
        /// <param name="errorsWarnings"></param>
        public void InitializeLogFile(string path, string name, string errorsWarnings)
        {
            logPath = path;
            fileName = name;
            fileNameErrorsWarnings = errorsWarnings;
            if (!Directory.Exists(logPath)) Directory.CreateDirectory(logPath);
        }

        /// <summary>
        /// Simple method to append the supplied string text to the log file
        /// </summary>
        /// <param name="output"></param>
        private void UpdateLogFile(string output)
        {
            //verify the directory exists prior to writing the log
            if (Directory.Exists(logPath))
            {
                output += Environment.NewLine;
                File.AppendAllText(fileName, output);
            }
            else
            {
                ProvideUpdate($"Warning! {logPath} does not exist! Could not write to log file!", false);
            }
        }

        /// <summary>
        /// Helper method so the user can manually for the log file to be dumped at any time to any location on the computer (for their own information)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WriteResults_Click(object sender, RoutedEventArgs e)
        {
            //option to write the results to a text file in a user-specified location.
            //A window will pop-up asking the user to navigate to their chosen directory and save the file with a custom name
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
                update.Text += Environment.NewLine + $"Output written to text file at: {string.Concat(fileName)}" + Environment.NewLine;
            }
        }
        #endregion

        #region abort run
        /// <summary>
        /// Event when user hits the abort button on the UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Abort_Click(object sender, RoutedEventArgs e)
        {
            //the user wants to stop the optimization loop. Set the abortOpt flag to true.
            //The optimization loop will stop when it reaches an appropriate point
            if (!isFinished)
            {
                string message = Environment.NewLine + Environment.NewLine +
                    " Abort command received!" + Environment.NewLine + " The optimization loop will be stopped at the next available stopping point!" + Environment.NewLine + " Be patient!";
                update.Text += message + Environment.NewLine;
                abortOpt = true;
                abortStatus.Text = "Canceling";
                abortStatus.Background = Brushes.Yellow;
                UpdateLogFile(message);
            }
        }

        /// <summary>
        /// Helper method to handle the case when the optimization loop was terminated early
        /// </summary>
        public void OptimizationRunAborted()
        {
            //the user requested to abort the optimization loop
            abortStatus.Text = "Aborted!";
            abortStatus.Background = Brushes.Red;
            CleanUpRun();
        }

        /// <summary>
        /// Helper method the handle the case when the optimization loop completed
        /// </summary>
        public void OptimizationRunCompleted()
        {
            //the optimization loop finished successfully
            abortStatus.Text = "Finished!";
            abortStatus.Background = Brushes.LimeGreen;
            CleanUpRun();
        }

        /// <summary>
        /// Helper method to do any last minute cleaning up of the UI thread including stopping the stopwatch, indicating to the UI thread it is safe to 
        /// close, and print any warning messages that appeared during the run in the text block in the UI
        /// </summary>
        private void CleanUpRun()
        {
            //stop the clock and report the total run time. Also set the canClose flag to true to let the code know the background thread has finished working and it is safe to close
            sw.Stop();
            dt.Stop();
            canClose = true;
            ProvideUpdate(100, Environment.NewLine + "Finished!", false);
            ProvideUpdate($"Total run time: {GetElapsedTime()}" + Environment.NewLine, false);

            ProvideUpdate("Errors and warnings:", false);
            LoadAndPrintErrorsWarnings();
        }

        /// <summary>
        /// Print the errors and warning messages that were encountered during the run to the user. These are written to a temp log file throughout
        /// the optimization loop. This temp file is loaded and its contents are printed to the text block in the UI
        /// </summary>
        private void LoadAndPrintErrorsWarnings()
        {
            if(!File.Exists(fileNameErrorsWarnings))
            {
                ProvideUpdate("None", false);
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(fileNameErrorsWarnings))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrEmpty(line)) ProvideUpdate(line, false);
                    }
                    reader.Close();
                }
                return;
            }
            catch (Exception e) 
            { 
                ProvideUpdate($"Error! Could not load errors and warnings log because: {e.Message}", true); 
            }
        }
        #endregion

        /// <summary>
        /// Event raised when the window is closing. Since the UI is running on a separate thread, ensure the main thread is in a place that is appropriate
        /// for stopping. If not, the main thread will be frozen and unresponsive
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            //extremely annoying message letting the user know that they cannot shut down the program until the optimization loop reaches a safe stopping point. The confirm window will keep popping up until 
            //the optimization loop reaches a safe stopping point. At that time, the user can close the application. If the user closes the taskProgress window before that time, the background thread will still be working.
            //If the user forces the application to close, the timestamp within eclipse will still be there and it is not good to kill multithreaded applications in this way.
            //Basically, this code is an e-bomb, and will ensure the program can't be killed by the user until a safe stopping point has been reached (at least without the use of the task manager)
            while (!canClose)
            {
                if (!abortOpt)
                {
                    abortStatus.Text = "Canceling";
                    abortStatus.Background = Brushes.Yellow;
                    abortOpt = true;
                }
                System.Windows.Forms.MessageBox.Show("I can't close until the optimization loop has stopped!"
                    + Environment.NewLine + "Please wait until the abort status says 'Aborted' or 'Finished' and then click 'Confirm'.");
            }
        }
    }
}
