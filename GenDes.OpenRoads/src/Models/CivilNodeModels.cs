using System;
using System.Collections.Generic;
using Bentley.GeometryNET;

namespace GenDes_OpenRoads.Models
{
    public sealed class AlignmentRequestFromPIs
    {
        public string Name;
        public List<DPoint3d> PIs;
        public List<double?> CurveRadii;
        public bool UseSpirals;
        public List<double?> SpiralLengthIn;
        public List<double?> SpiralLengthOut;
        public double StationStart;
        public string FeatureDefinition;
        public bool Persist;
        public string ExistingAlignmentId;
        public string NodeStableId;
    }

    public sealed class AlignmentRequestFromElements
    {
        public string Name;
        public List<DPoint3d> ChainPoints;
        public string SortMode;
        public double StationStart;
        public bool Persist;
        public string ExistingAlignmentId;
        public string NodeStableId;
    }

    public sealed class VerticalPi
    {
        public double Station;
        public double Elevation;
    }

    public sealed class VerticalCurveSpec
    {
        public string Type;
        public double? KValue;
        public double? Length;
        public bool? IsCrest;
    }

    public sealed class ProfileSegment
    {
        public string SegmentType;
        public double StartSta;
        public double StartElev;
        public double EndSta;
        public double EndElev;
        public double PVISta;
        public double PVIElev;
        public double? K;
        public double? Length;
        public bool? Crest;
    }

    public sealed class ProfileRequestFromPIs
    {
        public string AlignmentId;
        public string Name;
        public List<VerticalPi> VerticalPIs;
        public List<VerticalCurveSpec> VerticalCurves;
        public double? StartStation;
        public double? EndStation;
        public bool Persist;
        public string ExistingProfileId;
        public string NodeStableId;
    }

    public sealed class ProfileRequestFromSegments
    {
        public string AlignmentId;
        public string Name;
        public List<ProfileSegment> Segments;
        public bool Persist;
        public string ExistingProfileId;
        public string NodeStableId;
    }

    public sealed class CivilObjectResult
    {
        public string Id;
        public object Element;
        public string Report;
        public bool UsedCivilApi;
    }
}
