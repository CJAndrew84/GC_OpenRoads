# GC_OpenRoads

I built **GC_OpenRoads** to remove the repetitive parts of corridor cross-section production in **Bentley OpenRoads Designer 2025**.

This is a Generative Components add-on that gives me a parametric workflow for:
- corridor and alignment resolution,
- cross-section extraction,
- annotation + sheet layout,
- IFC export,
- Excel I/O,
- JSON querying,
- and Item Type data management.

> Developed by Chris Andrew

---

## Why I Built It

If you've done this manually, you know the pain:
- extracting sections station by station,
- calculating cut/fill outside the model,
- hand-building annotation tables,
- and then redoing all of it after a design change.

I wanted one graph where I can change interval, limits, scale, or layout and have everything update.

---

## What This Add-On Does

GC_OpenRoads runs as a node set inside Generative Components in OpenRoads 2025.

The core idea is simple: define the alignment/corridor once, then drive downstream geometry, data, annotation, and sheets from that source. The same graph can also push/pull Excel data, query JSON, export IFC, and read/write Item Types.

---

## Project Modules

The solution is split into six assemblies:

| Module | Assembly | Purpose |
|---|---|---|
| `GC.OpenRoads` | `GC.OpenRoads.dll` | Core cross-section workflow |
| `GC.Excel` | `GC.Excel.dll` | Excel read/write helpers |
| `GC.IFC` | `GC.IFC.dll` | Corridor IFC export |
| `GC.JSON` | `GC.JSON.dll` | JSON file/table/path queries |
| `GC.Lists` | `GC.Lists.dll` | Utility list operations |
| `GCItemTypes` | `GC.ItemTypes.dll` | Item Type read/write/attach |

---

## Feature Highlights

### Alignment + Corridor
- Reference an alignment by name.
- Resolve corridor automatically (or override by name).
- Clamp start/end station limits without touching corridor model data.
- Enumerate available alignments/corridors in dropdowns.

### Cross-Section Data
- Extract at fixed intervals or explicit station lists.
- Compute cut/fill areas.
- Compute volumes with average-end-area logic.
- Export section data to CSV.
- Sample against terrain models for existing ground values.

### 2D Section Geometry
- Convert 3D corridor sections into 2D sheet-space geometry.
- Control horizontal/vertical scales independently.
- Apply vertical exaggeration when needed.
- Output design/existing profiles, ticks, datum, and drop lines.
- Provide column center positions for clean annotation alignment.

### Annotation Bands
- Generate table-style annotation boxes under each section.
- Built-in rows: Point Label, Offset, Proposed Level, Existing Level.
- Optional rows: Delta (Δ), Cut/Fill depth.
- Custom row injection through key-value data.
- Control row heights, text size, and numeric formats.
- Preview in graph or place directly in DGN.

### Sheet Layout
- Parametric grid layout for multi-section sheets.
- Configurable rows/columns/margins/gaps.
- Automatically considers annotation band height.
- Reflows sections when settings change.

### Excel Integration
- Read `.xlsx`, `.xlsm`, `.xlsb`, `.xls` into 2D arrays.
- Select worksheet and range (e.g., `A1:F100`).
- Write 2D arrays back into worksheets.
- Includes path dialogs for file selection.

### IFC Export
- Export corridor strings/surfaces to IFC2x3, IFC4, IFC4x3.
- Keep layer/color context on string export.
- Export surfaces as polygonal face-set based elements.
- Tag contour strings with contour metadata.
- Traverse reference nesting with configurable depth.
- Include OpenRoads property sets as IFC property sets.
- Save to `.ifc`, `.ifczip`, or `.ifcxml`.

### JSON Tools
- Read top-level JSON metadata quickly.
- `ReadTable` for JSON array-of-object extraction.
- `ExtractColumn` for named property pullout.
- `QueryPath` for JSONPath queries.
- Optional schema file support for reusable extraction setups.

### List Utilities
- `ListFilter`: apply bool-mask filtering to string arrays.
- `SplitList`: split any list at index.
- `GroupBy`: group 2D table rows by a selected column.

### Item Types
- Inspect Item Type definitions from active DGN.
- Read Item Type property values from elements.
- Attach/write values in bulk.
- Write per-element values from 2D arrays.
- Write formatted counter values.
- Attach one or many Item Types without value writes.

---

## Typical OpenRoads Pipeline

```text
ORAlignmentNode
      │
      ▼
ORCorridorNode
      │
      ▼
ORCrossSectionDataNode  ──────► CSV Report
      │
      ▼
ORCrossSectionGeometryNode
      │
      ▼
ORAnnotationBandNode
      │
      ▼
ORCrossSectionSheetLayoutNode
```

Other node groups:

```text
GCExcel ──► ReadExcel / WriteExcel

GCIFC ──► IfcDatabase ──► SetSite ──► SetProject ──► IFCContent ──► SaveIFC

JSON ──► ReadFile / ReadTable / ExtractColumn / QueryPath

ListFilter / SplitList / GroupBy

GCItemTypes ──► GetItemTypeInfo / ReadItems / WriteItems / AttachItems
```

Use only the parts you need in a given graph.

---

## Node Reference

### GC.OpenRoads — Cross-Section Workflow

| Node | Category | Description |
|---|---|---|
| `ORAlignmentNode` | OpenRoads - Geometry | Reference/query horizontal alignment |
| `ORCorridorNode` | OpenRoads - Geometry | Resolve corridor by alignment or name |
| `ORCrossSectionDataNode` | OpenRoads - Cross Sections | Extract section data + cut/fill |
| `ORCrossSectionGeometryNode` | OpenRoads - Cross Sections | Build 2D section geometry |
| `ORAnnotationBandNode` | OpenRoads - Cross Sections | Build section annotation tables |
| `ORCrossSectionSheetLayoutNode` | OpenRoads - Drawing Production | Place sections across sheet layout |

### GC.Excel

| Node | Category | Technique | Description |
|---|---|---|---|
| `GCExcel` | GC Excel | `ReadExcel` (default) | Read worksheet range into a 2D array |
| `GCExcel` | GC Excel | `WriteExcel` | Write a 2D array into a worksheet |

### GC.IFC

| Node | Category | Technique | Description |
|---|---|---|---|
| `GCIFC` | {CoDe} GC IFC | `IfcDatabase` (default) | Create IFC database by release |
| `GCIFC` | {CoDe} GC IFC | `SetSite` | Define `IfcSite` |
| `GCIFC` | {CoDe} GC IFC | `SetProject` | Define `IfcProject` |
| `GCIFC` | {CoDe} GC IFC | `IFCContent` | Populate corridor geometry/content |
| `GCIFC` | {CoDe} GC IFC | `SaveIFC` | Save IFC output file |

### GC.JSON

| Node | Category | Technique | Description |
|---|---|---|---|
| `JSON` | {CoDe} GC Add-in | `ReadFile` (default) | Read JSON top-level names/values/item count |
| `JSON` | {CoDe} GC Add-in | `ReadTable` | Read array-of-objects to headers + rows |
| `JSON` | {CoDe} GC Add-in | `ExtractColumn` | Extract one property from all items |
| `JSON` | {CoDe} GC Add-in | `QueryPath` | Execute JSONPath and return matches |

### GC.Lists

| Node | Category | Description |
|---|---|---|
| `ListFilter` | GC Lists | Filter `string[]` with `bool[]` mask |
| `SplitList` | {CoDe} GC Lists | Split list at index |
| `GroupBy` | GC Lists | Group 2D rows by selected column |

### GCItemTypes

| Node | Category | Technique | Description |
|---|---|---|---|
| `GCItemTypes` | {CoDe} GC Add-in | `GetItemTypeInfo` (default) | Inspect Item Type and list properties |
| `GCItemTypes` | {CoDe} GC Add-in | `ReadItems` | Read Item Type values |
| `GCItemTypes` | {CoDe} GC Add-in | `WriteItems` | Attach type + write values |
| `GCItemTypes` | {CoDe} GC Add-in | `WriteItemsMultipleValues` | Write per-element values from 2D array |
| `GCItemTypes` | {CoDe} GC Add-in | `WriteCounterItems` | Write formatted incrementing values |
| `GCItemTypes` | {CoDe} GC Add-in | `AttachItems` | Attach type only |
| `GCItemTypes` | {CoDe} GC Add-in | `AttachMultipleItems` | Attach multiple types |

---

## Requirements

| Requirement | Version |
|---|---|
| Bentley OpenRoads Designer | 2025.00 |
| Generative Components | Included with OpenRoads 2025 |
| .NET Framework | 4.8.1 |
| FastExcel | NuGet (GC.Excel) |
| Newtonsoft.Json | NuGet (GC.JSON) |
| GeometryGym.IFC | NuGet (GC.IFC) |

---

## Installation

1. Build `GC_OpenRoads.slnx` in Visual Studio.
2. Copy built assemblies into:
   ```
   C:\Program Files\Bentley\OpenRoads Designer 2025.00\OpenRoadsDesigner\GenerativeComponents\MdlApps\
   ```
3. Deploy these DLLs:
   - `GC.OpenRoads.dll`
   - `GC.Excel.dll`
   - `GC.IFC.dll`
   - `GC.JSON.dll`
   - `GC.Lists.dll`
   - `GC.ItemTypes.dll`
4. Start OpenRoads Designer and open Generative Components.
5. Node groups should appear under **ORD**, **GC Excel**, **{CoDe} GC IFC**, **{CoDe} GC Add-in**, and **GC Lists**.

---

## Quick Comparison

| Without this add-on | With GC_OpenRoads |
|---|---|
| Manual station-by-station extraction | Bulk extraction at chosen intervals |
| Spreadsheet cut/fill workflows | Automated cut/fill + volume output |
| Hand-drawn annotation tables | Parametric annotation generation |
| Redrawing after corridor changes | Parameter update + automatic regeneration |
| Separate IFC scripts/export steps | Integrated parametric IFC export |
| Disconnected Excel tools | Direct Excel read/write nodes |
| External JSON scripting | Inline JSON query nodes |
| Project-specific Item Type scripts | Reusable generic Item Type nodes |

---

## License & Attribution

This is a personal repository developed and maintained by Chris Andrew.

Bentley SDK components remain subject to their respective Bentley Systems licenses.
