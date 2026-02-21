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
    [GCSummary("Builds/updates a vertical profile from station-elevation PI definitions.")]
    public class ORProfileFromPIsNode : GeometricNode
    {
        private static readonly CivilApiCapabilities _capabilities = CivilApiCapabilities.Detect();
        private static readonly IProfileService _service = new ProfileService(_capabilities);

        [GCDefaultTechnique]
        public NodeUpdateResult Build
        (
            NodeUpdateContext updateContext,
            [GCIn] string AlignmentHandle,
            [GCIn] string Name,
            [GCIn] object[] VerticalPIs,
            [GCIn] object[] VerticalCurves,
            [GCIn] double StartStation,
            [GCIn] double EndStation,
            [GCIn] bool Persist,
            [GCIn] string ExistingProfileId,
            [GCOut, GCInitiallyPinned] ref string ProfileHandle,
            [GCOut, GCInitiallyPinned] ref object ProfileElement,
            [GCOut, GCInitiallyPinned] ref string Report
        )
        {
            try
            {
                if (string.IsNullOrWhiteSpace(AlignmentHandle))
                    throw new InvalidOperationException("Profile.FromPIs: AlignmentHandle is required.");

                var warnings = new List<string>();
                var vpis = CivilInputParsers.ParseVerticalPIs(VerticalPIs, warnings);
                var curves = ParseVerticalCurves(VerticalCurves, warnings);

                var req = new ProfileRequestFromPIs
                {
                    AlignmentId = AlignmentHandle,
                    Name = string.IsNullOrWhiteSpace(Name) ? "GC_Profile" : Name,
                    VerticalPIs = vpis,
                    VerticalCurves = curves,
                    StartStation = StartStation > 0 ? (double?)StartStation : null,
                    EndStation = EndStation > 0 ? (double?)EndStation : null,
                    Persist = Persist,
                    ExistingProfileId = ExistingProfileId,
                    NodeStableId = this.Name
                };

                var result = _service.CreateOrUpdateFromPIs(req);
                ProfileHandle = result.Id;
                ProfileElement = result.Element;
                Report = result.Report + (warnings.Count > 0 ? " Warnings: " + string.Join(" | ", warnings.ToArray()) : string.Empty);
                return NodeUpdateResult.Success;
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }
        }

        private static List<VerticalCurveSpec> ParseVerticalCurves(object[] verticalCurves, List<string> warnings)
        {
            var list = new List<VerticalCurveSpec>();
            if (verticalCurves == null)
                return list;

            for (int i = 0; i < verticalCurves.Length; i++)
            {
                var vc = verticalCurves[i] as VerticalCurveSpec;
                if (vc != null)
                {
                    list.Add(vc);
                    continue;
                }

                warnings.Add("VerticalCurves index " + i + " ignored. Expected VerticalCurveSpec object.");
            }

            return list;
        }
    }
}
