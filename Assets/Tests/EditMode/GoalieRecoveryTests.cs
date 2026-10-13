using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// #44: the goalie character never freezes. After each result he plays out his reaction, glides back to his spot and
    /// recovers into the Ready Stance before the result hold ends; in the Ready Stance he shows Stance Life (breathing,
    /// weight shifts, stick taps). Tested through GoalieView (React, Watch, Live, SetProgress), PhysicsShotEffects and
    /// BreakawayView.Step, judged from the gear's world bounds, the mask and the hips.
    /// </summary>
    public class GoalieRecoveryTests
    {
        GameObject _parent;
        /// <summary>The Late Margin limit the Round passes (RoundSettings.LateMarginLimitSeconds).</summary>
        static readonly double LateMarginLimit = new RoundSettings().LateMarginLimitSeconds;
        static readonly Vector3 Eye = new Vector3(0f, 1.5f, -7.5f);
        static readonly Vector3 From = Eye + Vector3.down * 0.35f; // roughly where a Shot starts the puck
        const float Frame = 1f / 60f;
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

        readonly List<GoalieView> _views = new List<GoalieView>(); // every view Make built, disposed in TearDown

        GoalieView Make(out GoalieCharacter character, float facing = 0f)
        {
            character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            _views.Add(view);
            view.Show(true);
            view.Face(facing, facing);
            view.SetProgress(0f);
            return view;
        }

        static ShotResult Result(ShotResultKind kind, double x, double y) => new ShotResult(kind, true, x, y, 100, false);

        static Vector3 NetPoint(double x, double y)
        {
            NetFaceMapping.ToMetres(x, y, out double right, out double up);
            return new Vector3((float)right, (float)up, 0f);
        }

        static readonly string[] Tracked = { GearPiece.LeftPad, GearPiece.RightPad, GearPiece.Blocker, GearPiece.Catcher, GearPiece.Stick, GearPiece.Mask, GearPiece.LeftSkate, GearPiece.RightSkate };

        /// <summary>The tracked gear's centres, then the hips.</summary>
        static Vector3[] Points(GoalieCharacter character)
        {
            var points = new Vector3[Tracked.Length + 1];
            for (int i = 0; i < Tracked.Length; i++)
            {
                Assert.IsTrue(character.GearBounds(Tracked[i], out Bounds b), Tracked[i]);
                points[i] = b.center;
            }
            points[Tracked.Length] = character.Bone(HumanBodyBones.Hips).position;
            return points;
        }

        /// <summary>The two skates among the points (the points the push moves): indexes SkateFrom up to SkateTo.</summary>
        static readonly int SkateFrom = System.Array.IndexOf(Tracked, GearPiece.LeftSkate), SkateTo = System.Array.IndexOf(Tracked, GearPiece.RightSkate) + 1;

        static string Name(int i) => i < Tracked.Length ? Tracked[i] : "hips";

        /// <summary>The largest distance between matching points, and which one.</summary>
        static float Most(Vector3[] a, Vector3[] b, out int which, int from = 0, int to = int.MaxValue)
        {
            float most = 0f;
            which = 0;
            for (int i = from; i < Mathf.Min(a.Length, to); i++)
            {
                float d = Vector3.Distance(a[i], b[i]);
                if (d > most) { most = d; which = i; }
            }
            return most;
        }

        static float Bottom(GoalieCharacter character, string piece)
        {
            Assert.IsTrue(character.GearBounds(piece, out Bounds b));
            return b.min.y;
        }

        // ---- Stance Life --------------------------------------------------------------------------------------------------

        [TestCase(0f, TestName = "Square to the shooter")]
        [TestCase(20f, TestName = "Turned 20 degrees to a side shooter")]
        public void Stance_Life_keeps_the_goalie_moving_a_little_with_his_skates_planted_and_his_blade_never_in_the_ice(float facing)
        {
            GoalieView view = Make(out GoalieCharacter character, facing);
            Vector3[] still = Points(character);
            float blade = Bottom(character, GearPiece.Stick);
            float mask = 0f, gear = 0f, skates = 0f, hips = 0f, lift = 0f, dip = 0f;
            for (float t = 0f; t < 30f; t += 1f / 30f)
            {
                view.Live(t);
                view.SetProgress(0f);
                Vector3[] now = Points(character);
                mask = Mathf.Max(mask, Vector3.Distance(still[5], now[5]));
                for (int i = 0; i < 5; i++) gear = Mathf.Max(gear, Vector3.Distance(still[i], now[i]));
                skates = Mathf.Max(skates, Mathf.Max(Vector3.Distance(still[6], now[6]), Vector3.Distance(still[7], now[7])));
                hips = Mathf.Max(hips, Vector3.Distance(still[8], now[8]));
                float b = Bottom(character, GearPiece.Stick) - blade;
                lift = Mathf.Max(lift, b);
                dip = Mathf.Min(dip, b);
            }
            // Frame to frame (60 fps), as he turns a little too: never a jump.
            float worstStep = 0f, worstAt = 0f;
            int worstPoint = 0;
            Vector3[] last = null;
            for (float t = 0f; t < 60f; t += 1f / 60f)
            {
                float turn = facing + 10f * Mathf.Sin(t * 0.7f);
                view.Face(turn, turn);
                view.Live(t);
                view.SetProgress(0f);
                Vector3[] now = Points(character);
                if (last != null)
                {
                    float step = Most(last, now, out int which);
                    if (step > worstStep) { worstStep = step; worstPoint = which; worstAt = t; }
                }
                last = now;
            }
            TestContext.Out.WriteLine("worst frame step " + worstStep.ToString("F4") + " m (" + Name(worstPoint) + " at " + worstAt.ToString("F2") + " s)");
            Assert.Less(worstStep, 0.012f, "smooth from frame to frame: the " + Name(worstPoint) + " at " + worstAt + " s");
            TestContext.Out.WriteLine("facing " + facing + ": mask " + mask.ToString("F4") + " m, gear " + gear.ToString("F4") + " m, hips " + hips.ToString("F4")
                + " m, skates " + skates.ToString("F4") + " m, blade lift " + lift.ToString("F4") + " m, blade dip " + dip.ToString("F4") + " m");
            Assert.Greater(mask, 0.005f, "the head moves (he breathes and shifts)");
            Assert.Less(mask, 0.04f, "only a little");
            Assert.Greater(hips, 0.01f, "his weight shifts");
            Assert.Less(hips, 0.03f, "only a little");
            Assert.Less(gear, 0.1f, "the gear moves only a little (a stick tap the most)");
            Assert.Less(skates, 0.006f, "the skates stay planted");
            Assert.Greater(lift, 0.02f, "a stick tap lifts the blade");
            Assert.Less(lift, 0.1f, "a small tap");
            Assert.Greater(dip, -0.005f, "the blade never goes into the ice");
        }

        [TestCase(88.0, 82.0, TestName = "Glove save")]
        [TestCase(15.0, 12.0, TestName = "Pad save in the butterfly")]
        public void Fully_reacted_the_goalie_shows_exactly_what_the_Shot_measured_whatever_his_Stance_Life(double x, double y)
        {
            Vector3[] posedA, posedB;
            {
                GoalieView view = Make(out GoalieCharacter character);
                view.Live(0f);
                view.SetProgress(0f);
                view.React(Result(ShotResultKind.Save, x, y), From, null, double.PositiveInfinity, LateMarginLimit);
                view.SetProgress(1f);
                posedA = Points(character);
                Object.DestroyImmediate(character.Root);
            }
            {
                GoalieView view = Make(out GoalieCharacter character);
                view.Live(5.3f); // mid breath, weight to one side
                view.SetProgress(0f);
                view.React(Result(ShotResultKind.Save, x, y), From, null, double.PositiveInfinity, LateMarginLimit);
                view.SetProgress(1f);
                posedB = Points(character);
            }
            Assert.AreEqual(0f, Most(posedA, posedB, out int i), 1e-4f, "fully reacted, the " + Name(i) + " is where it was measured");
        }

        // ---- Recovery -----------------------------------------------------------------------------------------------------

        [TestCase(ShotResultKind.Save, 88.0, 82.0, TestName = "Glove save")]
        [TestCase(ShotResultKind.Save, 12.0, 82.0, TestName = "Blocker save")]
        [TestCase(ShotResultKind.Save, 15.0, 12.0, TestName = "Pad save in the butterfly")]
        [TestCase(ShotResultKind.Save, 50.0, 5.0, TestName = "Stick save")]
        [TestCase(ShotResultKind.Goal, 50.0, 90.0, TestName = "Goal top middle: stick slap")]
        [TestCase(ShotResultKind.Goal, 15.0, 85.0, TestName = "Goal top left: slump")]
        [TestCase(ShotResultKind.Miss, 112.0, 50.0, TestName = "Wide miss")]
        public void After_the_result_the_goalie_is_back_in_the_Ready_Stance_on_his_spot_before_the_hold_ends_with_no_snap(ShotResultKind kind, double x, double y)
        {
            GoalieView view = Make(out GoalieCharacter character);
            Vector3[] stance = Points(character);
            view.React(Result(kind, x, y), From, null, double.PositiveInfinity, LateMarginLimit);
            Vector3 puck = kind == ShotResultKind.Goal ? NetPoint(x, y) + Vector3.forward * 0.6f : NetPoint(x, y) + Vector3.back * 1.5f;
            float furthest = 0f, worstStep = 0f, atEnd = 0f;
            int worstPoint = 0, endPoint = 0;
            float worstAt = 0f;
            Vector3[] last = null;
            // From the Shot: 0.4 s of flight, then the result hold runs to 2.5 s after the puck gets there.
            for (float t = -0.4f; t <= 2.5f + 1e-4f; t += Frame)
            {
                view.Watch(puck, t, kind == ShotResultKind.Goal && t > 0.3f ? t - 0.3f : -1f);
                view.SetProgress(Mathf.Clamp01((t + 0.4f) / 0.2f));
                Vector3[] now = Points(character);
                furthest = Mathf.Max(furthest, Mathf.Abs(now[8].x - stance[8].x));
                if (last != null && t > 1.5f)
                {
                    float step = Most(last, now, out int which);
                    if (step > worstStep) { worstStep = step; worstPoint = which; worstAt = t; }
                }
                if (t >= 2.4f && atEnd == 0f) atEnd = Most(stance, now, out endPoint) + 1e-6f;
                last = now;
            }
            view.SetProgress(0f); // the hold is over: the next Shot's skate-in (or the end screen)
            float boundary = Most(last, Points(character), out int boundaryPoint);
            TestContext.Out.WriteLine(kind + " (" + x + "," + y + "): pushed " + furthest.ToString("F2") + " m, off the stance at 2.4 s " + atEnd.ToString("F4")
                + " m (" + Name(endPoint) + "), worst frame step after 1.5 s " + worstStep.ToString("F4") + " m (" + Name(worstPoint) + " at " + worstAt.ToString("F2") + " s), hold end step " + boundary.ToString("F5"));
            Assert.Less(atEnd, 0.01f, "back in the Ready Stance on his spot 2.4 s after the puck got there: the " + Name(endPoint));
            Assert.Less(boundary, 0.002f, "no snap when the hold ends: the " + Name(boundaryPoint));
            Assert.Less(worstStep, 0.05f, "recovers smoothly (at most 3 m/s): the " + Name(worstPoint) + " at " + worstAt + " s");
            if (kind != ShotResultKind.Save) Assert.LessOrEqual(furthest, GoalieCharacter.BeatenMaxSlide + 0.05f, "a beaten goalie no longer pushes aside (#45)");
        }

        [Test]
        public void After_a_glove_save_the_glove_comes_up_and_after_a_blocker_save_the_blocker_follows_through()
        {
            foreach (var (x, piece, least) in new[] { (88.0, GearPiece.Catcher, 0.06f), (12.0, GearPiece.Blocker, 0.05f) })
            {
                GoalieView view = Make(out GoalieCharacter character);
                view.React(Result(ShotResultKind.Save, x, 82), From, null, double.PositiveInfinity, LateMarginLimit);
                view.Watch(NetPoint(x, 82), 0.1f, -1f);
                view.SetProgress(1f);
                Assert.IsTrue(character.GearBounds(piece, out Bounds at));
                view.Watch(NetPoint(x, 82), 0.7f, -1f);
                view.SetProgress(1f);
                Assert.IsTrue(character.GearBounds(piece, out Bounds on));
                float moved = Vector3.Distance(at.center, on.center);
                TestContext.Out.WriteLine(piece + " follow-through " + moved.ToString("F3") + " m, up " + (on.center.y - at.center.y).ToString("F3"));
                Assert.Greater(moved, least, piece + " follows through");
                Assert.Less(moved, 0.2f, piece + ": a small follow-through");
                if (piece == GearPiece.Catcher) Assert.Greater(on.center.y - at.center.y, 0.05f, "the glove comes up");
                Object.DestroyImmediate(character.Root);
            }
        }

        // ---- Between Shots ------------------------------------------------------------------------------------------------

        [Test]
        public void Played_through_a_whole_Round_the_goalie_never_snaps_between_Shots_or_into_the_end_screen_and_turns_to_each_new_shooter()
        {
            NetBuilder.Build(_parent.transform);
            var cameraObject = new GameObject("Camera (test)", typeof(Camera));
            var driver = new GameObject("Breakaway (test)");
            PhysicsShotEffects effects = null;
            GoalieView view = null;
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                var character = new GoalieCharacter(_parent.transform);
                view = new GoalieView(_parent.transform, character);
                effects = new PhysicsShotEffects(_parent.transform, camera, new SeededRandom(7), view, name => null);
                Physics.SyncTransforms();
                var round = new Round(ScriptedRandom.Picking(AttackSide.Left, AttackSide.Middle, AttackSide.Right,
                    AttackSide.Left, AttackSide.Middle, AttackSide.Right), new RoundSettings(),
                    FakeReach.Saving((x, y) => !((x == 88 && y == 82) || (x == 15 && y == 85))), 0); // two Goals, three Saves, a Miss
                var breakaway = driver.AddComponent<BreakawayView>();
                breakaway.Init(round, camera, view, () => effects.FlightSeconds);
                var shots = new[] { (88.0, 82.0), (50.0, 90.0), (15.0, 12.0), (112.0, 50.0), (12.0, 82.0), (15.0, 85.0) };
                int shot = 0;
                float afterHold = -1f; // seconds since the last hold ended (-1: in a hold, or none yet)
                RoundPhase last = RoundPhase.Idle;
                Vector3[] before = null, beforeThat = null;
                float worstChange = 0f;
                string changeWhere = "";
                float worstHipsBoundary = 0f, worstBodyBoundary = 0f, worstSkateBoundary = 0f, worstSkateChange = 0f;
                string bodyWhere = "", skateWhere = "", skateChangeWhere = "";
                float worstBoundary = 0f, worstTurn = 0f, lastFacing = 0f, worstLate = 0f;
                string worstWhere = "", lateWhere = "";
                var boundaries = new List<string>();
                var fade = new ShotFade();
                var notBlackSnaps = new List<string>();
                round.StartRound();
                for (int frame = 0; frame < 60 * 80 && !(round.Phase == RoundPhase.RoundOver && afterHold >= 1.5f); frame++)
                {
                    for (int step = 0; step < 2; step++) // a frame is 1/60 s: two physics steps of 1/120 s
                    {
                        Physics.Simulate(PhysicsShot.StepSeconds);
                        effects.AfterPhysicsStep(PhysicsShot.StepSeconds);
                    }
                    round.Tick(Frame);
                    double black = fade.Step(round, Frame); // stepped as BreakawayView steps its own: how black the screen is this frame
                    view.Show(true);
                    if (round.Phase == RoundPhase.ShootingWindow && round.PhaseSecondsElapsed > 0.5 && shot < shots.Length)
                    {
                        round.SubmitShot(shots[shot].Item1, shots[shot].Item2, 100, false);
                        shot++;
                    }
                    RoundPhase phase = round.Phase;
                    effects.Update(round, Frame);
                    breakaway.Step(Frame * Time.timeScale, Frame);
                    Vector3[] now = Points(character);
                    float facing = view.ShownFacing;
                    if (before != null && last == RoundPhase.ResultHold && phase != RoundPhase.ResultHold)
                    {
                        float step = Most(before, now, out int which);
                        boundaries.Add(phase + " after Shot " + shot + ": " + step.ToString("F4") + " m (" + Name(which) + "), turn " + Mathf.Abs(facing - lastFacing).ToString("F2") + " deg");
                        if (step > worstBoundary) { worstBoundary = step; worstWhere = boundaries[boundaries.Count - 1]; }
                        // The body is every point but the skates: the gear before them and the hips after them.
                        float gearStep = Most(before, now, out int bodyWhich, 0, SkateFrom), hipsStep = Most(before, now, out int hipsWhich, SkateTo);
                        float bodyStep = Mathf.Max(gearStep, hipsStep);
                        if (hipsStep > gearStep) bodyWhich = hipsWhich;
                        float skateStep = Most(before, now, out int skateWhich, SkateFrom, SkateTo);
                        // Only a snap the player can SEE counts (#131): the cut into SkateIn is under full black since #129 (ShotFade).
                        if (hipsStep > 0.03f && black < 1.0) notBlackSnaps.Add(phase + " after Shot " + shot + ": " + hipsStep.ToString("F4") + " m, fade " + black.ToString("F2"));
                        if (black < 1.0) worstHipsBoundary = Mathf.Max(worstHipsBoundary, hipsStep);
                        if (black < 1.0 && bodyStep > worstBodyBoundary) { worstBodyBoundary = bodyStep; bodyWhere = phase + " after Shot " + shot + ": the " + Name(bodyWhich); }
                        if (black < 1.0 && skateStep > worstSkateBoundary) { worstSkateBoundary = skateStep; skateWhere = phase + " after Shot " + shot + ": the " + Name(skateWhich); }
                        afterHold = 0f;
                    }
                    else if (afterHold >= 0f) afterHold += Frame;
                    if (phase == RoundPhase.ResultHold) afterHold = -1f;
                    // The next skate-in's first 1.5 s (or the end screen's): smooth, and turning gently to the new shooter.
                    if (before != null && afterHold >= 0f && afterHold < 1.5f)
                    {
                        float move = Most(before, now, out int movedMost);
                        if (black < 1.0 && move > worstLate) { worstLate = move; lateWhere = phase + " after Shot " + shot + ", " + afterHold.ToString("F2") + " s in: the " + Name(movedMost); }
                        worstTurn = Mathf.Max(worstTurn, Mathf.Abs(facing - lastFacing));
                        if (beforeThat != null && black < 1.0)
                            for (int p = 0; p < now.Length; p++)
                            {
                                float change = (now[p] - 2f * before[p] + beforeThat[p]).magnitude;
                                bool skate = p >= SkateFrom && p < SkateTo;
                                if (!skate && change > worstChange) { worstChange = change; changeWhere = phase + " after Shot " + shot + ", " + afterHold.ToString("F2") + " s in: the " + Name(p); }
                                if (skate && change > worstSkateChange) { worstSkateChange = change; skateChangeWhere = phase + " after Shot " + shot + ", " + afterHold.ToString("F2") + " s in: the " + Name(p); }
                            }
                    }
                    beforeThat = before;
                    before = now;
                    lastFacing = facing;
                    last = phase;
                }
                TestContext.Out.WriteLine(string.Join("\n", boundaries));
                TestContext.Out.WriteLine("worst frame step settling " + worstLate.ToString("F4") + " m, worst turn " + worstTurn.ToString("F2") + " deg/frame");
                Assert.AreEqual(6, shot, "all six Shots taken");
                Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
                Assert.AreEqual(6, boundaries.Count, "five skate-ins and the end screen");
                // (#126) Only the two skates may add the push: the pushing skate goes out as the skate-in's first shuffle starts, at most its
                // full reach (PushOutMetres and PushBackMetres) over PushInSeconds, a frame's share of it (3.5 cm). Every other point keeps the old limits.
                float pushStep = (float)((GoalieSkatePush.PushOutMetres + GoalieSkatePush.PushBackMetres) / PushPose.PushInSeconds * Frame);
                Assert.Less(worstHipsBoundary, 0.03f, "no hips snap as a hold ends (a frame under full black is not counted)");
                Assert.IsEmpty(notBlackSnaps, "every hips step over 0.03 m at a cut is under full black (ShotFade amount 1)");
                Assert.Less(worstBodyBoundary, 0.03f, "no snap as a hold ends (only the start of the turn to the new shooter): " + bodyWhere);
                Assert.Less(worstSkateBoundary, 0.03f + pushStep, "no skate snap as a hold ends (the push may add a step): " + skateWhere);
                // 0.08, not 0.05 (#125): the skate-push model speeds up at PushAccel 40 m/s2 (3.2 m/s, 5.3 cm a frame, 0.08 s in; the
                // Catcher also swings as he turns), not the old 3 m/s cap. Still a smooth start with no snap (the next test is the turn).
                Assert.Less(worstLate, 0.08f, "smooth from the end of the hold into the next skate-in: " + lateWhere);
                // Step to step he may change speed by at most the push's PushAccel * dt^2 (40 / 3600 = 1.1 cm: the second difference of
                // his position), plus the Catcher's swing as the turn to the new shooter starts, which the boundary assert above
                // already allows at 3 cm. Measured worst: 2.6 cm (the Catcher, on the first frame of the skate-in). Unlike the 8 cm
                // step bound, this still catches a snap: a snap is a jump of one frame's step, not a change of speed.
                float allowed = (float)(new RoundSettings().PushAccelMetresPerSecondSquared * Frame * Frame) + 0.03f;
                Assert.Less(worstChange, allowed, "his speed changes smoothly from step to step: " + changeWhere);
                // (#126) The pushing skates start out at that same pace from rest, so their second difference may be one more pushStep.
                Assert.Less(worstSkateChange, allowed + pushStep, "his skates change speed smoothly from step to step: " + skateChangeWhere);
                Assert.Less(worstTurn, 2f, "turns to the next shooter at most 2 degrees a frame (no snap)");
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
