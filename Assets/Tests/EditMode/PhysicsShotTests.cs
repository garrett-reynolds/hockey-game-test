using System;
using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// Seam 2 (#109): Shots flown by Unity physics, stepped by the test at 1/120 s, judged by the Whistle rule. The rink has
    /// the ice, the posts and crossbar and the net's catch (ShotColliders); the goalie has no hit shapes yet (#111), so every
    /// Shot that is not a Goal is a Miss.
    /// </summary>
    public class PhysicsShotTests
    {
        const float Step = PhysicsShot.StepSeconds;
        static readonly RoundSettings Defaults = new RoundSettings();

        SimulationMode _oldMode;
        GameObject _root;
        Camera _camera;
        readonly List<PhysicsShot> _shots = new List<PhysicsShot>();

        [SetUp]
        public void SetUp()
        {
            _oldMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            _root = new GameObject("Physics Shot (test)");
            ShotColliders.Build(_root.transform);
            _camera = new GameObject("Shooter Camera (test)", typeof(Camera)).GetComponent<Camera>();
            _camera.transform.SetParent(_root.transform, false);
            _camera.aspect = 16f / 9f; // batch mode's screen size is not to be trusted
            PlaceEye(AttackSide.Middle, 1.0);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (PhysicsShot shot in _shots) shot.Dispose();
            _shots.Clear();
            Object.DestroyImmediate(_root);
            Physics.simulationMode = _oldMode;
        }

        /// <summary>The camera where the Round has the shooter's eye, windowSeconds into the shooting window (no bob or roll).</summary>
        void PlaceEye(AttackSide side, double windowSeconds)
        {
            Breakaway.SkatePoint(side, 1.0, out double x, out double distance);
            var ground = new Vector3((float)x, 0f, (float)-distance);
            Vector3 toNet = -ground;
            ground += toNet.normalized * (float)Breakaway.CappedDrift(Breakaway.Drift(side, Defaults.SlowDownSeconds, windowSeconds, Defaults), toNet.magnitude);
            _camera.transform.position = new Vector3(ground.x, (float)Breakaway.EyeHeight, ground.z);
            _camera.transform.LookAt(new Vector3(0f, (float)(NetFrame.FaceHeightMetres / 2.0), 0f));
        }

        /// <summary>What one flown Shot did.</summary>
        sealed class Flight
        {
            public PhysicsShot Shot;
            public double CrossedAt = double.NaN; // seconds after the Shot the puck's centre passed the Net Face plane
            public Vector3 CrossedWhere;
            public bool TouchedByNetFace;         // hit something (not the ice) before or as it crossed the Net Face plane
            public bool WentThrough;              // went into the net, then out past the catch or under the ice
            public bool OutOfViewInTheAir;        // left the camera's view before it reached the Net Face
            public bool WhistleBeforeNetFace;
            public Vector3 Highest;
        }

        Flight Fly(double x, double y, double kmh, int seed, double seconds = 4.0)
        {
            var shot = new PhysicsShot(_root.transform, _camera, Defaults, new SeededRandom(seed), name => null);
            _shots.Add(shot);
            shot.Launch(x, y, kmh);
            Physics.SyncTransforms();
            var flight = new Flight { Shot = shot };
            Vector3 before = shot.Puck.position;
            var planes = new Plane[6];
            bool wentIn = false;
            for (int i = 0; i < Math.Round(seconds / Step); i++)
            {
                Physics.Simulate(Step);
                bool whistled = shot.AfterStep(Step);
                Vector3 p = shot.Puck.position;
                if (double.IsNaN(flight.CrossedAt) && p.z >= 0f)
                {
                    float t = (0f - before.z) / (p.z - before.z);
                    flight.CrossedAt = (i + t) * Step;
                    flight.CrossedWhere = Vector3.Lerp(before, p, t);
                    flight.TouchedByNetFace = shot.Touched;
                }
                if (double.IsNaN(flight.CrossedAt))
                {
                    if (whistled) flight.WhistleBeforeNetFace = true;
                    GeometryUtility.CalculateFrustumPlanes(_camera, planes);
                    if (p.y > 1f && !GeometryUtility.TestPlanesAABB(planes, new Bounds(p, Vector3.one * PhysicsPuck.Diameter)))
                        flight.OutOfViewInTheAir = true;
                }
                if (p.y > flight.Highest.y) flight.Highest = p;
                if (ShotColliders.InsideCatch(p)) wentIn = true;
                if (wentIn && PastTheCatch(p)) flight.WentThrough = true;
                before = p;
            }
            return flight;
        }

        /// <summary>
        /// For a puck that went into the net: out through the catch, past its back, side or roof boxes' inner faces by more than
        /// the puck's radius (behind the goal line), or under the ice. Coming back out the front is not through.
        /// </summary>
        static bool PastTheCatch(Vector3 p)
        {
            const float slack = PhysicsPuck.Radius + 0.02f;
            float w = (float)NetFrame.FaceWidthMetres / 2f + ShotColliders.CatchGap + slack;
            float h = (float)NetFrame.FaceHeightMetres + ShotColliders.CatchGap + slack;
            float back = ShotColliders.NetDepth + ShotColliders.CatchGap + slack;
            return p.y < -0.02f || p.z > 0.05f && (p.z > back || Mathf.Abs(p.x) > w || p.y > h);
        }

        [TestCase(50, 50, 100)]
        [TestCase(20, 15, 160)]
        [TestCase(92, 85, 60)]
        [TestCase(35, 0, 120)]  // a spot on the ice: raised to the puck's half-thickness
        [TestCase(60, 40, 15)]  // a slow lob
        [TestCase(20, 75, 200)]
        public void The_puck_crosses_the_Net_Face_spot_at_about_the_Flight_Time(double x, double y, double kmh)
        {
            Flight flight = Fly(x, y, kmh, seed: 7, seconds: 3.0);
            Vector3 target = PhysicsPuck.Target(x, y);

            Assert.IsFalse(double.IsNaN(flight.CrossedAt), "the puck never reached the Net Face");
            // A hit in the crossing step bends the step's end point, so the crossing could not be read off it: such spots are
            // left out (fast Shots near the frame or the catch roof; ContinuousSpeculative reaches a step's travel ahead).
            Assert.IsFalse(flight.TouchedByNetFace, "hit " + flight.Shot.FirstTouched?.name + " before the Net Face: pick a spot clear of it");
            Assert.Less(Vector3.Distance(target, flight.CrossedWhere), 0.01f, "crossed at " + flight.CrossedWhere.ToString("F3") + ", aimed at " + target.ToString("F3"));
            double expected = ShotFlight.Seconds(RinkPoint.Distance(Breakaway.ShotStart(AttackSide.Middle, 1.0, Defaults),
                new RinkPoint(target.x, target.y, target.z)), kmh);
            Assert.AreEqual(expected, flight.CrossedAt, Step + 0.005, "the Round's Flight Time to the Net Face");
            Assert.GreaterOrEqual(target.y, PhysicsPuck.Thickness / 2f - 1e-6f);
        }

        [TestCase(50, 40, 100)]
        [TestCase(15, 75, 160)]
        [TestCase(85, 15, 50)]
        [TestCase(50, 70, 200)]
        public void An_open_net_Shot_is_a_Goal_and_comes_to_rest_inside_the_catch(double x, double y, double kmh)
        {
            Flight flight = Fly(x, y, kmh, seed: 3, seconds: 5.0);
            PhysicsShot shot = flight.Shot;

            Assert.AreEqual(ShotResultKind.Goal, shot.Result);
            Assert.Greater(shot.WhistleSeconds, flight.CrossedAt, "the Whistle goes once the whole puck is in, not before the Net Face");
            Assert.Less(shot.WhistleSeconds, flight.CrossedAt + 0.1, "the Whistle goes as the whole puck crosses the line");
            Vector3 p = shot.Puck.position;
            Assert.IsTrue(ShotColliders.InsideCatch(p), "ended at " + p.ToString("F3"));
            Assert.Less(shot.Puck.linearVelocity.magnitude, Defaults.WhistleVerySlowMetresPerSecond, "still moving at " + p.ToString("F3"));
            Assert.Less(p.y, PhysicsPuck.Diameter, "on the ice at " + p.ToString("F3"));
            Assert.IsFalse(flight.WentThrough);
        }

        [TestCase(-1, 100)]
        [TestCase(1, 140)]
        [TestCase(-1, 50)]
        public void A_Shot_off_the_post_that_stays_out_is_a_Miss(int side, double kmh)
        {
            // The middle of a post, half way up: just outside the Net Face (Net Face x below 0 or above 100).
            double x = side < 0 ? -NetFrame.PostWidth / 2 : 100 + NetFrame.PostWidth / 2;
            Flight flight = Fly(x, 50, kmh, seed: 5);
            PhysicsShot shot = flight.Shot;

            Assert.IsTrue(shot.Touched, "the puck hit the post");
            Assert.AreEqual(ShotResultKind.Miss, shot.Result);
            Assert.IsFalse(ShotColliders.InsideCatch(shot.Puck.position), "stayed out: ended at " + shot.Puck.position.ToString("F3"));
            Assert.Less(shot.WhistleSeconds, Defaults.WhistleCapSeconds + Step);
        }

        [Test]
        public void No_puck_ends_up_through_the_catch_over_many_seeded_Shots()
        {
            var random = new System.Random(109);
            AttackSide[] sides = { AttackSide.Left, AttackSide.Middle, AttackSide.Right };
            int shots = 0, goals = 0;
            var through = new List<string>();
            for (int n = 0; n < 150; n++)
            {
                AttackSide side = sides[n % 3];
                double window = random.NextDouble() * Defaults.ShootingWindowSeconds;
                PlaceEye(side, window);
                double x = 3 + random.NextDouble() * 94, y = random.NextDouble() * 96;
                double kmh = n % 5 == 0 ? TestShotSpeed.MaxKmh : 20 + random.NextDouble() * (TestShotSpeed.MaxKmh - 20); // fast Shots tunnel: every fifth at the top speed
                Flight flight = Fly(x, y, kmh, seed: n, seconds: 3.0);
                shots++;
                if (flight.Shot.Result == ShotResultKind.Goal) goals++;
                if (flight.WentThrough && through.Count < 5)
                    through.Add($"({x:0.#}, {y:0.#}) at {kmh:0} km/h from {side} ended at {flight.Shot.Puck.position:F3}");
                flight.Shot.Dispose();
                _shots.Remove(flight.Shot);
            }
            Assert.IsEmpty(through, string.Join("\n", through));
            Assert.Greater(goals, shots * 9 / 10, "most Shots at the open Net Face go in (" + goals + " of " + shots + ")");
        }

        [Test]
        public void A_slow_lob_gets_no_Whistle_before_it_reaches_the_Net_Face()
        {
            PlaceEye(AttackSide.Middle, 0); // furthest out, as the window opens: the slowest Shot lobs high over the top of the view
            Physics.SyncTransforms();
            Flight flight = Fly(50, 50, ShotFlight.MinSpeedKmh, seed: 11, seconds: 4.0);

            Assert.IsTrue(flight.OutOfViewInTheAir, "the lob goes over the top of the view (else this tests nothing): highest " + flight.Highest.ToString("F2") + ", crossed at " + flight.CrossedAt + " s, eye " + _camera.transform.position.ToString("F2"));
            Assert.IsFalse(flight.WhistleBeforeNetFace, "a Whistle before the Net Face, at " + flight.Shot.WhistleSeconds + " s");
            Assert.AreEqual(ShotResultKind.Goal, flight.Shot.Result);
        }

        [Test]
        public void The_puck_starts_below_the_view_and_is_not_gone_for_it()
        {
            Flight flight = Fly(50, 50, 60, seed: 2, seconds: Step * 2);
            var planes = GeometryUtility.CalculateFrustumPlanes(_camera);
            Vector3 start = flight.Shot.Start;

            Assert.IsFalse(GeometryUtility.TestPlanesAABB(planes, new Bounds(start, Vector3.one * PhysicsPuck.Diameter)), "seen at the start " + start.ToString("F3"));
            Assert.IsFalse(flight.Shot.HasWhistled);
            Assert.IsFalse(flight.Shot.Touched, "lying on the ice is no touch");
        }

        [Test]
        public void A_Test_Shot_launches_the_same_as_a_real_Shot()
        {
            var launched = new List<PhysicsShot>();
            foreach (bool isTestShot in new[] { false, true })
            {
                var round = new Round(ScriptedRandom.Forcing(AttackSide.Right), Defaults, FakeReach.NeverReaches(), 0);
                round.StartRound();
                round.Tick(Defaults.SkateInSeconds + Defaults.SlowDownSeconds + 0.8);
                round.SubmitShot(30, 65, 110, isTestShot);
                SubmittedShot submitted = round.LastShot.Value;
                Assert.AreEqual(isTestShot, submitted.IsTestShot);

                PlaceEye(round.AttackSide, 0.8);
                var shot = new PhysicsShot(_root.transform, _camera, Defaults, new SeededRandom(4), name => null);
                _shots.Add(shot);
                shot.Launch(submitted.X, submitted.Y, submitted.SpeedKmh);
                launched.Add(shot);

                RinkPoint roundStart = Breakaway.ShotStart(round.AttackSide, 0.8, Defaults);
                Vector3 cameraStart = PhysicsPuck.Start(_camera, Defaults);
                Assert.Less(Vector3.Distance(cameraStart, new Vector3((float)roundStart.X, (float)roundStart.Y, (float)roundStart.Z)), 1e-3f,
                    "the launch starts where the Round starts its Flight Time");
            }
            Assert.AreEqual(launched[0].Start, launched[1].Start);
            Assert.AreEqual(launched[0].LaunchVelocity, launched[1].LaunchVelocity);
            Assert.AreEqual(launched[0].Puck.angularVelocity, launched[1].Puck.angularVelocity);
        }
    }
}
