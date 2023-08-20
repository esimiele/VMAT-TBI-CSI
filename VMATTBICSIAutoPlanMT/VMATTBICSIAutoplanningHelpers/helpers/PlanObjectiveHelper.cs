using System;
using System.Collections.Generic;
using System.Linq;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class PlanObjectiveHelper
    {
        /// <summary>
        /// Helper method to take the supplied plan objectives and replace any targets with their corresponding TS targets
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="selectedSS"></param>
        /// <param name="tsTargets"></param>
        /// <returns></returns>
        public static List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> ConstructPlanObjectives(List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> obj,
                                                                                                                              StructureSet selectedSS,
                                                                                                                              List<Tuple<string,string>> tsTargets)
        {
            List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> tmp = new List<Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation>> { };
            if (selectedSS != null)
            {
                foreach (Tuple<string, OptimizationObjectiveType, double, double, DoseValuePresentation> itr in obj)
                {
                    string volume = itr.Item1;
                    if (tsTargets.Any(x => string.Equals(x.Item1, itr.Item1)))
                    {
                        //volume is a target and has a corresponding ts target
                        //update volume with ts target id
                        volume = tsTargets.First(x => string.Equals(x.Item1, itr.Item1)).Item2;
                    }
                    if (StructureTuningHelper.DoesStructureExistInSS(volume, selectedSS, true))
                    {
                        tmp.Add(Tuple.Create(volume, itr.Item2, itr.Item3, itr.Item4, itr.Item5));
                    }
                }
            }
            return tmp;
        }
    }
}
