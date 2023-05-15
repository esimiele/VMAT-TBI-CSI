using System;
using System.Windows.Threading;
using ESAPIThreadWorker;

namespace OptimizationProgressWindow
{
    public class OptimizationMTbase
    {
        private Dispatcher _dispatch;
        protected OptimizationMTProgress _pw;
        protected string logPath;
        protected string fileName;

        public virtual bool Run()
        {
            return false;
        }

        public bool Execute()
        {
            ESAPIWorker slave = new ESAPIWorker();
            //create a new frame (multithreading jargon)
            DispatcherFrame frame = new DispatcherFrame();
            slave.RunOnNewThread(() =>
            {
                //pass the progress window the newly created thread and this instance of the optimizationLoop class.
                OptimizationMTProgress pw = new OptimizationMTProgress();
                pw.SetCallerClass(slave, this);
                pw.ShowDialog();

                //tell the code to hold until the progress window closes.
                frame.Continue = false;
            });
            Dispatcher.PushFrame(frame);
            return slave.isError;
        }

        public void SetDispatcherAndUIInstance(Dispatcher d, OptimizationMTProgress p)
        {
            _dispatch = d;
            _pw = p;
            //perform logging on progress window UI thread
            _dispatch.BeginInvoke((Action)(() => { _pw.InitializeLogFile(logPath, fileName); }));
        }

        protected void SetAbortUIStatus(string message)
        {
            _dispatch.BeginInvoke((Action)(() => { _pw.abortStatus.Text = message; }));
        }

        public void UpdateUILabel(string message)
        {
            _dispatch.BeginInvoke((Action)(() => { _pw.UpdateLabel(message); }));
        }

        protected void ProvideUIUpdate(int percentComplete, string message = "", bool fail = false) 
        {
            _dispatch.BeginInvoke((Action)(() => { _pw.ProvideUpdate(percentComplete, message, fail); })); 
        }

        protected void ProvideUIUpdate(string message, bool fail = false) 
        {
            _dispatch.BeginInvoke((Action)(() => { _pw.ProvideUpdate(message, fail); })); 
        }

        protected void UpdateOverallProgress(int percentComplete)
        {
            _dispatch.BeginInvoke((Action)(() => { _pw.UpdateOverallProgress(percentComplete); }));
        }

        protected bool GetAbortStatus()
        {
            return _pw.GetAbortStatus();
        }

        protected void KillOptimizationLoop()
        {
            _dispatch.BeginInvoke((Action)(() => { _pw.OptimizationRunAborted(); }));
        }

        protected void OptimizationLoopFinished()
        {
            ProvideUIUpdate(100, Environment.NewLine + " Finished!");
            _dispatch.BeginInvoke((Action)(() => { _pw.SetFinishStatus(true);  _pw.OptimizationRunCompleted(); }));
        }

        protected string GetElapsedTime()
        {
            return _pw.GetElapsedTime();
        }
    }
}
