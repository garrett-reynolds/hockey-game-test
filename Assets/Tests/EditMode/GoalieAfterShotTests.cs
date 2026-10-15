using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// What the goalie character shows after its reach (#43), through GoalieView: React on the Shot, Watch each frame with
    /// the puck and the seconds since it got there, SetProgress to pose. The face is judged from the mask (its centre is in
    /// front of the head), the stick from its bounds, contact from the real baked triangles.
    /// </summary>
    public class GoalieAfterShotTests
    {
        GameObject _parent;
        /// <summary>The Late Margin limit the Round passes (RoundSettings.LateMarginLimitSeconds).</summary>
        static readonly double LateMarginLimit = new RoundSettings().LateMarginLimitSeconds;
        static readonly Vector3 Eye = new Vector3(0f, 1.5f, -7.5f);
        static readonly Vector3 From = Eye + Vector3.down * 0.35f; // roughly where a Shot starts the puck
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
        }

        readonly List<GoalieView> _views = new List<GoalieView>(); // every view Make built, disposed in TearDown

        GoalieView Make(out GoalieCharacter character)
        {
            character = new GoalieCharacter(_parent.transform);
            var view = new GoalieView(_parent.transform, character);
            _views.Add(view);
            view.Show(true);
            view.Face(0f, 0f);
            view.SetProgress(0f);
            Snapshot[character] = Mask(character).InverseTransformDirection(Vector3.back); // the Ready Stance looks square on
            return view;
        }

        static ShotResult Result(ShotResultKind kind, double x, double y) => new ShotResult(kind, true, x, y, 100, false);

        static Vector3 NetPoint(double x, double y)
        {
            NetFaceMapping.ToMetres(x, y, out double right, out double up);
            return new Vector3((float)right, (float)up, 0f);
        }

        static Transform Mask(GoalieCharacter character)
        {
            foreach (MeshRenderer r in character.Root.GetComponentsInChildren<MeshRenderer>(true))
                if (r.name == GearPiece.ObjectName(GearPiece.Mask)) return r.transform;
            Assert.Fail("no mask");
            return null;
        }

        /// <summary>Where the face looks: the way the mask faces, taken as square to the shooter (-z) in the Ready Stance.</summary>
        static Vector3 Face(GoalieCharacter character)
        {
            Transform mask = Mask(character);
            Snapshot.TryGetValue(character, out Vector3 local);
            return mask.TransformDirection(local);
        }

        static readonly Dictionary<GoalieCharacter, Vector3> Snapshot = new Dictionary<GoalieCharacter, Vector3>();

        /// <summary>Degrees the face is turned from square to the shooter (-z), + toward +x (the shooter's right).</summary>
        static float Yaw(GoalieCharacter character)
        {
            Vector3 flat = Vector3.ProjectOnPlane(Face(character), Vector3.up);
            return Mathf.Atan2(flat.x, -flat.z) * Mathf.Rad2Deg;
        }

        /// <summary>Degrees between where the face looks and the puck.</summary>
        static float OffPuck(GoalieCharacter character, Vector3 puck) => Vector3.Angle(Face(character), puck - character.Bone(HumanBodyBones.Head).position);

        static float MaskHeight(GoalieCharacter character)
        {
            Assert.IsTrue(character.GearBounds(GearPiece.Mask, out Bounds mask));
            return mask.center.y;
        }

        static float StickBottom(GoalieCharacter character)
        {
            Assert.IsTrue(character.GearBounds(GearPiece.Stick, out Bounds stick));
            return stick.min.y;
        }

        // The wiring (PhysicsShotEffects -> GoalieView.Watch's sinceContact -> YawTo / AfterShot.WithHeadFollow): before the Whistle
        // the head follows a touched puck in front of him, and stays square when the puck has not touched anything.
        [Test]
        public void Before_the_whistle_the_head_follows_a_puck_that_has_touched_something_in_front_of_him()
        {
            foreach (double side in new[] { -1.0, 1.0 })
            {
                GoalieView touched = Make(out GoalieCharacter touchedCharacter);
                GoalieView untouched = Make(out GoalieCharacter untouchedCharacter);
                var puck = new Vector3((float)side * 2f, 0.3f, -1.5f); // to one side, in front of him
                foreach (GoalieView view in new[] { touched, untouched })
                    view.React(Result(ShotResultKind.Save, 50, 50), From, null, double.PositiveInfinity, LateMarginLimit);
                touched.Watch(puck, -1f, -1f, 0.5f);
                touched.SetProgress(1f);
                untouched.Watch(puck, -1f, -1f, -1f);
                untouched.SetProgress(1f);
                Assert.Greater(Mathf.Abs(Yaw(touchedCharacter)), 25f, side + ": the head has turned toward the touched puck");
                Assert.AreEqual(Mathf.Sign((float)side), Mathf.Sign(Yaw(touchedCharacter)), side + ": toward its side");
                Assert.Less(Mathf.Abs(Yaw(untouchedCharacter)), 10f, side + ": no touch yet, the head stays square");
            }
        }

        void At(GoalieView view, Vector3 puck, float sinceArrival, float sinceNetting = -1f)
        {
            view.Watch(puck, sinceArrival, sinceNetting);
            view.SetProgress(1f);
        }

        [Test]
        public void With_AI_clips_on_a_goal_s_code_reaction_adds_no_head_turn_or_gesture_and_with_them_off_it_still_does()
        {
            // Garrett, 2026-10-14: the code-made head shake played along with the AI clip ("It looks insane").
            GoalieView clipped = Make(out GoalieCharacter clippedCharacter);
            GoalieView coded = Make(out GoalieCharacter codedCharacter);
            clipped.Clips.Use = true;
            clipped.Clips.ClipsFoundOverride = true; // as if Assets/AIClips were there (it is never committed)
            clipped.Clips.ClipThisHold = true;       // as if the hold had room for the clip (Post decides it in Play)
            coded.Clips.ClipThisHold = true;
            coded.Clips.Use = false;                 // K: the clips are off
            Assert.IsTrue(clipped.Clips.ReplacesReaction(AfterShotGesture.Slump));
            Assert.IsTrue(clipped.Clips.ReplacesReaction(AfterShotGesture.None), "a Save");
            Assert.IsTrue(clipped.Clips.ReplacesReaction(AfterShotGesture.Relief), "a Miss has miss-relief now");
            Assert.IsFalse(coded.Clips.ReplacesReaction(AfterShotGesture.Slump));

            Vector3 net = NetPoint(15, 85);
            foreach (GoalieView view in new[] { clipped, coded })
                view.React(Result(ShotResultKind.Goal, 15, 85), From, null, 0.2, 0.05); // a wide Late Margin: the Slump (its head shakes)
            At(clipped, net, 0f, -1f);
            float whistleYaw = Yaw(clippedCharacter);
            float worstClipped = 0f, worstCoded = 0f;
            for (float t = 0.1f; t <= 1.6f; t += 0.05f)
            {
                Vector3 puck = net + Vector3.forward * 0.6f + Vector3.down * 0.3f;
                At(clipped, puck, t, t - 0.35f);
                At(coded, puck, t, t - 0.35f);
                worstClipped = Mathf.Max(worstClipped, Mathf.Abs(Mathf.DeltaAngle(Yaw(clippedCharacter), whistleYaw)));
                worstCoded = Mathf.Max(worstCoded, Mathf.Abs(Mathf.DeltaAngle(Yaw(codedCharacter), whistleYaw)));
            }
            Assert.Greater(worstCoded, 70f, "clips off: the code still looks back into the net");
            Assert.Less(worstClipped, 3f, "clips on: no code look back or head shake");
        }

        [Test]
        public void After_a_goal_meets_the_netting_the_goalie_looks_back_into_the_net_over_the_shoulder_on_its_side()
        {
            foreach (double x in new[] { 15.0, 88.0 })
            {
                GoalieView view = Make(out GoalieCharacter character);
                Vector3 net = NetPoint(x, 85);
                view.React(Result(ShotResultKind.Goal, x, 85), From, null, double.PositiveInfinity, LateMarginLimit);
                At(view, net, -1f);
                Assert.Less(Mathf.Abs(Yaw(character)), 25f, x + ": the reach still looks out toward the shooter");
                At(view, net + Vector3.forward * 0.6f, 0.2f, -1f);
                Assert.Less(Mathf.Abs(Yaw(character)), 25f, x + ": no look back before the netting");
                At(view, net + Vector3.forward * 0.6f + Vector3.down * 0.3f, 1.0f, 0.65f);
                float yaw = Yaw(character);
                Assert.Greater(Mathf.Abs(yaw), 70f, x + ": looking back into the net");
                Assert.AreEqual(Mathf.Sign(net.x), Mathf.Sign(yaw), x + ": over the shoulder on the puck's side");
                Object.DestroyImmediate(character.Root);
            }
        }

        [Test]
        public void A_goal_s_two_annoyed_gestures_read_differently_and_each_ends_in_its_own_pose_before_he_recovers()
        {
            // Slump (15, 85): the head goes well down and stays there.
            GoalieView view = Make(out GoalieCharacter character);
            Vector3 net = NetPoint(15, 85), inNet = net + Vector3.forward * 0.6f;
            view.React(Result(ShotResultKind.Goal, 15, 85), From, null, 0.2, 0.05); // a wide Late Margin
            Assert.AreEqual(AfterShotGesture.Slump, view.Gesture);
            At(view, net, -1f);
            float reachHead = MaskHeight(character), reachStick = StickBottom(character);
            At(view, inNet, 1.7f, 1.4f); // the gesture's end, just before he recovers (#44)
            Assert.Less(MaskHeight(character), reachHead - 0.08f, "slumped: the head dropped");
            float slumpStick = StickBottom(character);
            Assert.Less(slumpStick, reachStick + 0.1f, "the stick stays down");
            Object.DestroyImmediate(character.Root);

            // Stick slap (50, 90): the stick comes well up off the ice, then down onto it.
            view = Make(out character);
            net = NetPoint(50, 90);
            inNet = net + Vector3.forward * 0.6f;
            view.React(Result(ShotResultKind.Goal, 50, 90), From, null, 0.01, 0.05); // a narrow Late Margin
            Assert.AreEqual(AfterShotGesture.StickSlap, view.Gesture);
            At(view, net, -1f);
            float onIce = StickBottom(character);
            At(view, inNet, 1.2f, 0.9f); // the stick held up, just before the slam
            Assert.Greater(StickBottom(character), onIce + 0.25f, "the stick is lifted");
            At(view, inNet, 1.7f, 1.4f);
            Assert.AreEqual(onIce, StickBottom(character), 0.03f, "slapped back down on the ice");
        }

        /// <summary>
        /// Plays a Shot through the Round and PhysicsShotEffects as the game does each frame (a physics step, then the Round, then
        /// the effects, then the goalie's set from Breakaway), calling back with the seconds since the Shot, whether the Whistle
        /// has gone, and the physics puck.
        /// </summary>
        void Play(AttackSide side, double x, double y, System.Action<GoalieCharacter, float, bool, Transform> frame)
        {
            NetBuilder.Build(_parent.transform);
            var camera = new GameObject("Camera (test)", typeof(Camera));
            camera.transform.position = Eye;
            camera.transform.LookAt(new Vector3(0f, 0.61f, 0f));
            PhysicsShotEffects effects = null;
            GoalieView view = null;
            try
            {
                var character = new GoalieCharacter(_parent.transform);
                view = new GoalieView(_parent.transform, character);
                effects = new PhysicsShotEffects(_parent.transform, camera.GetComponent<Camera>(), new SeededRandom(7), view, name => null);
                Physics.SyncTransforms();
                var round = new Round(ScriptedRandom.Forcing(side), new RoundSettings(), FakeReach.Saving((sx, sy) => sy < 50), 0); // low saved, high scored
                round.StartRound();
                round.Tick(3.7);
                view.Show(true);
                view.Face(0f, 0f);
                view.SetProgress(0f);
                Snapshot[character] = Mask(character).InverseTransformDirection(Vector3.back);
                round.SubmitShot(x, y, 100, false);
                effects.Update(round, 0f); // the Shot frame: the puck leaves
                Transform puck = effects.Shot.Puck.transform;
                bool arrived = false;
                const float Frame = 1f / 60f;
                for (int i = 0; i < 600 && round.Phase == RoundPhase.ResultHold; i++)
                {
                    for (int step = 0; step < 2; step++) // a frame is 1/60 s: two physics steps of 1/120 s
                    {
                        Physics.Simulate(PhysicsShot.StepSeconds);
                        effects.AfterPhysicsStep(PhysicsShot.StepSeconds);
                    }
                    round.Tick(Frame);
                    effects.Update(round, Frame);
                    if (round.Phase != RoundPhase.ResultHold) break;
                    arrived |= !round.AwaitingWhistle; // the Whistle has gone: the puck has got there
                    view.SetProgress((float)Breakaway.GoalieSet(round.Phase, round.PhaseSecondsElapsed, true, effects.FlightSeconds, round.LastRace));
                    frame(character, (float)round.PhaseSecondsElapsed, arrived, puck);
                }
                Assert.IsTrue(arrived);
            }
            finally
            {
                effects?.Dispose();
                view?.Dispose();
                Object.DestroyImmediate(camera);
            }
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void Played_through_a_goal_the_goalie_looks_back_after_the_netting_gestures_and_never_touches_the_puck()
        {
            float reachHead = 0f, lowestHead = float.MaxValue, lastStick = 0f, mostYaw = 0f, yawBefore = 0f;
            int frames = 0;
            Vector3 last = Vector3.zero;
            var touches = new List<string>();
            Play(AttackSide.Left, 50, 90, (character, t, arrived, puck) =>
            {
                float yaw = Mathf.Abs(Yaw(character));
                if (!arrived) { yawBefore = Mathf.Max(yawBefore, yaw); reachHead = MaskHeight(character); }
                else mostYaw = Mathf.Max(mostYaw, yaw);
                if (arrived) lowestHead = Mathf.Min(lowestHead, MaskHeight(character));
                lastStick = StickBottom(character);
                // The puck's move over the last few frames against the goalie as it stands now.
                if (frames++ % 3 == 0 && t > 0.2f)
                {
                    if (last != Vector3.zero && GoalieTriangles.Crosses(GoalieTriangles.Of(character), last, puck.position))
                        touches.Add(t.ToString("F2") + " s: " + puck.position.ToString("F2"));
                    last = puck.position;
                }
            });
            Assert.Less(yawBefore, 25f, "no look back while the puck flies");
            Assert.Greater(mostYaw, 15f, "looked back into the net"); // #45: he no longer pushes 0.8 m off the puck's line, so the net is nearer straight behind him (23 deg measured). Before #45: the physics puck rests nearer the front of the net than the scripted one did; 0.2 m out (#124) turns him less (44.6 deg measured)
            Assert.Less(lowestHead, reachHead - 0.05f, "the gesture drops his head");
            Assert.Less(lastStick, 0.03f, "the stick back on the ice");
            Assert.IsEmpty(touches, string.Join("; ", touches));
        }

        [Test]
        public void Played_through_a_pad_save_the_head_follows_the_rebound()
        {
            float worst = 0f, since = 0f;
            int watched = 0;
            Play(AttackSide.Middle, 15, 12, (character, t, arrived, puck) =>
            {
                if (!arrived) return; // the puck may be hidden once out of sight: the goalie still watches where it was last
                since += 1f / 60f;
                if (since < 0.3f) return; // turned in by then
                if (since > (float)AfterShot.SaveRecoverFrom) return; // then he recovers into the Ready Stance (#44)
                watched++;
                worst = Mathf.Max(worst, OffPuck(character, puck.position));
            });
            Assert.Greater(watched, 30);
            Assert.Less(worst, 30f, "the face stays on the rebounding puck");
        }

        static readonly string[] Gear = { GearPiece.LeftPad, GearPiece.RightPad, GearPiece.Blocker, GearPiece.Catcher, GearPiece.Stick, GearPiece.LeftSkate, GearPiece.RightSkate };

        static Vector3[] GearCentres(GoalieCharacter character)
        {
            var centres = new Vector3[Gear.Length];
            for (int i = 0; i < Gear.Length; i++)
            {
                Assert.IsTrue(character.GearBounds(Gear[i], out Bounds b), Gear[i]);
                centres[i] = b.center;
            }
            return centres;
        }

        static void AssertGearStill(Vector3[] before, GoalieCharacter character, string what)
        {
            Vector3[] now = GearCentres(character);
            for (int i = 0; i < Gear.Length; i++)
                Assert.AreEqual(0f, Vector3.Distance(before[i], now[i]), 0.001f, what + ": the " + Gear[i] + " stays where the rebound was measured");
        }

        [Test]
        public void On_a_pad_save_the_head_follows_the_rebounding_puck_and_only_the_head_moves()
        {
            GoalieView view = Make(out GoalieCharacter character);
            view.React(Result(ShotResultKind.Save, 15, 12), From, null, double.PositiveInfinity, LateMarginLimit);
            Vector3 contact = NetPoint(15, 12) + Vector3.back * 0.7f;
            At(view, contact, -1f);
            Vector3[] gear = GearCentres(character);
            foreach (Vector3 rebound in new[] { new Vector3(-1.2f, 0.05f, -1.6f), new Vector3(1.2f, 0.05f, -1.6f), new Vector3(0.3f, 0.6f, -2.5f) })
            {
                At(view, rebound, -1f);
                float before = OffPuck(character, rebound);
                At(view, rebound, 0.5f);
                float after = OffPuck(character, rebound);
                Assert.Less(after, before - 15f, rebound + ": the head has turned to the puck");
                Assert.Less(after, 30f, rebound + ": looking at it");
                AssertGearStill(gear, character, "rebound to " + rebound);
            }
        }

        [TestCase(112.0, 50.0, TestName = "Wide right: the head follows the puck by, then relief toward the right post")]
        [TestCase(-3.0, 60.0, TestName = "Off the left post (a Frame Hit): the head follows it, then relief toward that post")]
        public void On_a_miss_the_head_follows_the_puck_going_by_then_shows_relief(double x, double y)
        {
            GoalieView view = Make(out GoalieCharacter character);
            view.React(Result(ShotResultKind.Miss, x, y), From, null, double.PositiveInfinity, LateMarginLimit);
            Assert.AreEqual(AfterShotGesture.Relief, view.Gesture);
            float side = x > 50 ? 1f : -1f;
            At(view, NetPoint(x, y), -1f);
            Vector3[] gear = GearCentres(character);
            float before = Yaw(character);
            At(view, NetPoint(x, y) + new Vector3(side * 0.4f, 0f, 0.8f), 0.4f);
            Assert.Greater(Yaw(character) * side, 35f, "the head has followed the puck going by");
            Assert.Greater(Yaw(character) * side, before * side + 20f);
            AssertGearStill(gear, character, "following");
            At(view, NetPoint(x, y) + new Vector3(side * 0.8f, 0f, 3f), 0.85f);
            AssertGearStill(gear, character, "until a deflection is clear of him");
            At(view, NetPoint(x, y) + new Vector3(side * 0.8f, 0f, 3f), 1.1f);
            float breathIn = MaskHeight(character);
            At(view, NetPoint(x, y) + new Vector3(side * 0.8f, 0f, 3f), 1.65f);
            Assert.Greater(Yaw(character) * side, 20f, "glancing back at the post on that side");
            Assert.Less(MaskHeight(character), breathIn - 0.005f, "breathed out");
        }
    }
}
