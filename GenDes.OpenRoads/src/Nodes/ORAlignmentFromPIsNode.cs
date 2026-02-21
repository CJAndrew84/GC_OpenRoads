using System;
using System.Collections.Generic;
using Bentley.GenerativeComponents;
using Bentley.GenerativeComponents.AddInSupport;
using Bentley.GenerativeComponents.ElementBasedNodes;
using GenDes_OpenRoads.Models;
using GenDes_OpenRoads.Services;
using GenDes_OpenRoads.Utilities;

namespace GenDes_OpenRoads.Nodes
{
    [GCNamespace("ORD")]
    [GCNodeTypePaletteCategory("OpenRoads - Geometry")]
    [GCSummary("Builds/updates an alignment from PI points with optional curve/spiral metadata.")]
    public class ORAlignmentFromPIsNode : GeometricNode
    {
        private static readonly CivilApiCapabilities _capabilities = CivilApiCapabilities.Detect();
        private static readonly IAlignmentService _service = new AlignmentService(_capabilities);

        [GCDefaultTechnique]
        public NodeUpdateResult Build
        (
            NodeUpdateContext updateContext,
            [GCIn] string Name,
            [GCIn] object[] PIs,
            [GCIn] double[] CurveRadius,
            [GCIn] bool UseSpirals,
            [GCIn] double[] SpiralLengthIn,
            [GCIn] double[] SpiralLengthOut,
            [GCIn] double StationStart,
            [GCIn] string FeatureDefinition,
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
                var pis = CivilInputParsers.ParsePoints(PIs, warnings);
                var req = new AlignmentRequestFromPIs
                {
                    Name = string.IsNullOrWhiteSpace(Name) ? "GC_Alignment" : Name,
                    PIs = pis,
                    CurveRadii = CivilInputParsers.ParseOptionalDoubleList(CurveRadius),
                    UseSpirals = UseSpirals,
                    SpiralLengthIn = CivilInputParsers.ParseOptionalDoubleList(SpiralLengthIn),
                    SpiralLengthOut = CivilInputParsers.ParseOptionalDoubleList(SpiralLengthOut),
                    StationStart = StationStart,
                    FeatureDefinition = FeatureDefinition,
                    Persist = Persist,
                    ExistingAlignmentId = ExistingAlignmentId,
                    NodeStableId = this.Name
                };

                CivilObjectResult result = _service.CreateOrUpdateFromPIs(req);
                AlignmentHandle = result.Id;
                AlignmentElement = result.Element;
                Report = result.Report + BuildWarningSuffix(warnings, result.UsedCivilApi);
                return NodeUpdateResult.Success;
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }
        }

        private static string BuildWarningSuffix(List<string> warnings, bool usedCivilApi)
        {
            string mode = usedCivilApi ? " Mode=OpenRoads API." : " Mode=Proxy geometry metadata.";
            if (warnings == null || warnings.Count == 0)
                return mode;
            return mode + " Warnings: " + string.Join(" | ", warnings.ToArray());
        }
    }
}
