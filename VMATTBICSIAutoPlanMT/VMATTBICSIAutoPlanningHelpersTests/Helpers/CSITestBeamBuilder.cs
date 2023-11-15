using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telerik.JustMock;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.Tests.Helpers
{
    public class CSITestBeamBuilder
    {
        private static List<MetersetValue> BuildTestMetersetValues(int numIsos)
        {
            if (numIsos == 2)
            {
                return new List<MetersetValue>
                {
					//Brain
					new MetersetValue(10,DosimeterUnit.MU),
                    new MetersetValue(15,DosimeterUnit.MU),
					//Low Spine
					new MetersetValue(35,DosimeterUnit.MU),
                };
            }
            else if (numIsos == 3)
            {
                return new List<MetersetValue>
                {
					//Brain
					new MetersetValue(10,DosimeterUnit.MU),
                    new MetersetValue(15,DosimeterUnit.MU),
					//Up spine
					new MetersetValue(20,DosimeterUnit.MU),
					//Low Spine
					new MetersetValue(35,DosimeterUnit.MU),
                };
            }
            else return new List<MetersetValue> { };
        }

        private static List<VVector> BuildTestIsocenterPositions(int numIsos)
        {
            if (numIsos == 2)
            {
                return new List<VVector>
                {
					//brain
					new VVector(0,0, -10),
                    new VVector(0,0, -10),
					//Low spine
					new VVector(0,0, -25),
                };
            }
            else if (numIsos == 3)
            {
                return new List<VVector>
                {
					//Brain
					new VVector(0,0, -10),
                    new VVector(0,0, -10),
					//Up spine
					new VVector(0,0, -15),
					//Low spine
					new VVector(0,0, -25),
                };
            }
            else return new List<VVector> { };
        }

        public static List<List<Beam>> GetExpectedBeamListGroupedByZPos(List<Beam> beams)
        {
            if (beams.Count == 3)
            {
                return new List<List<Beam>>
                {
                    new List<Beam> { beams.ElementAt(0), beams.ElementAt(1)},
                    new List<Beam> { beams.ElementAt(2)}
                };
            }
            else if (beams.Count == 4)
            {
                return new List<List<Beam>>
                {
                    new List<Beam> { beams.ElementAt(0), beams.ElementAt(1)},
                    new List<Beam> { beams.ElementAt(2)},
                    new List<Beam> { beams.ElementAt(3)},
                };
            }
            else
            {
                return new List<List<Beam>> { };
            }
        }

        public static List<string> GetExpectedIsoNameListGroupedByZPos(int numBeams)
        {
            if (numBeams == 3)
            {
                return new List<string>
                {
                    "Brain",
                    "Low spine",
                };
            }
            else if (numBeams == 4)
            {
                return new List<string>
                {
                    "Brain",
                    "Up spine",
                    "Low spine",
                };
            }
            else
            {
                return new List<string> { };
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

        public static List<Beam> GenerateTestBeamSet(int numIsos)
        {
            List<VVector> testIsoPositions = BuildTestIsocenterPositions(numIsos);
            List<MetersetValue> testMUValues = BuildTestMetersetValues(numIsos);
            List<Beam> beams = BuildBeamList(testIsoPositions, testMUValues);

            //test data, expected
            return beams;
        }

        public static List<ExternalPlanSetup> GenerateTestPlanSet(int numIsos)
        {
            List<ExternalPlanSetup> planList = new List<ExternalPlanSetup> { };
            List<Beam> beams = GenerateTestBeamSet(numIsos);
            List<List<Beam>> groupedBeams = GetExpectedBeamListGroupedByZPos(beams);
            for (int i = 0; i < groupedBeams.Count; i++)
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
