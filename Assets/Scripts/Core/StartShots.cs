using System;

namespace HockeyGame.Core
{
    /// <summary>One wide shot of the arena for the start screen: the eye and the aim point move from From to To
    /// over Seconds (a fixed shot has From == To), with a vertical field of view.</summary>
    public readonly struct StartShot
    {
        public readonly string Name;
        public readonly RinkPoint EyeFrom, EyeTo, LookFrom, LookTo;
        public readonly double FieldOfViewDegrees, Seconds;
        public bool Moving => !EyeFrom.Equals(EyeTo) || !LookFrom.Equals(LookTo);

        public StartShot(string name, RinkPoint eyeFrom, RinkPoint eyeTo, RinkPoint lookFrom, RinkPoint lookTo, double fov, double seconds)
        {
            Name = name;
            EyeFrom = eyeFrom;
            EyeTo = eyeTo;
            LookFrom = lookFrom;
            LookTo = lookTo;
            FieldOfViewDegrees = fov;
            Seconds = seconds;
        }
    }

    /// <summary>Where the start-screen camera is at a time: the shot, and the eye and the aim point (rink metres).</summary>
    public readonly struct StartShotPose
    {
        public readonly int Shot;
        public readonly RinkPoint Eye, Look;
        public readonly double FieldOfViewDegrees;

        public StartShotPose(int shot, RinkPoint eye, RinkPoint look, double fov)
        {
            Shot = shot;
            Eye = eye;
            Look = look;
            FieldOfViewDegrees = fov;
        }
    }

    /// <summary>The wide shots the start screen cuts between, and the clock that picks one. Garrett tunes the numbers: one line per shot.</summary>
    public static class StartShots
    {
        public static readonly StartShot[] All =
        {
            new StartShot("High over the near net",
                new RinkPoint(0, 12, -1), new RinkPoint(0, 12, -1),
                new RinkPoint(0, 0, -32), new RinkPoint(0, 0, -32), 70, 7),
            new StartShot("Fly-over up the ice",
                new RinkPoint(0, 16, -52), new RinkPoint(0, 16, -8),
                new RinkPoint(0, 0, -30), new RinkPoint(0, 0, 6), 72, 8),
            new StartShot("High corner",
                new RinkPoint(11, 14, -54), new RinkPoint(11, 14, -54),
                new RinkPoint(-4, 2, -18), new RinkPoint(-4, 2, -18), 68, 7),
            new StartShot("Pan along the side",
                new RinkPoint(-11.5, 9, -27.1), new RinkPoint(-11.5, 9, -27.1),
                new RinkPoint(6, 2, -8), new RinkPoint(6, 2, -46), 66, 8),
            new StartShot("Net and Video Board",
                new RinkPoint(5, 6, -20), new RinkPoint(5, 6, -20),
                new RinkPoint(0, 7, 4), new RinkPoint(0, 7, 4), 64, 7),
        };

        public static double CycleSeconds
        {
            get
            {
                double sum = 0;
                foreach (StartShot shot in All) sum += shot.Seconds;
                return sum;
            }
        }

        /// <summary>The pose seconds after the start screen opened (loops; negative counts as 0). A moving shot eases in
        /// and out (smoothstep) between From and To.</summary>
        public static StartShotPose At(double seconds)
        {
            double cycle = CycleSeconds;
            double t = seconds > 0 ? seconds % cycle : 0;
            int index = 0;
            while (index < All.Length - 1 && t >= All[index].Seconds)
            {
                t -= All[index].Seconds;
                index++;
            }
            StartShot shot = All[index];
            double p = Numbers.Clamp01(t / shot.Seconds);
            double u = p * p * (3 - 2 * p);
            return new StartShotPose(index, Lerp(shot.EyeFrom, shot.EyeTo, u), Lerp(shot.LookFrom, shot.LookTo, u), shot.FieldOfViewDegrees);
        }

        private static RinkPoint Lerp(RinkPoint a, RinkPoint b, double u) =>
            new RinkPoint(a.X + (b.X - a.X) * u, a.Y + (b.Y - a.Y) * u, a.Z + (b.Z - a.Z) * u);
    }
}
