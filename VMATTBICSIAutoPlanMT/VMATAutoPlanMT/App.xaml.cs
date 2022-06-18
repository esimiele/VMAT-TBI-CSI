using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace VMATAutoPlanMT
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            selectOption SO = new selectOption();
            SO.ShowDialog();
            Window mw;
            if (SO.isVMATTBI) mw = new TBIAutoPlanMW(e.Args);
            else if (SO.isVMATCSI) mw = new CSIAutoPlanMW(e.Args);
            else return;
            mw.Show();
        }
    }
}
