using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GenDes_OpenRoads.Models;

namespace GenDes_OpenRoads.Services
{
    public sealed class ProfileService : IProfileService
    {
        private readonly CivilApiCapabilities _capabilities;

        public ProfileService(CivilApiCapabilities capabilities)
        {
            _capabilities = capabilities;
        }

        public CivilObjectResult CreateOrUpdateFromPIs(ProfileRequestFromPIs request)
        {
            ValidateVerticalPis(request.VerticalPIs, "Profile.FromPIs");
            var key = ResolveStableKey(request.ExistingProfileId, request.NodeStableId, request.Name, request.AlignmentId, request.VerticalPIs.Select(v => v.Station + ":" + v.Elevation));

            var payload = new
            {
                request.AlignmentId,
                request.VerticalPIs,
                request.VerticalCurves,
                request.StartStation,
                request.EndStation
            };

            var obj = ProxyCivilStore.Upsert(key, "Profile", request.Name, payload, request.Persist);
            var grades = ComputeGrades(request.VerticalPIs);
            return new CivilObjectResult
            {
                Id = obj.Id,
                Element = obj.Element,
                UsedCivilApi = _capabilities.HasOpenRoadsProfile,
                Report = string.Format("Profile '{0}' ({1}) updated. GradeMin={2:0.####}, GradeMax={3:0.####}, CurveCount={4}.", request.Name, obj.Id, grades.Item1, grades.Item2, request.VerticalCurves == null ? 0 : request.VerticalCurves.Count)
            };
        }

        public CivilObjectResult CreateOrUpdateFromSegments(ProfileRequestFromSegments request)
        {
            if (request.Segments == null || request.Segments.Count == 0)
                throw new InvalidOperationException("Profile.FromElementsOrSegments: At least one segment is required.");

            EnsureContinuity(request.Segments);
            var key = ResolveStableKey(request.ExistingProfileId, request.NodeStableId, request.Name, request.AlignmentId, request.Segments.Select(s => s.SegmentType + ":" + s.StartSta + ":" + s.EndSta));
            var obj = ProxyCivilStore.Upsert(key, "Profile", request.Name, request.Segments, request.Persist);

            return new CivilObjectResult
            {
                Id = obj.Id,
                Element = obj.Element,
                UsedCivilApi = _capabilities.HasOpenRoadsProfile,
                Report = string.Format("Profile '{0}' ({1}) built from segments. SegmentCount={2}.", request.Name, obj.Id, request.Segments.Count)
            };
        }

        private static void ValidateVerticalPis(List<VerticalPi> verticalPIs, string nodeName)
        {
            if (verticalPIs == null || verticalPIs.Count < 2)
                throw new InvalidOperationException(nodeName + ": Need at least 2 vertical PI points.");

            for (int i = 1; i < verticalPIs.Count; i++)
            {
                if (verticalPIs[i].Station <= verticalPIs[i - 1].Station)
                    throw new InvalidOperationException(nodeName + ": Stations must be strictly increasing.");
            }
        }

        private static void EnsureContinuity(List<ProfileSegment> segments)
        {
            for (int i = 1; i < segments.Count; i++)
            {
                var prev = segments[i - 1];
                var cur = segments[i];
                if (Math.Abs(prev.EndSta - cur.StartSta) > 1e-4 || Math.Abs(prev.EndElev - cur.StartElev) > 1e-4)
                    throw new InvalidOperationException("Profile.FromElementsOrSegments: Segment continuity check failed at index " + i + ".");
            }
        }

        private static Tuple<double, double> ComputeGrades(List<VerticalPi> verticalPIs)
        {
            double min = double.MaxValue;
            double max = double.MinValue;
            for (int i = 1; i < verticalPIs.Count; i++)
            {
                double ds = verticalPIs[i].Station - verticalPIs[i - 1].Station;
                double g = ds == 0 ? 0 : (verticalPIs[i].Elevation - verticalPIs[i - 1].Elevation) / ds;
                min = Math.Min(min, g);
                max = Math.Max(max, g);
            }

            return Tuple.Create(min, max);
        }

        private static string ResolveStableKey(string explicitId, string nodeStableId, string name, string alignmentId, IEnumerable<string> tokens)
        {
            if (!string.IsNullOrWhiteSpace(explicitId))
                return explicitId;

            var sb = new StringBuilder();
            sb.Append(nodeStableId).Append("|").Append(name).Append("|").Append(alignmentId);
            foreach (var token in tokens)
                sb.Append("|").Append(token);

            using (var sha = SHA1.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return "PROF_" + BitConverter.ToString(hash).Replace("-", string.Empty).Substring(0, 16);
            }
        }
    }
}
