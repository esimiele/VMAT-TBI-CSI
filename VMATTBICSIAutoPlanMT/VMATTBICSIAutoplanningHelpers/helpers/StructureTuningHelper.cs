using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMATTBICSIAutoplanningHelpers.Helpers
{
    public class StructureTuningHelper
    {
        //helper method to easily add sparing structures to a sparing structure list. The reason this is its own method is because of the logic used to include/remove sex-specific organs
        public List<Tuple<string, string, double>> AddTemplateSpecificStructureManipulations(List<Tuple<string, string, double>> caseSpareStruct, List<Tuple<string, string, double>> template, string sex)
        {
            foreach (Tuple<string, string, double> s in caseSpareStruct)
            {
                if (s.Item1.ToLower() == "ovaries" || s.Item1.ToLower() == "testes")
                {
                    if ((sex == "Female" && s.Item1.ToLower() == "ovaries") || (sex == "Male" && s.Item1.ToLower() == "testes"))
                    {
                        template.Add(s);
                    }
                }
                else template.Add(s);
            }
            return template;
        }
    }
}
