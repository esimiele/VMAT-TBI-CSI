using System;
using System.Linq;
using System.Text;
using System.Windows.Media.Media3D;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace VMATTBICSIAutoPlanningHelpers.Helpers
{
    public static class ContourHelper
    {
        /// <summary>
        /// Helper method to crop the given structure from the body structure
        /// </summary>
        /// <param name="theStructure"></param>
        /// <param name="selectedSS"></param>
        /// <param name="marginInCm"></param>
        /// <returns></returns>
        public static (bool, StringBuilder) CropStructureFromBody(Structure theStructure, StructureSet selectedSS, double marginInCm)
        {
            StringBuilder sb = new StringBuilder();
            bool fail = false;
            //margin is in cm
            Structure body = StructureTuningHelper.GetStructureFromId("Body", selectedSS);
            if (body != null)
            {
                if (marginInCm >= -5.0 && marginInCm <= 5.0) theStructure.SegmentVolume = theStructure.SegmentVolume.And(body.SegmentVolume.Margin(marginInCm * 10));
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

        /// <summary>
        /// Helper method to crop or subtract one structure from another ONTO structureToCrop
        /// </summary>
        /// <param name="structureToCrop"></param>
        /// <param name="baseStructure"></param>
        /// <param name="marginInCm"></param>
        /// <returns></returns>
        public static (bool, StringBuilder) CropStructureFromStructure(Structure structureToCrop, Structure baseStructure, double marginInCm)
        {
            StringBuilder sb = new StringBuilder();
            bool fail = false;
            //margin is in cm
            if (structureToCrop != null && baseStructure != null)
            {
                if (marginInCm >= -5.0 && marginInCm <= 5.0) structureToCrop.SegmentVolume = structureToCrop.SegmentVolume.Sub(baseStructure.SegmentVolume.Margin(marginInCm * 10));
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

        /// <summary>
        /// Contour overlap between two structures ONTO the normal structure
        /// </summary>
        /// <param name="target"></param>
        /// <param name="normal"></param>
        /// <param name="marginInCm"></param>
        /// <returns></returns>
        public static (bool, StringBuilder) ContourOverlap(Structure target, Structure normal, double marginInCm)
        {
            StringBuilder sb = new StringBuilder();
            bool fail = false;
            //margin is in cm
            if (target != null && normal != null)
            {
                if (marginInCm >= -5.0 && marginInCm <= 5.0) normal.SegmentVolume = target.SegmentVolume.And(normal.SegmentVolume.Margin(marginInCm * 10));
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

        /// <summary>
        /// Helper method to contour the union between two structures ONTO structureToUnion
        /// </summary>
        /// <param name="baseStructure"></param>
        /// <param name="structureToUnion"></param>
        /// <param name="marginInCm"></param>
        /// <returns></returns>
        public static (bool, StringBuilder) ContourUnion(Structure baseStructure, Structure structureToUnion, double marginInCm)
        {
            StringBuilder sb = new StringBuilder();
            bool fail = false;
            //margin is in cm
            if (baseStructure != null && structureToUnion != null)
            {
                if (marginInCm >= -5.0 && marginInCm <= 5.0) structureToUnion.SegmentVolume = baseStructure.SegmentVolume.Or(structureToUnion.SegmentVolume.Margin(marginInCm * 10));
                else
                {
                    sb.AppendLine("Added margin MUST be within +/- 5.0 cm!");
                    fail = true;
                }
            }
            else
            {
                sb.AppendLine("Error either target or normal structures are missing! Can't union target and normal structure!");
                fail = true;
            }
            return (fail, sb);
        }

        /// <summary>
        /// Helper mthod to create a ring structure from the supplied target structure using specified margin and thickness values
        /// </summary>
        /// <param name="target"></param>
        /// <param name="ring"></param>
        /// <param name="selectedSS"></param>
        /// <param name="marginInCm"></param>
        /// <param name="thickness"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Helper method to contour a PRV volume from the supplied base structure id using specified margin
        /// </summary>
        /// <param name="baseStructureId"></param>
        /// <param name="PRVStructure"></param>
        /// <param name="selectedSS"></param>
        /// <param name="marginInCm"></param>
        /// <returns></returns>
        public static (bool, StringBuilder) ContourPRVVolume(string baseStructureId, Structure PRVStructure, StructureSet selectedSS, double marginInCm)
        {
            StringBuilder sb = new StringBuilder();
            bool fail = false;
            Structure baseStructure = StructureTuningHelper.GetStructureFromId(baseStructureId, selectedSS);
            if (baseStructure != null)
            {
                if (marginInCm >= -5.0 && marginInCm <= 5.0) PRVStructure.SegmentVolume = baseStructure.SegmentVolume.Margin(marginInCm * 10);
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

        /// <summary>
        /// Helper method to copy the supplied base structure ONTO the structureToContour
        /// </summary>
        /// <param name="baseStructure"></param>
        /// <param name="structureToContour"></param>
        /// <returns></returns>
        public static (bool, StringBuilder) CopyStructureOntoStructure(Structure baseStructure, Structure structureToContour)
        {
            StringBuilder sb = new StringBuilder();
            bool fail = false;
            try
            {
                structureToContour.SegmentVolume = baseStructure.SegmentVolume.Margin(0.0);
            }
            catch(Exception e)
            {
                sb.AppendLine($"Error! Could not copy {baseStructure.Id} onto {structureToContour.Id} because:");
                sb.AppendLine(e.Message);
            }
            return (fail, sb);
        }

        /// <summary>
        /// Helper method to contour the overlap between two structures and union it with the unionStructure ONTO the unionStructure
        /// </summary>
        /// <param name="target"></param>
        /// <param name="normal"></param>
        /// <param name="unionStructure"></param>
        /// <param name="selectedSS"></param>
        /// <param name="marginInCm"></param>
        /// <returns></returns>
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
                    unionStructure.SegmentVolume = unionStructure.SegmentVolume.Or(dummy.SegmentVolume.Margin(0.0));
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

        //public static (bool, StringBuilder) CreateTargetStructure(string targetStructureId, string baseStructureId, StructureSet selectedSS, AxisAlignedMargins margin, string alternateBasStructureId = "")
        //{
        //    StringBuilder sb = new StringBuilder();
        //    bool fail = false;
        //    sb.AppendLine($"Failed to find {targetStructureId} Structure! Retrieving {baseStructureId} structure");
        //    Structure baseStructure = StructureTuningHelper.GetStructureFromId(baseStructureId, selectedSS);
        //    if (baseStructure == null && !string.IsNullOrEmpty(alternateBasStructureId))
        //    {
        //        baseStructure = StructureTuningHelper.GetStructureFromId(alternateBasStructureId, selectedSS); 
        //    }
        //    if (baseStructure == null)
        //    {
        //        sb.AppendLine($"Could not retrieve base structure {baseStructureId}. Exiting!");
        //        fail = true;
        //        return (fail, sb);
        //    }
        //    sb.AppendLine($"Creating {targetStructureId} structure!");
        //    if (selectedSS.CanAddStructure("CONTROL", $"{targetStructureId}"))
        //    {
        //        Structure target = selectedSS.AddStructure("CONTROL", $"{targetStructureId}");
        //        target.SegmentVolume = baseStructure.AsymmetricMargin(margin);
        //        sb.AppendLine($"Created {targetStructureId} structure!");
        //    }
        //    else
        //    {
        //        sb.AppendLine($"Failed to add {targetStructureId} to the structure set! Exiting!");
        //        fail = true;
        //        return (fail, sb);
        //    }
        //    return (fail, sb);
        //}

        /// <summary>
        /// Helper method to generate a margin for a structure on the given CT slice using the supplied contour points at the specified distance
        /// </summary>
        /// <param name="points"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Get the maximum lateral project of the supplied structure in the anterior-posterior and lateral directions at the given isocenter location
        /// </summary>
        /// <param name="target"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        public static (double, StringBuilder) GetMaxLatProjectionDistance(Structure target, VVector v)
        {
            StringBuilder sb = new StringBuilder();
            double maxDimension = 0;
            Point3DCollection pts = target.MeshGeometry.Positions;
            if (Math.Abs(pts.Max(p => p.X) - v.x) > maxDimension) maxDimension = Math.Abs(pts.Max(p => p.X) - v.x);
            if (Math.Abs(pts.Min(p => p.X) - v.x) > maxDimension) maxDimension = Math.Abs(pts.Min(p => p.X) - v.x);
            if (Math.Abs(pts.Max(p => p.Y) - v.y) > maxDimension) maxDimension = Math.Abs(pts.Max(p => p.Y) - v.y);
            if (Math.Abs(pts.Min(p => p.Y) - v.y) > maxDimension) maxDimension = Math.Abs(pts.Min(p => p.Y) - v.y);
            sb.AppendLine($"Iso position: ({v.x:0.0}, {v.y:0.0}, {v.z:0.0}) mm");
            sb.AppendLine($"Max lateral dimension: {maxDimension:0.0} mm");
            return (maxDimension, sb);
        }

        /// <summary>
        /// Get the maximum lateral projection of a structure in the anterior-posterior and lateral directions using the supplied bounding box for the structure at the given isocenter location
        /// </summary>
        /// <param name="boundingBox"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        public static (double, StringBuilder) GetMaxLatProjectionDistance(VVector[] boundingBox, VVector v)
        {
            StringBuilder sb = new StringBuilder();
            double maxDimension = 0;
            if (Math.Abs(boundingBox.Max(p => p.x) - v.x) > maxDimension) maxDimension = Math.Abs(boundingBox.Max(p => p.x) - v.x);
            if (Math.Abs(boundingBox.Min(p => p.x) - v.x) > maxDimension) maxDimension = Math.Abs(boundingBox.Min(p => p.x) - v.x);
            if (Math.Abs(boundingBox.Max(p => p.y) - v.y) > maxDimension) maxDimension = Math.Abs(boundingBox.Max(p => p.y) - v.y);
            if (Math.Abs(boundingBox.Min(p => p.y) - v.y) > maxDimension) maxDimension = Math.Abs(boundingBox.Min(p => p.y) - v.y);
            sb.AppendLine($"Iso position: ({v.x:0.0}, {v.y:0.0}, {v.z:0.0}) mm");
            sb.AppendLine($"Max lateral dimension: {maxDimension:0.0} mm");
            return (maxDimension, sb);
        }

        /// <summary>
        /// Create a 2D bounding box for the specified target in the ant-post and lateral directions with added margin (in cm)
        /// </summary>
        /// <param name="theStructure"></param>
        /// <param name="addedMargin"></param>
        /// <returns></returns>
        public static (VVector[], StringBuilder) GetLateralBoundingBoxForStructure(Structure theStructure, double addedMargin = 0.0)
        {
            StringBuilder sb = new StringBuilder();
            VVector[] boundingBox;

            Point3DCollection pts = theStructure.MeshGeometry.Positions;
            double xMax = pts.Max(p => p.X) + addedMargin * 10;
            double xMin = pts.Min(p => p.X) - addedMargin * 10;
            double yMax = pts.Max(p => p.Y) + addedMargin * 10;
            double yMin = pts.Min(p => p.Y) - addedMargin * 10;

            sb.AppendLine($"Lateral bounding box for structure: {theStructure.Id}");
            sb.AppendLine($"Added margin: {addedMargin} cm");
            sb.AppendLine($" xMax: {xMax}");
            sb.AppendLine($" xMin: {xMin}");
            sb.AppendLine($" yMax: {yMax}");
            sb.AppendLine($" yMin: {yMin}");

             boundingBox = new[] {
                                new VVector(xMax, yMax, 0),
                                new VVector(xMax, 0, 0),
                                new VVector(xMax, yMin, 0),
                                new VVector(0, yMin, 0),
                                new VVector(xMin, yMin, 0),
                                new VVector(xMin, 0, 0),
                                new VVector(xMin, yMax, 0),
                                new VVector(0, yMax, 0)};

            return (boundingBox, sb);
        }
    }
}
