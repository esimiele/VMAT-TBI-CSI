using System;
using System.Collections.Generic;
using System.Windows.Controls;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMATTBICSIAutoPlanningHelpers.PlanTemplateClasses;
using VMATTBICSIAutoPlanningHelpers.BaseClasses;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.Prompts;
using System.Linq;

namespace VMATTBICSIAutoPlanningHelpers.UIHelpers
{
    public static class GeneralUIHelper
    {
        /// <summary>
        /// UI utility method to clear a row (stackpanel) of items if the 'clear' button was hit. Clear then entire list if there was only one item in the list
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="sp"></param>
        /// <returns></returns>
        public static bool ClearRow(object sender, StackPanel sp)
        {
            //same deal as the clear sparing structure button (clearStructBtn_click)
            Button btn = (Button)sender;
            int i = 0;
            int k = 0;
            foreach (object obj in sp.Children)
            {
                foreach (object obj1 in ((StackPanel)obj).Children)
                {
                    if (obj1.Equals(btn)) k = i;
                }
                if (k > 0) break;
                i++;
            }

            //clear entire list if there are only two entries (header + 1 real entry)
            if (sp.Children.Count < 3) { return true; }
            else sp.Children.RemoveAt(k);
            return false;
        }

        /// <summary>
        /// Clear entire list of items in UI
        /// </summary>
        /// <param name="theSP"></param>
        public static void ClearList(StackPanel theSP)
        {
            theSP.Children.Clear();
        }

        /// <summary>
        /// Helper method to prompt the user to select a plan template
        /// </summary>
        /// <param name="availableTemplateIds"></param>
        /// <returns></returns>
        public static string PromptUserToSelectPlanTemplate(List<string> availableTemplateIds)
        {
            string selectedTemplateId = "";
            SelectItemPrompt SIP = new SelectItemPrompt("Please select an existing template!", availableTemplateIds);
            SIP.ShowDialog();
            if (SIP.GetSelection()) selectedTemplateId = availableTemplateIds.FirstOrDefault(x => string.Equals(x, SIP.GetSelectedItem()));
            return selectedTemplateId;
        }
    }
}
