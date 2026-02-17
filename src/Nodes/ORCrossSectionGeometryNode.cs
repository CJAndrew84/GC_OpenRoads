using System;
using System.Collections.Generic;
using System.Linq;
using Bentley.GenerativeComponents.AddInSupport;
using Bentley.GenerativeComponents.GCScript;
using Bentley.GenerativeComponents.ElementBasedNodes;
using Bentley.DgnPlatformNET.Elements;
using Bentley.GeometryNET;
using Bentley.MstnPlatformNET;
using BDPnet = Bentley.DgnPlatformNET;
using GC_OpenRoads_CrossSections.Models;
using GC_OpenRoads.Utilities;

namespace GC_OpenRoads.Nodes
{
    [GCNamespace("ORD")]
    [GCNodeTypePaletteCategory("OpenRoads - Cross Sections")]
    [GCNodeTypeIcon("Resources/CrossSection.png")]
    [GCSummary("Converts cross-section data into 2-D sheet-space geometry. " +
               "Supports vertical exaggeration (independent H and V scales). " +
               "Outputs ColumnCentreX and AnnotationBandTopY for wiring to ORAnnotationBand.")]
    public class ORCrossSectionGeometryNode : GeometricNode
    {
        private static readonly BDPnet.DgnFile  _dgnFile  = Session.Instance.GetActiveDgnFile();
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
        [GCParameter("SectionData",               "Cross-section data from ORCrossSectionData node.")]
        [GCParameter("HorizontalScaleDenominator","Scale denominator for horizontal axis, e.g. 200 for 1:200.")]
        [GCParameter("VerticalExaggerationFactor", "VE factor: 1 = true scale, 2 = twice as tall as horizontal.")]
        [GCParameter("OriginX",                   "Sheet X of the datum / CL (master units).")]
        [GCParameter("OriginY",                   "Sheet Y of the datum line (master units).")]
        [GCParameter("ProfileBottomClearance",     "Gap below lowest profile point before annotation band (master units).")]
        [GCParameter("TickHalfHeight",             "Half-height of feature point tick marks (master units).")]
        [GCParameter("DrawDatumLine",              "Include a horizontal datum line at the reference elevation.")]
        [GCParameter("DrawVerticalDropLines",      "Include vertical drop lines from each feature point to datum.")]
        [GCParameter("DesignProfilePoints",        "Polyline through the proposed design points (sheet space).")]
        [GCParameter("ExistingProfilePoints",      "Polyline through existing ground points (empty if no terrain).")]
        [GCParameter("DatumLinePoints",            "Two points defining the horizontal datum line.")]
        [GCParameter("TickMarkPoints",             "Paired points (base, tip) for each feature point tick.")]
        [GCParameter("DropLinePoints",             "Paired points (feature Y, datum Y) for drop lines.")]
        [GCParameter("ColumnCentreX",              "Sheet X centre of each feature point column — feed to ORAnnotationBand.")]
        [GCParameter("AnnotationBandTopY",         "Y of top edge of annotation band — feed to ORAnnotationBand.")]
        [GCParameter("ProfileLeftX",               "X of leftmost feature point.")]
        [GCParameter("ProfileRightX",              "X of rightmost feature point.")]
        [GCParameter("ProfileWidth",               "Total profile width in sheet units.")]
        [GCParameter("ProfileHeight",              "Profile height from datum to highest point.")]
        [GCParameter("CentreLineX",                "X of the centreline.")]
        [GCParameter("DatumY",                     "Y of the datum line.")]
        [GCParameter("EffectiveVScaleDenom",       "Computed vertical scale denominator after exaggeration.")]
        [GCParameter("ScaleLabel",                 "Human-readable scale string, e.g. 'H 1:200 / V 1:100 (×2 V.E.)'.")]
        [GCParameter("StationLabel",               "Station label for this section, e.g. '1+234.567'.")]
        public NodeUpdateResult Build2DProfile
        (
            NodeUpdateContext updateContext,
            [GCIn]  CrossSectionData SectionData,
            [GCIn]  double           HorizontalScaleDenominator,
            [GCIn]  double           VerticalExaggerationFactor,
            [GCIn]  double           OriginX,
            [GCIn]  double           OriginY,
            [GCIn]  double           ProfileBottomClearance,
            [GCIn]  double           TickHalfHeight,
            [GCIn]  bool             DrawDatumLine,
            [GCIn]  bool             DrawVerticalDropLines,
            [GCOut, GCInitiallyPinned] ref DPoint3d[] DesignProfilePoints,
            [GCOut, GCInitiallyPinned] ref DPoint3d[] ExistingProfilePoints,
            [GCOut, GCInitiallyPinned] ref DPoint3d[] DatumLinePoints,
            [GCOut, GCInitiallyPinned] ref DPoint3d[] TickMarkPoints,
            [GCOut, GCInitiallyPinned] ref DPoint3d[] DropLinePoints,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref double[]   ColumnCentreX,
            [GCOut, GCReplicatable, GCInitiallyPinned] ref double     AnnotationBandTopY,
            [GCOut, GCInitiallyPinned] ref double     ProfileLeftX,
            [GCOut, GCInitiallyPinned] ref double     ProfileRightX,
            [GCOut, GCInitiallyPinned] ref double     ProfileWidth,
            [GCOut, GCInitiallyPinned] ref double     ProfileHeight,
            [GCOut, GCInitiallyPinned] ref double     CentreLineX,
            [GCOut, GCInitiallyPinned] ref double     DatumY,
            [GCOut, GCInitiallyPinned] ref double     EffectiveVScaleDenom,
            [GCOut, GCInitiallyPinned] ref string     ScaleLabel,
            [GCOut, GCInitiallyPinned] ref string     StationLabel
        )
        {
            try
            {
                if (SectionData == null || SectionData.Points.Count == 0)
                    return new NodeUpdateResult.IncompleteInputs(nameof(SectionData));

                double hd  = HorizontalScaleDenominator <= 0 ? 200 : HorizontalScaleDenominator;
                double vef = VerticalExaggerationFactor  <= 0 ? 1   : VerticalExaggerationFactor;
                double vd  = hd / vef;

                EffectiveVScaleDenom = vd;
                ScaleLabel = Math.Abs(vef - 1.0) < 1e-6
                    ? $"1:{hd:G} (true scale)"
                    : $"H 1:{hd:G}  /  V 1:{vd:G}  (×{vef:G} V.E.)";

                var pts = SectionData.Points;

                // Reference point: prefer CL
                var clPt = pts.FirstOrDefault(p =>
                    p.PointCode.Equals("CL", StringComparison.OrdinalIgnoreCase));
                double refOff  = clPt?.Offset    ?? 0.0;
                double refElev = clPt?.Elevation ?? pts.Min(p => p.Elevation);

                DatumY = OriginY;

                var profile    = new List<DPoint3d>(pts.Count);
                var ticks      = new List<DPoint3d>(pts.Count * 2);
                var drops      = new List<DPoint3d>(pts.Count * 2);
                var colCentres = new List<double>(pts.Count);

                double minX = double.MaxValue, maxX = double.MinValue;
                double minY = double.MaxValue, maxY = double.MinValue;

                foreach (var pt in pts)
                {
                    double sx = OriginX + (pt.Offset - refOff)   / hd;
                    double sy = OriginY + (pt.Elevation - refElev) / vd;

                    profile.Add(new DPoint3d(sx, sy, 0));
                    ticks.Add(new DPoint3d(sx, sy - TickHalfHeight, 0));
                    ticks.Add(new DPoint3d(sx, sy + TickHalfHeight, 0));
                    drops.Add(new DPoint3d(sx, sy,     0));
                    drops.Add(new DPoint3d(sx, DatumY, 0));
                    colCentres.Add(sx);

                    if (sx < minX) minX = sx;
                    if (sx > maxX) maxX = sx;
                    if (sy < minY) minY = sy;
                    if (sy > maxY) maxY = sy;
                }

                DesignProfilePoints  = profile.ToArray();
                TickMarkPoints       = ticks.ToArray();
                DropLinePoints       = DrawVerticalDropLines ? drops.ToArray() : Array.Empty<DPoint3d>();
                ColumnCentreX        = colCentres.ToArray();
                ProfileLeftX         = minX;
                ProfileRightX        = maxX;
                ProfileWidth         = maxX - minX;
                ProfileHeight        = maxY - DatumY;
                CentreLineX          = OriginX + (refOff - refOff) / hd;
                StationLabel         = SectionData.StationLabel;
                AnnotationBandTopY   = minY - ProfileBottomClearance;

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
        [GCSummary("Computes 2-D geometry and places LineString elements into the active DGN model.")]
        [GCParameter("SectionData",               "Cross-section data.")]
        [GCParameter("HorizontalScaleDenominator","H scale denominator.")]
        [GCParameter("VerticalExaggerationFactor", "Vertical exaggeration factor.")]
        [GCParameter("OriginX",                   "Sheet X of datum (master units).")]
        [GCParameter("OriginY",                   "Sheet Y of datum (master units).")]
        [GCParameter("ProfileBottomClearance",     "Gap below profile before annotation band.")]
        [GCParameter("TickHalfHeight",             "Tick mark half-height.")]
        [GCParameter("DrawDatumLine",              "Draw datum line.")]
        [GCParameter("DrawVerticalDropLines",      "Draw drop lines to datum.")]
        [GCParameter("AnnotationBandTopY",         "Y of top edge of annotation band.")]
        [GCParameter("ColumnCentreX",              "Sheet X centre per feature point column.")]
        [GCParameter("ProfileLeftX",               "X of leftmost feature point.")]
        [GCParameter("ProfileRightX",              "X of rightmost feature point.")]
        [GCParameter("ScaleLabel",                 "Scale description string.")]
        public NodeUpdateResult PlaceInDgn
        (
            NodeUpdateContext updateContext,
            [GCIn]  CrossSectionData SectionData,
            [GCIn]  double           HorizontalScaleDenominator,
            [GCIn]  double           VerticalExaggerationFactor,
            [GCIn]  double           OriginX,
            [GCIn]  double           OriginY,
            [GCIn]  double           ProfileBottomClearance,
            [GCIn]  double           TickHalfHeight,
            [GCIn]  bool             DrawDatumLine,
            [GCIn]  bool             DrawVerticalDropLines,
            [GCOut, GCInitiallyPinned] ref double   AnnotationBandTopY,
            [GCOut, GCInitiallyPinned] ref double[] ColumnCentreX,
            [GCOut, GCInitiallyPinned] ref double   ProfileLeftX,
            [GCOut, GCInitiallyPinned] ref double   ProfileRightX,
            [GCOut, GCInitiallyPinned] ref string   ScaleLabel
        )
        {
            // Re-use Build2DProfile to compute all geometry
            DPoint3d[] designPts = Array.Empty<DPoint3d>();
            DPoint3d[] existingPts = Array.Empty<DPoint3d>();
            DPoint3d[] datumPts = Array.Empty<DPoint3d>();
            DPoint3d[] tickPts = Array.Empty<DPoint3d>();
            DPoint3d[] dropPts = Array.Empty<DPoint3d>();
            double[]   colX    = Array.Empty<double>();
            double     bandTopY = 0, leftX = 0, rightX = 0, width = 0, height = 0;
            double     clX = 0, datumY = 0, effVD = 0;
            string     stLabel = "";

            var r = Build2DProfile(updateContext,
                SectionData, HorizontalScaleDenominator, VerticalExaggerationFactor,
                OriginX, OriginY, ProfileBottomClearance, TickHalfHeight,
                DrawDatumLine, DrawVerticalDropLines,
                ref designPts, ref existingPts, ref datumPts, ref tickPts, ref dropPts,
                ref colX, ref bandTopY, ref leftX, ref rightX, ref width, ref height,
                ref clX, ref datumY, ref effVD, ref ScaleLabel, ref stLabel);

            if (r != NodeUpdateResult.Success) return r;

            try
            {
                if (designPts.Length  >= 2) new LineStringElement(_dgnModel, null, designPts).AddToModel();
                if (existingPts.Length >= 2) new LineStringElement(_dgnModel, null, existingPts).AddToModel();
                if (datumPts.Length   == 2) LineElement.Create(_dgnModel, datumPts[0], datumPts[1]).AddToModel();

                for (int i = 0; i + 1 < tickPts.Length; i += 2)
                    LineElement.Create(_dgnModel, tickPts[i], tickPts[i + 1]).AddToModel();
                for (int i = 0; i + 1 < dropPts.Length; i += 2)
                    LineElement.Create(_dgnModel, dropPts[i], dropPts[i + 1]).AddToModel();

                AnnotationBandTopY = bandTopY;
                ColumnCentreX      = colX;
                ProfileLeftX       = leftX;
                ProfileRightX      = rightX;
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }
    }
}
