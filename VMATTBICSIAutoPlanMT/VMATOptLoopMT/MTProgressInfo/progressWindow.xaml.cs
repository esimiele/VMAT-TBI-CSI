using System;
using System.Windows;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;
using VMATTBICSIOptLoopMT.baseClasses;
using VMATTBICSIAutoplanningHelpers.Prompts;
using VMATTBICSIAutoplanningHelpers.MTWorker;
using System.Windows.Forms;

namespace VMATTBICSIOptLoopMT.MTProgressInfo
{
    public partial class progressWindow : Window
    {
        //flags to let the code know if the user hit the 'Abort' button, if the optimization loop is finished, 
        //and if the GUI can close safely (you don't want to close it if the background thread hasn't stopped working)
        public bool abortOpt;
        public bool isFinished;
        public bool canClose;
        //used to copy the instances of the background thread and the optimizationLoop class
        ESAPIworker slave;
        MTbase callerClass;
        //path to where the log files should be written
        string logPath = "";
        //filename
        string fileName = "";
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
            doStuff();
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

        #region update UI methods
        public void UpdateLabel(string message)
        {
            taskLabel.Text = message;
        }

        //three overloaded methods to provide periodic updates on the progress of the optimization loop
        public void provideUpdate(int percentComplete, string message, bool fail)
        {
            if (fail) FailEvent();
            taskProgress.Value = percentComplete;
            if(!string.IsNullOrEmpty(message))
            {
                update.Text += message + Environment.NewLine;
                scroller.ScrollToBottom();
                updateLogFile(message);
            }
        }

        public void provideUpdate(string message, bool fail) 
        {
            if (fail) FailEvent();
            update.Text += message + Environment.NewLine; 
            scroller.ScrollToBottom(); 
            updateLogFile(message); 
        }

        public void updateOverallProgress(int percentComplete)
        {
            overallProgress.Value = percentComplete;
        }

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
        public void InitializeLogFile(string path, string name)
        {
            logPath = path;
            fileName = name;
            if (!Directory.Exists(logPath)) Directory.CreateDirectory(logPath);
        }

        private void updateLogFile(string output)
        {
            if (Directory.Exists(logPath))
            {
                output += Environment.NewLine;
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
        #endregion

        #region abort run
        private void Abort_Click(object sender, RoutedEventArgs e)
        {
            //the user wants to stop the optimization loop. Set the abortOpt flag to true. The optimization loop will stop when it reaches an appropriate point
            if (!isFinished)
            {
                string message = Environment.NewLine + Environment.NewLine +
                    " Abort command received!" + Environment.NewLine + " The optimization loop will be stopped at the next available stopping point!" + Environment.NewLine + " Be patient!";
                update.Text += message + Environment.NewLine;
                abortOpt = true;
                abortStatus.Text = "Canceling";
                abortStatus.Background = Brushes.Yellow;
                updateLogFile(message);
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
        #endregion

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            //extremely annoying message letting the user know that they cannot shut down the program until the optimization loop reaches a safe stopping point. The confirm window will keep popping up until 
            //the optimization loop reaches a safe stopping point. At that time, the user can close the application. If the user closes the taskProgress window before that time, the background thread will still be working.
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
