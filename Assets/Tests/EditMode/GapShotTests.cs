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
        const int Seeds = 90;

        /// <summary>The bar (#134): a Shot into the centre of a shown Gap is a Goal at least this often. Measured 83 % after the fix, 59 % before.</summary>
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
        }

        /// <summary>Plays the Round of this seed to a seeded moment in its window and shoots at the centre of a Gap (intoGap) or of a cell that is not one.</summary>
        Sample Play(int seed, bool intoGap)
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
                var round = new Round(new SeededRandom(seed), settings, new GoalieCharacterReach(character, settings), 0);
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
                        for (int cell = 0; cell < NetFaceGaps.Cells; cell++)
                            if (gaps.IsGap(cell % NetFaceGaps.Columns, cell / NetFaceGaps.Columns) == intoGap) cells.Add(cell);
                        if (cells.Count == 0) return sample; // nothing to aim at this time: not played
                        int pick = cells[rng.Next(cells.Count)];
                        NetFaceGaps.CellCentre(pick % NetFaceGaps.Columns, pick / NetFaceGaps.Columns, out double x, out double y);
                        sample.Side = round.AttackSide;
                        sample.GapCount = gaps.Count;
                        round.SubmitShot(x, y, 100, false);
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

        Rates Measure(bool intoGap)
        {
            var rates = new Rates();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                Sample s = Play(seed, intoGap);
                if (s.Played) rates.Add(s);
            }
            return rates;
        }

        [Test, Timeout(600000), Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void A_Shot_into_the_centre_of_a_shown_Gap_is_a_Goal_most_of_the_time()
        {
            Rates rates = Measure(true);
            Assert.GreaterOrEqual(rates.Shots, 60, "enough Shots were played: " + rates);
            Assert.GreaterOrEqual(rates.Goal, GapGoalBar, "Goal rate into a shown Gap (bar " + GapGoalBar.ToString("P0") + "): " + rates);
        }

        [Test, Timeout(600000), Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void A_Shot_into_a_cell_that_is_not_a_Gap_is_still_saved_most_of_the_time()
        {
            Rates rates = Measure(false);
            Assert.GreaterOrEqual(rates.Shots, 60, "enough Shots were played: " + rates);
            Assert.GreaterOrEqual(rates.Save, NotGapSaveBar, "Save rate away from the Gaps (bar " + NotGapSaveBar.ToString("P0") + "): " + rates);
        }
    }
}
