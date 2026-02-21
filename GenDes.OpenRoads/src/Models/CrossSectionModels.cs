// =============================================================================
//  Models/CrossSectionModels.cs
//
//  Plain-old C# objects (POCOs) that carry OpenRoads cross-section data
//  between GC nodes.  These are passed as GC node property values and can
//  also be serialised for reporting.
// =============================================================================

using System;
using System.Collections.Generic;

namespace GenDes_OpenRoads_CrossSections.Models
{
    // -------------------------------------------------------------------------
    /// <summary>
    /// A single named point on a cross section (e.g. "CL", "EOP_L", "EOP_R",
    /// "DAYLIGHT_L", "DAYLIGHT_R").  Offset is positive to the right, negative
    /// to the left when standing in the direction of increasing station.
    /// </summary>
    // -------------------------------------------------------------------------
    public class CrossSectionPoint
    {
        /// <summary>
        /// Corridor feature name from XSCutPoint.PointName (e.g. "EOP_L", "DAYLIGHT_L", "CL").
        /// This is the short name used in the corridor template.
        /// </summary>
        public string FeatureName { get; set; } = string.Empty;
        /// <summary>
        /// Cleaned feature definition name from XSCutPoint.PointFeatureName.
        /// The SDK returns a full file path (e.g. "C:\...\EOP.xml"); this stores
        /// the filename only (e.g. "EOP.xml") after path stripping.
        /// Used as the display label in the annotation band Point Label row.
        /// </summary>
        public string PointCode   { get; set; } = string.Empty;

        /// <summary>
        /// Rich feature metadata used to drive cross-section styling,
        /// color assignment, and cell placement by feature definition.
        /// Optional: existing consumers can ignore this safely.
        /// </summary>
        public FeatureDisplayMetadata DisplayMetadata { get; set; } = new();

        /// <summary>Horizontal offset from the alignment centreline (metres).</summary>
        public double Offset    { get; set; }

        /// <summary>Proposed (design) elevation above datum (metres).</summary>
        public double Elevation { get; set; }

        /// <summary>
        /// Existing ground elevation at this offset (metres).
        /// Populated by sampling the terrain model at the same offset.
        /// Null when no terrain model is available.
        /// </summary>
        public double? ExistingElevation { get; set; }

        /// <summary>
        /// World 3-D coordinates of this point (from XSCutPoint.PointOnPlan).
        /// X and Y are easting/northing in the file coordinate system (metres).
        /// Z is the elevation (metres).
        /// These are the plan coordinates; use Offset/Elevation for the 2-D cross-section view.
        /// </summary>
        public double WorldX { get; set; }
        public double WorldY { get; set; }
        public double WorldZ { get; set; }

        /// <summary>
        /// Cut/fill depth relative to existing ground.
        /// Positive = cut (design below existing), negative = fill.
        /// Computed by the data node as (ExistingElevation - Elevation) when
        /// terrain data is available; otherwise 0.
        /// Not a native field on XSCutPoint — derived here.
        /// </summary>
        public double CutFill   { get; set; }

        /// <summary>
        /// Arbitrary extra values keyed by the user's row label.
        /// Used by the annotation band for custom rows such as "Delta", "Grade%",
        /// "Superelevation", etc.  Populated by user-supplied delegates in
        /// AnnotationRowDefinition.ValueProvider.
        /// </summary>
        public Dictionary<string, string> CustomValues { get; set; } = new();

        public override string ToString() =>
            $"{PointCode} ({FeatureName}): offset={Offset:F3}  proposed={Elevation:F3}" +
            (ExistingElevation.HasValue ? $"  existing={ExistingElevation:F3}" : "") +
            $"  cut/fill={CutFill:F3}" +
            $"  world=({WorldX:F3},{WorldY:F3},{WorldZ:F3})";
    }

    // -------------------------------------------------------------------------
    /// <summary>
    /// All data for one cross section at a given station.
    /// </summary>
    // -------------------------------------------------------------------------
    public class CrossSectionData
    {
        /// <summary>Chainage / station value (metres).</summary>
        public double Station { get; set; }

        /// <summary>Station formatted as a string (e.g. "1+234.567").</summary>
        public string StationLabel { get; set; } = string.Empty;

        /// <summary>Named section points from the corridor template.</summary>
        public List<CrossSectionPoint> Points { get; set; } = new();

        /// <summary>
        /// Existing ground profile points sampled at this station.
        /// Each entry is (offset, elevation).
        /// </summary>
        public List<(double Offset, double Elevation)> TerrainPoints { get; set; } = new();

        /// <summary>
        /// Optional 3D model elements intersected by the cross-section plane,
        /// represented in section coordinates for drawing/display.
        /// </summary>
        public List<CrossSectionCutElement> CutElements { get; set; } = new();

        // ------------------------------------------------------------------
        //  Derived quantities (computed on first access)
        // ------------------------------------------------------------------

        private double? _cutArea;
        private double? _fillArea;

        public double CutArea => _cutArea ??= ComputeCutArea();
        public double FillArea => _fillArea ??= ComputeFillArea();

        // Simple trapezoidal integration over the section points.
        private double ComputeCutArea()
        {
            double area = 0.0;
            for (int i = 1; i < Points.Count; i++)
            {
                double cf0 = Points[i - 1].CutFill;
                double cf1 = Points[i].CutFill;
                if (cf0 > 0 || cf1 > 0)
                {
                    double dx = Points[i].Offset - Points[i - 1].Offset;
                    area += 0.5 * (Math.Max(cf0, 0) + Math.Max(cf1, 0)) * Math.Abs(dx);
                }
            }
            return area;
        }

        private double ComputeFillArea()
        {
            double area = 0.0;
            for (int i = 1; i < Points.Count; i++)
            {
                double cf0 = Points[i - 1].CutFill;
                double cf1 = Points[i].CutFill;
                if (cf0 < 0 || cf1 < 0)
                {
                    double dx = Points[i].Offset - Points[i - 1].Offset;
                    area += 0.5 * (Math.Abs(Math.Min(cf0, 0)) + Math.Abs(Math.Min(cf1, 0))) * Math.Abs(dx);
                }
            }
            return area;
        }

        public override string ToString() =>
            $"Station {StationLabel}: {Points.Count} points, cut={CutArea:F2}m², fill={FillArea:F2}m²";
    }

    // -------------------------------------------------------------------------
    /// <summary>
    /// Represents the layout envelope for one cross section on a drawing sheet.
    /// GC uses this to position and scale section graphics.
    /// </summary>
    // -------------------------------------------------------------------------
    public class CrossSectionLayout
    {
        public CrossSectionData Data { get; set; } = new();

        /// <summary>Sheet (paper) X origin in UOR master units.</summary>
        public double SheetX { get; set; }

        /// <summary>Sheet (paper) Y origin in UOR master units.</summary>
        public double SheetY { get; set; }

        /// <summary>Total width allocated on sheet (master units).</summary>
        public double Width  { get; set; }

        /// <summary>Total height allocated on sheet (master units).</summary>
        public double Height { get; set; }

        /// <summary>Horizontal scale factor (e.g. 1:200 → 0.005).</summary>
        public double HorizontalScale { get; set; } = 1.0 / 200.0;

        /// <summary>Vertical scale factor — often same as horizontal, or exaggerated.</summary>
        public double VerticalScale   { get; set; } = 1.0 / 200.0;

        /// <summary>True if the section has been placed on a sheet model.</summary>
        public bool   IsPlaced        { get; set; }
    }

    // -------------------------------------------------------------------------
    /// <summary>
    /// Named Boundary settings fed from the GC node into the ORD API.
    /// </summary>
    // -------------------------------------------------------------------------
    public class NamedBoundarySettings
    {
        public string AlignmentName  { get; set; } = string.Empty;
        public double StartStation   { get; set; }
        public double EndStation     { get; set; }
        public double StationInterval { get; set; } = 10.0;   // metres
        public double LeftOffset     { get; set; } = 20.0;   // metres from CL
        public double RightOffset    { get; set; } = 20.0;
        public double BottomClearance { get; set; } = 2.0;
        public double TopClearance   { get; set; } = 2.0;
        public string DrawingSeedName { get; set; } = "Cross Section";
        public string SheetSeedFile  { get; set; } = string.Empty;
    }

    // =========================================================================
    //  FEATURE / SYMBOLOGY DISPLAY METADATA
    // =========================================================================

    /// <summary>
    /// Normalized feature-definition information for one cross-section point.
    /// </summary>
    public class FeatureDisplayMetadata
    {
        /// <summary>Short corridor/template feature name, e.g. "EOP_L".</summary>
        public string FeatureName { get; set; } = string.Empty;

        /// <summary>Raw SDK value from XSCutPoint.PointFeatureName.</summary>
        public string FeatureDefinitionRaw { get; set; } = string.Empty;

        /// <summary>Leaf definition name, e.g. "EOP.xml".</summary>
        public string FeatureDefinitionName { get; set; } = string.Empty;

        /// <summary>
        /// Normalized hierarchy path using '/' separators,
        /// with filename removed.
        /// </summary>
        public string FeatureDefinitionPath { get; set; } = string.Empty;

        /// <summary>
        /// Optional high-level group for styling rules (e.g. Pavement, Drainage).
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>Optional explicit point/cell symbology mapping.</summary>
        public SymbologyMetadata Symbology { get; set; } = new();

        /// <summary>Optional cell insertion mapping for this feature.</summary>
        public CellDisplayMetadata Cell { get; set; } = new();
    }

    /// <summary>
    /// Explicit display settings resolved for a feature/element.
    /// </summary>
    public class SymbologyMetadata
    {
        public int? ColorIndex { get; set; }
        public int? LineStyle { get; set; }
        public int? LineWeight { get; set; }

        /// <summary>
        /// If true, resolve missing values from ORD feature-definition defaults.
        /// </summary>
        public bool UseFeatureDefinitionDefaults { get; set; } = true;

        /// <summary>
        /// Metadata source marker (FeatureDefinition, RuleTable, NodeOverride, etc.).
        /// </summary>
        public string SymbologySource { get; set; } = "FeatureDefinition";
    }

    /// <summary>
    /// Cell/block placement metadata associated with a feature definition.
    /// </summary>
    public class CellDisplayMetadata
    {
        public string CellName { get; set; } = string.Empty;
        public double Scale { get; set; } = 1.0;
        public double RotationDegrees { get; set; }

        /// <summary>AtPoint, AtDatum, AtBandRow, or custom node-specific mode.</summary>
        public string PlacementMode { get; set; } = "AtPoint";
    }

    /// <summary>
    /// Intersected 3D element footprint captured at a section station.
    /// </summary>
    public class CrossSectionCutElement
    {
        public string ElementId { get; set; } = string.Empty;
        public string SourceModel { get; set; } = string.Empty;
        public string FeatureDefinitionName { get; set; } = string.Empty;

        /// <summary>Section-space polyline points (Offset, Elevation).</summary>
        public List<(double Offset, double Elevation)> SectionPolyline { get; set; } = new();

        /// <summary>Optional world-space trace points (X, Y, Z).</summary>
        public List<(double X, double Y, double Z)> WorldPolyline { get; set; } = new();

        /// <summary>Resolved display style for this cut element.</summary>
        public SymbologyMetadata Symbology { get; set; } = new();
    }

    // =========================================================================
    //  ANNOTATION BAND MODELS
    // =========================================================================

    // -------------------------------------------------------------------------
    /// <summary>
    /// Defines how a single row in the annotation band is labelled, formatted,
    /// and sourced.
    ///
    /// Built-in rows (ProposedLevel, ExistingLevel, Offset, PointLabel) are
    /// created automatically by ORAnnotationBandNode.  Additional rows can be
    /// injected by the user by populating CustomRows on the node, or through a
    /// GC Script Almighty node that appends to the Rows list.
    /// </summary>
    // -------------------------------------------------------------------------
    public class AnnotationRowDefinition
    {
        // ------------------------------------------------------------------
        //  Identity
        // ------------------------------------------------------------------

        /// <summary>
        /// Internal key.  Must be unique within a band.  Used to look up values
        /// in <see cref="CrossSectionPoint.CustomValues"/>.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Text printed in the left-hand label column of the annotation box.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        // ------------------------------------------------------------------
        //  Data source
        // ------------------------------------------------------------------

        /// <summary>
        /// Built-in row types — the band node knows how to populate these
        /// directly from <see cref="CrossSectionPoint"/> fields.
        /// </summary>
        public AnnotationRowType RowType { get; set; } = AnnotationRowType.Custom;

        /// <summary>
        /// For <see cref="AnnotationRowType.Custom"/>: a delegate that receives
        /// a <see cref="CrossSectionPoint"/> and returns the formatted string to
        /// display in that cell.  Set this in a GC Script Almighty node.
        /// </summary>
        public Func<CrossSectionPoint, string>? ValueProvider { get; set; }

        // ------------------------------------------------------------------
        //  Formatting
        // ------------------------------------------------------------------

        /// <summary>Format string for numeric values (e.g. "F3" for 3 d.p.).</summary>
        public string NumericFormat { get; set; } = "F3";

        /// <summary>Row height in sheet master units.</summary>
        public double RowHeight { get; set; } = 5.0;

        /// <summary>Text size in sheet master units.</summary>
        public double TextHeight { get; set; } = 2.5;

        /// <summary>Horizontal text justification within each cell.</summary>
        public CellTextJustification TextJustification { get; set; }
            = CellTextJustification.Centre;

        // ------------------------------------------------------------------
        //  Visual styling
        // ------------------------------------------------------------------

        /// <summary>
        /// If true, this row has a coloured background fill.
        /// (Placed as a filled shape element underneath the text.)
        /// </summary>
        public bool HasBackground { get; set; } = false;

        /// <summary>Background colour index (MicroStation colour table index).</summary>
        public int BackgroundColour { get; set; } = 0;

        /// <summary>
        /// If true, the row is omitted from the band (useful to temporarily
        /// hide a row without removing its definition).
        /// </summary>
        public bool Hidden { get; set; } = false;

        public override string ToString() => $"[{Key}] {Label}";
    }

    // -------------------------------------------------------------------------
    /// <summary>Identifies the built-in annotation row types.</summary>
    // -------------------------------------------------------------------------
    public enum AnnotationRowType
    {
        /// <summary>Proposed (design) level at each feature point.</summary>
        ProposedLevel,

        /// <summary>Existing ground level at each feature point offset.</summary>
        ExistingLevel,

        /// <summary>Horizontal offset from the alignment CL (metres).</summary>
        Offset,

        /// <summary>The point code label (e.g. "CL", "EOP_L").</summary>
        PointLabel,

        /// <summary>Difference between proposed and existing level (Δ).</summary>
        Delta,

        /// <summary>Cut/fill value (positive = cut, negative = fill).</summary>
        CutFill,

        /// <summary>User-defined row; value comes from <see cref="AnnotationRowDefinition.ValueProvider"/>.</summary>
        Custom,
    }

    // -------------------------------------------------------------------------
    /// <summary>Text justification within annotation cells.</summary>
    // -------------------------------------------------------------------------
    public enum CellTextJustification
    {
        Left,
        Centre,
        Right
    }

    // -------------------------------------------------------------------------
    /// <summary>
    /// Computed geometry and text for a fully-resolved annotation band, ready
    /// to be placed into the DGN by the geometry node.
    /// </summary>
    // -------------------------------------------------------------------------
    public class AnnotationBandResult
    {
        /// <summary>The section this band belongs to.</summary>
        public CrossSectionData Section { get; set; } = new();

        /// <summary>Lower-left corner of the annotation box in sheet space.</summary>
        public double OriginX { get; set; }
        public double OriginY { get; set; }

        /// <summary>Total width of the annotation box (matches the section's profile width).</summary>
        public double TotalWidth  { get; set; }

        /// <summary>Total height of the annotation box (sum of all row heights + borders).</summary>
        public double TotalHeight { get; set; }

        /// <summary>
        /// One entry per row definition, in top-to-bottom order.
        /// Each entry maps column index → cell text.
        /// </summary>
        public List<AnnotationBandRow> Rows { get; set; } = new();

        /// <summary>X positions for vertical divider lines (one per feature point + outer edges).</summary>
        public List<double> ColumnDividerX { get; set; } = new();

        /// <summary>Y positions for horizontal divider lines (one per row boundary).</summary>
        public List<double> RowDividerY { get; set; } = new();

        /// <summary>The label column width (left edge of the box).</summary>
        public double LabelColumnWidth { get; set; } = 20.0;
    }

    // -------------------------------------------------------------------------
    /// <summary>One fully-computed row in the annotation band.</summary>
    // -------------------------------------------------------------------------
    public class AnnotationBandRow
    {
        public AnnotationRowDefinition Definition { get; set; } = new();

        /// <summary>Y coordinate of the top edge of this row in sheet space.</summary>
        public double TopY { get; set; }

        /// <summary>Y coordinate of the bottom edge in sheet space.</summary>
        public double BottomY { get; set; }

        /// <summary>Text for the left-hand label cell.</summary>
        public string LabelText { get; set; } = string.Empty;

        /// <summary>
        /// Cell values indexed by column (feature point index, left to right).
        /// </summary>
        public List<AnnotationBandCell> Cells { get; set; } = new();
    }

    // -------------------------------------------------------------------------
    /// <summary>One cell in the annotation band grid.</summary>
    // -------------------------------------------------------------------------
    public class AnnotationBandCell
    {
        /// <summary>Column index (matches index in CrossSectionData.Points).</summary>
        public int ColumnIndex { get; set; }

        /// <summary>The feature point this cell corresponds to.</summary>
        public CrossSectionPoint? Point { get; set; }

        /// <summary>Formatted text to display.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>X centre of the cell in sheet space.</summary>
        public double CellCentreX { get; set; }

        /// <summary>Y centre of the cell in sheet space.</summary>
        public double CellCentreY { get; set; }
    }
}
