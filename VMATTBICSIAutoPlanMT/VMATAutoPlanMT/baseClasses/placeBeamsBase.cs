using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Forms;
using System.Windows.Media.Media3D;

namespace VMATAutoPlanMT
{
    class placeBeamsBase
    {
        public bool contourOverlap = false;
        public bool checkIsoPlacement = false;
        public ExternalPlanSetup plan = null;
        public double checkIsoPlacementLimit = 5.0;
        public Course theCourse;
        public StructureSet selectedSS;
        public Tuple<int, DoseValue> prescription;
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

        public virtual ExternalPlanSetup generatePlan(string planType)
        {
            if (checkExistingCoursePlans(planType)) return null;
            if (createCourseAndPlan(planType)) return null;
            List<VVector> isoLocations = getIsocenterPositions();
            if (contourOverlap) contourFieldOverlap(isoLocations);
            setBeams(isoLocations);

            if (checkIsoPlacement) MessageBox.Show(String.Format("WARNING: < {0:0.00} cm margin at most superior and inferior locations of body! Verify isocenter placement!", checkIsoPlacementLimit / 10));
            return plan;
        }

        public virtual bool checkExistingCoursePlans(string planType)
        {
            //look for a course name VMAT TBI. If it does not exit, create it, otherwise load it into memory
            if (!selectedSS.Patient.Courses.Where(x => x.Id == planType).Any())
            {
                if (selectedSS.Patient.CanAddCourse())
                {
                    theCourse = selectedSS.Patient.AddCourse();
                    theCourse.Id = planType;
                }
                else
                {
                    MessageBox.Show("Error! \nCan't add a treatment course to the patient!");
                    return true;
                }
            }
            else theCourse = selectedSS.Patient.Courses.FirstOrDefault(x => x.Id == planType);

            if(checkExistingPlans(planType)) return true;
            return false;
        }

        public virtual bool checkExistingPlans(string planID)
        {
            //6-10-2020 EAS, research system only!
            //if (tbi.ExternalPlanSetups.Where(x => x.Id == "_VMAT TBI").Any()) if (tbi.CanRemovePlanSetup((tbi.ExternalPlanSetups.First(x => x.Id == "_VMAT TBI")))) tbi.RemovePlanSetup(tbi.ExternalPlanSetups.First(x => x.Id == "_VMAT TBI"));
            if (theCourse.ExternalPlanSetups.Where(x => x.Id == String.Format("_{0}", planID)).Any())
            {
                MessageBox.Show(String.Format("A plan named '_{0}' Already exists! \nESAPI can't remove plans in the clinical environment! \nPlease manually remove this plan and try again.",planID));
                return true;
            }
            return false;
        }

        public virtual bool createCourseAndPlan(string planType)
        {
            
            plan = theCourse.AddExternalPlanSetup(selectedSS);
            //100% dose prescribed in plan and plan ID is _VMAT TBI
            plan.SetPrescription(prescription.Item1, prescription.Item2, 1.0);
            plan.Id = String.Format("_{0}", planType);
            //ask the user to set the calculation model if not calculation model was set in UI.xaml.cs (up near the top with the global parameters)
            if (calculationModel == "")
            {
                IEnumerable<string> models = plan.GetModelsForCalculationType(CalculationType.PhotonVolumeDose);
                selectItem SUI = new selectItem();
                SUI.title.Text = "No calculation model set!" + Environment.NewLine + "Please select a calculation model!";
                foreach (string s in plan.GetModelsForCalculationType(CalculationType.PhotonVolumeDose)) SUI.itemCombo.Items.Add(s);
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
            plan.SetCalculationModel(CalculationType.PhotonVolumeDose, calculationModel);
            plan.SetCalculationModel(CalculationType.PhotonVMATOptimization, optimizationModel);

            //Dictionary<string, string> d = plan.GetCalculationOptions(plan.GetCalculationModel(CalculationType.PhotonVMATOptimization));
            //string m = "";
            //foreach (KeyValuePair<string, string> t in d) m += String.Format("{0}, {1}", t.Key, t.Value) + System.Environment.NewLine;
            //MessageBox.Show(m);

            //set the GPU dose calculation option (only valid for acuros)
            if (useGPUdose == "Yes" && !calculationModel.Contains("AAA")) plan.SetCalculationOption(calculationModel, "UseGPU", useGPUdose);
            else plan.SetCalculationOption(calculationModel, "UseGPU", "No");

            //set MR restart level option for the photon optimization
            plan.SetCalculationOption(optimizationModel, "VMAT/MRLevelAtRestart", MRrestart);

            //set the GPU optimization option
            if (useGPUoptimization == "Yes") plan.SetCalculationOption(optimizationModel, "General/OptimizerSettings/UseGPU", useGPUoptimization);
            else plan.SetCalculationOption(optimizationModel, "General/OptimizerSettings/UseGPU", "No");

            //reference point can only be added for a plan that IS CURRENTLY OPEN
            //plan.AddReferencePoint(selectedSS.Structures.First(x => x.Id == "TS_PTV_VMAT"), null, "VMAT TBI", "VMAT TBI");

            //these need to be fixed
            //v16 of Eclipse allows for the creation of a plan with a named target structure and named primary reference point. Neither of these options are available in v15
            //plan.TargetVolumeID = selectedSS.Structures.First(x => x.Id == "TS_PTV_VMAT");
            //plan.PrimaryReferencePoint = plan.ReferencePoints.Fisrt(x => x.Id == "VMAT TBI");

            return false;
        }

        //function used to cnotour the overlap between fields in adjacent isocenters for the VMAT Plan ONLY!
        //this option is requested by the user by selecting the checkbox on the main UI on the beam placement tab
        public virtual void contourFieldOverlap(List<VVector> isoLocations)
        {
            //grab the image and get the z resolution and dicom origin (we only care about the z position of the dicom origin)
            Image image = selectedSS.Image;
            double zResolution = image.ZRes;
            VVector dicomOrigin = image.Origin;
            //center position between adjacent isocenters, number of image slices to contour on, start image slice location for contouring
            List<Tuple<double, int, int>> overlap = new List<Tuple<double, int, int>> { };

            //calculate the center position between adjacent isocenters, number of image slices to contour on based on overlap and with additional user-specified margin (from main UI)
            //and the slice where the contouring should begin
            //string output = "";
            for (int i = 1; i < numVMATIsos; i++)
            {
                //calculate the center position between adjacent isocenters. NOTE: this calculation works from superior to inferior!
                double center = isoLocations.ElementAt(i - 1).z + (isoLocations.ElementAt(i).z - isoLocations.ElementAt(i - 1).z) / 2;
                //this is left as a double so I can cast it to an int in the second overlap item and use it in the calculation in the third overlap item
                double numSlices = Math.Ceiling(400.0 + contourOverlapMargin - Math.Abs(isoLocations.ElementAt(i).z - isoLocations.ElementAt(i - 1).z));
                overlap.Add(new Tuple<double, int, int>(
                    center,
                    (int)(numSlices / zResolution),
                    (int)(Math.Abs(dicomOrigin.z - center + numSlices / 2) / zResolution)));
                //add a new junction structure (named TS_jnx<i>) to the stack. Contours will be added to these structure later
                jnxs.Add(selectedSS.AddStructure("CONTROL", string.Format("TS_jnx{0}", i)));
                //output += String.Format("{0}, {1}, {2}\n", 
                //    isoLocations.ElementAt(i - 1).z + (isoLocations.ElementAt(i).z - isoLocations.ElementAt(i - 1).z) / 2,
                //    (int)Math.Ceiling((410.0 - Math.Abs(isoLocations.ElementAt(i).z - isoLocations.ElementAt(i - 1).z)) / zResolution),
                //    (int)(Math.Abs(dicomOrigin.z - (isoLocations.ElementAt(i - 1).z + ((isoLocations.ElementAt(i).z - isoLocations.ElementAt(i - 1).z) / 2)) + Math.Ceiling((410.0 - Math.Abs(isoLocations.ElementAt(i).z - isoLocations.ElementAt(i - 1).z))/2)) / zResolution));
            }
            //MessageBox.Show(output);

            //make a box at the min/max x,y positions of the target structure with 5 cm margin
            Point3DCollection targetPts = target.MeshGeometry.Positions;
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
                jnxs.ElementAt(count).SegmentVolume = jnxs.ElementAt(count).And(target.Margin(0));
                count++;
            }
        }

        public virtual void setBeams(List<VVector> isoLocations)
        {

        }


        public virtual List<VVector> getIsocenterPositions()
        {
            return new List<VVector>();
        }
    }
}
