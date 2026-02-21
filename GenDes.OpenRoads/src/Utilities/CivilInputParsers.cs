using System;
using System.Collections.Generic;
using Bentley.GeometryNET;
using GenDes_OpenRoads.Models;

namespace GenDes_OpenRoads.Utilities
{
    internal static class CivilInputParsers
    {
        public static List<DPoint3d> ParsePoints(object[] raw, List<string> warnings)
        {
            var points = new List<DPoint3d>();
            if (raw == null)
                return points;

            for (int i = 0; i < raw.Length; i++)
            {
                object item = raw[i];
                if (item is DPoint3d)
                {
                    points.Add((DPoint3d)item);
                    continue;
                }

                if (item is DPoint2d)
                {
                    var p2 = (DPoint2d)item;
                    points.Add(DPoint3d.FromXYZ(p2.X, p2.Y, 0.0));
                    continue;
                }

                if (item is double[])
                {
                    var arr = (double[])item;
                    if (arr.Length >= 2)
                    {
                        points.Add(DPoint3d.FromXYZ(arr[0], arr[1], arr.Length >= 3 ? arr[2] : 0.0));
                        continue;
                    }
                }

                warnings.Add("Unrecognized point input at index " + i + ". Expected DPoint2d/DPoint3d/double[].");
            }

            return points;
        }

        public static List<VerticalPi> ParseVerticalPIs(object[] raw, List<string> warnings)
        {
            var result = new List<VerticalPi>();
            if (raw == null) return result;

            for (int i = 0; i < raw.Length; i++)
            {
                object item = raw[i];
                if (item is VerticalPi)
                {
                    result.Add((VerticalPi)item);
                    continue;
                }

                if (item is DPoint2d)
                {
                    var p2 = (DPoint2d)item;
                    result.Add(new VerticalPi { Station = p2.X, Elevation = p2.Y });
                    continue;
                }

                if (item is double[])
                {
                    var arr = (double[])item;
                    if (arr.Length >= 2)
                    {
                        result.Add(new VerticalPi { Station = arr[0], Elevation = arr[1] });
                        continue;
                    }
                }

                warnings.Add("Unrecognized vertical PI at index " + i + ". Expected DPoint2d, VerticalPi, or double[2].");
            }

            return result;
        }

        public static List<double?> ParseOptionalDoubleList(double[] values)
        {
            var result = new List<double?>();
            if (values == null)
                return result;

            for (int i = 0; i < values.Length; i++)
            {
                result.Add(double.IsNaN(values[i]) ? (double?)null : values[i]);
            }

            return result;
        }
    }
}
