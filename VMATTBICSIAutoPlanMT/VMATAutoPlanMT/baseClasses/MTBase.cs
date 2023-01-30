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
        public Dispatcher dispatch;
        public MTProgress pw;
        public void MTBase()
        { 
        }
        public void SetDispatcherAndUIInstance(Dispatcher d, MTProgress p)
        {
            dispatch = d;
            pw = p;
            //ProvideUIUpdate("hello");
        }

        public virtual bool Run()
        {
            return false;
        }
    }
}
