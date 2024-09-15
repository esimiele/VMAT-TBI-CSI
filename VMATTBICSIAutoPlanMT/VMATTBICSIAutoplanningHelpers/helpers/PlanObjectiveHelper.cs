using System;
using System.Collections.Generic;
using System.Linq;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Models;
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
        public static List<PlanObjectiveModel> ConstructPlanObjectives(List<PlanObjectiveModel> obj,
                                                                  StructureSet selectedSS,
                                                                  Dictionary<string,string> tsTargets)
        {
            List<PlanObjectiveModel> tmp = new List<PlanObjectiveModel> { };
            if (selectedSS != null)
            {
                foreach (PlanObjectiveModel itr in obj)
                {
                    string volume = itr.StructureId;
                    if (tsTargets.Any(x => string.Equals(x.Key, itr.StructureId)))
                    {
                        //volume is a target and has a corresponding ts target
                        //update volume with ts target id
                        volume = tsTargets.First(x => string.Equals(x.Key, itr.StructureId)).Value;
                    }
                    if (StructureTuningHelper.DoesStructureExistInSS(volume, selectedSS, true))
                    {
                        tmp.Add(new PlanObjectiveModel(volume, itr.ConstraintType, itr.QueryDose, itr.QueryDoseUnits, itr.QueryVolume, Units.Percent));
                    }
                }
            }
            return tmp;
        }
    }
}
