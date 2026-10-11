namespace HockeyGame.Core
{
    /// <summary>Small number helpers shared inside the Round core.</summary>
    internal static class Numbers
    {
        /// <summary>Clamps to 0..1.</summary>
        internal static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        /// <summary>Smoothstep: clamps to 0..1, then eases in and out (0 at 0, 1 at 1, flat at both ends).</summary>
        internal static double SmoothStep01(double t)
        {
            t = Clamp01(t);
            return t * t * (3 - 2 * t);
        }
    }
}
