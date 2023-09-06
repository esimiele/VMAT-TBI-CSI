using System.Collections.Generic;
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
            List<string> theArguments = new List<string> { };
            for (int i = 0; i < e.Args.Length; i++) theArguments.Add(e.Args[i]);

            VMAT_TBI.TBIAutoPlanMW mw = new VMAT_TBI.TBIAutoPlanMW(theArguments);
            if (!mw.GetCloseOpenPatientWindowStatus()) mw.Show();
        }
    }
}
