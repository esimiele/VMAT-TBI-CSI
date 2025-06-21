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
            //startupArgs = new string[] { "-m", "$TBIDryRun_1", "-s", "TBIDryRun_1" };
            //startupArgs = new string[] { "-m", "$CSIDryRun_3", "-s", "C230822_CSI" };
            OptLoopMW mw = new OptLoopMW(startupArgs);
            mw.Show();
        }
    }
}
