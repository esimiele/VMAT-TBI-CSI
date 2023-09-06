using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.Enums;
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
        public static List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> UpdateOptimizationConstraints(List<Tuple<string, List<Tuple<string, string>>>> tsTargets,
                                                                                                                                             List<Tuple<string, string, int, DoseValue, double>> prescriptions,
                                                                                                                                             object selectedTemplate,
                                                                                                                                             List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> currentList = null)
        {
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> updatedList = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };
            if (!currentList.Any()) currentList = RetrieveOptConstraintsFromTemplate(selectedTemplate, prescriptions).Item1;
            if (currentList.Any())
            {
                string tmpPlanId = tsTargets.First().Item1;
                List<Tuple<string, string>> tmpTSTargetListForPlan = tsTargets.First().Item2;
                List<Tuple<string, OptimizationObjectiveType, double, double, int>> tmpList = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
                foreach (Tuple<string,List<Tuple<string, string>>> itr in tsTargets)
                {
                    if (!string.Equals(itr.Item1, tmpPlanId))
                    {
                        //new plan, update the list
                        tmpList.AddRange(currentList.First(x => string.Equals(x.Item1, tmpPlanId)).Item2.Where(y => !tmpTSTargetListForPlan.Any(k => string.Equals(k.Item1, y.Item1))));
                        updatedList.Add(Tuple.Create(tmpPlanId, new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(tmpList)));
                        tmpList = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
                        tmpPlanId = itr.Item1;
                        tmpTSTargetListForPlan = new List<Tuple<string, string>>(itr.Item2);
                    }
                    if (currentList.Any(x => string.Equals(x.Item1, tmpPlanId)))
                    {
                        foreach(Tuple<string,string> itr1 in itr.Item2)
                        {
                            //grab all optimization constraints from the plan of interest that have the same structure id as item 2 of itr
                            List<Tuple<string, OptimizationObjectiveType, double, double, int>> planOptList = currentList.First(x => string.Equals(x.Item1, tmpPlanId)).Item2.Where(y => string.Equals(y.Item1, itr1.Item1)).ToList();
                            foreach (Tuple<string, OptimizationObjectiveType, double, double, int> itr2 in planOptList)
                            {
                                //simple copy of constraints
                                tmpList.Add(Tuple.Create(itr1.Item2, itr2.Item2, itr2.Item3, itr2.Item4, itr2.Item5));
                            }
                        }
                        
                    }
                }
                tmpList.AddRange(currentList.FirstOrDefault(x => string.Equals(x.Item1, tmpPlanId)).Item2.Where(y => !tmpTSTargetListForPlan.Any(k => string.Equals(k.Item1, y.Item1))));
                updatedList.Add(Tuple.Create(tmpPlanId, new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(tmpList)));

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
        public static List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> UpdateOptimizationConstraints(List<Tuple<string, string, List<Tuple<string, string>>>> targetManipulations,
                                                                                                                                             List<Tuple<string, string, int, DoseValue, double>> prescriptions,
                                                                                                                                             object selectedTemplate,
                                                                                                                                             List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> currentList = null)
        {
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> updatedList = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };
            if(!currentList.Any()) currentList = RetrieveOptConstraintsFromTemplate(selectedTemplate, prescriptions).Item1;
            if (currentList.Any())
            {
                string tmpPlanId = targetManipulations.First().Item1;
                string tmpTargetId = targetManipulations.First().Item2;
                List<Tuple<string, OptimizationObjectiveType, double, double, int>> tmpList = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
                foreach (Tuple<string, string, List<Tuple<string, string>>> itr in targetManipulations)
                {
                    if (!string.Equals(itr.Item1, tmpPlanId))
                    {
                        //new plan, update the list
                        tmpList.AddRange(currentList.FirstOrDefault(x => string.Equals(x.Item1, tmpPlanId)).Item2.Where(y => !string.Equals(y.Item1, tmpTargetId)));
                        updatedList.Add(Tuple.Create(tmpPlanId, new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(tmpList)));
                        tmpList = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
                        tmpPlanId = itr.Item1;
                    }
                    if (currentList.Any(x => string.Equals(x.Item1, itr.Item1)))
                    {
                        //grab all optimization constraints from the plan of interest that have the same structure id as item 2 of itr
                        List<Tuple<string, OptimizationObjectiveType, double, double, int>> planOptList = currentList.FirstOrDefault(x => string.Equals(x.Item1, itr.Item1)).Item2.Where(y => string.Equals(y.Item1, itr.Item2)).ToList();
                        foreach (Tuple<string, string> itr1 in itr.Item3)
                        {
                            foreach (Tuple<string, OptimizationObjectiveType, double, double, int> itr2 in planOptList)
                            {
                                if (itr1.Item2.Contains("crop"))
                                {
                                    //simple copy of constraints
                                    tmpList.Add(Tuple.Create(itr1.Item1, itr2.Item2, itr2.Item3, itr2.Item4, itr2.Item5));
                                }
                                else
                                {
                                    //need to reduce upper and lower constraints
                                    tmpList.Add(Tuple.Create(itr1.Item1, itr2.Item2, itr2.Item3 * 0.95, itr2.Item4, itr2.Item5));
                                }
                            }
                        }
                    }
                    tmpTargetId = itr.Item2;
                }
                tmpList.AddRange(currentList.FirstOrDefault(x => string.Equals(x.Item1, tmpPlanId)).Item2.Where(y => !string.Equals(y.Item1, tmpTargetId)));
                updatedList.Add(Tuple.Create(tmpPlanId, new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(tmpList)));
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
        public static List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> UpdateOptimizationConstraints(List<Tuple<string, string, double>> addedRings,
                                                                                                                                      List<Tuple<string, string, int, DoseValue, double>> prescriptions,
                                                                                                                                      object selectedTemplate,
                                                                                                                                      List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> currentList = null)
        {
            if (!currentList.Any()) currentList = RetrieveOptConstraintsFromTemplate(selectedTemplate, prescriptions).Item1;
            if (currentList.Any())
            {
                foreach (Tuple<string, string, double> itr in addedRings)
                {
                    string planId = TargetsHelper.GetPlanIdFromTargetId(itr.Item1, prescriptions);
                    if (currentList.Any(x => string.Equals(x.Item1, planId)))
                    {
                        Tuple<string, OptimizationObjectiveType, double, double, int> ringConstraint = Tuple.Create(itr.Item2, OptimizationObjectiveType.Upper, itr.Item3, 0.0, 80);
                        currentList.First(x => string.Equals(x.Item1, planId)).Item2.Add(ringConstraint);
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
        public static List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> InsertTSJnxOptConstraints(List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> list,
                                                                                                                                         List<Tuple<ExternalPlanSetup, List<Structure>>> jnxs,
                                                                                                                                         List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            //we want to insert the optimization constraints for these junction structure right after the ptv constraints, so find the last index of the target ptv structure and insert
            //the junction structure constraints directly after the target structure constraints
            foreach (Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>> itr in list)
            {
                if (jnxs.Any(x => string.Equals(x.Item1.Id.ToLower(), itr.Item1.ToLower())))
                {
                    int index = itr.Item2.FindLastIndex(x => x.Item1.ToLower().Contains("ptv") || x.Item1.ToLower().Contains("ts_overlap"));
                    double rxDose = TargetsHelper.GetHighestRxForPlan(prescriptions, itr.Item1);
                    foreach (Structure itr1 in jnxs.First(x => string.Equals(x.Item1.Id.ToLower(), itr.Item1.ToLower())).Item2)
                    {
                        //per Nataliya's instructions, add both a lower and upper constraint to the junction volumes. Make the constraints match those of the ptv target
                        itr.Item2.Insert(++index, new Tuple<string, OptimizationObjectiveType, double, double, int>(itr1.Id, OptimizationObjectiveType.Lower, rxDose, 100.0, 100));
                        itr.Item2.Insert(++index, new Tuple<string, OptimizationObjectiveType, double, double, int>(itr1.Id, OptimizationObjectiveType.Upper, rxDose * 1.01, 0.0, 100));
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
        public static (List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>, StringBuilder) RetrieveOptConstraintsFromTemplate(object selectedTemplate, List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            StringBuilder sb = new StringBuilder();
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> list = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };
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
        public static (List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>, StringBuilder) RetrieveOptConstraintsFromTemplate(object selectedTemplate, List<Tuple<string, double, string>> targets)
        {
            StringBuilder sb = new StringBuilder();
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> list = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };
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
        private static List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> CreateOptimizationConstraintList(object selectedTemplate, List<Tuple<string,string>> planTargets)
        {
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> list = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };
            //no treatment template selected => scale optimization objectives by ratio of entered Rx dose to closest template treatment Rx dose
            if (selectedTemplate != null)
            {
                bool isCSIplan = false;
                if (selectedTemplate is CSIAutoPlanTemplate) isCSIplan = true;
                if (planTargets.Any())
                {
                    if(isCSIplan)
                    {
                        if ((selectedTemplate as CSIAutoPlanTemplate).GetInitOptimizationConstraints().Any()) list.Add(Tuple.Create(planTargets.ElementAt(0).Item1, (selectedTemplate as CSIAutoPlanTemplate).GetInitOptimizationConstraints()));
                        if (planTargets.Count > 1 && (selectedTemplate as CSIAutoPlanTemplate).GetBoostOptimizationConstraints().Any()) list.Add(Tuple.Create(planTargets.ElementAt(1).Item1, (selectedTemplate as CSIAutoPlanTemplate).GetBoostOptimizationConstraints()));
                    }
                    else if ((selectedTemplate as TBIAutoPlanTemplate).GetInitOptimizationConstraints().Any()) list.Add(Tuple.Create(planTargets.ElementAt(0).Item1, (selectedTemplate as TBIAutoPlanTemplate).GetInitOptimizationConstraints()));
                }
                else
                {
                    if (isCSIplan)
                    {
                        if ((selectedTemplate as CSIAutoPlanTemplate).GetInitOptimizationConstraints().Any()) list.Add(Tuple.Create("CSI-init", (selectedTemplate as CSIAutoPlanTemplate).GetInitOptimizationConstraints()));
                        if ((selectedTemplate as CSIAutoPlanTemplate).GetBoostRxDosePerFx() != 0.1 && (selectedTemplate as CSIAutoPlanTemplate).GetBoostOptimizationConstraints().Any()) list.Add(Tuple.Create("CSI-bst", (selectedTemplate as CSIAutoPlanTemplate).GetBoostOptimizationConstraints()));
                    }
                    else if ((selectedTemplate as TBIAutoPlanTemplate).GetInitOptimizationConstraints().Any()) list.Add(Tuple.Create("VMAT-TBI", (selectedTemplate as TBIAutoPlanTemplate).GetInitOptimizationConstraints()));
                }
            }
            return list;
        }
    }
}
