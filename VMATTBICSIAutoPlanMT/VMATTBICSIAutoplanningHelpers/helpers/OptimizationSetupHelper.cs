using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.Models;
using VMATTBICSIAutoPlanningHelpers.PlanTemplateClasses;
using System.Text;
using VMS.TPS.Common.Model.API;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class OptimizationSetupHelper
    {
        /// <summary>
        /// Helper method to update the optimization constraint list by replacing the target Ids in the original list with the associated TS target Ids
        /// </summary>
        /// <param name="tsTargets"></param>
        /// <param name="prescriptions"></param>
        /// <param name="selectedTemplate"></param>
        /// <param name="currentList"></param>
        /// <returns></returns>
        public static List<PlanOptimizationSetup> UpdateOptimizationConstraints(List<Tuple<string, Dictionary<string,string>>> tsTargets,
                                                                                                                                             List<Prescription> prescriptions,
                                                                                                                                             object selectedTemplate,
                                                                                                                                             List<PlanOptimizationSetup> currentList = null)
        {
            List<PlanOptimizationSetup> updatedList = new List<PlanOptimizationSetup> { };
            if (!currentList.Any()) currentList = RetrieveOptConstraintsFromTemplate(selectedTemplate, prescriptions).Item1;
            if (currentList.Any())
            {
                string tmpPlanId = tsTargets.First().Item1;
                Dictionary<string,string> tmpTSTargetListForPlan = tsTargets.First().Item2;
                List<OptimizationConstraint> tmpList = new List<OptimizationConstraint> { };
                foreach (Tuple<string,Dictionary<string,string>> itr in tsTargets)
                {
                    if (!string.Equals(itr.Item1, tmpPlanId))
                    {
                        //new plan, update the list
                        tmpList.AddRange(currentList.First(x => string.Equals(x.PlanId, tmpPlanId)).OptimizationConstraints.Where(y => !tmpTSTargetListForPlan.Any(k => string.Equals(k.Key, y.StructureId))));
                        updatedList.Add(new PlanOptimizationSetup(tmpPlanId, new List<OptimizationConstraint>(tmpList)));
                        tmpList = new List<OptimizationConstraint> { };
                        tmpPlanId = itr.Item1;
                        tmpTSTargetListForPlan = new Dictionary<string, string>(itr.Item2);
                    }
                    if (currentList.Any(x => string.Equals(x.PlanId, tmpPlanId)))
                    {
                        foreach(KeyValuePair<string,string> itr1 in itr.Item2)
                        {
                            //grab all optimization constraints from the plan of interest that have the same structure id as item 2 of itr
                            List<OptimizationConstraint> planOptList = currentList.First(x => string.Equals(x.PlanId, tmpPlanId)).OptimizationConstraints.Where(y => string.Equals(y.StructureId, itr1.Key)).ToList();
                            foreach (OptimizationConstraint itr2 in planOptList)
                            {
                                //simple copy of constraints
                                tmpList.Add(new OptimizationConstraint(itr1.Key, itr2.ConstraintType, itr2.QueryDose, Units.cGy, itr2.QueryVolume, itr2.Priority));
                            }
                        }
                        
                    }
                }
                tmpList.AddRange(currentList.FirstOrDefault(x => string.Equals(x.PlanId, tmpPlanId)).OptimizationConstraints.Where(y => !tmpTSTargetListForPlan.Any(k => string.Equals(k.Key, y.StructureId))));
                updatedList.Add(new PlanOptimizationSetup(tmpPlanId, new List<OptimizationConstraint>(tmpList)));

            }
            return updatedList;
        }

        /// <summary>
        /// Helper method to update the optimization constraint list by replacing the target Ids in the original list with the associated crop and overlap TS target Ids
        /// </summary>
        /// <param name="targetManipulations"></param>
        /// <param name="prescriptions"></param>
        /// <param name="selectedTemplate"></param>
        /// <param name="currentList"></param>
        /// <returns></returns>
        public static List<PlanOptimizationSetup> UpdateOptimizationConstraints(List<Tuple<string, string, List<Tuple<string, string>>>> targetManipulations,
                                                                                                                                             List<Prescription> prescriptions,
                                                                                                                                             object selectedTemplate,
                                                                                                                                             List<PlanOptimizationSetup> currentList = null)
        {
            List<PlanOptimizationSetup> updatedList = new List<PlanOptimizationSetup> { };
            if(!currentList.Any()) currentList = RetrieveOptConstraintsFromTemplate(selectedTemplate, prescriptions).Item1;
            if (currentList.Any())
            {
                string tmpPlanId = targetManipulations.First().Item1;
                string tmpTargetId = targetManipulations.First().Item2;
                List<OptimizationConstraint> tmpList = new List<OptimizationConstraint> { };
                foreach (Tuple<string, string, List<Tuple<string, string>>> itr in targetManipulations)
                {
                    if (!string.Equals(itr.Item1, tmpPlanId))
                    {
                        //new plan, update the list
                        tmpList.AddRange(currentList.FirstOrDefault(x => string.Equals(x.PlanId, tmpPlanId)).OptimizationConstraints.Where(y => !string.Equals(y.StructureId, tmpTargetId)));
                        updatedList.Add(new PlanOptimizationSetup(tmpPlanId, new List<OptimizationConstraint>(tmpList)));
                        tmpList = new List<OptimizationConstraint> { };
                        tmpPlanId = itr.Item1;
                    }
                    if (currentList.Any(x => string.Equals(x.PlanId, itr.Item1)))
                    {
                        //grab all optimization constraints from the plan of interest that have the same structure id as item 2 of itr
                        List<OptimizationConstraint> planOptList = currentList.FirstOrDefault(x => string.Equals(x.PlanId, itr.Item1)).OptimizationConstraints.Where(y => string.Equals(y.StructureId, itr.Item2)).ToList();
                        foreach (Tuple<string, string> itr1 in itr.Item3)
                        {
                            foreach (OptimizationConstraint itr2 in planOptList)
                            {
                                if (itr1.Item2.Contains("crop"))
                                {
                                    //simple copy of constraints
                                    tmpList.Add(new OptimizationConstraint(itr1.Item1, itr2.ConstraintType, itr2.QueryDose, Units.cGy, itr2.QueryVolume, itr2.Priority));
                                }
                                else
                                {
                                    //need to reduce upper and lower constraints
                                    tmpList.Add(new OptimizationConstraint(itr1.Item1, itr2.ConstraintType, itr2.QueryDose * 0.95, Units.cGy, itr2.QueryVolume, itr2.Priority));
                                }
                            }
                        }
                    }
                    tmpTargetId = itr.Item2;
                }
                tmpList.AddRange(currentList.FirstOrDefault(x => string.Equals(x.PlanId, tmpPlanId)).OptimizationConstraints.Where(y => !string.Equals(y.StructureId, tmpTargetId)));
                updatedList.Add(new PlanOptimizationSetup(tmpPlanId, new List<OptimizationConstraint>(tmpList)));
            }
            return updatedList;
        }

        /// <summary>
        /// Helper method to update the optimization constraint list by adding optimization constraints for the generated ring structures
        /// </summary>
        /// <param name="addedRings"></param>
        /// <param name="prescriptions"></param>
        /// <param name="selectedTemplate"></param>
        /// <param name="currentList"></param>
        /// <returns></returns>
        public static List<PlanOptimizationSetup> UpdateOptimizationConstraints(List<TSRing> addedRings,
                                                                                                      List<Prescription> prescriptions,
                                                                                                      object selectedTemplate,
                                                                                                      List<PlanOptimizationSetup> currentList = null)
        { 
            if (!currentList.Any()) currentList = RetrieveOptConstraintsFromTemplate(selectedTemplate, prescriptions).Item1;
            if (currentList.Any())
            {
                foreach (TSRing itr in addedRings)
                {
                    string planId = TargetsHelper.GetPlanIdFromTargetId(itr.TargetId, prescriptions);
                    if (currentList.Any(x => string.Equals(x.PlanId, planId)))
                    {
                        OptimizationConstraint ringConstraint = new OptimizationConstraint(itr.RingId, OptimizationObjectiveType.Upper, itr.DoseLevel, Units.cGy, 0.0, 80);
                        currentList.First(x => string.Equals(x.PlanId, planId)).OptimizationConstraints.Add(ringConstraint);
                    }
                }
            }
            return currentList;
        }

        /// <summary>
        /// Helper method to create optimization constraints for the generated junction structures and add them to the optimization constraint list
        /// </summary>
        /// <param name="list"></param>
        /// <param name="jnxs"></param>
        /// <param name="prescriptions"></param>
        /// <returns></returns>
        public static List<PlanOptimizationSetup> InsertTSJnxOptConstraints(List<PlanOptimizationSetup> list,
                                                                                                  List<PlanFieldJunctions> jnxs,
                                                                                                  List<Prescription> prescriptions)
        {
            //we want to insert the optimization constraints for these junction structure right after the ptv constraints, so find the last index of the target ptv structure and insert
            //the junction structure constraints directly after the target structure constraints
            foreach (PlanOptimizationSetup itr in list)
            {
                if (jnxs.Any(x => string.Equals(x.PlanSetup.Id.ToLower(), itr.PlanId.ToLower())))
                {
                    int index = itr.OptimizationConstraints.FindLastIndex(x => x.StructureId.ToLower().Contains("ptv") || x.StructureId.ToLower().Contains("ts_overlap"));
                    double rxDose = TargetsHelper.GetHighestRxForPlan(prescriptions, itr.PlanId);
                    foreach (Structure itr1 in jnxs.First(x => string.Equals(x.PlanSetup.Id.ToLower(), itr.PlanId.ToLower())).FieldJunctionStructures)
                    {
                        //per Nataliya's instructions, add both a lower and upper constraint to the junction volumes. Make the constraints match those of the ptv target
                        itr.OptimizationConstraints.Insert(++index, new OptimizationConstraint(itr1.Id, OptimizationObjectiveType.Lower, rxDose, Units.cGy, 100.0, 100));
                        itr.OptimizationConstraints.Insert(++index, new OptimizationConstraint(itr1.Id, OptimizationObjectiveType.Upper, rxDose * 1.01, Units.cGy, 0.0, 100));
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Helper utility method to retrieve the optimization constraints from the supplied auto plan template object
        /// </summary>
        /// <param name="selectedTemplate"></param>
        /// <param name="prescriptions"></param>
        /// <returns></returns>
        public static (List<PlanOptimizationSetup>, StringBuilder) RetrieveOptConstraintsFromTemplate(object selectedTemplate, List<Prescription> prescriptions)
        {
            StringBuilder sb = new StringBuilder();
            List<PlanOptimizationSetup> list = new List<PlanOptimizationSetup> { };
            //no treatment template selected => scale optimization objectives by ratio of entered Rx dose to closest template treatment Rx dose
            if (selectedTemplate != null)
            {
                list = CreateOptimizationConstraintList(selectedTemplate, TargetsHelper.GetHighestRxPlanTargetList(prescriptions));
            }
            else sb.AppendLine("No template selected!");
            return (list, sb);
        }

        /// <summary>
        /// Overloaded helper method to retrieve the optimization constraints from the supplied auto plan template object
        /// </summary>
        /// <param name="selectedTemplate"></param>
        /// <param name="targets"></param>
        /// <returns></returns>
        public static (List<PlanOptimizationSetup>, StringBuilder) RetrieveOptConstraintsFromTemplate(object selectedTemplate, List<PlanTarget> targets)
        {
            StringBuilder sb = new StringBuilder();
            List<PlanOptimizationSetup> list = new List<PlanOptimizationSetup> { };
            //no treatment template selected => scale optimization objectives by ratio of entered Rx dose to closest template treatment Rx dose
            if (selectedTemplate != null)
            {
                list = CreateOptimizationConstraintList(selectedTemplate, TargetsHelper.GetHighestRxPlanTargetList(targets));
            }
            else sb.AppendLine("No template selected!");
            return (list, sb);
        }

        /// <summary>
        /// Helper method to build the optimization constraint list from the supplied auto plan template and the highest Rx plan, target list
        /// </summary>
        /// <param name="selectedTemplate"></param>
        /// <param name="planTargets"></param>
        /// <returns></returns>
        public static List<PlanOptimizationSetup> CreateOptimizationConstraintList(object selectedTemplate, Dictionary<string,string> planTargets)
        {
            List<PlanOptimizationSetup> list = new List<PlanOptimizationSetup> { };
            //no treatment template selected => scale optimization objectives by ratio of entered Rx dose to closest template treatment Rx dose
            if (selectedTemplate != null)
            {
                bool isCSIplan = false;
                if (selectedTemplate is CSIAutoPlanTemplate) isCSIplan = true;
                if (planTargets.Any())
                {
                    if(isCSIplan)
                    {
                        if ((selectedTemplate as CSIAutoPlanTemplate).InitialOptimizationConstraints.Any()) list.Add(new PlanOptimizationSetup(planTargets.ElementAt(0).Key, (selectedTemplate as CSIAutoPlanTemplate).InitialOptimizationConstraints));
                        if (planTargets.Count > 1 && (selectedTemplate as CSIAutoPlanTemplate).BoostOptimizationConstraints.Any()) list.Add(new PlanOptimizationSetup(planTargets.ElementAt(1).Key, (selectedTemplate as CSIAutoPlanTemplate).BoostOptimizationConstraints));
                    }
                    else if ((selectedTemplate as TBIAutoPlanTemplate).InitialOptimizationConstraints.Any()) list.Add(new PlanOptimizationSetup(planTargets.ElementAt(0).Key, (selectedTemplate as TBIAutoPlanTemplate).InitialOptimizationConstraints));
                }
                else
                {
                    if (isCSIplan)
                    {
                        if ((selectedTemplate as CSIAutoPlanTemplate).InitialOptimizationConstraints.Any()) list.Add(new PlanOptimizationSetup("CSI-init", (selectedTemplate as CSIAutoPlanTemplate).InitialOptimizationConstraints));
                        if ((selectedTemplate as CSIAutoPlanTemplate).BoostRxDosePerFx != 0.1 && (selectedTemplate as CSIAutoPlanTemplate).BoostOptimizationConstraints.Any()) list.Add(new PlanOptimizationSetup("CSI-bst", (selectedTemplate as CSIAutoPlanTemplate).BoostOptimizationConstraints));
                    }
                    else if ((selectedTemplate as TBIAutoPlanTemplate).InitialOptimizationConstraints.Any()) list.Add(new PlanOptimizationSetup("VMAT-TBI", (selectedTemplate as TBIAutoPlanTemplate).InitialOptimizationConstraints));
                }
            }
            return list;
        }
    }
}
