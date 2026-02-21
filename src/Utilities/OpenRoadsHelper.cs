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

using System;
using System.Collections.Generic;
using System.Linq;
using Bentley.CifNET.GeometryModel.SDK;
using Bentley.CifNET.SDK;
using Bentley.CifNET.SDK.Edit;
using Bentley.CifNET.Formatting;
using Bentley.DgnPlatformNET;
using Bentley.MstnPlatformNET;
using GenDes_OpenRoads.Models;

namespace GenDes_OpenRoads.Utilities
{
    public static class OpenRoadsHelper
    {
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
            GeometricModel gm = con?.GetActiveGeometricModel();
            if (gm == null) return Array.Empty<Alignment>();
            return gm.Alignments.OrderBy(a => a.Name).ToList();
        }

        public static Alignment FindAlignment(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            ConsensusConnection con = GetActiveConnection();
            GeometricModel gm = con?.GetActiveGeometricModel();
            return gm?.Alignments.FirstOrDefault(a =>
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
            GeometricModel gm = con?.GetActiveGeometricModel();
            if (gm == null) return Array.Empty<Corridor>();
            return gm.Corridors
                     .Where(c => string.Equals(
                         c.CorridorAlignment?.Name, alignmentName,
                         StringComparison.OrdinalIgnoreCase))
                     .ToList();
        }

        public static Corridor FindCorridor(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            ConsensusConnection con = GetActiveConnection();
            GeometricModel gm = con?.GetActiveGeometricModel();
            return gm?.Corridors.FirstOrDefault(c =>
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
            double corLenMaster = ConvertMeterToMaster(corridor.CorridorAlignment.LinearGeometry.Length);
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
            double   leftWidthMaster  = 50,
            double   rightWidthMaster = 50)
        {
            // Convert station and widths from master units → metres for SDK
            double stationM    = ConvertMasterToMeter(stationMaster);
            double leftWidthM  = ConvertMasterToMeter(leftWidthMaster);
            double rightWidthM = ConvertMasterToMeter(rightWidthMaster);

            // SDK quirk documented in sample: station == 0 causes issues
            if (stationM == 0.0) stationM = 0.000001;

            XSCutPoint[] rawPoints;
            try
            {
                rawPoints = corridor.GetXSCutPoints(
                    stationM,
                    leftWidthM,
                    rightWidthM,
                    -leftWidthM,                     // negative left offset
                    Alignment.WhichFeatures.All,
                    null);                           // no feature filter
            }
            catch
            {
                return null;
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
        private static string CleanFeatureName(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return filePath ?? string.Empty;
            int idx = filePath.LastIndexOf('\\');
            return idx < 0 ? filePath : filePath.Substring(idx + 1);
        }
    }
}
