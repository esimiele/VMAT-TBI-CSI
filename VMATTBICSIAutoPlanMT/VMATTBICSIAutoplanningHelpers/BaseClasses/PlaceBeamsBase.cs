using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Media.Media3D;
using VMATTBICSIAutoPlanningHelpers.Prompts;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using SimpleProgressWindow;
using System.Reflection;
using VMATTBICSIAutoPlanningHelpers.Models;

namespace VMATTBICSIAutoPlanningHelpers.BaseClasses
{
    public class PlaceBeamsBase : SimpleMTbase
    {
        //get methods
        public List<ExternalPlanSetup> VMATPlans { get; protected set; } = new List<ExternalPlanSetup>();
        public List<PlanFieldJunctionModel> FieldJunctions { get; protected set; } = new List<PlanFieldJunctionModel> { };
        public string StackTraceError { get; protected set; } = string.Empty;

        protected bool contourOverlap = false;
        private string courseId;
        protected Course theCourse;
        protected StructureSet selectedSS;
        //plan ID, target Id, numFx, dosePerFx, cumulative dose
        protected List<PrescriptionModel> prescriptions;
        protected string calculationModel = "";
        protected string optimizationModel = "";
        protected string useGPUdose = "";
        protected string useGPUoptimization = "";
        protected string MRrestart = "";
        protected double contourOverlapMargin;

        #region virtual methods
        /// <summary>
        /// Helper method to check if there are any external beam plans in Aria that match the plan Ids in the prescription for this patient
        /// </summary>
        /// <returns></returns>
        protected virtual bool CheckExistingPlans()
        {
            UpdateUILabel("Checking for existing plans: ");
            int numExistingPlans = 0;
            int calcItems = prescriptions.Count;
            int counter = 0;
            foreach (PrescriptionModel itr in prescriptions)
            {
                if (theCourse.ExternalPlanSetups.Where(x => string.Equals(x.Id, itr.PlanId)).Any())
                {
                    numExistingPlans++;
                    ProvideUIUpdate(100 * ++counter / calcItems, $"Plan {itr.PlanId} EXISTS in course {courseId}");
                }
                else ProvideUIUpdate(100 * ++counter / calcItems, $"Plan {itr.PlanId} does not exist in course {courseId}");
            }
            if (numExistingPlans > 0)
            {
                ProvideUIUpdate(0, $"One or more plans exist in course {courseId}");
                ProvideUIUpdate("ESAPI can't remove plans in the clinical environment!");
                ProvideUIUpdate("Please manually remove this plan and try again.", true);
                return true;
            }
            else ProvideUIUpdate(100, $"No plans currently exist in course {courseId}!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        /// <summary>
        /// Virtual method for setting the VMAT beams
        /// </summary>
        /// <param name="isoLocations"></param>
        /// <returns></returns>
        protected virtual bool SetVMATBeams(PlanIsocenterModel isoLocations)
        {
            //needs to be implemented by deriving class
            return true;
        }

        /// <summary>
        /// Virtual method for calculating the isocenter positions for all plans
        /// </summary>
        /// <returns></returns>
        protected virtual List<PlanIsocenterModel> GetIsocenterPositions()
        {
            return new List<PlanIsocenterModel> { };
        }
        #endregion

        #region concrete methods
        /// <summary>
        /// Initialize the place beams functionality by copying the course Id and prescriptions locally
        /// </summary>
        /// <param name="cId"></param>
        /// <param name="presc"></param>
        public void Initialize(string cId, List<PrescriptionModel> presc)
        {
            courseId = cId;
            prescriptions = new List<PrescriptionModel>(presc);
        }

        /// <summary>
        /// Method to check if any courses exist in Aria that have a match Id to the request course Id. If so, grab that course. If not, create the course
        /// </summary>
        /// <returns></returns>
        protected bool CheckExistingCourse()
        {
            UpdateUILabel("Check course: ");
            int calcItems = 1;
            int counter = 0;
            ProvideUIUpdate(0, $"Checking for existing course {courseId}");
            //look for a course with id = courseId assigned at initialization. If it does not exit, create it, otherwise load it into memory
            if (selectedSS.Patient.Courses.Any(x => string.Equals(x.Id, courseId)))
            {
                ProvideUIUpdate(100 * ++counter / calcItems, $"Course {courseId} found!");
                theCourse = selectedSS.Patient.Courses.FirstOrDefault(x => string.Equals(x.Id, courseId));
            }
            else
            {
                ProvideUIUpdate(100 * ++counter / calcItems, $"Course  {courseId} does not exist. Creating now!");
                theCourse = CreateCourse();
            }
            if (theCourse == null)
            {
                ProvideUIUpdate(0, "Course creation or assignment failed! Exiting!", true);
                return true;
            }
            ProvideUIUpdate(100, $"Course {courseId} retrieved!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        /// <summary>
        /// Helper method to create a course with the requested course Id
        /// </summary>
        /// <returns></returns>
        private Course CreateCourse()
        {
            Course tmpCourse = null;
            if (selectedSS.Patient.CanAddCourse())
            {
                tmpCourse = selectedSS.Patient.AddCourse();
                tmpCourse.Id = courseId;
            }
            else
            {
                ProvideUIUpdate("Error! Can't add a treatment course to the patient!");
            }
            return tmpCourse;
        }

        /// <summary>
        /// Utility method to create all of the VMAT plans listed in the prescriptions array
        /// </summary>
        /// <returns></returns>
        protected bool CreateVMATPlans()
        {
            UpdateUILabel("Creating VMAT plans: ");
            foreach (PrescriptionModel itr in TargetsHelper.GetHighestRxPrescriptionForEachPlan(prescriptions))
            {
                int counter = 0;
                int calcItems = 5;
                ProvideUIUpdate(0, $"Creating plan {itr.PlanId}");
                ExternalPlanSetup thePlan = theCourse.AddExternalPlanSetup(selectedSS);
                ProvideUIUpdate(100 * ++counter / calcItems, $"Created plan {itr.PlanId}");

                //100% dose prescribed in plan and plan ID is in the prescriptions
                thePlan.SetPrescription(itr.NumberOfFractions, itr.DosePerFraction, 1.0);
                ProvideUIUpdate(100 * ++counter / calcItems, $"Set prescription for plan {itr.PlanId}");

                string planName = itr.PlanId;
                thePlan.Id = planName;
                ProvideUIUpdate(100 * ++counter / calcItems, $"Set plan Id for {planName}");

                //ask the user to set the calculation model if it was not configured
                if (string.IsNullOrEmpty(calculationModel))
                {
                    SelectItemPrompt SIP = new SelectItemPrompt("No calculation model set!" + Environment.NewLine + "Please select a calculation model!", thePlan.GetModelsForCalculationType(CalculationType.PhotonVolumeDose).ToList());
                    SIP.ShowDialog();
                    if (!SIP.GetSelection()) return true;
                    //get the plan the user chose from the combobox
                    calculationModel = SIP.GetSelectedItem();

                    //just an FYI that the calculation will likely run out of memory and crash the optimization when Acuros is used
                    if (calculationModel.ToLower().Contains("acuros") || calculationModel.ToLower().Contains("axb"))
                    {
                        ConfirmPrompt CP = new ConfirmPrompt("Warning!" + Environment.NewLine + "The optimization will likely crash (i.e., run out of memory) if Acuros is used!" + Environment.NewLine + "Continue?!");
                        CP.ShowDialog();
                        if (!CP.GetSelection()) return true;
                    }
                }

                thePlan = SetPlanCalculationOptions(thePlan);
                ProvideUIUpdate(100 * ++counter / calcItems, $"Assigned calculation options for plan: {planName}");

                //reference point can only be added for a plan that IS CURRENTLY OPEN
                //plan.AddReferencePoint(selectedSS.Structures.First(x => x.Id == "TS_PTV_VMAT"), null, "VMAT TBI", "VMAT TBI");

                //these need to be fixed
                //v16 of Eclipse allows for the creation of a plan with a named target structure and named primary reference point. Neither of these options are available in v15
                //plan.TargetVolumeID = selectedSS.Structures.First(x => x.Id == "xx");
                //plan.PrimaryReferencePoint = plan.ReferencePoints.Fisrt(x => x.Id == "xx");
                VMATPlans.Add(thePlan);
                ProvideUIUpdate(100 * ++counter / calcItems, $"Added plan {itr.PlanId} to stack!");
            }
            ProvideUIUpdate(100, "Finished creating and initializing plans!");
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        /// <summary>
        /// Helper method to set the calculation and optimization options for the created plan
        /// </summary>
        /// <param name="thePlan"></param>
        /// <returns></returns>
        private ExternalPlanSetup SetPlanCalculationOptions(ExternalPlanSetup thePlan)
        {
            int counter = 0;
            int calcItems = 5;

            thePlan.SetCalculationModel(CalculationType.PhotonVolumeDose, calculationModel);
            ProvideUIUpdate(100 * ++counter / calcItems, $"Set calculation model to {calculationModel}");

            thePlan.SetCalculationModel(CalculationType.PhotonVMATOptimization, optimizationModel);
            ProvideUIUpdate(100 * ++counter / calcItems, $"Set optimization model to {optimizationModel}");

            Dictionary<string, string> d = thePlan.GetCalculationOptions(thePlan.GetCalculationModel(CalculationType.PhotonVMATOptimization));
            ProvideUIUpdate($"Calculation options for {optimizationModel}:");
            foreach (KeyValuePair<string, string> t in d) ProvideUIUpdate($"{t.Key}, {t.Value}");

            //set the GPU dose calculation option (only valid for acuros)
            if (useGPUdose == "Yes" && !calculationModel.Contains("AAA"))
            {
                thePlan.SetCalculationOption(calculationModel, "UseGPU", useGPUdose);
                ProvideUIUpdate(100 * ++counter / calcItems, $"Set GPU option for dose calc to {useGPUdose}");
            }
            else
            {
                thePlan.SetCalculationOption(calculationModel, "UseGPU", "No");
                ProvideUIUpdate(100 * ++counter / calcItems, "Set GPU option for dose calculation to No");
            }

            //set MR restart level option for the photon optimization
            if (!thePlan.SetCalculationOption(optimizationModel, "MRLevelAtRestart", MRrestart))
            {
                ProvideUIUpdate(100 * ++counter / calcItems, $"Warning! VMAT/MRLevelAtRestart option not found for {optimizationModel}");
            }
            else ProvideUIUpdate(100 * ++counter / calcItems, $"MR restart level set to {MRrestart}");

            //set the GPU optimization option
            if (useGPUoptimization == "Yes")
            {
                thePlan.SetCalculationOption(optimizationModel, "General/OptimizerSettings/UseGPU", useGPUoptimization);
                ProvideUIUpdate(100 * ++counter / calcItems, $"Set GPU option for optimization to {useGPUoptimization}");
            }
            else
            {
                thePlan.SetCalculationOption(optimizationModel, "General/OptimizerSettings/UseGPU", "No");
                ProvideUIUpdate(100 * ++counter / calcItems, "Set GPU option for optimization to No");
            }
            return thePlan;
        }

        /// <summary>
        /// Helper method to round all of the calculated isocenter positions to the nearest integer
        /// </summary>
        /// <param name="v"></param>
        /// <param name="plan"></param>
        /// <returns></returns>
        protected VVector RoundIsocenterPosition(VVector v, ExternalPlanSetup plan)
        {
            int counter = 0;
            int calcItems = 3;
            ProvideUIUpdate(100 * ++counter / calcItems, "Rounding Y- and Z-positions to nearest integer values");
            v = plan.StructureSet.Image.DicomToUser(v, plan);
            //round z position to the nearest integer
            v.x = Math.Round(v.x / 10.0f) * 10.0f;
            v.y = Math.Round(v.y / 10.0f) * 10.0f;
            v.z = Math.Round(v.z / 10.0f) * 10.0f;
            ProvideUIUpdate(100 * ++counter / calcItems, $"Calculated isocenter position (user coordinates): ({v.x}, {v.y}, {v.z})");
            ProvideUIUpdate(100 * ++counter / calcItems, "Adding calculated isocenter position to stack!");
            return plan.StructureSet.Image.UserToDicom(v, plan);
        }

        /// <summary>
        /// Method to contour the overlap between fields in adjacent isocenters for the VMAT Plans ONLY!
        /// </summary>
        /// <param name="isoLocations"></param>
        /// <param name="isoCount"></param>
        /// <returns></returns>
        protected bool ContourFieldOverlap(PlanIsocenterModel isoLocations, int isoCount)
        {
            UpdateUILabel("Contour field overlap:");
            ProvideUIUpdate($"Contour overlap margin: {contourOverlapMargin:0.0} cm");
            contourOverlapMargin *= 10;
            ProvideUIUpdate($"Contour overlap margin: {contourOverlapMargin:0.00} mm");

            int percentCompletion = 0;
            int calcItems = 3 + 7 * isoLocations.Isocenters.Count - 1;
            //grab target Id for this prescription item
            if(!prescriptions.Any(x => string.Equals(x.PlanId, isoLocations.PlanId)))
            {
                ProvideUIUpdate($"Error! No matching prescrition found for iso plan name {isoLocations.PlanId}", true);
                return true;
            }
            string targetId = prescriptions.First(x => string.Equals(x.PlanId, isoLocations.PlanId)).TargetId;

            if(!StructureTuningHelper.DoesStructureExistInSS(targetId, selectedSS, true))
            {
                ProvideUIUpdate($"Error getting target structure ({targetId}) for plan: {isoLocations.PlanId}! Exiting!", true);
                return true;
            }
            Structure target_tmp = StructureTuningHelper.GetStructureFromId(targetId, selectedSS);
            ProvideUIUpdate(100 * ++percentCompletion / calcItems, $"Retrieved target: {target_tmp.Id} for plan: {isoLocations.PlanId}");

            //grab the image and get the z resolution and dicom origin (we only care about the z position of the dicom origin)
            Image image = selectedSS.Image;
            double zResolution = image.ZRes;
            VVector dicomOrigin = image.Origin;
            ProvideUIUpdate($"Retrived image: {image.Id}");
            ProvideUIUpdate($"Z resolution: {zResolution} mm");
            ProvideUIUpdate($"DICOM origin: ({dicomOrigin.x:0.00}, {dicomOrigin.y:0.00}, {dicomOrigin.z:0.00}) mm");

            //center position between adjacent isocenters, number of image slices to contour on, start image slice location for contouring
            List<FieldJunctionModel> overlap = new List<FieldJunctionModel> { };
            //calculate the center position between adjacent isocenters, number of image slices to contour on based on overlap and with additional user-specified margin (from main UI)
            //and the slice where the contouring should begin
            for (int i = 1; i < isoLocations.Isocenters.Count; i++)
            {
                (bool fail, FieldJunctionModel result) = CalculateOverlapParameters(i,
                                                                                    VMATPlans.First(x => string.Equals(x.Id, isoLocations.PlanId,StringComparison.OrdinalIgnoreCase)),
                                                                                    isoLocations.Isocenters.ElementAt(i - 1).IsocenterPosition,
                                                                                    isoLocations.Isocenters.ElementAt(i).IsocenterPosition,
                                                                                    zResolution,
                                                                                    dicomOrigin.z);
                if (fail) return true;

                ProvideUIUpdate(100 * ++percentCompletion / calcItems, $"Starting slice to contour: {result.StartSlice}");
                //add a new junction structure (named TS_jnx<i>) to the stack. Contours will be added to these structure later
                result.JunctionStructure = selectedSS.AddStructure("CONTROL", $"TS_jnx{isoCount + i}");
                ProvideUIUpdate(100 * ++percentCompletion / calcItems, $"Added TS junction to stack: TS_jnx{isoCount + 1}");
                overlap.Add(result);
            }
            FieldJunctions.Add(new PlanFieldJunctionModel(VMATPlans.First(x => string.Equals(x.Id, isoLocations.PlanId, StringComparison.OrdinalIgnoreCase)), overlap));

            //make a box at the min/max x,y positions of the target structure with no margin
            VVector[] targetBoundingBox = CreateTargetBoundingBox(target_tmp, 0.0);
            ProvideUIUpdate(100 * ++percentCompletion / calcItems, $"Created target bounding box for contouring overlap");
           
            //add the contours to each relevant plan for each structure in the jnxs stack
            int count = 0;
            foreach(PlanFieldJunctionModel itr in FieldJunctions)
            {
                foreach (FieldJunctionModel junction in overlap)
                {
                    percentCompletion = 0;
                    calcItems = junction.NumberOfCTSlices;
                    ProvideUIUpdate(0, $"Contouring junction: {junction.JunctionStructure.Id}");
                    for (int i = junction.StartSlice; i < (junction.StartSlice + junction.NumberOfCTSlices); i++)
                    {
                        junction.JunctionStructure.AddContourOnImagePlane(targetBoundingBox, i);
                        ProvideUIUpdate(100 * ++percentCompletion / calcItems);
                    }
                    //only keep the portion of the box contour that overlaps with the target
                    junction.JunctionStructure.SegmentVolume = junction.JunctionStructure.And(target_tmp.Margin(0));
                    count++;
                }
            }
            ProvideUIUpdate($"Elapsed time: {GetElapsedTime()}");
            return false;
        }

        /// <summary>
        /// Helper method to calculate the field overlap center, the total number of slices to contour, and the starting slice to contour
        /// </summary>
        /// <param name="jnx"></param>
        /// <param name="thePlan"></param>
        /// <param name="previousIso"></param>
        /// <param name="currentIso"></param>
        /// <param name="imageZResolution"></param>
        /// <param name="dicomOriginZ"></param>
        /// <returns></returns>
        private (bool, FieldJunctionModel) CalculateOverlapParameters(int jnx, ExternalPlanSetup thePlan, VVector previousIso, VVector currentIso, double imageZResolution, double dicomOriginZ)
        {
            int percentCompletion = 0;
            int calcItems = 5;
            ProvideUIUpdate($"Junction: {jnx}");
            //this is left as a double so I can cast it to an int in the second overlap item and use it in the calculation in the third overlap item
            //logic to consider the situation where the y extent of the fields are NOT 40 cm!
            Beam iso1Beam1 = thePlan.Beams.First(x => CalculationHelper.AreEqual(x.IsocenterPosition.z, previousIso.z));
            ProvideUIUpdate(100 * ++percentCompletion / calcItems, $"First beam in isocenter {jnx - 1}: {iso1Beam1.Id}");

            Beam iso2Beam1 = thePlan.Beams.First(x => CalculationHelper.AreEqual(x.IsocenterPosition.z, currentIso.z));
            ProvideUIUpdate(100 * ++percentCompletion / calcItems, $"First beam in isocenter {jnx}: {iso2Beam1.Id}");

            //assumes iso1beam1 y1 is oriented inferior on patient and iso2beam1 is oriented superior on patient
            double fieldLength = Math.Abs(iso1Beam1.GetEditableParameters().ControlPoints.First().JawPositions.Y1) + Math.Abs(iso2Beam1.GetEditableParameters().ControlPoints.First().JawPositions.Y2);
            ProvideUIUpdate(100 * ++percentCompletion / calcItems, $"Field length ({iso1Beam1.Id} Y1 + {iso2Beam1.Id} Y2): {fieldLength} mm");

            double numSlices = Math.Ceiling(fieldLength + contourOverlapMargin - Math.Abs(currentIso.z - previousIso.z));
            if (numSlices <= 0)
            {
                ProvideUIUpdate($"Error! Calculated number of slices is <= 0 ({numSlices}) for junction: {jnx}!", true);
                ProvideUIUpdate($"Field length: {fieldLength:0.00} mm");
                ProvideUIUpdate($"Contour overlap margin: {contourOverlapMargin:0.00} mm");
                ProvideUIUpdate($"Isocenter separation: {Math.Abs(currentIso.z - previousIso.z):0.00}!");
                return (true, null);
            }
            ProvideUIUpdate(100 * ++percentCompletion / calcItems, $"Number of slices to contour: {(int)(numSlices / imageZResolution)}");

            //calculate the center position between adjacent isocenters. NOTE: this calculation works from superior to inferior!
            double overlapCenter = previousIso.z + iso1Beam1.GetEditableParameters().ControlPoints.First().JawPositions.Y1 - contourOverlapMargin / 2 + numSlices / 2;
            ProvideUIUpdate(100 * ++percentCompletion / calcItems, $"Overlap center position: {overlapCenter:0.00} mm");
            return (false, new FieldJunctionModel(overlapCenter, // the center location
                                                  (int)(numSlices / imageZResolution), //total number of slices to contour
                                                  (int)((overlapCenter - numSlices / 2 - dicomOriginZ) / imageZResolution))); // starting slice to contour
        }

        /// <summary>
        /// Helper method to create a bounding box for the supplied target with an additional margin
        /// </summary>
        /// <param name="target"></param>
        /// <param name="margin"></param>
        /// <returns></returns>
        private VVector[] CreateTargetBoundingBox(Structure target, double margin)
        {
            margin *= 10;
            //margin is in cm
            Point3DCollection targetPts = target.MeshGeometry.Positions;
            double xMax = targetPts.Max(p => p.X) + margin;
            double xMin = targetPts.Min(p => p.X) - margin;
            double yMax = targetPts.Max(p => p.Y) + margin;
            double yMin = targetPts.Min(p => p.Y) - margin;

            VVector[] pts = new[] {
                                    new VVector(xMax, yMax, 0),
                                    new VVector(xMax, 0, 0),
                                    new VVector(xMax, yMin, 0),
                                    new VVector(0, yMin, 0),
                                    new VVector(xMin, yMin, 0),
                                    new VVector(xMin, 0, 0),
                                    new VVector(xMin, yMax, 0),
                                    new VVector(0, yMax, 0)};

            return pts;
        }

        /// <summary>
        /// Helper method to generate a DRR calculation parameters object with some default parameters
        /// </summary>
        /// <returns></returns>
        protected DRRCalculationParameters GenerateDRRParameters()
        {
            DRRCalculationParameters DRR = new DRRCalculationParameters
            {
                DRRSize = 500.0,
                FieldOutlines = true,
                StructureOutlines = true
            };
            DRR.SetLayerParameters(1, 1.0, 100.0, 1000.0);
            return DRR;
        }
        #endregion
    }
}
