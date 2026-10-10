using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;

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
            public Vector3 Leaving => Path[6] - Contact; // over the first 0.1 s
        }

        Run Shoot(GoaliePose pose, double x, double y, double kmh = 100, GoalieCharacter character = null, float facing = 0f)
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
            for (int i = 0; i < 1000 && round.Phase == RoundPhase.ResultHold; i++)
            {
                view.SetProgress(1f);
                effects.Update(round, Frame);
                round.Tick(Frame);
                if (round.Phase != RoundPhase.ResultHold) break;
                if (effects.PuckArrivedThisFrame) { arrived = true; run.Contact = puck.position; continue; }
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
            var r = Find(character.Root.transform, "Gear " + piece).GetComponent<MeshFilter>();
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
            Run run = Shoot(GoaliePose.MiddleButterfly, 10, 15, character: character);
            Assert.AreEqual(ShotResultKind.Save, run.Kind);
            Vector3 facing = GearFacing(character, "Right Pad", run.Contact); // the goalie's right pad is on the shooter's left
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
            Run run = Shoot(StickPose, StickX, StickY, character: character);
            Assert.AreEqual(ShotResultKind.Save, run.Kind);

            var probe = new GoalieView(_rink.transform, character);
            probe.Show(true, StickPose, false);
            var obstacles = new PuckObstacles();
            probe.AddObstacles(obstacles);
            NetFaceMapping.ToMetres(StickX, StickY, out double right, out double up);
            Assert.IsTrue(probe.SaveSurface(obstacles, run.Contact, new Vector3((float)right, (float)up, 0f), SoundCue.SaveImpact, out _, out PuckSurface surface));
            Assert.AreEqual(PuckSurface.Stick, surface, "made on the stick");
            Assert.Greater(Vector3.Dot(run.Leaving.normalized, GearFacing(character, "Stick", run.Contact)), 0.2f, "away from the blade");

            float highest = float.NegativeInfinity;
            foreach (Vector3 p in run.Path) highest = Mathf.Max(highest, p.y);
            Assert.Less(highest, run.Contact.y + 0.03f, "never rises off the blade");
        }

        // A low Shot the stick blade is nearer than the pads (found with the posed gear).
        const GoaliePose StickPose = GoaliePose.MiddleButterfly;
        const double StickX = 70, StickY = 5;

        [Test]
        public void Pad_blocker_stick_and_post_send_the_puck_off_in_different_directions()
        {
            var shots = new[]
            {
                (GoaliePose.MiddleButterfly, 50.0, 15.0),                                  // pad (where the pads meet)
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
            Run run = Shoot(GoaliePose.MiddleStandUp, 90, 80);
            Assert.AreEqual(ShotResultKind.Save, run.Kind);
            Assert.IsTrue(run.Visible, "shown in the glove");
            foreach (Vector3 p in run.Path) Assert.AreEqual(0f, Vector3.Distance(run.Contact, p), 1e-4f);
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
                // Squared up to the shooter (#39): the pose's own turn limit applies.
                Run run = Shoot(pose, x, y, character: character, facing: GoalieCharacter.FacingToward(_camera.transform.position));
                List<Triangle> goalie = GoalieTriangles(character);
                List<Triangle> near = Near(goalie, run.Contact, run.Path);
                string where = pose + " (" + x.ToString("F1") + ", " + y.ToString("F1") + ") ";
                Vector3 last = run.Contact;
                for (int i = 0; i < run.Path.Count; i++)
                {
                    Vector3 p = run.Path[i];
                    Vector3 axis = run.Turns[i] * Vector3.up;
                    float lowest = p.y - 0.075f * Mathf.Sqrt(Mathf.Max(0f, 1f - axis.y * axis.y)) - 0.025f * Mathf.Abs(axis.y);
                    if (lowest < -0.002f) { failures.Add(where + "into the ice at frame " + i); break; }
                    if (Crosses(near, last, p)) { failures.Add(where + "through the goalie at frame " + i); break; }
                    if (InFrame(p)) { failures.Add(where + "in the frame at frame " + i); break; }
                    last = p;
                }
                if (failures.Count > 0 && failures[failures.Count - 1].StartsWith(where)) continue;
                bool held = run.Path.TrueForAll(p => p == run.Contact);
                if (held) continue; // the catching glove
                Vector3 end = run.Path[run.Path.Count - 1];
                Vector3 endAxis = run.Turns[run.Turns.Count - 1] * Vector3.up;
                bool still = run.Path[run.Path.Count - 2] == end;
                bool flatOnIce = Mathf.Abs(endAxis.y) > 0.999f && Mathf.Abs(end.y - 0.025f) < 0.002f;
                if (!still || (!flatOnIce && !AgainstGear(goalie, end)))
                    failures.Add(where + "not lying still, flat on the ice (or held in the gear), at the end: " + end.ToString("F3"));
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
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
            box.Expand(0.002f);
            var near = new List<Triangle>();
            foreach (Triangle t in triangles)
            {
                Vector3 min = Vector3.Min(t.A, Vector3.Min(t.B, t.C)), max = Vector3.Max(t.A, Vector3.Max(t.B, t.C));
                if (min.x <= box.max.x && max.x >= box.min.x && min.y <= box.max.y && max.y >= box.min.y && min.z <= box.max.z && max.z >= box.min.z)
                    near.Add(t);
            }
            return near;
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

        readonly struct Triangle
        {
            public readonly Vector3 A, B, C;
            public Triangle(Vector3 a, Vector3 b, Vector3 c) { A = a; B = b; C = c; }
        }

        /// <summary>The posed gear and body (as the goalie stands now, fully set) in world space.</summary>
        static List<Triangle> GoalieTriangles(GoalieCharacter character)
        {
            var list = new List<Triangle>();
            foreach (MeshFilter f in character.Root.GetComponentsInChildren<MeshFilter>(true))
            {
                Vector3[] v = f.sharedMesh.vertices;
                int[] t = f.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                    list.Add(new Triangle(f.transform.TransformPoint(v[t[i]]), f.transform.TransformPoint(v[t[i + 1]]), f.transform.TransformPoint(v[t[i + 2]])));
            }
            var mesh = new Mesh();
            foreach (SkinnedMeshRenderer body in character.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                body.BakeMesh(mesh, true);
                Matrix4x4 m = Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one);
                Vector3[] v = mesh.vertices;
                int[] t = mesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                    list.Add(new Triangle(m.MultiplyPoint3x4(v[t[i]]), m.MultiplyPoint3x4(v[t[i + 1]]), m.MultiplyPoint3x4(v[t[i + 2]])));
            }
            Object.DestroyImmediate(mesh);
            return list;
        }

        /// <summary>True when the straight move from a to b passes through a triangle.</summary>
        static bool Crosses(List<Triangle> triangles, Vector3 a, Vector3 b)
        {
            Vector3 dir = b - a;
            if (dir.sqrMagnitude < 1e-12f) return false;
            foreach (Triangle tri in triangles)
            {
                Vector3 e1 = tri.B - tri.A, e2 = tri.C - tri.A, p = Vector3.Cross(dir, e2);
                float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-12f) continue;
                float inv = 1f / det;
                Vector3 s = a - tri.A;
                float u = Vector3.Dot(s, p) * inv;
                if (u < 0f || u > 1f) continue;
                Vector3 q = Vector3.Cross(s, e1);
                float v = Vector3.Dot(dir, q) * inv;
                if (v < 0f || u + v > 1f) continue;
                float t = Vector3.Dot(e2, q) * inv;
                if (t >= 0f && t <= 1f) return true;
            }
            return false;
        }
    }
}
