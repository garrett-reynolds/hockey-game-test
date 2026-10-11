namespace HockeyGame
{
    /// <summary>
    /// Maps a point on the Net Face plane (metres from the Net Face centre, shooter's view:
    /// +x right, +y up) to Net Face coordinates (0-100 from the bottom-left).
    /// Points beyond the Net Face give values outside 0-100 (a Miss).
    /// </summary>
    public static class NetFaceMapping
    {
        public const double WidthMetres = Core.NetFrame.FaceWidthMetres;
        public const double HeightMetres = Core.NetFrame.FaceHeightMetres;

        public static void FromOffsetFromCentre(double metresRight, double metresUp, out double x, out double y)
        {
            x = (metresRight / WidthMetres + 0.5) * 100.0;
            y = (metresUp / HeightMetres + 0.5) * 100.0;
        }

        /// <summary>
        /// Net Face coordinates (0-100 from the bottom-left; outside 0-100 = off the net) to metres right of the
        /// Net Face centre and metres up from its bottom.
        /// </summary>
        public static void ToMetres(double x, double y, out double metresRight, out double metresUp)
        {
            metresRight = (x / 100.0 - 0.5) * WidthMetres;
            metresUp = y / 100.0 * HeightMetres;
        }
    }
}
