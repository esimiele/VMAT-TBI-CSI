using System;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using VMATAutoPlanMT.MTProgressInfo;

namespace VMATAutoPlanMT.baseClasses
{
    public class MTbase
    {
        private Dispatcher _dispatch;
        private MTProgress _pw;
        private StringBuilder  _logOutput;
        public StringBuilder GetLogOutput() { return _logOutput; }

        public void MTBase()
        {
        }
        public void SetDispatcherAndUIInstance(Dispatcher d, MTProgress p)
        {
            _dispatch = d;
            _pw = p;
            _logOutput = new StringBuilder();
        }

        public virtual bool Run()
        {
            return false;
        }

        public void UpdateUILabel(string message) 
        { 
            try 
            { 
                _logOutput.AppendLine(message); 
            } 
            catch (Exception e) 
            { 
                _dispatch.BeginInvoke((Action)(() => { _pw.provideUpdate(e.Message); })); 
            }
            _dispatch.BeginInvoke((Action)(() => { _pw.UpdateLabel(message); })); 
        }

        public void ProvideUIUpdate(int percentComplete, string message, bool fail = false) {
            try
            {
                _logOutput.AppendLine(message);
            }
            catch (Exception e)
            {
                _dispatch.BeginInvoke((Action)(() => { _pw.provideUpdate(e.Message); }));
            }
            Thread.Sleep(1000);

            _dispatch.BeginInvoke((Action)(() => { _pw.provideUpdate(percentComplete, message, fail); })); }

        public void ProvideUIUpdate(int percentComplete) { Thread.Sleep(1000); _dispatch.BeginInvoke((Action)(() => { _pw.provideUpdate(percentComplete); })); }

        public void ProvideUIUpdate(string message, bool fail = false) {
            try
            {
                _logOutput.AppendLine(message);
            }
            catch (Exception e)
            {
                _dispatch.BeginInvoke((Action)(() => { _pw.provideUpdate(e.Message); }));
            }
            _dispatch.BeginInvoke((Action)(() => { _pw.provideUpdate(message, fail); })); }
    }
}
