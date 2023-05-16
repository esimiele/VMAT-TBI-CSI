using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ESAPIThreadWorker;

namespace SimpleProgressWindow
{
    public partial class SimpleMTProgress : Window
    {
        public string GetElapsedTime() { return $"{sw.Elapsed.Minutes:00}:{sw.Elapsed.Seconds:00}"; }

        private ESAPIWorker slave;
        private SimpleMTbase callerClass;
        //get instances of the stopwatch and dispatch timer to report how long the calculation takes at each reporting interval
        private Stopwatch sw = new Stopwatch();
        private DispatcherTimer dt = new DispatcherTimer(DispatcherPriority.Normal);

        public SimpleMTProgress()
        {
            InitializeComponent();
        }

        //template function
        public void SetCallerClass<T>(ESAPIWorker e, T caller)
        {
            //Make all worker classes derive from MTbase. This simplifies the type casting as opposed to try and figure out the class at run time
            callerClass = caller as SimpleMTbase;
            slave = e;
            //initialize and start the stopwatch
            dt.Tick += new EventHandler(Dt_tick);
            dt.Interval = new TimeSpan(0, 0, 0, 0, 100);
            DoStuff();
        }

        public void DoStuff()
        {
            slave.DoWork(() =>
            {
                //get instance of current dispatcher which is used to asynchronously execute tasks on the MT progress UI thread
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

        private void Dt_tick(object sender, EventArgs e)
        {
            //increment the time on the progress window for each "tick", which is set to intervals of 1 second
            if (sw.IsRunning)
            {
                TimeSpan ts = sw.Elapsed;
                runTime.Text = $"{ts.Minutes:00}:{ts.Seconds:00}";
            }
        }

        public void UpdateLabel(string message) 
        { 
            taskLabel.Text = message; 
        }

        public void ProvideUpdate(int percentComplete, string message = "", bool fail = false)
        {
            if(fail) FailEvent();
            progress.Value = percentComplete;
            if(!string.IsNullOrEmpty(message))
            {
                progressTB.Text += message + Environment.NewLine;
                scroller.ScrollToBottom();
            }
        }

        public void ProvideUpdate(string message, bool fail = false) 
        {
            if(fail) FailEvent();
            progressTB.Text += message + Environment.NewLine;
            scroller.ScrollToBottom(); 
        }

        private void FailEvent()
        {
            progress.Background = Brushes.Red;
            progress.Foreground = Brushes.Red;
            UpdateLabel("FAILED!");
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) 
        { 
            SizeToContent = SizeToContent.WidthAndHeight; 
        }
    }
}
