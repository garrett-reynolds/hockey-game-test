using System;
using System.Collections.Generic;
using HockeyGame;
using HockeyGame.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// Before the Shot (#70): the head follows the puck (the shoulders taking 15 % of the turn); on the skate-in he waits until
    /// 0.08 m off the angle, then shuffles over in 0.3 s; once set his Stance Life drops 85 % and he sits 3 cm lower. Each
    /// piece has its own RoundSettings switch, on by default, and none of them moves his spot at the Shot or changes the race.
    /// </summary>
    public class BeforeTheShotTests
    {
        const double Dt = 1.0 / 60.0;
        const float Frame = 1f / 60f;
        GameObject _parent;
        SimulationMode _oldMode;

        [SetUp]
        public void SetUp()
        {
            _oldMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            _parent = new GameObject("Rink (test)");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GoalieView v in _views) v?.Dispose();
            _views.Clear();
            Object.DestroyImmediate(_parent);
            Physics.simulationMode = _oldMode;
            Time.timeScale = 1f;
        }

        readonly List<GoalieView> _views = new List<GoalieView>(); // every view a test built outside a try/finally, disposed in TearDown

        static RoundSettings AllOff() => new RoundSettings { HeadOnPuck = false, SkateInShuffles = false, StillOnceSet = false };

        // ---- The settings ---------------------------------------------------------------------------------------------------

        [Test]
        public void Each_piece_is_on_by_default_with_the_tickets_numbers()
        {
            var s = new RoundSettings();
            Assert.IsTrue(s.HeadOnPuck && s.SkateInShuffles && s.StillOnceSet);
            Assert.AreEqual(0.15, s.HeadShoulderShare);
            Assert.AreEqual(0.08, s.ShuffleStartMetres);
            Assert.AreEqual(0.3, s.ShuffleSeconds);
            Assert.AreEqual(0.85, s.SetStillness);
            Assert.AreEqual(0.03, s.SetDropMetres);
        }

        [Test]
        public void Once_set_his_Stance_Life_drops_85_percent_and_he_sits_3_cm_lower_handing_over_to_the_reaction()
        {
            var s = new RoundSettings();
            Assert.AreEqual(0.15, BeforeTheShot.LifeShare(1, 0, s), 1e-12);
            Assert.AreEqual(0.03, BeforeTheShot.DropMetres(1, 0, s), 1e-12);
            Assert.AreEqual(1, BeforeTheShot.LifeShare(0, 0, s), 1e-12, "skating in: all of it");
            Assert.AreEqual(0, BeforeTheShot.DropMetres(0, 0, s), 1e-12);
            Assert.AreEqual(1, BeforeTheShot.LifeShare(1, 1, s), 1e-12, "reacted: the reaction's own Stance Life rule");
            Assert.AreEqual(0, BeforeTheShot.DropMetres(1, 1, s), 1e-12);
            Assert.AreEqual(0, BeforeTheShot.Settled(RoundPhase.SkateIn, 2, s));
            Assert.AreEqual(1, BeforeTheShot.Settled(RoundPhase.SlowDown, s.SlowDownSeconds, s), 1e-12, "set as the window opens");
            Assert.AreEqual(1, BeforeTheShot.Settled(RoundPhase.ShootingWindow, 1, s));
            Assert.AreEqual(0, BeforeTheShot.Settled(RoundPhase.ResultHold, s.ResultHoldSeconds, s), 1e-12, "back to life by the next skate-in");
        }

        [Test]
        public void The_head_finds_the_puck_on_the_skate_in_and_lets_it_go_to_the_reaction()
        {
            var s = new RoundSettings();
            Assert.AreEqual(0, BeforeTheShot.Watching(RoundPhase.SkateIn, 0, s));
            Assert.AreEqual(1, BeforeTheShot.Watching(RoundPhase.SkateIn, BeforeTheShot.FindPuckSeconds, s), 1e-12);
            Assert.AreEqual(1, BeforeTheShot.Watching(RoundPhase.ShootingWindow, 2, s));
            Assert.AreEqual(0, BeforeTheShot.Watching(RoundPhase.Idle, 0, s));
            Assert.AreEqual(1, BeforeTheShot.HeadWeight(1, 0, s), 1e-12);
            Assert.AreEqual(0, BeforeTheShot.HeadWeight(1, 1, s), 1e-12, "reacted: the reaction has the head");
        }

        [Test]
        public void Each_setting_turns_its_piece_off()
        {
            var s = AllOff();
            Assert.AreEqual(0, BeforeTheShot.HeadWeight(1, 0, s), "head");
            Assert.AreEqual(1, BeforeTheShot.LifeShare(1, 0, s), "stillness");
            Assert.AreEqual(0, BeforeTheShot.DropMetres(1, 0, s), "lower");
            // Shuffles off: BreakawayView steps with shuffles false, and he moves with the angle at once.
            var steps = new SkateInShuffles(new RinkPoint(0, 0, -0.6));
            for (int f = 1; f <= 60; f++)
            {
                var angle = new RinkPoint(0.2 * f * Dt, 0, -0.6);
                Assert.AreEqual(angle.X, steps.Step(angle, Dt, false, s).X, 1e-12, "frame " + f);
            }
        }

        // ---- The skate-in shuffles ------------------------------------------------------------------------------------------

        [Test]
        public void On_the_skate_in_he_waits_until_8_cm_off_the_angle_then_shuffles_over_in_0_3_s()
        {
            var s = new RoundSettings();
            var steps = new SkateInShuffles(new RinkPoint(0, 0, -0.6));
            const double speed = 0.2; // the angle sliding across (m/s)
            double x = 0, gapAtStart = 0;
            int start = -1, end = -1;
            for (int f = 1; f <= 120 && end < 0; f++)
            {
                double angleX = speed * f * Dt, before = x;
                RinkPoint shown = steps.Step(new RinkPoint(angleX, 0, -0.6), Dt, true, s);
                x = shown.X;
                Assert.AreEqual(-0.6, shown.Z, 1e-12, "at the angle's depth");
                if (start < 0)
                {
                    if (x != before) { start = f; gapAtStart = angleX - before; }
                    else Assert.LessOrEqual(angleX - x, s.ShuffleStartMetres + 1e-12, "waits while the angle is within 8 cm");
                }
                else if (Math.Abs(x - angleX) < 1e-12) end = f;
                else Assert.LessOrEqual(Math.Abs(angleX - x), s.ShuffleStartMetres + speed * Dt + 1e-9, "never further off than he waited");
            }
            Assert.Greater(start, 0, "he shuffles");
            Assert.Greater(gapAtStart, s.ShuffleStartMetres, "only once 8 cm off");
            Assert.LessOrEqual(gapAtStart, s.ShuffleStartMetres + speed * Dt + 1e-12);
            Assert.Greater(end, 0, "he gets there");
            double seconds = (end - start + 1) * Dt;
            Assert.That(seconds, Is.InRange(s.ShuffleSeconds - 1e-9, s.ShuffleSeconds + Dt), "over in 0.3 s");
        }

        [Test]
        public void Off_the_skate_in_he_finishes_his_shuffle_without_waiting_then_moves_with_the_angle()
        {
            var s = new RoundSettings();
            var steps = new SkateInShuffles(new RinkPoint(0, 0, -0.6));
            var near = new RinkPoint(0.05, 0, -0.6); // within 8 cm: on the skate-in he would wait
            Assert.AreEqual(0, steps.Step(near, Dt, true, s).X, 1e-12);
            double first = steps.Step(near, Dt, false, s).X;
            Assert.That(first, Is.GreaterThan(0).And.LessThan(0.05), "the slow-down: he shuffles over at once, no jump");
            int f = 1;
            while (Math.Abs(steps.Step(near, Dt, false, s).X - 0.05) > 1e-12 && f < 60) f++;
            Assert.LessOrEqual((f + 1) * Dt, s.ShuffleSeconds + Dt, "over in 0.3 s");
            for (int g = 1; g <= 30; g++)
            {
                var angle = new RinkPoint(0.05 + 0.3 * g * Dt, 0, -0.65);
                RinkPoint shown = steps.Step(angle, Dt, false, s);
                Assert.AreEqual(angle.X, shown.X, 1e-12, "then with the angle");
                Assert.AreEqual(angle.Z, shown.Z, 1e-12);
            }
        }

        // ---- On the goalie character ----------------------------------------------------------------------------------------

        sealed class Posed
        {
            public Vector3 Hips, LeftFoot, RightFoot;
            public Quaternion Head, Spine;
            public float BladeBottom;
        }

        Posed Pose(GoalieView view, GoalieCharacter character, float life, Vector3 puck, float watching, float settled, RoundSettings settings)
        {
            view.Live(life);
            view.ShowBeforeTheShot(puck, watching, settled, settings);
            view.SetProgress(0f);
            character.GearBounds(GearPiece.Stick, out Bounds stick);
            return new Posed
            {
                Hips = character.Bone(HumanBodyBones.Hips).position,
                LeftFoot = character.Bone(HumanBodyBones.LeftFoot).position,
                RightFoot = character.Bone(HumanBodyBones.RightFoot).position,
                Head = character.Bone(HumanBodyBones.Head).rotation,
                Spine = character.Bone(HumanBodyBones.Spine).rotation,
                BladeBottom = stick.min.y,
            };
        }

        GoalieView ReadyView(out GoalieCharacter character)
        {
            character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "needs the goalie model");
            var view = new GoalieView(_parent.transform, character);
            _views.Add(view);
            view.Show(true);
            view.Stand(new Vector3(0f, 0f, -0.6f));
            view.Face(0f, 0f);
            return view;
        }

        [Test]
        public void His_head_follows_the_puck_the_shoulders_taking_15_percent_and_the_setting_turns_it_off()
        {
            GoalieView view = ReadyView(out GoalieCharacter character);
            var puck = new Vector3(2.4f, 1.0f, -8f); // a right attack: off to his left
            Posed none = Pose(view, character, 0f, puck, 1f, 0f, null);
            Posed on = Pose(view, character, 0f, puck, 1f, 0f, new RoundSettings());
            Posed off = Pose(view, character, 0f, puck, 1f, 0f, new RoundSettings { HeadOnPuck = false });

            float head = Quaternion.Angle(none.Head, on.Head), shoulders = Quaternion.Angle(none.Spine, on.Spine);
            Assert.Greater(head, 10f, "the head turns to the puck");
            Assert.That(shoulders / head, Is.InRange(0.1f, 0.2f), "the shoulders take about 15 % of it");
            Assert.Less(Quaternion.Angle(none.Head, off.Head), 0.01f, "off: the head stays square");
            Assert.Less(Quaternion.Angle(none.Spine, off.Spine), 0.01f);
        }

        [Test]
        public void Once_set_he_sits_3_cm_lower_skates_and_blade_kept_on_the_ice_and_the_setting_turns_it_off()
        {
            GoalieView view = ReadyView(out GoalieCharacter character);
            var puck = new Vector3(0f, 1f, -8f);
            Posed skating = Pose(view, character, 0f, puck, 0f, 0f, new RoundSettings());
            Posed set = Pose(view, character, 0f, puck, 0f, 1f, new RoundSettings());
            Posed off = Pose(view, character, 0f, puck, 0f, 1f, new RoundSettings { StillOnceSet = false });

            Assert.AreEqual(0.03f, skating.Hips.y - set.Hips.y, 0.002f, "3 cm lower");
            Assert.Less(Vector3.Distance(skating.LeftFoot, set.LeftFoot), 0.002f, "skates stay on the ice");
            Assert.Less(Vector3.Distance(skating.RightFoot, set.RightFoot), 0.002f);
            Assert.AreEqual(skating.BladeBottom, set.BladeBottom, 0.004f, "stick blade stays on the ice");
            Assert.AreEqual(skating.Hips.y, off.Hips.y, 1e-5f, "off: his usual height");
        }

        [Test]
        public void Once_set_his_Stance_Life_drops_85_percent_and_the_setting_turns_it_off()
        {
            GoalieView view = ReadyView(out GoalieCharacter character);
            var puck = new Vector3(0f, 1f, -8f);
            const float alive = 1.7f; // breathed in, weight well over one skate
            float Shift(RoundSettings s, float settled)
            {
                Vector3 still = Pose(view, character, 0f, puck, 0f, settled, s).Hips; // life clock 0: the plain Ready Stance
                Vector3 moved = Pose(view, character, alive, puck, 0f, settled, s).Hips;
                return new Vector2(moved.x - still.x, moved.z - still.z).magnitude;
            }
            float full = Shift(new RoundSettings(), 0f);
            Assume.That(full, Is.GreaterThan(0.005f), "Stance Life moves his hips across at this moment");
            Assert.AreEqual(0.15f, Shift(new RoundSettings(), 1f) / full, 0.02f, "set: 15 % of it left");
            Assert.AreEqual(full, Shift(new RoundSettings { StillOnceSet = false }, 1f), 1e-5f, "off: all of it");
        }

        // ---- Through a whole Round: his spot at the Shot and the race -----------------------------------------------------

        sealed class Played
        {
            public readonly List<string> Shots = new List<string>(); // per Shot: Set-up, raced spot, reach and result
            public readonly List<string> Failures = new List<string>();
            public double WorstOffAngle, StillFrames;
            public float WorstSettleStep; // the most he moves in one frame through the slow-down and onto his spot as the window opens
        }

        static double OffTheLine(Vector3 spot, RinkPoint puck) =>
            Math.Abs(puck.X * spot.z - puck.Z * spot.x) / Math.Sqrt(puck.X * puck.X + puck.Z * puck.Z);

        // To the micrometre and microsecond: the Shot frames can fall a hair apart with the pieces on and off (most likely the Whistle: the physics
        // puck's comes at its own moment, so the hold ends, and the next Shot starts, a hair apart).
        // #91's longer Replay hold made that show (a Shot start 1e-8 m apart). The race itself is the same.
        static string R(RinkPoint p) => "(" + p.X.ToString("F6") + ", " + p.Y.ToString("F6") + ", " + p.Z.ToString("F6") + ")";

        Played PlayRound(RoundSettings settings)
        {
            var played = new Played();
            var rink = new GameObject("Rink (round)");
            rink.transform.SetParent(_parent.transform);
            NetBuilder.Build(rink.transform);
            var cameraObject = new GameObject("Camera (test)", typeof(Camera));
            var driver = new GameObject("Breakaway (test)");
            PhysicsShotEffects effects = null;
            GoalieView view = null;
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                var character = new GoalieCharacter(rink.transform);
                Assume.That(character.Present, "needs the goalie model");
                view = new GoalieView(rink.transform, character);
                effects = new PhysicsShotEffects(rink.transform, camera, new SeededRandom(7), view, name => null);
                Physics.SyncTransforms();
                // Every Set-up type, Cheat and Late Settle on both posts' sides, on the real race.
                var round = new Round(ScriptedRandom.Dealing(
                    new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right, AttackSide.Left, AttackSide.Right, AttackSide.Middle },
                    new[] { HockeyGame.Core.SetUp.Clean, HockeyGame.Core.SetUp.Cheat(Post.Left), HockeyGame.Core.SetUp.Depth,
                        HockeyGame.Core.SetUp.LateSettle(Post.Right), HockeyGame.Core.SetUp.Cheat(Post.Right), HockeyGame.Core.SetUp.Clean }),
                    settings, new GoalieCharacterReach(character, settings), 0);
                var breakaway = driver.AddComponent<BreakawayView>();
                breakaway.Init(round, camera, view, () => effects.FlightSeconds);
                var aims = new[] { (88.0, 82.0), (50.0, 90.0), (15.0, 12.0), (80.0, 30.0), (12.0, 82.0), (50.0, 15.0) };
                int shot = 0;
                Vector3 lastSpot = view.ShownSpot;
                RoundPhase last = RoundPhase.Idle;
                round.StartRound();
                for (int frame = 0; frame < 60 * 80 && round.Phase != RoundPhase.RoundOver; frame++)
                {
                    for (int step = 0; step < 2; step++) // a frame is 1/60 s: two physics steps of 1/120 s
                    {
                        Physics.Simulate(PhysicsShot.StepSeconds);
                        effects.AfterPhysicsStep(PhysicsShot.StepSeconds);
                    }
                    round.Tick(Frame);
                    view.Show(true);
                    // Shots at 0.3 .. 0.8 s into the window: a Late Settle is still closing.
                    bool shoot = round.Phase == RoundPhase.ShootingWindow && round.PhaseSecondsElapsed > 0.3 + 0.1 * shot && shot < aims.Length;
                    if (shoot) round.SubmitShot(aims[shot].Item1, aims[shot].Item2, 100, false);
                    effects.Update(round, Frame);
                    if (shoot)
                    {
                        shot++;
                        ShotRace race = round.LastRace.Value;
                        played.Shots.Add(race.SetUp + " " + race.AttackSide + ": spot " + R(race.Spot) + ", start " + R(race.ShotStart)
                            + ", reach " + race.ReachSeconds.ToString("F6") + " (move " + race.MoveSeconds.ToString("F6") + ", " + race.SaveReaction
                            + (race.InTheWay ? ", in the way" : "") + "), flight " + race.FlightSeconds.ToString("F6") + ": " + ScriptedShot.Result(round).Kind);
                        Vector3 spot = view.ShownSpot;
                        if (Math.Abs(spot.x - race.Spot.X) > 1e-5 || Math.Abs(spot.z - race.Spot.Z) > 1e-5)
                            played.Failures.Add("Shot " + shot + ": reacts from " + spot.ToString("F4") + ", race " + race.Spot);
                    }
                    breakaway.Step(Frame * Time.timeScale, Frame);

                    RoundPhase phase = round.Phase;
                    double seconds = round.PhaseSecondsElapsed;
                    if (phase == RoundPhase.SkateIn && seconds >= 0.5 * settings.SkateInSeconds)
                    {
                        RinkPoint puck = Breakaway.Puck(round.AttackSide, phase, seconds, settings);
                        played.WorstOffAngle = Math.Max(played.WorstOffAngle, OffTheLine(view.ShownSpot, puck));
                        if (view.ShownSpot == lastSpot) played.StillFrames++;
                    }
                    if (phase == RoundPhase.SlowDown || phase == RoundPhase.ShootingWindow && last != RoundPhase.ShootingWindow)
                        played.WorstSettleStep = Mathf.Max(played.WorstSettleStep, Vector3.Distance(view.ShownSpot, lastSpot));
                    if (phase == RoundPhase.ShootingWindow && last != RoundPhase.ShootingWindow)
                    {
                        RinkPoint spot = SetUps.Spot(round.SetUp, round.AttackSide, seconds, Breakaway.ShotStart(round.AttackSide, seconds, settings), settings);
                        if (Math.Abs(view.ShownSpot.x - spot.X) > 1e-5 || Math.Abs(view.ShownSpot.z - spot.Z) > 1e-5)
                            played.Failures.Add(round.SetUp + ": at the window " + view.ShownSpot.ToString("F4") + ", Set-up spot " + spot);
                    }
                    lastSpot = view.ShownSpot;
                    last = phase;
                }
                Assert.AreEqual(aims.Length, shot, "six Shots");
            }
            finally
            {
                effects?.Dispose();
                view?.Dispose();
                Object.DestroyImmediate(driver);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(rink);
                Time.timeScale = 1f;
            }
            return played;
        }

        [Test]
        public void His_spot_at_the_Shot_and_the_race_are_the_same_with_the_pieces_on_and_off_for_each_Set_up()
        {
            Played on = PlayRound(new RoundSettings());
            Played off = PlayRound(AllOff());
            foreach (string line in on.Shots) TestContext.Out.WriteLine(line);
            TestContext.Out.WriteLine("shuffles on: worst off the angle " + on.WorstOffAngle.ToString("F3") + " m, " + on.StillFrames
                + " still frames; off: " + off.WorstOffAngle.ToString("F4") + " m, " + off.StillFrames + " still frames; worst settle step "
                + on.WorstSettleStep.ToString("F4") + " m on, " + off.WorstSettleStep.ToString("F4") + " m off");

            Assert.IsEmpty(on.Failures, string.Join("\n", on.Failures));
            Assert.IsEmpty(off.Failures, string.Join("\n", off.Failures));
            CollectionAssert.AreEqual(off.Shots, on.Shots, "the same spot, reach and result on each Shot");
            // The shuffles show on the skate-in, never further off the angle than he waits (plus the angle's slide in a frame).
            Assert.Less(on.WorstOffAngle, 0.09, "within 8 cm of the angle");
            Assert.Greater(on.StillFrames, off.StillFrames + 30, "he waits between shuffles");
            // The slow-down finishes a shuffle smoothly: no jump onto his spot as the window opens.
            Assert.LessOrEqual(on.WorstSettleStep, off.WorstSettleStep + 0.001f, "no bigger step than gliding (" + on.WorstSettleStep + " vs " + off.WorstSettleStep + " m)");
        }
    }
}
