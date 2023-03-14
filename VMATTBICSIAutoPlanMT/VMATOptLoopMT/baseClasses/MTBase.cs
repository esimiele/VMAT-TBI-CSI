﻿using System;
using System.Text;
using System.Windows.Threading;
using VMATTBICSIAutoplanningHelpers.MTWorker;
using VMATTBICSIOptLoopMT.MTProgressInfo;

namespace VMATTBICSIOptLoopMT.baseClasses
{
    public class MTbase
    {
        private Dispatcher _dispatch;
        protected progressWindow _pw;
        //private StringBuilder  _logOutput;
        protected string logPath;
        protected string fileName;
        //public StringBuilder GetLogOutput() { return _logOutput; }

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
                progressWindow pw = new progressWindow();
                pw.setCallerClass(slave, this);
                pw.ShowDialog();

                //tell the code to hold until the progress window closes.
                frame.Continue = false;
            });
            Dispatcher.PushFrame(frame);
            return slave.isError;
        }

        public void SetDispatcherAndUIInstance(Dispatcher d, progressWindow p)
        {
            _dispatch = d;
            _pw = p;
            //perform logging on progress window UI thread
            _dispatch.BeginInvoke((Action)(() => { _pw.InitializeLogFile(logPath, fileName); }));
            //_logOutput = new StringBuilder();
        }

        protected void SetAbortUIStatus(string message)
        {
            //if (!string.IsNullOrEmpty(message)) _logOutput.AppendLine(message);
            _dispatch.BeginInvoke((Action)(() => { _pw.abortStatus.Text = message; }));
        }

        public void UpdateUILabel(string message)
        {
            _dispatch.BeginInvoke((Action)(() => { _pw.UpdateLabel(message); }));
        }

        protected void ProvideUIUpdate(int percentComplete, string message = "", bool fail = false) 
        {
            //if(!string.IsNullOrEmpty(message)) _logOutput.AppendLine(message);
            _dispatch.BeginInvoke((Action)(() => { _pw.provideUpdate(percentComplete, message, fail); })); 
        }

        protected void ProvideUIUpdate(string message, bool fail = false) 
        {
            //_logOutput.AppendLine(message);
            _dispatch.BeginInvoke((Action)(() => { _pw.provideUpdate(message, fail); })); 
        }

        protected void UpdateOverallProgress(int percentComplete)
        {
            _dispatch.BeginInvoke((Action)(() => { _pw.updateOverallProgress(percentComplete); }));
        }

        protected bool GetAbortStatus()
        {
            return _pw.abortOpt;
        }

        protected void KillOptimizationLoop()
        {
            _dispatch.BeginInvoke((Action)(() => { _pw.setAbortStatus(); }));
        }

        protected void OptimizationLoopFinished()
        {
            ProvideUIUpdate(100, Environment.NewLine + " Finished!");
            _dispatch.BeginInvoke((Action)(() => { _pw.isFinished = true;  _pw.setAbortStatus(); }));
        }

        protected string GetElapsedTime()
        {
            return _pw.currentTime;
        }
    }
}
