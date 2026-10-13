using System;
using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The game path (#109): the Round, PhysicsShotEffects and Unity physics, one physics step of 1/120 s per frame, in the
    /// game's order (physics step, then PhysicsShotEffects.AfterPhysicsStep, then the frame: Round.Tick and Update). The
    /// Round's Whistle comes from the physics puck (WhistleRule), not the race. These Shots have no goalie (null), so no
    /// gear to touch; gear touches are in GearTouchTests.
    /// </summary>
    public class PhysicsShotEffectsTests
    {
        const float Step = PhysicsShot.StepSeconds;

        SimulationMode _oldMode;
        GameObject _root;
        Camera _camera;
        PhysicsShotEffects _effects;

        [SetUp]
        public void SetUp()
        {
            _oldMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            _root = new GameObject("Rink (test)");
            _camera = new GameObject("Shooter Camera (test)", typeof(Camera)).GetComponent<Camera>();
            _camera.transform.SetParent(_root.transform, false);
            _camera.aspect = 16f / 9f; // batch mode's screen size is not to be trusted
            PlaceEye();
        }

        [TearDown]
        public void TearDown()
        {
            _effects?.Dispose();
            _effects = null;
            Object.DestroyImmediate(_root);
            Physics.simulationMode = _oldMode;
        }

        /// <summary>The camera where the Round has the shooter's eye on a middle attack, a second into the shooting window.</summary>
        void PlaceEye()
        {
            var settings = new RoundSettings();
            Breakaway.SkatePoint(AttackSide.Middle, 1.0, out double x, out double distance);
            var ground = new Vector3((float)x, 0f, (float)-distance);
            Vector3 toNet = -ground;
            ground += toNet.normalized * (float)Breakaway.CappedDrift(Breakaway.Drift(AttackSide.Middle, settings.SlowDownSeconds, 1.0, settings), toNet.magnitude);
            _camera.transform.position = new Vector3(ground.x, (float)Breakaway.EyeHeight, ground.z);
            _camera.transform.LookAt(new Vector3(0f, (float)(NetFrame.FaceHeightMetres / 2.0), 0f));
        }

        PhysicsShotEffects Effects(GoalieView goalie = null)
        {
            _effects = new PhysicsShotEffects(_root.transform, _camera, new SeededRandom(7), goalie, name => null);
            Physics.SyncTransforms();
            return _effects;
        }

        /// <summary>A Round in the shooting window on a middle attack; reach decides the race (it moves the goalie only).</summary>
        static Round InWindow(IGoalieReach reach)
        {
            var round = new Round(ScriptedRandom.Forcing(AttackSide.Middle), new RoundSettings(), reach, 0);
            round.StartRound();
            round.Tick(3.7 + 1.0);
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
            return round;
        }

        /// <summary>Submits a Shot and starts its puck on the Shot frame, as RoundDriver does.</summary>
        static void Shoot(Round round, PhysicsShotEffects effects, double x, double y, double kmh)
        {
            round.SubmitShot(x, y, kmh, false);
            effects.Update(round, 0f);
        }

        /// <summary>One frame of the game: a physics step, then the frame (the Round ticks, then the effects update).</summary>
        static void Frame(Round round, PhysicsShotEffects effects)
        {
            Physics.Simulate(Step);
            effects.AfterPhysicsStep(Step);
            round.Tick(Step);
            effects.Update(round, Step);
        }

        /// <summary>Plays frames until the Round has its Whistle (or seconds pass). Returns the frames played.</summary>
        static int UntilWhistle(Round round, PhysicsShotEffects effects, double seconds = 4.0)
        {
            int frames = 0;
            while (round.AwaitingWhistle && frames < seconds / Step)
            {
                Frame(round, effects);
                frames++;
            }
            return frames;
        }

        // ---- The Whistle comes from the physics Shot ----

        [Test]
        public void The_Round_s_Whistle_comes_from_the_physics_puck_not_the_race()
        {
            var effects = Effects();
            var round = InWindow(FakeReach.InTheWay());
            Shoot(round, effects, 50, 50, 100);
            Assert.IsTrue(round.LastRace.Value.IsSave, "the race saves it (gear in the way)");
            Assert.AreEqual(ShotResultKind.Save, ScriptedShot.Result(round).Kind);

            UntilWhistle(round, effects);
            Assert.IsFalse(round.AwaitingWhistle, "the Whistle came");
            Assert.AreEqual(ShotResultKind.Goal, round.LastResult.Value.Kind, "the puck went in: the goalie has no hit shapes yet (#111)");
            Assert.AreEqual(effects.Shot.Result, round.LastResult.Value.Kind);
            Assert.IsTrue(effects.Shot.LastFacts.WholePuckInNet);
            // The Round ticks in the frame, after the frame's physics step: its clock is a step behind the Shot's at the Whistle.
            Assert.AreEqual(effects.Shot.WhistleSeconds - Step, round.ResultDelaySeconds, Step / 2, "the Round's Shot-to-Whistle is the physics Shot's");
            Assert.AreEqual(effects.FlightSeconds, effects.Shot.WhistleSeconds, 0.05, "a Goal's Whistle: about the Flight Time");
        }

        [Test]
        public void A_Shot_off_the_Net_Face_flies_and_is_a_Miss_at_its_Whistle()
        {
            var effects = Effects();
            var round = InWindow(FakeReach.NeverReaches());
            Shoot(round, effects, 130, 50, 100);
            Assert.IsTrue(effects.Shot.Launched, "launched like any Shot");
            Assert.IsFalse(round.ResultShowing);
            UntilWhistle(round, effects);
            Assert.IsFalse(round.AwaitingWhistle);
            Assert.AreEqual(ShotResultKind.Miss, round.LastResult.Value.Kind);
            Assert.AreEqual(round.LastResult.Value.Kind, effects.Shot.Result);
        }

        [Test]
        public void A_Test_Shot_launches_the_same_and_gets_its_Whistle_the_same()
        {
            var effects = Effects();
            var round = InWindow(FakeReach.NeverReaches());
            round.SubmitShot(30, 40, 80, true);
            effects.Update(round, 0f);
            Assert.IsTrue(effects.Shot.Launched);
            UntilWhistle(round, effects);
            Assert.AreEqual(ShotResultKind.Goal, round.LastResult.Value.Kind);
            Assert.IsTrue(round.LastResult.Value.IsTestShot);
        }

        // ---- Effects at the Whistle ----

        /// <summary>A screen flow in the shooting window on a middle attack (the eye PlaceEye puts the camera at).</summary>
        static ScreenFlow FlowInWindow()
        {
            var flow = new ScreenFlow(ScriptedRandom.Forcing(AttackSide.Middle), new SaveData(), FakeReach.NeverReaches());
            flow.Press();
            flow.Tick(3.7 + 1.0);
            Assert.AreEqual(RoundPhase.ShootingWindow, flow.Round.Phase);
            return flow;
        }

        // The goal light has one driver, the Arena Show's Goal Show (RoundDriver sets it each frame): it comes on at a GOAL!'s
        // Whistle (the line crossing, from the physics puck) and goes off when the hold ends. PhysicsShotEffects only builds it.
        [Test]
        public void The_goal_light_comes_on_at_the_line_crossing_and_goes_off_after_the_hold()
        {
            var effects = Effects();
            ScreenFlow flow = FlowInWindow();
            Round round = flow.Round;
            Shoot(round, effects, 70, 60, 120);
            while (round.AwaitingWhistle && effects.Shot.SecondsSinceShot < 4.0)
            {
                Assert.IsFalse(ArenaShow.From(flow).GoalShow.GoalLight, "off before the crossing, at " + effects.Shot.SecondsSinceShot + " s");
                Frame(round, effects);
            }
            Assert.AreEqual(ShotResultKind.Goal, round.LastResult.Value.Kind);
            Assert.IsTrue(effects.Shot.LastFacts.WholePuckInNet, "the Whistle's step: the whole puck is past the goal line");
            Assert.IsTrue(ArenaShow.From(flow).GoalShow.GoalLight, "on from the Whistle's frame");
            Assert.IsTrue(round.ResultShowing, "GOAL! shows from the Whistle");

            while (round.Phase == RoundPhase.ResultHold) Frame(round, effects);
            Assert.IsFalse(ArenaShow.From(flow).GoalShow.GoalLight, "off when the hold ends");
        }

        [Test]
        public void A_post_hit_rings_from_its_physics_contact()
        {
            var effects = Effects();
            var round = InWindow(FakeReach.NeverReaches());
            float postCentre = -(NetBuilder.Post / 2f) / (float)NetFaceMapping.WidthMetres * 100f; // Net Face units
            Shoot(round, effects, postCentre, 50, 100);
            bool rang = false;
            for (int i = 0; i < 2.0 / Step && !rang; i++)
            {
                Frame(round, effects);
                if (effects.HitSound == SoundCue.PostPing)
                {
                    rang = true;
                    Assert.IsTrue(effects.Shot.Touched);
                    StringAssert.Contains("Post", effects.Shot.FirstTouched.name);
                }
                else Assert.IsNull(effects.HitSound);
            }
            Assert.IsTrue(rang, "the post rang");
        }

        [Test]
        public void A_crossbar_hit_rings_from_its_physics_contact()
        {
            var effects = Effects();
            var round = InWindow(FakeReach.NeverReaches());
            Shoot(round, effects, 50, 100 + NetFrame.CrossbarHeight / 2, 100); // the middle of the crossbar
            bool rang = false;
            for (int i = 0; i < 2.0 / Step && !rang; i++)
            {
                Frame(round, effects);
                if (effects.HitSound != SoundCue.PostPing) continue;
                rang = true;
                Assert.AreEqual("Crossbar", effects.Shot.FirstTouched.name);
            }
            Assert.IsTrue(rang, "the crossbar rang");
        }

        // #114: a fast puck a few centimetres clear of the frame takes no look-ahead push off it, so nothing rings.
        [TestCase(-9.2, 50)]  // 5 cm wide of the left post
        [TestCase(40, 109.8)] // 2 cm over the crossbar
        public void A_fast_Shot_just_clear_of_the_frame_rings_no_ping(double x, double y)
        {
            var effects = Effects();
            var round = InWindow(FakeReach.NeverReaches());
            Shoot(round, effects, x, y, 100);
            for (int i = 0; i < 1.0 / Step; i++)
            {
                Frame(round, effects);
                // Past the frame it may sound the boards or glass (#110); it must not ring the frame or rustle the netting.
                Assert.IsTrue(effects.HitSound == null || effects.HitSound == SoundCue.MissImpact || effects.HitSound == SoundCue.GlassImpact,
                    "rang " + effects.HitSound + " at " + effects.Shot.SecondsSinceShot + " s, off " + effects.Shot.FirstTouched?.name);
            }
            Assert.IsTrue(!effects.Shot.Touched || effects.Shot.FirstTouched.sharedMaterial == ShotColliders.Boards || effects.Shot.FirstTouched.sharedMaterial == ShotColliders.Glass);
        }

        // Wide of the net and low (the boards) or high (the glass): the first wall sound of the Shot, from its physics contact.
        // (Just wide of a post: wider Shots leave the camera's view first and the puck is hidden before it meets a wall.)
        [TestCase(-30, 20, SoundCue.MissImpact, "Boards")]
        [TestCase(130, 20, SoundCue.MissImpact, "Boards")]
        [TestCase(-30, 150, SoundCue.GlassImpact, "Glass")]
        [TestCase(130, 150, SoundCue.GlassImpact, "Glass")]
        public void A_board_hit_sounds_the_boards_cue_and_a_glass_hit_the_glass_cue(double x, double y, SoundCue expected, string wall)
        {
            var effects = Effects();
            var round = InWindow(FakeReach.NeverReaches());
            Shoot(round, effects, x, y, 100);
            var heard = new List<SoundCue>();
            for (int i = 0; i < 3.0 / Step; i++)
            {
                Frame(round, effects);
                if (effects.HitSound.HasValue) heard.Add(effects.HitSound.Value);
            }
            Assert.IsTrue(heard.Contains(expected), "heard " + string.Join(", ", heard) + "; touched " + effects.Shot.FirstTouched?.name + ", whistle " + effects.Shot.WhistleSeconds + " " + effects.Shot.Result + ", crossed " + effects.Shot.CrossedNetFace + ", active " + effects.Shot.Puck.gameObject.activeSelf + ", at " + effects.Shot.Puck.position);
            Assert.IsFalse(heard.Contains(SoundCue.PostPing), "no post in the way");
            StringAssert.Contains(wall, effects.Shot.FirstTouched.name);
            Assert.AreEqual(ShotResultKind.Miss, effects.Shot.Result);
        }

        GoalieView ShownGoalie()
        {
            var character = new GoalieCharacter(_root.transform);
            Assume.That(character.Present, "needs the goalie model");
            var view = new GoalieView(_root.transform, character);
            view.Show(true);
            view.Face(0f, 0f);
            view.SetProgress(0f);
            return view;
        }

        [Test]
        public void A_Miss_shows_Relief_from_its_Whistle_not_before()
        {
            GoalieView goalie = ShownGoalie();
            var effects = Effects(goalie);
            var round = InWindow(FakeReach.NeverReaches());
            Shoot(round, effects, 130, 50, 100);
            while (round.AwaitingWhistle && effects.Shot.SecondsSinceShot < 4.0)
            {
                Assert.Less(goalie.SecondsSinceArrival, 0f, "no after-shot motion before the Whistle");
                Frame(round, effects);
            }
            Assert.AreEqual(ShotResultKind.Miss, round.LastResult.Value.Kind);
            Assert.AreEqual(AfterShotGesture.Relief, goalie.Gesture);
            Assert.GreaterOrEqual(goalie.SecondsSinceArrival, 0f, "Relief runs from the Whistle's frame");
            Assert.Less(goalie.SecondsSinceArrival, 2 * Step);
        }

        // The race saves it, so the goalie reaches as for a Save (no gesture after it); the physics puck goes in, and at the
        // Whistle he takes the GOAL!'s Annoyed Gesture.
        [Test]
        public void The_goalie_s_gesture_follows_the_Whistle_s_result_not_the_race()
        {
            GoalieView goalie = ShownGoalie();
            var effects = Effects(goalie);
            var round = InWindow(FakeReach.InTheWay());
            Shoot(round, effects, 50, 50, 100);
            Assert.AreEqual(AfterShotGesture.None, goalie.Gesture, "the race's Save: no gesture");
            UntilWhistle(round, effects);
            Assert.AreEqual(ShotResultKind.Goal, round.LastResult.Value.Kind);
            Assert.IsTrue(goalie.Gesture == AfterShotGesture.StickSlap || goalie.Gesture == AfterShotGesture.Slump, "an Annoyed Gesture: " + goalie.Gesture);
        }

        // ---- What is seen: the puck ----

        [TestCase(70, 60, 120, TestName = "A Goal")]
        [TestCase(130, 50, 100, TestName = "Wide")]
        [TestCase(50, 90, 20, TestName = "A slow lob")]
        [TestCase(50, 140, 60, TestName = "Over the net")]
        public void The_puck_is_never_hidden_before_it_crosses_the_Net_Face_or_hits_something(double x, double y, double kmh)
        {
            var effects = Effects();
            var round = InWindow(FakeReach.NeverReaches());
            Shoot(round, effects, x, y, kmh);
            GameObject puck = effects.Shot.Puck.gameObject;
            Assert.IsTrue(puck.activeSelf, "shown from the launch (below the view: it flies up into it)");
            for (int i = 0; i < 4.0 / Step; i++)
            {
                Frame(round, effects);
                if (!puck.activeSelf)
                {
                    Assert.IsTrue(effects.Shot.CrossedNetFace || effects.Shot.Touched, "hidden only after the Net Face or a hit, step " + i);
                    Assert.IsTrue(effects.Shot.OutOfSight, "hidden only out of the view, step " + i);
                    break;
                }
            }
        }

        [Test]
        public void The_puck_stays_in_the_air_out_of_view_before_the_Net_Face_and_hides_once_out_of_view_after_it()
        {
            var effects = Effects();
            var round = InWindow(FakeReach.NeverReaches());
            Shoot(round, effects, 50, 50, 60);
            GameObject puck = effects.Shot.Puck.gameObject;
            Quaternion ahead = _camera.transform.rotation;

            _camera.transform.rotation = ahead * Quaternion.Euler(0f, 180f, 0f); // look away: the puck in the air is out of view
            for (int i = 0; i < 5; i++) Frame(round, effects);
            Assert.IsFalse(effects.Shot.CrossedNetFace);
            Assert.IsTrue(puck.activeSelf, "not hidden in the air, before the Net Face");

            _camera.transform.rotation = ahead;
            while (!effects.Shot.CrossedNetFace && effects.Shot.SecondsSinceShot < 3.0) Frame(round, effects);
            Assert.IsTrue(effects.Shot.CrossedNetFace);
            Frame(round, effects);
            Assert.IsTrue(puck.activeSelf, "in the net, in view: still shown");

            _camera.transform.rotation = ahead * Quaternion.Euler(0f, 180f, 0f);
            Frame(round, effects);
            Assert.IsFalse(puck.activeSelf, "out of view after the Net Face: hidden");
        }

        // The Replay's look-up turns the camera up to the Video Board, so a netted puck is out of its view: the hide rule waits
        // for the camera to come back down (CameraLookUp 0), and the puck is still in the net then.
        [Test]
        public void A_netted_puck_stays_shown_while_the_camera_looks_up_and_after_it_comes_back()
        {
            var effects = Effects();
            var round = InWindow(FakeReach.NeverReaches());
            Shoot(round, effects, 70, 60, 120);
            UntilWhistle(round, effects);
            Assert.AreEqual(ShotResultKind.Goal, round.LastResult.Value.Kind);
            for (int i = 0; i < 60; i++) Frame(round, effects); // into the netting
            GameObject puck = effects.Shot.Puck.gameObject;
            Assert.IsTrue(puck.activeSelf, "in the net, in view");

            Quaternion ahead = _camera.transform.rotation;
            effects.CameraLookUp = 1.0;
            _camera.transform.rotation = ahead * Quaternion.Euler(-60f, 0f, 0f); // up at the Video Board: the net is below the view
            for (int i = 0; i < 60; i++) Frame(round, effects);
            Assert.IsTrue(effects.Shot.OutOfSight, "the netted puck is out of the looked-up view");
            Assert.IsTrue(puck.activeSelf, "not hidden while the camera looks up");

            effects.CameraLookUp = 0.0;
            _camera.transform.rotation = ahead;
            for (int i = 0; i < 5; i++) Frame(round, effects);
            Assert.IsTrue(puck.activeSelf, "still in the net when the camera comes back down");
        }

        // ---- The Replay of a physics Shot (ReplayView) ----

        ReplayView MakeReplay(Round round, PhysicsShotEffects effects)
        {
            VideoBoards boards = VideoBoards.Build(_root.transform);
            var host = new GameObject("Replay (test)");
            host.transform.SetParent(_root.transform, false);
            var replay = host.AddComponent<ReplayView>();
            replay.Init(round, boards, _camera);
            replay.ReplayPhysicsPuck(() => effects.Shot?.Puck, name => null);
            return replay;
        }

        // The Replay records from the Shot (its result comes at the Whistle) and plays the physics puck's recorded Rigidbody poses
        // on a stand-in with no Rigidbody or hit shapes; the live puck is not drawn on Replay frames and physics keeps it.
        [Test]
        public void A_Goal_s_Replay_plays_the_physics_puck_s_recorded_flight_on_a_stand_in()
        {
            var effects = Effects();
            ScreenFlow flow = FlowInWindow();
            Round round = flow.Round;
            ReplayView replay = MakeReplay(round, effects);
            GameObject standIn = replay.ReplayPuck;
            Assert.IsNull(standIn.GetComponentInChildren<Rigidbody>(true), "the stand-in has no Rigidbody");
            Assert.IsNull(standIn.GetComponentInChildren<Collider>(true), "and no hit shapes");

            Shoot(round, effects, 70, 60, 120);
            Rigidbody live = effects.Shot.Puck;
            int replayFrames = 0;
            float nearestZ = float.MaxValue, furthestZ = float.MinValue;
            for (int f = 0; f < 10.0 / Step && round.Phase == RoundPhase.ResultHold; f++)
            {
                replay.RestoreLive();
                Frame(round, effects);
                replay.Show(ArenaShow.From(flow));
                replay.Step();
                Renderer[] liveLook = live.GetComponentsInChildren<Renderer>();
                if (replay.Showing)
                {
                    replayFrames++;
                    Assert.IsFalse(Array.Exists(liveLook, r => r.enabled), "the live puck is not drawn in the Replay, frame " + f);
                    Assert.IsFalse(live.isKinematic, "physics keeps the live puck");
                    if (standIn.activeSelf)
                    {
                        nearestZ = Mathf.Min(nearestZ, standIn.transform.position.z);
                        furthestZ = Mathf.Max(furthestZ, standIn.transform.position.z);
                    }
                }
                else
                {
                    Assert.IsTrue(Array.TrueForAll(liveLook, r => r.enabled), "the live puck is drawn outside the Replay, frame " + f);
                    Assert.IsFalse(standIn.activeSelf, "the stand-in shows only in the Replay, frame " + f);
                }
            }
            Assert.AreEqual(ShotResultKind.Goal, round.Results[0]);
            Assert.Greater(replay.RecordedMoments, 30, "the flight was recorded from the Shot");
            Assert.Greater(replayFrames, 100, "the Replay played (2 s at 120 frames a second)");
            Assert.Less(nearestZ, 0f, "the Replay shows the puck before the Net Face");
            Assert.Greater(furthestZ, 0f, "and in the net");
        }

        [Test]
        public void A_Wide_Miss_with_the_physics_puck_keeps_no_Replay()
        {
            var effects = Effects();
            ScreenFlow flow = FlowInWindow();
            Round round = flow.Round;
            ReplayView replay = MakeReplay(round, effects);

            Shoot(round, effects, -40, 50, 120); // well wide of the frame
            for (int f = 0; f < 10.0 / Step && round.Phase == RoundPhase.ResultHold; f++)
            {
                replay.RestoreLive();
                Frame(round, effects);
                replay.Show(ArenaShow.From(flow));
                replay.Step();
                Assert.IsFalse(replay.Showing, "no Replay, frame " + f);
            }
            Assert.AreEqual(ShotResultKind.Miss, round.Results[0]);
            Assert.AreEqual(0, replay.RecordedMoments, "the recording is dropped at the Whistle");
        }

        [Test]
        public void The_puck_stays_after_the_hold_and_the_next_Shot_replaces_it()
        {
            var effects = Effects();
            var round = InWindow(FakeReach.NeverReaches());
            Shoot(round, effects, 70, 60, 120);
            UntilWhistle(round, effects);
            PhysicsShot first = effects.Shot;
            GameObject firstPuck = first.Puck.gameObject;
            while (round.Phase != RoundPhase.ShootingWindow) Frame(round, effects); // the hold, then the next skate-in and slow-down
            Assert.IsTrue(firstPuck != null && firstPuck.activeSelf, "still there (in the net, in view) until the next Shot");

            Shoot(round, effects, 30, 40, 100);
            Assert.AreNotSame(first, effects.Shot);
            Assert.IsTrue(firstPuck == null, "the next Shot replaces the puck");
            Assert.AreEqual(1, _root.GetComponentsInChildren<Rigidbody>(true).Length, "one puck");
        }

        // ---- No hang ----

        [Test]
        public void A_Shot_the_physics_stops_feeding_still_gets_its_Whistle_and_the_Round_goes_on()
        {
            var effects = Effects();
            var round = InWindow(FakeReach.NeverReaches());
            Shoot(round, effects, 50, 50, 100);
            var settings = round.Settings;
            const float RealFrame = 1f / 60f;
            double untilFallback = settings.WhistleCapSeconds + PhysicsShotEffects.NoWhistleGraceSeconds;
            for (double t = 0; t < untilFallback - 2 * RealFrame; t += RealFrame) // no physics steps at all
            {
                round.Tick(RealFrame);
                effects.Update(round, RealFrame);
            }
            Assert.IsTrue(round.AwaitingWhistle, "waits while the cap and grace have not passed");
            for (int i = 0; i < 4; i++)
            {
                round.Tick(RealFrame);
                effects.Update(round, RealFrame);
            }
            Assert.IsFalse(round.AwaitingWhistle, "the cap's Whistle from the Shot's own clock");
            Assert.AreEqual(ShotResultKind.Miss, round.LastResult.Value.Kind);
            round.Tick(settings.ResultHoldSeconds + 0.01);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase, "the Round went on to the next Shot");
        }

        [Test]
        public void A_replaced_Shot_sounds_its_Whistle_first()
        {
            var effects = Effects();
            var round = InWindow(FakeReach.NeverReaches());
            Shoot(round, effects, 50, 50, 100);
            PhysicsShot shot = effects.Shot;
            effects.Dispose(); // the puck goes before its Whistle
            Assert.IsTrue(shot.HasWhistled);
            Assert.IsFalse(round.AwaitingWhistle);
            Assert.AreEqual(ShotResultKind.Miss, round.LastResult.Value.Kind);
        }

        // ---- The goalie's ease-in ----

        // BreakawayView's flight (PhysicsShotEffects.FlightSeconds) is the same before and after the Whistle, so a close, fast
        // Shot with gear in the way eases him in with no jump on the Whistle frame (the Whistle need not come at the Flight Time).
        [Test]
        public void A_close_fast_Shot_with_gear_in_the_way_eases_the_goalie_in_with_no_jump_at_the_Whistle()
        {
            var effects = Effects();
            var round = InWindow(FakeReach.InTheWay());
            Shoot(round, effects, 50, 50, 200);
            Assert.IsTrue(round.LastRace.Value.InTheWay);
            float flight = effects.FlightSeconds;
            Assert.Less(flight, Breakaway.GoalieReactSeconds, "a fast Shot: the flight is shorter than his ease-in");
            bool sawWhistle = false;
            double previous = 0, biggestStep = 0;
            for (int i = 0; i < 1.0 / Step && round.Phase == RoundPhase.ResultHold; i++)
            {
                bool waiting = round.AwaitingWhistle;
                Frame(round, effects);
                sawWhistle |= waiting && !round.AwaitingWhistle;
                Assert.AreEqual(flight, effects.FlightSeconds, "the same flight before and after the Whistle");
                double shown = Breakaway.GoalieSet(round.Phase, round.PhaseSecondsElapsed, true, effects.FlightSeconds, round.LastRace);
                Assert.GreaterOrEqual(shown, previous - 1e-9, "never back");
                biggestStep = Math.Max(biggestStep, shown - previous);
                previous = shown;
            }
            Assert.IsTrue(sawWhistle, "the Whistle came");
            Assert.Less(biggestStep, 0.5, "no jump in his progress");
        }

        // ---- The rink's colliders ----

        // The physics puck must meet only the Shot colliders: a model or placeholder left with its collider (Unity's primitives
        // come with one) would be an invisible wall in play that no seam-2 test sees.
        [Test]
        public void In_the_built_rink_the_puck_can_hit_only_the_Shot_colliders()
        {
            RinkBuilder.Build(out Camera camera);
            GameObject rink = GameObject.Find("Rink");
            try
            {
                var character = new GoalieCharacter(rink.transform);
                var view = new GoalieView(rink.transform, character);
                view.Show(true);
                using (var effects = new PhysicsShotEffects(rink.transform, camera, new SeededRandom(1), view))
                {
                    var round = InWindow(FakeReach.NeverReaches());
                    Shoot(round, effects, 50, 50, 100);
                    var stray = new List<string>();
                    foreach (Collider c in rink.GetComponentsInChildren<Collider>(true))
                        if (c.GetComponentInParent<Rigidbody>(true) == null && c.transform.parent?.name != ShotColliders.RootName && c.name != NetCloth.ProxyName) // the cloth's spheres ignore the puck (NetClothTests)
                            stray.Add(c.name + " under " + c.transform.parent?.name);
                    CollectionAssert.IsEmpty(stray);
                    Assert.IsEmpty(camera.GetComponentsInChildren<Collider>(true));
                }
            }
            finally
            {
                Object.DestroyImmediate(rink);
                Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
