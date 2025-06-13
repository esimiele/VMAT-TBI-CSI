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
            List<string> theArguments = e.Args.ToList();

            VMAT_TBI.TBIAutoPlanMW mw = new VMAT_TBI.TBIAutoPlanMW(theArguments);
            if (!mw.GetCloseOpenPatientWindowStatus()) mw.Show();
        }
    }
}
