namespace HockeyGame.Core
{
    public enum RoundPhase
    {
        Idle,
        SkateIn,
        SlowDown,
        ShootingWindow,
        ResultHold,
        RoundOver
    }

    public enum ShotResultKind
    {
        Goal,
        Save,
        Miss,
        TooLate
    }

    /// <summary>Outcome of one Shot. HasShot is false for TooLate (X, Y, SpeedKmh are then 0).</summary>
    public readonly struct ShotResult
    {
        public ShotResultKind Kind { get; }
        public bool HasShot { get; }
        public double X { get; }
        public double Y { get; }
        public double SpeedKmh { get; }
        public bool IsTestShot { get; }

        public ShotResult(ShotResultKind kind, bool hasShot, double x, double y, double speedKmh, bool isTestShot)
        {
            Kind = kind;
            HasShot = hasShot;
            X = x;
            Y = y;
            SpeedKmh = speedKmh;
            IsTestShot = isTestShot;
        }
    }
}
