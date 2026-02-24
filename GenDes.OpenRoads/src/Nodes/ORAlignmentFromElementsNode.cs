using System;
using System.Collections.Generic;
using Bentley.GenerativeComponents;
using Bentley.GenerativeComponents.AddInSupport;
using Bentley.GenerativeComponents.GCScript;
using Bentley.GenerativeComponents.ElementBasedNodes;
using Bentley.GenerativeComponents.GCScript.GCTypes;
using Bentley.GenerativeComponents.GeneralPurpose.Collections;
using Bentley.GenerativeComponents.View;
using GenDes_OpenRoads.Models;
using GenDes_OpenRoads.Services;
using GenDes_OpenRoads.Utilities;

namespace GenDes_OpenRoads.Nodes
{
    [GCNamespace("ORD")]
    [GCNodeTypePaletteCategory("OpenRoads - Geometry")]
    [GCSummary("Builds/updates an alignment from existing elements or chain geometry.")]
    public class ORAlignmentFromElementsNode : GeometricNode
    {
        private static readonly CivilApiCapabilities _capabilities = CivilApiCapabilities.Detect();
        private static readonly IAlignmentService _service = new AlignmentService(_capabilities);

        [GCDefaultTechnique]
        public NodeUpdateResult Build
        (
            NodeUpdateContext updateContext,
            [GCIn] string Name,
            [GCIn] object[] Elements,
            [GCIn] string SortMode,
            [GCIn] double StationStart,
            [GCIn] bool Persist,
            [GCIn] string ExistingAlignmentId,
            [GCOut, GCInitiallyPinned] ref string AlignmentHandle,
            [GCOut, GCInitiallyPinned] ref object AlignmentElement,
            [GCOut, GCInitiallyPinned] ref string Report
        )
        {
            try
            {
                var warnings = new List<string>();
                var chain = CivilInputParsers.ParsePoints(Elements, warnings);
                var req = new AlignmentRequestFromElements
                {
                    Name = string.IsNullOrWhiteSpace(Name) ? "GC_Alignment" : Name,
                    ChainPoints = chain,
                    SortMode = string.IsNullOrWhiteSpace(SortMode) ? "AsGiven" : SortMode,
                    StationStart = StationStart,
                    Persist = Persist,
                    ExistingAlignmentId = ExistingAlignmentId,
                    NodeStableId = this.Name
                };

                CivilObjectResult result = _service.CreateOrUpdateFromElements(req);
                AlignmentHandle = result.Id;
                AlignmentElement = result.Element;
                Report = result.Report + (warnings.Count > 0 ? " Warnings: " + string.Join(" | ", warnings.ToArray()) : string.Empty);
                return NodeUpdateResult.Success;
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }
        }
    }
}
