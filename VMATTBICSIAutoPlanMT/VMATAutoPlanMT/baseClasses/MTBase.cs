using System;
using System.Text;
using System.Windows.Threading;
using VMATTBICSIAutoplanningHelpers.MTWorker;
using VMATAutoPlanMT.MTProgressInfo;

namespace VMATAutoPlanMT.baseClasses
{
    public class MTbase
    {
        public StringBuilder GetLogOutput() { return _logOutput; }

        private Dispatcher _dispatch;
        private MTProgress _pw;
        private StringBuilder  _logOutput;

        public virtual bool Run()
        {
            return false;
        }

        public bool Execute()
        {
            ESAPIworker slave = new ESAPIworker();
            //create a new frame (multithreading jargon)
            DispatcherFrame frame = new DispatcherFrame();
            slave.RunOnNewThread(() =>
            {
                //pass the progress window the newly created thread and this instance of the optimizationLoop class.
                MTProgress pw = new MTProgress();
                pw.SetCallerClass(slave, this);
                pw.ShowDialog();

                //tell the code to hold until the progress window closes.
                frame.Continue = false;
            });
            Dispatcher.PushFrame(frame);
            return slave.isError;
        }

        public void SetDispatcherAndUIInstance(Dispatcher d, MTProgress p)
        {
            _dispatch = d;
            _pw = p;
            _logOutput = new StringBuilder();
        }

        protected void UpdateUILabel(string message) 
        { 
            _logOutput.AppendLine(message); 
            _dispatch.BeginInvoke((Action)(() => { _pw.UpdateLabel(message); })); 
        }

        protected void ProvideUIUpdate(int percentComplete, string message = "", bool fail = false) 
        {
            if(!string.IsNullOrEmpty(message)) _logOutput.AppendLine(message);
            _dispatch.BeginInvoke((Action)(() => { _pw.ProvideUpdate(percentComplete, message, fail); })); 
        }

        protected void ProvideUIUpdate(string message, bool fail = false) 
        {
            _logOutput.AppendLine(message);
            _dispatch.BeginInvoke((Action)(() => { _pw.ProvideUpdate(message, fail); })); 
        }
    }
}
