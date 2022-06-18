using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace VMATAutoPlanMT
{
    public class ESAPIworker
    {
        //instance of dataContainer structure to copy the optimization parameters to thread-local memory
        public autoRunData data;
        public readonly Dispatcher _dispatcher;

        //constructor
        public ESAPIworker(autoRunData d)
        {
            //copy optimization parameters from main thread to new thread
            data = d;
            //copy the dispatcher assigned to the main thread (the optimization loop will run on the main thread)
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        //asynchronously execute the supplied task on the main thread
        public void DoWork(Action<autoRunData> a)
        {
            _dispatcher.BeginInvoke(a, data);
        }
    }
}
