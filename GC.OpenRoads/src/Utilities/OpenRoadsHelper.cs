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
