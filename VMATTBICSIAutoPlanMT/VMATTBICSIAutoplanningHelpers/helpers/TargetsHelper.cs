using System;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using PlanType = VMATTBICSIAutoPlanningHelpers.Enums.PlanType;
using VMATTBICSIAutoPlanningHelpers.Models;
using System.Windows.Media.Media3D;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class TargetsHelper
    {
        /// <summary>
        /// Helper method to take the supplied target list and UI parameters and build the list of prescriptions that will be used throughout the rest
        /// of the script
        /// </summary>
        /// <param name="targets"></param>
        /// <param name="initDosePerFxText"></param>
        /// <param name="initNumFxText"></param>
        /// <param name="initRxText"></param>
        /// <param name="boostDosePerFxText"></param>
        /// <param name="boostNumFxText"></param>
        /// <param name="boostRxText"></param>
        /// <returns></returns>
        public static (List<PrescriptionModel>, StringBuilder) BuildPrescriptionList(List<PlanTargetsModel> targets, 
                                                                                string initDosePerFxText, 
                                                                                string initNumFxText, 
                                                                                string initRxText, 
                                                                                string boostDosePerFxText = "", 
                                                                                string boostNumFxText = "", 
                                                                                string boostRxText = "")
        {
            StringBuilder sb = new StringBuilder();
            List<PrescriptionModel> prescriptions = new List<PrescriptionModel> { };
            double dosePerFx = 0.0;
            int numFractions = 0;
            double boostRxDose = 0.0;
            //first, parse the total initial and boost Rx doses
            if(string.IsNullOrEmpty(initRxText) || !double.TryParse(initRxText, out double initRxDose))
            {
                sb.AppendLine("Error! Initial Plan Rx dose is either empty or could not be parsed! Exiting!");
                return (prescriptions, sb);
            }
            //slightly different logic, only want to try and parse if the boostRxText is not null or empty
            if (!string.IsNullOrEmpty(boostRxText) && !double.TryParse(boostRxText, out boostRxDose))
            {
                sb.AppendLine("Error! Boost Plan Rx dose is not empty or could not be parsed! Exiting!");
                return (prescriptions, sb);
            }

            //verify the integrity of the list and it is ready to be pushed to a prescription list
            (bool fail, StringBuilder errorMessage) = VerifyRequestedTargetIntegrity(targets, initRxDose, boostRxDose);
            if(fail)
            {
                return (prescriptions, errorMessage);
            }

            double priorRxDoses = 0.0;
            double rx;
            //build the prescription list
            foreach (PlanTargetsModel itr in targets)
            {
                TargetModel highestRxTgtForPlan = itr.Targets.Last();
                rx = highestRxTgtForPlan.TargetRxDose - priorRxDoses;
                if (rx == initRxDose)
                {
                    if (!double.TryParse(initDosePerFxText, out dosePerFx) || !int.TryParse(initNumFxText, out numFractions))
                    {
                        sb.AppendLine("Error! Could not parse dose per fx or number of fractions for initial plan! Exiting");
                        prescriptions = new List<PrescriptionModel> { };
                        return (prescriptions, sb);
                    }
                }
                else if (rx == boostRxDose)
                {
                    if (!double.TryParse(boostDosePerFxText, out dosePerFx) || !int.TryParse(boostNumFxText, out numFractions))
                    {
                        sb.AppendLine("Error! Could not parse dose per fx or number of fractions for boost plan! Exiting");
                        prescriptions = new List<PrescriptionModel> { };
                        return (prescriptions, sb);
                    }
                }
                foreach(TargetModel itr1 in itr.Targets)
                {
                    prescriptions.Add(new PrescriptionModel(itr.PlanId, itr1.TargetId, numFractions, new DoseValue((itr1.TargetRxDose - priorRxDoses) / numFractions, DoseValue.DoseUnit.cGy), itr1.TargetRxDose));
                }
                priorRxDoses += rx;
            }

            //sort the prescription list by the cumulative rx dose
            prescriptions.Sort((x, y) => x.CumulativeDoseToTarget.CompareTo(y.CumulativeDoseToTarget));

            StringBuilder msg = new StringBuilder();
            msg.AppendLine("Targets set successfully!" + Environment.NewLine);
            msg.AppendLine("Prescriptions:");
            foreach (PrescriptionModel itr in prescriptions)
            {
                msg.AppendLine($"{itr.PlanId}, {itr.TargetId}, {itr.NumberOfFractions}, {itr.DosePerFraction.Dose}, {itr.CumulativeDoseToTarget}");
            }
            MessageBox.Show(msg.ToString());
            return (prescriptions, sb);
        }

        /// <summary>
        /// Helper method to verify the integrity of the supplied plan target list. Ensures number of plans is <= 2 and the highest Rx doses for each plan
        /// match either the supplied initRx or boostRx doses
        /// </summary>
        /// <param name="planTargetDoseList"></param>
        /// <param name="initRxDose"></param>
        /// <param name="boostRxDose"></param>
        /// <returns></returns>
        private static (bool, StringBuilder) VerifyRequestedTargetIntegrity(List<PlanTargetsModel> planTargetDoseList, double initRxDose, double boostRxDose)
        {
            bool fail = false;
            StringBuilder sb = new StringBuilder();
            if (planTargetDoseList.Count > 2)
            {
                sb.AppendLine("Error! Number of request plans is > 2! Exiting!");
                fail = true;
                return (fail, sb);
            }
            double rx;
            double priorRxDoses = 0.0;
            foreach (PlanTargetsModel itr in planTargetDoseList)
            {
                TargetModel highestRxTgtForPlan = itr.Targets.Last();
                rx = highestRxTgtForPlan.TargetRxDose - priorRxDoses;
                priorRxDoses += rx;
                if (rx != initRxDose && rx != boostRxDose)
                {
                    if(rx != initRxDose) sb.AppendLine($"Error! Highest Rx target ({highestRxTgtForPlan.TargetId}, {rx} cGy) for plan: {itr.PlanId} does not match initial Rx dose: ({initRxDose} cGy)!");
                    else sb.AppendLine($"Error! Highest Rx target ({highestRxTgtForPlan.TargetId}, {rx} cGy) for plan: {itr.PlanId} does not match boost Rx dose: ({boostRxDose} cGy)!");
                    fail = true;
                }
            }
            return (fail, sb);
        }

        /// <summary>
        /// Helper method to evaluate the target list for a given plan and return the target with the greatest extent in that plan
        /// </summary>
        /// <param name="planTargetModel"></param>
        /// <param name="selectedSS"></param>
        /// <returns></returns>
        public static (bool, Structure, double, StringBuilder) GetLongestTargetInPlan(PlanTargetsModel planTargetModel, StructureSet selectedSS)
        {
            double maxTargetLength = 0.0;
            Structure longestTargetInPlan = null;
            bool fail = false;
            StringBuilder sb = new StringBuilder();
            if(planTargetModel != default)
            {
                foreach (TargetModel itr in planTargetModel.Targets)
                {
                    if(!StructureTuningHelper.DoesStructureExistInSS(itr.TargetId, selectedSS, true))
                    {
                        sb.AppendLine($"Error! No structure named: {itr.TargetId} found or contoured!");
                        fail = true;
                        return (fail, longestTargetInPlan, maxTargetLength, sb);
                    }
                    Structure targStruct = StructureTuningHelper.GetStructureFromId(itr.TargetId, selectedSS);
                    Point3DCollection pts = targStruct.MeshGeometry.Positions;
                    double diff = pts.Max(p => p.Z) - pts.Min(p => p.Z);
                    if (diff > maxTargetLength)
                    {
                        longestTargetInPlan = targStruct;
                        maxTargetLength = diff;
                    }
                }
            }
            return (fail, longestTargetInPlan, maxTargetLength, sb);
        }

        /// <summary>
        /// Helper method to build a list of plan Ids each paired with a list of target Ids for that plan
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <returns></returns>
        public static List<PlanTargetsModel> GetTargetListForEachPlan(List<PrescriptionModel> prescriptions)
        {
            return new List<PlanTargetsModel>(GroupPrescriptionsByPlanIdAndOrderByTargetRx(prescriptions));
        }

        /// <summary>
        /// Helper method to build a list of plan Id, target Id from the prescriptions
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <returns></returns>
        public static Dictionary<string, string> GetHighestRxPlanTargetList(List<PrescriptionModel> prescriptions)
        {
            Dictionary<string, string> plansTargets = new Dictionary<string, string> { };
            if (!prescriptions.Any()) return plansTargets;
            //sort by cumulative dose to targets
            List<PrescriptionModel> tmpList = prescriptions.OrderBy(x => x.CumulativeDoseToTarget).ToList();
            string tmpPlan = tmpList.First().PlanId;
            string tmpTarget = tmpList.First().TargetId;

            foreach (PrescriptionModel itr in tmpList)
            {
                //check if this is the start of a new plan, if so, the the previous target was the highest dose target in the previous plan
                if (!string.Equals(itr.PlanId, tmpPlan))
                {
                    plansTargets.Add(tmpPlan, tmpTarget);
                    tmpPlan = itr.PlanId;
                }
                tmpTarget = itr.TargetId;
            }
            plansTargets.Add(tmpPlan, tmpTarget);
            return plansTargets;
        }

        /// <summary>
        /// Overloaded helper method to build a list of plan Id and target Ids from the supplied target list
        /// </summary>
        /// <param name="targetList"></param>
        /// <returns></returns>
        public static Dictionary<string, string> GetHighestRxPlanTargetList(List<PlanTargetsModel> targetList)
        {
            //for this list, item1 is the target, item 2 is the cumulated dose (cGy), and item 3 is the plan
            Dictionary<string, string> plansTargets = new Dictionary<string, string> { };
            if (!targetList.Any()) return plansTargets;
            //sort by cumulative dose to targets
            string tmpTarget = string.Empty;
            string tmpPlan = targetList.First().PlanId;

            foreach (PlanTargetsModel itr in targetList)
            {
                //check if this is the start of a new plan, if so, the the previous target was the highest dose target in the previous plan
                if (!string.Equals(itr.PlanId, tmpPlan))
                {
                    plansTargets.Add(tmpPlan, tmpTarget);
                    tmpPlan = itr.PlanId;
                }
                tmpTarget = itr.Targets.Last().TargetId;
            }
            plansTargets.Add(tmpPlan, tmpTarget);
            return plansTargets;
        }

        /// <summary>
        /// Simple helper method to get the highest Rx dose for the supplied plan Id
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <param name="plandId"></param>
        /// <returns></returns>
        public static double GetHighestRxForPlan(List<PrescriptionModel> prescriptions, string plandId)
        {
            double dose = 0.0;
            List<PrescriptionModel> tmpList = prescriptions.OrderBy(x => x.CumulativeDoseToTarget).ToList();
            if (tmpList.Any(x => string.Equals(x.PlanId.ToLower(), plandId.ToLower())))
            {
                PrescriptionModel rx = tmpList.Last(x => string.Equals(x.PlanId.ToLower(), plandId.ToLower()));
                dose = rx.NumberOfFractions * rx.DosePerFraction.Dose;
            }
            return dose;
        }

        /// <summary>
        /// Simple helper method to get the Id of the target with the highest Rx dose for the supplied plan Id
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <param name="plandId"></param>
        /// <returns></returns>
        public static string GetHighestRxTargetIdForPlan(List<PrescriptionModel> prescriptions, string plandId)
        {
            string targetId = "";
            List<PrescriptionModel> tmpList = prescriptions.OrderBy(x => x.CumulativeDoseToTarget).ToList();
            if (tmpList.Any(x => string.Equals(x.PlanId.ToLower(), plandId.ToLower())))
            {
                PrescriptionModel rx = tmpList.Last(x => string.Equals(x.PlanId.ToLower(), plandId.ToLower()));
                targetId = rx.TargetId;
            }
            return targetId;
        }

        /// <summary>
        /// Simple helper method to return a list of the target Ids from the prescription list
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <returns></returns>
        public static IEnumerable<string> GetAllTargetIds(List<PrescriptionModel> prescriptions)
        {
            return prescriptions.Select(x => x.TargetId);
        }

        /// <summary>
        /// Helper method to retrieve the target structure for the supplied plan type. Mainly useful for cases where default logic needs to be applied if
        /// no target Id is supplied
        /// </summary>
        /// <param name="ss"></param>
        /// <param name="targetId"></param>
        /// <param name="useFlash"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Structure GetTargetStructureForPlanType(StructureSet ss, string targetId, bool useFlash, PlanType type)
        {
            Structure target;
            if (string.IsNullOrEmpty(targetId))
            {
                //case where no targetId is supplied --> use default target for all plans
                if(type == PlanType.VMAT_TBI)
                {
                    //flash should only be present for vmat tbi plans
                    if (useFlash) target = StructureTuningHelper.GetStructureFromId("ts_ptv_flash", ss); 
                    else target = StructureTuningHelper.GetStructureFromId("ts_ptv_vmat", ss); 
                }
                else target = StructureTuningHelper.GetStructureFromId("ts_ptv_csi", ss);
            }
            else
            {
                target = StructureTuningHelper.GetStructureFromId(targetId, ss);
            }
            return target;
        }

        /// <summary>
        /// Simple helper method to return the plan Id from the supplied target Id and prescriptions
        /// </summary>
        /// <param name="targetId"></param>
        /// <param name="prescriptions"></param>
        /// <returns></returns>
        public static string GetPlanIdFromTargetId(string targetId, List<PrescriptionModel> prescriptions)
        {
            string planId = "";
            if(prescriptions.Any(x => string.Equals(x.TargetId,targetId)))
            {
                planId = prescriptions.First(x => string.Equals(x.TargetId, targetId)).PlanId;
            }
            return planId;
        }

        /// <summary>
        /// Simple helper method to return a list of the plan id and the highest prescription dose for that plan
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <returns></returns>
        public static Dictionary<string,double> GetPlanIdHighesRxDoseFromPrescriptions(List<PrescriptionModel> prescriptions)
        {
            Dictionary<string, double> planIdRx = new Dictionary<string, double> { };
            if(!prescriptions.Any()) return planIdRx;
            List<PrescriptionModel> tmpList = prescriptions.OrderBy(x => x.CumulativeDoseToTarget).ToList();
            planIdRx.Add(tmpList.First().PlanId, GetHighestRxForPlan(prescriptions, tmpList.First().PlanId));
            if(tmpList.Any(x => !string.Equals(x.PlanId, planIdRx.First().Key)))
            {
                planIdRx.Add(tmpList.Last().PlanId, GetHighestRxForPlan(prescriptions, tmpList.Last().PlanId));
            }
            return planIdRx;
        }

        /// <summary>
        /// Helper method to grab the highest Rx for each plan in the prescription list. Returns a list of precriptions that should each have one target model object, which is the highest dose target for that plan
        /// prescription
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <returns></returns>
        public static List<PrescriptionModel> GetHighestRxPrescriptionForEachPlan(List<PrescriptionModel> prescriptions)
        {
            //List<Prescription> highestRxPrescriptions = new List<Prescription> { };
            ////sort prescriptions by cumulative Rx
            //List<Prescription> tmpList = prescriptions.OrderBy(x => x.CumulativeDoseToTarget).ToList();

            ////add the last item in the prescription list where the plan id matches the plan id in the first entry in the sorted prescription list
            //highestRxPrescriptions.Add(tmpList.Last(x => string.Equals(x.PlanId, tmpList.First().PlanId)));
            //if (tmpList.Any(x => !string.Equals(x.PlanId, tmpList.First().PlanId)))
            //{
            //    //add the last item in the prescription list where the plan id DOES NOT match the plan id in the first entry in the sorted prescription list
            //    highestRxPrescriptions.Add(tmpList.Last(x => !string.Equals(x.PlanId, tmpList.First().PlanId)));
            //}
            //return highestRxPrescriptions;

            return prescriptions.GroupBy(x => x.PlanId, (planId, groupedTargets) => new PrescriptionModel(planId, groupedTargets.Last().TargetId, groupedTargets.Last().NumberOfFractions, groupedTargets.Last().DosePerFraction, groupedTargets.Last().CumulativeDoseToTarget)).ToList();
        }

        /// <summary>
        /// Helper method to take an ungrouped, unordered list of plan target models and first group them by plan Id, then order the targets by target prescription dose
        /// </summary>
        /// <param name="ungrouped"></param>
        /// <returns></returns>
        public static List<PlanTargetsModel> GroupTargetsByPlanIdAndOrderByTargetRx(List<PlanTargetsModel> ungrouped)
        {
            return ungrouped.GroupBy(x => x.PlanId, (planId, groupedTargets) => new PlanTargetsModel(planId, groupedTargets.SelectMany(x => x.Targets).OrderBy(y => y.TargetRxDose))).ToList();
        }

        /// <summary>
        /// Helper method to take an ungrouped, unordered list of plan target models and first group them by plan Id, then order the targets by target prescription dose
        /// </summary>
        /// <param name="ungrouped"></param>
        /// <returns></returns>
        public static List<PlanTargetsModel> GroupPrescriptionsByPlanIdAndOrderByTargetRx(List<PrescriptionModel> ungrouped)
        {
            return ungrouped.GroupBy(x => x.PlanId, (planId, groupedTargets) =>
            {
                return new PlanTargetsModel(planId, groupedTargets.SelectMany(x => new List<TargetModel>{ new TargetModel(x.TargetId, x.CumulativeDoseToTarget) }).OrderBy(y => y.TargetRxDose));
            }).ToList();
        }
    }
}
