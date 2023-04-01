using System;
using System.Threading;
using System.Windows.Threading;

namespace VMATTBICSIAutoplanningHelpers.MTWorker
{
    //separate class to help facilitate multithreading
    public class ESAPIworker
    {
        public bool isError = false;
        public readonly Dispatcher _dispatcher;

        //constructor
        public ESAPIworker()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        //asynchronously execute the supplied task on the MAIN thread
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
