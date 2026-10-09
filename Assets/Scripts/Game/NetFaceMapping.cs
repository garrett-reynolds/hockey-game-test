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
        /// Net Face coordinates (0-100 from the bottom-left; outside 0-100 = off the net) to metres right of the
        /// Net Face centre and metres up from its bottom. Same frame as ZoneRectangle.
        /// </summary>
        public static void ToMetres(double x, double y, out double metresRight, out double metresUp)
        {
            metresRight = (x / 100.0 - 0.5) * WidthMetres;
            metresUp = y / 100.0 * HeightMetres;
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

        /// <summary>Share of a Contested Zone left open on a side that borders a Covered Zone or the net frame.</summary>
        public const float ContestedMarginShare = 0.3f;

        /// <summary>
        /// Rectangle of the goalie block in a Contested Zone. A side that borders an Open Zone is flush with the
        /// Zone edge (so the visible gap that touches an Open Zone only holds Open Zones); every other side
        /// (Covered neighbour or the frame) is inset by ContestedMarginShare of the Zone, leaving a visible partial opening.
        /// </summary>
        public static void ContestedRectangle(int column, int row, bool openLeft, bool openRight, bool openBelow, bool openAbove,
            out float minX, out float minY, out float maxX, out float maxY)
        {
            ZoneRectangle(column, row, 1f, out float zMinX, out float zMinY, out float zMaxX, out float zMaxY);
            float marginX = (zMaxX - zMinX) * ContestedMarginShare, marginY = (zMaxY - zMinY) * ContestedMarginShare;
            minX = openLeft ? zMinX : zMinX + marginX;
            maxX = openRight ? zMaxX : zMaxX - marginX;
            minY = openBelow ? zMinY : zMinY + marginY;
            maxY = openAbove ? zMaxY : zMaxY - marginY;
        }
    }
}
