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
        /// How long the goalie takes to move out of the Ready Stance on a Shot the race did not time (real seconds): gear already
        /// in the way (finished by the time the puck gets there, see GoalieSet), a reach no gear can make, or a Miss.
        /// </summary>
        public const double GoalieReactSeconds = 0.2;

        /// <summary>
        /// How far the goalie has moved from the Ready Stance into its reaction (0..1), on the result hold's clock (phaseSeconds,
        /// real time: the puck's on-screen flight runs on it too). 0 until the Shot: the player gets no hint of the reaction.
        /// With the Round's race (Reach in time) he moves as it timed him: still until the Reaction Time it rolled, then the
        /// winning gear at its race speed, at an even pace, so it is on the puck's line at the Reach Time: at or before the
        /// Flight Time on a Save, after the puck has passed on a Goal (#63). With no gear that can reach he still waits his Reaction
        /// Time, then reaches over GoalieReactSeconds. With gear already in the way (no move in the race), or with no race (a
        /// Miss), he eases in over GoalieReactSeconds, or over the on-screen flight (flightSeconds) if that is shorter, so he is
        /// fully in it when the puck gets there. With no Shot (too late) it stays 0.
        /// </summary>
        public static double GoalieSet(RoundPhase phase, double phaseSeconds, bool shotTaken, double flightSeconds, ShotRace? race = null)
        {
            if (phase != RoundPhase.ResultHold || !shotTaken) return 0;
            if (race is ShotRace r && !r.InTheWay)
            {
                double moving = phaseSeconds - r.ReactionSeconds;
                if (moving <= 0) return 0;
                if (double.IsPositiveInfinity(r.MoveSeconds)) return EaseOut(Numbers.Clamp01(moving / GoalieReactSeconds));
                return r.MoveSeconds > 0 ? Numbers.Clamp01(moving / r.MoveSeconds) : 1;
            }
            double react = Math.Min(GoalieReactSeconds, flightSeconds);
            return react > 0 ? EaseOut(Numbers.Clamp01(phaseSeconds / react)) : 1;
        }

        /// <summary>
        /// The flight the goalie's ease-in is timed by: the scripted flight while the Round awaits the Whistle (so he eases
        /// exactly as before and nothing jumps on the Whistle frame), the Round's Shot-to-Whistle time after it.
        /// </summary>
        public static double GoalieFlightSeconds(bool awaitingWhistle, double shotToWhistleSeconds, double scriptedFlightSeconds) =>
            awaitingWhistle ? scriptedFlightSeconds : shotToWhistleSeconds;

        /// <summary>Share of the shooting window ring still showing (1 full, 0 empty).</summary>
        public static double RingFraction(double windowSecondsLeft, RoundSettings settings) => Numbers.Clamp01(windowSecondsLeft / settings.ShootingWindowSeconds);

        /// <summary>Drift toward the net, capped so the camera stops ClosestDrift metres from the net.</summary>
        public static double CappedDrift(double drift, double distance) => Math.Max(0, Math.Min(drift, distance - ClosestDrift));

        /// <summary>Game seconds that pass in the first realSeconds of a phase (TimeScale summed up: the slow-down eases into slow time).</summary>
        public static double GameSeconds(RoundPhase phase, double realSeconds)
        {
            realSeconds = Math.Max(0, realSeconds);
            switch (phase)
            {
                case RoundPhase.SlowDown:
                    double ramp = Math.Min(realSeconds, SlowRampSeconds);
                    double s = ramp / SlowRampSeconds;
                    double eased = s + (1 - s) * (1 - s) * (1 - s) / 3.0 - 1.0 / 3.0; // EaseOut summed from 0 to s
                    return ramp + (SlowTimeScale - 1.0) * SlowRampSeconds * eased + (realSeconds - ramp) * SlowTimeScale;
                case RoundPhase.ShootingWindow:
                    return realSeconds * SlowTimeScale;
                default:
                    return realSeconds;
            }
        }

        /// <summary>
        /// How far the shooter has drifted on toward the net past the skate-in's stop (metres, not capped): the skate speed over
        /// the game seconds of the slow-down so far (slowDownSeconds, real) and of the shooting window so far (windowSeconds, real).
        /// </summary>
        public static double Drift(AttackSide side, double slowDownSeconds, double windowSeconds, RoundSettings settings) =>
            SkateSpeed(side, settings) * (GameSeconds(RoundPhase.SlowDown, Math.Min(slowDownSeconds, settings.SlowDownSeconds))
                                          + GameSeconds(RoundPhase.ShootingWindow, windowSeconds));

        /// <summary>The puck starts this far ahead of the shooter's eye, toward the net centre, and this far below it (camera-up).</summary>
        public const double PuckAheadMetres = 1.0, PuckBelowMetres = 0.35;

        /// <summary>
        /// Where the puck starts on a Shot taken windowSeconds (real) into the shooting window: just below the shooter's eye
        /// (as ShotEffects starts it), the eye at the skate-in's stop drifted on toward the net, looking at the Net Face centre.
        /// Leaves out the stride bob and roll (a few centimetres).
        /// </summary>
        public static RinkPoint ShotStart(AttackSide side, double windowSeconds, RoundSettings settings) =>
            PuckBelowEye(1.0, Drift(side, settings.SlowDownSeconds, windowSeconds, settings), side);

        /// <summary>
        /// Where the puck is on the approach, phaseSeconds (real) into the skate-in, the slow-down or the shooting window: just
        /// below the shooter's eye, as ShotStart (in the window it is ShotStart). The goalie plays the angle to it (SetUps.Approach).
        /// </summary>
        public static RinkPoint Puck(AttackSide side, RoundPhase phase, double phaseSeconds, RoundSettings settings)
        {
            switch (phase)
            {
                case RoundPhase.SkateIn: return PuckBelowEye(SkateProgress(phaseSeconds, settings), 0, side);
                case RoundPhase.SlowDown: return PuckBelowEye(1.0, Drift(side, phaseSeconds, 0, settings), side);
                default: return ShotStart(side, phaseSeconds, settings);
            }
        }

        /// <summary>The puck below the shooter's eye at skate progress u, drifted on toward the net (metres, capped).</summary>
        static RinkPoint PuckBelowEye(double u, double drift, AttackSide side)
        {
            SkatePoint(side, u, out double x, out double distance);
            double toNet = Math.Sqrt(x * x + distance * distance);
            double along = CappedDrift(drift, toNet);
            double eyeX = x - x / toNet * along, eyeZ = -distance + distance / toNet * along, eyeY = EyeHeight;

            // Looking at the Net Face centre (as Transform.LookAt): forward, then right = up x forward, then camera-up = forward x right.
            double fx = -eyeX, fy = NetFrame.FaceHeightMetres / 2.0 - eyeY, fz = -eyeZ;
            double f = Math.Sqrt(fx * fx + fy * fy + fz * fz);
            fx /= f; fy /= f; fz /= f;
            double rx = fz, rz = -fx, r = Math.Sqrt(rx * rx + rz * rz);
            rx /= r; rz /= r;
            double ux = fy * rz, uy = fz * rx - fx * rz, uz = -fy * rx;
            return new RinkPoint(eyeX + fx * PuckAheadMetres - ux * PuckBelowMetres,
                eyeY + fy * PuckAheadMetres - uy * PuckBelowMetres,
                eyeZ + fz * PuckAheadMetres - uz * PuckBelowMetres);
        }

        private static double EaseOut(double t) => 1 - (1 - t) * (1 - t);
    }
}
