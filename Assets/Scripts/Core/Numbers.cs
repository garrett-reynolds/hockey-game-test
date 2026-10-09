namespace HockeyGame.Core
{
    /// <summary>Small number helpers shared inside the Round core.</summary>
    internal static class Numbers
    {
        /// <summary>Clamps to 0..1.</summary>
        internal static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
