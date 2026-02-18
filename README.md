# GC_OpenRoads

**A Generative Components addon for Bentley OpenRoads Designer 2025 that automates cross-section extraction, annotation, and sheet production — parametrically.**

> Developed by Chris Andrew

---

## The Problem

Producing cross-section drawings in OpenRoads Designer is repetitive, manual, and error-prone. Engineers extract sections station by station, manually compute cut/fill areas, lay out annotation tables by hand, and rebuild everything from scratch whenever the corridor changes.

GC_OpenRoads eliminates that cycle entirely.

---

## What It Does

GC_OpenRoads is a node-based addon that slots into the Generative Components environment inside OpenRoads Designer 2025. It provides a **live, parametric pipeline** from corridor alignment through to fully annotated, sheet-ready cross-section drawings.

Change the corridor. Change the interval. Change the sheet scale. Everything updates automatically.

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
```

Each node is independently reusable. Wire only what you need.

---

## Node Reference

| Node | Category | Description |
|---|---|---|
| `ORAlignmentNode` | OpenRoads - Geometry | Reference and query a horizontal alignment |
| `ORCorridorNode` | OpenRoads - Geometry | Resolve a corridor by name or alignment |
| `ORCrossSectionDataNode` | OpenRoads - Cross Sections | Extract section data and compute cut/fill |
| `ORCrossSectionGeometryNode` | OpenRoads - Cross Sections | Convert section data to 2D sheet geometry |
| `ORAnnotationBandNode` | OpenRoads - Cross Sections | Generate annotation tables for each section |
| `ORCrossSectionSheetLayoutNode` | OpenRoads - Drawing Production | Arrange sections across sheets parametrically |

---

## Requirements

| Requirement | Version |
|---|---|
| Bentley OpenRoads Designer | 2025.00 |
| Generative Components | Included with OpenRoads 2025 |
| .NET Framework | 4.8.1 |

---

## Installation

1. Build the solution (`GC_OpenRoads.slnx`) in Visual Studio.
2. Copy the output `GC.OpenRoads.dll` to:
   ```
   C:\Program Files\Bentley\OpenRoads Designer 2025.00\OpenRoadsDesigner\GenerativeComponents\MdlApps\
   ```
3. Launch OpenRoads Designer and open the Generative Components palette.
4. The **ORD** node category will appear with all six nodes ready to use.

---

## Why GC_OpenRoads?

| Without GC_OpenRoads | With GC_OpenRoads |
|---|---|
| Manual section extraction per station | Bulk extraction at any interval in one step |
| Spreadsheet cut/fill calculations | Computed automatically with volume reporting |
| Hand-placed annotation tables | Generated and placed parametrically |
| Rebuild drawings after every design change | Update one parameter, everything regenerates |
| Hours of repetitive work per corridor | Minutes to configure, seconds to update |

---

## License & Attribution

Copyright © 2026 AtkinsRealis. All rights reserved.

This addon is developed and maintained by AtkinsRealis for use with Bentley OpenRoads Designer. All Bentley SDK components are subject to their respective Bentley Systems licences.
