using System.Collections.Generic;
using System.Windows;
using System.IO;
using System.Reflection;
using VMATAutoPlanMT.Prompts;

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
            if (!SO.isVMATCSI && !SO.isVMATTBI) Current.Shutdown();

            Window mw;
            List<string> theArguments = new List<string> { };
            for(int i = 0; i < e.Args.Length; i++) theArguments.Add(e.Args[i]);
            theArguments.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\configuration\\General_configuration.ini");
            if (SO.isVMATTBI) 
            { 
                mw = new VMAT_TBI.TBIAutoPlanMW(theArguments); 
            }
            else if (SO.isVMATCSI) 
            { 
                mw = new VMAT_CSI.CSIAutoPlanMW(theArguments);
            } 
            else return;
            mw.Show();
        }
    }
}
