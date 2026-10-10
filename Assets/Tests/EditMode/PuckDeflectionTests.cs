using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using Triangle = HockeyGame.Core.Tests.GoalieTriangles.Triangle;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The puck after a Save or a Frame Hit, end to end through ShotEffects with the goalie character and the net:
    /// it leaves the surface it hit the way that surface faces, the catching glove holds it, and it never goes into the
    /// goalie, the frame or the ice.
    /// </summary>
    public class PuckDeflectionTests
    {
        const float Frame = 1f / 60f;
        const float PostAxisX = 0.915f + 0.04f; // the right post's axis: Net Face half width + half the 8 cm frame (independent of the code)
        const float PostRadius = 0.04f;
        const float CrossbarAxisY = 1.22f + 0.04f;

        GameObject _rink, _camera;

        [SetUp]
        public void SetUp()
        {
            _rink = new GameObject("Rink (test)");
            _camera = new GameObject("Camera (test)", typeof(Camera));
            _camera.transform.position = new Vector3(0f, 1.5f, -9f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_rink);
            Object.DestroyImmediate(_camera);
        }

        sealed class Run
        {
            public ShotResultKind Kind;
            public Vector3 Contact;          // where the puck was on the frame it arrived
            public readonly List<Vector3> Path = new List<Vector3>();       // after the contact, one per frame
            public readonly List<Quaternion> Turns = new List<Quaternion>();
            public bool Visible = true;     // shown through the whole hold
            public SoundCue? Sound;         // ShotEffects.ContactSound on the arrival frame
            public PuckSurface? Surface;    // ShotEffects.ContactSurface on the arrival frame
            public Vector3 Leaving => Path[6] - Contact; // over the first 0.1 s
        }

        /// <param name="onArrive">Called on the frame the puck gets there, with the goalie fully reacted: as the rebound was
        /// measured (after it he follows through and recovers, #44), and where the puck is.</param>
        Run Shoot(GoaliePose pose, double x, double y, double kmh = 100, GoalieCharacter character = null, float facing = 0f, System.Action<Vector3> onArrive = null)
        {
            NetBuilder.Build(_rink.transform);
            character = character ?? new GoalieCharacter(_rink.transform);
            var view = new GoalieView(_rink.transform, character);
            var effects = new ShotEffects(_rink.transform, _camera.GetComponent<Camera>(), null, view);
            Transform puck = _rink.transform.Find("Puck");
            var round = new Round(ScriptedRandom.Forcing(pose), new RoundSettings(), 0);
            round.StartRound();
            round.Tick(3.7);
            view.Show(true, round.GoaliePose, false);
            view.Face(facing, facing);
            Assert.AreEqual(pose, round.GoaliePose);
            round.SubmitShot(x, y, kmh, false);
            var run = new Run { Kind = round.LastResult.Value.Kind };
            bool arrived = false;
            float flown = 0f;
            for (int i = 0; i < 1000 && round.Phase == RoundPhase.ResultHold; i++)
            {
                view.SetProgress(1f);
                // The frame the puck gets there ends just as it does, so Contact is where it met the gear, before any rebound.
                float left = (float)round.ResultDelaySeconds - flown;
                float dt = !arrived && i > 0 && left < Frame ? left + 1e-5f : Frame;
                flown += dt;
                effects.Update(round, dt);
                round.Tick(dt);
                if (round.Phase != RoundPhase.ResultHold) break;
                if (effects.PuckArrivedThisFrame) { arrived = true; run.Contact = puck.position; run.Sound = effects.ContactSound; run.Surface = effects.ContactSurface; onArrive?.Invoke(run.Contact); continue; }
                if (!arrived) continue;
                run.Path.Add(puck.position);
                run.Turns.Add(puck.rotation);
                run.Visible &= puck.gameObject.activeInHierarchy;
            }
            Assert.IsTrue(arrived, "the puck arrived");
            return run;
        }

        /// <summary>Outward direction of the gear at a point, from its nearest vertex (the test's own look at the mesh).</summary>
        static Vector3 GearFacing(GoalieCharacter character, string piece, Vector3 point)
        {
            var r = Find(character.Root.transform, GearPiece.ObjectName(piece)).GetComponent<MeshFilter>();
            float best = float.PositiveInfinity;
            Vector3 nearest = point;
            foreach (Vector3 v in r.sharedMesh.vertices)
            {
                Vector3 w = r.transform.TransformPoint(v);
                float d = (w - point).sqrMagnitude;
                if (d < best) { best = d; nearest = w; }
            }
            return (point - nearest).normalized;
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }

        [Test]
        public void A_pad_save_leaves_the_way_the_pad_faces_where_the_puck_met_it()
        {
            var character = new GoalieCharacter(_rink.transform);
            Run run = Shoot(GoaliePose.MiddleButterfly, 10, 30, character: character); // a standing pad save, the pad upright
            Assert.AreEqual(ShotResultKind.Save, run.Kind);
            Vector3 facing = GearFacing(character, GearPiece.RightPad, run.Contact); // the goalie's right pad is on the shooter's left
            Assert.Greater(Vector3.Dot(run.Leaving.normalized, facing), 0.3f, "away from the pad face");
            Assert.Less(run.Leaving.z, 0f, "back toward the shooter");
            Assert.AreEqual(Mathf.Sign(facing.x), Mathf.Sign(run.Leaving.x), "off to the side the pad faces");
        }

        [Test]
        public void A_blocker_save_is_kicked_hard_to_the_blocker_side()
        {
            Run run = Shoot(GoaliePose.MiddleStandUp, 10, 80);
            Assert.AreEqual(ShotResultKind.Save, run.Kind);
            Vector3 flat = new Vector3(run.Leaving.x, 0f, run.Leaving.z);
            Assert.Less(flat.x, -0.5f * flat.magnitude, "to the shooter's left, the blocker's outside");
        }

        [TestCase(0.75, 1f)]   // outer half of the right post: out wide
        [TestCase(0.25, -1f)]  // inner half: back across the slot
        public void A_post_hit_leaves_off_the_round_post_by_where_on_the_tube_it_hit(double acrossPost, float side)
        {
            double x = 100.0 + NetFrame.PostWidth * acrossPost;
            Run run = Shoot(GoaliePose.MiddleStandUp, x, 40);
            Assert.AreEqual(ShotResultKind.Miss, run.Kind);
            Vector3 tube = new Vector3(run.Contact.x - PostAxisX, 0f, run.Contact.z).normalized; // the tube's normal at the contact
            Assert.AreEqual(side, Mathf.Sign(run.Leaving.x), "sideways the way the tube faces there");
            Assert.Greater(Vector3.Dot(run.Leaving.normalized, tube), 0.3f, "away from the tube");
            Assert.Greater(run.Leaving.magnitude / 0.1f, 4f, "pinged away fast (m/s)");
        }

        [Test]
        public void A_stick_save_is_made_on_the_blade_and_deflected_low_the_way_the_blade_faces()
        {
            var character = new GoalieCharacter(_rink.transform);
            Vector3 blade = Vector3.zero;
            Run run = Shoot(StickPose, StickX, StickY, character: character, onArrive: contact => blade = GearFacing(character, GearPiece.Stick, contact));
            Assert.AreEqual(ShotResultKind.Save, run.Kind);
            Assert.AreEqual(PuckSurface.Stick, run.Surface, "made on the stick");
            Assert.AreEqual(SoundCue.BlockerHit, run.Sound, "sounds like the stick (wood)");
            Assert.Greater(Vector3.Dot(run.Leaving.normalized, blade), 0.2f, "away from the blade");

            float highest = float.NegativeInfinity;
            foreach (Vector3 p in run.Path) highest = Mathf.Max(highest, p.y);
            Assert.Less(highest, run.Contact.y + 0.03f, "never rises off the blade");
        }

        // One Save for each Save Reaction, each in a Zone its pose covers, and the surface the puck must meet (the test's own table).
        static readonly (GoaliePose pose, double x, double y, PuckSurface surface)[] ReactionSaves =
        {
            (GoaliePose.MiddleStandUp, 88, 82, PuckSurface.Catcher),
            (GoaliePose.MiddleStandUp, 12, 82, PuckSurface.Blocker),
            (GoaliePose.MiddleButterfly, 15, 15, PuckSurface.Pad),
            (GoaliePose.MiddleButterfly, 85, 25, PuckSurface.Pad),
            (GoaliePose.MiddleButterfly, 50, 5, PuckSurface.Stick),
            (GoaliePose.MiddleStandUp, 50, 55, PuckSurface.Body),
        };

        [Test]
        public void The_puck_meets_the_save_reaction_s_gear_and_sounds_and_deflects_like_it()
        {
            foreach (var (pose, x, y, surface) in ReactionSaves)
            {
                TearDown();
                SetUp();
                var character = new GoalieCharacter(_rink.transform);
                List<Triangle> goalie = null;
                Run run = Shoot(pose, x, y, character: character, onArrive: _ => goalie = GoalieTriangles(character));
                string where = "(" + x + ", " + y + ")";
                Assert.AreEqual(ShotResultKind.Save, run.Kind, where);
                Assert.AreEqual(surface, run.Surface, where + ": the puck meets the reaction's gear");
                Assert.AreEqual(Expected(surface), run.Sound, where + ": and sounds like it");
                Assert.AreEqual(RoundSoundCues.SaveContact(x, y), run.Sound, where + ": the save sound is the reaction's");
                // Touching the gear where the reaction put it: not short of it.
                float nearest = float.MaxValue;
                foreach (Triangle t in Core.Tests.GoalieTriangles.Near(goalie, new Bounds(run.Contact, Vector3.zero), 0.1f))
                    nearest = Mathf.Min(nearest, Vector3.Distance(run.Contact, ClosestPoint(t, run.Contact)));
                Assert.Less(nearest, 0.06f, where + ": touching the gear (nearest goalie point " + nearest.ToString("F3") + " m)");
            }
        }

        /// <summary>The nearest of a triangle's corners, edge middles and centre to p (close enough for a "touching" check).</summary>
        static Vector3 ClosestPoint(Triangle t, Vector3 p)
        {
            Vector3 best = t.A;
            foreach (Vector3 q in new[] { t.B, t.C, (t.A + t.B) / 2f, (t.B + t.C) / 2f, (t.C + t.A) / 2f, (t.A + t.B + t.C) / 3f })
                if ((q - p).sqrMagnitude < (best - p).sqrMagnitude) best = q;
            return best;
        }

        /// <summary>The result sound a test expects for each surface (its own table, not ShotEffects.SoundOf).</summary>
        static SoundCue Expected(PuckSurface surface) =>
            surface == PuckSurface.Frame ? SoundCue.PostPing
            : surface == PuckSurface.Blocker || surface == PuckSurface.Stick || surface == PuckSurface.Skate ? SoundCue.BlockerHit
            : surface == PuckSurface.Catcher ? SoundCue.GloveCatch
            : SoundCue.SaveImpact;

        [Test]
        public void A_Frame_Hit_rings_off_the_frame_as_the_goalie_s_reach_keeps_off_the_puck_and_it_is_still_a_Miss()
        {
            int frameHits = 0;
            foreach (TestCaseData c in AllSaves())
            {
                var pose = (GoaliePose)c.Arguments[0];
                double x = (double)c.Arguments[1], y = (double)c.Arguments[2];
                if (NetFrame.At(x, y) == FrameHit.None) continue;
                foreach (bool close in new[] { false, true })
                {
                    TearDown();
                    SetUp();
                    PlaceShooter(pose, close); // from 3 m, post-hugging gear used to cover the post (#38)
                    NetBuilder.Build(_rink.transform);
                    var view = new GoalieView(_rink.transform, new GoalieCharacter(_rink.transform));
                    var effects = new ShotEffects(_rink.transform, _camera.GetComponent<Camera>(), null, view);
                    var round = new Round(ScriptedRandom.Forcing(pose), new RoundSettings(), 0);
                    round.StartRound();
                    round.Tick(3.7);
                    view.Show(true, round.GoaliePose, false);
                    float facing = GoalieCharacter.FacingToward(_camera.transform.position);
                    view.Face(facing, facing);
                    round.SubmitShot(x, y, 100, false);
                    effects.Update(round, Frame); // the Shot frame: the contact is known from here
                    string where = pose + " (" + x.ToString("F1") + ", " + y.ToString("F1") + ")" + (close ? " from 3 m" : "");
                    Assert.AreEqual(ShotResultKind.Miss, round.LastResult.Value.Kind, where);
                    Assert.AreEqual(PuckSurface.Frame, effects.ContactSurface, where);
                    Assert.AreEqual(SoundCue.PostPing, effects.ContactSound, where);
                    frameHits++;
                }
            }
            Assert.Greater(frameHits, 0);
        }

        // A Shot along the ice through the middle: a stick save (SaveReactions), in a Zone MiddleButterfly covers.
        const GoaliePose StickPose = GoaliePose.MiddleButterfly;
        const double StickX = 50, StickY = 5;

        [Test]
        public void Pad_blocker_stick_and_post_send_the_puck_off_in_different_directions()
        {
            var shots = new[]
            {
                (GoaliePose.MiddleButterfly, 85.0, 30.0),                                  // pad (the goalie's left, standing)
                (GoaliePose.MiddleStandUp, 10.0, 80.0),                                    // blocker
                (StickPose, StickX, StickY),                                               // stick
                (GoaliePose.MiddleStandUp, 100.0 + NetFrame.PostWidth * 0.75, 40.0),       // post
            };
            var runs = new Vector3[shots.Length];
            for (int i = 0; i < shots.Length; i++)
            {
                TearDown();
                SetUp(); // a fresh rink for each Shot
                runs[i] = Shoot(shots[i].Item1, shots[i].Item2, shots[i].Item3).Leaving;
            }
            for (int a = 0; a < runs.Length; a++)
            for (int b = a + 1; b < runs.Length; b++)
                Assert.Greater(Vector3.Angle(runs[a], runs[b]), 15f, a + " vs " + b + ": " + string.Join(" ", System.Array.ConvertAll(runs, r => r.normalized.ToString("F2"))));
        }

        [Test]
        public void The_catching_glove_holds_the_puck_through_the_hold()
        {
            var character = new GoalieCharacter(_rink.transform);
            Vector3 inGlove = Vector3.zero;
            Run run = Shoot(GoaliePose.MiddleStandUp, 90, 80, character: character, onArrive: contact => inGlove = contact - Catcher(character));
            Assert.AreEqual(ShotResultKind.Save, run.Kind);
            Assert.IsTrue(run.Visible, "shown in the glove");
            // The glove comes up and back into the Ready Stance (#44): the puck goes with it.
            Assert.Greater(Vector3.Distance(run.Contact, run.Path[run.Path.Count - 1]), 0.05f, "the glove moved on");
            Assert.AreEqual(0f, Vector3.Distance(run.Path[run.Path.Count - 1] - Catcher(character), inGlove), 0.03f, "still in the glove");
        }

        static Vector3 Catcher(GoalieCharacter character)
        {
            Assert.IsTrue(character.GearBounds(GearPiece.Catcher, out Bounds b));
            return b.center;
        }

        [Test]
        public void In_flight_the_puck_spins_flat()
        {
            NetBuilder.Build(_rink.transform);
            var effects = new ShotEffects(_rink.transform, _camera.GetComponent<Camera>());
            Transform puck = _rink.transform.Find("Puck");
            var round = new Round(ScriptedRandom.Forcing(GoaliePose.MiddleStandUp), new RoundSettings(), 0);
            round.StartRound();
            round.Tick(3.7);
            round.SubmitShot(50, 50, 30, false); // slow, so the flight lasts
            effects.Update(round, Frame);
            Quaternion a = puck.rotation;
            effects.Update(round, Frame);
            Quaternion b = puck.rotation;
            Assert.AreEqual(0f, Vector3.Angle(a * Vector3.up, b * Vector3.up), 0.01f, "the face keeps facing the same way");
            Assert.Greater(Quaternion.Angle(a, b), 5f, "turning about it");
        }

        static IEnumerable<TestCaseData> AllSaves()
        {
            foreach (GoaliePose pose in System.Enum.GetValues(typeof(GoaliePose)))
                for (int x = 5; x < 100; x += 15)
                for (int y = 5; y < 100; y += 15)
                    if (GoaliePoses.CoverAt(pose, Zones.Column(x), Zones.Row(y)) == ZoneCover.Covered)
                        yield return new TestCaseData(pose, (double)x, (double)y);
            foreach (GoaliePose pose in new[] { GoaliePose.MiddleButterfly, GoaliePose.LeftPostHug, GoaliePose.RightPostHug })
            {
                foreach (double x in new[] { -NetFrame.PostWidth * 0.5, -NetFrame.PostWidth * 0.9, 100 + NetFrame.PostWidth * 0.2, 100 + NetFrame.PostWidth * 0.9 })
                    foreach (double y in new[] { 5.0, 30.0, 80.0 })
                        yield return new TestCaseData(pose, x, y);
                yield return new TestCaseData(pose, 30.0, 100 + NetFrame.CrossbarHeight * 0.5);
            }
        }

        /// <summary>
        /// Where the Shot is taken from for a pose's Attack Side (Breakaway.SkatePoint's stop, 30% of 9 m out to the side, eye
        /// 1.5 m): at the 9 m stop, or drifted in to 3 m from the net (Breakaway.ClosestDrift). Looks at the Net Face centre.
        /// </summary>
        void PlaceShooter(GoaliePose pose, bool close)
        {
            int side = (int)pose / 2 - 1; // left -1, middle 0, right +1
            var ground = new Vector2(side * 0.3f * 9f, -9f);
            if (close) ground *= 3f / ground.magnitude;
            _camera.transform.position = new Vector3(ground.x, 1.5f, ground.y);
            _camera.transform.LookAt(new Vector3(0f, 0.61f, 0f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void After_any_save_or_frame_hit_the_puck_stays_out_of_the_goalie_frame_and_ice_and_settles_flat(bool close)
        {
            var failures = new List<string>();
            foreach (TestCaseData c in AllSaves())
            {
                var pose = (GoaliePose)c.Arguments[0];
                double x = (double)c.Arguments[1], y = (double)c.Arguments[2];
                TearDown();
                SetUp();
                PlaceShooter(pose, close);
                var character = new GoalieCharacter(_rink.transform);
                // Squared up to the shooter (#39), and kept through the pose (#40). The puck moves among the gear as the
                // reaction left it (it was measured there); after it the goalie follows through and recovers (#44).
                List<Triangle> goalie = null;
                Run run = Shoot(pose, x, y, character: character, facing: GoalieCharacter.FacingToward(_camera.transform.position),
                    onArrive: _ => goalie = GoalieTriangles(character));
                List<Triangle> near = Near(goalie, run.Contact, run.Path);
                string where = pose + " (" + x.ToString("F1") + ", " + y.ToString("F1") + ") ";
                Vector3 last = run.Contact;
                // Still for 8 frames (0.13 s, just under PuckMotion's 0.15 s wedge span; the glove holds it still until its 0.25 s
                // follow-through): held, or wedged in the gear as measured. From then on it rides in the glove on the recovering
                // goalie (#44), settles at the ice, or drops off the gear to the ice clear of it, as the goalie has moved on (#48).
                bool rested = false;
                int stillFrames = 0;
                for (int i = 0; i < run.Path.Count; i++)
                {
                    Vector3 p = run.Path[i];
                    stillFrames = p == last ? stillFrames + 1 : 0;
                    rested |= stillFrames >= 8;
                    Vector3 axis = run.Turns[i] * Vector3.up;
                    float lowest = p.y - 0.075f * Mathf.Sqrt(Mathf.Max(0f, 1f - axis.y * axis.y)) - 0.025f * Mathf.Abs(axis.y);
                    if (lowest < -0.002f) { failures.Add(where + "into the ice at frame " + i); break; }
                    if (!rested && Crosses(near, last, p)) { failures.Add(where + "through the goalie at frame " + i); break; }
                    if (InFrame(p)) { failures.Add(where + "in the frame at frame " + i); break; }
                    last = p;
                }
                if (failures.Count > 0 && failures[failures.Count - 1].StartsWith(where)) continue;
                Vector3 end = run.Path[run.Path.Count - 1];
                if (end.y > 0.04f)
                {
                    // Off the ice: only held in the catching glove, carried there as he recovers (#48: no other gear carries it).
                    if (run.Surface != PuckSurface.Catcher) failures.Add(where + "off the ice at the end after a " + run.Surface + " save (carried on the goalie): " + end.ToString("F3"));
                    else if (!AgainstGear(GoalieTriangles(character), end)) failures.Add(where + "off the ice but not on the goalie at the end: " + end.ToString("F3"));
                    continue;
                }
                Vector3 endAxis = run.Turns[run.Turns.Count - 1] * Vector3.up;
                bool still = run.Path[run.Path.Count - 2] == end;
                bool flatOnIce = Mathf.Abs(endAxis.y) > 0.999f && Mathf.Abs(end.y - 0.025f) < 0.002f;
                if (!still || (!flatOnIce && !AgainstGear(goalie, end)))
                    failures.Add(where + "not lying still, flat on the ice (or held in the gear), at the end: " + end.ToString("F3"));
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        /// <summary>
        /// Every Save the blocker makes: Net Face spots 6 apart wherever the Save Reaction is the blocker, for each pose
        /// covering them. Each Shot takes the next Test Shot speed in turn, so the set spans the whole speed range.
        /// </summary>
        static IEnumerable<(GoaliePose pose, double x, double y, int kmh)> BlockerSaves()
        {
            int kmh = TestShotSpeed.MinKmh;
            foreach (GoaliePose pose in System.Enum.GetValues(typeof(GoaliePose)))
                for (double x = 2; x < 50; x += 6)
                for (double y = 42; y < 100; y += 6)
                {
                    if (SaveReactions.For(x, y) != SaveReaction.Blocker || GoaliePoses.CoverAt(pose, Zones.Column(x), Zones.Row(y)) != ZoneCover.Covered) continue;
                    yield return (pose, x, y, kmh);
                    kmh = kmh == TestShotSpeed.MaxKmh ? TestShotSpeed.MinKmh : kmh + TestShotSpeed.StepKmh;
                }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void A_blocker_save_bounces_off_the_blocker_and_ends_on_the_ice_never_carried(bool close)
        {
            var failures = new List<string>();
            int shots = 0;
            foreach ((GoaliePose pose, double x, double y, int kmh) in BlockerSaves())
            {
                TearDown();
                SetUp();
                PlaceShooter(pose, close);
                var character = new GoalieCharacter(_rink.transform);
                Run run = Shoot(pose, x, y, kmh, character, GoalieCharacter.FacingToward(_camera.transform.position));
                shots++;
                string where = pose + " (" + x + ", " + y + ") " + kmh + " km/h ";
                Assert.AreEqual(ShotResultKind.Save, run.Kind, where);
                Assert.AreEqual(PuckSurface.Blocker, run.Surface, where + "met the blocker");
                Vector3 end = run.Path[run.Path.Count - 1];
                // On the ice: lying flat with its centre half a puck up. Carried on the goalie, it would be off the ice.
                if (end.y > 0.04f) failures.Add(where + "off the ice at the end (on the goalie): " + end.ToString("F3"));
            }
            Assert.Greater(shots, 100, "blocker saves tried");
            Assert.IsEmpty(failures, failures.Count + " of " + shots + "\n" + string.Join("\n", failures));
        }

        // Body saves from 3 m out whose rebound comes to rest in a crease of the gear, well off the ice (found by a probe, #48;
        // found again once the puck flew at the Shot's real speed, #46).
        [TestCase(GoaliePose.MiddleButterfly, 38.0, 74.0)]
        [TestCase(GoaliePose.MiddleStandUp, 56.0, 74.0)]
        public void A_puck_at_rest_on_gear_that_is_not_the_catcher_drops_off_to_the_ice_never_carried(GoaliePose pose, double x, double y)
        {
            PlaceShooter(pose, true);
            Run run = Shoot(pose, x, y, 80, facing: GoalieCharacter.FacingToward(_camera.transform.position));
            Assert.AreEqual(ShotResultKind.Save, run.Kind);
            Assert.AreEqual(PuckSurface.Body, run.Surface);
            int still = 0;
            bool restedOnGear = false;
            for (int i = 1; i < run.Path.Count; i++)
            {
                still = run.Path[i] == run.Path[i - 1] ? still + 1 : 0;
                restedOnGear |= still >= 6 && run.Path[i].y > 0.06f;
            }
            Assert.IsTrue(restedOnGear, "came to rest on the gear, off the ice");
            Vector3 end = run.Path[run.Path.Count - 1];
            Assert.AreEqual(0.025f, end.y, 0.002f, "dropped off to lie on the ice: " + end.ToString("F3"));
            Assert.AreEqual(run.Path[run.Path.Count - 2], end, "lying still");
        }

        /// <summary>
        /// The triangles whose bounds overlap the bounds of the whole path (contact and every frame, padded 1 mm). Each
        /// frame's straight move lies inside those bounds, so any triangle it crosses is among these: the same answer as
        /// checking all ~55k, at a fraction of the cost.
        /// </summary>
        static List<Triangle> Near(List<Triangle> triangles, Vector3 contact, List<Vector3> path)
        {
            var box = new Bounds(contact, Vector3.zero);
            foreach (Vector3 p in path) box.Encapsulate(p);
            return Core.Tests.GoalieTriangles.Near(triangles, box, 0.001f);
        }

        /// <summary>Within 5 cm of a goalie triangle corner or edge midpoint: resting in a crease of the gear.</summary>
        static bool AgainstGear(List<Triangle> goalie, Vector3 p)
        {
            foreach (Triangle t in goalie)
                foreach (Vector3 q in new[] { t.A, t.B, t.C, (t.A + t.B) / 2f, (t.B + t.C) / 2f, (t.C + t.A) / 2f })
                    if ((q - p).sqrMagnitude < 0.0025f) return true;
            return false;
        }

        static bool InFrame(Vector3 p)
        {
            float post = Mathf.Min(new Vector2(p.x - PostAxisX, p.z).magnitude, new Vector2(p.x + PostAxisX, p.z).magnitude);
            bool byPost = p.y < CrossbarAxisY && post < PostRadius;
            bool byBar = Mathf.Abs(p.x) < PostAxisX && new Vector2(p.y - CrossbarAxisY, p.z).magnitude < PostRadius;
            return byPost || byBar;
        }

        static List<Triangle> GoalieTriangles(GoalieCharacter character) => Core.Tests.GoalieTriangles.Of(character);

        static bool Crosses(List<Triangle> triangles, Vector3 a, Vector3 b) => Core.Tests.GoalieTriangles.Crosses(triangles, a, b);
    }
}
