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
        public static List<PlanOptimizationSetupModel> UpdateOptimizationConstraints(List<PlanTargetsModel> tsTargets,
                                                                                List<PrescriptionModel> prescriptions,
                                                                                object selectedTemplate,
                                                                                List<PlanOptimizationSetupModel> currentList = null)
        {
            List<PlanOptimizationSetupModel> updatedList = new List<PlanOptimizationSetupModel> { };
            if (!currentList.Any()) currentList = RetrieveOptConstraintsFromTemplate(selectedTemplate, prescriptions).Item1;
            if (currentList.Any())
            {
                string tmpPlanId = tsTargets.First().PlanId;
                List<TargetModel> tmpTSTargetListForPlan = tsTargets.First().Targets;
                List<OptimizationConstraintModel> tmpList = new List<OptimizationConstraintModel> { };
                foreach (PlanTargetsModel itr in tsTargets)
                {
                    if (!string.Equals(itr.PlanId, tmpPlanId))
                    {
                        //new plan, update the list
                        tmpList.AddRange(currentList.First(x => string.Equals(x.PlanId, tmpPlanId)).OptimizationConstraints.Where(y => !tmpTSTargetListForPlan.Any(k => string.Equals(k.TargetId, y.StructureId))));
                        updatedList.Add(new PlanOptimizationSetupModel(tmpPlanId, new List<OptimizationConstraintModel>(tmpList)));
                        tmpList = new List<OptimizationConstraintModel> { };
                        tmpPlanId = itr.PlanId;
                        tmpTSTargetListForPlan = new List<TargetModel>(itr.Targets);
                    }
                    if (currentList.Any(x => string.Equals(x.PlanId, tmpPlanId)))
                    {
                        foreach(TargetModel itr1 in itr.Targets)
                        {
                            //grab all optimization constraints from the plan of interest that have the same structure id as item 2 of itr
                            List<OptimizationConstraintModel> planOptList = currentList.First(x => string.Equals(x.PlanId, tmpPlanId)).OptimizationConstraints.Where(y => string.Equals(y.StructureId, itr1.TargetId)).ToList();
                            foreach (OptimizationConstraintModel itr2 in planOptList)
                            {
                                //simple copy of constraints
                                tmpList.Add(new OptimizationConstraintModel(itr1.TsTargetId, itr2.ConstraintType, itr2.QueryDose, Units.cGy, itr2.QueryVolume, itr2.Priority));
                            }
                        }
                        
                    }
                }
                tmpList.AddRange(currentList.FirstOrDefault(x => string.Equals(x.PlanId, tmpPlanId)).OptimizationConstraints.Where(y => !tmpTSTargetListForPlan.Any(k => string.Equals(k.TargetId, y.StructureId))));
                updatedList.Add(new PlanOptimizationSetupModel(tmpPlanId, new List<OptimizationConstraintModel>(tmpList)));

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
        public static List<PlanOptimizationSetupModel> UpdateOptimizationConstraints(List<TSTargetCropOverlapModel> targetManipulations,
                                                                                    List<PrescriptionModel> prescriptions,
                                                                                    object selectedTemplate,
                                                                                    List<PlanOptimizationSetupModel> currentList = null)
        {
            List<PlanOptimizationSetupModel> updatedList = new List<PlanOptimizationSetupModel> { };
            if(!currentList.Any()) currentList = RetrieveOptConstraintsFromTemplate(selectedTemplate, prescriptions).Item1;
            if (currentList.Any())
            {
                string tmpPlanId = targetManipulations.First().PlanId;
                string tmpTargetId = targetManipulations.First().TargetId;
                List<OptimizationConstraintModel> tmpList = new List<OptimizationConstraintModel> { };
                foreach (TSTargetCropOverlapModel itr in targetManipulations)
                {
                    if (!string.Equals(itr.PlanId, tmpPlanId))
                    {
                        //new plan, update the list
                        tmpList.AddRange(currentList.FirstOrDefault(x => string.Equals(x.PlanId, tmpPlanId)).OptimizationConstraints.Where(y => !string.Equals(y.StructureId, tmpTargetId)));
                        updatedList.Add(new PlanOptimizationSetupModel(tmpPlanId, new List<OptimizationConstraintModel>(tmpList)));
                        tmpList = new List<OptimizationConstraintModel> { };
                        tmpPlanId = itr.PlanId;
                    }
                    if (currentList.Any(x => string.Equals(x.PlanId, itr.PlanId)))
                    {
                        //grab all optimization constraints from the plan of interest that have the same structure id as item 2 of itr
                        List<OptimizationConstraintModel> planOptList = currentList.FirstOrDefault(x => string.Equals(x.PlanId, itr.PlanId)).OptimizationConstraints.Where(y => string.Equals(y.StructureId, itr.TargetId)).ToList();
                        foreach (OptimizationConstraintModel itr2 in planOptList)
                        {
                            if (itr.ManipulationType == TSManipulationType.CropTargetFromStructure)
                            {
                                //simple copy of constraints
                                tmpList.Add(new OptimizationConstraintModel(itr.ManipulationTargetId, itr2.ConstraintType, itr2.QueryDose, Units.cGy, itr2.QueryVolume, itr2.Priority));
                            }
                            else
                            {
                                //need to reduce upper and lower constraints
                                tmpList.Add(new OptimizationConstraintModel(itr.ManipulationTargetId, itr2.ConstraintType, itr2.QueryDose * 0.95, Units.cGy, itr2.QueryVolume, itr2.Priority));
                            }
                        }
                    }
                    tmpTargetId = itr.TargetId;
                }
                tmpList.AddRange(currentList.FirstOrDefault(x => string.Equals(x.PlanId, tmpPlanId)).OptimizationConstraints.Where(y => !string.Equals(y.StructureId, tmpTargetId)));
                updatedList.Add(new PlanOptimizationSetupModel(tmpPlanId, new List<OptimizationConstraintModel>(tmpList)));
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
        public static List<PlanOptimizationSetupModel> UpdateOptimizationConstraints(List<TSRingStructureModel> addedRings,
                                                                                                      List<PrescriptionModel> prescriptions,
                                                                                                      object selectedTemplate,
                                                                                                      List<PlanOptimizationSetupModel> currentList = null)
        { 
            if (!currentList.Any()) currentList = RetrieveOptConstraintsFromTemplate(selectedTemplate, prescriptions).Item1;
            if (currentList.Any())
            {
                foreach (TSRingStructureModel itr in addedRings)
                {
                    string planId = TargetsHelper.GetPlanIdFromTargetId(itr.TargetId, prescriptions);
                    if (currentList.Any(x => string.Equals(x.PlanId, planId)))
                    {
                        OptimizationConstraintModel ringConstraint = new OptimizationConstraintModel(itr.RingId, OptimizationObjectiveType.Upper, itr.DoseLevel, Units.cGy, 0.0, 80);
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
        public static List<PlanOptimizationSetupModel> InsertTSJnxOptConstraints(List<PlanOptimizationSetupModel> list,
                                                                                                  List<PlanFieldJunctionModel> jnxs,
                                                                                                  List<PrescriptionModel> prescriptions)
        {
            //we want to insert the optimization constraints for these junction structure right after the ptv constraints, so find the last index of the target ptv structure and insert
            //the junction structure constraints directly after the target structure constraints
            foreach (PlanOptimizationSetupModel itr in list)
            {
                if (jnxs.Any(x => string.Equals(x.PlanSetup.Id.ToLower(), itr.PlanId.ToLower())))
                {
                    int index = itr.OptimizationConstraints.FindLastIndex(x => x.StructureId.ToLower().Contains("ptv") || x.StructureId.ToLower().Contains("ts_overlap"));
                    double rxDose = TargetsHelper.GetHighestRxForPlan(prescriptions, itr.PlanId);
                    foreach (Structure itr1 in jnxs.First(x => string.Equals(x.PlanSetup.Id.ToLower(), itr.PlanId.ToLower())).FieldJunctions.Select(x => x.JunctionStructure))
                    {
                        //per Nataliya's instructions, add both a lower and upper constraint to the junction volumes. Make the constraints match those of the ptv target
                        itr.OptimizationConstraints.Insert(++index, new OptimizationConstraintModel(itr1.Id, OptimizationObjectiveType.Lower, rxDose, Units.cGy, 100.0, 100));
                        itr.OptimizationConstraints.Insert(++index, new OptimizationConstraintModel(itr1.Id, OptimizationObjectiveType.Upper, rxDose * 1.01, Units.cGy, 0.0, 100));
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
        public static (List<PlanOptimizationSetupModel>, StringBuilder) RetrieveOptConstraintsFromTemplate(object selectedTemplate, List<PrescriptionModel> prescriptions)
        {
            StringBuilder sb = new StringBuilder();
            List<PlanOptimizationSetupModel> list = new List<PlanOptimizationSetupModel> { };
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
        public static (List<PlanOptimizationSetupModel>, StringBuilder) RetrieveOptConstraintsFromTemplate(object selectedTemplate, List<PlanTargetsModel> targets)
        {
            StringBuilder sb = new StringBuilder();
            List<PlanOptimizationSetupModel> list = new List<PlanOptimizationSetupModel> { };
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
        public static List<PlanOptimizationSetupModel> CreateOptimizationConstraintList(object selectedTemplate, Dictionary<string,string> planTargets)
        {
            List<PlanOptimizationSetupModel> list = new List<PlanOptimizationSetupModel> { };
            //no treatment template selected => scale optimization objectives by ratio of entered Rx dose to closest template treatment Rx dose
            if (selectedTemplate != null)
            {
                bool isCSIplan = false;
                if (selectedTemplate is CSIAutoPlanTemplate) isCSIplan = true;
                if (planTargets.Any())
                {
                    if(isCSIplan)
                    {
                        if ((selectedTemplate as CSIAutoPlanTemplate).InitialOptimizationConstraints.Any()) list.Add(new PlanOptimizationSetupModel(planTargets.ElementAt(0).Key, (selectedTemplate as CSIAutoPlanTemplate).InitialOptimizationConstraints));
                        if (planTargets.Count > 1 && (selectedTemplate as CSIAutoPlanTemplate).BoostOptimizationConstraints.Any()) list.Add(new PlanOptimizationSetupModel(planTargets.ElementAt(1).Key, (selectedTemplate as CSIAutoPlanTemplate).BoostOptimizationConstraints));
                    }
                    else if ((selectedTemplate as TBIAutoPlanTemplate).InitialOptimizationConstraints.Any()) list.Add(new PlanOptimizationSetupModel(planTargets.ElementAt(0).Key, (selectedTemplate as TBIAutoPlanTemplate).InitialOptimizationConstraints));
                }
                else
                {
                    if (isCSIplan)
                    {
                        if ((selectedTemplate as CSIAutoPlanTemplate).InitialOptimizationConstraints.Any()) list.Add(new PlanOptimizationSetupModel("CSI-init", (selectedTemplate as CSIAutoPlanTemplate).InitialOptimizationConstraints));
                        if ((selectedTemplate as CSIAutoPlanTemplate).BoostRxDosePerFx != 0.1 && (selectedTemplate as CSIAutoPlanTemplate).BoostOptimizationConstraints.Any()) list.Add(new PlanOptimizationSetupModel("CSI-bst", (selectedTemplate as CSIAutoPlanTemplate).BoostOptimizationConstraints));
                    }
                    else if ((selectedTemplate as TBIAutoPlanTemplate).InitialOptimizationConstraints.Any()) list.Add(new PlanOptimizationSetupModel("VMAT-TBI", (selectedTemplate as TBIAutoPlanTemplate).InitialOptimizationConstraints));
                }
            }
            return list;
        }
    }
}
