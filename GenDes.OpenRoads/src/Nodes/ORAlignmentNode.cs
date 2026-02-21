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
using BDPnet = Bentley.DgnPlatformNET;
using Bentley.MstnPlatformNET;
using Bentley.CifNET.GeometryModel.SDK;
using GenDes_OpenRoads.Utilities;

namespace GenDes_OpenRoads.Nodes
{
    [GCNamespace("ORD")]
    [GCNodeTypePaletteCategory("OpenRoads - Geometry")]
    [GCNodeTypeIcon("Resources/Alignment.png")]
    [GCSummary("References an OpenRoads horizontal alignment and exposes its station range.")]
    public class ORAlignmentNode : GeometricNode
    {
        private static readonly BDPnet.DgnFile  _dgnFile  = Session.Instance.GetActiveDgnFile();
        private static readonly BDPnet.DgnModel _dgnModel = Session.Instance.GetActiveDgnModel();

        // ── Cached SDK object across replication ──────────────────────────────
        private static Alignment _cachedAlignment;

        // ─────────────────────────────────────────────────────────────────────
        //  DEFAULT TECHNIQUE
        // ─────────────────────────────────────────────────────────────────────

        [GCDefaultTechnique]
        [GCSummary("Resolves the named alignment and outputs its station range.")]
        [GCParameter("AlignmentName", "Name of the horizontal alignment as shown in OpenRoads.")]
        [GCParameter("StartStation",  "Start chainage of the alignment (metres).")]
        [GCParameter("EndStation",    "End chainage of the alignment (metres).")]
        [GCParameter("Length",        "Total length of the alignment (metres).")]
        [GCParameter("AvailableAlignments", "All alignment names in the active model (for reference).")]
        public NodeUpdateResult ByName
        (
            NodeUpdateContext updateContext,
            [GCIn]  string     AlignmentName,
            [GCOut, GCInitiallyPinned] ref double   StartStation,
            [GCOut, GCInitiallyPinned] ref double   EndStation,
            [GCOut, GCInitiallyPinned] ref double   Length,
            [GCOut, GCInitiallyPinned] ref string[] AvailableAlignments
        )
        {
            try
            {
                // Always refresh the available list
                var allAlignments = OpenRoadsHelper.GetAllAlignments();
                AvailableAlignments = allAlignments.Select(a => a.Name ?? "").ToArray();

                if (string.IsNullOrWhiteSpace(AlignmentName))
                    return new NodeUpdateResult.IncompleteInputs(AlignmentName);

                var al = OpenRoadsHelper.FindAlignment(AlignmentName);
                if (al == null)
                    return new NodeUpdateResult.TechniqueInvalidArguments(AlignmentName);

                _cachedAlignment = al;
                (StartStation, EndStation) = OpenRoadsHelper.GetAlignmentStationRange(al);
                Length = EndStation - StartStation;
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ADDITIONAL TECHNIQUE — sub-range override
        // ─────────────────────────────────────────────────────────────────────

        [GCTechnique]
        [GCSummary("Resolves the alignment and clamps the station range to user-supplied limits.")]
        [GCParameter("AlignmentName",      "Name of the horizontal alignment.")]
        [GCParameter("CustomStartStation", "Override start chainage (metres).")]
        [GCParameter("CustomEndStation",   "Override end chainage (metres).")]
        [GCParameter("StartStation",       "Clamped start chainage output (metres).")]
        [GCParameter("EndStation",         "Clamped end chainage output (metres).")]
        [GCParameter("Length",             "Length of the clamped range (metres).")]
        public NodeUpdateResult ByNameWithStationOverride
        (
            NodeUpdateContext updateContext,
            [GCIn]  string AlignmentName,
            [GCIn]  double CustomStartStation,
            [GCIn]  double CustomEndStation,
            [GCOut, GCInitiallyPinned] ref double StartStation,
            [GCOut, GCInitiallyPinned] ref double EndStation,
            [GCOut, GCInitiallyPinned] ref double Length
        )
        {
            try
            {
                if (string.IsNullOrWhiteSpace(AlignmentName))
                    return new NodeUpdateResult.IncompleteInputs(AlignmentName);

                var al = OpenRoadsHelper.FindAlignment(AlignmentName);
                if (al == null)
                    return new NodeUpdateResult.TechniqueInvalidArguments(AlignmentName);

                if (CustomStartStation >= CustomEndStation)
                    return new NodeUpdateResult.TechniqueInvalidArguments(nameof(CustomStartStation));

                _cachedAlignment = al;
                (double alStart, double alEnd) = OpenRoadsHelper.GetAlignmentStationRange(al);
                StartStation = Math.Max(CustomStartStation, alStart);
                EndStation   = Math.Min(CustomEndStation,   alEnd);
                Length       = EndStation - StartStation;
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }
    }
}
