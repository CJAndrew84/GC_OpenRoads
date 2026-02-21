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
using GenDes_OpenRoads.Utilities;
using GenDes_OpenRoads_CrossSections.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using BDPnet = Bentley.DgnPlatformNET;

namespace GenDes_OpenRoads.Nodes
{
    [GCNamespace("ORD")]
    [GCNodeTypePaletteCategory("OpenRoads - Cross Sections")]
    [GCNodeTypeIcon("Resources/CrossSection.png")]
    [GCSummary("Converts cross-section data into 2-D sheet-space geometry. " +
               "Supports vertical exaggeration (independent H and V scales). " +
               "Outputs ColumnCentreX and AnnotationBandTopY for wiring to ORAnnotationBand.")]
    public class ORCrossSectionGeometryNode : GeometricNode
    {
        private static readonly BDPnet.DgnFile _dgnFile = Session.Instance.GetActiveDgnFile();
        private static readonly BDPnet.DgnModel _dgnModel = Session.Instance.GetActiveDgnModel();

        // ─────────────────────────────────────────────────────────────────────
        //  DEFAULT TECHNIQUE — compute 2-D profile geometry
        //
        //  SCALE MODEL
        //  -----------
        //  HorizontalScaleDenominator : e.g. 200 → 1 real metre = 1/200 sheet unit
        //  VerticalExaggerationFactor : e.g. 2.0 → V drawn 2× taller than H
        //    Effective V denominator  = H denom / VE factor
        //    e.g.  200 / 2.0 = 100  →  V 1:100 while H 1:200
        //
        //  Sheet coords:
        //    sheetX = OriginX + (offset    − refOffset) / HScaleDenom
        //    sheetY = OriginY + (elevation − refElev)   / EffectiveVDenom
        // ─────────────────────────────────────────────────────────────────────

        [GCDefaultTechnique]
        [GCSummary("Computes 2-D sheet geometry for a single cross section. " +
                   "No DGN writes — connect outputs to ORAnnotationBand and GC line nodes.")]
        [GCParameter("DrawingModel", "Drawing Space to store Cross Section Data")]
        [GCParameter("SectionData", "Cross-section data from ORCrossSectionData node.")]
        [GCParameter("HorizontalScaleDenominator", "Scale denominator for horizontal axis, e.g. 200 for 1:200.")]
        [GCParameter("VerticalExaggerationFactor", "VE factor: 1 = true scale, 2 = twice as tall as horizontal.")]
        [GCParameter("OriginX", "Sheet X of the datum / CL (master units).")]
        [GCParameter("OriginY", "Sheet Y of the datum line (master units).")]
        [GCParameter("ProfileBottomClearance", "Gap below lowest profile point before annotation band (master units).")]
        [GCParameter("TickHalfHeight", "Half-height of feature point tick marks (master units).")]
        [GCParameter("DrawDatumLine", "Include a horizontal datum line at the reference elevation.")]
        [GCParameter("DrawVerticalDropLines", "Include vertical drop lines from each feature point to datum.")]
        [GCParameter("DesignProfilePoints", "Polyline through the proposed design points (sheet space).")]
        [GCParameter("ExistingProfilePoints", "Polyline through existing ground points (empty if no terrain).")]
        [GCParameter("DatumLinePoints", "Two points defining the horizontal datum line.")]
        [GCParameter("TickMarkPoints", "Paired points (base, tip) for each feature point tick.")]
        [GCParameter("DropLinePoints", "Paired points (feature Y, datum Y) for drop lines.")]
        [GCParameter("CutElementProfilePoints", "2D cut-element polylines from curve-plane intersections (flattened pairs separated by NaN).")]
        [GCParameter("ColumnCentreX", "Sheet X centre of each feature point column — feed to ORAnnotationBand.")]
        [GCParameter("AnnotationBandTopY", "Y of top edge of annotation band — feed to ORAnnotationBand.")]
        [GCParameter("ProfileLeftX", "X of leftmost feature point.")]
        [GCParameter("ProfileRightX", "X of rightmost feature point.")]
        [GCParameter("ProfileWidth", "Total profile width in sheet units.")]
        [GCParameter("ProfileHeight", "Profile height from datum to highest point.")]
        [GCParameter("CentreLineX", "X of the centreline.")]
        [GCParameter("DatumY", "Y of the datum line.")]
        [GCParameter("EffectiveVScaleDenom", "Computed vertical scale denominator after exaggeration.")]
        [GCParameter("ScaleLabel", "Human-readable scale string, e.g. 'H 1:200 / V 1:100 (×2 V.E.)'.")]
        [GCParameter("StationLabel", "Station label for this section, e.g. '1+234.567'.")]
        public NodeUpdateResult Build2DProfile
        (
            NodeUpdateContext updateContext,
            [GCIn] DgnModel DrawingModel,
            [GCIn] CrossSectionData SectionData,
            [GCIn] double HorizontalScaleDenominator,
            [GCIn] double VerticalExaggerationFactor,
            [GCIn] double OriginX,
            [GCIn] double OriginY,
            [GCIn] double ProfileBottomClearance,
            [GCIn] double TickHalfHeight,
            [GCIn] bool DrawDatumLine,
            [GCIn] bool DrawVerticalDropLines,
            [GCOut, GCInitiallyPinned] ref DPoint3d[] DesignProfilePoints,
            [GCOut, GCInitiallyPinned] ref DPoint3d[] ExistingProfilePoints,
            [GCOut, GCInitiallyPinned] ref DPoint3d[] DatumLinePoints,
            [GCOut, GCInitiallyPinned] ref DPoint3d[] TickMarkPoints,
            [GCOut, GCInitiallyPinned] ref DPoint3d[] DropLinePoints,
            [GCOut, GCInitiallyPinned] ref DPoint3d[] CutElementProfilePoints,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref double[] ColumnCentreX,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref double AnnotationBandTopY,
            [GCOut, GCInitiallyPinned] ref double ProfileLeftX,
            [GCOut, GCInitiallyPinned] ref double ProfileRightX,
            [GCOut, GCInitiallyPinned] ref double ProfileWidth,
            [GCOut, GCInitiallyPinned] ref double ProfileHeight,
            [GCOut, GCInitiallyPinned] ref double CentreLineX,
            [GCOut, GCInitiallyPinned] ref double DatumY,
            [GCOut, GCInitiallyPinned] ref double EffectiveVScaleDenom,
            [GCOut, GCInitiallyPinned] ref string ScaleLabel,
            [GCOut, GCInitiallyPinned] ref string StationLabel
        )
        {
            try
            {
                if (SectionData == null || SectionData.Points.Count == 0)
                    return new NodeUpdateResult.IncompleteInputs(nameof(SectionData));

                double hd = HorizontalScaleDenominator <= 0 ? 200 : HorizontalScaleDenominator;
                double vef = VerticalExaggerationFactor <= 0 ? 1 : VerticalExaggerationFactor;
                double vd = hd / vef;

                EffectiveVScaleDenom = vd;
                ScaleLabel = Math.Abs(vef - 1.0) < 1e-6
                    ? $"1:{hd:G} (true scale)"
                    : $"H 1:{hd:G}  /  V 1:{vd:G}  (×{vef:G} V.E.)";

                var pts = SectionData.Points;

                // Reference point: prefer CL
                var clPt = pts.FirstOrDefault(p =>
                    p.PointCode.Equals("CL", StringComparison.OrdinalIgnoreCase));
                double refOff = clPt?.Offset ?? 0.0;
                double refElev = clPt?.Elevation ?? pts.Min(p => p.Elevation);

                DatumY = OriginY;

                var profile = new List<DPoint3d>(pts.Count);
                var ticks = new List<DPoint3d>(pts.Count * 2);
                var drops = new List<DPoint3d>(pts.Count * 2);
                var colCentres = new List<double>(pts.Count);

                double minX = double.MaxValue, maxX = double.MinValue;
                double minY = double.MaxValue, maxY = double.MinValue;

                foreach (var pt in pts)
                {
                    double sx = OriginX + (pt.Offset - refOff) / hd;
                    double sy = OriginY + (pt.Elevation - refElev) / vd;

                    profile.Add(new DPoint3d(sx, sy, 0));
                    ticks.Add(new DPoint3d(sx, sy - TickHalfHeight, 0));
                    ticks.Add(new DPoint3d(sx, sy + TickHalfHeight, 0));
                    drops.Add(new DPoint3d(sx, sy, 0));
                    drops.Add(new DPoint3d(sx, DatumY, 0));
                    colCentres.Add(sx);

                    if (sx < minX) minX = sx;
                    if (sx > maxX) maxX = sx;
                    if (sy < minY) minY = sy;
                    if (sy > maxY) maxY = sy;
                }

                DesignProfilePoints = profile.ToArray();
                TickMarkPoints = ticks.ToArray();
                DropLinePoints = DrawVerticalDropLines ? drops.ToArray() : Array.Empty<DPoint3d>();
                ColumnCentreX = colCentres.ToArray();
                ProfileLeftX = minX;
                ProfileRightX = maxX;
                ProfileWidth = maxX - minX;
                ProfileHeight = maxY - DatumY;
                CentreLineX = OriginX + (refOff - refOff) / hd;
                StationLabel = SectionData.StationLabel;
                AnnotationBandTopY = minY - ProfileBottomClearance;

                DatumLinePoints = DrawDatumLine
                    ? new[] { new DPoint3d(minX, DatumY, 0), new DPoint3d(maxX, DatumY, 0) }
                    : Array.Empty<DPoint3d>();

                // Existing ground profile (only if terrain data is present)
                ExistingProfilePoints = pts
                    .Where(p => p.ExistingElevation.HasValue)
                    .Select(p => new DPoint3d(
                        OriginX + (p.Offset - refOff) / hd,
                        OriginY + (p.ExistingElevation!.Value - refElev) / vd,
                        0))
                    .ToArray();
                if (ExistingProfilePoints.Length < 2)
                    ExistingProfilePoints = Array.Empty<DPoint3d>();

                // Flatten cut-element section polylines into a single array,
                // separating each polyline with a NaN sentinel point.
                var cutPts = new List<DPoint3d>();
                if (SectionData.CutElements != null && SectionData.CutElements.Count > 0)
                {
                    foreach (var cut in SectionData.CutElements)
                    {
                        if (cut?.SectionPolyline == null || cut.SectionPolyline.Count < 2)
                            continue;

                        foreach (var q in cut.SectionPolyline.OrderBy(p => p.Offset))
                        {
                            cutPts.Add(new DPoint3d(
                                OriginX + (q.Offset - refOff) / hd,
                                OriginY + (q.Elevation - refElev) / vd,
                                0));
                        }

                        cutPts.Add(new DPoint3d(double.NaN, double.NaN, double.NaN));
                    }
                }

                if (cutPts.Count > 0 && double.IsNaN(cutPts[cutPts.Count - 1].X))
                    cutPts.RemoveAt(cutPts.Count - 1);

                CutElementProfilePoints = cutPts.Count > 0 ? cutPts.ToArray() : Array.Empty<DPoint3d>();

            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  MULTI-SECTION TECHNIQUE — compute 2-D profile geometry for many
        //
        //  LAYOUT
        //  ------
        //  • Sections placed in columns with up to MaxRowsPerColumn rows per column.
        //  • Horizontal column spacing = (LeftWidth + RightWidth) * ColumnSpacingMultiplier
        //      (width measured at sheet scale for each section; column uses the max width
        //       of its contained sections to guarantee no overlap).
        //  • Vertical row spacing per column:
        //      If RowSpacing <= 0 → auto: max(profileHeight + margins) in that column.
        //      Else → use RowSpacing for all rows in that column.
        //  • Row index increases downward in sheet Y (subtract spacing from OriginY).
        //
        //  SCALE MODEL
        //  -----------
        //  HorizontalScaleDenominator : e.g. 200 → 1 real metre = 1/200 sheet unit
        //  VerticalExaggerationFactor : e.g. 2.0 → V drawn 2× taller than H
        //    Effective V denominator  = H denom / VE factor
        //
        //  Per-section sheet coords:
        //    sheetX = SectionOriginX + (offset    − refOffset) / HScaleDenom
        //    sheetY = SectionDatumY  + (elevation − refElev)   / EffectiveVDenom
        // ─────────────────────────────────────────────────────────────────────
        /*
        [GCTechnique]
            [GCSummary(
            "Computes 2-D sheet geometry for multiple cross sections and places them in a columnar layout. " +
            "Columns hold up to MaxRowsPerColumn rows; horizontal spacing uses (Left+Right width) × ColumnSpacingMultiplier. " +
            "No DGN writes — connect outputs to ORAnnotationBand and GC line/curve nodes."
        )]
                [GCParameter("DrawingModel", "Drawing Space to store Cross Section Data")]
                [GCParameter("Sections", "Array of cross-section data (from ORCrossSectionData).")]
            [GCParameter("HorizontalScaleDenominator", "Scale denominator for horizontal axis, e.g. 200 for 1:200.")]
            [GCParameter("VerticalExaggerationFactor", "VE factor: 1 = true scale, 2 = twice as tall as horizontal.")]
            [GCParameter("OriginX", "Global base X for the first column’s datum/CL (master units).")]
            [GCParameter("OriginY", "Global base Y for row 0 in all columns (master units).")]
            [GCParameter("MaxRowsPerColumn", "Maximum number of rows per column (>= 1).")]
            [GCParameter("ProfileBottomClearance", "Gap below lowest profile point before annotation band (master units).")]
            [GCParameter("TickHalfHeight", "Half-height of feature point tick marks (master units).")]
            [GCParameter("DrawDatumLine", "Include a horizontal datum line at the reference elevation.")]
            [GCParameter("DrawVerticalDropLines", "Include vertical drop lines from each feature point to datum.")]
            [GCParameter("ColumnSpacingMultiplier", "Multiplier for horizontal spacing: (Left+Right width)*multiplier. Default 1.5.")]
            [GCParameter("RowSpacing", "Vertical spacing between rows (sheet units). If <= 0, auto per column.")]
            // Per-section outputs (replicated to match Sections[])
            [GCParameter("DesignProfilePoints",   "Per-section polyline through proposed design points (IGCObject[][] of DPoint3d[]).")]
            [GCParameter("ExistingProfilePoints", "Per-section polyline through existing ground points. Empty if no terrain data.")]
            [GCParameter("DatumLinePoints",       "Per-section two-point datum line. Empty if DrawDatumLine is false.")]
            [GCParameter("TickMarkPoints",        "Per-section paired tick mark points (base, tip per feature point).")]
            [GCParameter("DropLinePoints",        "Per-section paired drop line points. Empty if DrawVerticalDropLines is false.")]
            [GCParameter("ColumnCentreX",      "Per-section X centres of each feature column (IGCObject[][]). Wire to ORAnnotationBand.ColumnCentreXGC.")]
            [GCParameter("AnnotationBandTopY", "Per-section Y of the top edge of the annotation band — feed to ORAnnotationBand.")]
            [GCParameter("ProfileLeftX", "Per-section X of leftmost feature point.")]
            [GCParameter("ProfileRightX", "Per-section X of rightmost feature point.")]
            [GCParameter("ProfileWidth", "Per-section total profile width in sheet units.")]
            [GCParameter("ProfileHeight", "Per-section profile height from datum to highest point.")]
            [GCParameter("CentreLineX", "Per-section X of the centreline.")]
            [GCParameter("DatumY", "Per-section Y of the datum line.")]
            [GCParameter("SectionOriginX", "Per-section X origin actually used (column offset applied).")]
            [GCParameter("RowIndex", "Per-section row index in the layout (0-based, top to bottom).")]
            [GCParameter("ColumnIndex", "Per-section column index in the layout (0-based, left to right).")]
            [GCParameter("StationLabel", "Per-section station labels.")]
            // Global outputs
            [GCParameter("EffectiveVScaleDenom", "Computed vertical scale denominator after exaggeration.")]
            [GCParameter("ScaleLabel", "Human-readable scale string, e.g. 'H 1:200 / V 1:100 (×2 V.E.)'.")]
            [GCParameter("ColumnCount", "Total number of columns used.")]
            public NodeUpdateResult Build2DProfiles
        (
            NodeUpdateContext updateContext,
            [GCIn] DgnModel DrawingModel,
            [GCIn] CrossSectionData[] Sections,
            [GCIn] double HorizontalScaleDenominator,
            [GCIn] double VerticalExaggerationFactor,
            [GCIn] double OriginX,
            [GCIn] double OriginY,
            [GCIn] int MaxRowsPerColumn,
            [GCIn] double ProfileBottomClearance,
            [GCIn] double TickHalfHeight,
            [GCIn] bool DrawDatumLine,
            [GCIn] bool DrawVerticalDropLines,
            [GCIn] double ColumnSpacingMultiplier,
            [GCIn] double RowSpacing,

            [GCOut, GCReplicatable, GCInitiallyPinned] ref IGCObject[][] DesignProfilePoints,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref IGCObject[][] ExistingProfilePoints,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref IGCObject[][] DatumLinePoints,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref IGCObject[][] TickMarkPoints,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref IGCObject[][] DropLinePoints,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref IGCObject[][] ColumnCentreX,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref double[] AnnotationBandTopY,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref double[] ProfileLeftX,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref double[] ProfileRightX,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref double[] ProfileWidth,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref double[] ProfileHeight,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref double[] CentreLineX,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref double[] DatumY,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref double[] SectionOriginX,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref int[] RowIndex,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref int[] ColumnIndex,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref string[] StationLabel,

            // Global
            [GCOut, GCInitiallyPinned] ref double EffectiveVScaleDenom,
            [GCOut, GCInitiallyPinned] ref string ScaleLabel,
            [GCOut, GCInitiallyPinned] ref int ColumnCount
        )
            {
                try
                {
                    if (Sections == null || Sections.Length == 0)
                        return new NodeUpdateResult.IncompleteInputs(nameof(Sections));

                    // Validate at least one section has points
                    if (!Sections.Any(s => s != null && s.Points != null && s.Points.Count > 0))
                        return new NodeUpdateResult.IncompleteInputs($"{nameof(Sections)} (no points)");

                    double hd = HorizontalScaleDenominator <= 0 ? 200 : HorizontalScaleDenominator;
                    double vef = VerticalExaggerationFactor <= 0 ? 1 : VerticalExaggerationFactor;
                    double vd = hd / vef;

                    EffectiveVScaleDenom = vd;
                    ScaleLabel = Math.Abs(vef - 1.0) < 1e-6
                        ? $"1:{hd:G} (true scale)"
                        : $"H 1:{hd:G}  /  V 1:{vd:G}  (×{vef:G} V.E.)";

                    int n = Sections.Length;
                    int maxRows = MaxRowsPerColumn <= 0 ? int.MaxValue : MaxRowsPerColumn;

                    // Determine column count from rows per column
                    ColumnCount = (int)Math.Ceiling(n / (double)Math.Max(1, maxRows));
                    double colMult = ColumnSpacingMultiplier <= 0 ? 1.5 : ColumnSpacingMultiplier;

                    // Pre-scan each section for width and height at sheet scale
                    // Width based on left/right extents about CL (or min-elev ref if no CL).
                    var perSectionWidth = new double[n];     // in sheet units
                    var perSectionHeight = new double[n];    // in sheet units (datum -> highest design point)
                    var hasPoints = new bool[n];

                    for (int i = 0; i < n; i++)
                    {
                        var sec = Sections[i];
                        if (sec == null || sec.Points == null || sec.Points.Count == 0)
                        {
                            hasPoints[i] = false;
                            perSectionWidth[i] = 0;
                            perSectionHeight[i] = 0;
                            continue;
                        }
                        hasPoints[i] = true;

                        var pts = sec.Points;

                        // Reference point: prefer CL
                        var clPt = pts.FirstOrDefault(p => p.PointCode.Equals("CL", StringComparison.OrdinalIgnoreCase));
                        double refOff = clPt?.Offset ?? 0.0;
                        double refElev = clPt?.Elevation ?? pts.Min(p => p.Elevation);

                        double minOff = pts.Min(p => p.Offset);
                        double maxOff = pts.Max(p => p.Offset);
                        double leftWm = Math.Max(0, refOff - minOff); // metres (or model units)
                        double rightWm = Math.Max(0, maxOff - refOff);
                        perSectionWidth[i] = (leftWm + rightWm) / hd;  // sheet units

                        double maxElev = pts.Max(p => p.Elevation);
                        perSectionHeight[i] = Math.Max(0, (maxElev - refElev) / vd);
                    }

                    // Build per-column spacing (max width in column × multiplier)
                    // and per-column row spacing (auto if RowSpacing <= 0).
                    var columnMaxWidth = new double[ColumnCount];
                    var columnRowSpacing = new double[ColumnCount];

                    for (int c = 0; c < ColumnCount; c++)
                    {
                        int start = c * Math.Max(1, maxRows);
                        int end = Math.Min(start + Math.Max(1, maxRows), n);

                        double maxW = 0.0;
                        double autoRowSpacing = 0.0;

                        for (int i = start; i < end; i++)
                        {
                            if (!hasPoints[i]) continue;
                            maxW = Math.Max(maxW, perSectionWidth[i]);

                            // Auto row spacing = profileHeight + ticks + bottom clearance + 20% breathing space
                            double h = perSectionHeight[i] + (TickHalfHeight * 2.0) + ProfileBottomClearance;
                            autoRowSpacing = Math.Max(autoRowSpacing, h);
                        }

                        columnMaxWidth[c] = maxW * colMult;
                        columnRowSpacing[c] = (RowSpacing > 0) ? RowSpacing : (autoRowSpacing * 1.2);
                        if (columnRowSpacing[c] <= 0) columnRowSpacing[c] = 1.0; // fallback minimal spacing
                    }

                    // Compute cumulative X offsets per column
                    var columnXOffset = new double[ColumnCount];
                    double xAccum = 0.0;
                    for (int c = 0; c < ColumnCount; c++)
                    {
                        columnXOffset[c] = xAccum;
                        xAccum += (c < ColumnCount ? columnMaxWidth[c] : 0.0);
                    }

                    // Allocate outputs
                    var outDesign = new DPoint3d[n][];
                    var outExisting = new DPoint3d[n][];
                    var outDatum = new DPoint3d[n][];
                    var outTicks = new DPoint3d[n][];
                    var outDrops = new DPoint3d[n][];
                    var outCols = new double[n][];
                    var outAnnTop = new double[n];
                    var outLeftX = new double[n];
                    var outRightX = new double[n];
                    var outWidth = new double[n];
                    var outHeight = new double[n];
                    var outCLX = new double[n];
                    var outDatumY = new double[n];
                    var outOriginX = new double[n];
                    var outRow = new int[n];
                    var outCol = new int[n];
                    var outStn = new string[n];

                    for (int i = 0; i < n; i++)
                    {
                        var sec = Sections[i];
                        int col = (maxRows <= 0) ? 0 : (i / Math.Max(1, maxRows));
                        int row = (maxRows <= 0) ? i : (i % Math.Max(1, maxRows));
                        outCol[i] = col;
                        outRow[i] = row;

                        double secOriginX = OriginX + columnXOffset[col];
                        double secDatumY = OriginY - row * columnRowSpacing[col];

                        outOriginX[i] = secOriginX;
                        outDatumY[i] = secDatumY;

                        if (sec == null || sec.Points == null || sec.Points.Count == 0)
                        {
                            outDesign[i] = Array.Empty<DPoint3d>();
                            outExisting[i] = Array.Empty<DPoint3d>();
                            outDatum[i] = Array.Empty<DPoint3d>();
                            outTicks[i] = Array.Empty<DPoint3d>();
                            outDrops[i] = Array.Empty<DPoint3d>();
                            outCols[i] = Array.Empty<double>();
                            outAnnTop[i] = secDatumY - ProfileBottomClearance;
                            outLeftX[i] = secOriginX;
                            outRightX[i] = secOriginX;
                            outWidth[i] = 0;
                            outHeight[i] = 0;
                            outCLX[i] = secOriginX;
                            outStn[i] = sec?.StationLabel ?? string.Empty;
                            continue;
                        }

                        var pts = sec.Points;

                        // Reference point: prefer CL
                        var clPt = pts.FirstOrDefault(p => p.PointCode.Equals("CL", StringComparison.OrdinalIgnoreCase));
                        double refOff = clPt?.Offset ?? 0.0;
                        double refElev = clPt?.Elevation ?? pts.Min(p => p.Elevation);

                        var profile = new List<DPoint3d>(pts.Count);
                        var ticks = new List<DPoint3d>(pts.Count * 2);
                        var drops = new List<DPoint3d>(pts.Count * 2);
                        var colCentres = new List<double>(pts.Count);

                        double minX = double.MaxValue, maxX = double.MinValue;
                        double minY = double.MaxValue, maxY = double.MinValue;

                        foreach (var p in pts)
                        {
                            double sx = secOriginX + (p.Offset - refOff) / hd;
                            double sy = secDatumY + (p.Elevation - refElev) / vd;

                            profile.Add(new DPoint3d(sx, sy, 0));
                            ticks.Add(new DPoint3d(sx, sy - TickHalfHeight, 0));
                            ticks.Add(new DPoint3d(sx, sy + TickHalfHeight, 0));

                            drops.Add(new DPoint3d(sx, sy, 0));
                            drops.Add(new DPoint3d(sx, secDatumY, 0));

                            colCentres.Add(sx);

                            if (sx < minX) minX = sx;
                            if (sx > maxX) maxX = sx;
                            if (sy < minY) minY = sy;
                            if (sy > maxY) maxY = sy;
                        }

                        outDesign[i] = profile.ToArray();
                        outTicks[i] = ticks.ToArray();
                        outDrops[i] = DrawVerticalDropLines ? drops.ToArray() : Array.Empty<DPoint3d>();
                        outCols[i] = colCentres.ToArray();
                        outLeftX[i] = minX;
                        outRightX[i] = maxX;
                        outWidth[i] = Math.Max(0, maxX - minX);
                        outHeight[i] = Math.Max(0, maxY - secDatumY);
                        outCLX[i] = secOriginX + (refOff - refOff) / hd; // explicit for clarity
                        outStn[i] = sec.StationLabel ?? string.Empty;
                        outAnnTop[i] = minY - ProfileBottomClearance;

                        outDatum[i] = DrawDatumLine
                            ? new[] { new DPoint3d(minX, secDatumY, 0), new DPoint3d(maxX, secDatumY, 0) }
                            : Array.Empty<DPoint3d>();

                        // Existing ground profile (only if terrain data is present)
                        var eg = pts
                            .Where(p => p.ExistingElevation.HasValue)
                            .Select(p => new DPoint3d(
                                secOriginX + (p.Offset - refOff) / hd,
                                secDatumY + (p.ExistingElevation!.Value - refElev) / vd,
                                0))
                            .ToArray();

                        outExisting[i] = (eg.Length >= 2) ? eg : Array.Empty<DPoint3d>();
                    }

                    // Assign outputs — box all jagged arrays to IGCObject[][] for GC compatibility
                    Boxer boxer = GCEnvironment().Boxer;
                    DesignProfilePoints   = outDesign.Select(pts => pts.Select(p => boxer.BoxToCompatible(p)).ToArray()).ToArray();
                    ExistingProfilePoints = outExisting.Select(pts => pts.Select(p => boxer.BoxToCompatible(p)).ToArray()).ToArray();
                    DatumLinePoints       = outDatum.Select(pts => pts.Select(p => boxer.BoxToCompatible(p)).ToArray()).ToArray();
                    TickMarkPoints        = outTicks.Select(pts => pts.Select(p => boxer.BoxToCompatible(p)).ToArray()).ToArray();
                    DropLinePoints        = outDrops.Select(pts => pts.Select(p => boxer.BoxToCompatible(p)).ToArray()).ToArray();
                    ColumnCentreX         = outCols.Select(c => c.Select(v => boxer.BoxToCompatible(v)).ToArray()).ToArray();
                    AnnotationBandTopY = outAnnTop;
                    ProfileLeftX = outLeftX;
                    ProfileRightX = outRightX;
                    ProfileWidth = outWidth;
                    ProfileHeight = outHeight;
                    CentreLineX = outCLX;
                    DatumY = outDatumY;
                    SectionOriginX = outOriginX;
                    RowIndex = outRow;
                    ColumnIndex = outCol;
                    StationLabel = outStn;
                }
                catch (Exception ex)
                {
                    return new NodeUpdateResult.TechniqueException(ex);
                }

                return NodeUpdateResult.Success;
            }
        */

        // ─────────────────────────────────────────────────────────────────────
        //  TECHNIQUE — compute AND place in DGN
        // ─────────────────────────────────────────────────────────────────────

        [GCTechnique]
        [GCSummary("Computes 2-D geometry and places LineString elements into the active DGN model.")]
        [GCParameter("DrawingModel", "Drawing Space to store Cross Section Data")]
        [GCParameter("SectionData", "Cross-section data.")]
        [GCParameter("HorizontalScaleDenominator", "H scale denominator.")]
        [GCParameter("VerticalExaggerationFactor", "Vertical exaggeration factor.")]
        [GCParameter("OriginX", "Sheet X of datum (master units).")]
        [GCParameter("OriginY", "Sheet Y of datum (master units).")]
        [GCParameter("ProfileBottomClearance", "Gap below profile before annotation band.")]
        [GCParameter("TickHalfHeight", "Tick mark half-height.")]
        [GCParameter("DrawDatumLine", "Draw datum line.")]
        [GCParameter("DrawVerticalDropLines", "Draw drop lines to datum.")]
        [GCParameter("AnnotationBandTopY", "Y of top edge of annotation band.")]
        [GCParameter("ColumnCentreX", "Sheet X centre per feature point column.")]
        [GCParameter("ProfileLeftX", "X of leftmost feature point.")]
        [GCParameter("ProfileRightX", "X of rightmost feature point.")]
        [GCParameter("ScaleLabel", "Scale description string.")]
        public NodeUpdateResult PlaceInDgn
            (
                NodeUpdateContext updateContext,
                [GCIn] DgnModel DrawingModel,
                [GCIn] CrossSectionData SectionData,
                [GCIn] double HorizontalScaleDenominator,
                [GCIn] double VerticalExaggerationFactor,
                [GCIn] double OriginX,
                [GCIn] double OriginY,
                [GCIn] double ProfileBottomClearance,
                [GCIn] double TickHalfHeight,
                [GCIn] bool DrawDatumLine,
                [GCIn] bool DrawVerticalDropLines,
                [GCOut, GCInitiallyPinned] ref double AnnotationBandTopY,
                [GCOut, GCInitiallyPinned] ref double[] ColumnCentreX,
                [GCOut, GCInitiallyPinned] ref double ProfileLeftX,
                [GCOut, GCInitiallyPinned] ref double ProfileRightX,
                [GCOut, GCInitiallyPinned] ref string ScaleLabel
            )
        {
            // Re-use Build2DProfile to compute all geometry
            DPoint3d[] designPts = Array.Empty<DPoint3d>();
            DPoint3d[] existingPts = Array.Empty<DPoint3d>();
            DPoint3d[] datumPts = Array.Empty<DPoint3d>();
            DPoint3d[] tickPts = Array.Empty<DPoint3d>();
            DPoint3d[] dropPts = Array.Empty<DPoint3d>();
            DPoint3d[] cutElePts = Array.Empty<DPoint3d>();
            double[] colX = Array.Empty<double>();
            double bandTopY = 0, leftX = 0, rightX = 0, width = 0, height = 0;
            double clX = 0, datumY = 0, effVD = 0;
            string stLabel = "";

            var r = Build2DProfile(updateContext,
                DrawingModel, SectionData, HorizontalScaleDenominator, VerticalExaggerationFactor,
                OriginX, OriginY, ProfileBottomClearance, TickHalfHeight,
                DrawDatumLine, DrawVerticalDropLines,
                ref designPts, ref existingPts, ref datumPts, ref tickPts, ref dropPts,
                ref cutElePts, ref colX, ref bandTopY, ref leftX, ref rightX, ref width, ref height,
                ref clX, ref datumY, ref effVD, ref ScaleLabel, ref stLabel);

            if (r != NodeUpdateResult.Success) return r;

            try
            {
                if (designPts.Length >= 2) new LineStringElement(DrawingModel, null, designPts).AddToModel();
                if (existingPts.Length >= 2) new LineStringElement(DrawingModel, null, existingPts).AddToModel();
                if (datumPts.Length == 2) new LineElement(DrawingModel, null, new Bentley.GeometryNET.DSegment3d(new Bentley.GeometryNET.DPoint3d(clX, datumY - 10, 0), new Bentley.GeometryNET.DPoint3d(clX, datumY + 10, 0))).AddToModel();

                for (int i = 0; i + 1 < tickPts.Length; i += 2)
                    new LineElement(DrawingModel, null, new Bentley.GeometryNET.DSegment3d(tickPts[i], tickPts[i + 1])).AddToModel();
                for (int i = 0; i + 1 < dropPts.Length; i += 2)
                    new LineElement(DrawingModel, null, new Bentley.GeometryNET.DSegment3d(dropPts[i], dropPts[i + 1])).AddToModel();

                AnnotationBandTopY = bandTopY;
                ColumnCentreX = colX;
                ProfileLeftX = leftX;
                ProfileRightX = rightX;
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }
        /*
        // ─────────────────────────────────────────────────────────────────────
//  TECHNIQUE — compute AND place multiple cross sections in DGN
// ─────────────────────────────────────────────────────────────────────

[GCTechnique]
    [GCSummary(
    "Computes 2-D geometry for multiple cross sections and places LineString/Line elements " +
    "into the active DGN model using a columnar layout. " +
    "Columns hold up to MaxRowsPerColumn rows; horizontal spacing uses (Left+Right width) × ColumnSpacingMultiplier."
)]
    [GCParameter("DrawingModel", "Drawing model to store the placed section geometry.")]
    [GCParameter("Sections", "Array of cross-section data (from ORCrossSectionData).")]
    [GCParameter("HorizontalScaleDenominator", "Horizontal scale denominator, e.g. 200 for 1:200.")]
    [GCParameter("VerticalExaggerationFactor", "Vertical exaggeration factor; 1 = true scale.")]
    [GCParameter("OriginX", "Global base X for the first column’s datum/CL (master units).")]
    [GCParameter("OriginY", "Global base Y for row 0 in all columns (master units).")]
    [GCParameter("MaxRowsPerColumn", "Maximum number of rows per column (>= 1).")]
    [GCParameter("ColumnSpacingMultiplier", "Multiplier for horizontal column spacing; default 1.5.")]
    [GCParameter("RowSpacing", "Vertical spacing between rows (sheet units). If <= 0, auto per column.")]
    [GCParameter("ProfileBottomClearance", "Gap below the lowest profile point before annotation band (master units).")]
    [GCParameter("TickHalfHeight", "Half-height of feature point tick marks (master units).")]
    [GCParameter("DrawDatumLine", "Include a horizontal datum line at the reference elevation for each section.")]
    [GCParameter("DrawVerticalDropLines", "Include vertical drop lines from each feature point to its datum.")]
    // Per-section outputs (replicated to match Sections[])
    [GCParameter("ColumnCentreX",      "Per-section X centres of each feature column (IGCObject[][]). Wire to ORAnnotationBand.ColumnCentreXGC.")]
    [GCParameter("AnnotationBandTopY", "Per-section Y of the top edge of the annotation band (feed to ORAnnotationBand).")]
    [GCParameter("ProfileLeftX", "Per-section X of the leftmost feature point.")]
    [GCParameter("ProfileRightX", "Per-section X of the rightmost feature point.")]
    [GCParameter("ProfileWidth", "Per-section total profile width in sheet units.")]
    [GCParameter("ProfileHeight", "Per-section profile height from datum to highest design point.")]
    [GCParameter("CentreLineX", "Per-section X of the centreline.")]
    [GCParameter("DatumY", "Per-section Y of the datum line.")]
    [GCParameter("SectionOriginX", "Per-section X origin used (column offset applied).")]
    [GCParameter("RowIndex", "Per-section row index in the layout (0-based, top to bottom).")]
    [GCParameter("ColumnIndex", "Per-section column index in the layout (0-based, left to right).")]
    [GCParameter("StationLabel", "Per-section station labels.")]
    // Global outputs
    [GCParameter("ScaleLabel", "Human-readable scale label, e.g. 'H 1:200 / V 1:100 (×2 V.E.)'.")]
    [GCParameter("EffectiveVScaleDenom", "Computed vertical scale denominator after exaggeration.")]
    [GCParameter("ColumnCount", "Total number of columns used in the layout.")]
    public NodeUpdateResult PlaceMultipleInDgn
(
    NodeUpdateContext updateContext,
    [GCIn] DgnModel DrawingModel,
    [GCIn] CrossSectionData[] Sections,
    [GCIn] double HorizontalScaleDenominator,
    [GCIn] double VerticalExaggerationFactor,
    [GCIn] double OriginX,
    [GCIn] double OriginY,
    [GCIn] int MaxRowsPerColumn,
    [GCIn] double ColumnSpacingMultiplier,
    [GCIn] double RowSpacing,
    [GCIn] double ProfileBottomClearance,
    [GCIn] double TickHalfHeight,
    [GCIn] bool DrawDatumLine,
    [GCIn] bool DrawVerticalDropLines,

    // Per-section outputs
    [GCOut, GCReplicatable, GCInitiallyPinned] ref IGCObject[][] ColumnCentreX,
    [GCOut, GCReplicatable, GCInitiallyPinned] ref double[] AnnotationBandTopY,
    [GCOut, GCReplicatable, GCInitiallyPinned] ref double[] ProfileLeftX,
    [GCOut, GCReplicatable, GCInitiallyPinned] ref double[] ProfileRightX,
    [GCOut, GCReplicatable, GCInitiallyPinned] ref double[] ProfileWidth,
    [GCOut, GCReplicatable, GCInitiallyPinned] ref double[] ProfileHeight,
    [GCOut, GCReplicatable, GCInitiallyPinned] ref double[] CentreLineX,
    [GCOut, GCReplicatable, GCInitiallyPinned] ref double[] DatumY,
    [GCOut, GCReplicatable, GCInitiallyPinned] ref double[] SectionOriginX,
    [GCOut, GCReplicatable, GCInitiallyPinned] ref int[] RowIndex,
    [GCOut, GCReplicatable, GCInitiallyPinned] ref int[] ColumnIndex,
    [GCOut, GCReplicatable, GCInitiallyPinned] ref string[] StationLabel,

    // Global outputs
    [GCOut, GCInitiallyPinned] ref string ScaleLabel,
    [GCOut, GCInitiallyPinned] ref double EffectiveVScaleDenom,
    [GCOut, GCInitiallyPinned] ref int ColumnCount
)
    {
        try
        {
            if (DrawingModel == null)
                return new NodeUpdateResult.IncompleteInputs(nameof(DrawingModel));
            if (Sections == null || Sections.Length == 0)
                return new NodeUpdateResult.IncompleteInputs(nameof(Sections));

            // --- Call the multi-section builder to compute everything in sheet space ---
            IGCObject[][] designPts   = Array.Empty<IGCObject[]>();
            IGCObject[][] existingPts = Array.Empty<IGCObject[]>();
            IGCObject[][] datumPts    = Array.Empty<IGCObject[]>();
            IGCObject[][] tickPts     = Array.Empty<IGCObject[]>();
            IGCObject[][] dropPts     = Array.Empty<IGCObject[]>();
            IGCObject[][] colCentres  = Array.Empty<IGCObject[]>();
            double[] annTopY = Array.Empty<double>();
            double[] leftX = Array.Empty<double>();
            double[] rightX = Array.Empty<double>();
            double[] profWidth = Array.Empty<double>();
            double[] profHeight = Array.Empty<double>();
            double[] clX = Array.Empty<double>();
            double[] datumY = Array.Empty<double>();
            double[] secOriginX = Array.Empty<double>();
            int[] rowIdx = Array.Empty<int>();
            int[] colIdx = Array.Empty<int>();
            string[] stnLabel = Array.Empty<string>();
            string scaleLabel = string.Empty;
            double effVDenom = 0.0;
            int colCount = 0;

            var buildResult = Build2DProfiles(
                updateContext,
                DrawingModel,
                Sections,
                HorizontalScaleDenominator,
                VerticalExaggerationFactor,
                OriginX,
                OriginY,
                MaxRowsPerColumn,
                ProfileBottomClearance,
                TickHalfHeight,
                DrawDatumLine,
                DrawVerticalDropLines,
                ColumnSpacingMultiplier,
                RowSpacing,
                ref designPts,
                ref existingPts,
                ref datumPts,
                ref tickPts,
                ref dropPts,
                ref colCentres,
                ref annTopY,
                ref leftX,
                ref rightX,
                ref profWidth,
                ref profHeight,
                ref clX,
                ref datumY,
                ref secOriginX,
                ref rowIdx,
                ref colIdx,
                ref stnLabel,
                ref effVDenom,
                ref scaleLabel,
                ref colCount
            );

            if (buildResult != NodeUpdateResult.Success)
                return buildResult;

            // --- Place elements in DGN for each section ---
            // Unbox IGCObject[] → DPoint3d[] before creating DGN elements.
            // Each element is registered with SetElement so GC can delete it on rerun.
            Unboxer unboxer = GCEnvironment().Unboxer;
            int n = Sections.Length;

            for (int i = 0; i < n; i++)
            {
                // Design profile
                if (designPts != null && i < designPts.Length && designPts[i] != null && designPts[i].Length >= 2)
                {
                    var pts = designPts[i].Select(o => unboxer.Unbox<DPoint3d>(o)).ToArray();
                    var e = new LineStringElement(DrawingModel, null, pts);
                    e.AddToModel();
                    SetElement(e);
                }

                // Existing profile (terrain) if available
                if (existingPts != null && i < existingPts.Length && existingPts[i] != null && existingPts[i].Length >= 2)
                {
                    var pts = existingPts[i].Select(o => unboxer.Unbox<DPoint3d>(o)).ToArray();
                    var e = new LineStringElement(DrawingModel, null, pts);
                    e.AddToModel();
                    SetElement(e);
                }

                // Datum line (horizontal)
                if (DrawDatumLine && datumPts != null && i < datumPts.Length && datumPts[i] != null && datumPts[i].Length == 2)
                {
                    var dp = datumPts[i].Select(o => unboxer.Unbox<DPoint3d>(o)).ToArray();
                    var seg = new Bentley.GeometryNET.DSegment3d(dp[0], dp[1]);
                    var e = new LineElement(DrawingModel, null, seg);
                    e.AddToModel();
                    SetElement(e);
                }

                // Short vertical tick at CL on the datum
                if (i < clX.Length && i < datumY.Length)
                {
                    var p0 = new Bentley.GeometryNET.DPoint3d(clX[i], datumY[i] - 10.0, 0);
                    var p1 = new Bentley.GeometryNET.DPoint3d(clX[i], datumY[i] + 10.0, 0);
                    var e = new LineElement(DrawingModel, null, new Bentley.GeometryNET.DSegment3d(p0, p1));
                    e.AddToModel();
                    SetElement(e);
                }

                // Feature ticks
                if (tickPts != null && i < tickPts.Length && tickPts[i] != null)
                {
                    var arr = tickPts[i].Select(o => unboxer.Unbox<DPoint3d>(o)).ToArray();
                    for (int k = 0; k + 1 < arr.Length; k += 2)
                    {
                        var e = new LineElement(DrawingModel, null, new Bentley.GeometryNET.DSegment3d(arr[k], arr[k + 1]));
                        e.AddToModel();
                        SetElement(e);
                    }
                }

                // Drop lines
                if (DrawVerticalDropLines && dropPts != null && i < dropPts.Length && dropPts[i] != null)
                {
                    var arr = dropPts[i].Select(o => unboxer.Unbox<DPoint3d>(o)).ToArray();
                    for (int k = 0; k + 1 < arr.Length; k += 2)
                    {
                        var e = new LineElement(DrawingModel, null, new Bentley.GeometryNET.DSegment3d(arr[k], arr[k + 1]));
                        e.AddToModel();
                        SetElement(e);
                    }
                }
            }

            // Return outputs to GC (for downstream nodes like ORAnnotationBand)
            ColumnCentreX = colCentres;
            AnnotationBandTopY = annTopY;
            ProfileLeftX = leftX;
            ProfileRightX = rightX;
            ProfileWidth = profWidth;
            ProfileHeight = profHeight;
            CentreLineX = clX;
            DatumY = datumY;
            SectionOriginX = secOriginX;
            RowIndex = rowIdx;
            ColumnIndex = colIdx;
            StationLabel = stnLabel;

            ScaleLabel = scaleLabel;
            EffectiveVScaleDenom = effVDenom;
            ColumnCount = colCount;
        }
        catch (Exception ex)
        {
            return new NodeUpdateResult.TechniqueException(ex);
        }

        return NodeUpdateResult.Success;
    }*/
    }
}
