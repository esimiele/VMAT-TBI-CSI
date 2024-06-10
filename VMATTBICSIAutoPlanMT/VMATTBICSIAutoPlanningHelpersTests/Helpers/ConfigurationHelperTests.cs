using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMATTBICSIAutoPlanningHelpers.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.Types;
using VMATTBICSIAutoPlanningHelpers.Models;
using System.Windows.Input;
using Telerik.JustMock.AutoMock.Ninject.Planning.Targets;
using VMS.TPS.Common.Model.API;
using Telerik.JustMock.AutoMock.Ninject.Planning;
using VMS.TPS.Common.Model;
using System.Security.AccessControl;
using VMATTBICSIAutoPlanningHelpersTests.EqualityComparerClasses;

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    [TestClass()]
    public class ConfigurationHelperTests
    {
        [TestMethod()]
        public void CropLineTestPass1()
        {
            string testLine = "create ring{PTV_CSI,1.5,2.0,600}";
            string expected = "PTV_CSI,1.5,2.0,600}";

            Assert.AreEqual(expected, ConfigurationHelper.CropLine(testLine, "{"));
        }

        [TestMethod()]
        public void CropLineTestPass2()
        {
            string testLine = "PTV_CSI,1.5,2.0,600}";
            string expected = "1.5,2.0,600}";

            Assert.AreEqual(expected, ConfigurationHelper.CropLine(testLine, ","));
        }

        [TestMethod()]
        public void CropLineTestFail()
        {
            string testLine = "create ring{PTV_CSI,1.5,2.0,600}";
            string expected = "PTV_CSI,1.5,2.0,600}";
            Assert.AreNotEqual(expected, ConfigurationHelper.CropLine(testLine, ","));
        }

        [TestMethod()]
        public void ParseJawPositionsTestPass()
        {
            string testJawPos = "add jaw position{-100.0,-100.0,100.0,100.0}";
            VRect<double> expected = new VRect<double>(-100.0, -100.0, 100.0, 100.0);
            (bool fail, VRect<double> result) = ConfigurationHelper.ParseJawPositions(testJawPos);
            Assert.AreEqual(expected, result);
        }

        [TestMethod()]
        public void ParseJawPositionsTestFail1()
        {
            string testJawPos = "add jaw position{-100.0,-100.0,100.0,100.0}";
            VRect<double> expected = new VRect<double>(-200.0, -100.0, 100.0, 100.0);
            (bool fail, VRect<double> result) = ConfigurationHelper.ParseJawPositions(testJawPos);
            Assert.AreNotEqual(expected, result);
        }

        [TestMethod()]
        public void ParseJawPositionsTestFail2()
        {
            string testJawPos = "add jaw position{-100.0,-100.0,100.0,100.0,100.0}";
            (bool fail, VRect<double> result) = ConfigurationHelper.ParseJawPositions(testJawPos);
            Assert.AreEqual(true, fail);
        }

        [TestMethod()]
        public void ParseCreateTSTestPass()
        {
            string testCreateTS = "create TS{CONTROL,TS_Eyes}";
            RequestedTSStructureModel expected = new RequestedTSStructureModel("CONTROL", "TS_Eyes");
            RequestedTSStructureModel result = ConfigurationHelper.ParseCreateTS(testCreateTS);
            Assert.AreEqual(expected.StructureId, result.StructureId);
            Assert.AreEqual(expected.DICOMType, result.DICOMType);

            testCreateTS = "create TS{PTV,TS_Eyes}";
            expected = new RequestedTSStructureModel("CONTROL", "TS_Eyes");
            result = ConfigurationHelper.ParseCreateTS(testCreateTS);
            Assert.AreEqual(expected.StructureId, result.StructureId);
            Assert.AreNotEqual(expected.DICOMType, result.DICOMType);
        }

        [TestMethod()]
        public void ParseDaemonSettingsTest()
        {
            string dummyDaemon = "Aria DB daemon={VMSDBD,10.151.176.60,51402}";
            DaemonModel expected = new DaemonModel("VMSDBD", "10.151.176.60", 51402);
            DaemonModelComparer comparer = new DaemonModelComparer();
            Assert.IsTrue(comparer.Equals(expected, ConfigurationHelper.ParseDaemonSettings(dummyDaemon)));
        }

        [TestMethod()]
        public void ParseCreateRingTest()
        {
            string dummyRing = "create ring{PTV_CSI,1.5,2.0,600}";
            TSRingStructureModel expected = new TSRingStructureModel("PTV_CSI", 1.5, 2.0, 600);
            RingModelComparer comparer = new RingModelComparer();
            Assert.IsTrue(comparer.Equals(expected, ConfigurationHelper.ParseCreateRing(dummyRing)));
        }

        [TestMethod()]
        public void ParseTargetsTest()
        {
            string dummyTarget = "add target{PTV_CSI,1200,CSI-init}";
            PlanTargetsModel expected = new PlanTargetsModel("CSI-init", new TargetModel("PTV_CSI", 1200));
            PlanTargetModelComparer comparer = new PlanTargetModelComparer();
            Assert.IsTrue(comparer.Equals(expected, ConfigurationHelper.ParseTargets(dummyTarget)));
        }

        [TestMethod()]
        public void ParseTSManipulationTest()
        {
            List<string> dummyTSManipulations = new List<string>
            {
                "add TS manipulation{Lenses,Crop from body,0.0}",
                "add TS manipulation{Lungs,Contour substructure,-1.0}",
                "add TS manipulation{Brainstem,Crop target from structure,0.0}",
                "add TS manipulation{Eyes,Contour overlap with target,0.0}",
                "add TS manipulation{skin,Crop target from structure,3.0}"
            };
            List<RequestedTSManipulationModel> expected = new List<RequestedTSManipulationModel>
            {
                new RequestedTSManipulationModel("Lenses", Enums.TSManipulationType.CropFromBody,0),
                new RequestedTSManipulationModel("Lungs", Enums.TSManipulationType.ContourSubStructure, -1),
                new RequestedTSManipulationModel("Brainstem", Enums.TSManipulationType.CropTargetFromStructure,0),
                new RequestedTSManipulationModel("Eyes", Enums.TSManipulationType.ContourOverlapWithTarget, 0),
                new RequestedTSManipulationModel("skin", Enums.TSManipulationType.CropTargetFromStructure, 3)
            };

            TSManipulationComparer comparer = new TSManipulationComparer();
            for (int i = 0; i < expected.Count; i++)
            {
                RequestedTSManipulationModel resultTMP = ConfigurationHelper.ParseTSManipulation(dummyTSManipulations.ElementAt(i));
                Console.WriteLine($"{comparer.Print(expected.ElementAt(i))} | {comparer.Print(resultTMP)}");
                Assert.IsTrue(comparer.Equals(expected.ElementAt(i), resultTMP));
            }
        }

        [TestMethod()]
        public void ParseOptimizationConstraintTest()
        {
            List<string> dummyConstraints = new List<string>
            {
                "add opt constraint{ PTV_Body,Lower,1200.0,100.0,100}",
                "add opt constraint{ PTV_Body,Upper,1212.0,0.0,100}",
                "add opt constraint{ PTV_Body,Lower,1202.0,98.0,100}",
                "add opt constraint{ Kidneys,Mean,750.0,0.0,80}",
                "add opt constraint{ Kidneys - 1.0cm,Mean,400.0,0.0,50}",
                "add opt constraint{ Lenses,Mean,1140.0,0.0,50}",
                "add opt constraint{ Lungs,Mean,600.0,0.0,90}",
                "add opt constraint{ Lungs - 1.0cm,Mean,300.0,0.0,80}",
                "add opt constraint{ Lungs - 2.0cm,Mean,200.0,0.0,70}",
                "add opt constraint{ Bowel,Upper,1205.0,0.0,50}"
            };

            List<OptimizationConstraintModel> expected = new List<OptimizationConstraintModel>
            {
                new OptimizationConstraintModel("PTV_Body", Enums.OptimizationObjectiveType.Lower, 1200, Enums.Units.cGy, 100.0, 100),
                new OptimizationConstraintModel("PTV_Body", Enums.OptimizationObjectiveType.Upper, 1212, Enums.Units.cGy, 0.0, 100),
                new OptimizationConstraintModel("PTV_Body", Enums.OptimizationObjectiveType.Lower, 1202, Enums.Units.cGy, 98.0, 100),
                new OptimizationConstraintModel("Kidneys", Enums.OptimizationObjectiveType.Mean, 750, Enums.Units.cGy, 0.0, 80),
                new OptimizationConstraintModel("Kidneys - 1.0cm", Enums.OptimizationObjectiveType.Mean, 400, Enums.Units.cGy, 0.0, 50),
                new OptimizationConstraintModel("Lenses", Enums.OptimizationObjectiveType.Mean, 1140, Enums.Units.cGy, 0.0, 50),
                new OptimizationConstraintModel("Lungs", Enums.OptimizationObjectiveType.Mean, 600, Enums.Units.cGy, 0.0, 90),
                new OptimizationConstraintModel("Lungs - 1.0cm", Enums.OptimizationObjectiveType.Mean, 300, Enums.Units.cGy, 0.0, 80),
                new OptimizationConstraintModel("Lungs - 2.0cm", Enums.OptimizationObjectiveType.Mean, 200, Enums.Units.cGy, 0.0, 70),
                new OptimizationConstraintModel("Bowel", Enums.OptimizationObjectiveType.Upper, 1205, Enums.Units.cGy, 0.0, 50),
            };

            OptimizationConstraintComparer comparer = new OptimizationConstraintComparer();
            for (int i = 0; i < expected.Count; i++)
            {
                OptimizationConstraintModel resultTMP = ConfigurationHelper.ParseOptimizationConstraint(dummyConstraints.ElementAt(i));
                Console.WriteLine($"{comparer.Print(expected.ElementAt(i))} | {comparer.Print(resultTMP)}");
                Assert.IsTrue(comparer.Equals(expected.ElementAt(i), resultTMP));
            }

        }

        [TestMethod()]
        public void ParseRequestedPlanDoseInfoTest()
        {
            List<string> dummyRequests = new List<string>
            {
                "add plan dose info{<plan>,Dmax,%}",
                "add plan dose info{<target>,Dmax,%}",
                "add plan dose info{<target>,Dmin,%}",
                "add plan dose info{<target>,DoseAtVolume,90,%,%}",
                "add plan dose info{<target>,VolumeAtDose,95,%,%}"
            };

            List<RequestedPlanMetricModel> expected = new List<RequestedPlanMetricModel>
            {
                new RequestedPlanMetricModel("<plan>", Enums.DVHMetric.Dmax, Enums.Units.Percent),
                new RequestedPlanMetricModel("<target>", Enums.DVHMetric.Dmax, Enums.Units.Percent),
                new RequestedPlanMetricModel("<target>", Enums.DVHMetric.Dmin, Enums.Units.Percent),
                new RequestedPlanMetricModel("<target>", Enums.DVHMetric.DoseAtVolume, 90.0,Enums.Units.Percent, Enums.Units.Percent),
                new RequestedPlanMetricModel("<target>", Enums.DVHMetric.VolumeAtDose, 95.0,Enums.Units.Percent, Enums.Units.Percent),
            };

            RequestedPlanMetricComparer comparer = new RequestedPlanMetricComparer();
            for (int i = 0; i < dummyRequests.Count; i++)
            {
                RequestedPlanMetricModel resultTmp = ConfigurationHelper.ParseRequestedPlanDoseInfo(dummyRequests.ElementAt(i));
                Console.WriteLine($"{comparer.Print(expected.ElementAt(i))} | {comparer.Print(resultTmp)}");
                Assert.IsTrue(comparer.Equals(expected.ElementAt(i), resultTmp));
            }
        }

        [TestMethod()]
        public void ParseOptTSCreationCriteriaTest()
        {
            List<string> dummyRequestOptStructures = new List<string>
            {
                "{}}",
                "{Dmax > 120 %}}",
                "{Dmax > 130 %, V 110 % > 20.0 %}}",
                "{Dmax > 130 %}}",
                "{Dmax > 120 %, Dmax > 107 %, V 110 % > 10 %}}",
                "{finalOpt, Dmax > 107 %, V 110 % > 10.0 %}}",
                "{finalOpt, Dmax > 110 %}}",
            };

            List<List<OptTSCreationCriteriaModel>> expected = new List<List<OptTSCreationCriteriaModel>>
            {
                new List<OptTSCreationCriteriaModel>{ },
                new List<OptTSCreationCriteriaModel>
                {
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.Dmax, Enums.InequalityOperator.GreaterThan, 120, Enums.Units.Percent)
                },
                new List<OptTSCreationCriteriaModel>
                {
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.Dmax, Enums.InequalityOperator.GreaterThan, 130, Enums.Units.Percent),
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.VolumeAtDose, 110, Enums.Units.Percent, Enums.InequalityOperator.GreaterThan, 20, Enums.Units.Percent)
                },
                new List<OptTSCreationCriteriaModel>
                {
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.Dmax, Enums.InequalityOperator.GreaterThan, 130, Enums.Units.Percent)
                },
                new List<OptTSCreationCriteriaModel>
                {
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.Dmax, Enums.InequalityOperator.GreaterThan, 120, Enums.Units.Percent),
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.Dmax, Enums.InequalityOperator.GreaterThan, 107, Enums.Units.Percent),
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.VolumeAtDose, 110, Enums.Units.Percent, Enums.InequalityOperator.GreaterThan, 10, Enums.Units.Percent)
                },
                new List<OptTSCreationCriteriaModel>
                {
                   new OptTSCreationCriteriaModel(true),
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.Dmax, Enums.InequalityOperator.GreaterThan, 107, Enums.Units.Percent),
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.VolumeAtDose, 110, Enums.Units.Percent, Enums.InequalityOperator.GreaterThan, 10, Enums.Units.Percent)
                },
                new List<OptTSCreationCriteriaModel>
                {
                   new OptTSCreationCriteriaModel(true),
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.Dmax, Enums.InequalityOperator.GreaterThan, 110, Enums.Units.Percent),
                },
            };

            OptTSCreationCriteriaComparer comparer = new OptTSCreationCriteriaComparer();
            for (int i = 0; i < dummyRequestOptStructures.Count; i++)
            {
                List<OptTSCreationCriteriaModel> resultTmp = ConfigurationHelper.ParseOptTSCreationCriteria(dummyRequestOptStructures[i]);
                if (expected.ElementAt(i).Count == resultTmp.Count)
                {
                    if (resultTmp.Any())
                    {
                        for (int j = 0; j < resultTmp.Count; j++)
                        {
                            Console.WriteLine($"{comparer.Print(expected.ElementAt(i).ElementAt(j))} | {comparer.Print(resultTmp.ElementAt(j))}");
                            Assert.IsTrue(comparer.Equals(expected.ElementAt(i).ElementAt(j), resultTmp.ElementAt(j)));
                        }
                    }
                    else Console.WriteLine("No creation criteria present");
                }
                else
                {
                    Console.WriteLine($"Error! number of expected and acutal elements on itr {i} do not match! {expected.ElementAt(i).Count} vs {resultTmp.Count}");
                    Assert.Fail();
                }
                Console.WriteLine("-----------------------------------------------");
            }
        }

        [TestMethod()]
        public void ParseOptimizationTSstructureTest()
        {
            List<string> dummyRequestOptStructures = new List<string>
            {
                "add optimization TS structure{TS_heater90,90.0,100.0,100.0,60,{}}",
                "add optimization TS structure{TS_heater80,80.0,90.0,100.0,70,{Dmax > 120 %}}",
                "add optimization TS structure{TS_heater70,70.0,80.0,100.0,80,{Dmax > 130 %, V 110 % > 20.0 %}}",
                "add optimization TS structure{TS_cooler120,110.0,108.0,0.0,80,{Dmax > 130 %}}",
                "add optimization TS structure{TS_cooler110,110.0,108,0.0,80,{Dmax > 120 %, Dmax > 107 %, V 110 % > 10 %}}",
                "add optimization TS structure{TS_cooler105,105.0,101.0,0.0,70,{finalOpt, Dmax > 107 %,V 110 % > 10.0 %}}",
                "add optimization TS structure{TS_cooler107,107.0,102.0,0.0,70,{finalOpt, Dmax > 110 %}}",
            };

            List<RequestedOptimizationTSStructureModel> expected = new List<RequestedOptimizationTSStructureModel>
            {
                new TSHeaterStructureModel("TS_heater90", 90, 100, 60, new List<OptTSCreationCriteriaModel>{ }),
                new TSHeaterStructureModel("TS_heater80", 80, 90, 70, new List<OptTSCreationCriteriaModel>
                {
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.Dmax, Enums.InequalityOperator.GreaterThan, 120, Enums.Units.Percent)
                }),
                new TSHeaterStructureModel("TS_heater70", 70, 80, 80, new List<OptTSCreationCriteriaModel>
                {
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.Dmax, Enums.InequalityOperator.GreaterThan, 130, Enums.Units.Percent),
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.VolumeAtDose, 110, Enums.Units.Percent, Enums.InequalityOperator.GreaterThan, 20, Enums.Units.Percent)
                }),
                new TSCoolerStructureModel("TS_cooler120", 110, 108, 80, new List<OptTSCreationCriteriaModel>
                {
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.Dmax, Enums.InequalityOperator.GreaterThan, 130, Enums.Units.Percent)
                }),
                new TSCoolerStructureModel("TS_cooler110", 110, 108, 80, new List<OptTSCreationCriteriaModel>
                {
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.Dmax, Enums.InequalityOperator.GreaterThan, 120, Enums.Units.Percent),
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.Dmax, Enums.InequalityOperator.GreaterThan, 107, Enums.Units.Percent),
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.VolumeAtDose, 110, Enums.Units.Percent, Enums.InequalityOperator.GreaterThan, 10, Enums.Units.Percent)
                }),
                new TSCoolerStructureModel("TS_cooler105", 105, 101, 70, new List<OptTSCreationCriteriaModel>
                {
                   new OptTSCreationCriteriaModel(true),
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.Dmax, Enums.InequalityOperator.GreaterThan, 107, Enums.Units.Percent),
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.VolumeAtDose, 110, Enums.Units.Percent, Enums.InequalityOperator.GreaterThan, 10, Enums.Units.Percent)
                }),
                new TSCoolerStructureModel("TS_cooler107", 107, 102, 70, new List<OptTSCreationCriteriaModel>
                {
                   new OptTSCreationCriteriaModel(true),
                   new OptTSCreationCriteriaModel(Enums.DVHMetric.Dmax, Enums.InequalityOperator.GreaterThan, 110, Enums.Units.Percent),
                }),
            };

            RequestedOptTSStructureComparer comparer = new RequestedOptTSStructureComparer();
            for (int i = 0; i < dummyRequestOptStructures.Count; i++)
            {
                RequestedOptimizationTSStructureModel resultTmp = ConfigurationHelper.ParseOptimizationTSstructure(dummyRequestOptStructures[i]);
                Console.WriteLine("Expected:");
                Console.WriteLine(comparer.Print(expected.ElementAt(i)));

                Console.WriteLine("Result:");
                Console.WriteLine(comparer.Print(resultTmp));
                Console.WriteLine("-----------------------------------------------");
                Console.WriteLine("-----------------------------------------------");

                Assert.IsTrue(comparer.Equals(expected.ElementAt(i), resultTmp));
            }
        }

        [TestMethod()]
        public void ParsePlanObjectiveTest()
        {
            List<string> dummyPlanObj = new List<string>
            {
                "add plan objective{PTV_Boost,Lower,100.0,95.0,Relative}",
                "add plan objective{PTV_Boost,Upper,110.0,0.0,Relative}",
                "add plan objective{PTV_CSI,Lower,3600.0,95.0,cGy}",
                "add plan objective{PTV_CSI,Upper,3750.0,0.0,cGy}",
                "add plan objective{Brainstem,Upper,104.0,0.0,Relative}",
                "add plan objective{OpticChiasm,Upper,100.0,0.0,Relative}",
                "add plan objective{SpinalCord,Upper,104.0,0.0,Relative}",
                "add plan objective{OpticNrvs,Upper,100.0,0.0,Relative}",
                "add plan objective{Cochleas,Upper,102.0,0.0,Relative}",
                "add plan objective{Cochleas,Mean,3700.0,0.0,cGy}",
                "add plan objective{Parotids,Mean,1500.0,0.0,cGy}",
                "add plan objective{Pituitary,Upper,110.0,0.0,Relative}",
                "add plan objective{Eyes,Upper,4500,0.0,cGy}",
                "add plan objective{Eyes,Mean,3600,0.0,cGy}"
            };

            List<PlanObjectiveModel> expected = new List<PlanObjectiveModel>
            {
                new PlanObjectiveModel("PTV_Boost", Enums.OptimizationObjectiveType.Lower, 100, Enums.Units.Percent, 95),
                new PlanObjectiveModel("PTV_Boost", Enums.OptimizationObjectiveType.Upper, 110, Enums.Units.Percent, 0),
                new PlanObjectiveModel("PTV_CSI", Enums.OptimizationObjectiveType.Lower, 3600, Enums.Units.cGy, 95),
                new PlanObjectiveModel("PTV_CSI", Enums.OptimizationObjectiveType.Upper, 3750, Enums.Units.cGy, 0),
                new PlanObjectiveModel("Brainstem", Enums.OptimizationObjectiveType.Upper, 104, Enums.Units.Percent, 0),
                new PlanObjectiveModel("OpticChiasm", Enums.OptimizationObjectiveType.Upper, 100, Enums.Units.Percent, 0),
                new PlanObjectiveModel("SpinalCord", Enums.OptimizationObjectiveType.Upper, 104, Enums.Units.Percent, 0),
                new PlanObjectiveModel("OpticNrvs", Enums.OptimizationObjectiveType.Upper, 100, Enums.Units.Percent, 0),
                new PlanObjectiveModel("Cochleas", Enums.OptimizationObjectiveType.Upper, 102, Enums.Units.Percent, 0),
                new PlanObjectiveModel("Cochleas", Enums.OptimizationObjectiveType.Mean, 3700, Enums.Units.cGy, 0),
                new PlanObjectiveModel("Parotids", Enums.OptimizationObjectiveType.Mean, 1500, Enums.Units.cGy, 0),
                new PlanObjectiveModel("Pituitary", Enums.OptimizationObjectiveType.Upper, 110, Enums.Units.Percent, 0),
                new PlanObjectiveModel("Eyes", Enums.OptimizationObjectiveType.Upper, 4500, Enums.Units.cGy, 0),
                new PlanObjectiveModel("Eyes", Enums.OptimizationObjectiveType.Mean, 3600, Enums.Units.cGy, 0),
            };

            PlanObjectiveModelComparer comparer = new PlanObjectiveModelComparer();
            for(int i = 0; i < dummyPlanObj.Count; i++)
            {
                PlanObjectiveModel resultTmp = ConfigurationHelper.ParsePlanObjective(dummyPlanObj[i]);
                Console.WriteLine($"{comparer.Print(expected.ElementAt(i))} | {comparer.Print(resultTmp)}");
                Assert.IsTrue(comparer.Equals(expected.ElementAt(i), resultTmp));
            }
        }
    }
}

//Unit testing a private method
//[TestMethod]
//public void IsLifeBeautiful_returns_true_when_your_name_is_God()
//{
//    God sut = new God();
//    object[] parameters = { "God" };
//    PrivateObject po = new PrivateObject(sut);

//    var returnValue = po.Invoke("IsLifeBeautiful", parameters);

//    Assert.IsTrue((bool)returnValue);
//}