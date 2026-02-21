# GC_OpenRoads

**A Generative Components addon for Bentley OpenRoads Designer 2025 that automates cross-section extraction, annotation, sheet production, IFC export, Excel integration, JSON data access, and element data management — parametrically.**

> Developed by Chris Andrew

---

## The Problem

Producing cross-section drawings in OpenRoads Designer is repetitive, manual, and error-prone. Engineers extract sections station by station, manually compute cut/fill areas, lay out annotation tables by hand, and rebuild everything from scratch whenever the corridor changes.

GC_OpenRoads eliminates that cycle entirely.

---

## What It Does

GC_OpenRoads is a node-based addon that slots into the Generative Components environment inside OpenRoads Designer 2025. It provides a **live, parametric pipeline** from corridor alignment through to fully annotated, sheet-ready cross-section drawings — and extends further into IFC export, Excel read/write, JSON data querying, list manipulation, and element item type management.

Change the corridor. Change the interval. Change the sheet scale. Everything updates automatically.

---

## Modules

The solution is composed of six modules, each compiled as a separate assembly:

| Module | Assembly | Description |
|---|---|---|
| `GC.OpenRoads` | `GC.OpenRoads.dll` | Core cross-section pipeline nodes |
| `GC.Excel` | `GC.Excel.dll` | Read and write Excel workbooks |
| `GC.IFC` | `GC.IFC.dll` | Export corridor geometry as IFC |
| `GC.JSON` | `GC.JSON.dll` | Read and query JSON files |
| `GC.Lists` | `GC.Lists.dll` | List manipulation utility nodes |
| `GCItemTypes` | `GC.ItemTypes.dll` | Read, write, and attach element Item Types |

---

## Feature Highlights

### Alignment & Corridor Resolution
- Reference any OpenRoads horizontal alignment by name
- Auto-detect the associated corridor or specify it explicitly
- Clamp to custom start/end stations without touching the model
- Enumerate all available alignments and corridors via dropdown

### Cross-Section Data Extraction
- Extract sections at **uniform station intervals** or at **individual stations**
- Compute **cut and fill areas** via trapezoidal integration
- Compute **volumes** using the average-end-area method
- Export all section data to **CSV** for reporting or downstream workflows
- Integrates with terrain models for existing ground sampling

### 2D Geometry Generation
- Convert 3D corridor data to **2D sheet-space geometry** in one step
- Set independent horizontal and vertical scales
- Apply **vertical exaggeration** (e.g. 2×) to improve visual clarity in flat terrain
- Outputs design and existing ground profiles, tick marks, datum lines, and drop lines
- Generates column centre positions for precise annotation alignment

### Annotation Bands
- Automatically generates tabular annotation boxes beneath each section
- **Built-in rows**: Point Label, Offset, Proposed Level, Existing Level
- **Optional rows**: Delta (Δ), Cut/Fill depth
- **Custom rows**: inject any user-defined data via key-value pairs
- Configurable row heights, text sizes, and numeric formats (e.g. `F3`, `P1`)
- Write directly to the DGN or preview without placing elements

### Sheet Layout
- Parametrically arrange multiple sections across drawing sheets
- Multi-column, multi-row layouts with configurable margins and gaps
- Accounts for annotation band height automatically
- Sections reflow instantly when parameters change

### Excel Integration
- **Read** any `.xlsx`, `.xlsm`, `.xlsb`, or `.xls` file into a 2D GC array
- Specify sheet name and cell range (e.g. `A1:F100`)
- **Write** a 2D array back to a named worksheet
- File browser dialogs for both input and output paths

### IFC Export
- Export corridor strings and surfaces directly to **IFC** (IFC2x3, IFC4, IFC4x3)
- Corridor strings map to `IfcAnnotation` elements with layer and colour preserved
- Corridor surfaces map to `IfcBuiltElement` polygon face sets
- Contour strings are tagged as `ContourLine` with their elevation value
- Supports nested reference file traversal up to a configurable depth
- Attaches all OpenRoads property sets as IFC property sets
- Save to any path and extension (`.ifc`, `.ifczip`, `.ifcxml`)

### JSON Data Access
- **Read** any JSON file and inspect its top-level structure (property names, values, item count)
- **ReadTable**: extract a JSON array of objects as a typed table with headers and rows; supports an optional JSONPath to navigate nested arrays and an optional schema file
- **ExtractColumn**: pull a single named property from every object in an array as both string and numeric arrays
- **QueryPath**: evaluate any JSONPath expression (wildcards, filters, recursive descent) against a file
- Schema files allow reusable column/path definitions without changing node inputs

### List Utilities
- **ListFilter**: filter any `string[]` using a parallel `bool[]` mask
- **SplitList**: split any list at a given index into two output lists
- **GroupBy**: partition a 2D data table by the unique values in a chosen column; outputs both the full list of group keys and the matching rows for a specified group value

### Element Item Types
- **GetItemTypeInfo**: inspect an Item Type from the active DGN file and list its property names; dropdown populated from the live file
- **ReadItems**: read custom Item Type property values from replicated input elements
- **WriteItems**: attach an Item Type to elements and write property values in bulk
- **WriteItemsMultipleValues**: write per-element values from a 2D value array using replication index
- **WriteCounterItems**: write auto-formatted counter strings (`prefix + index + suffix`) to a single property
- **AttachItems**: attach an Item Type to elements without writing values
- **AttachMultipleItems**: attach multiple Item Types to elements in one operation

---

## Node Pipeline

```
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


GCExcel ──► ReadExcel / WriteExcel

GCIFC ──► IfcDatabase ──► SetSite ──► SetProject ──► IFCContent ──► SaveIFC

JSON ──► ReadFile / ReadTable / ExtractColumn / QueryPath

ListFilter / SplitList / GroupBy

GCItemTypes ──► GetItemTypeInfo / ReadItems / WriteItems / AttachItems
```

Each node is independently reusable. Wire only what you need.

---

## Node Reference

### GC.OpenRoads — OpenRoads Cross-Section Pipeline

| Node | Category | Description |
|---|---|---|
| `ORAlignmentNode` | OpenRoads - Geometry | Reference and query a horizontal alignment |
| `ORCorridorNode` | OpenRoads - Geometry | Resolve a corridor by name or alignment |
| `ORCrossSectionDataNode` | OpenRoads - Cross Sections | Extract section data and compute cut/fill |
| `ORCrossSectionGeometryNode` | OpenRoads - Cross Sections | Convert section data to 2D sheet geometry |
| `ORAnnotationBandNode` | OpenRoads - Cross Sections | Generate annotation tables for each section |
| `ORCrossSectionSheetLayoutNode` | OpenRoads - Drawing Production | Arrange sections across sheets parametrically |

### GC.Excel — Excel Read/Write

| Node | Category | Technique | Description |
|---|---|---|---|
| `GCExcel` | GC Excel | `ReadExcel` (default) | Read a cell range from a named worksheet into a 2D array |
| `GCExcel` | GC Excel | `WriteExcel` | Write a 2D array to a named worksheet |

### GC.IFC — IFC Export

| Node | Category | Technique | Description |
|---|---|---|---|
| `GCIFC` | {CoDe} GC IFC | `IfcDatabase` (default) | Create an IFC database at a chosen release version |
| `GCIFC` | {CoDe} GC IFC | `SetSite` | Define the `IfcSite` within the database |
| `GCIFC` | {CoDe} GC IFC | `SetProject` | Define the `IfcProject` containing the site |
| `GCIFC` | {CoDe} GC IFC | `IFCContent` | Populate the IFC with corridor strings and surfaces |
| `GCIFC` | {CoDe} GC IFC | `SaveIFC` | Write the IFC database to disk |

### GC.JSON — JSON Data Access

| Node | Category | Technique | Description |
|---|---|---|---|
| `JSON` | {CoDe} GC Add-in | `ReadFile` (default) | Read a JSON file; output property names, values, and item count |
| `JSON` | {CoDe} GC Add-in | `ReadTable` | Extract a JSON array of objects as a table (headers + rows) |
| `JSON` | {CoDe} GC Add-in | `ExtractColumn` | Pull a single property from every item in a JSON array |
| `JSON` | {CoDe} GC Add-in | `QueryPath` | Evaluate a JSONPath expression and return matching values |

### GC.Lists — List Utilities

| Node | Category | Description |
|---|---|---|
| `ListFilter` | GC Lists | Filter a `string[]` by a parallel `bool[]` mask |
| `SplitList` | {CoDe} GC Lists | Split a list at an index into two output lists |
| `GroupBy` | GC Lists | Partition 2D table rows by a column value; output group keys and filtered rows |

### GCItemTypes — Element Item Types

| Node | Category | Technique | Description |
|---|---|---|---|
| `GCItemTypes` | {CoDe} GC Add-in | `GetItemTypeInfo` (default) | Inspect an Item Type and list its property names |
| `GCItemTypes` | {CoDe} GC Add-in | `ReadItems` | Read Item Type property values from elements |
| `GCItemTypes` | {CoDe} GC Add-in | `WriteItems` | Attach an Item Type and write property values to elements |
| `GCItemTypes` | {CoDe} GC Add-in | `WriteItemsMultipleValues` | Write per-element values from a 2D array via replication |
| `GCItemTypes` | {CoDe} GC Add-in | `WriteCounterItems` | Write auto-incrementing counter strings to a property |
| `GCItemTypes` | {CoDe} GC Add-in | `AttachItems` | Attach an Item Type to elements without writing values |
| `GCItemTypes` | {CoDe} GC Add-in | `AttachMultipleItems` | Attach multiple Item Types to elements in one step |

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

1. Build the solution (`GC_OpenRoads.slnx`) in Visual Studio.
2. Copy the output DLLs to:
   ```
   C:\Program Files\Bentley\OpenRoads Designer 2025.00\OpenRoadsDesigner\GenerativeComponents\MdlApps\
   ```
   The assemblies to deploy are:
   - `GC.OpenRoads.dll`
   - `GC.Excel.dll`
   - `GC.IFC.dll`
   - `GC.JSON.dll`
   - `GC.Lists.dll`
   - `GC.ItemTypes.dll`
3. Launch OpenRoads Designer and open the Generative Components palette.
4. The node categories will appear: **ORD**, **GC Excel**, **{CoDe} GC IFC**, **{CoDe} GC Add-in**, **GC Lists**.

---

## Why GC_OpenRoads?

| Without GC_OpenRoads | With GC_OpenRoads |
|---|---|
| Manual section extraction per station | Bulk extraction at any interval in one step |
| Spreadsheet cut/fill calculations | Computed automatically with volume reporting |
| Hand-placed annotation tables | Generated and placed parametrically |
| Rebuild drawings after every design change | Update one parameter, everything regenerates |
| Manual IFC export with fixed schema | Parametric IFC from live corridor data |
| Separate Excel macro workflows | Read/write Excel directly from GC nodes |
| JSON parsing in external scripts | Query any JSON structure inline in the graph |
| Custom Item Type scripts per project | Generic nodes reusable across any Item Type schema |
| Hours of repetitive work per corridor | Minutes to configure, seconds to update |

---

## License & Attribution

Copyright © 2026 AtkinsRealis. All rights reserved.

This addon is developed and maintained by AtkinsRealis for use with Bentley OpenRoads Designer. All Bentley SDK components are subject to their respective Bentley Systems licences.
