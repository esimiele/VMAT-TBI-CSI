using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace VMATTBIAutoPlanMT
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            string[] startupArgs = e.Args;
            //startupArgs = new string[] { "-m", "$TBIDryRun_2", "-s", "TBIDryRun_2" };
            VMAT_TBI.TBIAutoPlanMW mw = new VMAT_TBI.TBIAutoPlanMW(startupArgs.ToList());
            if (!mw.GetCloseOpenPatientWindowStatus()) mw.Show();
        }
    }
}
