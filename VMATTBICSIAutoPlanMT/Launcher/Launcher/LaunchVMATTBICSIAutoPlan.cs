using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;

namespace VMS.TPS
{
    public class Script
    {
        /// <summary>
        /// Simple method to launch the launcher executable while passing the patient mrn and structure set id
        /// </summary>
        /// <param name="context"></param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context)
        {
            try
            {
                if (context.Patient != null)
                {
                    bool addOptLaunchOption = false;
                    ExternalPlanSetup plan = context.ExternalPlanSetup;
                    if (plan != null && (plan.Id.ToLower().Contains("tbi") || plan.Id.ToLower().Contains("csi")))
                    {
                        addOptLaunchOption = true;
                    }
                    else
                    {
                        List<Course> courses = context.Patient.Courses.Where(x => x.Id.ToLower().Contains("tbi") || x.Id.ToLower().Contains("csi")).ToList();
                        if (courses.Any())
                        {
                            if(courses.SelectMany(x => x.ExternalPlanSetups).Any(x => x.Id.ToLower().Contains("tbi") || x.Id.ToLower().Contains("csi")))
                            {
                                addOptLaunchOption = true;
                            }
                        }
                    }
                    string exeName = "Launcher";
                    string path = AppExePath(exeName);
                    if (!string.IsNullOrEmpty(path))
                    {
                        ProcessStartInfo p = new ProcessStartInfo(path);
                        p.Arguments = SerializeEclipseContext(context);
                        if (addOptLaunchOption) p.Arguments += String.Format(" -opt true");
                        else p.Arguments += String.Format(" -opt false");
                        Process.Start(p);
                    }
                    else MessageBox.Show(String.Format("Error! {0} executable NOT found!", exeName));
                }
                else MessageBox.Show("Please open a patient before launching the autoplanning tool!");
            }
            catch (Exception e) { MessageBox.Show(e.Message); };
        }

        /// <summary>
        /// Retrieve the full file name of the executable
        /// </summary>
        /// <param name="exeName"></param>
        /// <returns></returns>
        private string AppExePath(string exeName)
        {
            return FirstExePathIn(Path.GetDirectoryName(GetSourceFilePath()) + @"\VMAT-TBI-CSI\", exeName);
        }

        /// <summary>
        /// Return the first identified executable in the supplied directory that has a name matching the supplied name
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="exeName"></param>
        /// <returns></returns>
        private string FirstExePathIn(string dir, string exeName)
        {
            return Directory.GetFiles(dir, "*.exe").FirstOrDefault(x => x.Contains(exeName));
        }

        /// <summary>
        /// Clever trick to return the full path of the currently executing file
        /// </summary>
        /// <param name="sourceFilePath"></param>
        /// <returns></returns>
        private string GetSourceFilePath([CallerFilePath] string sourceFilePath = "")
        {
            return sourceFilePath;
        }

        private string SerializeEclipseContext(ScriptContext context)
        {
            string serializedContext = "";
            if (context != null)
            {
                if (context.Patient != null) serializedContext += string.Format("-m {0}", context.Patient.Id);
                if (context.StructureSet != null) serializedContext += string.Format(" -s {0}", context.StructureSet.Id);
            }
            return serializedContext;
        }
    }
}
