using System.Collections.Generic;
using System.Text;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// Seam 2 (#111): the goalie's gear hit shapes, with physics stepped by the test at 1/120 s as the game does: each step the
    /// goalie is posed for the end of the step, the hit shapes moved to the gear, then physics simulated. Many seeded Shots
    /// (the real launch) at each piece of gear, with the gear still, arriving (the Save Reaction comes in as the puck does) and
    /// swinging (in and out four times a second, so gear hits the puck at full speed), at 30 and 45 m/s. Every Shot must touch
    /// the gear, and none may end up through it. A control run with thin boxes and a discrete puck shows the test can see a tunnel.
    /// </summary>
    public class GoalieHitShapesTests
    {
        const float Step = PhysicsShot.StepSeconds;
        const float ArriveSeconds = 0.18f, SwingHz = 4f, After = 0.25f;
        static readonly RoundSettings Defaults = new RoundSettings();

        public enum Motion { Still, Arriving, Swinging }

        // Net Face spots (metres right of the centre, metres up from the ice) on each piece of gear, shooter's view.
        static readonly (string name, float right, float up)[] Targets =
        {
            ("glove", 0.6f, 0.95f), ("blocker", -0.6f, 0.95f), ("pad left", 0.5f, 0.15f), ("pad right", -0.5f, 0.15f),
            ("stick", 0.05f, 0.03f), ("body", 0f, 0.7f),
        };
        static readonly float[] SpeedsKmh = { 30f * 3.6f, 45f * 3.6f };

        SimulationMode _oldMode;
        GameObject _root;
        Camera _camera;
        GoalieCharacter _goalie;

        [SetUp]
        public void SetUp()
        {
            _oldMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            _root = new GameObject("Goalie Hit Shapes (test)");
            ShotColliders.Build(_root.transform);
            _camera = new GameObject("Shooter Camera (test)", typeof(Camera)).GetComponent<Camera>();
            _camera.transform.SetParent(_root.transform, false);
            _camera.aspect = 16f / 9f; // batch mode's screen size is not to be trusted
            _goalie = new GoalieCharacter(_root.transform);
            Assert.IsTrue(_goalie.Present, "Assets/Resources/Models/goalie-dressing.fbx is missing");
            _goalie.Root.SetActive(true);
            _goalie.Spot = new Vector3(0f, 0f, GoalieCharacter.StandZ);
            _goalie.ShowReaction(0f);
        }

        // PuckReach keeps a static set of gear colliders: every hit shape built here is disposed, so none is left in it.
        readonly List<GoalieHitShapes> _built = new List<GoalieHitShapes>();
        readonly List<GoalieView> _views = new List<GoalieView>();

        GoalieHitShapes MakeHits(float minDepth = GoalieHitShapes.MinDepth)
        {
            var hits = new GoalieHitShapes(_goalie, _root.transform, minDepth);
            _built.Add(hits);
            return hits;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GoalieHitShapes hits in _built) hits.Dispose();
            foreach (GoalieView view in _views) view.Dispose();
            _built.Clear();
            _views.Clear();
            Object.DestroyImmediate(_root);
            Physics.simulationMode = _oldMode;
        }

        /// <summary>The camera where the Round has the shooter's eye at the end of the skate-in, a second into the shooting window.</summary>
        void PlaceEye(AttackSide side)
        {
            Breakaway.SkatePoint(side, 1.0, out double x, out double distance);
            var ground = new Vector3((float)x, 0f, (float)-distance);
            Vector3 toNet = -ground;
            ground += toNet.normalized * (float)Breakaway.CappedDrift(Breakaway.Drift(side, Defaults.SlowDownSeconds, 1.0, Defaults), toNet.magnitude);
            _camera.transform.position = new Vector3(ground.x, (float)Breakaway.EyeHeight, ground.z);
            _camera.transform.LookAt(new Vector3(0f, (float)(NetFrame.FaceHeightMetres / 2.0), 0f));
        }

        /// <summary>
        /// Where the swing is when the Shot is launched, so the gear is swinging across the line as the puck arrives: 75 to 95 %
        /// of the way in (the Save Reaction's pose is 100 %), going in or coming back out by turns.
        /// </summary>
        static float SwingPhase(float flight, System.Random rng, int seed)
        {
            float arrivalSet = 0.75f + 0.2f * (float)rng.NextDouble();
            float angle = Mathf.Acos(1f - 2f * arrivalSet);                    // set = 0.5 - 0.5 cos(angle), going in
            if (seed % 2 == 1) angle = 2f * Mathf.PI - angle;                  // coming back out
            float period = 1f / SwingHz;
            float at = angle / (2f * Mathf.PI * SwingHz) - flight;             // phase + flight must land on that angle
            return ((at % period) + period) % period;
        }

        static float SetAt(Motion motion, float t, float flight)
        {
            switch (motion)
            {
                case Motion.Arriving: return Mathf.Clamp01((t - (flight - ArriveSeconds)) / ArriveSeconds);
                case Motion.Swinging: return 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * SwingHz * t);
                default: return 0f;
            }
        }

        sealed class Tally
        {
            public int Shots, Touched, Through;
            public readonly List<string> Failures = new List<string>();
        }

        /// <summary>
        /// Fires seeds Shots at each target at each speed, and tallies how many touched the gear and how many ended up through it.
        /// Still gear is aimed at: its own pieces, each one's middle seen from the shooter's eye. Moving gear gets Shots at
        /// the Net Face spots its Save Reaction covers (it comes to the line, wherever it started).
        /// </summary>
        Tally Fire(Motion motion, GoalieHitShapes hits, int seeds, bool discretePuck, float[] speedsKmh)
        {
            var tally = new Tally();
            var gear = new HashSet<Collider>(hits.Colliders);
            foreach (float kmh in speedsKmh)
            for (int seed = 0; seed < seeds; seed++)
            {
                var rng = new System.Random(1000 * seed + (int)kmh);
                PlaceEye((AttackSide)(seed % 3));
                Vector3 eye = PhysicsPuck.Start(_camera, Defaults);
                _goalie.ShowReaction(0f);
                hits.Snap();
                var aims = new List<(string name, double x, double y)>();
                if (motion == Motion.Still)
                {
                    // Each piece's middle as seen from this eye: aimed at what the eye sees there (the first gear surface on the way,
                    // 2 cm in), so a piece behind another is not aimed at through it.
                    foreach (Collider piece in hits.Colliders)
                    {
                        Vector3 middle = piece.bounds.center + new Vector3(Jitter(rng, 0.03f), Jitter(rng, 0.03f), 0f);
                        Vector3 look = (middle - eye).normalized;
                        float nearest = float.MaxValue;
                        foreach (RaycastHit hit in Physics.RaycastAll(eye, look, 30f, ~0, QueryTriggerInteraction.Ignore))
                            if (gear.Contains(hit.collider)) nearest = Mathf.Min(nearest, hit.distance);
                        if (nearest == float.MaxValue) continue; // the line misses every piece: nothing to aim at
                        Vector3 point = eye + look * (nearest + 0.02f);
                        Vector3 onNetFace = eye + (point - eye) * (-eye.z / (point.z - eye.z));
                        NetFaceMapping.FromOffsetFromCentre(onNetFace.x, onNetFace.y - NetFaceMapping.HeightMetres / 2.0, out double x, out double y);
                        aims.Add((piece.attachedRigidbody.name + " " + piece.name, x, y));
                    }
                }
                else
                {
                    foreach (var target in Targets)
                    {
                        NetFaceMapping.FromOffsetFromCentre(target.right + Jitter(rng, 0.1f), Mathf.Max(0.03f, target.up + Jitter(rng, 0.06f)) - NetFaceMapping.HeightMetres / 2.0, out double x, out double y);
                        aims.Add((target.name, x, y));
                    }
                }
                foreach (var aim in aims) Fly(motion, hits, gear, aim.x, aim.y, kmh, seed, rng, discretePuck, aim.name, tally);
            }
            return tally;
        }

        static float Jitter(System.Random rng, float half) => (float)(rng.NextDouble() * 2.0 - 1.0) * half;

        void Fly(Motion motion, GoalieHitShapes hits, HashSet<Collider> gear, double x, double y, float kmh, int seed, System.Random rng,
            bool discretePuck, string aimedAt, Tally tally)
        {
            using (var shot = new PhysicsShotDisposer(new PhysicsShot(_root.transform, _camera, Defaults, new SeededRandom(seed), name => null)))
            {
                bool touched = false;
                shot.Shot.Hit += (other, impulse) => touched |= gear.Contains(other);
                shot.Shot.Launch(x, y, kmh);
                if (discretePuck) shot.Shot.Puck.collisionDetectionMode = CollisionDetectionMode.Discrete;

                // The goalie makes the Save Reaction for this Shot's line; he meets it at his own spot, not at the Net Face.
                Vector3 from = shot.Shot.Start, to = shot.Shot.Target;
                _goalie.Spot = new Vector3(0f, 0f, GoalieCharacter.StandZ);
                _goalie.React(SaveReactions.For(x, y), from, to, 0f, true);
                float speed = kmh / 3.6f;
                float flight = (to - from).magnitude * (GoalieCharacter.StandZ - from.z) / (to.z - from.z) / speed;
                float phase = motion == Motion.Swinging ? SwingPhase(flight, rng, seed) : 0f;
                _goalie.ShowReaction(SetAt(motion, phase, flight));
                hits.Snap();
                Physics.SyncTransforms();

                bool through = false;
                string where = null;
                Vector3 last = shot.Shot.Puck.position;
                for (float t = 0f; t < flight + After; t += Step)
                {
                    _goalie.ShowReaction(SetAt(motion, t + Step + phase, flight)); // the goalie for the end of this step
                    hits.Move();
                    Physics.Simulate(Step);
                    shot.Shot.AfterStep(Step);
                    Vector3 p = shot.Shot.Puck.position;

                    string tunnel = Tunnel(gear, last, p);
                    if (tunnel != null) { through = true; where = tunnel + " at t " + t.ToString("F3"); }
                    last = p;
                    if (through) break;
                }

                tally.Shots++;
                if (touched) tally.Touched++;
                else tally.Failures.Add($"{motion} {kmh / 3.6f:0} m/s seed {seed} aimed {aimedAt}: never touched the gear");
                if (through)
                {
                    tally.Through++;
                    tally.Failures.Add($"{motion} {kmh / 3.6f:0} m/s seed {seed} aimed {aimedAt}: through, {where}");
                }
            }
        }

        /// <summary>
        /// Whether the puck's centre, moving from `from` to `to` in one step, got through a gear hit shape (gear as it is now), and
        /// how; else null. Through is: crossing the whole shape in the step (in one side and out the other), or ending sunk in it
        /// deeper than the puck is wide from its middle to its rim (its radius). Pushing a little way into a shape is not through: that is the soft edge of any contact.
        /// </summary>
        static string Tunnel(HashSet<Collider> gear, Vector3 from, Vector3 to)
        {
            Vector3 travel = to - from;
            if (travel.sqrMagnitude > 1e-8f)
            {
                // A ray starting inside a shape does not see it: a shape seen from both ends is one the step went clean across.
                var entered = new HashSet<Collider>();
                foreach (RaycastHit hit in Physics.RaycastAll(from, travel.normalized, travel.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    if (gear.Contains(hit.collider)) entered.Add(hit.collider);
                foreach (RaycastHit hit in Physics.RaycastAll(to, -travel.normalized, travel.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    if (entered.Contains(hit.collider)) return "crossed clean across " + Describe(hit.collider);
            }
            foreach (Collider c in gear)
                if (SunkIn(c, to, PhysicsPuck.Radius)) return "sunk in " + Describe(c);
            return null;
        }

        static string Describe(Collider c) => c.attachedRigidbody.name + " (" + c.GetType().Name + ")";

        /// <summary>The point is inside a box or capsule by at least depth (metres) from every surface.</summary>
        static bool SunkIn(Collider c, Vector3 world, float depth)
        {
            Vector3 local = c.transform.InverseTransformPoint(world);
            if (c is BoxCollider box)
            {
                Vector3 inset = box.size / 2f - Vector3.one * depth;
                Vector3 d = local - box.center;
                return inset.x > 0f && inset.y > 0f && inset.z > 0f && Mathf.Abs(d.x) < inset.x && Mathf.Abs(d.y) < inset.y && Mathf.Abs(d.z) < inset.z;
            }
            var capsule = (CapsuleCollider)c; // built along its own y
            float along = Mathf.Clamp(local.y - capsule.center.y, -(capsule.height / 2f - capsule.radius), capsule.height / 2f - capsule.radius);
            var onAxis = new Vector3(capsule.center.x, capsule.center.y + along, capsule.center.z);
            return (local - onAxis).magnitude < capsule.radius - depth;
        }

        // ---- Ghost Saves: a Shot that passes near moving gear without reaching it must not be gear touched (#111, PuckReach keeps every contact with moving gear) ----

        /// <summary>
        /// Flies one swinging-gear Shot. free: the puck ignores the gear (so it flies its true line), and the least gap (metres, a
        /// negative gap is overlap) between the puck and any gear hit shape, over the Shot, is returned: along each step's travel and
        /// against the gear where it is then, four times a step. Not free:
        /// the real Shot, and whether it was gear touched.
        /// </summary>
        (float gap, bool touched, string how) FlyNear(GoalieHitShapes hits, double x, double y, double reactX, double reactY, float kmh, int seed, bool free)
        {
            var rng = new System.Random(77 * seed + 5);
            using (var shot = new PhysicsShotDisposer(new PhysicsShot(_root.transform, _camera, Defaults, new SeededRandom(seed), name => null)))
            {
                shot.Shot.Launch(x, y, kmh);
                if (free)
                    foreach (Collider puck in shot.Shot.Puck.GetComponentsInChildren<Collider>())
                    foreach (Collider piece in hits.Colliders) Physics.IgnoreCollision(puck, piece, true);
                Vector3 from = shot.Shot.Start, to = shot.Shot.Target;
                _goalie.Spot = new Vector3(0f, 0f, GoalieCharacter.StandZ);
                _goalie.React(SaveReactions.For(reactX, reactY), from, PhysicsPuck.Target(reactX, reactY), 0f, true); // he reacts to the spot beside the line
                float flight = (to - from).magnitude * (GoalieCharacter.StandZ - from.z) / (to.z - from.z) / (kmh / 3.6f);
                float phase = SwingPhase(flight, rng, seed);
                _goalie.ShowReaction(SetAt(Motion.Swinging, phase, flight));
                hits.Snap();
                Physics.SyncTransforms();

                float gap = float.MaxValue;
                string tunnel = null;
                Vector3 last = shot.Shot.Puck.position;
                float dt = free ? Step / 4f : Step; // flying free it is looked at four times a step, with the gear in step with it
                for (float t = 0f; t < flight + After; t += dt)
                {
                    _goalie.ShowReaction(SetAt(Motion.Swinging, t + dt + phase, flight));
                    hits.Move();
                    Physics.Simulate(dt);
                    shot.Shot.AfterStep(dt);
                    Vector3 now = shot.Shot.Puck.position;
                    if (free) gap = Mathf.Min(gap, Gap(hits, last, now));
                    Vector3 before = last;
                    last = now;
                    if (!free && tunnel == null) tunnel = Tunnel(new HashSet<Collider>(hits.Colliders), before, now);
                    if (!free && shot.Shot.GearTouched) return (0f, true, tunnel);
                }
                return (gap, shot.Shot.GearTouched, tunnel);
            }
        }

        /// <summary>The least gap between the puck (its radius) and any gear hit shape, from points along a to b.</summary>
        static float Gap(GoalieHitShapes hits, Vector3 a, Vector3 b)
        {
            float least = float.MaxValue;
            for (int i = 0; i <= 6; i++)
            {
                Vector3 point = Vector3.Lerp(a, b, i / 6f);
                foreach (Collider piece in hits.Colliders)
                {
                    if (piece.bounds.SqrDistance(point) > 0.25f) continue;
                    least = Mathf.Min(least, Vector3.Distance(piece.ClosestPoint(point), point) - PhysicsPuck.Radius);
                }
            }
            return least;
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void A_Shot_that_passes_near_moving_gear_without_reaching_it_is_not_gear_touched()
        {
            var hits = MakeHits();
            var rng = new System.Random(4242);
            // Shots by how far the puck flies from the gear (its least gap, flying free): gear touched / Shots in each band.
            var edges = new[] { float.MinValue, -0.03f, 0f, 0.02f, 0.05f, 0.10f, 0.20f };
            var labels = new[] { "overlap deeper than 3 cm", "overlap up to 3 cm", "clear by 0-2 cm", "clear by 2-5 cm", "clear by 5-10 cm", "clear by 10-20 cm", "clear by 20 cm or more" };
            var shots = new int[edges.Length];
            var touched = new int[edges.Length];
            var ghosts = new List<string>();
            var tunnels = new List<string>();
            int total = 0;
            foreach (float kmh in SpeedsKmh)
            for (int seed = 1; seed <= 90; seed++)
            {
                PlaceEye((AttackSide)(seed % 3));
                NetFaceMapping.FromOffsetFromCentre(Jitter(rng, 1.3f), 0.03f + (float)rng.NextDouble() * 1.4f - NetFaceMapping.HeightMetres / 2.0, out double x, out double y);
                // He reacts to a spot 0.1 to 0.5 m from the Shot's, so his gear swings beside its line.
                double shift = 0.1 + 0.4 * rng.NextDouble(), turn = 2.0 * System.Math.PI * rng.NextDouble();
                NetFaceMapping.ToMetres(x, y, out double metresRight, out double metresUp);
                NetFaceMapping.FromOffsetFromCentre(metresRight + shift * System.Math.Cos(turn), metresUp - NetFaceMapping.HeightMetres / 2.0 + shift * System.Math.Sin(turn), out double rx, out double ry);
                var free = FlyNear(hits, x, y, rx, ry, kmh, seed, free: true);
                var real = FlyNear(hits, x, y, rx, ry, kmh, seed, free: false);
                int band = 0;
                while (band + 1 < edges.Length && free.gap >= edges[band + 1]) band++;
                shots[band]++;
                if (real.touched) touched[band]++;
                if (real.how != null) tunnels.Add($"{kmh / 3.6f:0} m/s seed {seed} aim ({x:0.0}, {y:0.0}) free gap {free.gap * 100f:0.0} cm: {real.how}");
                if (!real.touched && free.gap < -0.03f) ghosts.Add($"MISSED {kmh / 3.6f:0} m/s seed {seed} aim ({x:0.0}, {y:0.0}) react ({rx:0.0}, {ry:0.0}) free gap {free.gap * 100f:0.0} cm");
                if (real.touched && free.gap >= 0.02f) ghosts.Add($"{kmh / 3.6f:0} m/s seed {seed} aim ({x:0.0}, {y:0.0}) gap {free.gap * 100f:0.0} cm");
                total++;
            }
            var report = new StringBuilder($"GHOST SAVE MEASUREMENT, {total} Shots at swinging gear (gear touched / Shots)\n");
            for (int i = 0; i < edges.Length; i++) report.Append("  ").Append(labels[i]).Append(": ").Append(touched[i]).Append('/').Append(shots[i]).Append('\n');
            foreach (string tunnel in tunnels) report.Append("  TUNNEL: ").Append(tunnel).Append('\n');
            foreach (string ghost in ghosts) report.Append("  ghost: ").Append(ghost).Append('\n');
            TestContext.Out.WriteLine(report.ToString());

            Assert.GreaterOrEqual(shots[3] + shots[4] + shots[5] + shots[6], 5, "enough near misses (clear by 2 cm or more) to measure with\n" + report);
            Assert.AreEqual(0, tunnels.Count, "the puck went through gear\n" + report);
            Assert.AreEqual(shots[0], touched[0], "every Shot that flew deep into the gear touched it\n" + report);
            // The look-ahead (PuckReach) keeps a foreseen contact when the puck comes within a few centimetres of where the gear
            // may be, and a turning or speeding piece is only roughly foreseen: some Shots that pass a little clear of it are still
            // touched. Before the look-ahead followed the gear's move every one of them was (11 of 11 here, 26 of 27 in a
            // 300-Shot run); it must keep most of them clear now.
            int clear = shots[3] + shots[4] + shots[5] + shots[6], clearTouched = touched[3] + touched[4] + touched[5] + touched[6];
            Assert.LessOrEqual(clearTouched, 0.7f * clear, "ghost Saves: " + clearTouched + " of " + clear + " Shots that passed 2 cm or more clear were gear touched\n" + report);
        }

        /// <summary>Disposes the Shot (a using block for it).</summary>
        sealed class PhysicsShotDisposer : System.IDisposable
        {
            public readonly PhysicsShot Shot;
            public PhysicsShotDisposer(PhysicsShot shot) { Shot = shot; }
            public void Dispose() => Shot.Dispose();
        }

        static string Report(Tally tally)
        {
            var sb = new StringBuilder($"{tally.Touched}/{tally.Shots} touched, {tally.Through} through\n");
            foreach (string failure in tally.Failures.GetRange(0, Mathf.Min(3, tally.Failures.Count))) sb.Append(failure).Append('\n');
            return sb.ToString();
        }

        // Moving gear: 6 targets x 2 speeds x 6 seeds = 72 Shots. Still gear: each hit shape x 2 speeds x 3 eyes (about 110).
        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void Every_Shot_at_the_gear_touches_it_and_none_ends_up_through_it([Values(Motion.Still, Motion.Arriving, Motion.Swinging)] Motion motion)
        {
            var hits = MakeHits();
            Tally tally = Fire(motion, hits, seeds: motion == Motion.Still ? 3 : 6, discretePuck: false, SpeedsKmh);

            Assert.GreaterOrEqual(tally.Shots, 70, "Shots fired");
            Assert.LessOrEqual(tally.Shots, 120, "Shots fired");
            Assert.AreEqual(0, tally.Failures.Count, Report(tally));
        }

        [Test]
        public void A_discrete_puck_against_thin_gear_does_tunnel_so_the_test_can_see_one()
        {
            var hits = MakeHits(minDepth: 0.01f);
            Tally tally = Fire(Motion.Swinging, hits, seeds: 5, discretePuck: true, new[] { 45f * 3.6f });

            Assert.Greater(tally.Through, 0, "no tunnel seen in " + tally.Shots + " Shots: the test cannot see one");
        }

        /// <summary>Where the glove's hit shape is.</summary>
        static Vector3 Glove(Transform hitRoot) => hitRoot.Find("Hit " + GearPiece.Catcher).GetComponent<Collider>().bounds.center;

        [Test]
        public void The_view_keeps_the_hit_shapes_on_his_pose_while_he_is_shown_and_off_while_he_is_hidden()
        {
            var view = new GoalieView(_root.transform, _goalie);
            _views.Add(view);
            Transform hitRoot = _root.transform.Find(GoalieHitShapes.RootName);
            Assert.IsNotNull(hitRoot, "the view builds the hit shapes");
            Assert.IsFalse(hitRoot.gameObject.activeSelf, "hidden with the goalie");

            view.Show(true);
            Assert.IsTrue(hitRoot.gameObject.activeSelf);
            _goalie.React(SaveReaction.Glove, new Vector3(0f, 0.3f, -3f), new Vector3(0.8f, 1f, 0f), 0f, true);
            view.SetProgress(0f);
            view.MoveHitShapes();
            Physics.Simulate(Step);
            Vector3 ready = Glove(hitRoot);
            view.SetProgress(1f);
            view.MoveHitShapes();
            Physics.Simulate(Step);
            Vector3 reaching = Glove(hitRoot);
            Assert.Greater(Vector3.Distance(ready, reaching), 0.05f, "the hit shapes went with the gear into the reaction");

            view.Show(false);
            Assert.IsFalse(hitRoot.gameObject.activeSelf);
        }

        [Test]
        public void Each_physics_step_poses_the_goalie_for_the_end_of_the_step_and_moves_the_hit_shapes_there()
        {
            var settings = new RoundSettings { ReactionWobbleSeconds = 0 };
            var cameraObject = new GameObject("Camera (test)", typeof(Camera));
            var driver = new GameObject("Breakaway (test)");
            PhysicsShotEffects effects = null;
            try
            {
                var view = new GoalieView(_root.transform, _goalie);
                _views.Add(view);
                effects =new PhysicsShotEffects(_root.transform, cameraObject.GetComponent<Camera>(), new SeededRandom(7), view, name => null);
                Physics.SyncTransforms();
                var round = new Round(new SeededRandom(1), settings, FakeReach.NeverReaches(), 0);
                var breakaway = driver.AddComponent<BreakawayView>();
                breakaway.Init(round, cameraObject.GetComponent<Camera>(), view, () => effects.FlightSeconds);
                Transform hitRoot = _root.transform.Find(GoalieHitShapes.RootName);

                const float Frame = 1f / 60f;
                round.StartRound();
                for (int frame = 0; frame < 60 * 30 && round.Phase != RoundPhase.ResultHold; frame++)
                {
                    for (int step = 0; step < 2; step++) // a frame is 1/60 s: two physics steps of 1/120 s
                    {
                        Physics.Simulate(PhysicsShot.StepSeconds);
                        effects.AfterPhysicsStep(PhysicsShot.StepSeconds);
                    }
                    round.Tick(Frame);
                    view.Show(true);
                    if (round.Phase == RoundPhase.ShootingWindow && round.PhaseSecondsElapsed > 0.5) round.SubmitShot(70, 60, 100, false);
                    effects.Update(round, Frame);
                    breakaway.Step(Frame * Time.timeScale, Frame);
                }
                Assume.That(round.Phase, Is.EqualTo(RoundPhase.ResultHold));

                // The result hold's clock is at its start: a physics step a little later poses him further into the reaction.
                breakaway.PoseForPhysics(0f);
                Physics.Simulate(Step);
                Vector3 now = Glove(hitRoot);
                breakaway.PoseForPhysics(0.15f);
                Physics.Simulate(Step);
                Vector3 later = Glove(hitRoot);
                Assert.Greater(Vector3.Distance(now, later), 0.02f, "the hit shapes follow the pose of the step's end");
                Time.timeScale = 1f;
            }
            finally
            {
                effects?.Dispose();
                Object.DestroyImmediate(driver);
                Object.DestroyImmediate(cameraObject);
                Time.timeScale = 1f;
            }
        }

        [Test]
        public void Each_hit_shape_follows_its_gear_and_the_gear_has_its_feel_group()
        {
            var hits = MakeHits();
            _goalie.ShowReaction(0f);
            hits.Snap();
            _goalie.ShowReaction(1f, 20f);
            hits.Move();
            Physics.Simulate(Step);

            var materials = new HashSet<PhysicsMaterial>();
            foreach (Collider c in hits.Colliders)
            {
                Rigidbody body = c.attachedRigidbody;
                Assert.IsNotNull(body, c.name);
                Assert.IsTrue(body.isKinematic && body.collisionDetectionMode == CollisionDetectionMode.ContinuousSpeculative, body.name);
                Assert.IsFalse(body.transform.IsChildOf(_goalie.Root.transform), "not on the goalie's bones: " + body.name);
                Assert.IsTrue(ShotColliders.IsGear(c.sharedMaterial), c.name);
                materials.Add(c.sharedMaterial);
            }
            CollectionAssert.AreEquivalent(new[] { ShotColliders.GearPads, ShotColliders.GearStick, ShotColliders.GearBody, ShotColliders.GearGlove }, materials);
            Assert.AreEqual(0.02f, ShotColliders.GearGlove.bounciness);
            Assert.AreEqual(0.25f, ShotColliders.GearPads.bounciness);
            Assert.AreEqual(0.2f, ShotColliders.GearStick.bounciness);
            Assert.AreEqual(0.1f, ShotColliders.GearBody.bounciness);
            Assert.AreEqual(PhysicsMaterialCombine.Maximum, ShotColliders.GearPads.bounceCombine);
            Assert.IsFalse(ShotColliders.IsGear(ShotColliders.Boards));
        }
    }
}
