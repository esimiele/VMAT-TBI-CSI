using System;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using PlanType = VMATTBICSIAutoPlanningHelpers.Enums.PlanType;
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
        public static (List<Tuple<string, string, int, DoseValue, double>>, StringBuilder) BuildPrescriptionList(List<Tuple<string, double, string>> targets, 
                                                                                                                 string initDosePerFxText, 
                                                                                                                 string initNumFxText, 
                                                                                                                 string initRxText, 
                                                                                                                 string boostDosePerFxText = "", 
                                                                                                                 string boostNumFxText = "", 
                                                                                                                 string boostRxText = "")
        {
            StringBuilder sb = new StringBuilder();
            List<Tuple<string, string, int, DoseValue, double>> prescriptions = new List<Tuple<string, string, int, DoseValue, double>> { };
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
                        targets = new List<Tuple<string, double, string>> { };
                        prescriptions = new List<Tuple<string, string, int, DoseValue, double>> { };
                        return (prescriptions, sb);
                    }
                }
                else if (rx == boostRxDose)
                {
                    if (!double.TryParse(boostDosePerFxText, out dosePerFx) || !int.TryParse(boostNumFxText, out numFractions))
                    {
                        sb.AppendLine("Error! Could not parse dose per fx or number of fractions for boost plan! Exiting");
                        targets = new List<Tuple<string, double, string>> { };
                        prescriptions = new List<Tuple<string, string, int, DoseValue, double>> { };
                        return (prescriptions, sb);
                    }
                }
                foreach(Tuple<string,double> itr1 in itr.Item2)
                {
                    prescriptions.Add(Tuple.Create(itr.Item1, itr1.Item1, numFractions, new DoseValue((itr1.Item2 - priorRxDoses) / numFractions, DoseValue.DoseUnit.cGy), itr1.Item2));
                }
                priorRxDoses += rx;
            }
            
            //sort the prescription list by the cumulative rx dose
            prescriptions.Sort(delegate (Tuple<string, string, int, DoseValue, double> x, Tuple<string, string, int, DoseValue, double> y) { return x.Item5.CompareTo(y.Item5); });

            StringBuilder msg = new StringBuilder();
            msg.AppendLine("Targets set successfully!" + Environment.NewLine);
            msg.AppendLine("Prescriptions:");
            foreach (Tuple<string, string, int, DoseValue, double> itr in prescriptions)
            {
                msg.AppendLine($"{itr.Item1}, {itr.Item2}, {itr.Item3}, {itr.Item4.Dose}, {itr.Item5}");
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
        private static List<Tuple<string,List<Tuple<string,double>>>> GetPlanTargetRxDoseList(List<Tuple<string, double, string>> targets)
        {
            List<Tuple<string, List<Tuple<string, double>>>> theList = new List<Tuple<string, List<Tuple<string, double>>>> { };
            List<Tuple<string, double>> tgtListTmp = new List<Tuple<string, double>> { };
            List<Tuple<string, double, string>> tmpList = targets.OrderBy(x => x.Item2).ToList();
            string tmpPlanId = tmpList.First().Item3;
            foreach(Tuple<string, double, string> itr in tmpList)
            {
                if(!string.Equals(itr.Item3, tmpPlanId))
                {
                    //new plan --> add the plan id and list of targets for that plan to the list
                    theList.Add(Tuple.Create(tmpPlanId, new List<Tuple<string, double>>(tgtListTmp)));
                    tmpPlanId = itr.Item3;
                    tgtListTmp = new List<Tuple<string, double>> { };
                }
                tgtListTmp.Add(Tuple.Create(itr.Item1, itr.Item2));
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
        public static List<Tuple<string,List<string>>> GetTargetListForEachPlan(List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            List<Tuple<string, List<string>>> planIdTargets = new List<Tuple<string, List<string>>> { };
            string tmpPlanId = prescriptions.First().Item1;
            List<string> targs = new List<string> { };
            foreach (Tuple<string, string, int, DoseValue, double> itr in prescriptions)
            {
                if (itr.Item1 != tmpPlanId)
                {
                    planIdTargets.Add(new Tuple<string, List<string>>(tmpPlanId, new List<string>(targs)));
                    tmpPlanId = itr.Item1;
                    targs = new List<string> { itr.Item2 };
                }
                else targs.Add(itr.Item2);
            }
            planIdTargets.Add(new Tuple<string, List<string>>(tmpPlanId, new List<string>(targs)));
            return planIdTargets;
        }

        /// <summary>
        /// Helper method to build a list of plan Id, target Id from the prescriptions
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <returns></returns>
        public static List<Tuple<string, string>> GetHighestRxPlanTargetList(List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            List<Tuple<string, string>> plansTargets = new List<Tuple<string, string>> { };
            if (!prescriptions.Any()) return plansTargets;
            //sort by cumulative dose to targets
            List<Tuple<string, string, int, DoseValue, double>> tmpList = prescriptions.OrderBy(x => x.Item5).ToList();
            string tmpPlan = tmpList.First().Item1;
            string tmpTarget = tmpList.First().Item2;

            foreach (Tuple<string, string, int, DoseValue, double> itr in tmpList)
            {
                //check if this is the start of a new plan, if so, the the previous target was the highest dose target in the previous plan
                if (!string.Equals(itr.Item1, tmpPlan))
                {
                    plansTargets.Add(Tuple.Create<string, string>(tmpPlan, tmpTarget));
                    tmpPlan = itr.Item1;
                }
                tmpTarget = itr.Item2;
            }
            plansTargets.Add(Tuple.Create<string, string>(tmpPlan, tmpTarget));
            return plansTargets;
        }

        /// <summary>
        /// Overloaded helper method to build a list of plan Id and target Ids from the supplied target list
        /// </summary>
        /// <param name="targetList"></param>
        /// <returns></returns>
        public static List<Tuple<string, string>> GetHighestRxPlanTargetList(List<Tuple<string, double, string>> targetList)
        {
            //for this list, item1 is the target, item 2 is the cumulated dose (cGy), and item 3 is the plan
            List<Tuple<string, string>> plansTargets = new List<Tuple<string, string>> { };
            if (!targetList.Any()) return plansTargets;
            //sort by cumulative dose to targets
            List<Tuple<string, double, string>> tmpList = targetList.OrderBy(x => x.Item2).ToList();
            string tmpTarget = tmpList.First().Item1;
            string tmpPlan = tmpList.First().Item3;

            foreach (Tuple<string, double, string> itr in tmpList)
            {
                //check if this is the start of a new plan, if so, the the previous target was the highest dose target in the previous plan
                if (!string.Equals(itr.Item3, tmpPlan))
                {
                    plansTargets.Add(Tuple.Create<string, string>(tmpPlan, tmpTarget));
                    tmpPlan = itr.Item3;
                }
                tmpTarget = itr.Item1;
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
        public static double GetHighestRxForPlan(List<Tuple<string, string, int, DoseValue, double>> prescriptions, string plandId)
        {
            double dose = 0.0;
            List<Tuple<string, string, int, DoseValue, double>> tmpList = prescriptions.OrderBy(x => x.Item5).ToList();
            if (tmpList.Any(x => string.Equals(x.Item1.ToLower(), plandId.ToLower())))
            {
                Tuple<string, string, int, DoseValue, double> rx = tmpList.Last(x => string.Equals(x.Item1.ToLower(), plandId.ToLower()));
                dose = rx.Item3 * rx.Item4.Dose;
            }
            return dose;
        }

        /// <summary>
        /// Simple helper method to get the Id of the target with the highest Rx dose for the supplied plan Id
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <param name="plandId"></param>
        /// <returns></returns>
        public static string GetHighestRxTargetIdForPlan(List<Tuple<string, string, int, DoseValue, double>> prescriptions, string plandId)
        {
            string targetId = "";
            List<Tuple<string, string, int, DoseValue, double>> tmpList = prescriptions.OrderBy(x => x.Item5).ToList();
            if (tmpList.Any(x => string.Equals(x.Item1.ToLower(), plandId.ToLower())))
            {
                Tuple<string, string, int, DoseValue, double> rx = tmpList.Last(x => string.Equals(x.Item1.ToLower(), plandId.ToLower()));
                targetId = rx.Item2;
            }
            return targetId;
        }

        /// <summary>
        /// Simple h
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <returns></returns>
        public static List<Tuple<string, double>> GetSortedTargetIdsByRxDose(List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            List<Tuple<string, double>> sortedTargets = new List<Tuple<string, double>> { };
            if (!prescriptions.Any()) return sortedTargets;
            //sort by cumulative dose to targets
            List<Tuple<string, string, int, DoseValue, double>> tmpList = prescriptions.OrderBy(x => x.Item5).ToList();

            foreach (Tuple<string, string, int, DoseValue, double> itr in tmpList)
            {
                sortedTargets.Add(Tuple.Create(itr.Item2, itr.Item5));
            }
            return sortedTargets;
        }

        /// <summary>
        /// Simple helper method to return a list of the target Ids from the prescription list
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <returns></returns>
        public static List<string> GetAllTargetIds(List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            return prescriptions.Select(x => x.Item2).ToList();
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
        public static string GetPlanIdFromTargetId(string targetId, List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            string planId = "";
            if(prescriptions.Any(x => string.Equals(x.Item2,targetId)))
            {
                planId = prescriptions.First(x => string.Equals(x.Item2, targetId)).Item1;
            }
            return planId;
        }

        /// <summary>
        /// Simple helper method to return a list of the plan id and the highest prescription dose for that plan
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <returns></returns>
        public static List<Tuple<string,double>> GetPlanIdHighesRxDoseFromPrescriptions(List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            List<Tuple<string, double>> planIdRx = new List<Tuple<string, double>> { };
            if(!prescriptions.Any()) return planIdRx;
            List<Tuple<string, string, int, DoseValue, double>> tmpList = prescriptions.OrderBy(x => x.Item5).ToList();
            planIdRx.Add(Tuple.Create(tmpList.First().Item1, GetHighestRxForPlan(prescriptions, tmpList.First().Item1)));
            if(tmpList.Any(x => !string.Equals(x.Item1, planIdRx.First().Item1)))
            {
                planIdRx.Add(Tuple.Create(tmpList.Last().Item1, GetHighestRxForPlan(prescriptions, tmpList.Last().Item1)));
            }
            return planIdRx;
        }

        /// <summary>
        /// Helper method to grab the highest Rx for each plan in the prescription list. Returns the entire tuple element for the highest found plan
        /// prescription
        /// </summary>
        /// <param name="prescriptions"></param>
        /// <returns></returns>
        public static List<Tuple<string, string, int, DoseValue, double>> GetHighestRxPrescriptionForEachPlan(List<Tuple<string, string, int, DoseValue, double>> prescriptions)
        {
            List<Tuple<string, string, int, DoseValue, double>> highestRxPrescriptions = new List<Tuple<string, string, int, DoseValue, double>> { };
            //sort prescriptions by cumulative Rx
            List<Tuple<string, string, int, DoseValue, double>> tmpList = prescriptions.OrderBy(x => x.Item5).ToList();

            //add the last item in the prescription list where the plan id matches the plan id in the first entry in the sorted prescription list
            highestRxPrescriptions.Add(tmpList.Last(x => string.Equals(x.Item1, tmpList.First().Item1)));
            if (tmpList.Any(x => !string.Equals(x.Item1, tmpList.First().Item1)))
            {
                //add the last item in the prescription list where the plan id DOES NOT match the plan id in the first entry in the sorted prescription list
                highestRxPrescriptions.Add(tmpList.Last(x => !string.Equals(x.Item1, tmpList.First().Item1)));
            }
            return highestRxPrescriptions;
        }
    }
}
