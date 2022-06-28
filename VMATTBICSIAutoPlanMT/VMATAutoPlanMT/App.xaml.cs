using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.IO;
using System.Reflection;

namespace VMATAutoPlanMT
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            selectOption SO;

            if (e.Args.Length == 3) SO = new selectOption(true);
            else SO = new selectOption(false);
            SO.ShowDialog();
            Window mw;
            List<string> theArguments = new List<string> { e.Args[0], e.Args[1]};
            if (SO.isVMATTBI) 
            { 
                theArguments.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location + "\\configuration\\VMAT_TBI_config.ini")); 
                mw = new TBIAutoPlanMW(theArguments); 
            }
            else if (SO.isVMATCSI) 
            { 
                theArguments.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\VMAT_CSI_config.ini"); 
                mw = new CSIAutoPlanMW(theArguments); 
            } 
            else return;
            mw.Show();
        }
    }
}
