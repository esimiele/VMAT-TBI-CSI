using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using VMATAutoPlanMT.Prompts;
using System.Windows.Threading;
using VMATAutoPlanMT.MTProgressInfo;
using System.Diagnostics;
using System.Reflection;

namespace VMATAutoPlanMT.baseClasses
{
    class placeBeamsBase : MTbase
    {
        public bool contourOverlap = false;
        public bool checkIsoPlacement = false;
        public List<ExternalPlanSetup> plans = new List<ExternalPlanSetup> { };
        public double checkIsoPlacementLimit = 5.0;
        public string courseId;
        public Course theCourse;
        public StructureSet selectedSS;
        //plan ID, target Id, numFx, dosePerFx, cumulative dose
        public List<Tuple<string, string, int, DoseValue, double>> prescriptions;
        public string calculationModel = "";
        public string optimizationModel = "";
        public string useGPUdose = "";
        public string useGPUoptimization = "";
        public string MRrestart = "";
        public double contourOverlapMargin;
        public List<Structure> jnxs = new List<Structure> { };
        public Structure target = null;
        public int numVMATIsos;

        public placeBeamsBase()
        {

        }

        public virtual bool Initialize(string cId, List<Tuple<string, string, int, DoseValue, double>> presc)
        {
            courseId = cId;
            prescriptions = new List<Tuple<string, string, int, DoseValue, double>>(presc);
            return false;
        }

        public virtual bool Execute()
        {
            ESAPIworker slave = new ESAPIworker();
            //create a new frame (multithreading jargon)
            DispatcherFrame frame = new DispatcherFrame();
            slave.RunOnNewThread(() =>
            {
                //pass the progress window the newly created thread and this instance of the optimizationLoop class.
                MTProgress pw = new MTProgress();
                pw.setCallerClass(slave, this);
                pw.ShowDialog();

                //tell the code to hold until the progress window closes.
                frame.Continue = false;
            });
            Dispatcher.PushFrame(frame);
            return false;
        }

        public virtual List<ExternalPlanSetup> GeneratePlanList()
        {
            if (checkExistingCoursePlans(courseId)) return null;
            if (createPlans()) return null;
            //plan, isocenter positions, isocenter names, number of beams per isocenter
            List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> isoLocations = getIsocenterPositions();
            UpdateUILabel("Assigning isocenters: ");
            int isoCount = 0;
            foreach(Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>> itr in isoLocations)
            {
                if (contourOverlap) contourFieldOverlap(itr, isoCount);
                if(setBeams(itr)) return null;
                isoCount += itr.Item2.Count;
            }
            MessageBox.Show("Beams placed successfully!\nPlease proceed to the optimization setup tab!");

            if (checkIsoPlacement) MessageBox.Show(String.Format("WARNING: < {0:0.00} cm margin at most superior and inferior locations of body! Verify isocenter placement!", checkIsoPlacementLimit / 10));
            return plans;
        }

        private bool checkExistingCoursePlans(string courseId)
        {
            UpdateUILabel("Check course: ");
            int calcItems = 1;
            int counter = 0;
            ProvideUIUpdate(0, String.Format("Checking for existing course {0}", courseId));
            //look for a course name VMAT TBI. If it does not exit, create it, otherwise load it into memory
            if (selectedSS.Patient.Courses.Where(x => x.Id == courseId).Any())
            {
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Course {0} found!", courseId));
                theCourse = selectedSS.Patient.Courses.FirstOrDefault(x => x.Id == courseId);
            }
            else
            {
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Course {0} does not exist. Creating now!", courseId));
                theCourse = createCourse(courseId);
            }
            if (theCourse == null)
            {
                ProvideUIUpdate(0, String.Format("Course creation or assignment failed! Exiting!"));
                return true;
            }
            ProvideUIUpdate(100, String.Format("Course {0} retrieved!", courseId));
            if (checkExistingPlans()) return true;
            return false;
        }

        public virtual bool checkExistingPlans()
        {
            UpdateUILabel("Checking for existing plans: ");
            //string msg = "";
            int numExistingPlans = 0;
            int calcItems = prescriptions.Count;
            int counter = 0;
            foreach(Tuple<string, string, int, DoseValue, double> itr in prescriptions)
            {
                if (theCourse.ExternalPlanSetups.Where(x => x.Id == itr.Item1).Any())
                {
                    //msg += itr.Item1 + Environment.NewLine;
                    numExistingPlans++;
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Plan {0} EXISTS in course {1}", itr.Item1, courseId));
                }
                else ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Plan {0} does not exist in course {1}", itr.Item1, courseId));
            }
            if(numExistingPlans > 0)
            {
                ProvideUIUpdate(0, String.Format("One or more plans exist in course {0}", courseId));
                ProvideUIUpdate(String.Format("ESAPI can't remove plans in the clinical environment!"));
                ProvideUIUpdate(String.Format("Please manually remove this plan and try again."));
                //MessageBox.Show(String.Format("The following plans already exist in course '{0}': {1}\nESAPI can't remove plans in the clinical environment! \nPlease manually remove this plan and try again.", theCourse, msg));
                return true;
            }
            else ProvideUIUpdate(100, String.Format("No plans currently exist in course {0}!", courseId));

            return false;
        }

        private Course createCourse(string courseId)
        {
            Course tmpCourse = null;
            if (selectedSS.Patient.CanAddCourse())
            {
                tmpCourse = selectedSS.Patient.AddCourse();
                tmpCourse.Id = courseId;
            }
            else
            {
                ProvideUIUpdate(String.Format("Error! Can't add a treatment course to the patient!"));
                //MessageBox.Show("Error! \nCan't add a treatment course to the patient!");
            }
            return tmpCourse;
        }

        private bool createPlans()
        {
            UpdateUILabel("Creating plans: ");
            foreach (Tuple<string, string, int, DoseValue, double> itr in prescriptions)
            {
                int calcItems = 9 * prescriptions.Count;
                int counter = 0;
                ProvideUIUpdate(0, String.Format("Creating plan {0}", itr.Item1));
                ExternalPlanSetup thePlan = theCourse.AddExternalPlanSetup(selectedSS);
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Created plan {0}", itr.Item1));
                //100% dose prescribed in plan and plan ID is in the prescriptions
                thePlan.SetPrescription(itr.Item3, itr.Item4, 1.0);
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Set prescription for plan {0}", itr.Item1));

                string planName = itr.Item1;
                thePlan.Id = planName;
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Set plan Id for {0}", itr.Item1));

                //ask the user to set the calculation model if not calculation model was set in UI.xaml.cs (up near the top with the global parameters)
                if (calculationModel == "")
                {
                    selectItem SUI = new selectItem();
                    SUI.title.Text = "No calculation model set!" + Environment.NewLine + "Please select a calculation model!";
                    foreach (string s in thePlan.GetModelsForCalculationType(CalculationType.PhotonVolumeDose)) SUI.itemCombo.Items.Add(s);
                    SUI.ShowDialog();
                    if (!SUI.confirm) return true;
                    //get the plan the user chose from the combobox
                    calculationModel = SUI.itemCombo.SelectedItem.ToString();

                    //just an FYI that the calculation will likely run out of memory and crash the optimization when Acuros is used
                    if (calculationModel.ToLower().Contains("acuros") || calculationModel.ToLower().Contains("axb"))
                    {
                        confirmUI CUI = new confirmUI();
                        CUI.message.Text = "Warning!" + Environment.NewLine + "The optimization will likely crash (i.e., run out of memory) if Acuros is used!" + Environment.NewLine + "Continue?!";
                        CUI.ShowDialog();
                        if (!CUI.confirm) return true;
                    }
                }

                thePlan.SetCalculationModel(CalculationType.PhotonVolumeDose, calculationModel);
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Set calculation model to {0}", calculationModel));

                thePlan.SetCalculationModel(CalculationType.PhotonVMATOptimization, optimizationModel);
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Set optimization model to {0}", optimizationModel));

                //Dictionary<string, string> d = thePlan.GetCalculationOptions(thePlan.GetCalculationModel(CalculationType.PhotonVMATOptimization));
                //string m = "";
                //foreach (KeyValuePair<string, string> t in d) m += String.Format("{0}, {1}", t.Key, t.Value) + System.Environment.NewLine;
                //MessageBox.Show(m);

                //set the GPU dose calculation option (only valid for acuros)
                if (useGPUdose == "Yes" && !calculationModel.Contains("AAA"))
                {
                    thePlan.SetCalculationOption(calculationModel, "UseGPU", useGPUdose);
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Set GPU option for dose calc to {0}", useGPUdose));
                }
                else
                {
                    thePlan.SetCalculationOption(calculationModel, "UseGPU", "No");
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Set GPU option for dose calculation to {0}", "No"));
                }


                //set MR restart level option for the photon optimization
                thePlan.SetCalculationOption(optimizationModel, "VMAT/MRLevelAtRestart", MRrestart);
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Set MR Restart level to {0}", MRrestart));

                //set the GPU optimization option
                if (useGPUoptimization == "Yes")
                {
                    thePlan.SetCalculationOption(optimizationModel, "General/OptimizerSettings/UseGPU", useGPUoptimization);
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Set GPU option for optimization to {0}", useGPUoptimization));
                }
                else
                {
                    thePlan.SetCalculationOption(optimizationModel, "General/OptimizerSettings/UseGPU", "No");
                    ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Set GPU option for optimization to {0}", "No"));
                }

                //reference point can only be added for a plan that IS CURRENTLY OPEN
                //plan.AddReferencePoint(selectedSS.Structures.First(x => x.Id == "TS_PTV_VMAT"), null, "VMAT TBI", "VMAT TBI");

                //these need to be fixed
                //v16 of Eclipse allows for the creation of a plan with a named target structure and named primary reference point. Neither of these options are available in v15
                //plan.TargetVolumeID = selectedSS.Structures.First(x => x.Id == "TS_PTV_VMAT");
                //plan.PrimaryReferencePoint = plan.ReferencePoints.Fisrt(x => x.Id == "VMAT TBI");
                plans.Add(thePlan);
                ProvideUIUpdate((int)(100 * ++counter / calcItems), String.Format("Added plan {0} to stack!", itr.Item1));
            }
            ProvideUIUpdate(100, String.Format("Finished creating and initializing plans!"));
            return false;
        }

        //function used to contour the overlap between fields in adjacent isocenters for the VMAT Plan ONLY!
        //this option is requested by the user by selecting the checkbox on the main UI on the beam placement tab
        private void contourFieldOverlap(Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>> isoLocations, int isoCount)
        {
            //only one isocenter. No adjacent isocenters requiring overlap contouring
            if (isoLocations.Item2.Count == 1) return;
            Structure target_tmp = selectedSS.Structures.FirstOrDefault(x => x.Id == prescriptions.FirstOrDefault(y => y.Item1 == isoLocations.Item1.Id).Item2);
            if (target_tmp == null) { MessageBox.Show(String.Format("Error getting target structure ({0}) for plan: {1}! Exiting!", prescriptions.FirstOrDefault(y => y.Item1 == isoLocations.Item1.Id).Item2, prescriptions.FirstOrDefault(y => y.Item1 == isoLocations.Item1.Id))); return; }
            //grab the image and get the z resolution and dicom origin (we only care about the z position of the dicom origin)
            Image image = selectedSS.Image;
            double zResolution = image.ZRes;
            VVector dicomOrigin = image.Origin;
            //center position between adjacent isocenters, number of image slices to contour on, start image slice location for contouring
            List<Tuple<double, int, int>> overlap = new List<Tuple<double, int, int>> { };

            //calculate the center position between adjacent isocenters, number of image slices to contour on based on overlap and with additional user-specified margin (from main UI)
            //and the slice where the contouring should begin
            //string output = "";
            for (int i = 1; i < isoLocations.Item2.Count; i++)
            {
                //calculate the center position between adjacent isocenters. NOTE: this calculation works from superior to inferior!
                double center = isoLocations.Item2.ElementAt(i - 1).Item1.z + (isoLocations.Item2.ElementAt(i).Item1.z - isoLocations.Item2.ElementAt(i - 1).Item1.z) / 2;
                //this is left as a double so I can cast it to an int in the second overlap item and use it in the calculation in the third overlap item
                double numSlices = Math.Ceiling(400.0 + contourOverlapMargin - Math.Abs(isoLocations.Item2.ElementAt(i).Item1.z - isoLocations.Item2.ElementAt(i - 1).Item1.z));
                overlap.Add(new Tuple<double, int, int>(
                    center,
                    (int)(numSlices / zResolution),
                    (int)(Math.Abs(dicomOrigin.z - center + numSlices / 2) / zResolution)));
                //add a new junction structure (named TS_jnx<i>) to the stack. Contours will be added to these structure later
                jnxs.Add(selectedSS.AddStructure("CONTROL", string.Format("TS_jnx{0}", isoCount + i)));
                //output += String.Format("{0}, {1}, {2}\n", 
                //    isoLocations.ElementAt(i - 1).z + (isoLocations.ElementAt(i).z - isoLocations.ElementAt(i - 1).z) / 2,
                //    (int)Math.Ceiling((410.0 - Math.Abs(isoLocations.ElementAt(i).z - isoLocations.ElementAt(i - 1).z)) / zResolution),
                //    (int)(Math.Abs(dicomOrigin.z - (isoLocations.ElementAt(i - 1).z + ((isoLocations.ElementAt(i).z - isoLocations.ElementAt(i - 1).z) / 2)) + Math.Ceiling((410.0 - Math.Abs(isoLocations.ElementAt(i).z - isoLocations.ElementAt(i - 1).z))/2)) / zResolution));
            }
            //MessageBox.Show(output);


            //make a box at the min/max x,y positions of the target structure with 5 cm margin
            Point3DCollection targetPts = target_tmp.MeshGeometry.Positions;
            double xMax = targetPts.Max(p => p.X) + 50.0;
            double xMin = targetPts.Min(p => p.X) - 50.0;
            double yMax = targetPts.Max(p => p.Y) + 50.0;
            double yMin = targetPts.Min(p => p.Y) - 50.0;

            VVector[] pts = new[] {
                                    new VVector(xMax, yMax, 0),
                                    new VVector(xMax, 0, 0),
                                    new VVector(xMax, yMin, 0),
                                    new VVector(0, yMin, 0),
                                    new VVector(xMin, yMin, 0),
                                    new VVector(xMin, 0, 0),
                                    new VVector(xMin, yMax, 0),
                                    new VVector(0, yMax, 0)};

            //add the contours to each relevant plan for each structure in the jnxs stack
            int count = 0;
            foreach (Tuple<double, int, int> value in overlap)
            {
                for (int i = value.Item3; i < (value.Item3 + value.Item2); i++) jnxs.ElementAt(count).AddContourOnImagePlane(pts, i);
                //only keep the portion of the box contour that overlaps with the target
                jnxs.ElementAt(count).SegmentVolume = jnxs.ElementAt(count).And(target_tmp.Margin(0));
                count++;
            }
        }

        public virtual bool setBeams(Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>> isoLocations)
        {
            return false;
        }

        public virtual List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> getIsocenterPositions()
        {
            return new List<Tuple<ExternalPlanSetup, List<Tuple<VVector, string, int>>>> { };
        }
    }
}
