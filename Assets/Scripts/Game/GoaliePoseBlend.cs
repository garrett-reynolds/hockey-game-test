namespace HockeyGame
{
    /// <summary>
    /// The goalie moving into its Goalie Pose: each Zone's shape morphs from a neutral shape
    /// (rows "C X C": middle column covered, side columns half covered) to its place in the pose.
    /// Rectangles are Net Face metres (x right of centre, y up from the bottom), as in NetFaceMapping.
    /// </summary>
    public static class GoaliePoseBlend
    {
        /// <summary>A rectangle on the Net Face, in metres.</summary>
        public readonly struct Shape
        {
            public readonly float MinX, MinY, MaxX, MaxY;
            public Shape(float minX, float minY, float maxX, float maxY) { MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; }
        }

        /// <summary>The shape in this Zone before the goalie sets (the neutral shape).</summary>
        public static Shape Neutral(int column, int row)
        {
            float a, b, c, d;
            if (column == 1)
                NetFaceMapping.ZoneRectangle(column, row, 1f, out a, out b, out c, out d);
            else
                NetFaceMapping.ContestedRectangle(column, row, false, false, false, false, out a, out b, out c, out d);
            return new Shape(a, b, c, d);
        }

        /// <summary>Where an Open Zone's shape ends: a point at the Zone centre (nothing left).</summary>
        public static Shape OpenTarget(int column, int row)
        {
            NetFaceMapping.ZoneRectangle(column, row, 0f, out float a, out float b, out float c, out float d);
            return new Shape(a, b, c, d);
        }

        public static Shape Lerp(Shape from, Shape to, float t)
        {
            return new Shape(
                from.MinX + (to.MinX - from.MinX) * t,
                from.MinY + (to.MinY - from.MinY) * t,
                from.MaxX + (to.MaxX - from.MaxX) * t,
                from.MaxY + (to.MaxY - from.MaxY) * t);
        }
    }
}
