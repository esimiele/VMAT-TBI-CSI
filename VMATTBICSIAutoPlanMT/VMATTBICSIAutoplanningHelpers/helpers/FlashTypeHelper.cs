using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class FlashTypeHelper
    {
        public static FlashType GetFlashType(string flashChoice)
        {
            flashChoice = flashChoice.ToLower();
            if (string.Equals(flashChoice, "global")) return FlashType.Global;
            else return FlashType.Local;
        }
    }
}
