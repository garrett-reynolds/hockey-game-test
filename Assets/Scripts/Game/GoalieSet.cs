namespace HockeyGame
{
    /// <summary>
    /// Shapes for the goalie setting into a pose: each Zone block morphs from a neutral stance
    /// (rows "C X C": middle column covered, side columns half covered) to its place in the pose.
    /// Rectangles are Net Face metres (x right of centre, y up from the bottom), as in NetFaceMapping.
    /// </summary>
    public static class GoalieSet
    {
        public readonly struct Rect
        {
            public readonly float MinX, MinY, MaxX, MaxY;
            public Rect(float minX, float minY, float maxX, float maxY) { MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; }
        }

        /// <summary>The block in this Zone for the neutral stance.</summary>
        public static void NeutralRectangle(int column, int row, out float minX, out float minY, out float maxX, out float maxY)
        {
            if (column == 1)
                NetFaceMapping.ZoneRectangle(column, row, 1f, out minX, out minY, out maxX, out maxY);
            else
                NetFaceMapping.ContestedRectangle(column, row, false, false, false, false, out minX, out minY, out maxX, out maxY);
        }

        public static Rect Neutral(int column, int row)
        {
            NeutralRectangle(column, row, out float a, out float b, out float c, out float d);
            return new Rect(a, b, c, d);
        }

        /// <summary>Where an Open Zone's block ends: a point at the Zone centre (nothing left).</summary>
        public static Rect OpenTarget(int column, int row)
        {
            NetFaceMapping.ZoneRectangle(column, row, 0f, out float a, out float b, out float c, out float d);
            return new Rect(a, b, c, d);
        }

        public static Rect Lerp(Rect from, Rect to, float t)
        {
            return new Rect(
                from.MinX + (to.MinX - from.MinX) * t,
                from.MinY + (to.MinY - from.MinY) * t,
                from.MaxX + (to.MaxX - from.MaxX) * t,
                from.MaxY + (to.MaxY - from.MaxY) * t);
        }
    }
}
