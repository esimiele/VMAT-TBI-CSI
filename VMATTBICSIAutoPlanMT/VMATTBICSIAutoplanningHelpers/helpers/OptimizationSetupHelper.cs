using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.Enums;
using VMATTBICSIAutoPlanningHelpers.PlanTemplateClasses;
using System.Text;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class OptimizationSetupHelper
    {
        //for crop/overlap operations with targets
        public static List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> UpdateOptimizationConstraints(List<Tuple<string, string, List<Tuple<string, string>>>> targetManipulations,
                                                                                                                                      List<Tuple<string, string, int, DoseValue, double>> prescriptions,
                                                                                                                                      object selectedTemplate,
                                                                                                                                      List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> currentList = null)
        {
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> updatedList = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };
            if(!currentList.Any()) currentList = RetrieveOptConstraintsFromTemplate(selectedTemplate as CSIAutoPlanTemplate, prescriptions).Item1;
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

        public static List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> UpdateOptimizationConstraints(List<Tuple<string, string, double>> addedRings,
                                                                                                                                      List<Tuple<string, string, int, DoseValue, double>> prescriptions,
                                                                                                                                      object selectedTemplate,
                                                                                                                                      List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> currentList = null)
                        {
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> updatedList = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };
            if (!currentList.Any()) currentList = RetrieveOptConstraintsFromTemplate(selectedTemplate as CSIAutoPlanTemplate, prescriptions).Item1;
            if (currentList.Any())
            {
                //string tmpTargetId = addedRings.First().Item2;
                //string tmpPlanId = new TargetsHelper().GetPlanIdFromTargetId(tmpTargetId, prescriptions);
                //List<Tuple<string, string, double, double, int>> tmpList = new List<Tuple<string, string, double, double, int>> { };
                foreach (Tuple<string, string, double> itr in addedRings)
                {
                    string planId = TargetsHelper.GetPlanIdFromTargetId(itr.Item1, prescriptions);
                    if (currentList.Any(x => string.Equals(x.Item1, planId)))
                    {
                        Tuple<string, OptimizationObjectiveType, double, double, int> ringConstraint = Tuple.Create(itr.Item2, OptimizationObjectiveType.Upper, itr.Item3, 0.0, 80);
                        currentList.FirstOrDefault(x => string.Equals(x.Item1, planId)).Item2.Add(ringConstraint);
                    }
                }
            }
            return currentList;
        }

        //public static List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> UpdateOptimizationConstraints(List<Tuple<string, string>> tsTargets,
        //                                                                                                                              List<Tuple<string, string, int, DoseValue, double>> prescriptions,
        //                                                                                                                              object selectedTemplate,
        //                                                                                                                              List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> currentList = null)
        //{
        //    List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> updatedList = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };
        //    if (!currentList.Any()) currentList = RetrieveOptConstraintsFromTemplate(selectedTemplate as CSIAutoPlanTemplate, prescriptions).Item1;
        //    if (currentList.Any())
        //    {
        //        string tmpPlanId = TargetsHelper.GetPlanIdFromTargetId(tsTargets.First().Item1, prescriptions);
        //        List<Tuple<string, OptimizationObjectiveType, double, double, int>> tmpList = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
        //        foreach (Tuple<string, string> itr in tsTargets)
        //        {
        //            if (!string.Equals(TargetsHelper.GetPlanIdFromTargetId(itr.Item1, prescriptions), tmpPlanId))
        //            {
        //                //new plan, update the list
        //                tmpList.AddRange(currentList.FirstOrDefault(x => string.Equals(x.Item1, tmpPlanId)).Item2.Where(y => !string.Equals(y.Item1, itr.Item1)));
        //                updatedList.Add(Tuple.Create(tmpPlanId, new List<Tuple<string, OptimizationObjectiveType, double, double, int>>(tmpList)));
        //                tmpList = new List<Tuple<string, OptimizationObjectiveType, double, double, int>> { };
        //                tmpPlanId = TargetsHelper.GetPlanIdFromTargetId(itr.Item1, prescriptions);
        //            }
        //            if (currentList.Any(x => string.Equals(x.Item1, tmpPlanId)))
        //            {
        //                //grab all optimization constraints from the plan of interest that have the same structure id as item 2 of itr
        //                List<Tuple<string, OptimizationObjectiveType, double, double, int>> planOptList = currentList.FirstOrDefault(x => string.Equals(x.Item1, tmpPlanId)).Item2.Where(y => string.Equals(y.Item1, itr.Item1)).ToList();
        //                foreach (Tuple<string, OptimizationObjectiveType, double, double, int> itr2 in planOptList)
        //                {
        //                    //simple copy of constraints
        //                    tmpList.Add(Tuple.Create(itr.Item2, itr2.Item2, itr2.Item3, itr2.Item4, itr2.Item5));
        //                }
        //                tmpList.AddRange(currentList.FirstOrDefault(x => string.Equals(x.Item1, tmpPlanId)).Item2.Where(y => !string.Equals(y.Item1, itr.Item1)));
        //            }
        //        }
        //    }
        //    return currentList;
        //}

        public static (List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>, StringBuilder) RetrieveOptConstraintsFromTemplate(CSIAutoPlanTemplate selectedTemplate, List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            StringBuilder sb = new StringBuilder();
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> list = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };
            //no treatment template selected => scale optimization objectives by ratio of entered Rx dose to closest template treatment Rx dose
            if (selectedTemplate != null)
            {
                if (selectedTemplate != null)
                {
                    list = CreateOptimizationConstraintList(selectedTemplate, TargetsHelper.GetPlanTargetList(prescriptions));
                }
            }
            else sb.AppendLine("No template selected!");
            return (list, sb);
        }

        //overload method to accept target list instead of prescription list
        public static (List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>>, StringBuilder) RetrieveOptConstraintsFromTemplate(CSIAutoPlanTemplate selectedTemplate, List<Tuple<string, double, string>> targets)
        {
            StringBuilder sb = new StringBuilder();
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> list = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };
            //no treatment template selected => scale optimization objectives by ratio of entered Rx dose to closest template treatment Rx dose
            if (selectedTemplate != null)
            {
                list = CreateOptimizationConstraintList(selectedTemplate, TargetsHelper.GetPlanTargetList(targets));
            }
            else sb.AppendLine("No template selected!");
            return (list, sb);
        }

        private static List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> CreateOptimizationConstraintList(CSIAutoPlanTemplate selectedTemplate, List<Tuple<string,string>> planTargets)
        {
            List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> list = new List<Tuple<string, List<Tuple<string, OptimizationObjectiveType, double, double, int>>>> { };
            //no treatment template selected => scale optimization objectives by ratio of entered Rx dose to closest template treatment Rx dose
            if (selectedTemplate != null)
            {
                if (planTargets.Any())
                {
                    if (selectedTemplate.GetInitOptimizationConstraints().Any()) list.Add(Tuple.Create(planTargets.ElementAt(0).Item1, selectedTemplate.GetInitOptimizationConstraints()));
                    if (selectedTemplate.GetBoostOptimizationConstraints().Any()) list.Add(Tuple.Create(planTargets.ElementAt(1).Item1, selectedTemplate.GetBoostOptimizationConstraints()));
                }
                else
                {
                    if (selectedTemplate.GetInitOptimizationConstraints().Any()) list.Add(Tuple.Create("CSI-init", selectedTemplate.GetInitOptimizationConstraints()));
                    if (selectedTemplate.GetBoostOptimizationConstraints().Any()) list.Add(Tuple.Create("CSI-bst", selectedTemplate.GetBoostOptimizationConstraints()));
                }
            }
            return list;
        }
    }
}
