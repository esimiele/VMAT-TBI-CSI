using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace VMATTBICSIOptLoopMT
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            string[] startupArgs = e.Args;
            startupArgs = new string[] { "-m", "$TBIDryRun_1", "-s", "TBIDryRun_1", "-p", "1.2.246.352.71.5.251621835082.1766061.20250506062818", "-c", "VMAT-TBI"};
            OptLoopMW mw = new OptLoopMW(startupArgs);
            mw.Show();
        }
    }
}
