using System;
using System.Threading;
using System.Windows.Threading;

namespace OptimizationProgressWindow
{
    //separate class to help facilitate multithreading
    public class ESAPIWorker
    {
        public bool isError = false;
        public readonly Dispatcher _dispatcher;

        /// <summary>
        /// Constructor
        /// </summary>
        public ESAPIWorker()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        /// <summary>
        /// Method to asynchronously execute the supplied task on the MAIN thread
        /// </summary>
        /// <param name="a"></param>
        public void DoWork(Action a)
        {
            _dispatcher.BeginInvoke(a);
        }

        /// <summary>
        /// Method to create a new thread, set the apartment state, set the new thread to be a background thread, 
        /// and execute the action supplied to this method
        /// </summary>
        /// <param name="a"></param>
        public void RunOnNewThread(Action a)
        {
            Thread t = new Thread(() => a());
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }
    }
}
