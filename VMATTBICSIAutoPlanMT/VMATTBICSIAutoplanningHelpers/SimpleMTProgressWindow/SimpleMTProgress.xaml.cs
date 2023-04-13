using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using VMATTBICSIAutoplanningHelpers.MTWorker;

namespace VMATTBICSIAutoplanningHelpers.SimpleMTProgressWindow
{
    public partial class SimpleMTProgress : Window
    {
        private ESAPIworker slave;
        private SimpleMTbase callerClass;

        public SimpleMTProgress()
        {
            InitializeComponent();
        }

        //template function
        public void SetCallerClass<T>(ESAPIworker e, T caller)
        {
            //Make all worker classes derive from MTbase. This simplifies the type casting as opposed to try and figure out the class at run time
            callerClass = caller as SimpleMTbase;
            slave = e;
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
                //start the tasks asynchronously
                if (callerClass.Run()) slave.isError = true;
            });
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
