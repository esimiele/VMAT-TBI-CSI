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
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context)
        {
            try
            {
                if (context.Patient != null)
                {
                    bool addOptLaunchOption = false;
                    bool isCSIPlan = false;
                    ExternalPlanSetup plan = context.ExternalPlanSetup;
                    if (plan != null && (plan.Id.ToLower().Contains("tbi") || plan.Id.ToLower().Contains("csi") && !plan.IsDoseValid))
                    {
                        addOptLaunchOption = true;
                        if (plan.Id.ToLower().Contains("csi")) isCSIPlan = true;
                    }
                    else
                    {
                        List<Course> courses = context.Patient.Courses.Where(x => x.Id.ToLower().Contains("tbi") || x.Id.ToLower().Contains("csi")).ToList();
                        if (courses.Any())
                        {
                            foreach (Course c in courses)
                            {
                                if (c.ExternalPlanSetups.Where(x => (x.Id.ToLower().Contains("tbi") || x.Id.ToLower().Contains("csi")) && !x.IsDoseValid).Any())
                                {
                                    if (c.ExternalPlanSetups.Where(x => x.Id.ToLower().Contains("csi")).Any()) isCSIPlan = true;
                                    addOptLaunchOption = true;
                                    break;
                                }
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
                            if (!addOptLaunchOption) p.Arguments = String.Format("{0} {1}", context.Patient.Id, context.StructureSet.Id);
                            else
                            {
                                if(isCSIPlan) p.Arguments = String.Format("{0} {1} {2} {3}", context.Patient.Id, context.StructureSet.Id, "true", "true");
                                else p.Arguments = String.Format("{0} {1} {2}", context.Patient.Id, context.StructureSet.Id, "true");
                            }
                        }
                        Process.Start(p);
                    }
                    else MessageBox.Show(String.Format("Error! {0} executable NOT found!", exeName));
                }
                else MessageBox.Show("Please open a patient before launching the autoplanning tool!");
            }
            catch (Exception e) { MessageBox.Show(e.Message); };
        }

        private string AppExePath(string exeName)
        {
            return FirstExePathIn(Path.GetDirectoryName(GetSourceFilePath()), exeName);
        }

        private string FirstExePathIn(string dir, string exeName)
        {
            return Directory.GetFiles(dir, "*.exe").FirstOrDefault(x => x.Contains(exeName));
        }

        private string GetSourceFilePath([CallerFilePath] string sourceFilePath = "")
        {
            return sourceFilePath;
        }
    }
}
