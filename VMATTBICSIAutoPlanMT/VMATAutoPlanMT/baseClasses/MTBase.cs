using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using VMATAutoPlanMT.MTProgressInfo;

namespace VMATAutoPlanMT.baseClasses
{
    public class MTbase
    {
        private Dispatcher _dispatch;
        private MTProgress _pw;
        public void MTBase()
        { 
        }
        public void SetDispatcherAndUIInstance(Dispatcher d, MTProgress p)
        {
            _dispatch = d;
            _pw = p;
        }

        public virtual bool Run()
        {
            return false;
        }

        public void UpdateUILabel(string message) { _dispatch.BeginInvoke((Action)(() => { _pw.UpdateLabel(message); })); }

        public void ProvideUIUpdate(int percentComplete, string message, bool fail = false) { _dispatch.BeginInvoke((Action)(() => { _pw.provideUpdate(percentComplete, message, fail); })); }

        public void ProvideUIUpdate(int percentComplete) { _dispatch.BeginInvoke((Action)(() => { _pw.provideUpdate(percentComplete); })); }

        public void ProvideUIUpdate(string message, bool fail = false) { _dispatch.BeginInvoke((Action)(() => { _pw.provideUpdate(message, fail); })); }
    }
}
