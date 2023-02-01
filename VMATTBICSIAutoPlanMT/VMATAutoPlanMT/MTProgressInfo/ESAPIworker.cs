using System;
using System.Collections.Generic;
using System.Windows.Threading;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Threading;

namespace VMATAutoPlanMT.MTProgressInfo
{
    public class ESAPIworker
    {
        public void RunOnNewThread(Action a)
        {
            Thread t = new Thread(() => a());
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }

        //instance of dataContainer structure to copy the optimization parameters to thread-local memory
        public readonly Dispatcher _dispatcher;

        //constructor
        public ESAPIworker()
        {
            //copy optimization parameters from main thread to new thread
            //copy the dispatcher assigned to the main thread (the optimization loop will run on the main thread)
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        //asynchronously execute the supplied task on the main thread
        public void DoWork(Action a)
        {
            _dispatcher.BeginInvoke(a);
        }
    }
}
