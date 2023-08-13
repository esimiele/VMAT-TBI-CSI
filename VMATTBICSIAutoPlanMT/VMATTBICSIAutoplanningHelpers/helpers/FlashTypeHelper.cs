using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class FlashTypeHelper
    {
        public static FlashType GetFlashType(string flashChoice)
        {
            if (string.Equals(flashChoice.ToLower(), "global")) return FlashType.Global;
            else return FlashType.Local;
        }
    }
}
