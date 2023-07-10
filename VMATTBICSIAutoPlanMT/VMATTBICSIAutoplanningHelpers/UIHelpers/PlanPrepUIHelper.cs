using System;
using System.Linq;
using System.Text;
using VMATTBICSIAutoPlanningHelpers.Logging;
using VMATTBICSIAutoPlanningHelpers.Prompts;
using VMS.TPS.Common.Model.API;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class PlanPrepUIHelper
    {
        public static (ExternalPlanSetup, StringBuilder) RetrieveVMATPlan(Patient pi, string logPath, string courseId)
        {
            ExternalPlanSetup thePlan = null;
            StringBuilder sb = new StringBuilder();
            string fullLogName = LogHelper.GetFullLogFileFromExistingMRN(pi.Id, logPath);
            if (!string.IsNullOrEmpty(fullLogName))
            {
                (string initPlanUID, StringBuilder errorMessage) = LogHelper.LoadVMATPlanUIDFromLogFile(fullLogName);
                if (string.IsNullOrEmpty(initPlanUID))
                {
                    sb.Append(errorMessage);
                    return (thePlan, sb);
                }
                ExternalPlanSetup tmp = pi.Courses.SelectMany(x => x.ExternalPlanSetups).FirstOrDefault(x => string.Equals(x.UID, initPlanUID));
                if (tmp != null)
                {
                    Course theCourse = tmp.Course;
                    if (theCourse.ExternalPlanSetups.Count() > 1)
                    {
                        thePlan = PromptForUserToSelectPlan(theCourse);
                    }
                    else thePlan = tmp;
                }
            }
            if (thePlan == null)
            {
                if (pi.Courses.Any(x => string.Equals(x.Id.ToLower(), courseId.ToLower())))
                {
                    Course theCourse = pi.Courses.FirstOrDefault(x => string.Equals(x.Id.ToLower(), courseId.ToLower()));
                    if (theCourse.ExternalPlanSetups.Count() > 1)
                    {
                        thePlan = PlanPrepUIHelper.PromptForUserToSelectPlan(theCourse);
                    }
                    else thePlan = theCourse.ExternalPlanSetups.First();
                }
                else
                {
                    sb.AppendLine($"Error! No log file found and no course named {courseId} found! Nothing to prep! Exiting!");
                }
            }
            return (thePlan, sb);
        }

        public static ExternalPlanSetup PromptForUserToSelectPlan(Course theCourse)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Multiple plans found in {theCourse.Id} course!");
            sb.AppendLine("Please select a plan to prep!");
            SelectItemPrompt SIP = new SelectItemPrompt(sb.ToString(), theCourse.ExternalPlanSetups.Where(x => !x.Id.ToLower().Contains("legs")).Select(x => x.Id).ToList());
            SIP.ShowDialog();
            if (!SIP.GetSelection()) return null;
            //get the plan the user chose from the combobox
            return theCourse.ExternalPlanSetups.FirstOrDefault(x => string.Equals(x.Id, SIP.GetSelectedItem()));
        }

        public static bool CheckForFlash(StructureSet ss)
        {
            //look in the structure set to see if any of the structures contain the string 'flash'. If so, return true indicating flash was included in this plan
            if (ss.Structures.Any(x => x.Id.ToLower().Contains("flash") && !x.IsEmpty)) return true;
            return false;
        }
    }
}
