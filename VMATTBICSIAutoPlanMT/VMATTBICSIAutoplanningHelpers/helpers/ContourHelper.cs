using System;
using System.Linq;
using System.Text;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

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
            Structure baseStructure = StructureTuningHelper.GetStructureFromId(baseStructureId, selectedSS);
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

        public static (bool, StringBuilder) ContourOverlapAndUnion(Structure target, Structure normal, Structure unionStructure, StructureSet selectedSS, double marginInCm)
        {
            StringBuilder sb = new StringBuilder();
            bool fail = false;
            //margin is in cm
            if (target != null && normal != null)
            {
                if (marginInCm >= -5.0 && marginInCm <= 5.0)
                {
                    Structure dummy = selectedSS.AddStructure("CONTROL", "Dummy");
                    dummy.SegmentVolume = target.And(normal.Margin(marginInCm * 10));
                    unionStructure.SegmentVolume = unionStructure.Or(dummy.Margin(0.0));
                    selectedSS.RemoveStructure(dummy);
                }
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

        public static (bool, StringBuilder) CreateTargetStructure(string targetStructureId, string baseStructureId, StructureSet selectedSS, AxisAlignedMargins margin, string alternateBasStructureId = "")
        {
            StringBuilder sb = new StringBuilder();
            bool fail = false;
            sb.AppendLine($"Failed to find {targetStructureId} Structure! Retrieving {baseStructureId} structure");
            Structure baseStructure = StructureTuningHelper.GetStructureFromId(baseStructureId, selectedSS);
            if (baseStructure == null && !string.IsNullOrEmpty(alternateBasStructureId))
            {
                baseStructure = StructureTuningHelper.GetStructureFromId(alternateBasStructureId, selectedSS); 
            }
            if (baseStructure == null)
            {
                sb.AppendLine($"Could not retrieve base structure {baseStructureId}. Exiting!");
                fail = true;
                return (fail, sb);
            }
            sb.AppendLine($"Creating {targetStructureId} structure!");
            if (selectedSS.CanAddStructure("CONTROL", $"{targetStructureId}"))
            {
                Structure target = selectedSS.AddStructure("CONTROL", $"{targetStructureId}");
                target.SegmentVolume = baseStructure.AsymmetricMargin(margin);
                sb.AppendLine($"Created {targetStructureId} structure!");
            }
            else
            {
                sb.AppendLine($"Failed to add {targetStructureId} to the structure set! Exiting!");
                fail = true;
                return (fail, sb);
            }
            return (fail, sb);
        }

        public static VVector[] GenerateContourPoints(VVector[] points, double distance)
        {
            VVector[] newPoints = new VVector[points.GetLength(0)];
            double centerX = (points.Max(p => p.x) + points.Min(p => p.x)) / 2;
            double centerY = (points.Max(p => p.y) + points.Min(p => p.y)) / 2;
            for (int i = 0; i < points.GetLength(0); i++)
            {
                double r = Math.Sqrt(Math.Pow(points[i].x - centerX, 2) + Math.Pow(points[i].y - centerY, 2));
                VVector u = new VVector((points[i].x - centerX) / r, (points[i].y - centerY) / r, 0);
                newPoints[i] = new VVector(u.x * (r + distance) + centerX, u.y * (r + distance) + centerY, 0);
                //ProvideUIUpdate($"{points[i][j].x - centerX:0.00}, {points[i][j].y - centerY:0.00}, {r:0.00}, {u.x:0.00}, {u.y:0.00}, {centerX:0.00}, {centerY:0.00}");
            }
            return newPoints;
        }
    }
}
