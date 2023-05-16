using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleProgressWindow;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATCSIAutoPlanMT.VMAT_CSI
{
    public class GenerateTargets_CSI : SimpleMTbase
    {
        public List<string> GetAddedTargetStructures() { return addedTargetIds; }
        public string GetErrorStackTrace() { return stackTraceError; }

        //DICOM types
        //Possible values are "AVOIDANCE", "CAVITY", "CONTRAST_AGENT", "CTV", "EXTERNAL", "GTV", "IRRAD_VOLUME", 
        //"ORGAN", "PTV", "TREATED_VOLUME", "SUPPORT", "FIXATION", "CONTROL", and "DOSE_REGION". 
        private List<Tuple<string, string>> createPrelimTargetList;
        StructureSet selectedSS;
        protected List<string> addedTargetIds = new List<string> { };
        protected string stackTraceError;

        public GenerateTargets_CSI(List<Tuple<string, string>> tgts, StructureSet ss)
        {
            createPrelimTargetList = new List<Tuple<string, string>>(tgts);
            selectedSS = ss;
        }

        public override bool Run()
        {
            try
            {
                if (PreliminaryChecks()) return true;
                if (CheckForTargetStructures()) return true;
                UpdateUILabel("Finished!");
                ProvideUIUpdate(100, "Finished Generating Preliminary Targets!");
                ProvideUIUpdate($"Run time: {GetElapsedTime()} (mm:ss)");
                return false;
            }
            catch (Exception e)
            {
                ProvideUIUpdate($"{e.Message}", true);
                stackTraceError = e.StackTrace;
                return true;
            }
        }

        private bool PreliminaryChecks()
        {
            UpdateUILabel("Performing Preliminary Checks: ");
            int calcItems = 1;
            int counter = 0;
            //verify brain and spine structures are present
            if (!selectedSS.Structures.Any(x => string.Equals(x.Id.ToLower(), "brain") && !x.IsEmpty) ||
                !selectedSS.Structures.Any(x => (string.Equals(x.Id.ToLower(), "spinalcord") || string.Equals(x.Id.ToLower(), "spinal_cord")) && !x.IsEmpty))
            {
                ProvideUIUpdate("Missing brain and/or spine structures! Please add and try again!", true);
                return true;
            }

            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Brain and spinal cord structures exist");
            ProvideUIUpdate(100, "Preliminary checks complete!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        #region Target Creation
        private bool CheckForTargetStructures()
        {
            UpdateUILabel("Checking For Missing Target Structures: ");
            ProvideUIUpdate(0, "Checking for missing target structures!");
            List<Tuple<string, string>> missingTargets = new List<Tuple<string, string>> { };
            int calcItems = createPrelimTargetList.Count;
            int counter = 0;
            foreach (Tuple<string, string> itr in createPrelimTargetList)
            {
                Structure tmp = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), itr.Item2.ToLower()));
                if (tmp == null || tmp.IsEmpty)
                {
                    ProvideUIUpdate($"Target: {itr.Item2} is missing or empty");
                    missingTargets.Add(itr);
                }
                else ProvideUIUpdate($"Target: {itr.Item2} is exists and is contoured");
                ProvideUIUpdate((int)(100 * ++counter / calcItems));
            }
            if (missingTargets.Any())
            {
                ProvideUIUpdate("Targets missing from the structure set! Creating them now!");
                if (CreateTargetStructures(missingTargets)) return true;
            }
            ProvideUIUpdate("All requested targets are present and contoured! Skipping target creation!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        protected bool CreateTargetStructures(List<Tuple<string, string>> missingTargets)
        {
            UpdateUILabel("Create Missing Target Structures: ");
            ProvideUIUpdate(0, "Creating missing target structures!");
            //create the CTV and PTV structures
            //if these structures were present, they should have been removed (regardless if they were contoured or not). 
            List<Structure> addedTargets = new List<Structure> { };
            //List<Tuple<string, string>> prospectiveTargets = createTSStructureList.Where(x => x.Item2.ToLower().Contains("ctv") || x.Item2.ToLower().Contains("ptv")).OrderBy(x => x.Item2).ToList();
            //int calcItems = prospectiveTargets.Count;
            int calcItems = missingTargets.Count;
            int counter = 0;
            foreach (Tuple<string, string> itr in missingTargets)
            {
                if (selectedSS.CanAddStructure(itr.Item1, itr.Item2))
                {
                    addedTargetIds.Add(itr.Item2);
                    addedTargets.Add(selectedSS.AddStructure(itr.Item1, itr.Item2));
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Added target: {itr.Item2}");
                    //optParameters.Add(new Tuple<string,string>(itr.Item1, itr.Item2));
                }
                else
                {
                    ProvideUIUpdate($"Can't add {itr.Item2} to the structure set!", true);
                    //MessageBox.Show($"Can't add {0} to the structure set!", itr.Item2));
                    return true;
                }
            }

            Structure tmp = null;
            calcItems = addedTargets.Count + 5;
            counter = 0;
            foreach (Structure itr in addedTargets)
            {
                ProvideUIUpdate((int)(100 * ++counter / calcItems), $"Contouring target: {itr.Id}");
                if (itr.Id.ToLower().Contains("brain"))
                {
                    tmp = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "brain"));
                    if (tmp != null && !tmp.IsEmpty)
                    {
                        if (itr.Id.ToLower().Contains("ctv"))
                        {
                            //CTV structure. Brain CTV IS the brain structure
                            itr.SegmentVolume = tmp.Margin(0.0);
                        }
                        else
                        {
                            //PTV structure
                            //5 mm uniform margin to generate PTV
                            itr.SegmentVolume = tmp.Margin(5.0);
                        }
                    }
                    else
                    {
                        ProvideUIUpdate("Error! Could not retrieve brain structure! Exiting!", true);
                        return true;
                    }
                }
                else if (itr.Id.ToLower().Contains("spine"))
                {
                    tmp = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "spinalcord") || string.Equals(x.Id.ToLower(), "spinal_cord"));
                    if (tmp != null && !tmp.IsEmpty)
                    {
                        if (itr.Id.ToLower().Contains("ctv"))
                        {
                            //CTV structure. Brain CTV IS the brain structure
                            //AxisAlignedMargins(inner or outer margin, margin from negative x, margin for negative y, margin for negative z, margin for positive x, margin for positive y, margin for positive z)
                            //according to Nataliya: CTV_spine = spinal_cord+0.5cm ANT, +1.5cm Inf, and +1.0 cm in all other directions
                            itr.SegmentVolume = tmp.AsymmetricMargin(new AxisAlignedMargins(StructureMarginGeometry.Outer,
                                                                                            10.0,
                                                                                            5.0,
                                                                                            15.0,
                                                                                            10.0,
                                                                                            10.0,
                                                                                            10.0));
                        }
                        else
                        {
                            //PTV structure
                            //5 mm uniform margin to generate PTV
                            tmp = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "ctv_spine"));
                            if (tmp != null && !tmp.IsEmpty) itr.SegmentVolume = tmp.Margin(5.0);
                            else { ProvideUIUpdate("Error! Could not retrieve CTV_Spine structure! Exiting!", true); return true; }
                        }
                    }
                    else
                    {
                        ProvideUIUpdate("Error! Could not retrieve brain structure! Exiting!", true);
                        return true;
                    }
                }
            }

            if (addedTargetIds.Any(x => string.Equals(x.ToLower(), "ptv_csi")))
            {
                ProvideUIUpdate((int)(100 * ++counter / calcItems), "Generating: PTV_CSI");
                ProvideUIUpdate((int)(100 * ++counter / calcItems), "Retrieving: PTV_CSI, PTV_Brain, and PTV_Spine");
                //used to create the ptv_csi structures
                Structure combinedTarget = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "ptv_csi"));
                Structure brainTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_brain"));
                Structure spineTarget = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower().Contains("ptv_spine"));
                ProvideUIUpdate((int)(100 * ++counter / calcItems), "Unioning PTV_Brain and PTV_Spine to make PTV_CSI");
                combinedTarget.SegmentVolume = brainTarget.Margin(0.0);
                combinedTarget.SegmentVolume = combinedTarget.Or(spineTarget.Margin(0.0));

                ProvideUIUpdate((int)(100 * ++counter / calcItems), "Cropping PTV_CSI from body with 3 mm inner margin");
                //1/3/2022, crop PTV structure from body by 3mm
                (bool fail, StringBuilder errorMessage) = ContourHelper.CropStructureFromBody(combinedTarget, selectedSS, -0.3);
                if (fail)
                {
                    ProvideUIUpdate(errorMessage.ToString());
                    return true;
                }
            }
            else ProvideUIUpdate((int)(100 * ++counter / calcItems), "PTV_CSI already exists in the structure set! Skipping!");
            ProvideUIUpdate((int)(100 * ++counter / calcItems), "Targets added and contoured!");
            return false;
        }
        #endregion
    }
}
