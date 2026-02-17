using Bentley.CifNET.LinearGeometry;
using Bentley.DgnPlatformNET;
using Bentley.DgnPlatformNET.Elements;
using Bentley.GenerativeComponents;
using Bentley.GenerativeComponents.AddInSupport;
using Bentley.GenerativeComponents.ElementBasedNodes;
using Bentley.GenerativeComponents.GCScript;
using Bentley.GenerativeComponents.GCScript.GCTypes;
using Bentley.GenerativeComponents.GeneralPurpose.Collections;
using Bentley.GenerativeComponents.View;
using Bentley.GeometryNET;
using Bentley.MstnPlatformNET;
using GC_OpenRoads_CrossSections.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using BDPnet = Bentley.DgnPlatformNET;

namespace GC_OpenRoads.Nodes
{
    // =========================================================================
    //  ORAnnotationBandNode
    //
    //  Draws the tabular annotation box below each cross-section profile.
    //  Each column aligns with a corridor feature point; each row is one
    //  data type.  Mandatory rows are always on; optional and custom rows
    //  are controlled by GCIn parameters.
    //
    //  BOX LAYOUT (top → bottom)
    //  ──────────────────────────────────────────────────────────────────────
    //  ┌────────────┬──────┬──────┬──────┬──────┬──────┐
    //  │ Point Label│PT_L  │EOP_L │  CL  │EOP_R │PT_R  │  always
    //  ├────────────┼──────┼──────┼──────┼──────┼──────┤
    //  │ Offset     │-15.23│-7.000│ 0.000│ 7.000│16.450│  always
    //  ├────────────┼──────┼──────┼──────┼──────┼──────┤
    //  │ Proposed Lv│98.142│97.500│97.000│97.500│96.890│  always
    //  ├────────────┼──────┼──────┼──────┼──────┼──────┤
    //  │ Existing Lv│98.700│97.600│97.080│97.450│97.200│  always
    //  ├────────────┼──────┼──────┼──────┼──────┼──────┤
    //  │ Δ          │-0.558│-0.100│-0.080│ 0.050│-0.310│  EnableDeltaRow
    //  ├────────────┼──────┼──────┼──────┼──────┼──────┤
    //  │ Cut / Fill │ 0.558│ 0.100│ 0.080│-0.050│ 0.310│  EnableCutFillRow
    //  ├────────────┼──────┼──────┼──────┼──────┼──────┤
    //  │ Grade %    │  2.30│  1.50│  0.00│  1.50│  2.30│  ExtraRowKeys/Labels
    //  └────────────┴──────┴──────┴──────┴──────┴──────┘
    //
    //  COORDINATE WIRING
    //  ──────────────────────────────────────────────────────────────────────
    //  BandTopY      ← ORCrossSectionGeometry.AnnotationBandTopY
    //  ColumnCentreX ← ORCrossSectionGeometry.ColumnCentreX
    //  BandLeftX     ← ORCrossSectionGeometry.ProfileLeftX
    //  BandRightX    ← ORCrossSectionGeometry.ProfileRightX
    // =========================================================================

    [GCNamespace("ORD")]
    [GCNodeTypePaletteCategory("OpenRoads - Cross Sections")]
    [GCNodeTypeIcon("Resources/Annotation.png")]
    [GCSummary("Draws the tabular annotation band below a cross-section profile. " +
               "Mandatory rows: Point Label, Offset, Proposed Level, Existing Level. " +
               "Optional rows: Delta (Δ), Cut/Fill. " +
               "User rows: any number via ExtraRowKeys / ExtraRowLabels.")]
    public class ORAnnotationBandNode : GeometricNode
    {
        private static readonly BDPnet.DgnFile  _dgnFile  = Session.Instance.GetActiveDgnFile();
        private static readonly BDPnet.DgnModel _dgnModel = Session.Instance.GetActiveDgnModel();

        // ─────────────────────────────────────────────────────────────────────
        //  DEFAULT TECHNIQUE — compute only, no DGN writes
        // ─────────────────────────────────────────────────────────────────────

        [GCDefaultTechnique]
        [GCSummary("Computes annotation band row/column geometry. No DGN writes. " +
                   "Use TotalBandHeight output to set AnnotationBandHeight in the layout node.")]
        [GCParameter("SectionData",            "Cross-section data for this section.")]
        [GCParameter("ColumnCentreX",          "Sheet X centre of each column — from single-section Build2DProfile.ColumnCentreX (double[]).")]
        [GCParameter("ColumnCentreXGC",        "GC-boxed column centres from multi-section Build2DProfiles/PlaceMultipleInDgn.ColumnCentreX (IGCObject[]). When set, overrides ColumnCentreX.")]
        [GCParameter("BandTopY",               "Y of the top edge of the band — from ORCrossSectionGeometry.AnnotationBandTopY.")]
        [GCParameter("BandLeftX",         "X of left edge of data area — from ORCrossSectionGeometry.ProfileLeftX.")]
        [GCParameter("BandRightX",        "X of right edge of data area — from ORCrossSectionGeometry.ProfileRightX.")]
        [GCParameter("RowHeight",         "Height of each row in sheet master units. Default 5.")]
        [GCParameter("TextHeight",        "Text height for data values (master units). Default 2.5.")]
        [GCParameter("LabelTextHeight",   "Text height for row labels (master units). Default 2.0.")]
        [GCParameter("LabelColumnWidth",  "Width of the left-hand label column (master units). Default 20.")]
        [GCParameter("NumericFormat",     "Format string for numeric values, e.g. 'F3'. Default 'F3'.")]
        [GCParameter("EnableDeltaRow",    "Show Δ row (Proposed − Existing level).")]
        [GCParameter("EnableCutFillRow",  "Show Cut/Fill row from corridor data.")]
        [GCParameter("ExtraRowKeys",      "Comma-separated keys for custom rows, looked up in CrossSectionPoint.CustomValues.")]
        [GCParameter("ExtraRowLabels",    "Comma-separated display labels matching ExtraRowKeys.")]
        [GCParameter("ExtraRowFormats",   "Comma-separated format strings for extra rows (e.g. 'F2,P1'). Falls back to NumericFormat.")]
        [GCParameter("TotalBandHeight",   "Total height of the band in sheet units. Use to set AnnotationBandHeight in layout node.")]
        [GCParameter("BandBottomY",       "Y of the bottom edge of the band.")]
        [GCParameter("RowCount",          "Total number of rows including all enabled rows.")]
        [GCParameter("RowLabels",         "Left-hand label text for each row (top to bottom).")]
        public NodeUpdateResult Build
        (
            NodeUpdateContext updateContext,
            [GCIn]  CrossSectionData SectionData,
            [GCIn]  double[]         ColumnCentreX,
            [GCIn]  IGCObject[]      ColumnCentreXGC,
            [GCIn]  double           BandTopY,
            [GCIn]  double           BandLeftX,
            [GCIn]  double           BandRightX,
            [GCIn]  double           RowHeight,
            [GCIn]  double           TextHeight,
            [GCIn]  double           LabelTextHeight,
            [GCIn]  double           LabelColumnWidth,
            [GCIn]  string           NumericFormat,
            [GCIn]  bool             EnableDeltaRow,
            [GCIn]  bool             EnableCutFillRow,
            [GCIn]  string           ExtraRowKeys,
            [GCIn]  string           ExtraRowLabels,
            [GCIn]  string           ExtraRowFormats,
            [GCOut, GCInitiallyPinned] ref double   TotalBandHeight,
            [GCOut, GCInitiallyPinned] ref double   BandBottomY,
            [GCOut, GCInitiallyPinned] ref int      RowCount,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref string[] RowLabels
        )
        {
            try
            {
                var colX = UnboxColumnCentres(ColumnCentreXGC, ColumnCentreX);
                var valid = ValidateInputs(SectionData, colX, ExtraRowKeys, ExtraRowLabels);
                if (valid != NodeUpdateResult.Success) return valid;

                double rh  = RowHeight > 0 ? RowHeight : 5.0;
                string fmt = string.IsNullOrWhiteSpace(NumericFormat) ? "F3" : NumericFormat;

                var rows = BuildRowDefinitions(rh, fmt, EnableDeltaRow, EnableCutFillRow,
                    ExtraRowKeys, ExtraRowLabels, ExtraRowFormats, TextHeight);

                TotalBandHeight = rows.Count * rh;
                BandBottomY     = BandTopY - TotalBandHeight;
                RowCount        = rows.Count;
                RowLabels       = rows.Select(r => r.Label).ToArray();
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TECHNIQUE — compute AND place in DGN
        // ─────────────────────────────────────────────────────────────────────

        [GCTechnique]
        [GCSummary("Computes the annotation band and places border lines, dividers, and text " +
                   "into the active DGN model.")]
        [GCParameter("SectionData",            "Cross-section data for this section.")]
        [GCParameter("ColumnCentreX",          "Sheet X centre of each column — from single-section Build2DProfile.ColumnCentreX (double[]).")]
        [GCParameter("ColumnCentreXGC",        "GC-boxed column centres from multi-section techniques (IGCObject[]). When set, overrides ColumnCentreX.")]
        [GCParameter("BandTopY",               "Y of the top edge of the band.")]
        [GCParameter("BandLeftX",         "X of left edge of data area.")]
        [GCParameter("BandRightX",        "X of right edge of data area.")]
        [GCParameter("RowHeight",         "Row height (master units).")]
        [GCParameter("TextHeight",        "Data text height (master units).")]
        [GCParameter("LabelTextHeight",   "Label column text height (master units).")]
        [GCParameter("LabelColumnWidth",  "Label column width (master units).")]
        [GCParameter("NumericFormat",     "Numeric format string.")]
        [GCParameter("EnableDeltaRow",    "Show Δ row.")]
        [GCParameter("EnableCutFillRow",  "Show Cut/Fill row.")]
        [GCParameter("ExtraRowKeys",      "Comma-separated custom row keys.")]
        [GCParameter("ExtraRowLabels",    "Comma-separated custom row labels.")]
        [GCParameter("ExtraRowFormats",   "Comma-separated custom row format strings.")]
        [GCParameter("DrawOuterBorder",   "Draw outer rectangle border.")]
        [GCParameter("DrawRowDividers",   "Draw horizontal lines between rows.")]
        [GCParameter("DrawColumnDividers","Draw vertical lines between data columns.")]
        [GCParameter("DrawLabelDivider",  "Draw vertical line between label and data columns.")]
        [GCParameter("TotalBandHeight",   "Total height of the annotation box placed.")]
        [GCParameter("BandBottomY",       "Y of the bottom edge of the box.")]
        [GCParameter("RowCount",          "Number of rows placed.")]
        [GCParameter("RowLabels",         "Row label texts (top to bottom).")]
        public NodeUpdateResult BuildAndPlace
        (
            NodeUpdateContext updateContext,
            [GCIn]  CrossSectionData SectionData,
            [GCIn]  double[]         ColumnCentreX,
            [GCIn]  IGCObject[]      ColumnCentreXGC,
            [GCIn]  double           BandTopY,
            [GCIn]  double           BandLeftX,
            [GCIn]  double           BandRightX,
            [GCIn]  double           RowHeight,
            [GCIn]  double           TextHeight,
            [GCIn]  double           LabelTextHeight,
            [GCIn]  double           LabelColumnWidth,
            [GCIn]  string           NumericFormat,
            [GCIn]  bool             EnableDeltaRow,
            [GCIn]  bool             EnableCutFillRow,
            [GCIn]  string           ExtraRowKeys,
            [GCIn]  string           ExtraRowLabels,
            [GCIn]  string           ExtraRowFormats,
            [GCIn]  bool             DrawOuterBorder,
            [GCIn]  bool             DrawRowDividers,
            [GCIn]  bool             DrawColumnDividers,
            [GCIn]  bool             DrawLabelDivider,
            [GCOut, GCInitiallyPinned] ref double   TotalBandHeight,
            [GCOut, GCInitiallyPinned] ref double   BandBottomY,
            [GCOut, GCInitiallyPinned] ref int      RowCount,
            [GCOut, GCInitiallyPinned] ref string[] RowLabels
        )
        {
            try
            {
                var colX  = UnboxColumnCentres(ColumnCentreXGC, ColumnCentreX);
                var valid = ValidateInputs(SectionData, colX, ExtraRowKeys, ExtraRowLabels);
                if (valid != NodeUpdateResult.Success) return valid;

                double rh  = RowHeight > 0 ? RowHeight : 5.0;
                string fmt = string.IsNullOrWhiteSpace(NumericFormat) ? "F3" : NumericFormat;
                double lcw = LabelColumnWidth > 0 ? LabelColumnWidth : 20.0;

                var rows = BuildRowDefinitions(rh, fmt, EnableDeltaRow, EnableCutFillRow,
                    ExtraRowKeys, ExtraRowLabels, ExtraRowFormats, TextHeight);

                TotalBandHeight = rows.Count * rh;
                BandBottomY     = BandTopY - TotalBandHeight;
                RowCount        = rows.Count;
                RowLabels       = rows.Select(r => r.Label).ToArray();

                // ── Column divider X positions ────────────────────────────────
                var colDivX = new List<double> { BandLeftX };
                for (int c = 0; c < colX.Length - 1; c++)
                    colDivX.Add(0.5 * (colX[c] + colX[c + 1]));
                colDivX.Add(BandRightX);

                double boxLeft   = BandLeftX - lcw;
                double boxRight  = BandRightX;
                double boxTop    = BandTopY;
                double boxBottom = BandBottomY;
                double dataLeft  = BandLeftX;

                // ── Outer border ──────────────────────────────────────────────
                if (DrawOuterBorder)
                    PlaceRect(boxLeft, boxBottom, boxRight, boxTop);

                // ── Label column divider ──────────────────────────────────────
                if (DrawLabelDivider)
                    PlaceLine(dataLeft, boxTop, dataLeft, boxBottom);

                // ── Rows ──────────────────────────────────────────────────────
                var tStyle = GetOrCreateStyle("XS_Data",  TextHeight);
                var lStyle = GetOrCreateStyle("XS_Label", LabelTextHeight > 0 ? LabelTextHeight : 2.0);

                for (int r = 0; r < rows.Count; r++)
                {
                    var row    = rows[r];
                    double top = BandTopY - r * rh;
                    double bot = top - rh;
                    double mid = 0.5 * (top + bot);

                    // Row divider above (skip very first — outer border handles it)
                    if (DrawRowDividers && r > 0)
                        PlaceLine(boxLeft, top, boxRight, top);

                    // Row label
                    PlaceText(row.Label, boxLeft + 1.0, mid, lStyle, TextJustification.LeftCenter);

                    // Data cells
                    for (int c = 0; c < SectionData.Points.Count; c++)
                    {
                        var pt     = SectionData.Points[c];
                        double cl  = colDivX[c];
                        double cr  = colDivX[c + 1];
                        double cx  = 0.5 * (cl + cr);

                        // Column divider on left (not before first data column)
                        if (DrawColumnDividers && c > 0)
                            PlaceLine(cl, boxTop, cl, boxBottom);

                        string cellText = GetCellText(row, pt, fmt);
                        PlaceText(cellText, cx, mid, tStyle, TextJustification.CenterCenter);
                    }
                }

                // Bottom border if not drawn by outer border
                if (!DrawOuterBorder && DrawRowDividers)
                    PlaceLine(boxLeft, boxBottom, boxRight, boxBottom);
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }

        // =====================================================================
        //  PRIVATE HELPERS
        // =====================================================================

        private double[] UnboxColumnCentres(IGCObject[] gcArr, double[] fallback)
        {
            if (gcArr == null || gcArr.Length == 0)
                return fallback ?? Array.Empty<double>();
            Unboxer unboxer = GCEnvironment().Unboxer;
            return gcArr.Select(o => unboxer.Unbox<double>(o)).ToArray();
        }

        private static NodeUpdateResult ValidateInputs(
            CrossSectionData data, double[] colX, string extraKeys, string extraLabels)
        {
            if (data == null || data.Points.Count == 0)
                return new NodeUpdateResult.IncompleteInputs(nameof(data));
            if (colX == null || colX.Length == 0)
                return new NodeUpdateResult.IncompleteInputs(nameof(colX));
            if (colX.Length != data.Points.Count)
                return new NodeUpdateResult.TechniqueInvalidArguments(
                    $"ColumnCentreX has {colX.Length} entries but SectionData has {data.Points.Count} points.");

            var keys   = Split(extraKeys);
            var labels = Split(extraLabels);
            if (keys.Count > 0 && labels.Count > 0 && keys.Count != labels.Count)
                return new NodeUpdateResult.TechniqueInvalidArguments(
                    $"ExtraRowKeys ({keys.Count}) and ExtraRowLabels ({labels.Count}) counts differ.");

            return NodeUpdateResult.Success;
        }

        private static List<AnnotationRowDefinition> BuildRowDefinitions(
            double rh, string fmt, bool delta, bool cutFill,
            string extraKeys, string extraLabels, string extraFormats,
            double textH)
        {
            var rows = new List<AnnotationRowDefinition>
            {
                MakeRow("point_label",    "Point Label",    AnnotationRowType.PointLabel,  rh, fmt, textH),
                MakeRow("offset",         "Offset",         AnnotationRowType.Offset,      rh, fmt, textH),
                MakeRow("proposed_level", "Proposed Level", AnnotationRowType.ProposedLevel,rh, fmt, textH),
                MakeRow("existing_level", "Existing Level", AnnotationRowType.ExistingLevel,rh, fmt, textH),
            };

            if (delta)
                rows.Add(MakeRow("delta",   "Δ (Prop − Exist)", AnnotationRowType.Delta,   rh, fmt, textH));
            if (cutFill)
                rows.Add(MakeRow("cutfill", "Cut / Fill",       AnnotationRowType.CutFill, rh, fmt, textH));

            var keys    = Split(extraKeys);
            var labels  = Split(extraLabels);
            var formats = Split(extraFormats);
            for (int i = 0; i < keys.Count; i++)
            {
                string key    = keys[i];
                string label  = i < labels.Count  ? labels[i]  : key;
                string rowFmt = i < formats.Count ? formats[i] : fmt;
                rows.Add(MakeRow(key, label, AnnotationRowType.Custom, rh, rowFmt, textH));
            }

            return rows;
        }

        private static AnnotationRowDefinition MakeRow(
            string key, string label, AnnotationRowType type,
            double rh, string fmt, double textH)
            => new AnnotationRowDefinition
            {
                Key           = key,
                Label         = label,
                RowType       = type,
                RowHeight     = rh,
                NumericFormat = fmt,
                TextHeight    = textH > 0 ? textH : 2.5,
            };

        private static string GetCellText(
            AnnotationRowDefinition row, CrossSectionPoint pt, string fallbackFmt)
        {
            string fmt = string.IsNullOrWhiteSpace(row.NumericFormat)
                ? fallbackFmt : row.NumericFormat;
            try
            {
                return row.RowType switch
                {
                    AnnotationRowType.PointLabel =>
                        string.IsNullOrEmpty(pt.PointCode) ? pt.FeatureName : pt.PointCode,

                    AnnotationRowType.Offset =>
                        pt.Offset.ToString(fmt),

                    AnnotationRowType.ProposedLevel =>
                        pt.Elevation.ToString(fmt),

                    AnnotationRowType.ExistingLevel =>
                        pt.ExistingElevation.HasValue
                            ? pt.ExistingElevation.Value.ToString(fmt) : "—",

                    AnnotationRowType.Delta =>
                        pt.ExistingElevation.HasValue
                            ? (pt.Elevation - pt.ExistingElevation.Value).ToString(fmt) : "—",

                    AnnotationRowType.CutFill =>
                        pt.CutFill.ToString(fmt),

                    AnnotationRowType.Custom =>
                        row.ValueProvider != null
                            ? row.ValueProvider(pt)
                            : pt.CustomValues.TryGetValue(row.Key, out var v) ? v ?? "—" : "—",

                    _ => string.Empty
                };
            }
            catch { return "ERR"; }
        }

        // ── DGN element placement ─────────────────────────────────────────────

        private void PlaceLine(double x1, double y1, double x2, double y2)
            => new LineElement(_dgnModel, null, new Bentley.GeometryNET.DSegment3d(new Bentley.GeometryNET.DPoint3d(x1, y1, 0), new Bentley.GeometryNET.DPoint3d(x2, y2, 0)))
                          .AddToModel();

        private void PlaceRect(double l, double b, double r, double t)
        {
            var pts = new DPoint3d[]
            {
                new(l, b, 0), new(r, b, 0), new(r, t, 0), new(l, t, 0), new(l, b, 0)
            };
            new LineStringElement(_dgnModel, null, pts).AddToModel();
        }

        private void PlaceText(string text, double x, double y,
            DgnTextStyle style, TextJustification justif)
        {
            if (string.IsNullOrEmpty(text)) return;
            DSegment3d segment = new DSegment3d(new DPoint3d(x,y), new DPoint3d(x+1, y));
            DVector3d rotVector = segment.UnitTangent;
            var textblock = new TextBlock(style, _dgnModel);
            textblock.AppendText(text);
            textblock.SetUserOrigin(new DPoint3d(x, y, 0));
            textblock.SetOrientation(DMatrix3d.Rotation(2, rotVector.AngleXY));
            TextElement.CreateElement(null, textblock).AddToModel();
        }

        private DgnTextStyle GetOrCreateStyle(string name, double height)
        {
            var existing = DgnTextStyle.GetByName(name, _dgnFile);
            if (existing != null) return existing;

            var style = new DgnTextStyle(name, _dgnFile);
            style.SetProperty(TextStyleProperty.Height, height);
            style.SetProperty(TextStyleProperty.Width,  height * 0.8);
            return style;
        }

        private static List<string> Split(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<string>();
            return csv.Split(',')
                      .ToList();
        }
    }
}
