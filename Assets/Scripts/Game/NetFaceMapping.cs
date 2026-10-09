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

        /// <summary>
        /// Rectangle (metres; x right of Net Face centre, y up from the bottom of the Net Face) of a Zone
        /// (column 0 = left, row 0 = bottom, shooter's view), shrunk about its centre by fraction
        /// (1 = the whole Zone, 0.55 = a smaller block leaving a margin open).
        /// </summary>
        public static void ZoneRectangle(int column, int row, float fraction,
            out float minX, out float minY, out float maxX, out float maxY)
        {
            float zoneW = (float)WidthMetres / 3f, zoneH = (float)HeightMetres / 3f;
            float centreX = -(float)WidthMetres / 2f + (column + 0.5f) * zoneW;
            float centreY = (row + 0.5f) * zoneH;
            minX = centreX - zoneW * fraction / 2f;
            maxX = centreX + zoneW * fraction / 2f;
            minY = centreY - zoneH * fraction / 2f;
            maxY = centreY + zoneH * fraction / 2f;
        }
    }
}
