using VMATTBICSIAutoPlanningHelpers.Enums;

namespace VMATTBICSIAutoPlanningHelpers.EnumTypeHelpers
{
    public static class FlashTypeHelper
    {
        /// <summary>
        /// Simple helper method to convert the string representation of the flash type to the enum representation
        /// </summary>
        /// <param name="flashChoice"></param>
        /// <returns></returns>
        public static FlashType GetFlashType(string flashChoice)
        {
            if (string.Equals(flashChoice.ToLower(), "global")) return FlashType.Global;
            else return FlashType.Local;
        }
    }
}
