using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Bentley.GeometryNET;
using GenDes_OpenRoads.Models;

namespace GenDes_OpenRoads.Services
{
    public sealed class AlignmentService : IAlignmentService
    {
        private readonly CivilApiCapabilities _capabilities;

        public AlignmentService(CivilApiCapabilities capabilities)
        {
            _capabilities = capabilities;
        }

        public CivilObjectResult CreateOrUpdateFromPIs(AlignmentRequestFromPIs request)
        {
            ValidatePiGeometry(request.PIs, "Alignment.FromPIs");

            var key = ResolveStableKey(request.ExistingAlignmentId, request.NodeStableId, request.Name, request.PIs);
            var payload = new
            {
                request.PIs,
                request.CurveRadii,
                request.UseSpirals,
                request.SpiralLengthIn,
                request.SpiralLengthOut,
                request.StationStart,
                request.FeatureDefinition
            };

            var obj = ProxyCivilStore.Upsert(key, "Alignment", request.Name, payload, request.Persist);
            int segmentCount = Math.Max(0, request.PIs.Count - 1);
            string curveMode = request.CurveRadii != null && request.CurveRadii.Any(r => r.HasValue) ? "tangent-curve" : "tangents-only";
            string spiralNote = request.UseSpirals ? "Spirals requested; proxy mode stores metadata and reports approximation." : "Spirals disabled.";

            return new CivilObjectResult
            {
                Id = obj.Id,
                Element = obj.Element,
                UsedCivilApi = _capabilities.HasOpenRoadsAlignment,
                Report = string.Format("Alignment '{0}' ({1}) {2}. Segments={3}, StationStart={4:0.###}. {5}", request.Name, obj.Id, curveMode, segmentCount, request.StationStart, spiralNote)
            };
        }

        public CivilObjectResult CreateOrUpdateFromElements(AlignmentRequestFromElements request)
        {
            ValidatePiGeometry(request.ChainPoints, "Alignment.FromElements");
            var key = ResolveStableKey(request.ExistingAlignmentId, request.NodeStableId, request.Name, request.ChainPoints);
            var payload = new
            {
                request.ChainPoints,
                request.SortMode,
                request.StationStart
            };

            var obj = ProxyCivilStore.Upsert(key, "Alignment", request.Name, payload, request.Persist);
            return new CivilObjectResult
            {
                Id = obj.Id,
                Element = obj.Element,
                UsedCivilApi = _capabilities.HasOpenRoadsAlignment,
                Report = string.Format("Alignment '{0}' ({1}) built from element chain. SortMode={2}, Segments={3}, StationStart={4:0.###}.", request.Name, obj.Id, request.SortMode, Math.Max(0, request.ChainPoints.Count - 1), request.StationStart)
            };
        }

        private static void ValidatePiGeometry(List<DPoint3d> points, string nodeName)
        {
            if (points == null || points.Count < 2)
                throw new InvalidOperationException(nodeName + ": Need at least 2 PI points.");

            for (int i = 0; i < points.Count; i++)
            {
                if (double.IsNaN(points[i].X) || double.IsNaN(points[i].Y) || double.IsNaN(points[i].Z))
                    throw new InvalidOperationException(nodeName + ": PI points cannot contain NaN coordinates.");

                if (i > 0)
                {
                    if (points[i - 1].Distance(points[i]) < 1e-6)
                        throw new InvalidOperationException(nodeName + ": Duplicate PI points detected within tolerance.");
                }
            }
        }

        private static string ResolveStableKey(string explicitId, string nodeStableId, string name, List<DPoint3d> points)
        {
            if (!string.IsNullOrWhiteSpace(explicitId))
                return explicitId;

            var sb = new StringBuilder();
            sb.Append(nodeStableId).Append("|").Append(name);
            foreach (var p in points)
                sb.AppendFormat("|{0:0.###},{1:0.###},{2:0.###}", p.X, p.Y, p.Z);

            using (var sha = SHA1.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return "ALN_" + BitConverter.ToString(hash).Replace("-", string.Empty).Substring(0, 16);
            }
        }
    }
}
