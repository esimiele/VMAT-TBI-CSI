using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace VMATTBI_optLoopMT
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var args = e.Args;
            VMATTBI_optLoop.MainWindow mw = new VMATTBI_optLoop.MainWindow(e.Args);
            mw.Show();
        }
    }
}
