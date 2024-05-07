using System;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using PlanType = VMATTBICSIAutoPlanningHelpers.Enums.PlanType;
using VMATTBICSIAutoPlanningHelpers.UtilityClasses;
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
        public static (List<Prescription>, StringBuilder) BuildPrescriptionList(List<PlanTarget> targets, 
                                                                                string initDosePerFxText, 
                                                                                string initNumFxText, 
                                                                                string initRxText, 
                                                                                string boostDosePerFxText = "", 
                                                                                string boostNumFxText = "", 
                                                                                string boostRxText = "")
        {
            StringBuilder sb = new StringBuilder();
            List<Prescription> prescriptions = new List<Prescription> { };
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

            //Build an ordered organized list of the plans, targets, and rx doses
            List<Tuple<string, List<Tuple<string, double>>>> orderedList = GetPlanTargetRxDoseList(targets);
            //verify the integrity of the list and it is ready to be pushed to a prescription list
            (bool fail, StringBuilder errorMessage) = VerifyRequestedTargetIntegrity(orderedList, initRxDose, boostRxDose);
            if(fail)
            {
                return (prescriptions, errorMessage);
            }

            double priorRxDoses = 0.0;
            double rx;
            //build the prescription list
            foreach (Tuple<string, List<Tuple<string, double>>> itr in orderedList)
            {
                Tuple<string, double> highestRxTgtForPlan = itr.Item2.Last();
                rx = highestRxTgtForPlan.Item2 - priorRxDoses;
                if (rx == initRxDose)
                {
                    if (!double.TryParse(initDosePerFxText, out dosePerFx) || !int.TryParse(initNumFxText, out numFractions))
                    {
                        sb.AppendLine("Error! Could not parse dose per fx or number of fractions for initial plan! Exiting");
                        prescriptions = new List<Prescription> { };
                        return (prescriptions, sb);
                    }
                }
                else if (rx == boostRxDose)
                {
                    if (!double.TryParse(boostDosePerFxText, out dosePerFx) || !int.TryParse(boostNumFxText, out numFractions))
                    {
                        sb.AppendLine("Error! Could not parse dose per fx or number of fractions for boost plan! Exiting");
                        prescriptions = new List<Prescription> { };
                        return (prescriptions, sb);
                    }
                }
                foreach(Tuple<string,double> itr1 in itr.Item2)
                {
                    prescriptions.Add(new Prescription(itr.Item1, itr1.Item1, numFractions, new DoseValue((itr1.Item2 - priorRxDoses) / numFractions, DoseValue.DoseUnit.cGy), itr1.Item2));
                }
                priorRxDoses += rx;
            }

            //sort the prescription list by the cumulative rx dose
            prescriptions.Sort((x, y) => x.CumulativeDoseToTarget.CompareTo(y.CumulativeDoseToTarget));
            //prescriptions.Sort(delegate (Tuple<string, string, int, DoseValue, double> x, Tuple<string, string, int, DoseValue, double> y) { return x.Item5.CompareTo(y.Item5); });

            StringBuilder msg = new StringBuilder();
            msg.AppendLine("Targets set successfully!" + Environment.NewLine);
            msg.AppendLine("Prescriptions:");
            foreach (Prescription itr in prescriptions)
            {
                msg.AppendLine($"{itr.PlanId}, {itr.TargetId}, {itr.NumberOfFractions}, {itr.DoseValue.Dose}, {itr.CumulativeDoseToTarget}");
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
        private static (bool, StringBuilder) VerifyRequestedTargetIntegrity(List<Tuple<string, List<Tuple<string, double>>>> planTargetDoseList, double initRxDose, double boostRxDose)
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
            foreach (Tuple<string, List<Tuple<string, double>>> itr in planTargetDoseList)
            {
                Tuple<string, double> highestRxTgtForPlan = itr.Item2.Last();
                rx = highestRxTgtForPlan.Item2 - priorRxDoses;
                priorRxDoses += rx;
                if (rx != initRxDose && rx != boostRxDose)
                {
                    if(rx != initRxDose) sb.AppendLine($"Error! Highest Rx target ({highestRxTgtForPlan.Item1}, {rx} cGy) for plan: {itr.Item1} does not match initial Rx dose: ({initRxDose} cGy)!");
                    else sb.AppendLine($"Error! Highest Rx target ({highestRxTgtForPlan.Item1}, {rx} cGy) for plan: {itr.Item1} does not match boost Rx dose: ({boostRxDose} cGy)!");
                    fail = true;
                }
            }
            return (fail, sb);
        }

        /// <summary>
        /// Helper method to take the supplied target list and build a list of plan ids with an accompanying list of target ids and Rx doses
        /// </summary>
        /// <param name="targets"></param>
        /// <returns></returns>
        private static List<Tuple<string,List<Tuple<string,double>>>> GetPlanTargetRxDoseList(List<PlanTarget> targets)
        {
            List<Tuple<string, List<Tuple<string, double>>>> theList = new List<Tuple<string, List<Tuple<string, double>>>> { };
            List<Tuple<string, double>> tgtListTmp = new List<Tuple<string, double>> { };
            List<PlanTarget> tmpList = targets.OrderBy(x => x.TargetRxDose).ToList();
            string tmpPlanId = tmpList.First().PlanId;
            foreach(PlanTarget itr in tmpList)
            {
                if(!string.Equals(itr.PlanId, tmpPlanId))
                {
                    //new plan --> add the plan id and list of targets for that plan to the list
                    theList.Add(Tuple.Create(tmpPlanId, new List<Tuple<string, double>>(tgtListTmp)));
                    tmpPlanId = itr.PlanId;
                    tgtListTmp = new List<Tuple<string, double>> { };
                }
                tgtListTmp.Add(Tuple.Create(itr.TargetId, itr.TargetRxDose));
            }
            theList.Add(Tuple.Create(tmpPlanId, new List<Tuple<string, double>>(tgtListTmp)));
            return theList;
        }

        /// <summary>
        /// Helper method to evaluate the target list for a given plan and return the target with the greatest extent in that plan
        /// </summary>
        /// <param name="targetListForAllPlans"></param>
        /// <param name="selectedSS"></param>
        /// <returns></returns>
        public static (bool, Structure, double, StringBuilder) GetLongestTargetInPlan(Tuple<string, List<string>> targetListForAllPlans, StructureSet selectedSS)
        {
            double maxTargetLength = 0.0;
            Structure longestTargetInPlan = null;
            bool fail = false;
            StringBuilder sb = new StringBuilder();
            if(targetListForAllPlans != default)
            {
                foreach (string itr in targetListForAllPlans.Item2)
                {
                    if(!StructureTuningHelper.DoesStructureExistInSS(itr, selectedSS, true))
                    {
                        sb.AppendLine($"Error! No structure named: {itr} found or contoured!");
                        fail = true;
                        return (fail, longestTargetInPlan, maxTargetLength, sb);
                    }
                    Structure targStruct = StructureTuningHelper.GetStructureFromId(itr, selectedSS);
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
        public static List<Tuple<string,List<string>>> GetTargetListForEachPlan(List<Prescription> prescriptions)
        {
            List<Tuple<string, List<string>>> planIdTargets = new List<Tuple<string, List<string>>> { };
            string tmpPlanId = prescriptions.First().PlanId;
            List<string> targs = new List<string> { };
            foreach (Prescription itr in prescriptions)
            {
                if (itr.PlanId != tmpPlanId)
                {
                    planIdTargets.Add(new Tuple<string, List<string>>(tmpPlanId, new List<string>(targs)));
                    tmpPlanId = itr.PlanId;
                    targs = new List<string> { itr.TargetId };
                }
                else targs.Add(itr.TargetId);
            }
            planIdTargets.Add(new Tuple<string, List<string>>(tmpPlanId, new List<string>(targs)));
            return planIdTargets;
        }

        /// <summary>
        /// Helper method to build a list of plan Id, target Id from the prescriptions
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <returns></returns>
        public static List<Tuple<string, string>> GetHighestRxPlanTargetList(List<Prescription> prescriptions)
        {
            List<Tuple<string, string>> plansTargets = new List<Tuple<string, string>> { };
            if (!prescriptions.Any()) return plansTargets;
            //sort by cumulative dose to targets
            List<Prescription> tmpList = prescriptions.OrderBy(x => x.CumulativeDoseToTarget).ToList();
            string tmpPlan = tmpList.First().PlanId;
            string tmpTarget = tmpList.First().TargetId;

            foreach (Prescription itr in tmpList)
            {
                //check if this is the start of a new plan, if so, the the previous target was the highest dose target in the previous plan
                if (!string.Equals(itr.PlanId, tmpPlan))
                {
                    plansTargets.Add(Tuple.Create<string, string>(tmpPlan, tmpTarget));
                    tmpPlan = itr.PlanId;
                }
                tmpTarget = itr.TargetId;
            }
            plansTargets.Add(Tuple.Create<string, string>(tmpPlan, tmpTarget));
            return plansTargets;
        }

        /// <summary>
        /// Overloaded helper method to build a list of plan Id and target Ids from the supplied target list
        /// </summary>
        /// <param name="targetList"></param>
        /// <returns></returns>
        public static List<Tuple<string, string>> GetHighestRxPlanTargetList(List<PlanTarget> targetList)
        {
            //for this list, item1 is the target, item 2 is the cumulated dose (cGy), and item 3 is the plan
            List<Tuple<string, string>> plansTargets = new List<Tuple<string, string>> { };
            if (!targetList.Any()) return plansTargets;
            //sort by cumulative dose to targets
            List<PlanTarget> tmpList = targetList.OrderBy(x => x.TargetRxDose).ToList();
            string tmpTarget = tmpList.First().TargetId;
            string tmpPlan = tmpList.First().PlanId;

            foreach (PlanTarget itr in tmpList)
            {
                //check if this is the start of a new plan, if so, the the previous target was the highest dose target in the previous plan
                if (!string.Equals(itr.PlanId, tmpPlan))
                {
                    plansTargets.Add(Tuple.Create<string, string>(tmpPlan, tmpTarget));
                    tmpPlan = itr.PlanId;
                }
                tmpTarget = itr.TargetId;
            }
            plansTargets.Add(Tuple.Create<string, string>(tmpPlan, tmpTarget));
            return plansTargets;
        }

        /// <summary>
        /// Simple helper method to get the highest Rx dose for the supplied plan Id
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <param name="plandId"></param>
        /// <returns></returns>
        public static double GetHighestRxForPlan(List<Prescription> prescriptions, string plandId)
        {
            double dose = 0.0;
            List<Prescription> tmpList = prescriptions.OrderBy(x => x.CumulativeDoseToTarget).ToList();
            if (tmpList.Any(x => string.Equals(x.PlanId.ToLower(), plandId.ToLower())))
            {
                Prescription rx = tmpList.Last(x => string.Equals(x.PlanId.ToLower(), plandId.ToLower()));
                dose = rx.NumberOfFractions * rx.DoseValue.Dose;
            }
            return dose;
        }

        /// <summary>
        /// Simple helper method to get the Id of the target with the highest Rx dose for the supplied plan Id
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <param name="plandId"></param>
        /// <returns></returns>
        public static string GetHighestRxTargetIdForPlan(List<Prescription> prescriptions, string plandId)
        {
            string targetId = "";
            List<Prescription> tmpList = prescriptions.OrderBy(x => x.CumulativeDoseToTarget).ToList();
            if (tmpList.Any(x => string.Equals(x.PlanId.ToLower(), plandId.ToLower())))
            {
                Prescription rx = tmpList.Last(x => string.Equals(x.PlanId.ToLower(), plandId.ToLower()));
                targetId = rx.TargetId;
            }
            return targetId;
        }

        /// <summary>
        /// Simple h
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <returns></returns>
        public static List<Tuple<string, double>> GetSortedTargetIdsByRxDose(List<Prescription> prescriptions)
        {
            List<Tuple<string, double>> sortedTargets = new List<Tuple<string, double>> { };
            if (!prescriptions.Any()) return sortedTargets;
            //sort by cumulative dose to targets
            List<Prescription> tmpList = prescriptions.OrderBy(x => x.CumulativeDoseToTarget).ToList();

            foreach (Prescription itr in tmpList)
            {
                sortedTargets.Add(Tuple.Create(itr.TargetId, itr.CumulativeDoseToTarget));
            }
            return sortedTargets;
        }

        /// <summary>
        /// Simple helper method to return a list of the target Ids from the prescription list
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <returns></returns>
        public static IEnumerable<string> GetAllTargetIds(List<Prescription> prescriptions)
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
        public static string GetPlanIdFromTargetId(string targetId, List<Prescription> prescriptions)
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
        public static List<Tuple<string,double>> GetPlanIdHighesRxDoseFromPrescriptions(List<Prescription> prescriptions)
        {
            List<Tuple<string, double>> planIdRx = new List<Tuple<string, double>> { };
            if(!prescriptions.Any()) return planIdRx;
            List<Prescription> tmpList = prescriptions.OrderBy(x => x.CumulativeDoseToTarget).ToList();
            planIdRx.Add(Tuple.Create(tmpList.First().PlanId, GetHighestRxForPlan(prescriptions, tmpList.First().PlanId)));
            if(tmpList.Any(x => !string.Equals(x.PlanId, planIdRx.First().Item1)))
            {
                planIdRx.Add(Tuple.Create(tmpList.Last().PlanId, GetHighestRxForPlan(prescriptions, tmpList.Last().PlanId)));
            }
            return planIdRx;
        }

        /// <summary>
        /// Helper method to grab the highest Rx for each plan in the prescription list. Returns the entire tuple element for the highest found plan
        /// prescription
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <returns></returns>
        public static List<Prescription> GetHighestRxPrescriptionForEachPlan(List<Prescription> prescriptions)
        {
            List<Prescription> highestRxPrescriptions = new List<Prescription> { };
            //sort prescriptions by cumulative Rx
            List<Prescription> tmpList = prescriptions.OrderBy(x => x.CumulativeDoseToTarget).ToList();

            //add the last item in the prescription list where the plan id matches the plan id in the first entry in the sorted prescription list
            highestRxPrescriptions.Add(tmpList.Last(x => string.Equals(x.PlanId, tmpList.First().PlanId)));
            if (tmpList.Any(x => !string.Equals(x.PlanId, tmpList.First().PlanId)))
            {
                //add the last item in the prescription list where the plan id DOES NOT match the plan id in the first entry in the sorted prescription list
                highestRxPrescriptions.Add(tmpList.Last(x => !string.Equals(x.PlanId, tmpList.First().PlanId)));
            }
            return highestRxPrescriptions;
        }
    }
}
