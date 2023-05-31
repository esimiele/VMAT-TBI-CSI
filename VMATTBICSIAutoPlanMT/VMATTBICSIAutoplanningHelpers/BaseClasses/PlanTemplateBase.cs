using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMATTBICSIAutoPlanningHelpers.BaseClasses
{
    public class PlanTemplateBase
    {
        //this is only here for the display name data binding. All other references to the template name use the explicit get method

        public string TemplateName { get { return templateName; } }
        public string GetTemplateName() { return templateName; }

        protected string templateName;
    }
}
