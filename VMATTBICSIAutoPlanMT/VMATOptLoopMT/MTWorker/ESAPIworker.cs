using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace VMATTBICSIOptLoopMT.MTWorker
{
    //separate class to help facilitate multithreading
    public class ESAPIworker
    {
        public bool isError = false;
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

        //method to create the new thread, set the apartment state, set the new thread to be a background thread, and execute the action supplied to this method
        public void RunOnNewThread(Action a)
        {
            Thread t = new Thread(() => a());
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }
    }
}
