using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HockeyGame.Core;
using UnityEditor;
using UnityEngine;

namespace HockeyGame.Editor
{
    /// <summary>
    /// A developer tool, not a test: plays many Rounds through Round with the real goalie-reach port (GoalieCharacterReach on
    /// a GoalieCharacter) and prints the Goals per Round, by Set-up and by speed band, for a random shooter and a Gap-reading
    /// shooter. Used to tune toward 1 Goal in 3 Shots for a Gap-reader (RoundSettings).
    /// <para>
    /// The random shooter aims anywhere on the Net Face. The Gap-reader probes the same IGoalieReach race over a grid of Net
    /// Face points, aims at the one with the biggest expected Late Margin for this Set-up (at the nominal Reaction Time), then adds
    /// aim noise; a noisy Shot that leaves the Net Face is a Miss. Both shoot at a random speed and a random time in the window.
    /// </para>
    /// Run from the menu (Hockey Game/Measure Goal Rate), or from the CLI: Unity -batchmode -projectPath ... -executeMethod
    /// HockeyGame.Editor.GoalRateTool.RunFromCommandLine [-goalRounds N] [-goalSeed N] [-goalNoise units] -logFile ...
    /// </summary>
    public static class GoalRateTool
    {
        public const int DefaultRounds = 200;
        public const int DefaultSeed = 1;
        /// <summary>Standard deviation of the Gap-reader's aim noise, in Net Face units (the Net Face is 0 to 100 each way).</summary>
        public const double DefaultAimNoise = 8;

        const int GridSize = 10; // probe points at 5, 15, ... 95 on each axis
        static readonly double[] ShotTimes = { 0.0, 1.0, 2.0, 2.9 }; // seconds into the shooting window
        static readonly double[] BandEdges = { 10, 50, 90, 130, 170, 200 }; // km/h; the last band includes 200

        [MenuItem("Hockey Game/Measure Goal Rate")]
        public static void RunFromMenu() => Debug.Log(Run(DefaultRounds, DefaultSeed, DefaultAimNoise).Text);

        public static void RunFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int rounds = (int)Arg(args, "-goalRounds", DefaultRounds);
            int seed = (int)Arg(args, "-goalSeed", DefaultSeed);
            double noise = Arg(args, "-goalNoise", DefaultAimNoise);
            Debug.Log(Run(rounds, seed, noise).Text);
        }

        static double Arg(string[] args, string name, double fallback)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name && double.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                    return v;
            return fallback;
        }

        /// <summary>The numbers for one shooter: Shots and Goals overall, by Set-up and by speed band.</summary>
        public sealed class Tally
        {
            public string Shooter;
            public int Rounds, Shots, Goals, Saves, Misses;
            public readonly Dictionary<string, int[]> BySetUp = new Dictionary<string, int[]>(); // { shots, goals }
            public readonly int[] BandShots = new int[BandEdges.Length - 1];
            public readonly int[] BandGoals = new int[BandEdges.Length - 1];

            public double GoalsPerRound => Rounds == 0 ? 0 : (double)Goals / Rounds;
            public double GoalRate => Shots == 0 ? 0 : (double)Goals / Shots;
        }

        public sealed class Report
        {
            public int Rounds, Seed;
            public double AimNoise;
            public Tally Random, GapReader;
            public string Text;
        }

        /// <summary>Plays the given number of Rounds for each shooter, the same seed each, so a run repeats. Needs the goalie model to load (else every Shot is a Goal).</summary>
        public static Report Run(int rounds, int seed, double aimNoise)
        {
            var root = new GameObject("Goal rate tool (temporary)");
            try
            {
                var settings = new RoundSettings();
                var character = new GoalieCharacter(root.transform);
                var report = new Report { Rounds = rounds, Seed = seed, AimNoise = aimNoise };
                var reach = new GoalieCharacterReach(character, settings);
                report.Random = Play("Random", rounds, seed, settings, reach, aimNoise, false);
                report.GapReader = Play("Gap-reader", rounds, seed, settings, reach, aimNoise, true);
                report.Text = Format(report, character.Present);
                return report;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        sealed class SystemRandom : IRandomSource
        {
            readonly System.Random _random;
            public SystemRandom(int seed) { _random = new System.Random(seed); }
            public double Next() => _random.NextDouble();
        }

        static Tally Play(string shooter, int rounds, int seed, RoundSettings settings, IGoalieReach reach, double aimNoise, bool readsGaps)
        {
            var tally = new Tally { Shooter = shooter };
            var roundRandom = new SystemRandom(seed);
            var shooterRandom = new System.Random(seed + 7919); // the shooter's own: speed, time, aim noise
            var probes = new Dictionary<(SetUp, AttackSide, double, int), GoalieReachAnswer>();
            var round = new Round(roundRandom, settings, reach, 0);
            for (int r = 0; r < rounds; r++)
            {
                round.StartRound();
                tally.Rounds++;
                while (round.Phase != RoundPhase.RoundOver)
                {
                    double time = ShotTimes[shooterRandom.Next(ShotTimes.Length)];
                    double speed = 10 + 190 * shooterRandom.NextDouble();
                    round.Tick(settings.SkateInSeconds + settings.SlowDownSeconds + time);
                    double x, y;
                    if (readsGaps)
                    {
                        AimAtBiggestGap(round, settings, reach, probes, speed, out x, out y);
                        x += aimNoise * Gaussian(shooterRandom);
                        y += aimNoise * Gaussian(shooterRandom);
                    }
                    else
                    {
                        x = 100 * shooterRandom.NextDouble();
                        y = 100 * shooterRandom.NextDouble();
                    }
                    SetUp setUp = round.SetUp;
                    round.SubmitShot(x, y, speed, false);
                    Count(tally, setUp, speed, round.LastResult.Value.Kind);
                    round.Tick(settings.ResultHoldSeconds);
                }
            }
            return tally;
        }

        /// <summary>The probe grid point with the biggest expected Late Margin (Reach Time minus Flight Time, at the nominal Reaction Time).</summary>
        static void AimAtBiggestGap(Round round, RoundSettings settings, IGoalieReach reach,
            Dictionary<(SetUp, AttackSide, double, int), GoalieReachAnswer> probes, double speedKmh, out double bestX, out double bestY)
        {
            double t = round.PhaseSecondsElapsed;
            RinkPoint from = Breakaway.ShotStart(round.AttackSide, t, settings);
            RinkPoint spot = SetUps.Spot(round.SetUp, round.AttackSide, t, from, settings);
            double best = double.NegativeInfinity;
            bestX = bestY = 50;
            for (int i = 0; i < GridSize * GridSize; i++)
            {
                double x = 5 + 10 * (i % GridSize), y = 5 + 10 * (i / GridSize);
                var key = (round.SetUp, round.AttackSide, t, i);
                if (!probes.TryGetValue(key, out GoalieReachAnswer answer))
                    probes[key] = answer = reach.Reach(round.SetUp, round.AttackSide, t, from, RinkPoint.OnNetFace(x, y));
                double flight = ShotFlight.Seconds(RinkPoint.Distance(from, RinkPoint.AtDepth(from, RinkPoint.OnNetFace(x, y), spot.Z)), speedKmh);
                double expectedReach = answer.InTheWay ? 0 : settings.ReactionSeconds + answer.MoveSeconds;
                double margin = double.IsPositiveInfinity(expectedReach) ? 1e6 - flight : expectedReach - flight; // no gear can reach: the biggest gap there is
                if (margin > best)
                {
                    best = margin;
                    bestX = x;
                    bestY = y;
                }
            }
        }

        static double Gaussian(System.Random random)
        {
            double u = 1 - random.NextDouble(), v = random.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v);
        }

        static void Count(Tally tally, SetUp setUp, double speed, ShotResultKind kind)
        {
            tally.Shots++;
            bool goal = kind == ShotResultKind.Goal;
            if (goal) tally.Goals++;
            else if (kind == ShotResultKind.Save) tally.Saves++;
            else tally.Misses++;

            string name = setUp.Kind.ToString();
            if (!tally.BySetUp.TryGetValue(name, out int[] bySetUp)) tally.BySetUp[name] = bySetUp = new int[2];
            bySetUp[0]++;
            if (goal) bySetUp[1]++;

            int band = 0;
            while (band < BandEdges.Length - 2 && speed >= BandEdges[band + 1]) band++;
            tally.BandShots[band]++;
            if (goal) tally.BandGoals[band]++;
        }

        static string Format(Report report, bool goaliePresent)
        {
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            sb.Append(string.Format(inv, "Goal rate: {0} Rounds of {1} Shots per shooter, seed {2}, Gap-reader aim noise {3:0.#}.\n", report.Rounds, Round.ShotsPerRound, report.Seed, report.AimNoise));
            if (!goaliePresent) sb.Append("WARNING: the goalie model did not load, so no gear can reach: every Shot on the Net Face is a Goal.\n");
            foreach (Tally t in new[] { report.Random, report.GapReader })
            {
                sb.Append(string.Format(inv, "{0}: {1:0.00} Goals per Round ({2:0.0%} of Shots); {3} Goals, {4} Saves, {5} Misses\n",
                    t.Shooter, t.GoalsPerRound, t.GoalRate, t.Goals, t.Saves, t.Misses));
                foreach (SetUpKind kind in Enum.GetValues(typeof(SetUpKind)))
                    if (t.BySetUp.TryGetValue(kind.ToString(), out int[] c))
                        sb.Append(string.Format(inv, "  Set-up {0,-10} {1:0.0%} ({2} of {3})\n", kind, (double)c[1] / c[0], c[1], c[0]));
                for (int b = 0; b < t.BandShots.Length; b++)
                    sb.Append(string.Format(inv, "  {0,3}-{1,3} km/h    {2:0.0%} ({3} of {4})\n", BandEdges[b], BandEdges[b + 1],
                        t.BandShots[b] == 0 ? 0 : (double)t.BandGoals[b] / t.BandShots[b], t.BandGoals[b], t.BandShots[b]));
            }
            return sb.ToString();
        }
    }
}
