using System.Linq;
using System.Text;
using VMS.TPS.Common.Model.API;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class ContourHelper
    {
        public static (bool, StringBuilder) CropStructureFromBody(Structure theStructure, StructureSet selectedSS, double marginInCm)
        {
            StringBuilder sb = new StringBuilder();
            bool fail = false;
            //margin is in cm
            Structure body = selectedSS.Structures.FirstOrDefault(x => string.Equals(x.Id.ToLower(), "body"));
            if (body != null)
            {
                if (marginInCm >= -5.0 && marginInCm <= 5.0) theStructure.SegmentVolume = theStructure.And(body.Margin(marginInCm * 10));
                else 
                { 
                    sb.AppendLine("Cropping margin from body MUST be within +/- 5.0 cm!"); 
                    fail = true; 
                }
            }
            else
            {
                sb.AppendLine("Could not find body structure! Can't crop target from body!");
                fail = true;
            }
            return (fail, sb);
        }

        public static (bool, StringBuilder) CropTargetFromStructure(Structure target, Structure normal, double marginInCm)
        {
            StringBuilder sb = new StringBuilder();
            bool fail = false;
            //margin is in cm
            if (target != null && normal != null)
            {
                if (marginInCm >= -5.0 && marginInCm <= 5.0) target.SegmentVolume = target.Sub(normal.Margin(marginInCm * 10));
                else 
                { 
                    sb.AppendLine("Cropping margin MUST be within +/- 5.0 cm!"); 
                    fail = true; 
                }
            }
            else
            {
                sb.AppendLine("Error either target or normal structures are missing! Can't crop target from normal structure!");
                fail = true;
            }
            return (fail, sb);
        }

        public static (bool, StringBuilder) ContourOverlap(Structure target, Structure normal, double marginInCm)
        {
            StringBuilder sb = new StringBuilder();
            bool fail = false;
            //margin is in cm
            if (target != null && normal != null)
            {
                if (marginInCm >= -5.0 && marginInCm <= 5.0) normal.SegmentVolume = target.And(normal.Margin(marginInCm * 10));
                else
                {
                    sb.AppendLine("Added margin MUST be within +/- 5.0 cm!");
                    fail = true;
                }
            }
            else
            {
                sb.AppendLine("Error either target or normal structures are missing! Can't contour overlap between target and normal structure!");
                fail = true;
            }
            return (fail, sb);
        }

        

        public static (bool, StringBuilder) CreateRing(Structure target, Structure ring, StructureSet selectedSS, double marginInCm, double thickness)
        {
            StringBuilder sb = new StringBuilder();
            bool fail = false;
            //margin is in cm
            if ((marginInCm >= -5.0 && marginInCm <= 5.0) && (thickness + marginInCm >= -5.0 && thickness + marginInCm <= 5.0))
            {
                ring.SegmentVolume = target.Margin((thickness + marginInCm) * 10);
                ring.SegmentVolume = ring.Sub(target.Margin(marginInCm * 10));
                CropStructureFromBody(ring, selectedSS, 0.0);
            }
            else
            {
                sb.AppendLine("Added margin or ring thickness + margin MUST be within +/- 5.0 cm! Exiting!");
                fail = true;
            }
            return (fail, sb);
        }

        public static (bool, StringBuilder) ContourPRVVolume(string baseStructureId, Structure addedStructure, StructureSet selectedSS, double marginInCm)
        {
            StringBuilder sb = new StringBuilder();
            bool fail = false;
            Structure baseStructure = selectedSS.Structures.FirstOrDefault(x => x.Id.ToLower() == baseStructureId.ToLower());
            if (baseStructure != null)
            {
                if (marginInCm >= -5.0 && marginInCm <= 5.0) addedStructure.SegmentVolume = baseStructure.Margin(marginInCm * 10);
                else
                {
                    sb.AppendLine($"Error! Requested PRV margin ({marginInCm:0.0} cm) is outside +/- 5 cm! Exiting!");
                    fail = true;
                }
            }
            else
            {
                sb.AppendLine($"Error! Cannot find base structure: {baseStructureId}! Exiting!");
                fail = true;
            }
            return (fail, sb);
        }
    }
}
