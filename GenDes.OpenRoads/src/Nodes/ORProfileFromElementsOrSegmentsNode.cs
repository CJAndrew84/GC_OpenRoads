using System;
using System.Collections.Generic;
using Bentley.GenerativeComponents;
using Bentley.GenerativeComponents.AddInSupport;
using Bentley.GenerativeComponents.ElementBasedNodes;
using GenDes_OpenRoads.Models;
using GenDes_OpenRoads.Services;

namespace GenDes_OpenRoads.Nodes
{
    [GCNamespace("ORD")]
    [GCNodeTypePaletteCategory("OpenRoads - Geometry")]
    [GCSummary("Builds/updates a profile from explicit profile segments.")]
    public class ORProfileFromElementsOrSegmentsNode : GeometricNode
    {
        private static readonly CivilApiCapabilities _capabilities = CivilApiCapabilities.Detect();
        private static readonly IProfileService _service = new ProfileService(_capabilities);

        [GCDefaultTechnique]
        public NodeUpdateResult Build
        (
            NodeUpdateContext updateContext,
            [GCIn] string AlignmentHandle,
            [GCIn] string Name,
            [GCIn] object[] Segments,
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
                    throw new InvalidOperationException("Profile.FromElementsOrSegments: AlignmentHandle is required.");

                var warnings = new List<string>();
                var parsed = ParseSegments(Segments, warnings);
                var req = new ProfileRequestFromSegments
                {
                    AlignmentId = AlignmentHandle,
                    Name = string.IsNullOrWhiteSpace(Name) ? "GC_Profile" : Name,
                    Segments = parsed,
                    Persist = Persist,
                    ExistingProfileId = ExistingProfileId,
                    NodeStableId = this.Name
                };

                var result = _service.CreateOrUpdateFromSegments(req);
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

        private static List<ProfileSegment> ParseSegments(object[] segments, List<string> warnings)
        {
            var list = new List<ProfileSegment>();
            if (segments == null)
                return list;

            for (int i = 0; i < segments.Length; i++)
            {
                var seg = segments[i] as ProfileSegment;
                if (seg != null)
                {
                    list.Add(seg);
                }
                else
                {
                    warnings.Add("Segments index " + i + " ignored. Expected ProfileSegment object.");
                }
            }

            return list;
        }
    }
}
