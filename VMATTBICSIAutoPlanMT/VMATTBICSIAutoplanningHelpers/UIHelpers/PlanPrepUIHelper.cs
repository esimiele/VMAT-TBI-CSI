using System.Linq;
using System.Text;
using VMATTBICSIAutoPlanningHelpers.Logging;
using VMATTBICSIAutoPlanningHelpers.Prompts;
using VMS.TPS.Common.Model.API;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class PlanPrepUIHelper
    {
        /// <summary>
        /// Helper method to retrieve the VMAT plan that should be prepared for treatment
        /// </summary>
        /// <param name="pi"></param>
        /// <param name="logPath"></param>
        /// <param name="courseId"></param>
        /// <returns></returns>
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
                    if (theCourse.ExternalPlanSetups.Where(x => !x.Id.ToLower().Contains("legs")).Count() > 1)
                    {
                        thePlan = PromptForUserToSelectPlan(theCourse);
                        if (ReferenceEquals(thePlan, null)) return (thePlan, sb.AppendLine("No plan selected. Exiting"));
                    }
                    else thePlan = tmp;
                }
            }
            if (thePlan == null)
            {
                if (pi.Courses.Any(x => string.Equals(x.Id.ToLower(), courseId.ToLower())))
                {
                    Course theCourse = pi.Courses.FirstOrDefault(x => string.Equals(x.Id.ToLower(), courseId.ToLower()));
                    if (theCourse.ExternalPlanSetups.Where(x => !x.Id.ToLower().Contains("legs")).Count() > 1)
                    {
                        thePlan = PromptForUserToSelectPlan(theCourse);
                        if (ReferenceEquals(thePlan, null)) return (thePlan, sb.AppendLine("No plan selected. Exiting"));
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

        /// <summary>
        /// Helper method to prompt the user to select a VMAT plan that should be prepared for treatment
        /// </summary>
        /// <param name="theCourse"></param>
        /// <returns></returns>
        public static ExternalPlanSetup PromptForUserToSelectPlan(Course theCourse)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Multiple plans found in {theCourse.Id} course!");
            sb.AppendLine("Please select a plan to prep!");
            SelectItemPrompt SIP = new SelectItemPrompt(sb.ToString(), theCourse.ExternalPlanSetups.Where(x => x.Beams.Any(y => !y.IsSetupField) && !x.Id.ToLower().Contains("leg")).Select(x => x.Id).ToList());
            SIP.ShowDialog();
            if (!SIP.GetSelection()) return null;
            //get the plan the user chose from the combobox
            return theCourse.ExternalPlanSetups.FirstOrDefault(x => string.Equals(x.Id, SIP.GetSelectedItem()));
        }

        /// <summary>
        /// Simple helper method to check if flash structures were added to the structure
        /// </summary>
        /// <param name="ss"></param>
        /// <returns></returns>
        public static bool CheckForFlash(StructureSet ss)
        {
            //look in the structure set to see if any of the structures contain the string 'flash'. If so, return true indicating flash was included in this plan
            if (ss.Structures.Any(x => x.Id.ToLower().Contains("flash") && !x.IsEmpty)) return true;
            return false;
        }
    }
}
