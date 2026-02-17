using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bentley.GenerativeComponents.AddInSupport;
using Bentley.GenerativeComponents.GCScript;
using Bentley.GenerativeComponents.ElementBasedNodes;
using Bentley.GenerativeComponents.GCScript.GCTypes;
using Bentley.GenerativeComponents.GeneralPurpose.Collections;
using BDPnet = Bentley.DgnPlatformNET;
using Bentley.MstnPlatformNET;
using Bentley.CifNET.GeometryModel.SDK;
using GC_OpenRoads_CrossSections.Models;
using GC_OpenRoads.Utilities;

namespace GC_OpenRoads.Nodes
{
    [GCNamespace("ORD")]
    [GCNodeTypePaletteCategory("OpenRoads - Cross Sections")]
    [GCNodeTypeIcon("Resources/CrossSection.png")]
    [GCSummary("Samples an OpenRoads corridor at a station interval and returns structured " +
               "cross-section data. AlignmentName enables terrain sampling for Existing Levels.")]
    public class ORCrossSectionDataNode : GeometricNode
    {
        private static readonly BDPnet.DgnFile  _dgnFile  = Session.Instance.GetActiveDgnFile();
        private static readonly BDPnet.DgnModel _dgnModel = Session.Instance.GetActiveDgnModel();

        // ─────────────────────────────────────────────────────────────────────
        //  DEFAULT TECHNIQUE — uniform interval
        // ─────────────────────────────────────────────────────────────────────

        [GCDefaultTechnique]
        [GCSummary("Extracts cross-section data at a uniform station interval.")]
        [GCParameter("CorridorName",      "Corridor to sample.")]
        [GCParameter("AlignmentName",     "Parent alignment name. Enables terrain sampling for ExistingElevation.")]
        [GCParameter("StartStation",      "Start chainage (metres). 0 = corridor start.")]
        [GCParameter("EndStation",        "End chainage (metres). 0 = corridor end.")]
        [GCParameter("StationInterval",   "Distance between sections (metres). Default 10.")]
        [GCParameter("ExtraStations",     "Comma-separated extra chainages to always include.")]
        [GCParameter("Sections",          "Extracted section data objects.")]
        [GCParameter("SectionCount",      "Number of sections extracted.")]
        [GCParameter("TotalCutArea",      "Sum of cut areas across all sections (m²).")]
        [GCParameter("TotalFillArea",     "Sum of fill areas across all sections (m²).")]
        [GCParameter("CutVolumeM3",       "Average-end-area cut volume (m³).")]
        [GCParameter("FillVolumeM3",      "Average-end-area fill volume (m³).")]
        [GCParameter("CsvReport",         "All section point data as CSV text.")]
        [GCParameter("StationLabels",     "Formatted station strings for all sections.")]
        public NodeUpdateResult ByInterval
        (
            NodeUpdateContext updateContext,
            [GCIn]  string   CorridorName,
            [GCIn]  string   AlignmentName,
            [GCIn]  double   StartStation,
            [GCIn]  double   EndStation,
            [GCIn]  double   StationInterval,
            [GCIn]  string   ExtraStations,
            [GCIn]  double   LeftWidth,
            [GCIn]  double   RightWidth,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref CrossSectionData[] Sections,
            [GCOut, GCInitiallyPinned] ref int                SectionCount,
            [GCOut, GCInitiallyPinned] ref double             TotalCutArea,
            [GCOut, GCInitiallyPinned] ref double             TotalFillArea,
            [GCOut, GCInitiallyPinned] ref double             CutVolumeM3,
            [GCOut, GCInitiallyPinned] ref double             FillVolumeM3,
            [GCOut, GCInitiallyPinned] ref string             CsvReport,
            [GCOut, GCInitiallyPinned] ref string[]           StationLabels
        )
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CorridorName))
                    return new NodeUpdateResult.IncompleteInputs(CorridorName);

                double interval = StationInterval <= 0 ? 10.0 : StationInterval;

                // Reset replication state on first call
                if (this.ReplicationIndex < 1)
                    this.ClearReplicatedChildNodes();

                var corridor = OpenRoadsHelper.FindCorridor(CorridorName);
                if (corridor == null)
                    return new NodeUpdateResult.TechniqueInvalidArguments(nameof(CorridorName));

                // Resolve alignment for terrain sampling
                Alignment alignment = string.IsNullOrWhiteSpace(AlignmentName)
                    ? null
                    : OpenRoadsHelper.FindAlignment(AlignmentName);

                double corLen = OpenRoadsHelper.ConvertMeterToMaster(
                    corridor.CorridorAlignment.LinearGeometry.Length);
                double start = StartStation > 0 ? StartStation : 0.0;
                double end   = EndStation   > 0 ? EndStation   : corLen;

                if (start >= end)
                    return new NodeUpdateResult.TechniqueInvalidArguments(nameof(StartStation));

                double lw = LeftWidth  > 0 ? LeftWidth  : 50.0;
                double rw = RightWidth > 0 ? RightWidth : 50.0;
                var sections = OpenRoadsHelper.ExtractCrossSections(
                    corridor, start, end, interval, lw, rw);

                // Merge extra stations
                if (!string.IsNullOrWhiteSpace(ExtraStations))
                {
                    foreach (var part in ExtraStations.Split(',',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (double.TryParse(part, out double sta) && sta >= start && sta <= end)
                        {
                            var extra = OpenRoadsHelper.ExtractCrossSectionAtStation(
                                corridor, sta, lw, rw);
                            if (extra != null) sections.Add(extra);
                        }
                    }
                    sections = sections
                        .OrderBy(s => s.Station)
                        .DistinctBy(s => Math.Round(s.Station, 3))
                        .ToList();
                }

                Sections      = sections.ToArray();
                SectionCount  = sections.Count;
                StationLabels = sections.Select(s => s.StationLabel).ToArray();
                TotalCutArea  = sections.Sum(s => s.CutArea);
                TotalFillArea = sections.Sum(s => s.FillArea);

                // Average-end-area volumes
                double cut = 0, fill = 0;
                for (int i = 1; i < sections.Count; i++)
                {
                    double dist = sections[i].Station - sections[i - 1].Station;
                    cut  += 0.5 * (sections[i - 1].CutArea  + sections[i].CutArea)  * dist;
                    fill += 0.5 * (sections[i - 1].FillArea + sections[i].FillArea) * dist;
                }
                CutVolumeM3  = cut;
                FillVolumeM3 = fill;
                CsvReport    = BuildCsv(sections);
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TECHNIQUE — single station
        // ─────────────────────────────────────────────────────────────────────

        [GCTechnique]
        [GCSummary("Extracts a single cross section at the given station.")]
        [GCParameter("CorridorName",  "Corridor to sample.")]
        [GCParameter("AlignmentName", "Parent alignment (enables terrain sampling).")]
        [GCParameter("Station",       "Chainage to sample (metres).")]
        [GCParameter("Section",       "Extracted section data.")]
        public NodeUpdateResult AtStation
        (
            NodeUpdateContext updateContext,
            [GCIn]  string           CorridorName,
            [GCIn]  string           AlignmentName,
            [GCIn]  double           Station,
            [GCOut, GCInitiallyPinned] ref CrossSectionData Section
        )
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CorridorName))
                    return new NodeUpdateResult.IncompleteInputs(CorridorName);

                var corridor = OpenRoadsHelper.FindCorridor(CorridorName);
                if (corridor == null)
                    return new NodeUpdateResult.TechniqueInvalidArguments(nameof(CorridorName));

                Alignment alignment = string.IsNullOrWhiteSpace(AlignmentName)
                    ? null
                    : OpenRoadsHelper.FindAlignment(AlignmentName);

                var section = OpenRoadsHelper.ExtractCrossSectionAtStation(
                    corridor, Station, alignment);

                if (section == null)
                    return new NodeUpdateResult.TechniqueInvalidArguments(nameof(Station));

                Section = section;
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }

        // ─────────────────────────────────────────────────────────────────────
        private static string BuildCsv(IEnumerable<CrossSectionData> sections)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Station,StationLabel,FeatureName,PointCode,Offset,ProposedLevel,ExistingLevel,CutFill");
            foreach (var s in sections)
                foreach (var pt in s.Points)
                    sb.AppendLine(
                        $"{s.Station:F3}," +
                        $"\"{s.StationLabel}\"," +
                        $"\"{pt.FeatureName}\"," +
                        $"\"{pt.PointCode}\"," +
                        $"{pt.Offset:F4}," +
                        $"{pt.Elevation:F4}," +
                        $"{(pt.ExistingElevation.HasValue ? pt.ExistingElevation.Value.ToString("F4") : "")}," +
                        $"{pt.CutFill:F4}");
            return sb.ToString();
        }
    }
}
