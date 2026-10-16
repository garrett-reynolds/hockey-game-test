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
    /// Unity physics (in a preview physics scene of its own), 60 fps with two 1/120 s physics steps a frame, the goalie posed
    /// and his hit shapes moved before each step as BreakawayView.FixedUpdate does. At a seeded moment in the shooting window the
    /// Gaps are asked (Round.GapsNow: the cells Easy mode glows), the centre of one is shot at (100 km/h), and the Round's Whistle
    /// says Goal, Save or Miss. Only API that also exists on main before #130 (f7f9d2d) is used, so the same test measured the old code.
    /// </summary>
    public class GapShotTests
    {
        const float Frame = 1f / 60f;
        const int Seeds = 130; // (#150: 90 played only 56 Shots into a Gap once the Gaps got roomier; the guard stays at 60)

        /// <summary>The bar (#134): a Shot into the centre of a shown Gap is a Goal at least this often. Measured 83 % after the fix, 59 % before (#134); 93 % with the thin-shape casts (#150).</summary>
        public const double GapGoalBar = 0.75;

        /// <summary>A Shot into a cell that is not a Gap is saved at least this often (before and after the fix: 84 %).</summary>
        public const double NotGapSaveBar = 0.75;

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

        struct Sample
        {
            public AttackSide Side;
            public ShotResultKind Kind;
            public bool Played;
            public int GapCount;
            public SetUpKind SetUpKind;
        }

        /// <summary>Where the Shot is aimed: the centre of a shown Gap, of a cell that is not one, or of a cell the race finds gear in the way of (the cells that were not Gaps before #134).</summary>
        enum Aim { Gap, NotGap, GearInTheWay }

        /// <summary>Plays the Round of this seed to a seeded moment in its window and shoots at the centre of a cell picked by aim.</summary>
        Sample Play(int seed, Aim aim, double speedKmh = 100)
        {
            var sample = new Sample();
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
                var port = new GoalieCharacterReach(character, settings);
                var round = new Round(new SeededRandom(seed), settings, port, 0);
                var breakaway = driver.AddComponent<BreakawayView>();
                breakaway.Init(round, camera, view, () => effects.FlightSeconds);
                round.StartRound();
                bool shot = false;
                for (int frame = 0; frame < 60 * 30; frame++)
                {
                    for (int step = 0; step < 2; step++)
                    {
                        breakaway.PoseForPhysics((step + 1) * PhysicsShot.StepSeconds);
                        _physics.Simulate(PhysicsShot.StepSeconds);
                        effects.AfterPhysicsStep(PhysicsShot.StepSeconds);
                    }
                    round.Tick(Frame);
                    view.Show(true);
                    if (!shot && round.Phase == RoundPhase.ShootingWindow && round.PhaseSecondsElapsed > shootAfter)
                    {
                        NetFaceGaps gaps = round.GapsNow();
                        var cells = new List<int>();
                        RinkPoint start = Breakaway.ShotStart(round.AttackSide, round.PhaseSecondsElapsed, settings);
                        for (int cell = 0; cell < NetFaceGaps.Cells; cell++)
                        {
                            bool isGap = gaps.IsGap(cell % NetFaceGaps.Columns, cell / NetFaceGaps.Columns);
                            if (aim == Aim.Gap ? isGap : aim == Aim.NotGap ? !isGap : WasInTheWay(port, round, start, cell)) cells.Add(cell);
                        }
                        if (cells.Count == 0) return sample; // nothing to aim at this time: not played
                        int pick = cells[rng.Next(cells.Count)];
                        NetFaceGaps.CellCentre(pick % NetFaceGaps.Columns, pick / NetFaceGaps.Columns, out double x, out double y);
                        sample.Side = round.AttackSide;
                        sample.GapCount = gaps.Count;
                        sample.SetUpKind = round.SetUp.Kind;
                        round.SubmitShot(x, y, speedKmh, false);
                        shot = true;
                    }
                    effects.Update(round, Frame);
                    breakaway.Step(Frame * Time.timeScale, Frame);
                    if (shot && !round.AwaitingWhistle && round.LastResult.HasValue)
                    {
                        sample.Kind = round.LastResult.Value.Kind;
                        sample.Played = true;
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

        static bool WasInTheWay(GoalieCharacterReach port, Round round, RinkPoint start, int cell)
        {
            NetFaceGaps.CellCentre(cell % NetFaceGaps.Columns, cell / NetFaceGaps.Columns, out double x, out double y);
            return port.Reach(round.SetUp, round.AttackSide, round.PhaseSecondsElapsed, start, RinkPoint.OnNetFace(x, y)).InTheWay;
        }

        sealed class Rates
        {
            public int Shots, Goals, Saves, GapCells;
            public readonly int[] SideShots = new int[3], SideGoals = new int[3];
            public double Goal => Shots == 0 ? 0 : (double)Goals / Shots;
            public double Save => Shots == 0 ? 0 : (double)Saves / Shots;

            public void Add(Sample s)
            {
                Shots++;
                GapCells += s.GapCount;
                SideShots[(int)s.Side]++;
                if (s.Kind == ShotResultKind.Goal) { Goals++; SideGoals[(int)s.Side]++; }
                else if (s.Kind == ShotResultKind.Save) Saves++;
            }

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.Append(Shots + " Shots: " + Goals + " Goals, " + Saves + " Saves; Goal rate " + Goal.ToString("P0") + ", Save rate " + Save.ToString("P0")
                    + "; Gap cells shown on average " + (Shots == 0 ? 0 : (double)GapCells / Shots).ToString("F1") + "; Goals by Attack Side:");
                for (int i = 0; i < 3; i++) sb.Append(" " + (AttackSide)i + " " + SideGoals[i] + "/" + SideShots[i]);
                return sb.ToString();
            }
        }

        /// <summary>Seeds 1 to 90, or (second set) 1001 to 1090.</summary>
        Rates Measure(Aim aim, int firstSeed, string name)
        {
            var rates = new Rates();
            for (int seed = firstSeed; seed < firstSeed + Seeds; seed++)
            {
                Sample s = Play(seed, aim);
                if (s.Played) rates.Add(s);
            }
            // The numbers are kept beside the notes when there is a notes folder (the main checkout has none).
            string notes = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../_notes"));
            if (System.IO.Directory.Exists(notes))
                System.IO.File.AppendAllText(System.IO.Path.Combine(notes, "t134-rates.txt"), name + " (seeds from " + firstSeed + "): " + rates + "\n");
            return rates;
        }

        /// <summary>
        /// The scoring measure (#191, spec #190): the same seeded Rounds at one speed, once aimed at the centre of a shown Gap (whatever
        /// Set-up the seed brings: Clean, Cheat, Depth, Late Settle) and once at a cell with goalie gear in the way (the goalie set).
        /// One table line per set; it asserts only that Shots were played and the counts add up. Same seeds every run, so the same counts.
        /// </summary>
        [Test, Timeout(1200000), Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void Scoring_measure_goal_rate_at_one_speed([Values(30, 40, 50)] int speedKmh)
        {
            foreach (Aim aim in new[] { Aim.Gap, Aim.GearInTheWay })
            {
                var rates = new Rates();
                var kindShots = new int[4];
                var kindGoals = new int[4];
                for (int seed = 1; seed < 1 + Seeds; seed++)
                {
                    Sample s = Play(seed, aim, speedKmh);
                    if (!s.Played) continue;
                    rates.Add(s);
                    kindShots[(int)s.SetUpKind]++;
                    if (s.Kind == ShotResultKind.Goal) kindGoals[(int)s.SetUpKind]++;
                }
                var line = new StringBuilder("SCORING MEASURE " + speedKmh + " km/h, " + (aim == Aim.Gap ? "Gap set" : "goalie set") + ": Goals/Shots "
                    + rates.Goals + "/" + rates.Shots + " (" + rates.Goal.ToString("P0") + "), Saves " + rates.Saves + "; by Set-up:");
                int sumShots = 0;
                for (int k = 0; k < kindShots.Length; k++)
                {
                    line.Append(" " + (SetUpKind)k + " " + kindGoals[k] + "/" + kindShots[k]);
                    sumShots += kindShots[k];
                }
                Debug.Log(line.ToString());
                TestContext.Progress.WriteLine(line.ToString());
                string notes = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../_notes"));
                if (System.IO.Directory.Exists(notes))
                    System.IO.File.AppendAllText(System.IO.Path.Combine(notes, "t191-measure.txt"), line + "\n");
                Assert.GreaterOrEqual(rates.Shots, 60, "enough Shots were played: " + line);
                Assert.AreEqual(rates.Shots, sumShots, "the Set-up counts add up: " + line);
                Assert.LessOrEqual(rates.Goals + rates.Saves, rates.Shots, "Goals and Saves add up: " + line);
            }
        }

        [Test, Timeout(600000), Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void A_Shot_into_the_centre_of_a_shown_Gap_is_a_Goal_most_of_the_time([Values(1, 1001)] int firstSeed)
        {
            Rates rates = Measure(Aim.Gap, firstSeed, "into a shown Gap");
            Assert.GreaterOrEqual(rates.Shots, 60, "enough Shots were played: " + rates);
            Assert.GreaterOrEqual(rates.Goal, GapGoalBar, "Goal rate into a shown Gap (bar " + GapGoalBar.ToString("P0") + "): " + rates);
        }

        [Test, Timeout(600000), Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void A_Shot_into_a_cell_that_is_not_a_Gap_is_still_saved_most_of_the_time()
        {
            Rates rates = Measure(Aim.NotGap, 1, "into a cell that is not a Gap");
            Assert.GreaterOrEqual(rates.Shots, 60, "enough Shots were played: " + rates);
            Assert.GreaterOrEqual(rates.Save, NotGapSaveBar, "Save rate away from the Gaps (bar " + NotGapSaveBar.ToString("P0") + "): " + rates);
        }

        [Test, Timeout(600000), Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void A_Shot_into_a_cell_the_race_finds_gear_in_the_way_of_is_still_saved_most_of_the_time()
        {
            // The cells that were not Gaps before #134 (physics and the race are unchanged, so this is the old "away from the Gaps" rate).
            Rates rates = Measure(Aim.GearInTheWay, 1, "into a cell the race finds gear in the way of");
            Assert.GreaterOrEqual(rates.Shots, 60, "enough Shots were played: " + rates);
            Assert.GreaterOrEqual(rates.Save, NotGapSaveBar, "Save rate into gear in the way (bar " + NotGapSaveBar.ToString("P0") + "): " + rates);
        }
    }
}
