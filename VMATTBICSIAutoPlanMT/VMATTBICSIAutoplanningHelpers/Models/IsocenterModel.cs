using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class IsocenterModel
    {
        public string IsocenterId { get; set; } = string.Empty;
        public int NumberOfBeams { get; set; } = -1;
        public VVector IsocenterPosition { get; set; } = new VVector();

        public IsocenterModel() { }

        public IsocenterModel(string name)
        {
            IsocenterId = name;
        }

        public IsocenterModel(string name, int numBeams)
        {
            IsocenterId = name;
            NumberOfBeams = numBeams;
        }

        public IsocenterModel(string isocenterName, int numberOfBeams, VVector isocenterPosition)
        {
            IsocenterId = isocenterName;
            NumberOfBeams = numberOfBeams;
            IsocenterPosition = isocenterPosition;
        }
    }
}
