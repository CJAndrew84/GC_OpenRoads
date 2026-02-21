# {Gen:Des}_OpenRoads

I built **{Gen:Des}_OpenRoads** to remove the repetitive parts of corridor cross-section production in **Bentley OpenRoads Designer 2025**.

This is a Generative Components add-on that gives me a parametric workflow for:
- corridor and alignment resolution,
- cross-section extraction,
- annotation + sheet layout,
- IFC export,
- Excel I/O,
- JSON querying,
- and Item Type data management.

> Developed by Chris Andrew

> Acknowledgement: Special thanks to **Edward Ashbolt** and the [**GC Community Repo**](https://github.com/edashbolt/generative-components) for the inspiration and for starting an Item Types Node that allow me to extend to what is in this repo.

---

## Why I Built It

If you've used the native OpenRoads Designer tools for Drawing Production, you know the pain:
- creating sections from named boundaries is slow,
- graphics are dynamic but annotation isnt,
- exporting for others to use (DWG) is a pain,
- and then redoing all of it after a design change.

I wanted one graph where I can change interval, limits, scale, or layout and have everything update.

---

## What This Add-On Does

{Gen:Des}_OpenRoads runs as a node set inside Generative Components in OpenRoads 2025.

The core idea is simple: define the alignment/corridor once, then drive downstream geometry, data, annotation, and sheets from that source. The same graph can also push/pull Excel data, query JSON, export IFC, and read/write Item Types.

---

## Project Modules

The solution is split into six assemblies:

| Module | Assembly | Purpose |
|---|---|---|
| `GenDes.OpenRoads` | `GenDes.OpenRoads.dll` | Core cross-section workflow |
| `GenDes.Excel` | `GenDes.Excel.dll` | Excel read/write helpers |
| `GenDes.IFC` | `GenDes.IFC.dll` | Corridor IFC export |
| `GenDes.JSON` | `GenDes.JSON.dll` | JSON file/table/path queries |
| `GenDes.Lists` | `GenDes.Lists.dll` | Utility list operations |
| `GenDesItemTypes` | `GenDes.ItemTypes.dll` | Item Type read/write/attach |

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
GenDesExcel ──► ReadExcel / WriteExcel

GenDesIFC ──► IfcDatabase ──► SetSite ──► SetProject ──► IFCContent ──► SaveIFC

JSON ──► ReadFile / ReadTable / ExtractColumn / QueryPath

ListFilter / SplitList / GroupBy

GenDesItemTypes ──► GetItemTypeInfo / ReadItems / WriteItems / AttachItems
```

Use only the parts you need in a given graph.

---

## Node Reference

### {Gen:Des} OpenRoads — Cross-Section Workflow

| Node | Category | Description |
|---|---|---|
| `ORAlignmentNode` | OpenRoads - Geometry | Reference/query horizontal alignment |
| `ORCorridorNode` | OpenRoads - Geometry | Resolve corridor by alignment or name |
| `ORCrossSectionDataNode` | OpenRoads - Cross Sections | Extract section data + cut/fill |
| `ORCrossSectionGeometryNode` | OpenRoads - Cross Sections | Build 2D section geometry |
| `ORAnnotationBandNode` | OpenRoads - Cross Sections | Build section annotation tables |
| `ORCrossSectionSheetLayoutNode` | OpenRoads - Drawing Production | Place sections across sheet layout |

### {Gen:Des} Excel

| Node | Category | Technique | Description |
|---|---|---|---|
| `GenDesExcel` | {Gen:Des} Excel | `ReadExcel` (default) | Read worksheet range into a 2D array |
| `GenDesExcel` | {Gen:Des} Excel | `WriteExcel` | Write a 2D array into a worksheet |

### {Gen:Des} IFC

| Node | Category | Technique | Description |
|---|---|---|---|
| `GenDesIFC` | {Gen:Des} IFC | `IfcDatabase` (default) | Create IFC database by release |
| `GenDesIFC` | {Gen:Des} IFC | `SetSite` | Define `IfcSite` |
| `GenDesIFC` | {Gen:Des} IFC | `SetProject` | Define `IfcProject` |
| `GenDesIFC` | {Gen:Des} IFC | `IFCContent` | Populate corridor geometry/content |
| `GenDesIFC` | {Gen:Des} IFC | `SaveIFC` | Save IFC output file |

### {Gen:Des} JSON

| Node | Category | Technique | Description |
|---|---|---|---|
| `JSON` | {Gen:Des} Add-in | `ReadFile` (default) | Read JSON top-level names/values/item count |
| `JSON` | {Gen:Des} Add-in | `ReadTable` | Read array-of-objects to headers + rows |
| `JSON` | {Gen:Des} Add-in | `ExtractColumn` | Extract one property from all items |
| `JSON` | {Gen:Des} Add-in | `QueryPath` | Execute JSONPath and return matches |

### {Gen:Des} Lists

| Node | Category | Description |
|---|---|---|
| `ListFilter` | {Gen:Des} Lists | Filter `string[]` with `bool[]` mask |
| `SplitList` | {Gen:Des} Lists | Split list at index |
| `GroupBy` | {Gen:Des} Lists | Group 2D rows by selected column |

### {Gen:Des} Item Types

| Node | Category | Technique | Description |
|---|---|---|---|
| `GenDesItemTypes` | {Gen:Des} Add-in | `GetItemTypeInfo` (default) | Inspect Item Type and list properties |
| `GenDesItemTypes` | {Gen:Des} Add-in | `ReadItems` | Read Item Type values |
| `GenDesItemTypes` | {Gen:Des} Add-in | `WriteItems` | Attach type + write values |
| `GenDesItemTypes` | {Gen:Des} Add-in | `WriteItemsMultipleValues` | Write per-element values from 2D array |
| `GenDesItemTypes` | {Gen:Des} Add-in | `WriteCounterItems` | Write formatted incrementing values |
| `GenDesItemTypes` | {Gen:Des} Add-in | `AttachItems` | Attach type only |
| `GenDesItemTypes` | {Gen:Des} Add-in | `AttachMultipleItems` | Attach multiple types |

---

## Requirements

| Requirement | Version |
|---|---|
| Bentley OpenRoads Designer | 2025.00 |
| Generative Components | Included with OpenRoads 2025 |
| .NET Framework | 4.8.1 |
| FastExcel | NuGet (GenDes.Excel) |
| Newtonsoft.Json | NuGet (GenDes.JSON) |
| GeometryGym.IFC | NuGet (GenDes.IFC) |

---

## Installation

1. Build `GenDes_OpenRoads.slnx` in Visual Studio.
2. Copy built assemblies into:
   ```
   C:\Program Files\Bentley\OpenRoads Designer 2025.00\OpenRoadsDesigner\GenerativeComponents\MdlApps\
   ```
3. Deploy these DLLs:
   - `GenDes.OpenRoads.dll`
   - `GenDes.Excel.dll`
   - `GenDes.IFC.dll`
   - `GenDes.JSON.dll`
   - `GenDes.Lists.dll`
   - `GenDes.ItemTypes.dll`
4. Start OpenRoads Designer and open Generative Components.
5. Node groups should appear under **ORD**, **{Gen:Des} Excel**, **{Gen:Des} IFC**, **{Gen:Des} Add-in**, and **{Gen:Des} Lists**.

---

## Quick Comparison

| Without this add-on | With {Gen:Des}_OpenRoads |
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
