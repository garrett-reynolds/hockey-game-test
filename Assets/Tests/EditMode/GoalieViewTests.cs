using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class GoalieViewTests
    {
        /// <summary>The Late Margin limit the Round passes (RoundSettings.LateMarginLimitSeconds).</summary>
        static readonly double LateMarginLimit = new RoundSettings().LateMarginLimitSeconds;

        GameObject _parent;

        [SetUp]
        public void SetUp() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void TearDown()
        {
            foreach (GoalieView v in _views) v?.Dispose();
            _views.Clear();
            Object.DestroyImmediate(_parent);
        }

        readonly List<GoalieView> _views = new List<GoalieView>(); // every view a test built, disposed in TearDown

        Transform Group(string name) => _parent.transform.Find(name);

        static bool AnyDrawn(Transform group)
        {
            foreach (Renderer r in group.GetComponentsInChildren<Renderer>())
                if (r.enabled) return true;
            return false;
        }

        [Test]
        public void With_the_character_the_goalie_block_is_not_drawn_and_the_character_shows_instead()
        {
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            _views.Add(view);
            view.Show(true);

            Assert.IsFalse(AnyDrawn(Group(GoalieView.BlockName)), "the goalie block is hidden");
            Assert.IsTrue(character.Root.activeInHierarchy, "the character shows");

            view.Show(false);
            Assert.IsFalse(character.Root.activeInHierarchy, "the character hides with the goalie");
        }

        [Test]
        public void Easy_mode_glow_still_shows_with_the_character()
        {
            var view = new GoalieView(_parent.transform, new GoalieCharacter(_parent.transform));
            _views.Add(view);
            view.Show(true);
            view.ShowGaps(NetFaceGaps.Where((x, y) => x > 70));
            Transform glow = Group("Gap glow");
            Assert.IsTrue(glow.gameObject.activeInHierarchy);
            Assert.IsTrue(AnyDrawn(glow));
        }

        [Test]
        public void Without_the_character_model_the_goalie_block_shows()
        {
            var character = new GoalieCharacter(_parent.transform, name => null);
            var view = new GoalieView(_parent.transform, character);
            _views.Add(view);
            view.Show(true);
            Assert.IsTrue(AnyDrawn(Group(GoalieView.BlockName)));

            var noCharacter = new GoalieView(_parent.transform);
            _views.Add(noCharacter);
            Assert.DoesNotThrow(() => noCharacter.Show(true));
        }

        [Test]
        public void The_goalie_block_stands_across_the_Net_Face_where_the_goalie_stands_and_hides_with_him()
        {
            var view = new GoalieView(_parent.transform);
            _views.Add(view);
            view.Show(true);
            Transform block = Group(GoalieView.BlockName);
            Vector3 before = block.position;
            view.Stand(new Vector3(0.3f, 0f, -0.6f));
            Assert.IsTrue(AnyDrawn(block));
            Assert.AreEqual(0.3f, block.position.x, 1e-5f, "across with his spot");
            Assert.AreEqual(before.y, block.position.y, 1e-5f, "on the ice as before");
            Assert.AreEqual(before.z, block.position.z, 1e-5f, "still just in front of the Net Face");
            Assert.AreEqual(0.3f, view.ShownSpot.x, 1e-5f);

            view.Stand(new Vector3(-0.25f, 0f, -0.6f));
            Assert.AreEqual(-0.25f, block.position.x, 1e-5f);

            view.Show(false);
            Assert.IsFalse(block.gameObject.activeInHierarchy, "hidden with the goalie");
        }

        [Test]
        public void The_goalie_squares_up_to_the_shooter_from_where_it_stands_in_the_crease()
        {
            // 0.4 m out from the Net Face: a shooter 2.7 m to the left and 9 m out is 8.6 m ahead of the goalie.
            Assert.AreEqual(17.43f, GoalieCharacter.FacingToward(new Vector3(-2.7f, 1.5f, -9f)), 0.01f);
            Assert.AreEqual(-17.43f, GoalieCharacter.FacingToward(new Vector3(2.7f, 1.5f, -9f)), 0.01f);
            Assert.AreEqual(0f, GoalieCharacter.FacingToward(new Vector3(0f, 1.5f, -3f)), 0.01f);
        }

        [Test]
        public void The_goalie_turns_about_its_own_spot_to_face_the_shooter_and_finishes_the_turn_as_it_reacts()
        {
            var reference = new GoalieCharacter(new GameObject("Reference").transform);
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            _views.Add(view);
            Transform hips = character.Bone(HumanBodyBones.Hips), referenceHips = reference.Bone(HumanBodyBones.Hips);
            view.Show(true);
            reference.ShowReaction(0f);

            void AssertTurned(float degrees, string why)
            {
                Assert.AreEqual(0f, Quaternion.Angle(Quaternion.AngleAxis(degrees, Vector3.up) * referenceHips.rotation, hips.rotation), 0.05f, why + ": turned");
                Assert.AreEqual(0f, new Vector2(hips.position.x - referenceHips.position.x, hips.position.z - referenceHips.position.z).magnitude, 0.001f, why + ": on its own spot");
            }

            view.Face(12f, 18f);
            view.SetProgress(0f);
            AssertTurned(12f, "Ready Stance, facing where the goalie has turned so far");
            view.SetProgress(0.5f);
            AssertTurned(15f, "half way through the reaction");
            view.SetProgress(1f);
            AssertTurned(18f, "reacted, squared up to the shooter");
            view.Face(0f, 0f);
            AssertTurned(0f, "straight out again");
            Object.DestroyImmediate(reference.Root.transform.parent.gameObject);
        }

        [Test]
        public void Squared_up_the_goalie_shoulders_lie_square_across_the_line_to_the_shooter()
        {
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            _views.Add(view);
            view.Show(true);
            view.SetProgress(0f);
            Transform left = character.Bone(HumanBodyBones.LeftUpperArm), right = character.Bone(HumanBodyBones.RightUpperArm);
            Transform hips = character.Bone(HumanBodyBones.Hips);
            foreach (var shooter in new[] { new Vector3(-2.7f, 1.5f, -9f), new Vector3(2.7f, 1.5f, -9f), new Vector3(-8.2f, 1.5f, -15.5f) })
            {
                float SquareOff()
                {
                    Vector3 shoulders = Vector3.ProjectOnPlane(right.position - left.position, Vector3.up).normalized;
                    Vector3 toShooter = Vector3.ProjectOnPlane(shooter - hips.position, Vector3.up).normalized;
                    return Mathf.Abs(Vector3.Dot(shoulders, toShooter));
                }
                view.Face(0f, 0f);
                Assert.Greater(SquareOff(), 0.2f, shooter + ": straight out, the shoulders are not square to a side shooter");
                float facing = GoalieCharacter.FacingToward(shooter);
                view.Face(facing, facing);
                Assert.Less(SquareOff(), 0.03f, shooter + ": turned, they are");
            }
        }

        static ShotResult Result(ShotResultKind kind, double x, double y) => new ShotResult(kind, true, x, y, 100, false);

        static Vector3 NetPoint(double x, double y)
        {
            NetFaceMapping.ToMetres(x, y, out double right, out double up);
            return new Vector3((float)right, (float)up, 0f);
        }

        [Test]
        public void Without_a_Save_Reaction_from_the_race_a_save_reaction_comes_from_the_Shot_point()
        {
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            _views.Add(view);
            var from = new Vector3(0f, 1.15f, -9f);
            view.Show(true);
            foreach (var (x, y, piece) in new[] { (85.0, 85.0, GearPiece.Catcher), (15.0, 85.0, GearPiece.Blocker), (15.0, 10.0, GearPiece.RightPad),
                (85.0, 30.0, GearPiece.LeftPad), (50.0, 5.0, GearPiece.Stick), (50.0, 55.0, GearPiece.Body) })
            {
                view.SetProgress(0f);
                view.React(Result(ShotResultKind.Save, x, y), from, null, double.PositiveInfinity, LateMarginLimit);
                Assert.AreEqual(piece, view.ReactionPiece, "(" + x + ", " + y + ")");
            }
        }

        [Test]
        public void The_goalie_holds_the_Ready_Stance_until_the_Shot_then_the_set_blend_moves_it_into_the_reaction()
        {
            var reference = new GoalieCharacter(new GameObject("Reference").transform);
            reference.ShowReaction(0f);
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            _views.Add(view);
            view.Show(true);
            Transform hand = character.Bone(HumanBodyBones.LeftHand), referenceHand = reference.Bone(HumanBodyBones.LeftHand);

            view.React(Result(ShotResultKind.Save, 90, 80), new Vector3(0f, 1.15f, -9f), null, double.PositiveInfinity, LateMarginLimit); // the Shot frame: a glove save
            view.SetProgress(0f);
            Assert.AreEqual(0f, Vector3.Distance(referenceHand.position, hand.position), 0.001f, "still in the Ready Stance on the Shot frame");

            view.SetProgress(1f);
            Assert.IsTrue(character.ReactionGearBounds(GearPiece.Catcher, out Bounds measured));
            Assert.IsTrue(character.GearBounds(GearPiece.Catcher, out Bounds shown));
            Assert.AreEqual(0f, Vector3.Distance(measured.center, shown.center), 0.001f, "fully reacted, the catcher is where the save was measured");
            Vector3 reached = hand.position;
            Assert.Greater(Vector3.Distance(referenceHand.position, reached), 0.1f, "the catching hand has moved to the puck");

            view.SetProgress(0.5f);
            float part = Vector3.Distance(referenceHand.position, hand.position);
            Assert.Greater(part, 0.01f);
            Assert.Less(part, Vector3.Distance(referenceHand.position, reached) - 0.01f, "half way, part of the way there");

            view.React(new ShotResult(ShotResultKind.Miss, false, 0, 0, 0, false), Vector3.zero, null, double.PositiveInfinity, LateMarginLimit); // too late: no Shot
            Assert.IsNull(view.ReactionPiece);
            Object.DestroyImmediate(reference.Root.transform.parent.gameObject);
        }

        // #216: a374060ef43e4d5e899a5067c2d0d3c8-trace.json.shot.json, accepted live Save.
        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void The_recorded_depth_pad_save_keeps_the_immediate_React_hip_step_below_the_play_check_jump_limit()
        {
            const double phaseSeconds = 0.054803489893674895;
            const double reactionSeconds = 0.29455858679700575;
            const double x = 20.833333333333332, y = 31.25, speedKmh = 100.0;
            // All fields from the shot fixture, including live settings that differ from defaults.
            var settings = new RoundSettings
            {
                SkateInSeconds = 2.5, SlowDownSeconds = 1.2, ShootingWindowSeconds = 3.0,
                ResultHoldSeconds = 2.5, ClipHoldSeconds = 1.5, ReplayOn = true, TestMode = false,
                TestModeHoldSeconds = 1.0, WhistleDeadSeconds = 0.3, WhistleCapSeconds = 3.0,
                WhistleVerySlowMetresPerSecond = 0.3, ViewFieldOfViewDegrees = 60.0, ShotStartMarginMetres = 0.05,
                ReactionSeconds = 0.3, ReactionWobbleSeconds = 0.03, HandMetresPerSecond = 3.0,
                LegMetresPerSecond = 3.0, MaxPushMetres = 0.3, ReachLimitMetres = 0.595,
                ShuffleMaxMetres = 0.3, PushAccelMetresPerSecondSquared = 40.0, GlideDecelMetresPerSecondSquared = 20.0,
                NormalDepthMetres = 0.2, CheatMetres = 0.3, LateSettleMetres = 0.3, LateSettleSeconds = 1.0,
                LateSettles = false, GapThinGearReachMetres = 0.01, LateMarginLimitSeconds = 0.05,
                HeadOnPuck = true, HeadShoulderShare = 0.15, SkateInShuffles = true, ShuffleStartMetres = 0.08,
                ShuffleSeconds = 0.3, StillOnceSet = true, SetStillness = 0.85, SetDropMetres = 0.03
            };
            var character = new GoalieCharacter(_parent.transform);
            Assert.IsTrue(character.Present, "the real goalie rig must load");
            var view = new GoalieView(_parent.transform, character);
            _views.Add(view);
            view.Show(true);
            var reach = new GoalieCharacterReach(character, settings);
            ShotRace race = ShotRace.At(HockeyGame.Core.SetUp.Depth, AttackSide.Middle, phaseSeconds,
                x, y, speedKmh, reactionSeconds, reach, settings);
            Assert.IsTrue(race.IsSave, "the recorded race was a Save");
            Assert.IsTrue(race.InTheWay, "the recorded race already blocked the puck");
            Assert.AreEqual(SaveReaction.Pads, race.SaveReaction, "the recorded race selected Pads");
            Assert.AreEqual(0.0, race.MoveSeconds, "the recorded race had no timed move");
            Assert.AreEqual(0.0, race.ReachSeconds, "the recorded reach was immediate");
            Assert.AreEqual(0.17695650388636722, race.FlightSeconds, 1e-8, "match the recorded flight");
            Assert.AreEqual(-0.17695650388636722, race.LateMarginSeconds, 1e-8, "match the recorded margin");
            Assert.AreEqual(0.0, race.ShotStart.X);
            Assert.AreEqual(0.0125, race.ShotStart.Y, 1e-8);
            Assert.AreEqual(-4.8724599601258136, race.ShotStart.Z, 1e-8);
            Assert.AreEqual(0.0, race.Spot.X);
            Assert.AreEqual(0.0, race.Spot.Y);
            Assert.AreEqual(0.0, race.Spot.Z);
            var from = new Vector3((float)race.ShotStart.X, (float)race.ShotStart.Y, (float)race.ShotStart.Z);
            view.StandFor(race);
            view.Face(0f, view.PosedFacing());
            view.ShowBeforeTheShot(from,
                (float)BeforeTheShot.Watching(RoundPhase.ShootingWindow, phaseSeconds, settings),
                (float)BeforeTheShot.Settled(RoundPhase.ShootingWindow, phaseSeconds, settings), settings);
            view.SetProgress(1f); // candidate retained progress from the preceding frame; the private live value is unproved
            Transform hips = character.Bone(HumanBodyBones.Hips);
            Vector3 before = hips.position, beforeRoot = character.Root.transform.position;
            view.React(new ShotResult(ShotResultKind.Save, true, x, y, speedKmh, false), from,
                race.SaveReaction, race.LateMarginSeconds, settings.LateMarginLimitSeconds);
            float step = Vector3.Distance(before, hips.position);
            TestContext.WriteLine($"#216 recorded React hips {before.ToString("F6")} -> {hips.position.ToString("F6")}; root {beforeRoot.ToString("F6")} -> {character.Root.transform.position.ToString("F6")}; immediate step={step:F6} m");
            Assert.LessOrEqual(step, 6f / 60f,
                "#216: the recorded Save React step must fit the existing 6 m/s hip limit at 60 frames/s");
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void A_long_game_frame_blends_the_final_drawn_pose_on_wall_time_without_moving_physics_targets()
        {
            const double wallStep = 0.0535354614, gameStep = 0.4082098007;
            var character = new GoalieCharacter(_parent.transform);
            Assert.IsTrue(character.Present);
            var view = new GoalieView(_parent.transform, character);
            _views.Add(view);
            view.Show(true);
            var from = new Vector3(1.500582626f, 0.0125f, -5.001942087f);
            view.Stand(new Vector3(0.36f, 0f, -0.2f));
            view.Face(0f, GoalieCharacter.FacingToward(from));
            view.SetProgress(0f);
            view.ShowDrawnPose(10.0);
            Vector3 previous = character.Bone(HumanBodyBones.Hips).position;
            view.SetProgress(0.7990120649f); // physics ahead must not replace the last drawn pose
            view.React(new ShotResult(ShotResultKind.Save, true, 29.166666666666668, 6.25, 100, false),
                from, SaveReaction.Pads, -0.18471247276657579, LateMarginLimit);
            float target = (float)Breakaway.GoalieSet(RoundPhase.ResultHold, gameStep, true, 0.1940816343);
            Assert.AreEqual(1f, target, "the long game frame reaches the full reaction target");
            view.SetProgress(target);
            view.MoveHitShapes();
            Assert.Greater(Vector3.Distance(previous, character.Bone(HumanBodyBones.Hips).position) / wallStep, 6.0,
                "the full long-frame target must expose the captured jump before the drawn blend");
            Transform[] bones = character.Root.GetComponentsInChildren<Transform>();
            var positions = new Vector3[bones.Length];
            var rotations = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) { positions[i] = bones[i].position; rotations[i] = bones[i].rotation; }
            Collider[] hits = _parent.transform.Find(GoalieHitShapes.RootName).GetComponentsInChildren<Collider>();
            Assert.Greater(hits.Length, 0);
            var hitPositions = new Vector3[hits.Length];
            var hitRotations = new Quaternion[hits.Length];
            for (int i = 0; i < hits.Length; i++) { hitPositions[i] = hits[i].transform.position; hitRotations[i] = hits[i].transform.rotation; }
            view.ShowDrawnPose(10.0 + wallStep);
            double speed = Vector3.Distance(previous, character.Bone(HumanBodyBones.Hips).position) / wallStep;
            Assert.LessOrEqual(speed, 6.0, "the original final-drawn wall-time limit applies");
            for (int i = 0; i < hits.Length; i++)
            {
                Assert.AreEqual(hitPositions[i], hits[i].transform.position, "drawn blend must not move a hit shape");
                Assert.AreEqual(hitRotations[i], hits[i].transform.rotation);
            }
            view.SetProgress(target);
            view.MoveHitShapes();
            for (int i = 0; i < bones.Length; i++)
            {
                Assert.AreEqual(positions[i], bones[i].position, bones[i].name);
                Assert.AreEqual(rotations[i], bones[i].rotation, bones[i].name);
            }
            for (int i = 0; i < hits.Length; i++)
            {
                Assert.AreEqual(hitPositions[i], hits[i].transform.position, hits[i].name);
                Assert.AreEqual(hitRotations[i], hits[i].transform.rotation, hits[i].name);
            }
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void A_paused_drawn_clock_keeps_the_reaction_at_the_same_pose()
        {
            bool wasPaused = PauseSwitch.IsPaused;
            float oldScale = Time.timeScale;
            bool oldAudioPause = AudioListener.pause;
            try
            {
                PauseSwitch.Set(true);
                double stopped = PauseSwitch.DrawnSeconds;
                var character = new GoalieCharacter(_parent.transform);
                var view = new GoalieView(_parent.transform, character);
                _views.Add(view);
                view.Show(true);
                view.SetProgress(0f);
                view.ShowDrawnPose(stopped);
                Vector3 before = character.Bone(HumanBodyBones.Hips).position;
                view.React(Result(ShotResultKind.Save, 15, 8), new Vector3(0f, 0.0125f, -5f),
                    SaveReaction.Pads, double.PositiveInfinity, LateMarginLimit);
                view.SetProgress(1f);
                Assert.AreEqual(stopped, PauseSwitch.DrawnSeconds, "wall time must stop for the full pause");
                view.ShowDrawnPose(PauseSwitch.DrawnSeconds);
                Assert.AreEqual(before, character.Bone(HumanBodyBones.Hips).position);

                // A long paused span adds no time to the same public drawn-pose seam after Resume.
                var clock = new PauseClock();
                clock.Pause(stopped);
                clock.Resume(stopped + 30.0);
                view.SetProgress(1f);
                view.ShowDrawnPose(clock.Now(stopped + 30.05));
                Assert.LessOrEqual(Vector3.Distance(before, character.Bone(HumanBodyBones.Hips).position) / 0.05, 6.0);
            }
            finally
            {
                PauseSwitch.Set(wasPaused);
                Time.timeScale = oldScale;
                AudioListener.pause = oldAudioPause;
            }
        }

        static IEnumerable<(string, Vector3)> AllEyes()
        {
            foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
                foreach (var eye in ShooterEyes.Of(side)) yield return (side + " " + eye.Item1, eye.Item2);
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void A_goal_or_miss_reach_never_touches_the_puck()
        {
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            _views.Add(view);
            view.Show(true);
            var failures = new List<string>();
            int shots = 0, inTheWay = 0;
            foreach (var (eyeName, eye) in AllEyes())
            {
                float facing = GoalieCharacter.FacingToward(eye);
                view.Face(facing, facing);
                Vector3 from = eye + Vector3.down * 0.35f;
                for (double x = -10; x <= 110; x += 15)
                for (double y = 5; y <= 110; y += 15)
                {
                    bool inside = x >= 0 && x <= 100 && y <= 100;
                    view.SetProgress(0f);
                    // A line through the goalie standing in the Ready Stance is no Goal or Miss: the puck would hit him there (#45: he no longer
                    // pushes 0.8 m out of its way). Only the lines the Ready Stance leaves clear are judged.
                    if (GoalieTriangles.PuckTouches(GoalieTriangles.Of(character), from, NetPoint(x, y))) { inTheWay++; continue; }
                    shots++;
                    view.React(Result(inside ? ShotResultKind.Goal : ShotResultKind.Miss, x, y), from, null, double.PositiveInfinity, LateMarginLimit);
                    Assert.IsNull(view.ReactionPiece);
                    view.SetProgress(1f);
                    if (GoalieTriangles.PuckTouches(GoalieTriangles.Of(character), from, NetPoint(x, y)))
                        failures.Add(eyeName + " (" + x + ", " + y + ") " + (inside ? "goal" : "miss") + " " + SaveReactions.For(x, y) + " slide " + character.ReactionSlideMetres.ToString("0.00"));
                }
            }
            Assert.IsEmpty(failures, failures.Count + " of " + shots + " touch the puck (" + inTheWay + " lines skipped: in the way in the Ready Stance):\n" + string.Join("\n", failures));
        }

        [Test]
        public void A_goal_or_miss_reach_never_pushes_the_goalie_away_from_the_puck_and_moves_him_little()
        {
            var character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            _views.Add(view);
            view.Show(true);
            var failures = new List<string>();
            int shots = 0;
            float most = 0f;
            foreach (var (eyeName, eye) in AllEyes())
            {
                float facing = GoalieCharacter.FacingToward(eye);
                view.Face(facing, facing);
                Vector3 from = eye + Vector3.down * 0.35f;
                for (double x = -10; x <= 110; x += 15)
                for (double y = 5; y <= 110; y += 15)
                {
                    bool inside = x >= 0 && x <= 100 && y <= 100;
                    shots++;
                    view.SetProgress(0f);
                    float hipsX = character.Bone(HumanBodyBones.Hips).position.x;
                    view.React(Result(inside ? ShotResultKind.Goal : ShotResultKind.Miss, x, y), from, null, double.PositiveInfinity, LateMarginLimit);
                    float slide = character.ReactionSlideMetres;
                    most = Mathf.Max(most, Mathf.Abs(slide));
                    bool away = (hipsX - NetPoint(x, y).x) * slide > 0f; // the same side as the hips stand of the puck
                    if (away || Mathf.Abs(slide) > GoalieCharacter.BeatenMaxSlide + 0.0001f)
                        failures.Add(eyeName + " (" + x + ", " + y + ") " + (inside ? "goal" : "miss") + " slide " + slide.ToString("0.00"));
                }
            }
            Assert.IsEmpty(failures, failures.Count + " of " + shots + " push away or too far (largest " + most.ToString("0.00") + " m):\n" + string.Join("\n", failures));
        }

        // ---- The skate push (#126): the view steps it once per frame (ShowPush) and lays it on every pose after the reaction ----

        const double PushFrame = 1.0 / 60;

        GoalieView PushView(out GoalieCharacter character)
        {
            character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "needs the goalie model");
            var view = new GoalieView(_parent.transform, character);
            _views.Add(view);
            view.Show(true);
            view.SetProgress(0f);
            return view;
        }

        /// <summary>The foot bone with the larger world X (Skate.Right).</summary>
        static Transform RightSkate(GoalieCharacter c)
        {
            Transform l = c.Bone(HumanBodyBones.LeftFoot), r = c.Bone(HumanBodyBones.RightFoot);
            return l.position.x > r.position.x ? l : r;
        }

        static void Settle(GoalieView view, PushShown shown)
        {
            for (int i = 0; i < 20; i++) view.ShowPush(shown, PushFrame);
        }

        [Test]
        public void ShowPush_moves_a_foot_and_PoseForPhysics_keeps_it()
        {
            GoalieView view = PushView(out GoalieCharacter character);
            float rest = RightSkate(character).position.x;

            Settle(view, new PushShown(PushKind.Shuffle, Skate.Right, PushPhase.Push, 1));
            view.SetProgress(0f);
            view.MoveHitShapes();
            float out1 = RightSkate(character).position.x;
            PushPoseMoment moment = view.PushMoment;
            Assert.Greater(out1 - rest, 0.05f, "the far-side skate is out toward +X");
            Assert.AreEqual(1.0, moment.PushOut, 1e-9);

            view.SetProgress(0f); // posed again with no ShowPush in between (BreakawayView.PoseForPhysics): the same moment
            view.MoveHitShapes();
            Assert.AreEqual(out1, RightSkate(character).position.x, 1e-6f, "the foot stays out");
            Assert.AreEqual(moment.PushOut, view.PushMoment.PushOut, 0.0, "the pose was not stepped");
            Assert.AreEqual(moment.LeadTurn, view.PushMoment.LeadTurn, 0.0);
        }

        [Test]
        public void A_Save_Reaction_that_starts_mid_push_blends_the_push_down_with_no_one_frame_jump()
        {
            GoalieView view = PushView(out GoalieCharacter character);
            var shown = new PushShown(PushKind.Shuffle, Skate.Right, PushPhase.Push, 1);
            Settle(view, shown);
            view.SetProgress(0f);
            Transform skate = RightSkate(character);
            Transform hips = character.Bone(HumanBodyBones.Hips);
            Vector3 foot = skate.position, hipsAt = hips.position;
            float perFrame = (GoalieSkatePush.PushOutMetres + GoalieSkatePush.PushBackMetres) / (float)PushPose.PushInSeconds * (float)PushFrame;

            view.React(Result(ShotResultKind.Save, 90, 80), new Vector3(0f, 1.15f, -9f), null, double.PositiveInfinity, LateMarginLimit);
            float worstFoot = 0f, worstHips = 0f;
            for (int i = 0; i < 30; i++)
            {
                view.ShowPush(shown, PushFrame);
                view.SetProgress(0f); // set 0: the reaction is not in yet, so only the push changes
                worstFoot = Mathf.Max(worstFoot, Vector3.Distance(foot, skate.position));
                worstHips = Mathf.Max(worstHips, Vector3.Distance(hipsAt, hips.position));
                foot = skate.position;
                hipsAt = hips.position;
            }
            Assert.LessOrEqual(worstFoot, perFrame, "the push foot moves at most the push's own per-frame speed (no 0.25 m jump)");
            Assert.Less(worstHips, 0.05f, "the hips lean and drop come down over a few frames, not one");
        }

        [Test]
        public void The_legs_only_push_share_of_a_Save_Reaction_is_reset_when_the_goalie_is_hidden()
        {
            GoalieView view = PushView(out GoalieCharacter character);
            var shown = new PushShown(PushKind.Shuffle, Skate.Right, PushPhase.Push, 1);
            Assert.AreEqual(1f, view.BodyShare, "the full push before any Shot");
            view.React(Result(ShotResultKind.Save, 90, 80), new Vector3(0f, 1.15f, -9f), null, double.PositiveInfinity, LateMarginLimit);
            for (int i = 0; i < 30; i++) view.ShowPush(shown, PushFrame);
            Assert.AreEqual(0f, view.BodyShare, 1e-4f, "legs only after a Save Reaction");

            view.Show(false);
            Assert.AreEqual(1f, view.BodyShare, "hidden: the full push again, not left at 0");
        }

        [Test]
        public void After_a_Save_the_next_Shots_skate_in_push_has_the_full_body_share()
        {
            GoalieView view = PushView(out GoalieCharacter character);
            var shown = new PushShown(PushKind.Shuffle, Skate.Right, PushPhase.Push, 1);
            view.React(Result(ShotResultKind.Save, 90, 80), new Vector3(0f, 1.15f, -9f), null, double.PositiveInfinity, LateMarginLimit);
            for (int i = 0; i < 30; i++) view.ShowPush(shown, PushFrame);
            Assert.AreEqual(0f, view.BodyShare, 1e-4f, "recovery after a Save keeps the legs-only push");
            Assert.IsNotNull(view.ReactionPiece);

            view.NewShot(); // the cut into the next Shot's skate-in
            Assert.AreEqual(1f, view.BodyShare, "the full push at once");
            Assert.IsNull(view.ReactionPiece, "the last Shot's reaction is over");
            for (int i = 0; i < 30; i++) view.ShowPush(shown, PushFrame);
            Assert.AreEqual(1f, view.BodyShare, 1e-4f, "and it stays full through the skate-in's pushes");
        }

        [Test]
        public void During_the_Done_frames_after_a_push_the_same_foot_stays_out_and_the_other_does_not_move()
        {
            GoalieView view = PushView(out GoalieCharacter character);
            Transform l = character.Bone(HumanBodyBones.LeftFoot), r = character.Bone(HumanBodyBones.RightFoot);
            Transform small = l.position.x < r.position.x ? l : r, large = small == l ? r : l;
            float smallRest = small.position.x, largeRest = large.position.x;

            Settle(view, new PushShown(PushKind.Shuffle, Skate.Right, PushPhase.Push, 1)); // a move toward -X: the larger-X foot pushes
            int outFrames = 0;
            for (int i = 0; i < 20; i++)
            {
                view.ShowPush(PushShown.None, PushFrame); // hold, then return
                view.SetProgress(0f);
                if (view.PushMoment.PushOut > 0.0)
                {
                    outFrames++;
                    Assert.Greater(large.position.x - largeRest, 0.0f, "frame " + i + ": the pushing foot stays out toward +X");
                    Assert.AreEqual(smallRest, small.position.x, 1e-5f, "frame " + i + ": the other foot does not move");
                }
            }
            Assert.Greater(outFrames, 10, "the hold and the return were seen");
        }

        [Test]
        public void A_hidden_goalie_forgets_the_push()
        {
            GoalieView view = PushView(out GoalieCharacter character);
            float rest = RightSkate(character).position.x;
            Settle(view, new PushShown(PushKind.TPush, Skate.Right, PushPhase.Push, 1));
            Assert.Greater(view.PushMoment.PushOut, 0.9);

            view.Show(false);
            Assert.AreEqual(PushPhase.Done, view.Push.Phase);
            Assert.AreEqual(0.0, view.PushMoment.PushOut);
            Assert.AreEqual(0.0, view.PushMoment.LeadTurn);

            view.Show(true);
            view.SetProgress(0f);
            Assert.AreEqual(rest, RightSkate(character).position.x, 1e-5f, "shown again, he is in the plain stance");
        }

        [Test]
        public void No_push_gives_the_old_pose()
        {
            GoalieView view = PushView(out GoalieCharacter character);
            var before = new Dictionary<HumanBodyBones, (Vector3 position, Quaternion rotation)>();
            foreach (HumanBodyBones b in System.Enum.GetValues(typeof(HumanBodyBones)))
            {
                Transform t = b == HumanBodyBones.LastBone ? null : character.Bone(b);
                if (t != null) before[b] = (t.position, t.rotation);
            }
            Assert.Greater(before.Count, 20, "the bones were read");

            Settle(view, PushShown.None);
            view.SetProgress(0f);

            foreach (var pair in before)
            {
                Transform t = character.Bone(pair.Key);
                Assert.Less(Vector3.Distance(pair.Value.position, t.position), 1e-5f, pair.Key + " position");
                Assert.Less(Quaternion.Angle(pair.Value.rotation, t.rotation), 1e-3f, pair.Key + " rotation");
            }
        }
    }
}
