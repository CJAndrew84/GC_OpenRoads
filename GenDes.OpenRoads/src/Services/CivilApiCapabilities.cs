using System;

namespace GenDes_OpenRoads.Services
{
    public sealed class CivilApiCapabilities
    {
        public bool HasOpenRoadsAlignment;
        public bool HasOpenRoadsProfile;

        public static CivilApiCapabilities Detect()
        {
            var c = new CivilApiCapabilities();
            c.HasOpenRoadsAlignment = Type.GetType("Bentley.CifNET.GeometryModel.SDK.Alignment, Bentley.CifNET.GeometryModel.SDK.4.0", false) != null;
            c.HasOpenRoadsProfile = Type.GetType("Bentley.CifNET.GeometryModel.SDK.Profile, Bentley.CifNET.GeometryModel.SDK.4.0", false) != null;
            return c;
        }
    }
}
