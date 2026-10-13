using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// A gear touch (#111): the physics puck is soaked once per hit (it keeps 70 % of its speed, the glove 30 %), the Shot is
    /// gear touched (the Whistle rule gives a Save, or a Goal if the puck goes in), and the save sound plays at each touch.
    /// Stepped physics at 1/120 s, as the game does. The gear here is plain boxes of the gear materials (ShotColliders.Gear*);
    /// the goalie's own hit shapes are in GoalieHitShapesTests.
    /// </summary>
    public class GearTouchTests
    {
        const float Step = PhysicsShot.StepSeconds;
        static readonly RoundSettings Defaults = new RoundSettings();

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
            ShotColliders.Build(_root.transform);
            _camera = new GameObject("Shooter Camera (test)", typeof(Camera)).GetComponent<Camera>();
            _camera.transform.SetParent(_root.transform, false);
            _camera.aspect = 16f / 9f; // batch mode's screen size is not to be trusted
            Breakaway.SkatePoint(AttackSide.Middle, 1.0, out double x, out double distance);
            var ground = new Vector3((float)x, 0f, (float)-distance);
            Vector3 toNet = -ground;
            ground += toNet.normalized * (float)Breakaway.CappedDrift(Breakaway.Drift(AttackSide.Middle, Defaults.SlowDownSeconds, 1.0, Defaults), toNet.magnitude);
            _camera.transform.position = new Vector3(ground.x, (float)Breakaway.EyeHeight, ground.z);
            _camera.transform.LookAt(new Vector3(0f, (float)(NetFrame.FaceHeightMetres / 2.0), 0f));
        }

        [TearDown]
        public void TearDown()
        {
            _effects?.Dispose();
            _effects = null;
            Object.DestroyImmediate(_root);
            Physics.simulationMode = _oldMode;
        }

        const string WallName = "Gear wall (test)";

        /// <summary>A static box of this feel (a gear material, or a copy of one that the puck does not know as gear), centred at `at`, turned to `facing`.</summary>
        GameObject Box(PhysicsMaterial feel, Vector3 size, Vector3 at, Quaternion facing, string name = "Gear (test)", bool onBody = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.transform.SetPositionAndRotation(at, facing);
            if (onBody) go.AddComponent<Rigidbody>().isKinematic = true;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.sharedMaterial = feel;
            Physics.SyncTransforms();
            return go;
        }

        /// <summary>A wall across the Net Face line, 1 m out: every Shot at the middle of the Net Face hits it head on.</summary>
        GameObject Wall(PhysicsMaterial feel, string name = WallName, bool onBody = false) =>
            Box(feel, new Vector3(6f, 4f, 0.3f), new Vector3(0f, 1.5f, -1f), Quaternion.identity, name, onBody);

        static PhysicsMaterial NotGear(PhysicsMaterial like) => new PhysicsMaterial("Not gear (test)")
        {
            bounciness = like.bounciness, dynamicFriction = like.dynamicFriction, staticFriction = like.staticFriction,
            frictionCombine = like.frictionCombine, bounceCombine = like.bounceCombine,
        };

        // ---- The soak: once per hit ----

        /// <summary>Flies one Shot at a wall of this feel and returns the puck's speed 2 steps after the first hit, how many touches were told, and the step of the first hit.</summary>
        (float speed, int touches, int hitStep) HeadOn(PhysicsMaterial feel, bool overlapped = false)
        {
            foreach (string name in new[] { WallName, WallName + " 2" })
            {
                GameObject old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
            }
            Wall(feel);
            // A second piece overlapping the first, its front face (the one the puck meets) level with it: both report the same hit.
            if (overlapped) Box(feel, new Vector3(6f, 4f, 0.4f), new Vector3(0f, 1.5f, -0.95f), Quaternion.identity, WallName + " 2");
            using (var shot = new PhysicsShot(_root.transform, _camera, Defaults, new SeededRandom(3), name => null))
            {
                int touches = 0, hitStep = -1, step = 0;
                shot.GearTouch += _ => touches++;
                shot.Hit += (other, impulse) => { if (other.sharedMaterial == feel && hitStep < 0) hitStep = step; };
                shot.Launch(50, 50, 100);
                for (step = 1; step < 240; step++)
                {
                    Physics.Simulate(Step);
                    shot.AfterStep(Step);
                    if (hitStep >= 0 && step == hitStep + 2) return (shot.Puck.linearVelocity.magnitude, touches, hitStep);
                }
            }
            Assert.Fail("the puck never hit the wall");
            return default;
        }

        [TestCase(0.7f, false, TestName = "Pads keep 70 % of the speed, once, not 0.7 three times")]
        [TestCase(0.3f, true, TestName = "The glove keeps 30 % of the speed, once")]
        public void A_hit_soaks_the_puck_once_however_many_of_its_boxes_report_it(float keep, bool glove)
        {
            PhysicsMaterial feel = glove ? ShotColliders.GearGlove : ShotColliders.GearPads;
            var soaked = HeadOn(feel);
            var plain = HeadOn(NotGear(feel)); // the same physics (same bounce and grip), but not gear: no soak

            Assert.AreEqual(plain.hitStep, soaked.hitStep, "same Shot, same hit");
            Assert.AreEqual(1, soaked.touches, "one touch told");
            Assert.Greater(plain.speed, 0.5f, "the control keeps some speed to compare with");
            Assert.AreEqual(keep, soaked.speed / plain.speed, 0.08f, $"soaked {soaked.speed:F2} m/s, unsoaked {plain.speed:F2} m/s");
        }

        [Test]
        public void A_hit_on_two_overlapping_pieces_soaks_once_not_once_per_piece()
        {
            var one = HeadOn(ShotColliders.GearPads);
            var two = HeadOn(ShotColliders.GearPads, overlapped: true);

            Assert.AreEqual(1, two.touches, "one touch told");
            Assert.Greater(one.speed, 0.5f, "the single piece keeps some speed to compare with");
            // soaked twice it would keep 0.7 of what one piece leaves
            Assert.AreEqual(1f, two.speed / one.speed, 0.1f, $"two pieces {two.speed:F2} m/s, one piece {one.speed:F2} m/s");
        }

        // ---- A dead puck ----

        [Test]
        public void A_PuckReach_whose_puck_is_gone_is_skipped_not_thrown_on()
        {
            var body = new GameObject("Dead puck (test)", typeof(Rigidbody), typeof(SphereCollider)).GetComponent<Rigidbody>();
            var reach = new PuckReach(body);
            reach.LookAhead(Step); // so it is looking, as a live Shot's is
            Object.DestroyImmediate(body.gameObject); // never disposed, as after a Play session with domain reload off
            try
            {
                Assert.DoesNotThrow(PuckReach.GearMoved);
                Assert.DoesNotThrow(PuckReach.GearMoved, "and it is dropped, not tried again");
            }
            finally { reach.Dispose(); } // already dropped: harmless
        }

        // ---- The Save and the Goal ----

        /// <summary>A Round in the shooting window on a middle attack; reach decides the race (it moves the goalie only).</summary>
        static Round InWindow()
        {
            var round = new Round(ScriptedRandom.Forcing(AttackSide.Middle), new RoundSettings(), FakeReach.NeverReaches(), 0);
            round.StartRound();
            round.Tick(3.7 + 1.0);
            return round;
        }

        PhysicsShotEffects Effects()
        {
            _effects = new PhysicsShotEffects(_root.transform, _camera, new SeededRandom(7), null, name => null);
            Physics.SyncTransforms();
            return _effects;
        }

        static void Frame(Round round, PhysicsShotEffects effects)
        {
            Physics.Simulate(Step);
            effects.AfterPhysicsStep(Step);
            round.Tick(Step);
            effects.Update(round, Step);
        }

        [Test]
        public void A_Shot_that_touches_the_gear_and_stays_out_is_a_Save()
        {
            var effects = Effects();
            Wall(ShotColliders.GearPads);
            var round = InWindow();
            round.SubmitShot(50, 50, 100, false);
            effects.Update(round, 0f);
            for (int i = 0; i < 4.0 / Step && round.AwaitingWhistle; i++) Frame(round, effects);

            Assert.IsFalse(round.AwaitingWhistle, "the Whistle went");
            Assert.IsTrue(effects.Shot.GearTouched);
            Assert.AreEqual(ShotResultKind.Save, round.LastResult.Value.Kind);
        }

        [Test]
        public void A_post_hit_and_then_a_gear_touch_is_a_Save()
        {
            var effects = Effects();
            var round = InWindow();
            float postCentre = -(NetBuilder.Post / 2f) / (float)NetFaceMapping.WidthMetres * 100f; // Net Face units
            round.SubmitShot(postCentre, 50, 100, false);
            effects.Update(round, 0f);
            bool placed = false;
            effects.Shot.Hit += (other, impulse) =>
            {
                if (placed || other.sharedMaterial != ShotColliders.Frame) return;
                placed = true; // gear appears in the puck's way after the post: a box 25 cm along where it bounced to
                Vector3 away = effects.Shot.Puck.linearVelocity.sqrMagnitude > 1e-4f ? effects.Shot.Puck.linearVelocity.normalized : Vector3.back;
                Box(ShotColliders.GearPads, new Vector3(2f, 2f, 0.2f), effects.Shot.Puck.position + away * 0.25f, Quaternion.LookRotation(away));
            };
            for (int i = 0; i < 4.0 / Step && round.AwaitingWhistle; i++) Frame(round, effects);

            Assert.IsTrue(placed, "the Shot hit the post");
            Assert.IsFalse(round.AwaitingWhistle, "the Whistle went");
            Assert.IsTrue(effects.Shot.GearTouched, "then the gear");
            Assert.AreEqual(ShotResultKind.Save, round.LastResult.Value.Kind);
        }

        [Test]
        public void A_gear_touch_and_then_in_the_net_is_a_Goal()
        {
            var effects = Effects();
            var round = InWindow();
            round.SubmitShot(50, 50, 100, false);
            effects.Update(round, 0f);
            // A gear wall 1.6 m long along the puck's line, turned 4 degrees into it so the line meets its face 1.3 m before the Net Face: the puck glances off it
            // (keeping its way and 70 % of its speed) and goes on in.
            Vector3 start = effects.Shot.Start, target = effects.Shot.Target;
            Vector3 along = new Vector3(target.x - start.x, 0f, target.z - start.z).normalized;
            float x = Mathf.Lerp(start.x, target.x, (-1.3f - start.z) / (target.z - start.z));
            Vector3 face = Quaternion.AngleAxis(-4f, Vector3.up) * along;
            Vector3 sideways = Vector3.Cross(Vector3.up, face);
            Box(ShotColliders.GearPads, new Vector3(0.2f, 4f, 1.6f), new Vector3(x, 1.5f, -1.3f) + face * 0.2f + sideways * 0.1f, Quaternion.LookRotation(face));
            for (int i = 0; i < 4.0 / Step && round.AwaitingWhistle; i++) Frame(round, effects);

            Assert.IsTrue(effects.Shot.GearTouched, "it touched the gear");
            Assert.AreEqual(ShotResultKind.Goal, round.LastResult.Value.Kind, "puck at " + effects.Shot.Puck.position + ", " + effects.Shot.Puck.linearVelocity.magnitude + " m/s, whistle " + effects.Shot.WhistleSeconds + ", wall at x " + x + ", path " + along);
        }

        // ---- The save sound ----

        public enum Gear { Pads, Body, Glove, Stick, Blocker, Skate }

        [TestCase(Gear.Pads, SoundCue.SaveImpact)]
        [TestCase(Gear.Body, SoundCue.SaveImpact)]
        [TestCase(Gear.Glove, SoundCue.GloveCatch)]
        [TestCase(Gear.Stick, SoundCue.BlockerHit)]
        [TestCase(Gear.Blocker, SoundCue.BlockerHit)]
        [TestCase(Gear.Skate, SoundCue.BlockerHit)]
        public void The_save_sound_plays_at_each_gear_touch_and_is_the_gears(Gear gear, SoundCue expected)
        {
            var effects = Effects();
            PhysicsMaterial feel = gear == Gear.Glove ? ShotColliders.GearGlove : gear == Gear.Stick ? ShotColliders.GearStick
                : gear == Gear.Body ? ShotColliders.GearBody : ShotColliders.GearPads;
            string piece = gear == Gear.Blocker ? GearPiece.Blocker : gear == Gear.Skate ? GearPiece.LeftSkate : gear == Gear.Glove ? GearPiece.Catcher : GearPiece.LeftPad;
            Wall(feel, GoalieHitShapes.BodyName(piece), onBody: true);
            var round = InWindow();
            round.SubmitShot(50, 50, 100, false);
            effects.Update(round, 0f);
            int touches = 0;
            effects.Shot.GearTouch += _ => touches++;
            var heard = new List<SoundCue>();
            for (int i = 0; i < 3.0 / Step; i++)
            {
                Frame(round, effects);
                if (effects.HitSound.HasValue) heard.Add(effects.HitSound.Value);
            }

            Assert.GreaterOrEqual(touches, 1);
            Assert.AreEqual(touches, heard.FindAll(c => c == expected).Count, "one sound per touch: heard " + string.Join(", ", heard));
            Assert.AreEqual(touches, heard.Count, "and nothing else: heard " + string.Join(", ", heard));
        }
    }
}
