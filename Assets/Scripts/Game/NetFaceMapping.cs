namespace HockeyGame
{
    /// <summary>
    /// Maps a point on the Net Face plane (metres from the Net Face centre, shooter's view:
    /// +x right, +y up) to Net Face coordinates (0-100 from the bottom-left).
    /// Points beyond the Net Face give values outside 0-100 (a Miss).
    /// </summary>
    public static class NetFaceMapping
    {
        public const double WidthMetres = 1.83;
        public const double HeightMetres = 1.22;

        public static void FromOffsetFromCentre(double metresRight, double metresUp, out double x, out double y)
        {
            x = (metresRight / WidthMetres + 0.5) * 100.0;
            y = (metresUp / HeightMetres + 0.5) * 100.0;
        }
    }
}
