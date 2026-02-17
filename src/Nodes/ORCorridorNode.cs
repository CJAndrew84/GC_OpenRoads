using System;
using System.Linq;
using Bentley.GenerativeComponents.AddInSupport;
using Bentley.GenerativeComponents.GCScript;
using Bentley.GenerativeComponents.ElementBasedNodes;
using BDPnet = Bentley.DgnPlatformNET;
using Bentley.MstnPlatformNET;
using Bentley.CifNET.GeometryModel.SDK;
using GC_OpenRoads.Utilities;

namespace GC_OpenRoads.Nodes
{
    [GCNamespace("ORD")]
    [GCNodeTypePaletteCategory("OpenRoads - Geometry")]
    [GCNodeTypeIcon("Resources/Corridor.png")]
    [GCSummary("References an OpenRoads corridor. Resolves by name or by its parent alignment.")]
    public class ORCorridorNode : GeometricNode
    {
        private static readonly BDPnet.DgnFile  _dgnFile  = Session.Instance.GetActiveDgnFile();
        private static readonly BDPnet.DgnModel _dgnModel = Session.Instance.GetActiveDgnModel();

        [GCDefaultTechnique]
        [GCSummary("Resolves the corridor by explicit name or via the parent alignment name.")]
        [GCParameter("CorridorName",          "Explicit corridor name. Leave empty to auto-resolve from AlignmentName.")]
        [GCParameter("AlignmentName",         "Used when CorridorName is empty — picks the first corridor on this alignment.")]
        [GCParameter("ResolvedCorridorName",  "Confirmed corridor name output.")]
        [GCParameter("StartStation",          "Corridor start chainage (metres).")]
        [GCParameter("EndStation",            "Corridor end chainage (metres).")]
        [GCParameter("AvailableCorridors",    "All corridor names in the active model.")]
        public NodeUpdateResult ByNameOrAlignment
        (
            NodeUpdateContext updateContext,
            [GCIn]  string   CorridorName,
            [GCIn]  string   AlignmentName,
            [GCOut, GCInitiallyPinned] ref string   ResolvedCorridorName,
            [GCOut, GCInitiallyPinned] ref double   StartStation,
            [GCOut, GCInitiallyPinned] ref double   EndStation,
            [GCOut, GCInitiallyPinned] ref string[] AvailableCorridors
        )
        {
            try
            {
                using var con = OpenRoadsHelper.GetReadConnection();
                var gm = con.GetActiveGeometricModel();
                AvailableCorridors = gm?.Corridors
                                        .Select(c => c.Name ?? "")
                                        .OrderBy(n => n)
                                        .ToArray()
                                     ?? Array.Empty<string>();

                Corridor corridor = null;

                if (!string.IsNullOrWhiteSpace(CorridorName))
                {
                    corridor = OpenRoadsHelper.FindCorridor(CorridorName);
                    if (corridor == null)
                        return new NodeUpdateResult.TechniqueInvalidArguments(nameof(CorridorName));
                }
                else if (!string.IsNullOrWhiteSpace(AlignmentName))
                {
                    var corridors = OpenRoadsHelper.GetCorridorsForAlignment(AlignmentName);
                    if (corridors.Count == 0)
                        return new NodeUpdateResult.TechniqueInvalidArguments(nameof(AlignmentName));
                    corridor = corridors[0];
                }
                else
                {
                    return new NodeUpdateResult.IncompleteInputs(CorridorName);
                }

                ResolvedCorridorName = corridor.Name ?? string.Empty;
                // CorridorAlignment.LinearGeometry.Length is in metres — convert to master units
                double lengthMaster = OpenRoadsHelper.ConvertMeterToMaster(
                    corridor.CorridorAlignment.LinearGeometry.Length);
                StartStation = 0.0;
                EndStation   = lengthMaster;
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }
    }
}
