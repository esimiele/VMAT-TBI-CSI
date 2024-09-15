using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMATTBICSIAutoPlanningHelpers.Models;
using VMS.TPS.Common.Model.API;
using TSManipulationType = VMATTBICSIAutoPlanningHelpers.Enums.TSManipulationType;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class StructureTuningHelper
    {
        /// <summary>
        /// Helper method to easily add tuning structure manipulations to the final list that will be used in the GenerateTS classes
        /// </summary>
        /// <param name="caseSpareStruct"></param>
        /// <param name="template"></param>
        /// <param name="sex"></param>
        /// <returns></returns>
        public static List<RequestedTSManipulationModel> AddTemplateSpecificStructureManipulations(List<RequestedTSManipulationModel> templateManipulationList, List<RequestedTSManipulationModel> manipulationListToUpdate, string sex)
        {
            foreach (RequestedTSManipulationModel s in templateManipulationList)
            {
                if (string.Equals(s.StructureId.ToLower(), "ovaries") || string.Equals(s.StructureId.ToLower(), "testes"))
                {
                    if ((sex == "Female" && s.StructureId.ToLower() == "ovaries") || (sex == "Male" && s.StructureId.ToLower() == "testes"))
                    {
                        manipulationListToUpdate.Add(s);
                    }
                }
                else manipulationListToUpdate.Add(s);
            }
            return manipulationListToUpdate;
        }

        /// <summary>
        /// Helper method to look through the structure set and identify a list of left and right structures that should be unioned together
        /// </summary>
        /// <param name="selectedSS"></param>
        /// <returns></returns>
        public static List<UnionStructureModel> CheckStructuresToUnion(StructureSet selectedSS)
        {
            //left structure, right structure, unioned structure name
            List<UnionStructureModel> structuresToUnion = new List<UnionStructureModel> { };
            List<Structure> LStructs = selectedSS.Structures.Where(x => !x.IsEmpty && (x.Id.Substring(x.Id.Length - 2, 2).ToLower() == "_l" || x.Id.Substring(x.Id.Length - 2, 2).ToLower() == " l")).ToList();
            List<Structure> RStructs = selectedSS.Structures.Where(x => !x.IsEmpty && (x.Id.Substring(x.Id.Length - 2, 2).ToLower() == "_r" || x.Id.Substring(x.Id.Length - 2, 2).ToLower() == " r")).ToList();
            foreach (Structure itr in LStructs)
            {
                Structure RStruct = RStructs.FirstOrDefault(x => x.Id.Substring(0, x.Id.Length - 2) == itr.Id.Substring(0, itr.Id.Length - 2));
                string newName = AddProperEndingToName(itr.Id.Substring(0, itr.Id.Length - 2).ToLower());
                if (!ReferenceEquals(RStruct,null) && !selectedSS.Structures.Any(x => string.Equals(x.Id.ToLower(),newName.ToLower()) && !x.IsEmpty))
                {
                    structuresToUnion.Add(new UnionStructureModel(itr, RStruct, newName));
                }
            }
            return structuresToUnion;
        }

        /// <summary>
        /// Simple helper method to add the proper ending to the unioned structure name
        /// </summary>
        /// <param name="initName"></param>
        /// <returns></returns>
        private static string AddProperEndingToName(string initName)
        {
            string unionedName;
            if (initName.Substring(initName.Length - 1, 1) == "y" && initName.Substring(initName.Length - 2, 2) != "ey") unionedName = initName.Substring(0, initName.Length - 1) + "ies";
            else if (initName.Substring(initName.Length - 1, 1) == "s") unionedName = initName + "es";
            else unionedName = initName + "s";
            return unionedName;
        }

        /// <summary>
        /// Utility method to take the supplied left and right structures and union them together
        /// </summary>
        /// <param name="itr"></param>
        /// <param name="selectedSS"></param>
        /// <returns></returns>
        public static (bool, StringBuilder) UnionLRStructures(UnionStructureModel itr, StructureSet selectedSS)
        {
            StringBuilder sb = new StringBuilder();
            Structure newStructure;
            string newName = itr.ProposedUnionStructureId;
            try
            {
                //a structure already exists in the structure set with the intended name
                if (DoesStructureExistInSS(newName, selectedSS)) newStructure = GetStructureFromId(newName, selectedSS);
                else newStructure = selectedSS.AddStructure("CONTROL", newName);
                (bool copyFail, StringBuilder copyMessage) = ContourHelper.CopyStructureOntoStructure(itr.Structure_Left, newStructure);
                if (copyFail) return (true, copyMessage);
                (bool unionFail, StringBuilder unionMessage) = ContourHelper.ContourUnion(itr.Structure_Right, newStructure, 0.0);
                if (unionFail) return (true, unionMessage);
            }
            catch (Exception except) 
            { 
                sb.Append($"Warning! Could not union {itr.Structure_Left.Id} and {itr.Structure_Right.Id} onto {newName}\nBecause: {except.Message}"); 
                return (true, sb); 
            }
            return (false, sb);
        }

        /// <summary>
        /// Super helpful method to return the first structure with Id matching the supplied Id from the structure set
        /// </summary>
        /// <param name="id"></param>
        /// <param name="selectedSS"></param>
        /// <param name="createIfEmpty"></param>
        /// <returns></returns>
        public static Structure GetStructureFromId(string id, StructureSet selectedSS, bool createIfEmpty = false)
        {
            Structure theStructure = null;
            if (DoesStructureExistInSS(id, selectedSS))
            {
                theStructure = selectedSS.Structures.First(x => string.Equals(x.Id.ToLower(), id.ToLower()));
            }
            else if (createIfEmpty)
            {
                if (selectedSS.CanAddStructure("CONTROL", id))
                {
                    theStructure = selectedSS.AddStructure("CONTROL", id);
                }
            }
            return theStructure;
        }

        /// <summary>
        /// Super helpful method to determine if the supplied structur ids exists in the structure set
        /// </summary>
        /// <param name="id"></param>
        /// <param name="selectedSS"></param>
        /// <param name="checkIsEmpty"></param>
        /// <returns></returns>
        public static bool DoesStructureExistInSS(string id, StructureSet selectedSS, bool checkIsEmpty = false)
        {
            if(!checkIsEmpty) return selectedSS.Structures.Any(x => string.Equals(id.ToLower(), x.Id.ToLower()));
            else return selectedSS.Structures.Any(x => string.Equals(id.ToLower(), x.Id.ToLower()) && !x.IsEmpty);
        }

        /// <summary>
        /// Helper method to determine if any of the supplied structure ids exist in the structure set
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="selectedSS"></param>
        /// <param name="checkIsEmpty"></param>
        /// <returns></returns>
        public static bool DoesStructureExistInSS(List<string> ids, StructureSet selectedSS, bool checkIsEmpty = false)
        {
            foreach(string itr in ids)
            {
                if(checkIsEmpty)
                {
                    if (selectedSS.Structures.Any(x => string.Equals(itr.ToLower(), x.Id.ToLower()) && !x.IsEmpty)) return true;
                }
                else
                {
                    if (selectedSS.Structures.Any(x => string.Equals(itr.ToLower(), x.Id.ToLower()))) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Simple method to determine if there is overlap between the supplied target and normal structures
        /// </summary>
        /// <param name="target"></param>
        /// <param name="normal"></param>
        /// <returns></returns>
        public static bool IsOverlap(Structure target, System.Windows.Media.Media3D.Point3DCollection normal)
        {
            return normal.Any(x => target.IsPointInsideSegment(new VMS.TPS.Common.Model.Types.VVector(x.X, x.Y, x.Z)));
        }
    }
}
