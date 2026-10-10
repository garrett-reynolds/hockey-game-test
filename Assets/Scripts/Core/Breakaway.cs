using System;

namespace HockeyGame.Core
{
    /// <summary>
    /// Pure numbers for the breakaway feel (skate-in, slow-down, shooting window ring), from #5 variant B.
    /// Distances are metres in front of the Net Face; x is metres right of the Net Face centre, shooter's view.
    /// The scene reads these each frame; the Round core still only gets real time. Phase lengths come from the
    /// Round's RoundSettings, so the feel follows the phases the Round actually runs.
    /// </summary>
    public static class Breakaway
    {
        public const double StartDistance = 22.0;
        public const double StopDistance = 9.0;
        public const double SlowTimeScale = 0.25;
        public const double SlowRampSeconds = 0.35;
        public const double EyeHeight = 1.5;
        public const double StrideBob = 0.06;
        public const double RollRadians = 0.025;
        /// <summary>The stride bob goes round this fast (radians per game second): one visible stride per cycle.</summary>
        public const double BobRadiansPerSecond = 10.0;
        /// <summary>The roll goes round half as fast as the bob: it leans one way on one stride and the other way on the next.</summary>
        public const double RollRadiansPerSecond = 5.0;

        /// <summary>Camera height offset (metres) from the stride bob, gameSeconds into the skate-in.</summary>
        public static double Bob(double gameSeconds) => StrideBob * Math.Sin(gameSeconds * BobRadiansPerSecond);

        /// <summary>Camera roll (radians) gameSeconds into the skate-in. Positive leans the camera left (Unity +z roll).</summary>
        public static double Roll(double gameSeconds) => RollRadians * Math.Sin(gameSeconds * RollRadiansPerSecond);

        /// <summary>When the k-th push of the stride lands (game seconds into the skate-in, k from 0): the low point of the bob.</summary>
        public static double StridePushSeconds(int k) => (1.5 * Math.PI + 2.0 * Math.PI * k) / BobRadiansPerSecond;
        /// <summary>The drift toward the net never comes closer than this to the net (metres).</summary>
        public const double ClosestDrift = 3.0;

        /// <summary>-1 left, 0 middle, +1 right.</summary>
        public static int SideSign(AttackSide side) => side == AttackSide.Left ? -1 : side == AttackSide.Right ? 1 : 0;

        /// <summary>
        /// Point on the skate-in curl at progress u (0 = start, 1 = stop): a quadratic curve from the wide start,
        /// through a control point straight ahead of the start halfway in, to the stop point.
        /// </summary>
        public static void SkatePoint(AttackSide side, double u, out double x, out double distance)
        {
            int sign = SideSign(side);
            double startX = sign == 0 ? 6.0 : sign * 10.0;
            double stopX = sign * 0.3 * StopDistance;
            double controlDistance = (StartDistance + StopDistance) / 2.0;
            double a = (1 - u) * (1 - u), b = 2 * (1 - u) * u, c = u * u;
            x = a * startX + b * startX + c * stopX;
            distance = a * StartDistance + b * controlDistance + c * StopDistance;
        }

        /// <summary>Skate-in progress (0..1), linear in real seconds since the skate-in began.</summary>
        public static double SkateProgress(double skateSeconds, RoundSettings settings) => Numbers.Clamp01(skateSeconds / settings.SkateInSeconds);

        /// <summary>Skate speed (m/s): straight start-to-stop distance over the skate-in time. Used for the slow-down drift.</summary>
        public static double SkateSpeed(AttackSide side, RoundSettings settings)
        {
            SkatePoint(side, 0, out double x0, out double d0);
            SkatePoint(side, 1, out double x1, out double d1);
            double dx = x1 - x0, dd = d1 - d0;
            return Math.Sqrt(dx * dx + dd * dd) / settings.SkateInSeconds;
        }

        /// <summary>World time scale for a phase, given real seconds spent in it.</summary>
        public static double TimeScale(RoundPhase phase, double phaseSeconds)
        {
            switch (phase)
            {
                case RoundPhase.SlowDown:
                    double t = EaseOut(Numbers.Clamp01(phaseSeconds / SlowRampSeconds));
                    return 1.0 + (SlowTimeScale - 1.0) * t;
                case RoundPhase.ShootingWindow:
                    return SlowTimeScale;
                default:
                    return 1.0;
            }
        }

        /// <summary>Strength of the slow-mo look (grey, contrast, vignette): 0 at 1x, 1 at the slow time scale.</summary>
        public static double LookAmount(double timeScale) => Numbers.Clamp01((1.0 - timeScale) / (1.0 - SlowTimeScale));

        /// <summary>
        /// How long the goalie takes to react out of the Ready Stance after the Shot (real seconds; into its Save Reaction, or
        /// the Goalie Pose for the block goalie).
        /// Shorter than ShotFlight.MinSeconds, so the reaction is complete before even the fastest puck reaches the Net Face.
        /// </summary>
        public const double GoalieReactSeconds = 0.2;

        /// <summary>
        /// How far the goalie has moved from the Ready Stance into its reaction (0..1). 0 until the Shot: the player gets no hint
        /// of the pose. In the result hold after a Shot it eases out to 1 over GoalieReactSeconds (real time); with no Shot
        /// (too late) it stays 0.
        /// </summary>
        public static double GoalieSet(RoundPhase phase, double phaseSeconds, bool shotTaken)
        {
            if (phase != RoundPhase.ResultHold || !shotTaken) return 0;
            return EaseOut(Numbers.Clamp01(phaseSeconds / GoalieReactSeconds));
        }

        /// <summary>Share of the shooting window ring still showing (1 full, 0 empty).</summary>
        public static double RingFraction(double windowSecondsLeft, RoundSettings settings) => Numbers.Clamp01(windowSecondsLeft / settings.ShootingWindowSeconds);

        /// <summary>Drift toward the net, capped so the camera stops ClosestDrift metres from the net.</summary>
        public static double CappedDrift(double drift, double distance) => Math.Max(0, Math.Min(drift, distance - ClosestDrift));

        private static double EaseOut(double t) => 1 - (1 - t) * (1 - t);
    }
}
