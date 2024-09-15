using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telerik.JustMock;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.Helpers.Tests
{
    public static class TBITestBeamBuilder
    {
        private static List<MetersetValue> BuildLaterlTestMetersetValues(int numLatIsos)
        {
            if (numLatIsos == 1)
            {
                return new List<MetersetValue>
                {
					//head
					new MetersetValue(10,DosimeterUnit.MU),
                    new MetersetValue(15,DosimeterUnit.MU),
					//Mid chest
					new MetersetValue(20,DosimeterUnit.MU),
                    new MetersetValue(25,DosimeterUnit.MU),
                    new MetersetValue(30,DosimeterUnit.MU),
					//Mid abdomen
					new MetersetValue(35,DosimeterUnit.MU),
                    new MetersetValue(40,DosimeterUnit.MU),
                    new MetersetValue(45, DosimeterUnit.MU),
					//Pelvis
					new MetersetValue(50,DosimeterUnit.MU),
                    new MetersetValue(55,DosimeterUnit.MU),
                };
            }
            else if (numLatIsos == 2)
            {
                return new List<MetersetValue>
                {
					//head
					new MetersetValue(10,DosimeterUnit.MU),
                    new MetersetValue (15, DosimeterUnit.MU),
					//L chest
					new MetersetValue(20,DosimeterUnit.MU),
                    new MetersetValue(25, DosimeterUnit.MU),
                    new MetersetValue(30,DosimeterUnit.MU),
					//R chest
					new MetersetValue(35,DosimeterUnit.MU),
                    new MetersetValue(40,DosimeterUnit.MU),
                    new MetersetValue(45,DosimeterUnit.MU),
					//L Abdomen
					new MetersetValue(50,DosimeterUnit.MU),
                    new MetersetValue(55,DosimeterUnit.MU),
                    new MetersetValue(60,DosimeterUnit.MU),
					//R abdomen
					new MetersetValue(65,DosimeterUnit.MU),
                    new MetersetValue(70,DosimeterUnit.MU),
                    new MetersetValue(75,DosimeterUnit.MU),
					//Pelvis
					new MetersetValue(80,DosimeterUnit.MU),
                    new MetersetValue(85,DosimeterUnit.MU),
                };
            }
            else if (numLatIsos == 3)
            {
                return new List<MetersetValue>
                {
					//head
					new MetersetValue(10,DosimeterUnit.MU),
                    new MetersetValue (15, DosimeterUnit.MU),
					//L chest
					new MetersetValue(20,DosimeterUnit.MU),
                    new MetersetValue(25, DosimeterUnit.MU),
                    new MetersetValue(30,DosimeterUnit.MU),
					//Mid chest
					new MetersetValue(35,DosimeterUnit.MU),
                    new MetersetValue(40,DosimeterUnit.MU),
                    new MetersetValue(45,DosimeterUnit.MU),
					//R chest
					new MetersetValue(50,DosimeterUnit.MU),
                    new MetersetValue(55,DosimeterUnit.MU),
                    new MetersetValue(60,DosimeterUnit.MU),
					//L Abdomen
					new MetersetValue(65,DosimeterUnit.MU),
                    new MetersetValue(70,DosimeterUnit.MU),
                    new MetersetValue(75,DosimeterUnit.MU),
					//Mid abdomen
					new MetersetValue(80,DosimeterUnit.MU),
                    new MetersetValue(85,DosimeterUnit.MU),
                    new MetersetValue(90,DosimeterUnit.MU),
					//R abdomen
					new MetersetValue(95,DosimeterUnit.MU),
                    new MetersetValue(100,DosimeterUnit.MU),
                    new MetersetValue(105,DosimeterUnit.MU),
					//Pelvis
                    new MetersetValue(110,DosimeterUnit.MU),
                    new MetersetValue(115,DosimeterUnit.MU),
                };
            }
            else return new List<MetersetValue> { };
        }

        private static List<VVector> BuildLaterlTestIsocenterPositions(int numLatIsos)
        {
            if (numLatIsos == 1)
            {
                return new List<VVector>
                {
					//head
					new VVector(0,0, -10),
                    new VVector(0,0, -10),
					//Mid chest
					new VVector(0,0, -15),
                    new VVector(0,0, -15),
                    new VVector(0,0, -15),
					//Mid abdomen
					new VVector(0,0, -25),
                    new VVector(0,0, -25),
                    new VVector(0,0, -25),
					//Pelvis
					new VVector(0,0, -35),
                    new VVector(0,0, -35),
                };
            }
            else if (numLatIsos == 2)
            {
                return new List<VVector>
                {
					//head
					new VVector(0,0, -10),
                    new VVector(0,0, -10),
					//L chest
					new VVector(5,0, -15),
                    new VVector(5,0, -15),
                    new VVector(5,0, -15),
					//R chest
					new VVector(-5,0, -15),
                    new VVector(-5,0, -15),
                    new VVector(-5,0, -15),
					//L Abdomen
					new VVector(5,0, -25),
                    new VVector(5,0, -25),
                    new VVector(5,0, -25),
					//R abdomen
					new VVector(-5,0, -25),
                    new VVector(-5,0, -25),
                    new VVector(-5,0, -25),
					//Pelvis
					new VVector(0,0, -35),
                    new VVector(0,0, -35),
                };
            }
            else if (numLatIsos == 3)
            {
                return new List<VVector>
                {
					//head
					new VVector(0,0, -10),
                    new VVector(0,0, -10),
					//L chest
					new VVector(5,0, -15),
                    new VVector(5,0, -15),
                    new VVector(5,0, -15),
					//Mid chest
					new VVector(0,0, -15),
                    new VVector(0,0, -15),
                    new VVector(0,0, -15),
					//R chest
					new VVector(-5,0, -15),
                    new VVector(-5,0, -15),
                    new VVector(-5,0, -15),
					//L Abdomen
					new VVector(5,0, -25),
                    new VVector(5,0, -25),
                    new VVector(5,0, -25),
					//Mid abdomen
					new VVector(0,0, -25),
                    new VVector(0,0, -25),
                    new VVector(0,0, -25),
					//R abdomen
					new VVector(-5,0, -25),
                    new VVector(-5,0, -25),
                    new VVector(-5,0, -25),
					//Pelvis
					new VVector(0,0, -35),
                    new VVector(0,0, -35),
                };
            }
            else return new List<VVector> { };
        }

        public static List<List<Beam>> GetExpectedBeamListGroupedByZPos(List<Beam> beams)
        {
            if (beams.Count == 10)
            {
                return new List<List<Beam>>
                {
                    new List<Beam> { beams.ElementAt(0), beams.ElementAt(1)},
                    new List<Beam> { beams.ElementAt(2), beams.ElementAt(3), beams.ElementAt(4)},
                    new List<Beam> { beams.ElementAt(5), beams.ElementAt(6), beams.ElementAt(7)},
                    new List<Beam> { beams.ElementAt(8), beams.ElementAt(9)}
                };
            }
            else if (beams.Count == 16)
            {
                return new List<List<Beam>>
                {
                    new List<Beam> { beams.ElementAt(0), beams.ElementAt(1)},
                    new List<Beam> { beams.ElementAt(2), beams.ElementAt(3), beams.ElementAt(4), beams.ElementAt(5), beams.ElementAt(6), beams.ElementAt(7)},
                    new List<Beam> { beams.ElementAt(8), beams.ElementAt(9), beams.ElementAt(10), beams.ElementAt(11), beams.ElementAt(12), beams.ElementAt(13)},
                    new List<Beam> { beams.ElementAt(14), beams.ElementAt(15)},
                };
            }
            else
            {
                return new List<List<Beam>>
                {
                    new List<Beam> { beams.ElementAt(0), beams.ElementAt(1)},
                    new List<Beam> { beams.ElementAt(2), beams.ElementAt(3), beams.ElementAt(4), beams.ElementAt(5), beams.ElementAt(6), beams.ElementAt(7), beams.ElementAt(8), beams.ElementAt(9), beams.ElementAt(10)},
                    new List<Beam> { beams.ElementAt(11), beams.ElementAt(12), beams.ElementAt(13), beams.ElementAt(14), beams.ElementAt(15), beams.ElementAt(16), beams.ElementAt(17), beams.ElementAt(18), beams.ElementAt(19)},
                    new List<Beam> { beams.ElementAt(20), beams.ElementAt(21)},
                };
            }
        }

        public static List<List<Beam>> GetExpectedBeamListGroupedByZAndXPos(List<Beam> beams)
        {
            if (beams.Count == 10)
            {
                return new List<List<Beam>>
                {
                    new List<Beam> { beams.ElementAt(0), beams.ElementAt(1)},
                    new List<Beam> { beams.ElementAt(2), beams.ElementAt(3), beams.ElementAt(4)},
                    new List<Beam> { beams.ElementAt(5), beams.ElementAt(6), beams.ElementAt(7)},
                    new List<Beam> { beams.ElementAt(8), beams.ElementAt(9)}
                };
            }
            else if (beams.Count == 16)
            {
                return new List<List<Beam>>
                {
                    new List<Beam> { beams.ElementAt(0), beams.ElementAt(1)},
                    new List<Beam> { beams.ElementAt(2), beams.ElementAt(3), beams.ElementAt(4)},
                    new List<Beam> { beams.ElementAt(5), beams.ElementAt(6), beams.ElementAt(7)},
                    new List<Beam> { beams.ElementAt(11), beams.ElementAt(12), beams.ElementAt(13)},
                    new List<Beam> { beams.ElementAt(8), beams.ElementAt(9), beams.ElementAt(10)},
                    new List<Beam> { beams.ElementAt(14), beams.ElementAt(15)},
                };
            }
            else
            {
                return new List<List<Beam>>
                {
                    new List<Beam> { beams.ElementAt(0), beams.ElementAt(1)},
                    new List<Beam> { beams.ElementAt(5), beams.ElementAt(6), beams.ElementAt(7)},
                    new List<Beam> { beams.ElementAt(2), beams.ElementAt(3), beams.ElementAt(4)},
                    new List<Beam> { beams.ElementAt(8), beams.ElementAt(9), beams.ElementAt(10)},
                    new List<Beam> { beams.ElementAt(17), beams.ElementAt(18), beams.ElementAt(19)},
                    new List<Beam> { beams.ElementAt(14), beams.ElementAt(15), beams.ElementAt(16)},
                    new List<Beam> { beams.ElementAt(11), beams.ElementAt(12), beams.ElementAt(13)},
                    new List<Beam> { beams.ElementAt(20), beams.ElementAt(21)},
                };
            }
        }

        public static List<string> GetExpectedIsoNameListGroupedByZAndXPos(int numBeams)
        {
            if (numBeams == 10)
            {
                //VMAT only
                return new List<string>
                {
                    "Head",
                    "Chest",
                    "Pelvis",
                    "Legs"
                };
            }
            else if (numBeams == 16)
            {
                //VMAT with AP/PA
                return new List<string>
                {
                    "Head",
                    "L Chest",
                    "R Chest",
                    "R Abdomen",
                    "L Abdomen",
                    "Pelvis"
                };
            }
            else
            {
                //VMAT with AP/PA
                return new List<string>
                {
                    "Head",
                    "Mid Chest",
                    "L Chest",
                    "R Chest",
                    "R Abdomen",
                    "Mid Abdomen",
                    "L Abdomen",
                    "Pelvis"
                };
            }
        }

        public static List<Beam> BuildBeamList(List<VVector> testIsoPositions, List<MetersetValue> testMUvals)
        {
            List<Beam> beams = new List<Beam> { };
            for (int i = 0; i < testIsoPositions.Count; i++)
            {
                Beam b = Mock.Create<Beam>();
                Mock.Arrange(() => b.IsSetupField).Returns(false);
                Mock.Arrange(() => b.IsocenterPosition).Returns(testIsoPositions.ElementAt(i));
                Mock.Arrange(() => b.Meterset).Returns(testMUvals.ElementAt(i));
                beams.Add(b);
            }
            return beams;
        }

        public static List<Beam> GenerateVMATTestBeamSet(int numLatIsos)
        {
            List<VVector> testIsoPositions = BuildLaterlTestIsocenterPositions(numLatIsos);
            List<MetersetValue> testMUValues = BuildLaterlTestMetersetValues(numLatIsos);

            //test data, expected
            return BuildBeamList(testIsoPositions, testMUValues); 
        }

        public static List<VVector> BuildAPPATestIsoPositions(int numAPPAIsos)
        {
            if (numAPPAIsos == 1)
            {
                return new List<VVector>
                {
					//Upper Leg
					new VVector(0,0, -35),
                    new VVector(0,0, -35),
                };
            }
            else if (numAPPAIsos == 2)
            {
                return new List<VVector>
                {
					//Upper Leg
					new VVector(0,0, -35),
                    new VVector(0,0, -35),
                    //Lower leg
                    new VVector(0,0,-45),
                    new VVector(0,0,-45),
                };
            }
            else if (numAPPAIsos == 3)
            {
                return new List<VVector>
                {
					//Upper Leg
					new VVector(0,0, -35),
                    new VVector(0,0, -35),
                    //Mid Leg
                    new VVector(0,0,-45),
                    new VVector(0,0,-45),
                    //Lower leg
                    new VVector(0,0,-55),
                    new VVector(0,0,-55),
                };
            }
            else return new List<VVector> { };
        }

        public static List<MetersetValue> BuildAPPATestMetersetValues(int numAPPAIsos)
        {
            if (numAPPAIsos == 1)
            {
                return new List<MetersetValue>
                {
					//Upper Leg
					new MetersetValue(10,DosimeterUnit.MU),
                    new MetersetValue(15,DosimeterUnit.MU),
                };
            }
            else if (numAPPAIsos == 2)
            {
                return new List<MetersetValue>
                {
					//Upper Leg
					new MetersetValue(10,DosimeterUnit.MU),
                    new MetersetValue(15,DosimeterUnit.MU),
                    //Lower Leg
                    new MetersetValue(20,DosimeterUnit.MU),
                    new MetersetValue(25,DosimeterUnit.MU),
                };
            }
            else if (numAPPAIsos == 3)
            {
                return new List<MetersetValue>
                {
					//Upper Leg
					new MetersetValue(10,DosimeterUnit.MU),
                    new MetersetValue(15,DosimeterUnit.MU),
                    //Mid Leg
                    new MetersetValue(20,DosimeterUnit.MU),
                    new MetersetValue(25,DosimeterUnit.MU),
                    //Lower Leg
                    new MetersetValue(30,DosimeterUnit.MU),
                    new MetersetValue(35,DosimeterUnit.MU),
                };
            }
            else return new List<MetersetValue> { };
        }

        public static List<Beam> GenerateAPPATestBeamSet(int numAPPAIsos)
        {
            List<VVector> testIsoPositions = BuildAPPATestIsoPositions(numAPPAIsos);
            List<MetersetValue> testMUValues = BuildAPPATestMetersetValues(numAPPAIsos);
            return BuildBeamList(testIsoPositions, testMUValues);

        }

        public static List<ExternalPlanSetup> GenerateTestPlanSet(int numLatIsos)
        {
            List<ExternalPlanSetup> planList = new List<ExternalPlanSetup> { };
            List<Beam> beams = GenerateVMATTestBeamSet(numLatIsos);
            List<List<Beam>> groupedBeams = GetExpectedBeamListGroupedByZAndXPos(beams);
            for(int i = 0; i < groupedBeams.Count; i++)
            {
                ExternalPlanSetup plan = Mock.Create<ExternalPlanSetup>();
                Mock.Arrange(() => plan.Id).Returns($"{i}");
                Mock.Arrange(() => plan.Beams).Returns(groupedBeams.ElementAt(i));
                planList.Add(plan); 
            }
            return planList;
        }
    }
}
