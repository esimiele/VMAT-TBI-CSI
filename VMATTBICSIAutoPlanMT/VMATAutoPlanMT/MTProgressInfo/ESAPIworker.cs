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

        //data structure to hold all this crap
        public struct dataContainer
        {
            //data members
            //DICOM types
            //Possible values are "AVOIDANCE", "CAVITY", "CONTRAST_AGENT", "CTV", "EXTERNAL", "GTV", "IRRAD_VOLUME", 
            //"ORGAN", "PTV", "TREATED_VOLUME", "SUPPORT", "FIXATION", "CONTROL", and "DOSE_REGION". 
            public List<Tuple<string, string, double>> spareStructList;
            public List<Tuple<string, string>> TS_structures;
            public List<Tuple<string, double, string>> targets;
            public List<Tuple<string, string, int, DoseValue, double>> prescriptions;
            public int numIsos;
            public int numVMATIsos;
            public StructureSet selectedSS;

            //simple method to automatically assign/initialize the above data members
            public void construct(List<Tuple<string, string>> ts, List<Tuple<string, string, double>> list, List<Tuple<string, double, string>> targs, List<Tuple<string, string, int, DoseValue, double>> presc, StructureSet ss)
            {
                TS_structures = new List<Tuple<string, string>>(ts);
                spareStructList = new List<Tuple<string, string, double>>(list);
                targets = new List<Tuple<string, double, string>>(targs);
                prescriptions = new List<Tuple<string, string, int, DoseValue, double>>(presc);
                selectedSS = ss;
            }

            public void construct()
            {

            }
        }

        //instance of dataContainer structure to copy the optimization parameters to thread-local memory
        public dataContainer data;
        public readonly Dispatcher _dispatcher;

        //constructor
        public ESAPIworker(dataContainer d)
        {
            //copy optimization parameters from main thread to new thread
            data = d;
            //copy the dispatcher assigned to the main thread (the optimization loop will run on the main thread)
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        //asynchronously execute the supplied task on the main thread
        public void DoWork(Action<dataContainer> a)
        {
            _dispatcher.BeginInvoke(a, data);
        }
    }
}
