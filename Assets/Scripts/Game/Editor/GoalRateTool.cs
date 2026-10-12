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
    /// The random shooter aims anywhere on the Net Face. The Gap-reader aims at a Gap: of the cells the goalie-reach port calls
    /// Gaps (IGoalieReach.Gaps, the cells Easy mode glows), the centre of the one with the biggest Late Margin in Round's own race
    /// (ShotRace.At, at the nominal Reaction Time). Then it adds aim noise; a noisy Shot that leaves the Net Face is a Miss. Both
    /// shoot at a random speed and a random time in the window. The report says how many Gap-reader Shots had no Gap to aim at.
    /// </para>
    /// Run from the menu (Hockey Game/Measure Goal Rate), or from the CLI: Unity -batchmode -quit -projectPath ... -executeMethod
    /// HockeyGame.Editor.GoalRateTool.RunFromCommandLine [-goalRounds N] [-goalSeed N] [-goalNoise units] -logFile ...
    /// </summary>
    public static class GoalRateTool
    {
        public const int DefaultRounds = 200;
        public const int DefaultSeed = 1;
        /// <summary>Standard deviation of the Gap-reader's aim noise, in Net Face units (the Net Face is 0 to 100 each way).</summary>
        public const double DefaultAimNoise = 8;

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
            /// <summary>Gap-reader only: Shots aimed at a Gap, and Shots with no Gap to aim at (aimed at the best-margin cell).</summary>
            public int AimedAtGap, NoGap;
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
                var remembered = new RememberedReach(reach); // for the Gap-reader's aim; Round asks the port itself
                report.Random = Play("Random", rounds, seed, settings, reach, remembered, aimNoise, false);
                report.GapReader = Play("Gap-reader", rounds, seed, settings, reach, remembered, aimNoise, true);
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

        /// <summary>
        /// The port's answers, remembered: they hang only on the Set-up, the Attack Side, the time in the window and the Net Face
        /// point (the Shot start follows from the side and the time), never on the speed, and the Gap-reader asks the same ones
        /// over and over.
        /// </summary>
        sealed class RememberedReach : IGoalieReach
        {
            readonly IGoalieReach _reach;
            readonly Dictionary<(SetUp, AttackSide, double, double, double, double), GoalieReachAnswer> _answers =
                new Dictionary<(SetUp, AttackSide, double, double, double, double), GoalieReachAnswer>();
            readonly Dictionary<(SetUp, AttackSide, double, int, int), NetFaceGaps> _gaps =
                new Dictionary<(SetUp, AttackSide, double, int, int), NetFaceGaps>();

            public RememberedReach(IGoalieReach reach) { _reach = reach; }

            public GoalieReachAnswer Reach(SetUp setUp, AttackSide side, double secondsSinceWindowOpened, RinkPoint shotStart, RinkPoint netPoint)
            {
                var key = (setUp, side, secondsSinceWindowOpened, netPoint.X, netPoint.Y, netPoint.Z);
                if (!_answers.TryGetValue(key, out GoalieReachAnswer answer))
                    _answers[key] = answer = _reach.Reach(setUp, side, secondsSinceWindowOpened, shotStart, netPoint);
                return answer;
            }

            public NetFaceGaps Gaps(SetUp setUp, AttackSide side, double secondsSinceWindowOpened, RinkPoint shotStart, int firstCell, int cellCount)
            {
                var key = (setUp, side, secondsSinceWindowOpened, firstCell, cellCount);
                if (!_gaps.TryGetValue(key, out NetFaceGaps gaps))
                    _gaps[key] = gaps = _reach.Gaps(setUp, side, secondsSinceWindowOpened, shotStart, firstCell, cellCount);
                return gaps;
            }
        }

        static Tally Play(string shooter, int rounds, int seed, RoundSettings settings, IGoalieReach reach, IGoalieReach aimReach,
            double aimNoise, bool readsGaps)
        {
            var tally = new Tally { Shooter = shooter };
            var roundRandom = new SystemRandom(seed);
            var shooterRandom = new System.Random(seed + 7919); // the shooter's own: speed, time, aim noise
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
                        if (AimAtAGap(round, settings, aimReach, speed, out x, out y)) tally.AimedAtGap++;
                        else tally.NoGap++;
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
                    round.Whistle(ScriptedShot.Result(round).Kind); // no puck here: the Whistle with today's result
                    Count(tally, setUp, speed, round.LastResult.Value.Kind);
                    round.Tick(settings.ResultHoldSeconds);
                }
            }
            return tally;
        }

        /// <summary>
        /// Where the Gap-reader aims: the centre of the Gap (IGoalieReach.Gaps, the cells Easy mode glows) with the biggest Late
        /// Margin in Round's race (ShotRace.At) at the nominal Reaction Time; among Gaps no gear can reach (an endless Late Margin),
        /// the one the puck gets to soonest. With no Gap at all, it falls back to the Net Face cell with the biggest Late Margin
        /// (the old best-margin aim, on the Gaps' cells). True when it aimed at a Gap.
        /// </summary>
        static bool AimAtAGap(Round round, RoundSettings settings, IGoalieReach reach, double speedKmh, out double bestX, out double bestY)
        {
            double t = round.PhaseSecondsElapsed;
            RinkPoint from = Breakaway.ShotStart(round.AttackSide, t, settings);
            NetFaceGaps gaps = reach.Gaps(round.SetUp, round.AttackSide, t, from, 0, NetFaceGaps.Cells);
            bool anyGap = gaps.Count > 0;
            double best = double.NegativeInfinity, bestFlight = double.PositiveInfinity;
            bestX = bestY = 50;
            for (int row = 0; row < NetFaceGaps.Rows; row++)
            for (int column = 0; column < NetFaceGaps.Columns; column++)
            {
                if (anyGap && !gaps.IsGap(column, row)) continue;
                NetFaceGaps.CellCentre(column, row, out double x, out double y);
                ShotRace race = ShotRace.At(round.SetUp, round.AttackSide, t, x, y, speedKmh, settings.ReactionSeconds, reach, settings);
                double margin = race.LateMarginSeconds;
                if (margin > best || (margin == best && race.FlightSeconds < bestFlight))
                {
                    best = margin;
                    bestFlight = race.FlightSeconds;
                    bestX = x;
                    bestY = y;
                }
            }
            return anyGap;
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
                if (t.AimedAtGap + t.NoGap > 0)
                    sb.Append(string.Format(inv, "  aimed at a Gap on {0} Shots; no Gap to aim at on {1} (aimed at the best-margin cell)\n", t.AimedAtGap, t.NoGap));
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
