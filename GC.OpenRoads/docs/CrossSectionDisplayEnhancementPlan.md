# Cross-Section Display Enhancement Plan

## Scope

This plan focuses on enhancing the *current* cross-section pipeline under `GC.OpenRoads/src` (not the legacy root `src`).

Current flow:

1. `OpenRoadsHelper.ExtractCrossSectionAtStation()` pulls `XSCutPoint` data (`FeatureName`, `PointFeatureName`, world coordinates).  
2. `CrossSectionData` + `CrossSectionPoint` carry this data between nodes.  
3. `ORCrossSectionGeometryNode` builds 2D geometry from offsets/elevations.  
4. `ORAnnotationBandNode` renders tabular rows, including custom values.

## Current Gaps (relative to requested outcome)

The current model stores point names and derived values, but does **not** yet carry enough metadata to drive styling or per-feature symbol placement in a robust way:

- No explicit feature definition hierarchy metadata (name, branch, full path components).
- No explicit symbology metadata (color/weight/style/line type source, fill behavior).
- No cell/block mapping metadata for feature-based symbol insertion.
- No explicit model for 3D elements intersected by the section plane.
- Existing annotation cells are value-oriented, but not tied to feature/symbology provenance.

## Target Data Model Additions

Add optional metadata models so existing nodes continue to work while new display logic can be progressively enabled.

### 1) Feature Definition Metadata

Introduce a lightweight normalized descriptor:

```csharp
public class FeatureDisplayMetadata
{
    public string FeatureName { get; set; } = string.Empty;          // e.g., EOP_L
    public string FeatureDefinitionRaw { get; set; } = string.Empty; // from PointFeatureName
    public string FeatureDefinitionName { get; set; } = string.Empty; // cleaned leaf
    public string FeatureDefinitionPath { get; set; } = string.Empty; // normalized hierarchy
    public string Category { get; set; } = string.Empty;              // Pavement/Drainage/etc (optional)
}
```

Attach this to `CrossSectionPoint` as an optional property (`DisplayMetadata`) to avoid breaking existing consumers.

### 2) Symbology Metadata

Capture explicit display intent independent of DGN element defaults:

```csharp
public class SymbologyMetadata
{
    public int? ColorIndex { get; set; }
    public int? LineStyle { get; set; }
    public int? LineWeight { get; set; }
    public bool UseFeatureDefinitionDefaults { get; set; } = true;
    public string SymbologySource { get; set; } = string.Empty; // FeatureDef, RuleTable, Override
}
```

`CrossSectionPoint` should reference this through `DisplayMetadata` (or directly via `PointSymbology`).

### 3) Cell Association Metadata

Add mapping data so feature definitions can drive block/cell placement in section views and/or annotation bands:

```csharp
public class CellDisplayMetadata
{
    public string CellName { get; set; } = string.Empty;
    public double Scale { get; set; } = 1.0;
    public double RotationDegrees { get; set; } = 0.0;
    public string PlacementMode { get; set; } = "AtPoint"; // AtPoint, AtDatum, AtBandRow
}
```

### 4) 3D Cut Element Representation

Represent intersected 3D elements as independent records linked to each station:

```csharp
public class CrossSectionCutElement
{
    public string ElementId { get; set; } = string.Empty;
    public string SourceModel { get; set; } = string.Empty;
    public string FeatureDefinitionName { get; set; } = string.Empty;

    // Intersection footprint in section coordinates
    public List<(double Offset, double Elevation)> SectionPolyline { get; set; } = new();

    // Optional world-space points for traceability/debug
    public List<(double X, double Y, double Z)> WorldPolyline { get; set; } = new();

    // Styling
    public SymbologyMetadata Symbology { get; set; } = new();
}
```

Then add to `CrossSectionData`:

```csharp
public List<CrossSectionCutElement> CutElements { get; set; } = new();
```

## Extraction Strategy

## A) Feature + Symbology Enrichment Layer

Implement as a post-processing stage inside `OpenRoadsHelper.ExtractCrossSectionAtStation()`:

1. Keep existing `GetXSCutPoints()` extraction unchanged.
2. For each point, normalize `PointFeatureName` into structured metadata.
3. Resolve symbology by priority:
   - explicit rule table override,
   - feature definition defaults,
   - node-level fallback.
4. Populate point-level metadata objects.

This makes style resolution deterministic and testable.

## B) 3D Cut Plane Sampling

Add a new helper API (rather than overloading point extraction):

```csharp
public static List<CrossSectionCutElement> ExtractCutElementsAtStation(
    Corridor corridor,
    double stationMaster,
    double leftWidthMaster,
    double rightWidthMaster,
    string[] modelFilters,
    string[] featureFilters)
```

Recommended implementation shape:

1. Build section plane at station from corridor alignment tangent + vertical axis.
2. Collect candidate 3D elements using model + range prefilter.
3. Compute plane intersections per element geometry type.
4. Transform intersection points into section coordinates (offset/elevation).
5. Group/merge by element + feature definition.
6. Attach resolved symbology metadata.

## Node-Level Enhancements

## 1) `ORCrossSectionDataNode`

Add optional outputs:

- `FeatureMetadataJson` (debug/reporting)
- `CutElements` (array per section)
- `SymbologyAudit` (optional diagnostics)

And optional inputs:

- `EnableFeatureSymbology`
- `EnableCut3DElements`
- `ModelFilterCsv`
- `FeatureFilterCsv`

## 2) `ORCrossSectionGeometryNode`

Add optional outputs to support display orchestration:

- `FeatureStyledPolylines`
- `FeatureMarkerPoints` (for cells)
- `CutElementPolylines`

Include a style resolution switch:

- `StyleMode = FeatureDefinition | NodeOverrides | Hybrid`

## 3) `ORAnnotationBandNode`

Extend to support feature-driven cell and color behavior:

- Per-row/per-column color assignment based on feature category.
- Optional cell placement in band rows (`AtBandRow`).
- New built-in row types:
  - `FeatureDefinition`
  - `SymbologyName`
  - `CellName`

## Rendering/Display Rules

Use a deterministic precedence chain:

1. Explicit node input override.
2. Rule table mapped by feature definition/category.
3. Feature definition defaults from ORD.
4. Global fallback style.

Keep the resolved style on each point/cut element so downstream nodes never need to re-resolve.

## Incremental Delivery Plan

### Milestone 1 — Metadata scaffolding (low risk)

- Add metadata classes and optional properties on existing models.
- Populate feature-definition normalization.
- Preserve existing outputs and behavior.

### Milestone 2 — Symbology resolution

- Add rule-table input + resolver in helper layer.
- Output diagnostics to validate mappings.

### Milestone 3 — Cell association

- Add feature-to-cell mapping and placement points.
- Render cells in geometry/annotation nodes.

### Milestone 4 — 3D cut elements

- Add cut-plane extraction helper and model payload.
- Render cut polylines in 2D cross sections with style mapping.

### Milestone 5 — QA + performance tuning

- Station-by-station profiling.
- Candidate filtering by range/model/feature.
- Caching feature style resolution.

## Validation Checklist

- Same corridor station returns stable feature metadata across runs.
- Symbology precedence works in all three modes (feature, override, hybrid).
- Cell placement remains aligned after scale/VE changes.
- 3D cut elements are clipped to section width and sorted left-to-right.
- Multi-section layout remains overlap-free with cut-element overlays enabled.
