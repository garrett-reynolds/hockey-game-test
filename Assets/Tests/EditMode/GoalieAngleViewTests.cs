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
    /// The goalie character plays the angle on the skate-in (#64), played through a whole Round with BreakawayView, the goalie
    /// view and the Shot effects at 60 fps: on the line from the puck to the net centre through the skate-in, set on his
    /// Set-up spot when the window opens, squared up to the puck, and reacting from the spot the Round's race measured.
    /// </summary>
    public class GoalieAngleViewTests
    {
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
            Object.DestroyImmediate(_parent);
            Physics.simulationMode = _oldMode;
            Time.timeScale = 1f;
        }

        // ---- The skate push (#126) through a whole Round ------------------------------------------------------------------

        /// <summary>One frame of a played Round, after BreakawayView.Step.</summary>
        sealed class PushFrame
        {
            public Round Round;
            public GoalieView View;
            public GoalieCharacter Character;
            public PhysicsShotEffects Effects;
            public RoundSettings Settings;
            public RoundPhase Phase;
            public double Seconds;
            public bool ShotFrame;
            /// <summary>The set blend the view used this frame.</summary>
            public double Set => Breakaway.GoalieSet(Phase, Seconds, Round.LastShot.HasValue, Effects.FlightSeconds, Round.LastRace);
        }

        /// <summary>
        /// Plays a Round at 60 fps with the real BreakawayView, goalie view and Shot effects. shootAfter: the Shot (aimX, aimY) goes this
        /// many seconds into the first shooting window (negative: none). each runs after every frame's BreakawayView.Step and returns
        /// false to stop.
        /// </summary>
        void PlayPush(IRandomSource random, RoundSettings settings, IGoalieReach reach, double shootAfter, double aimX, double aimY, Func<PushFrame, bool> each)
        {
            var cameraObject = new GameObject("Camera (test)", typeof(Camera));
            var driver = new GameObject("Breakaway (test)");
            PhysicsShotEffects effects = null;
            GoalieView view = null;
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                var character = new GoalieCharacter(_parent.transform);
                Assume.That(character.Present, "needs the goalie model");
                view = new GoalieView(_parent.transform, character);
                effects = new PhysicsShotEffects(_parent.transform, camera, new SeededRandom(7), view, name => null);
                Physics.SyncTransforms();
                var round = new Round(random, settings, reach, 0);
                var breakaway = driver.AddComponent<BreakawayView>();
                breakaway.Init(round, camera, view, () => effects.FlightSeconds);
                bool shot = false;
                round.StartRound();
                for (int frame = 0; frame < 60 * 60 && round.Phase != RoundPhase.RoundOver; frame++)
                {
                    for (int step = 0; step < 2; step++)
                    {
                        Physics.Simulate(PhysicsShot.StepSeconds);
                        effects.AfterPhysicsStep(PhysicsShot.StepSeconds);
                    }
                    round.Tick(Frame);
                    view.Show(true);
                    bool shoot = !shot && shootAfter >= 0 && round.Phase == RoundPhase.ShootingWindow && round.PhaseSecondsElapsed > shootAfter;
                    if (shoot) { round.SubmitShot(aimX, aimY, 100, false); shot = true; }
                    effects.Update(round, Frame);
                    breakaway.Step(Frame * Time.timeScale, Frame);
                    var f = new PushFrame { Round = round, View = view, Character = character, Effects = effects, Settings = settings,
                        Phase = round.Phase, Seconds = round.PhaseSecondsElapsed, ShotFrame = shoot };
                    if (!each(f)) break;
                }
            }
            finally
            {
                effects?.Dispose();
                view?.Dispose();
                Object.DestroyImmediate(driver);
                Object.DestroyImmediate(cameraObject);
            }
        }

        static RoundSettings PushSettings(bool shuffles = true) =>
            new RoundSettings { NormalDepthMetres = 0.2, ReactionWobbleSeconds = 0, SkateInShuffles = shuffles };

        static Transform RightSkateBone(GoalieCharacter c)
        {
            Transform l = c.Bone(HumanBodyBones.LeftFoot), r = c.Bone(HumanBodyBones.RightFoot);
            return l.position.x > r.position.x ? l : r;
        }

        static string Describe(PushShown s) => s.Kind + " " + s.Skate + " " + s.Phase + " " + s.SpeedShare.ToString("F3");

        static bool Same(PushShown a, PushShown b) =>
            a.Kind == b.Kind && a.Skate == b.Skate && a.Phase == b.Phase && Math.Abs(a.SpeedShare - b.SpeedShare) < 1e-9;

        [Test]
        public void A_Ready_Stance_shuffle_shows_the_far_side_skate_pushing()
        {
            var failures = new List<string>();
            int pushFrames = 0, movedLeft = 0, movedRight = 0;
            double prevX = double.NaN, mostOut = 0;
            PlayPush(ScriptedRandom.Forcing(AttackSide.Left), PushSettings(), FakeReach.NeverReaches(), -1, 0, 0, f =>
            {
                double x = f.View.ShownSpot.x;
                if (f.Phase == RoundPhase.SkateIn && !double.IsNaN(prevX))
                {
                    PushShown s = f.View.Push;
                    double dx = x - prevX;
                    if (s.Phase != PushPhase.Done && Math.Abs(dx) > 1e-5)
                    {
                        pushFrames++;
                        Skate expected = dx < 0 ? Skate.Right : Skate.Left;
                        if (dx < 0) movedLeft++; else movedRight++;
                        if (s.Skate != expected) failures.Add(f.Seconds.ToString("F3") + " s: moved " + dx.ToString("F5") + ", shows " + Describe(s));
                        if (s.Kind != PushKind.Shuffle) failures.Add(f.Seconds.ToString("F3") + " s: a skate-in step is a Shuffle, shows " + Describe(s));
                        mostOut = Math.Max(mostOut, f.View.PushMoment.PushOut);
                    }
                }
                prevX = x;
                return f.Phase == RoundPhase.SkateIn || f.Phase == RoundPhase.Idle;
            });
            TestContext.Out.WriteLine(pushFrames + " push frames (" + movedLeft + " toward -X, " + movedRight + " toward +X), most out " + mostOut.ToString("F2"));
            Assert.IsEmpty(failures, string.Join("\n", failures));
            Assert.Greater(pushFrames, 0, "he shuffled and the push showed");
            Assert.Greater(mostOut, 0.2, "the skate went out (a skate-in step is short, so not fully)");
        }

        [Test]
        public void A_slow_drift_with_the_angle_shows_no_push()
        {
            // Gliding with the angle (no skate-in shuffles): once he has moved onto it (the first 0.3 s are a real 9 cm shuffle), a left
            // attack's angle drifts across, far slower than any push.
            int driftFrames = 0, pushFrames = 0;
            double worstOut = 0;
            var seenFrames = new List<string>();
            PlayPush(ScriptedRandom.Forcing(AttackSide.Left), PushSettings(false), FakeReach.NeverReaches(), -1, 0, 0, f =>
            {
                if (f.Phase == RoundPhase.SkateIn && f.Seconds > 0.3)
                {
                    driftFrames++;
                    if (f.View.Push.Phase != PushPhase.Done) { pushFrames++; seenFrames.Add(f.Seconds.ToString("F3") + " s " + Describe(f.View.Push) + " x " + f.View.ShownSpot.x.ToString("F4")); }
                    worstOut = Math.Max(worstOut, f.View.PushMoment.PushOut);
                }
                return f.Phase == RoundPhase.SkateIn || f.Phase == RoundPhase.Idle;
            });
            TestContext.Out.WriteLine(driftFrames + " skate-in frames, " + pushFrames + " showed a push, most out " + worstOut.ToString("F3"));
            Assert.Greater(driftFrames, 100);
            Assert.AreEqual(0, pushFrames, "a slow drift is not a push:\n" + string.Join("\n", seenFrames));
            Assert.AreEqual(0.0, worstOut, "and the skate never moved");
        }

        [Test]
        public void A_Late_Settle_slide_shows_a_push_from_the_Spot_shape([Values(Post.Left, Post.Right)] Post post)
        {
            // He comes back toward the angle: from the Left post's side (-X of the angle) he moves +X, pushing from Skate.Left.
            var settings = PushSettings();
            double signed = post == Post.Left ? settings.LateSettleMetres : -settings.LateSettleMetres;
            var reference = new SkatePush(signed, settings);
            var failures = new List<string>();
            var phases = new List<PushPhase>();
            int windowFrames = 0;
            double firstX = double.NaN, lastX = 0, mostOutInPush = 0;
            PlayPush(ScriptedRandom.Forcing(AttackSide.Middle, HockeyGame.Core.SetUp.LateSettle(post)), settings, FakeReach.NeverReaches(), -1, 0, 0, f =>
            {
                if (f.Phase != RoundPhase.ShootingWindow) return f.Phase != RoundPhase.ResultHold && f.Phase != RoundPhase.RoundOver;
                windowFrames++;
                if (double.IsNaN(firstX)) firstX = f.View.ShownSpot.x;
                lastX = f.View.ShownSpot.x;
                PushShown expected = PushShown.AtProgress(signed, f.Seconds / settings.LateSettleSeconds, settings);
                PushShown s = f.View.Push;
                if (!Same(s, expected)) failures.Add(f.Seconds.ToString("F3") + " s: shows " + Describe(s) + ", Spot shape " + Describe(expected));
                if (phases.Count == 0 || phases[phases.Count - 1] != s.Phase) phases.Add(s.Phase);
                if (s.Phase == PushPhase.Push) mostOutInPush = Math.Max(mostOutInPush, f.View.PushMoment.PushOut);
                if (s.Phase != PushPhase.Done && (s.Kind != reference.Kind || s.Skate != reference.PushingSkate))
                    failures.Add(f.Seconds.ToString("F3") + " s: " + Describe(s) + ", SkatePush says " + reference.Kind + " " + reference.PushingSkate);
                if (f.Seconds >= settings.LateSettleSeconds && s.Phase != PushPhase.Done) failures.Add(f.Seconds.ToString("F3") + " s: still pushing after the Late Settle");
                return true;
            });
            Assert.IsEmpty(failures, string.Join("\n", failures));
            Assert.Greater(windowFrames, 30);
            Assert.GreaterOrEqual(mostOutInPush, 0.9, "the pose steps on the Round's clock (the window runs slow), so the skate is fully out within the Push");
            Assert.AreEqual(new[] { PushPhase.Push, PushPhase.Glide, PushPhase.Done }, phases.ToArray(), "Push, Glide, Done, never back");
            Assert.AreEqual(post == Post.Left ? Skate.Left : Skate.Right, reference.PushingSkate, "the Left post's side moves +X from the left skate");
            Assert.AreEqual(Math.Sign(signed), Math.Sign(lastX - firstX), "he really moves that way");
        }

        [Test]
        public void A_clean_Set_up_window_shows_no_push()
        {
            int windowFrames = 0;
            var failures = new List<string>();
            PlayPush(ScriptedRandom.Forcing(AttackSide.Left), PushSettings(), FakeReach.NeverReaches(), -1, 0, 0, f =>
            {
                if (f.Phase != RoundPhase.ShootingWindow) return f.Phase != RoundPhase.ResultHold && f.Phase != RoundPhase.RoundOver;
                windowFrames++;
                if (f.View.Push.Phase != PushPhase.Done) failures.Add(f.Seconds.ToString("F3") + " s: " + Describe(f.View.Push));
                if (f.Seconds > 0.5 && f.View.PushMoment.PushOut > 0.0) failures.Add(f.Seconds.ToString("F3") + " s: pose still out " + f.View.PushMoment.PushOut);
                return true;
            });
            Assert.Greater(windowFrames, 30);
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void A_Save_Reaction_to_the_right_pushes_from_the_left_skate()
        {
            var settings = PushSettings();
            var failures = new List<string>();
            var phases = new List<PushPhase>();
            float hipsAtShot = 0f, hipsNow = 0f, slide = 0f;
            Skate seen = Skate.Right;
            PlayPush(ScriptedRandom.Forcing(AttackSide.Middle), settings, FakeReach.InTheWay(), 0.5, 98, 85, f =>
            {
                float hips = f.Character.Bone(HumanBodyBones.Hips).position.x;
                if (f.ShotFrame) hipsAtShot = hips;
                if (f.Phase != RoundPhase.ResultHold) return f.Phase != RoundPhase.RoundOver;
                hipsNow = hips;
                Assume.That(f.Round.LastRace.Value.SaveReaction, Is.EqualTo(SaveReaction.Glove));
                slide = f.View.ReactionSlideMetres;
                PushShown expected = PushShown.AtProgress(slide, (float)f.Set, settings); // the view works on the float set
                PushShown s = f.View.Push;
                if (!Same(s, expected)) failures.Add("set " + f.Set.ToString("F3") + ": shows " + Describe(s) + ", slide shape " + Describe(expected));
                if (phases.Count == 0 || phases[phases.Count - 1] != s.Phase) phases.Add(s.Phase);
                if (s.Phase == PushPhase.Push) seen = s.Skate;
                if (f.Set >= 1.0 && s.Phase != PushPhase.Done) failures.Add("set 1: " + Describe(s));
                return f.Seconds < 1.0;
            });
            TestContext.Out.WriteLine("slide " + slide.ToString("F3") + " m, hips moved " + (hipsNow - hipsAtShot).ToString("F3") + " m, phases " + string.Join(",", phases));
            Assert.IsEmpty(failures, string.Join("\n", failures));
            Assert.Greater(slide, 0.1f, "the aim to the right slides him to +X");
            // (#130) hipsAtShot still has the push's hip lean (it blends out over BlendSeconds once the Save Reaction starts), so the net
            // move is the slide less that lean (at most LeanMetres, 0.10 m): measured 0.093 with it. The slide above is the 0.1 m check.
            Assert.Greater(hipsNow - hipsAtShot, 0.05f, "and his body really goes to +X");
            Assert.AreEqual(Skate.Left, seen, "a slide toward +X pushes from the left skate");
            Assert.AreEqual(new[] { PushPhase.Push, PushPhase.Glide, PushPhase.Done }, phases.ToArray(), "Push, Glide, Done, never back");
        }

        [Test]
        public void A_Pads_reaction_shows_no_push()
        {
            var failures = new List<string>();
            int holdFrames = 0;
            float mostSlide = 0f;
            PlayPush(ScriptedRandom.Forcing(AttackSide.Middle), PushSettings(), FakeReach.InTheWay(), 0.5, 20, 20, f =>
            {
                if (f.Phase != RoundPhase.ResultHold) return f.Phase != RoundPhase.RoundOver;
                Assert.AreEqual(SaveReaction.Pads, f.Round.LastRace.Value.SaveReaction);
                holdFrames++;
                mostSlide = Mathf.Max(mostSlide, Mathf.Abs(f.View.ReactionSlideMetres));
                if (f.View.Push.Phase != PushPhase.Done) failures.Add("set " + f.Set.ToString("F3") + ": " + Describe(f.View.Push));
                if (f.View.PushMoment.PushOut != 0.0 || f.View.PushMoment.LeadTurn != 0.0) failures.Add("set " + f.Set.ToString("F3") + ": pose " + f.View.PushMoment.PushOut);
                return f.Seconds < 1.0;
            });
            Assert.Greater(holdFrames, 20);
            Assert.GreaterOrEqual(mostSlide, PushShown.MinMetres, "the Pads reaction does slide, so a push would show if Pads were not left out");
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void Both_feet_stay_on_the_ice_through_a_Save_Reaction_push()
        {
            var failures = new List<string>();
            float leftY = 0f, rightY = 0f;
            double mostOut = 0;
            PlayPush(ScriptedRandom.Forcing(AttackSide.Middle), PushSettings(), FakeReach.InTheWay(), 0.5, 98, 85, f =>
            {
                Transform l = f.Character.Bone(HumanBodyBones.LeftFoot), r = f.Character.Bone(HumanBodyBones.RightFoot);
                if (f.ShotFrame) { leftY = l.position.y; rightY = r.position.y; }
                if (f.Phase != RoundPhase.ResultHold) return f.Phase != RoundPhase.RoundOver;
                mostOut = Math.Max(mostOut, f.View.PushMoment.PushOut);
                if (f.View.PushMoment.PushOut > 0.0 && (Math.Abs(l.position.y - leftY) > 0.001f || Math.Abs(r.position.y - rightY) > 0.001f))
                    failures.Add("set " + f.Set.ToString("F3") + ": feet y " + l.position.y.ToString("F4") + ", " + r.position.y.ToString("F4") + " (ice " + leftY.ToString("F4") + ", " + rightY.ToString("F4") + ")");
                return f.Seconds < 1.0;
            });
            Assert.Greater(mostOut, 0.1, "the push showed (a Save Reaction's Push is short)");
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void The_pose_fades_to_nothing_when_the_Round_is_idle()
        {
            var cameraObject = new GameObject("Camera (test)", typeof(Camera));
            var driver = new GameObject("Breakaway (test)");
            GoalieView view = null;
            try
            {
                var character = new GoalieCharacter(_parent.transform);
                Assume.That(character.Present, "needs the goalie model");
                view = new GoalieView(_parent.transform, character);
                var round = new Round(ScriptedRandom.Forcing(AttackSide.Middle), PushSettings(), FakeReach.NeverReaches(), 0);
                var breakaway = driver.AddComponent<BreakawayView>();
                breakaway.Init(round, cameraObject.GetComponent<Camera>(), view, () => 0.5f);
                view.Show(true);
                for (int i = 0; i < 20; i++) view.ShowPush(new PushShown(PushKind.Shuffle, Skate.Right, PushPhase.Push, 1), Frame);
                Assert.AreEqual(1.0, view.PushMoment.PushOut, 1e-9);
                Assert.AreEqual(RoundPhase.Idle, round.Phase);

                for (int i = 0; i < 12; i++) breakaway.Step(Frame, Frame); // past the 0.10 s hold
                Assert.Less(view.PushMoment.PushOut, 1.0, "the Idle frames fed the tracker");
                for (int i = 0; i < 60; i++) breakaway.Step(Frame, Frame);
                Assert.AreEqual(0.0, view.PushMoment.PushOut, 0.0, "faded out");
                Assert.AreEqual(PushPhase.Done, view.Push.Phase);
            }
            finally
            {
                view?.Dispose();
                Object.DestroyImmediate(driver);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void PoseForPhysics_poses_with_the_push_as_it_is_and_does_not_step_it()
        {
            var cameraObject = new GameObject("Camera (test)", typeof(Camera));
            var driver = new GameObject("Breakaway (test)");
            GoalieView view = null;
            try
            {
                var character = new GoalieCharacter(_parent.transform);
                Assume.That(character.Present, "needs the goalie model");
                view = new GoalieView(_parent.transform, character);
                var round = new Round(ScriptedRandom.Forcing(AttackSide.Middle), PushSettings(), FakeReach.NeverReaches(), 0);
                var breakaway = driver.AddComponent<BreakawayView>();
                breakaway.Init(round, cameraObject.GetComponent<Camera>(), view, () => 0.5f);
                view.Show(true);
                breakaway.PoseForPhysics(0f);
                float rest = RightSkateBone(character).position.x;

                for (int i = 0; i < 20; i++) view.ShowPush(new PushShown(PushKind.Shuffle, Skate.Right, PushPhase.Push, 1), Frame);
                breakaway.PoseForPhysics(0.01f);
                float pushed = RightSkateBone(character).position.x;
                PushPoseMoment moment = view.PushMoment;
                breakaway.PoseForPhysics(0.02f);

                Assert.Greater(pushed - rest, 0.05f, "the physics pose has the foot out");
                Assert.AreEqual(pushed, RightSkateBone(character).position.x, 1e-6f);
                Assert.AreEqual(moment.PushOut, view.PushMoment.PushOut, 0.0, "the pose was not stepped");
            }
            finally
            {
                view?.Dispose();
                Object.DestroyImmediate(driver);
                Object.DestroyImmediate(cameraObject);
            }
        }

        static double OffTheLine(Vector3 spot, RinkPoint puck) =>
            Math.Abs(puck.X * spot.z - puck.Z * spot.x) / Math.Sqrt(puck.X * puck.X + puck.Z * puck.Z);

        static float Distance(Vector3 a, RinkPoint b) => Vector3.Distance(a, new Vector3((float)b.X, (float)b.Y, (float)b.Z));

        [Test]
        public void Through_a_Round_he_plays_the_angle_on_the_skate_in_is_set_on_his_Set_up_spot_at_the_window_and_reacts_from_the_raced_spot()
        {
            // Gliding with the angle: the skate-in shuffles (#70) wait until he is 8 cm off it; BeforeTheShotTests plays them.
            var settings = new RoundSettings { NormalDepthMetres = 0.2, ReactionWobbleSeconds = 0, SkateInShuffles = false };
            var cameraObject = new GameObject("Camera (test)", typeof(Camera));
            var driver = new GameObject("Breakaway (test)");
            PhysicsShotEffects effects = null;
            GoalieView view = null;
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                var character = new GoalieCharacter(_parent.transform);
                Assume.That(character.Present, "needs the goalie model");
                view = new GoalieView(_parent.transform, character);
                effects = new PhysicsShotEffects(_parent.transform, camera, new SeededRandom(7), view, name => null);
                Physics.SyncTransforms();
                // Shots 1-3 (left, middle, right) are Clean, Depth (centred on a middle attack too) and Clean; 4-6 are dealt mistakes.
                var round = new Round(ScriptedRandom.Dealing(new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right,
                    AttackSide.Left, AttackSide.Middle, AttackSide.Right }, new[] { HockeyGame.Core.SetUp.Clean, HockeyGame.Core.SetUp.Depth, HockeyGame.Core.SetUp.Clean }),
                    settings, FakeReach.NeverReaches(), 0);
                var breakaway = driver.AddComponent<BreakawayView>();
                breakaway.Init(round, camera, view, () => effects.FlightSeconds);

                var failures = new List<string>();
                var hipsAtWindow = new Dictionary<AttackSide, float>();
                double worstOffLine = 0, worstFacing = 0, worstMistakeFacing = 0;
                string offWhere = "", facingWhere = "";
                int shots = 0, windows = 0, skateChecks = 0;
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
                    bool shoot = round.Phase == RoundPhase.ShootingWindow && round.PhaseSecondsElapsed > 0.5;
                    if (shoot) round.SubmitShot(70, 60, 100, false);
                    effects.Update(round, Frame);
                    if (shoot)
                    {
                        // The Shot frame: he reacts (React, before this frame's BreakawayView step) from the spot the race measured.
                        shots++;
                        ShotRace race = round.LastRace.Value;
                        if (Distance(view.ShownSpot, race.Spot) > 1e-5f) failures.Add("Shot " + shots + ": reacts from " + view.ShownSpot + ", race " + race.Spot);
                        double facing = SetUps.Facing(race.Spot, race.ShotStart);
                        if (Math.Abs(view.PosedFacing() - facing) > 1e-3) failures.Add("Shot " + shots + ": reaction built at " + view.PosedFacing() + " deg, race " + facing);
                    }
                    breakaway.Step(Frame * Time.timeScale, Frame);

                    RoundPhase phase = round.Phase;
                    AttackSide side = round.AttackSide;
                    double seconds = round.PhaseSecondsElapsed;
                    // On the angle through the second half of the skate-in; through the slow-down too in a Clean Set-up (a mistake
                    // settles off it then, onto the Set-up spot checked as the window opens). After a Depth he is still easing the last
                    // millimetre back out early in the second half of the skate-in, so the depth holds to 1 mm.
                    if (phase == RoundPhase.SkateIn && seconds >= 0.5 * settings.SkateInSeconds || phase == RoundPhase.SlowDown && round.SetUp == HockeyGame.Core.SetUp.Clean)
                    {
                        RinkPoint puck = Breakaway.Puck(side, phase, seconds, settings);
                        double off = OffTheLine(view.ShownSpot, puck);
                        if (off > worstOffLine) { worstOffLine = off; offWhere = side + " " + phase + " " + seconds.ToString("F2") + " s: spot " + view.ShownSpot.ToString("F3") + ", puck " + puck; }
                        if (Math.Abs(view.ShownSpot.z + 0.2f) > 1e-3f) failures.Add(phase + " " + seconds.ToString("F2") + ": depth " + view.ShownSpot.z);
                        skateChecks++;
                    }
                    if (phase == RoundPhase.ShootingWindow && last != RoundPhase.ShootingWindow)
                    {
                        windows++;
                        RinkPoint spot = SetUps.Spot(round.SetUp, side, seconds, Breakaway.ShotStart(side, seconds, settings), settings);
                        if (Distance(view.ShownSpot, spot) > 1e-5f) failures.Add(side + ": at the window " + view.ShownSpot + ", Set-up spot " + spot);
                        if (!hipsAtWindow.ContainsKey(side)) hipsAtWindow[side] = character.Bone(HumanBodyBones.Hips).position.x; // Shots 1-3
                    }
                    if (phase == RoundPhase.ShootingWindow)
                    {
                        RinkPoint puck = Breakaway.ShotStart(side, seconds, settings);
                        RinkPoint spot = new RinkPoint(view.ShownSpot.x, 0, view.ShownSpot.z);
                        double facingOff = Math.Abs(view.ShownFacing - SetUps.Facing(spot, puck));
                        if (round.SetUp.IsMistake) // he moved across into it (in slow motion) or still slides: his turn trails a little
                            worstMistakeFacing = Math.Max(worstMistakeFacing, facingOff);
                        else if (facingOff > worstFacing) { worstFacing = facingOff; facingWhere = side + " " + round.SetUp + " " + seconds.ToString("F2") + " s"; }
                    }
                    last = phase;
                }
                TestContext.Out.WriteLine("worst off the angle line " + worstOffLine.ToString("F4") + " m over " + skateChecks + " frames; worst facing " + worstFacing.ToString("F2") + " deg (" + worstMistakeFacing.ToString("F2") + " deg in a mistake)");
                Assert.AreEqual(6, shots, "six Shots");
                Assert.AreEqual(6, windows, "six windows");
                Assert.IsEmpty(failures, string.Join("\n", failures));
                Assert.Less(worstOffLine, 0.01, "on the angle line through the second half of the skate-in and the slow-down: " + offWhere);
                Assert.Less(worstFacing, 0.5, "squared up to the puck in the window: " + facingWhere);
                Assert.Less(worstMistakeFacing, 2.0, "nearly squared up in a Set-up Mistake");
                Assert.Greater(hipsAtWindow[AttackSide.Right] - hipsAtWindow[AttackSide.Left], 0.08f, "his body is across to the left on a left attack, to the right on a right one");
                Assert.Less(Math.Abs(hipsAtWindow[AttackSide.Middle] - (hipsAtWindow[AttackSide.Left] + hipsAtWindow[AttackSide.Right]) / 2f), 0.05f, "centred on a middle attack");
            }
            finally
            {
                effects?.Dispose();
                view?.Dispose();
                Object.DestroyImmediate(driver);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
