using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using VMATTBICSIAutoplanningHelpers.MTWorker;
using VMATAutoPlanMT.baseClasses;

namespace VMATAutoPlanMT.MTProgressInfo
{
    public partial class MTProgress : Window
    {
        ESAPIworker slave;
        MTbase callerClass;
        public List<string> addedStructures = new List<string> { };

        public MTProgress()
        {
            InitializeComponent();
        }

        //template function
        public void setCallerClass<T>(ESAPIworker e, T caller)
        {
            //Make all worker classes derive from MTbase. This simplifies the type casting as opposed to try and figure out the class at run time
            callerClass = caller as MTbase;
            slave = e;
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
                //start the tasks asynchronously
                if (callerClass.Run()) slave.isError = true;
            });
        }

        public void UpdateLabel(string message) 
        { 
            theLabel.Text = message; 
        }

        public void provideUpdate(int percentComplete, string message = "", bool fail = false)
        {
            if(fail) FailEvent();
            progress.Value = percentComplete;
            if(!string.IsNullOrEmpty(message))
            {
                progressTB.Text += message + Environment.NewLine;
                scroller.ScrollToBottom();
            }
        }

        public void provideUpdate(string message, bool fail = false) 
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

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) { SizeToContent = SizeToContent.WidthAndHeight; }
    }
}
