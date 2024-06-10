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
            Assert.AreEqual(expected, result);
        }

        [TestMethod()]
        public void ParseCreateTSTestFail1()
        {
            string testCreateTS = "create TS{PTV,TS_Eyes}";
            RequestedTSStructureModel expected = new RequestedTSStructureModel("CONTROL", "TS_Eyes");
            RequestedTSStructureModel result = ConfigurationHelper.ParseCreateTS(testCreateTS);
            Assert.AreNotEqual(expected, result);
        }

        [TestMethod()]
        public void ParseDaemonSettingsTest()
        {
            string dummyDaemon = "Aria DB daemon={VMSDBD,10.151.176.60,51402}";
            DaemonModel expected = new DaemonModel("VMSDBD", "10.151.176.60", 51402);
            Assert.AreEqual(expected, ConfigurationHelper.ParseDaemonSettings(dummyDaemon));
        }

        [TestMethod()]
        public void ParseCreateRingTest()
        {
            string dummyRing = "create ring{PTV_CSI,1.5,2.0,600}";
            TSRingStructureModel expected = new TSRingStructureModel("PTV_CSI", 1.5, 2.0, 600);
            Assert.AreEqual(expected, ConfigurationHelper.ParseCreateRing(dummyRing));
        }

        [TestMethod()]
        public void ParseTargetsTest()
        {
            string dummyTarget = "add target{PTV_CSI,1200,CSI-init}";
            PlanTargetsModel expected = new PlanTargetsModel("PTV_CSI", new TargetModel("CSI-init", 1200));
            Assert.AreEqual(expected, ConfigurationHelper.ParseTargets(dummyTarget));
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

        public class OptTSCreationCriteriaComparer : IEqualityComparer<OptTSCreationCriteriaModel>
        {
            public string Print(OptTSCreationCriteriaModel x)
            {
                return $"{x.CreateForFinalOptimization} {x.DVHMetric} {x.Operator} {x.QueryValue} {x.QueryUnits} {x.Limit} {x.QueryResultUnits}";
            }

            public bool Equals(OptTSCreationCriteriaModel x, OptTSCreationCriteriaModel y)
            {
                if (x == null && y == null) return true;
                else if (x == null || y == null) return false;
                else if (object.ReferenceEquals(x, y)) return true;

                return x.CreateForFinalOptimization == y.CreateForFinalOptimization
                    && x.DVHMetric == y.DVHMetric
                    && x.Operator == y.Operator
                    && ((double.IsNaN(x.QueryValue) && double.IsNaN(y.QueryValue)) || CalculationHelper.AreEqual(x.QueryValue, y.QueryValue))
                    && x.QueryUnits == y.QueryUnits
                    && ((double.IsNaN(x.Limit) && double.IsNaN(y.Limit)) || CalculationHelper.AreEqual(x.Limit, y.Limit))
                    && x.QueryResultUnits == y.QueryResultUnits;
            }

            public bool Equals(IEnumerable<OptTSCreationCriteriaModel> x, IEnumerable<OptTSCreationCriteriaModel> y)
            {
                if (x == null && y == null) return true;
                else if (x == null || y == null) return false;
                else if (object.ReferenceEquals(x, y)) return true;

                List<bool> areEqual = new List<bool> { };
                if (x.Count() == y.Count())
                {
                    for (int i = 0; i < x.Count(); i++)
                    {
                        areEqual.Add(Equals(x.ElementAt(i), y.ElementAt(i)));
                    }
                }
                return areEqual.All(a => a);
            }

            public int GetHashCode(OptTSCreationCriteriaModel obj)
            {
                throw new NotImplementedException();
            }
        }

        [TestMethod()]
        public void ParseOptimizationTSstructureTest()
        {
            List<string> dummyRequestOptStructures = new List<string>
            {
                "add optimization TS structure{TS_heater90,90.0,100.0,100.0,60,{}}",
                "add optimization TS structure{ TS_heater80,80.0,90.0,100.0,70,{ Dmax > 120 %}}",
                "add optimization TS structure{ TS_heater70,70.0,80.0,100.0,80,{ Dmax > 130 %, V 110 % > 20.0 %}}",
                "add optimization TS structure{TS_cooler120,110.0,108.0,0.0,80,{Dmax > 130 %}}",
                "add optimization TS structure{ TS_cooler110,110.0,108,0.0,80,{ Dmax > 120 %, Dmax > 107 %, V 110 % > 10 %}}",
                "add optimization TS structure{ TS_cooler105,105.0,101.0,0.0,70,{ finalOpt, Dmax > 107 %,V 110 % > 10.0 %}}",
                "add optimization TS structure{ TS_cooler107,107.0,102.0,0.0,70,{ finalOpt, Dmax > 110 %}}",
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
            for(int i = 0; i < dummyRequestOptStructures.Count; i++)
            {
                RequestedOptimizationTSStructureModel resultTmp = ConfigurationHelper.ParseOptimizationTSstructure(dummyRequestOptStructures[i]);
                Console.WriteLine(comparer.Print(expected.ElementAt(i)));
                Console.WriteLine("-----------------------------------------------");

                Console.WriteLine(comparer.Print(resultTmp));
                Console.WriteLine("-----------------------------------------------");
                Console.WriteLine("-----------------------------------------------");

                Assert.IsTrue(comparer.Equals(expected.ElementAt(i), resultTmp));
            }
        }

        public class RequestedOptTSStructureComparer : IEqualityComparer<RequestedOptimizationTSStructureModel>
        {

            public string Print(RequestedOptimizationTSStructureModel x)
            {
                string result;
                OptimizationConstraintComparer optComparer = new OptimizationConstraintComparer();
                OptTSCreationCriteriaComparer creationComparer = new OptTSCreationCriteriaComparer();
                if (x.GetType() == typeof(TSCoolerStructureModel))
                {
                    result = $"{x.TSStructureId} {(x as TSCoolerStructureModel).UpperDoseValue}" + Environment.NewLine;
                }
                else
                {
                    result = $"{x.TSStructureId} {(x as TSHeaterStructureModel).UpperDoseValue} {(x as TSHeaterStructureModel).LowerDoseValue}" + Environment.NewLine;
                }
                foreach (OptimizationConstraintModel itr in x.Constraints) result += $"{optComparer.Print(itr)}" + Environment.NewLine;
                foreach (OptTSCreationCriteriaModel itr in x.CreationCriteria) result += $"{creationComparer.Print(itr)}" + Environment.NewLine;
                return result;
            }

            public bool Equals(RequestedOptimizationTSStructureModel x, RequestedOptimizationTSStructureModel y)
            {
                if (x == null && y == null) return true;
                else if (x == null || y == null) return false;
                else if (object.ReferenceEquals(x, y)) return true;

                OptimizationConstraintComparer optComparer = new OptimizationConstraintComparer();
                OptTSCreationCriteriaComparer creationComparer = new OptTSCreationCriteriaComparer();

                if (x.GetType() == typeof(TSCoolerStructureModel) && y.GetType() == typeof(TSCoolerStructureModel))
                {
                    return string.Equals(x.TSStructureId, x.TSStructureId)
                        && CalculationHelper.AreEqual((x as TSCoolerStructureModel).UpperDoseValue, (y as TSCoolerStructureModel).UpperDoseValue)
                        && optComparer.Equals(x.Constraints, y.Constraints)
                        && creationComparer.Equals(x.CreationCriteria, y.CreationCriteria);
                }
                else if (x.GetType() == typeof(TSHeaterStructureModel) && y.GetType() == typeof(TSHeaterStructureModel))
                {
                    return string.Equals(x.TSStructureId, x.TSStructureId)
                        && CalculationHelper.AreEqual((x as TSHeaterStructureModel).UpperDoseValue, (y as TSHeaterStructureModel).UpperDoseValue)
                        && CalculationHelper.AreEqual((x as TSHeaterStructureModel).LowerDoseValue, (y as TSHeaterStructureModel).LowerDoseValue)
                        && optComparer.Equals(x.Constraints, y.Constraints)
                        && creationComparer.Equals(x.CreationCriteria, y.CreationCriteria);
                }
                else return false;
            }

            public int GetHashCode(RequestedOptimizationTSStructureModel obj)
            {
                throw new NotImplementedException();
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
    }

    public class RequestedPlanMetricComparer : IEqualityComparer<RequestedPlanMetricModel>
    {
        public string Print(RequestedPlanMetricModel x)
        {
            return $"{x.StructureId} {x.DVHMetric} {x.QueryValue} {x.QueryUnits} {x.QueryResultUnits}";
        }
        public bool Equals(RequestedPlanMetricModel x, RequestedPlanMetricModel y)
        {
            if (x == null && y == null) return true;
            else if (x == null || y == null) return false;
            else if (object.ReferenceEquals(x, y)) return true;

            return string.Equals(x.StructureId, y.StructureId)
                && x.DVHMetric == y.DVHMetric
                && ((double.IsNaN(x.QueryValue) && double.IsNaN(y.QueryValue)) || CalculationHelper.AreEqual(x.QueryValue, y.QueryValue))
                && x.QueryUnits == y.QueryUnits
                && x.QueryResultUnits == y.QueryResultUnits;
        }

        public int GetHashCode(RequestedPlanMetricModel obj)
        {
            throw new NotImplementedException();
        }
    }

    public class OptimizationConstraintComparer : IEqualityComparer<OptimizationConstraintModel>
    {
        public string Print(OptimizationConstraintModel c)
        {
            return $"{c.StructureId} {c.ConstraintType} {c.QueryDose} {c.QueryDoseUnits} {c.QueryVolume} {c.QueryVolumeUnits} {c.Priority}";
        }

        public bool Equals(OptimizationConstraintModel x, OptimizationConstraintModel y)
        {
            if (x == null && y == null) return true;
            else if (x == null || y == null) return false;
            else if (object.ReferenceEquals(x, y)) return true;

            return string.Equals(x.StructureId, y.StructureId)
                && x.ConstraintType == y.ConstraintType
                && CalculationHelper.AreEqual(x.QueryDose, y.QueryDose)
                && x.QueryDoseUnits == y.QueryDoseUnits
                && CalculationHelper.AreEqual(x.QueryVolume, y.QueryVolume)
                && x.QueryVolumeUnits == y.QueryVolumeUnits
                && x.Priority == y.Priority;
        }

        public bool Equals(IEnumerable<OptimizationConstraintModel> x, IEnumerable<OptimizationConstraintModel> y)
        {
            if (x == null && y == null) return true;
            else if (x == null || y == null) return false;
            else if (object.ReferenceEquals(x, y)) return true;
            List<bool> areEqual = new List<bool> { };
            if(x.Count() == y.Count())
            {
                for(int i = 0; i < x.Count(); i++)
                {
                    areEqual.Add(Equals(x.ElementAt(i), y.ElementAt(i)));
                }
            }
            return areEqual.All(a => a);
        }

        public int GetHashCode(OptimizationConstraintModel obj)
        {
            throw new NotImplementedException();
        }
    }

    public class TSManipulationComparer : IEqualityComparer<RequestedTSManipulationModel>
    {
        public string Print(RequestedTSManipulationModel x)
        {
            return $"{x.StructureId} {x.ManipulationType} {x.MarginInCM}";
        }
        public bool Equals(RequestedTSManipulationModel x, RequestedTSManipulationModel y)
        {
            if (x == null && y == null) return true;
            else if (x == null || y == null) return false;
            else if (object.ReferenceEquals(x,y)) return true;

            return string.Equals(x.StructureId, y.StructureId)
                && x.ManipulationType == y.ManipulationType
                && CalculationHelper.AreEqual(x.MarginInCM, y.MarginInCM);
        }

        public int GetHashCode(RequestedTSManipulationModel obj)
        {
            throw new NotImplementedException();
        }
    }
}