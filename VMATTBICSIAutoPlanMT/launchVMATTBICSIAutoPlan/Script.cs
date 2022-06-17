using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using System.Diagnostics;
using System.Windows.Forms;
using System.IO;
using System.Reflection;

namespace VMS.TPS
{
    public class Script
    {
        public void Execute(ScriptContext context)
        {
            try
            {
                string configFile = "";
                launchVMATTBIAutoPlan.esapi.selectOption SO = new launchVMATTBIAutoPlan.esapi.selectOption();
                SO.ShowDialog();
                string extension = "";
                string exeName = "";
                if (SO.isVMATTBI) { extension = "\\configuration\\VMAT_TBI_config.ini"; exeName = "VMATTBIAutoPlanMT"; }
                else if (SO.isVMATCSI) { extension = "\\configuration\\VMAT_CSI_config.ini"; exeName = "VMATCSIAutoPlanMT"; }
                else return;
                string path = AppExePath(exeName);
                if (!string.IsNullOrEmpty(path))
                {
                    ProcessStartInfo p = new ProcessStartInfo(path);
                    if (File.Exists(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + extension)) configFile = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + extension;
                    if (context.Patient != null) p.Arguments = String.Format("{0} {1} {2}", context.Patient.Id, context.StructureSet.Id, configFile);
                    Process.Start(p);
                }
                else MessageBox.Show(String.Format("Error! {0} executable NOT found!", exeName));
            }
            catch (Exception e) { MessageBox.Show(e.Message); };
        }
        private string AppExePath(string exeName)
        {
            return FirstExePathIn(AssemblyDirectory(), exeName);
        }

        private string FirstExePathIn(string dir, string exeName)
        {
            return Directory.GetFiles(dir, "*.exe").FirstOrDefault(x => x.Contains(exeName));
        }

        private string AssemblyDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }
    }
}
