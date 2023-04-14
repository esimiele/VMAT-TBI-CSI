﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;

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

        public List<Tuple<Structure, Structure, string>> CheckStructuresToUnion(StructureSet selectedSS)
        {
            //left structure, right structure, unioned structure name
            List<Tuple<Structure, Structure, string>> structuresToUnion = new List<Tuple<Structure, Structure, string>> { };
            List<Structure> LStructs = selectedSS.Structures.Where(x => x.Id.Substring(x.Id.Length - 2, 2).ToLower() == "_l" || x.Id.Substring(x.Id.Length - 2, 2).ToLower() == " l").ToList();
            List<Structure> RStructs = selectedSS.Structures.Where(x => x.Id.Substring(x.Id.Length - 2, 2).ToLower() == "_r" || x.Id.Substring(x.Id.Length - 2, 2).ToLower() == " r").ToList();
            foreach (Structure itr in LStructs)
            {
                Structure RStruct = RStructs.FirstOrDefault(x => x.Id.Substring(0, x.Id.Length - 2) == itr.Id.Substring(0, itr.Id.Length - 2));
                string newName = AddProperEndingToName(itr.Id.Substring(0, itr.Id.Length - 2).ToLower());
                if (RStruct != null && !selectedSS.Structures.Any(x => string.Equals(x.Id.ToLower(),newName.ToLower()) && !x.IsEmpty))
                {
                    structuresToUnion.Add(new Tuple<Structure, Structure, string>(itr, RStruct, newName));
                }
            }
            return structuresToUnion;
        }

        private string AddProperEndingToName(string initName)
        {
            string unionedName;
            if (initName.Substring(initName.Length - 1, 1) == "y" && initName.Substring(initName.Length - 2, 2) != "ey") unionedName = initName.Substring(0, initName.Length - 1) + "ies";
            else if (initName.Substring(initName.Length - 1, 1) == "s") unionedName = initName + "es";
            else unionedName = initName + "s";
            return unionedName;
        }

        public (bool, StringBuilder) UnionLRStructures(Tuple<Structure, Structure, string> itr, StructureSet selectedSS)
        {
            StringBuilder sb = new StringBuilder();
            Structure newStructure = null;
            string newName = itr.Item3;
            try
            {
                Structure existStructure = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == newName);
                //a structure already exists in the structure set with the intended name
                if (existStructure != null) newStructure = existStructure;
                else newStructure = selectedSS.AddStructure("CONTROL", newName);
                newStructure.SegmentVolume = itr.Item1.Margin(0.0);
                newStructure.SegmentVolume = newStructure.Or(itr.Item2.Margin(0.0));
            }
            catch (Exception except) 
            { 
                sb.Append(String.Format("Warning! Could not add structure: {0}\nBecause: {1}", newName, except.Message)); 
                return (true, sb); 
            }
            return (false, sb);
        }
    }
}
