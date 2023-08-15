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
                        if (context.Patient != null)
                        {
                            if (!addOptLaunchOption) p.Arguments = $"{context.Patient.Id} {context.StructureSet.Id}";
                            else p.Arguments = $"{context.Patient.Id} {context.StructureSet.Id} {true}";
                        }
                        Process.Start(p);
                    }
                    else MessageBox.Show($"Error! {exeName} executable NOT found!");
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
            return FirstExePathIn(Path.GetDirectoryName(GetSourceFilePath()), exeName);
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
    }
}
