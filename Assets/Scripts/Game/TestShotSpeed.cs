namespace HockeyGame
{
    /// <summary>Test Shot speed in km/h: 10-200 in steps of 5.</summary>
    public static class TestShotSpeed
    {
        public const int MinKmh = 10;
        public const int MaxKmh = 200;
        public const int StepKmh = 5;
        public const int DefaultKmh = 80;

        public static int Step(int current, int steps)
        {
            int next = current + steps * StepKmh;
            if (next < MinKmh) return MinKmh;
            if (next > MaxKmh) return MaxKmh;
            return next;
        }
    }
}
