using System;
using System.Collections.Generic;
using System.Linq;
using Bentley.GenerativeComponents;
using Bentley.GenerativeComponents.AddInSupport;
using Bentley.GenerativeComponents.GCScript;
using Bentley.GenerativeComponents.ElementBasedNodes;
using Bentley.GenerativeComponents.GCScript.GCTypes;
using Bentley.GenerativeComponents.GeneralPurpose.Collections;
using Bentley.GenerativeComponents.View;
using Bentley.GeometryNET;
using BDPnet = Bentley.DgnPlatformNET;
using Bentley.MstnPlatformNET;
using GenDes_OpenRoads_CrossSections.Models;

namespace GenDes_OpenRoads.Nodes
{
    [GCNamespace("ORD")]
    [GCNodeTypePaletteCategory("OpenRoads - Drawing Production")]
    [GCNodeTypeIcon("Resources/SheetLayout.png")]
    [GCSummary("Parametrically arranges cross sections onto sheets. " +
               "Reserves AnnotationBandHeight below each profile cell. " +
               "Changing StationInterval or sheet parameters reflows all sections automatically.")]
    public class ORCrossSectionSheetLayoutNode : GeometricNode
    {
        private static readonly BDPnet.DgnFile  _dgnFile  = Session.Instance.GetActiveDgnFile();
        private static readonly BDPnet.DgnModel _dgnModel = Session.Instance.GetActiveDgnModel();

        [GCDefaultTechnique]
        [GCSummary("Computes sheet positions for all sections, reserving space for annotation bands.")]
        [GCParameter("AllSections",              "Section data list from ORCrossSectionData node.")]
        [GCParameter("SheetWidth",               "Sheet width in master units, e.g. 594 for A1.")]
        [GCParameter("SheetHeight",              "Sheet height in master units, e.g. 420 for A1.")]
        [GCParameter("ColumnsPerSheet",          "Number of section columns per sheet.")]
        [GCParameter("RowsPerColumn",            "Number of sections per column.")]
        [GCParameter("MarginLeft",               "Left margin (master units).")]
        [GCParameter("MarginRight",              "Right margin (master units).")]
        [GCParameter("MarginTop",                "Top margin (master units).")]
        [GCParameter("MarginBottom",             "Bottom margin (master units).")]
        [GCParameter("GapHorizontal",            "Horizontal gap between section cells (master units).")]
        [GCParameter("GapVertical",              "Vertical gap between section cells (master units).")]
        [GCParameter("HorizontalScale",          "Horizontal scale denominator, e.g. 200 for 1:200.")]
        [GCParameter("VerticalExaggerationFactor","VE factor — must match ORCrossSectionGeometry nodes.")]
        [GCParameter("AnnotationBandHeight",     "Height reserved below each profile for the annotation band.")]
        [GCParameter("TotalSheets",              "Number of sheets required.")]
        [GCParameter("CellWidth",               "Width of each section cell (master units).")]
        [GCParameter("CellProfileHeight",       "Profile-only height of each cell (above the band).")]
        [GCParameter("CellTotalHeight",         "Total cell height including annotation band.")]
        [GCParameter("SectionOriginX",          "Sheet X for each section's profile datum.")]
        [GCParameter("SectionOriginY",          "Sheet Y for each section's profile datum (bottom of profile, top of band).")]
        [GCParameter("AnnotationBandTopY",      "Y of annotation band top for each section.")]
        [GCParameter("SectionSheetIndex",       "0-based sheet index for each section.")]
        [GCParameter("StationLabels",           "Station label strings in sheet order.")]
        [GCParameter("ScaleLabel",              "Human-readable scale description.")]
        public NodeUpdateResult ComputeLayout
        (
            NodeUpdateContext updateContext,
            [GCIn]  CrossSectionData[] AllSections,
            [GCIn]  double             SheetWidth,
            [GCIn]  double             SheetHeight,
            [GCIn]  int                ColumnsPerSheet,
            [GCIn]  int                RowsPerColumn,
            [GCIn]  double             MarginLeft,
            [GCIn]  double             MarginRight,
            [GCIn]  double             MarginTop,
            [GCIn]  double             MarginBottom,
            [GCIn]  double             GapHorizontal,
            [GCIn]  double             GapVertical,
            [GCIn]  double             HorizontalScale,
            [GCIn]  double             VerticalExaggerationFactor,
            [GCIn]  double             AnnotationBandHeight,
            [GCOut, GCInitiallyPinned] ref int      TotalSheets,
            [GCOut, GCInitiallyPinned] ref double   CellWidth,
            [GCOut, GCInitiallyPinned] ref double   CellProfileHeight,
            [GCOut, GCInitiallyPinned] ref double   CellTotalHeight,
            [GCOut, GCInitiallyPinned] ref double[] SectionOriginX,
            [GCOut, GCInitiallyPinned] ref double[] SectionOriginY,
            [GCOut, GCInitiallyPinned] ref double[] AnnotationBandTopY,
            [GCOut, GCInitiallyPinned] ref int[]    SectionSheetIndex,
            [GCOut, GCInitiallyPinned] ref string[] StationLabels,
            [GCOut, GCInitiallyPinned] ref string   ScaleLabel
        )
        {
            try
            {
                if (AllSections == null || AllSections.Length == 0)
                    return new NodeUpdateResult.IncompleteInputs(nameof(AllSections));

                int cols = Math.Max(ColumnsPerSheet, 1);
                int rows = Math.Max(RowsPerColumn,   1);
                int perSheet = cols * rows;

                TotalSheets = (int)Math.Ceiling((double)AllSections.Length / perSheet);

                double drawW = SheetWidth  - MarginLeft - MarginRight;
                double drawH = SheetHeight - MarginTop  - MarginBottom;

                CellWidth        = (drawW - GapHorizontal * (cols - 1)) / cols;
                CellTotalHeight  = (drawH - GapVertical   * (rows - 1)) / rows;
                CellProfileHeight = CellTotalHeight - AnnotationBandHeight;

                double vef = VerticalExaggerationFactor <= 0 ? 1 : VerticalExaggerationFactor;
                double vd  = (HorizontalScale <= 0 ? 200 : HorizontalScale) / vef;
                ScaleLabel = Math.Abs(vef - 1.0) < 1e-6
                    ? $"1:{HorizontalScale:G} (true scale)"
                    : $"H 1:{HorizontalScale:G}  /  V 1:{vd:G}  (×{vef:G} V.E.)";

                var oxList    = new List<double>(AllSections.Length);
                var oyList    = new List<double>(AllSections.Length);
                var bandTopYL = new List<double>(AllSections.Length);
                var sheetIdxL = new List<int>(AllSections.Length);
                var labelList = new List<string>(AllSections.Length);

                // Sheets stacked horizontally (offset each sheet by SheetWidth + 50)
                for (int i = 0; i < AllSections.Length; i++)
                {
                    int sheetIdx   = i / perSheet;
                    int posOnSheet = i % perSheet;
                    int col        = posOnSheet % cols;
                    int row        = posOnSheet / cols;

                    double sheetBaseX = sheetIdx * (SheetWidth + 50.0);
                    double ox = sheetBaseX + MarginLeft  + col * (CellWidth       + GapHorizontal);
                    double oy = MarginBottom              + (rows - 1 - row) * (CellTotalHeight + GapVertical)
                                + AnnotationBandHeight;   // datum sits above the band

                    oxList.Add(ox);
                    oyList.Add(oy);
                    bandTopYL.Add(oy);            // geometry node subtracts ProfileBottomClearance itself
                    sheetIdxL.Add(sheetIdx);
                    labelList.Add(AllSections[i].StationLabel);
                }

                SectionOriginX    = oxList.ToArray();
                SectionOriginY    = oyList.ToArray();
                AnnotationBandTopY = bandTopYL.ToArray();
                SectionSheetIndex = sheetIdxL.ToArray();
                StationLabels     = labelList.ToArray();
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }
    }
}
