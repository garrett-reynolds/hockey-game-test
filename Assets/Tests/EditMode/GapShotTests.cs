using System;
using System.Collections.Generic;
using System.Text;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// A Shot into a shown Gap (#134): a whole Round is played with the real BreakawayView, goalie view, goalie-reach port and
    /// Unity physics (in a preview physics scene of its own), 60 fps with two 1/120 s physics steps a frame, as the game does.
    /// At a seeded moment in the shooting window the Gaps are asked (Round.GapsNow: the cells Easy mode glows), the centre of
    /// one is shot at, and the Round's Whistle says Goal, Save or Miss. Only API that also exists on main before #130 (f7f9d2d)
    /// is used, so the same test measures the old code.
    /// </summary>
    public class GapShotTests
    {
        const float Frame = 1f / 60f;
        UnityEngine.SceneManagement.Scene _scene;
        PhysicsScene _physics;
        SimulationMode _oldMode;
        GameObject _parent;

        [SetUp]
        public void SetUp()
        {
            _oldMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            _scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            _physics = _scene.GetPhysicsScene();
            Assert.IsTrue(_physics != Physics.defaultPhysicsScene, "the preview scene has no physics scene of its own");
            _parent = new GameObject("Rink (test)");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_parent, _scene);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_parent);
            if (_scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(_scene);
            _scene = default;
            Physics.simulationMode = _oldMode;
            Time.timeScale = 1f;
        }

        /// <summary>One Shot's numbers.</summary>
        public struct Sample
        {
            public AttackSide Side;
            public SetUp SetUp;
            public int Column, Row;
            public bool IntoGap, Roomy;
            public ShotResultKind Kind;
            public bool Played;
            public string Touched;
            public bool RaceSaves, InTheWay;
            public double LateMargin;
            public SaveReaction Gear;
        }

        /// <summary>The cell is a Gap and so is each of its 4 neighbours on the Net Face: the opening has room round the puck.</summary>
        static bool HasRoom(NetFaceGaps gaps, int column, int row)
        {
            if (!gaps.IsGap(column, row)) return false;
            if (column > 0 && !gaps.IsGap(column - 1, row)) return false;
            if (column < NetFaceGaps.Columns - 1 && !gaps.IsGap(column + 1, row)) return false;
            if (row > 0 && !gaps.IsGap(column, row - 1)) return false;
            if (row < NetFaceGaps.Rows - 1 && !gaps.IsGap(column, row + 1)) return false;
            return true;
        }

        /// <summary>
        /// Plays one Round (seed) to the shooting window and shoots at the centre of a seeded cell: a Gap (intoGap) or a cell that is
        /// not one, `pick` choosing among those cells. The Shot goes at the seeded time 0.2 to 2.8 s into the window, at 100 km/h.
        /// </summary>
        Sample Play(int seed, bool intoGap, bool roomyOnly, double kmh = 100)
        {
            var sample = new Sample { IntoGap = intoGap };
            var cameraObject = new GameObject("Camera (test)", typeof(Camera));
            var driver = new GameObject("Breakaway (test)");
            PhysicsShotEffects effects = null;
            GoalieView view = null;
            try
            {
                var rng = new System.Random(seed * 7919 + 13);
                double shootAfter = 0.2 + 2.6 * rng.NextDouble();
                var camera = cameraObject.GetComponent<Camera>();
                var settings = new RoundSettings();
                var character = new GoalieCharacter(_parent.transform);
                Assume.That(character.Present, "needs the goalie model");
                view = new GoalieView(_parent.transform, character);
                effects = new PhysicsShotEffects(_parent.transform, camera, new SeededRandom(7), view, name => null);
                Physics.SyncTransforms();
                var reach = new GoalieCharacterReach(character, settings);
                var round = new Round(new SeededRandom(seed), settings, reach, 0);
                var breakaway = driver.AddComponent<BreakawayView>();
                breakaway.Init(round, camera, view, () => effects.FlightSeconds);
                round.StartRound();
                bool shot = false;
                for (int frame = 0; frame < 60 * 30; frame++)
                {
                    for (int step = 0; step < 2; step++)
                    {
                        _physics.Simulate(PhysicsShot.StepSeconds);
                        effects.AfterPhysicsStep(PhysicsShot.StepSeconds);
                    }
                    round.Tick(Frame);
                    view.Show(true);
                    if (!shot && round.Phase == RoundPhase.ShootingWindow && round.PhaseSecondsElapsed > shootAfter)
                    {
                        NetFaceGaps gaps = round.GapsNow();
                        var cells = new List<int>();
                        for (int cell = 0; cell < NetFaceGaps.Cells; cell++)
                        {
                            int c = cell % NetFaceGaps.Columns, r = cell / NetFaceGaps.Columns;
                            bool isGap = gaps.IsGap(c, r);
                            if (isGap == intoGap && (!intoGap || !roomyOnly || HasRoom(gaps, c, r))) cells.Add(cell);
                        }
                        if (cells.Count == 0) return sample; // nothing to aim at this time: not played
                        int pick = cells[rng.Next(cells.Count)];
                        sample.Column = pick % NetFaceGaps.Columns;
                        sample.Row = pick / NetFaceGaps.Columns;
                        sample.Roomy = HasRoom(gaps, sample.Column, sample.Row);
                        sample.Side = round.AttackSide;
                        sample.SetUp = round.SetUp;
                        NetFaceGaps.CellCentre(sample.Column, sample.Row, out double x, out double y);
                        round.SubmitShot(x, y, kmh, false);
                        shot = true;
                    }
                    effects.Update(round, Frame);
                    breakaway.Step(Frame * Time.timeScale, Frame);
                    if (shot && !round.AwaitingWhistle && round.LastResult.HasValue)
                    {
                        sample.Kind = round.LastResult.Value.Kind;
                        sample.Played = true;
                        sample.Touched = effects.Shot != null && effects.Shot.FirstTouched != null ? effects.Shot.FirstTouched.name : "-";
                        if (round.LastRace.HasValue)
                        {
                            ShotRace race = round.LastRace.Value;
                            sample.RaceSaves = race.IsSave;
                            sample.InTheWay = race.InTheWay;
                            sample.LateMargin = race.LateMarginSeconds;
                            sample.Gear = race.SaveReaction;
                        }
                        break;
                    }
                }
            }
            finally
            {
                effects?.Dispose();
                view?.Dispose();
                Object.DestroyImmediate(driver);
                Object.DestroyImmediate(cameraObject);
            }
            return sample;
        }

        sealed class Rates
        {
            public int Shots, Goals, Saves, Misses;
            public readonly int[] SideShots = new int[3], SideGoals = new int[3];
            public double Goal => Shots == 0 ? 0 : (double)Goals / Shots;
            public double Save => Shots == 0 ? 0 : (double)Saves / Shots;
            public void Add(Sample s)
            {
                Shots++;
                int side = (int)s.Side;
                SideShots[side]++;
                if (s.Kind == ShotResultKind.Goal) { Goals++; SideGoals[side]++; }
                else if (s.Kind == ShotResultKind.Save) Saves++;
                else Misses++;
            }
            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.Append(Shots + " Shots: " + Goals + " Goals, " + Saves + " Saves, " + Misses + " Misses; Goal rate " + Goal.ToString("P0") + ", Save rate " + Save.ToString("P0") + "; by Attack Side:");
                for (int i = 0; i < 3; i++) sb.Append(" " + (AttackSide)i + " " + SideGoals[i] + "/" + SideShots[i]);
                return sb.ToString();
            }
        }

        Rates Measure(int seeds, bool intoGap, bool roomyOnly, out string log, double kmh = 100)
        {
            var rates = new Rates();
            var text = new StringBuilder();
            int notPlayed = 0;
            for (int seed = 1; seed <= seeds; seed++)
            {
                Sample s = Play(seed, intoGap, roomyOnly, kmh);
                if (!s.Played) { notPlayed++; continue; }
                rates.Add(s);
                text.AppendLine($"seed {seed} {s.Side} {s.SetUp} cell {s.Column},{s.Row} roomy {s.Roomy} -> {s.Kind} | race saves {s.RaceSaves} wayIn {s.InTheWay} margin {s.LateMargin:F2} {s.Gear} | touched {s.Touched}");
            }
            text.AppendLine("not played (no cell to aim at, or no result): " + notPlayed);
            log = text.ToString();
            return rates;
        }

        /// <summary>The bar: a Shot into the centre of a Gap that fits the puck with room is a Goal at least this often (#134).</summary>
        public const double GoalBar = 0.75;

        const int Seeds = 90;

        [Test, Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void A_Shot_into_the_centre_of_a_roomy_Gap_is_a_Goal_most_of_the_time()
        {
            Rates rates = Measure(Seeds, true, true, out string log);
            Assert.GreaterOrEqual(rates.Shots, 50, "enough Shots were played\n" + log);
            Assert.GreaterOrEqual(rates.Goal, GoalBar, "Goal rate into a roomy Gap: " + rates + "\n" + log);
        }

        [Test, Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void DIAG_any_Gap()
        {
            Rates any = Measure(Seeds, true, false, out string log);
            Assert.Fail("INTO ANY GAP: " + any + "\n" + log);
        }

        [Test, Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void DIAG_not_a_Gap()
        {
            Rates not = Measure(Seeds, false, false, out string log);
            Assert.Fail("NOT INTO A GAP: " + not + "\n" + log);
        }

        [Test, Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void DIAG_roomy_Gap()
        {
            Rates r = Measure(Seeds, true, true, out string log);
            Assert.Fail("INTO ROOMY GAP: " + r + "\n" + log);
        }
    }
}
