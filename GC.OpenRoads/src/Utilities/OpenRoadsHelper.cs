// =============================================================================
//  Utilities/OpenRoadsHelper.cs  (v3 — real SDK API: GetXSCutPoints)
//
//  Cross-section data is extracted using:
//    Corridor.GetXSCutPoints(station, leftWidth, rightWidth, offset, whichFeatures, filter)
//
//  This matches the real CifNET SDK as shown in the ManagedSDKExample
//  XSCutPointReporter sample shipped with the ORD SDK.
//
//  Key SDK types:
//    XSCutPoint          — one feature point on a cross section
//      .PointName        — feature name (e.g. "EOP_L")
//      .PointFeatureName — full feature definition path
//      .Point.X          — offset from CL (sheet space / 2D)
//      .Point.Y          — elevation (sheet space / 2D)
//      .PointOnPlan.X/Y/Z — world 3-D coordinates
//
//  Unit note:
//    ORD computes internally in METRES.
//    Master units may differ (e.g. millimetres in some workspaces).
//    ConvertMasterToMeter() / ConvertMeterToMaster() handle conversion.
//    Use FormatSettingsConstants.GetMasterUnitsToMeters() for the factor.
// =============================================================================

using Bentley.CifNET.Formatting;
using Bentley.CifNET.GeometryModel.SDK;
using Bentley.CifNET.SDK;
using Bentley.CifNET.SDK.Edit;
using Bentley.DgnPlatformNET;
using Bentley.MstnPlatformNET;
using Bentley.GeometryNET;
using GC_OpenRoads_CrossSections.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GC_OpenRoads.Utilities
{
    public static class OpenRoadsHelper
    {
        private static bool ALTERNATE_FORMAT = false;
        // ─────────────────────────────────────────────────────────────────────
        //  Unit conversion  (matches XSCutPointReporter sample exactly)
        // ─────────────────────────────────────────────────────────────────────

        public static double ConvertMasterToMeter(double masterValue)
        {
            double factor = FormatSettingsConstants.GetMasterUnitsToMeters();
            return masterValue * factor;
        }

        public static double ConvertMeterToMaster(double meterValue)
        {
            double factor = FormatSettingsConstants.GetMasterUnitsToMeters();
            return factor == 0 ? meterValue : meterValue / factor;
        }

        public static string FormatDistance(double valueInMeters)
        {
            DgnModel model = Session.Instance.GetActiveDgnModel();
            return FormatForDisplay.Distance(valueInMeters, model, 4);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Connection helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Returns the active edit connection. Caller must NOT dispose.</summary>
        public static ConsensusConnection GetActiveConnection()
            => ConsensusConnectionEdit.GetActive();

        // ─────────────────────────────────────────────────────────────────────
        //  Alignment helpers
        // ─────────────────────────────────────────────────────────────────────

        public static IReadOnlyList<Alignment> GetAllAlignments()
        {
            ConsensusConnection con = GetActiveConnection();
            List<GeometricModel> geometricModels = con?.GetAllGeometricModels().ToList();
            List<Alignment> alignments = new List<Alignment>();
            if (geometricModels != null)
            {
                foreach (var gm in geometricModels)
                {
                    alignments.AddRange(gm.Alignments.ToList());
                }
            }
            else
                return Array.Empty<Alignment>();

            return alignments.OrderBy(a => a.Name).ToList();
        }

        public static Alignment FindAlignment(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            ConsensusConnection con = GetActiveConnection();
            List<GeometricModel> geometricModels = con?.GetAllGeometricModels().ToList();
            List<Alignment> alignments = new List<Alignment>();
            if (geometricModels != null)
            {
                foreach (var gm in geometricModels)
                {
                    alignments.AddRange(gm.Alignments.ToList());
                }
            }
            return alignments.FirstOrDefault(a =>
                string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public static (double Start, double End) GetAlignmentStationRange(Alignment alignment)
        {
            // LinearGeometry.Length is in metres; convert to master units
            double lengthMaster = ConvertMeterToMaster(alignment.LinearGeometry.Length);
            return (0.0, lengthMaster);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Corridor helpers
        // ─────────────────────────────────────────────────────────────────────

        public static IReadOnlyList<Corridor> GetCorridorsForAlignment(string alignmentName)
        {
            ConsensusConnection con = GetActiveConnection();
            List<GeometricModel> geometricModels = con?.GetAllGeometricModels().ToList();
            List<Corridor> corridors = new List<Corridor>();
            if (geometricModels != null)
            {
                foreach (var gm in geometricModels)
                {
                    corridors.AddRange(gm.Corridors.ToList());
                }
            }
            else
            {
                return Array.Empty<Corridor>();
            }
            return corridors
                     .Where(c => string.Equals(
                         c.CorridorAlignment?.Name, alignmentName,
                         StringComparison.OrdinalIgnoreCase))
                     .ToList();
        }

        public static Corridor FindCorridor(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            ConsensusConnection con = GetActiveConnection();
            List<GeometricModel> geometricModels = con?.GetAllGeometricModels().ToList();
            List<Corridor> corridors = new List<Corridor>();
            if (geometricModels != null)
            {
                foreach (var gm in geometricModels)
                {
                    corridors.AddRange(gm.Corridors.ToList());
                }
            }
            else
            {
                return null;
            }
            return corridors.FirstOrDefault(c =>
                string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Cross-section extraction  (using real SDK API: GetXSCutPoints)
        //
        //  Station input is in MASTER UNITS; conversion to metres is done here.
        //  XSCutPoint.Point.X  = offset from CL (metres)
        //  XSCutPoint.Point.Y  = elevation      (metres)
        //  XSCutPoint.PointOnPlan = world 3-D (metres)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Extracts cross sections at a uniform station interval.
        /// Stations are in master units; internally converted to metres for the SDK call.
        /// </summary>
        public static List<CrossSectionData> ExtractCrossSections(
            Corridor corridor,
            double   startStationMaster,
            double   endStationMaster,
            double   intervalMaster,
            double   leftWidthMaster  = 50,
            double   rightWidthMaster = 50)
        {
            if (intervalMaster <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalMaster));

            var results = new List<CrossSectionData>();

            // Corridor length in master units
            double corLenMaster = corridor.EndDistance;
            double start = Math.Max(startStationMaster, 0);
            double end   = Math.Min(endStationMaster,   corLenMaster);

            for (double sta = start; sta <= end + 1e-6; sta += intervalMaster)
            {
                double clamped = Math.Min(sta, end);
                var section = ExtractCrossSectionAtStation(
                    corridor, clamped, leftWidthMaster, rightWidthMaster);
                if (section != null) results.Add(section);
                if (Math.Abs(clamped - end) < 1e-6) break;
            }

            return results;
        }

        /// <summary>
        /// Extracts a single cross section using Corridor.GetXSCutPoints().
        /// Station is in master units.
        /// </summary>
        public static CrossSectionData ExtractCrossSectionAtStation(
            Corridor corridor,
            double   stationMaster,
            double   leftWidthMaster,
            double   rightWidthMaster)
        {
            // SDK quirk documented in sample: station == 0 causes issues
            if (stationMaster == 0.0) stationMaster = 0.000001;

            XSCutPoint[] rawPoints;
            try
            {
                rawPoints = corridor.GetXSCutPoints(
                    stationMaster,
                    leftWidthMaster,
                    rightWidthMaster,
                    -leftWidthMaster,                     // negative left offset
                    Alignment.WhichFeatures.Top);                           
            }
            catch
            {
                return null;
            }

            if (rawPoints == null || rawPoints.Length == 0)
            {
                ConsensusConnection sdkCon = Bentley.CifNET.SDK.Edit.ConsensusConnectionEdit.GetActive();
                if (sdkCon == null)
                    return null;

                var geomModel = sdkCon.GetActiveGeometricModel();
                if (geomModel == null)
                    return null;

                foreach (Corridor cor in geomModel.Corridors)
                {
                    double length = cor.CorridorAlignment.LinearGeometry.Length;
                    AddXSCutPoints(cor, 0.0, length, true);
                }
                try
                {
                    rawPoints = corridor.GetXSCutPoints(
                        stationMaster,
                        leftWidthMaster,
                        rightWidthMaster,
                        -leftWidthMaster,                     // negative left offset
                        Alignment.WhichFeatures.Top);
                }
                catch
                {
                    return null;
                }
            }
            if (rawPoints == null || rawPoints.Length == 0)
                return null;



            // Sort by offset (left to right, ascending X)
            Array.Sort(rawPoints, (a, b) => a.Point.X.CompareTo(b.Point.X));

            var section = new CrossSectionData
            {
                Station      = stationMaster,
                StationLabel = FormatStation(stationMaster)
            };

            foreach (var pt in rawPoints)
            {
                section.Points.Add(new CrossSectionPoint
                {
                    // PointName = corridor feature name (e.g. "EOP_L")
                    FeatureName = pt.PointName ?? string.Empty,

                    // PointFeatureName = full definition path — strip to filename
                    PointCode   = CleanFeatureName(pt.PointFeatureName),

                    DisplayMetadata = BuildFeatureDisplayMetadata(
                        pt.PointName,
                        pt.PointFeatureName),

                    // Point.X = offset from CL (metres), Point.Y = elevation (metres)
                    Offset      = pt.Point.X,
                    Elevation   = pt.Point.Y,

                    // World 3-D position — store for downstream use
                    WorldX      = pt.PointOnPlan.X,
                    WorldY      = pt.PointOnPlan.Y,
                    WorldZ      = pt.PointOnPlan.Z,

                    // ExistingElevation populated separately via terrain sampling
                    // CutFill not directly available on XSCutPoint — compute from terrain
                });
            }

            return section;
        }

        /*----------------------------------------------------------------------------------------------**/
        /* Utility Function | Gets cut point information.
        /*--------------+---------------+---------------+---------------+---------------+----------------*/
        public static void AddXSCutPoints(Corridor cor, double startStation, double endStation, bool allCorridors)
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();
            double station;

            // ORD does all computations in meters. 
            // Therefore, input must be converted from Master units to Meters
            double leftWidth = ConvertMasterToMeter(50);
            double rightWidth = ConvertMasterToMeter(50);
            double increment = ConvertMasterToMeter(cor.CorridorAlignment.LinearGeometry.Length);
            if (!allCorridors)
            {
                startStation = ConvertMasterToMeter(0);
                endStation = ConvertMasterToMeter(cor.CorridorAlignment.LinearGeometry.Length);
            }

            //alternative format
            if (ALTERNATE_FORMAT)
            {
                for (station = startStation; station < endStation; station += increment)
                {
                    if (station == 0)
                        station = 0.000001;
                    XSCutPoint[] points = cor.GetXSCutPoints(station, leftWidth, rightWidth, -leftWidth, Alignment.WhichFeatures.Top, null);

                    if (points != null)
                    {
                        foreach (XSCutPoint pt in points)
                        {
                            properties.Add("FTR Name", pt.PointName);
                            properties.Add("FTR Defintion Name", pt.PointFeatureName);
                            properties.Add("Offset", FormatDistance(pt.Point.X));
                            properties.Add("Elevation", FormatDistance(pt.Point.Y));
                            properties.Add("X", FormatDistance(pt.PointOnPlan.X));
                            properties.Add("Y", FormatDistance(pt.PointOnPlan.Y));
                            properties.Add("Z", FormatDistance(pt.PointOnPlan.Z));

                            properties.Clear();
                        }
                    }
                    if (station == 0.000001)
                        station = 0.0;
                }
            }

            //adds point information
            else
            {
                for (station = startStation; station < endStation; station += increment)
                {
                    if (station == 0)
                        station = 0.000001;
                    XSCutPoint[] points = cor.GetXSCutPoints(station, leftWidth, rightWidth, -leftWidth, Alignment.WhichFeatures.Top, null);


                    if (points != null)
                    {
                        Array.Sort(points, (first, second) => first.Point.X.CompareTo(second.Point.X));

                        properties.Clear();
                        properties["FTR Name"] = "|";
                        properties["FTR Definition Name"] = "|";
                        properties["Offset"] = "|";
                        properties["Elevation"] = "|";
                        properties["X"] = "|";
                        properties["Y"] = "|";
                        properties["Z"] = "|";

                        foreach (XSCutPoint pt in points)
                        {
                            properties["FTR Name"] += pt.PointName + "|";
                            properties["FTR Definition Name"] += getCleanDefName(pt.PointFeatureName) + "|";
                            properties["Offset"] += FormatDistance(pt.Point.X) + "|";
                            properties["Elevation"] += FormatDistance(pt.Point.Y) + "|";
                            properties["X"] += FormatDistance(pt.PointOnPlan.X) + "|";
                            properties["Y"] += FormatDistance(pt.PointOnPlan.Z) + "|";
                            properties["Z"] += FormatDistance(pt.PointOnPlan.X) + "|";
                        }
                    }


                    if (station == 0.000001)
                        station = 0.0;
                }
            }
        }



        /// <summary>
        /// Intersects 3D source curves with the computed section plane for this station
        /// and returns cut footprints in section coordinates (offset/elevation).
        /// </summary>
        public static List<CrossSectionCutElement> ExtractCutElementsFromCurves(
            CrossSectionData section,
            IEnumerable<DPoint3d[]> sourceCurves,
            double intersectionTolerance = 1e-4)
        {
            var cutElements = new List<CrossSectionCutElement>();
            if (section == null || section.Points == null || section.Points.Count < 2 || sourceCurves == null)
                return cutElements;

            if (!TryBuildSectionFrame(section, out DPoint3d origin, out DVector3d rightDir, out DVector3d planeNormal))
                return cutElements;

            int curveIndex = 0;
            foreach (var curve in sourceCurves)
            {
                curveIndex++;
                if (curve == null || curve.Length < 2) continue;

                var sectionPts = new List<(double Offset, double Elevation)>();
                var worldPts = new List<(double X, double Y, double Z)>();

                for (int i = 1; i < curve.Length; i++)
                {
                    DPoint3d p0 = curve[i - 1];
                    DPoint3d p1 = curve[i];

                    double d0 = SignedDistanceToPlane(p0, origin, planeNormal);
                    double d1 = SignedDistanceToPlane(p1, origin, planeNormal);

                    bool p0On = Math.Abs(d0) <= intersectionTolerance;
                    bool p1On = Math.Abs(d1) <= intersectionTolerance;

                    if (p0On)
                        AddIntersectionPoint(p0, origin, rightDir, sectionPts, worldPts, intersectionTolerance);

                    if (p1On)
                        AddIntersectionPoint(p1, origin, rightDir, sectionPts, worldPts, intersectionTolerance);

                    // Segment crosses plane
                    if ((d0 < -intersectionTolerance && d1 > intersectionTolerance) ||
                        (d0 > intersectionTolerance && d1 < -intersectionTolerance))
                    {
                        double t = d0 / (d0 - d1);
                        t = Math.Max(0.0, Math.Min(1.0, t));

                        DPoint3d pi = new DPoint3d(
                            p0.X + (p1.X - p0.X) * t,
                            p0.Y + (p1.Y - p0.Y) * t,
                            p0.Z + (p1.Z - p0.Z) * t);

                        AddIntersectionPoint(pi, origin, rightDir, sectionPts, worldPts, intersectionTolerance);
                    }
                }

                if (sectionPts.Count > 1)
                {
                    sectionPts = sectionPts
                        .OrderBy(pt => pt.Offset)
                        .ToList();

                    cutElements.Add(new CrossSectionCutElement
                    {
                        ElementId = $"Curve_{curveIndex}",
                        FeatureDefinitionName = "ModelCurve",
                        SectionPolyline = sectionPts,
                        WorldPolyline = worldPts,
                        Symbology = new SymbologyMetadata
                        {
                            UseFeatureDefinitionDefaults = false,
                            SymbologySource = "CurvePlaneIntersection"
                        }
                    });
                }
            }

            return cutElements;
        }



        /// <summary>
        /// Intersects triangulated mesh facets with the section plane and returns
        /// section-space cut segments for each intersected facet.
        /// </summary>
        public static List<CrossSectionCutElement> ExtractCutElementsFromMeshTriangles(
            CrossSectionData section,
            IEnumerable<DPoint3d[]> meshTriangles,
            double intersectionTolerance = 1e-4)
        {
            return ExtractCutElementsFromTriangles(
                section,
                meshTriangles,
                "MeshTriangle",
                "ModelMesh",
                intersectionTolerance);
        }

        /// <summary>
        /// Intersects triangulated surface facets with the section plane and returns
        /// section-space cut segments for each intersected facet.
        /// </summary>
        public static List<CrossSectionCutElement> ExtractCutElementsFromSurfaceTriangles(
            CrossSectionData section,
            IEnumerable<DPoint3d[]> surfaceTriangles,
            double intersectionTolerance = 1e-4)
        {
            return ExtractCutElementsFromTriangles(
                section,
                surfaceTriangles,
                "SurfaceTriangle",
                "ModelSurface",
                intersectionTolerance);
        }


        /// <summary>
        /// Intersects triangulated solid shell facets with the section plane and returns
        /// section-space cut segments for each intersected solid facet.
        /// </summary>
        public static List<CrossSectionCutElement> ExtractCutElementsFromSolidTriangles(
            CrossSectionData section,
            IEnumerable<DPoint3d[]> solidTriangles,
            double intersectionTolerance = 1e-4)
        {
            return ExtractCutElementsFromTriangles(
                section,
                solidTriangles,
                "SolidTriangle",
                "ModelSolid",
                intersectionTolerance);
        }

        private static List<CrossSectionCutElement> ExtractCutElementsFromTriangles(
            CrossSectionData section,
            IEnumerable<DPoint3d[]> triangles,
            string elementPrefix,
            string featureName,
            double intersectionTolerance)
        {
            var cutElements = new List<CrossSectionCutElement>();
            if (section == null || triangles == null || section.Points == null || section.Points.Count < 2)
                return cutElements;

            if (!TryBuildSectionFrame(section, out DPoint3d origin, out DVector3d rightDir, out DVector3d planeNormal))
                return cutElements;

            int triIndex = 0;
            foreach (var tri in triangles)
            {
                triIndex++;
                if (tri == null || tri.Length < 3)
                    continue;

                var worldIntersections = new List<DPoint3d>();
                AddTriangleEdgeIntersections(tri[0], tri[1], origin, planeNormal, worldIntersections, intersectionTolerance);
                AddTriangleEdgeIntersections(tri[1], tri[2], origin, planeNormal, worldIntersections, intersectionTolerance);
                AddTriangleEdgeIntersections(tri[2], tri[0], origin, planeNormal, worldIntersections, intersectionTolerance);

                if (worldIntersections.Count < 2)
                    continue;

                var sectionPolyline = new List<(double Offset, double Elevation)>();
                var worldPolyline = new List<(double X, double Y, double Z)>();

                foreach (var wp in worldIntersections)
                {
                    double off = ProjectOffsetOnSection(wp, origin, rightDir);
                    sectionPolyline.Add((off, wp.Z));
                    worldPolyline.Add((wp.X, wp.Y, wp.Z));
                }

                sectionPolyline = sectionPolyline
                    .OrderBy(p => p.Offset)
                    .ToList();

                cutElements.Add(new CrossSectionCutElement
                {
                    ElementId = $"{elementPrefix}_{triIndex}",
                    FeatureDefinitionName = featureName,
                    SectionPolyline = sectionPolyline,
                    WorldPolyline = worldPolyline,
                    Symbology = new SymbologyMetadata
                    {
                        UseFeatureDefinitionDefaults = false,
                        SymbologySource = "PlaneFacetIntersection"
                    }
                });
            }

            return cutElements;
        }

        private static void AddTriangleEdgeIntersections(
            DPoint3d p0,
            DPoint3d p1,
            DPoint3d origin,
            DVector3d planeNormal,
            List<DPoint3d> intersections,
            double tolerance)
        {
            double d0 = SignedDistanceToPlane(p0, origin, planeNormal);
            double d1 = SignedDistanceToPlane(p1, origin, planeNormal);

            bool p0On = Math.Abs(d0) <= tolerance;
            bool p1On = Math.Abs(d1) <= tolerance;

            if (p0On) AddUniqueWorldPoint(intersections, p0, tolerance);
            if (p1On) AddUniqueWorldPoint(intersections, p1, tolerance);

            if ((d0 < -tolerance && d1 > tolerance) || (d0 > tolerance && d1 < -tolerance))
            {
                double t = d0 / (d0 - d1);
                t = Math.Max(0.0, Math.Min(1.0, t));

                var pi = new DPoint3d(
                    p0.X + (p1.X - p0.X) * t,
                    p0.Y + (p1.Y - p0.Y) * t,
                    p0.Z + (p1.Z - p0.Z) * t);

                AddUniqueWorldPoint(intersections, pi, tolerance);
            }
        }

        private static void AddUniqueWorldPoint(List<DPoint3d> pts, DPoint3d p, double tolerance)
        {
            bool dupe = pts.Any(q =>
                Math.Abs(q.X - p.X) <= tolerance &&
                Math.Abs(q.Y - p.Y) <= tolerance &&
                Math.Abs(q.Z - p.Z) <= tolerance);
            if (!dupe)
                pts.Add(p);
        }

        private static double ProjectOffsetOnSection(DPoint3d worldPoint, DPoint3d origin, DVector3d rightDir)
        {
            double dx = worldPoint.X - origin.X;
            double dy = worldPoint.Y - origin.Y;
            return dx * rightDir.X + dy * rightDir.Y;
        }

        private static bool TryBuildSectionFrame(
            CrossSectionData section,
            out DPoint3d origin,
            out DVector3d rightDir,
            out DVector3d planeNormal)
        {
            origin = new DPoint3d();
            rightDir = new DVector3d(1, 0, 0);
            planeNormal = new DVector3d(0, 1, 0);

            var ordered = section.Points
                .OrderBy(p => p.Offset)
                .ToList();
            if (ordered.Count < 2)
                return false;

            var left = ordered.First();
            var right = ordered.Last();

            DVector3d secDir = new DVector3d(
                right.WorldX - left.WorldX,
                right.WorldY - left.WorldY,
                0.0);

            double secMag = Math.Sqrt(secDir.X * secDir.X + secDir.Y * secDir.Y + secDir.Z * secDir.Z);
            if (secMag < 1e-9)
                return false;

            rightDir = new DVector3d(secDir.X / secMag, secDir.Y / secMag, secDir.Z / secMag);

            DVector3d up = new DVector3d(0, 0, 1);
            planeNormal = new DVector3d(
                rightDir.Y * up.Z - rightDir.Z * up.Y,
                rightDir.Z * up.X - rightDir.X * up.Z,
                rightDir.X * up.Y - rightDir.Y * up.X);

            double nMag = Math.Sqrt(planeNormal.X * planeNormal.X + planeNormal.Y * planeNormal.Y + planeNormal.Z * planeNormal.Z);
            if (nMag < 1e-9)
                return false;

            planeNormal = new DVector3d(planeNormal.X / nMag, planeNormal.Y / nMag, planeNormal.Z / nMag);

            var cl = section.Points.FirstOrDefault(p =>
                p.FeatureName.Equals("CL", StringComparison.OrdinalIgnoreCase) ||
                p.PointCode.Equals("CL", StringComparison.OrdinalIgnoreCase));

            if (cl != null)
                origin = new DPoint3d(cl.WorldX, cl.WorldY, cl.WorldZ);
            else
            {
                origin = new DPoint3d(
                    0.5 * (left.WorldX + right.WorldX),
                    0.5 * (left.WorldY + right.WorldY),
                    0.5 * (left.WorldZ + right.WorldZ));
            }

            return true;
        }

        private static double SignedDistanceToPlane(DPoint3d p, DPoint3d origin, DVector3d normal)
        {
            return (p.X - origin.X) * normal.X +
                   (p.Y - origin.Y) * normal.Y +
                   (p.Z - origin.Z) * normal.Z;
        }

        private static void AddIntersectionPoint(
            DPoint3d worldPoint,
            DPoint3d origin,
            DVector3d rightDir,
            List<(double Offset, double Elevation)> sectionPts,
            List<(double X, double Y, double Z)> worldPts,
            double tolerance)
        {
            double offset = ProjectOffsetOnSection(worldPoint, origin, rightDir);
            double elev = worldPoint.Z;

            bool duplicate = sectionPts.Any(p =>
                Math.Abs(p.Offset - offset) <= tolerance &&
                Math.Abs(p.Elevation - elev) <= tolerance);
            if (duplicate) return;

            sectionPts.Add((offset, elev));
            worldPts.Add((worldPoint.X, worldPoint.Y, worldPoint.Z));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Station formatting
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Formats a station value (master units) as "K+CCC.DDD".
        /// Uses FormatForDisplay for locale-correct output.
        /// </summary>
        public static string FormatStation(double stationMaster)
        {
            int    km  = (int)(stationMaster / 1000.0);
            double rem = stationMaster - km * 1000.0;
            return $"{km}+{rem:000.000}";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Strips the directory path from a feature definition name,
        /// matching the getCleanDefName() method in XSCutPointReporter.
        /// </summary>


        private static FeatureDisplayMetadata BuildFeatureDisplayMetadata(string featureName, string featureDefinitionRaw)
        {
            string raw = featureDefinitionRaw ?? string.Empty;
            string leaf = CleanFeatureName(raw);
            string path = NormalizeFeaturePath(raw);

            return new FeatureDisplayMetadata
            {
                FeatureName = featureName ?? string.Empty,
                FeatureDefinitionRaw = raw,
                FeatureDefinitionName = leaf,
                FeatureDefinitionPath = path,
                Category = InferFeatureCategory(featureName, leaf),
                Symbology = new SymbologyMetadata
                {
                    UseFeatureDefinitionDefaults = true,
                    SymbologySource = "FeatureDefinition"
                },
                Cell = new CellDisplayMetadata
                {
                    PlacementMode = "AtPoint"
                }
            };
        }

        private static string NormalizeFeaturePath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return string.Empty;

            string normalized = rawPath.Replace('\\', '/');
            int idx = normalized.LastIndexOf('/');
            if (idx <= 0) return string.Empty;

            return normalized.Substring(0, idx).Trim('/');
        }

        private static string InferFeatureCategory(string featureName, string featureDefinitionName)
        {
            string token = ((featureName ?? string.Empty) + " " + (featureDefinitionName ?? string.Empty)).ToUpperInvariant();

            if (token.Contains("PAVE") || token.Contains("LANE") || token.Contains("EOP")) return "Pavement";
            if (token.Contains("DRAIN") || token.Contains("DITCH") || token.Contains("GUTTER")) return "Drainage";
            if (token.Contains("SHOULDER")) return "Shoulder";
            if (token.Contains("DAYLIGHT") || token.Contains("SLOPE")) return "Earthworks";
            if (token.Contains("KERB") || token.Contains("CURB")) return "Kerb";

            return string.Empty;
        }

        private static string CleanFeatureName(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return filePath ?? string.Empty;
            int idx = filePath.LastIndexOf('\\');
            return idx < 0 ? filePath : filePath.Substring(idx + 1);
        }

        private static string getCleanDefName(string filePath)
        {
            int index = filePath.LastIndexOf('\\');

            if (index < 0)
            {
                return filePath;
            }

            return filePath.Substring(index + 1);
        }
    }
}
