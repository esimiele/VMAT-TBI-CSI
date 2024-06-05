using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.Models
{
    public class IsocenterShiftModel
    {
        public VVector ShiftFromBBs { get; set; } = new VVector();
        public VVector ShiftFromPreviousIsocenter { get; set; } = new VVector();
        public IsocenterShiftModel() { }
        public IsocenterShiftModel(VVector bbShift)
        {
            ShiftFromBBs = bbShift;
        }
        public IsocenterShiftModel(VVector bbShift, VVector previousIsocenter)
        {
            ShiftFromBBs = bbShift;
            ShiftFromPreviousIsocenter = previousIsocenter;
        }
    }
}
