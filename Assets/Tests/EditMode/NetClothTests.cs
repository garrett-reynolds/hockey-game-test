using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The cloth netting (#112). Built-in Cloth does not step under a test's hand-stepped Physics.Simulate in batch EditMode
    /// (its vertices stay put), so these cover what the cloth hangs on: its settings, the capsule it is given, and the moment
    /// the puck meets the netting (the real catch contact), which times the Annoyed Gesture and sounds the netting. How it
    /// gives and settles is a by-eye check.
    /// </summary>
    public class NetClothTests
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
            _camera.aspect = 16f / 9f;
            var settings = new RoundSettings();
            Breakaway.SkatePoint(AttackSide.Middle, 1.0, out double x, out double distance);
            var ground = new Vector3((float)x, 0f, (float)-distance);
            Vector3 toNet = -ground;
            ground += toNet.normalized * (float)Breakaway.CappedDrift(Breakaway.Drift(AttackSide.Middle, settings.SlowDownSeconds, 1.0, settings), toNet.magnitude);
            _camera.transform.position = new Vector3(ground.x, (float)Breakaway.EyeHeight, ground.z);
            _camera.transform.LookAt(new Vector3(0f, (float)(NetFrame.FaceHeightMetres / 2.0), 0f));
            _effects = new PhysicsShotEffects(_root.transform, _camera, new SeededRandom(7), null, name => null);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            _effects?.Dispose();
            _effects = null;
            Object.DestroyImmediate(_root);
            Physics.simulationMode = _oldMode;
        }

        Round Shoot(double x, double y, double kmh)
        {
            var round = new Round(ScriptedRandom.Forcing(AttackSide.Middle), new RoundSettings(), FakeReach.NeverReaches(), 0);
            round.StartRound();
            round.Tick(3.7 + 1.0);
            round.SubmitShot(x, y, kmh, false);
            _effects.Update(round, 0f);
            return round;
        }

        void Frame(Round round)
        {
            Physics.Simulate(Step);
            _effects.AfterPhysicsStep(Step);
            round.Tick(Step);
            _effects.Update(round, Step);
        }

        void FrameSteps(Round round, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                Physics.Simulate(Step);
                _effects.AfterPhysicsStep(Step);
            }
            round.Tick(Step * steps);
            _effects.Update(round, Step * steps);
        }

        [Test]
        public void The_capsule_stays_where_it_was_once_the_puck_is_hidden()
        {
            Round round = Shoot(130, 50, 100);
            for (int i = 0; i < 5; i++) Frame(round);
            _effects.Shot.Puck.gameObject.SetActive(false); // a hidden body may read position zero
            Frame(round);
            var held = _effects.NetCloth.Capsule.Value;
            for (int i = 0; i < 3; i++) Frame(round);
            var capsule = _effects.NetCloth.Capsule.Value;
            Assert.AreEqual(held.Now, capsule.Now, "no following a hidden puck");
            Assert.AreEqual(held.Was, capsule.Was);
            Assert.Greater(Vector3.Distance(capsule.Now, _root.transform.position), 0.5f, "not stretched to the origin");
        }

        [Test]
        public void The_cloth_netting_has_the_Hard_settings()
        {
            Cloth cloth = _effects.NetCloth.Cloth;
            Assert.IsNotNull(cloth);
            Assert.IsNotNull(cloth.GetComponent<SkinnedMeshRenderer>().sharedMesh);
            Assert.AreEqual(1f, cloth.stretchingStiffness);
            Assert.AreEqual(1f, cloth.bendingStiffness);
            Assert.AreEqual(0.8f, cloth.damping, 1e-6f);
            Assert.AreEqual(240f, cloth.clothSolverFrequency);
            Assert.AreEqual(0f, cloth.worldVelocityScale);
            Assert.AreEqual(0f, cloth.worldAccelerationScale);
            int pinned = 0, roof = 0, sides = 0;
            foreach (ClothSkinningCoefficient c in cloth.coefficients)
            {
                if (c.maxDistance == 0f) pinned++;
                else if (Mathf.Approximately(c.maxDistance, 0.04f)) roof++;
                else if (Mathf.Approximately(c.maxDistance, 0.08f)) sides++;
                else Assert.Fail("a coefficient moves " + c.maxDistance + " m");
            }
            Assert.Greater(pinned, 0, "pinned at the ice, posts and crossbar");
            Assert.Greater(roof, 0, "the roof moves 4 cm");
            Assert.Greater(sides, 0, "the sides and back move 8 cm");
            Assert.AreEqual(cloth.coefficients.Length, pinned + roof + sides);
        }

        [Test]
        public void The_cloth_sees_the_puck_as_a_capsule_from_where_it_was_last_frame_to_where_it_is()
        {
            Round round = Shoot(50, 50, 100);
            for (int i = 0; i < 5; i++) Frame(round);
            Vector3 lastFrame = _effects.Shot.Puck.position;
            for (int i = 0; i < 3; i++) FrameSteps(round, 1 + i % 2); // frames of one and of two physics steps
            FrameSteps(round, 3);
            var capsule = _effects.NetCloth.Capsule.Value;
            Assert.AreNotEqual(lastFrame, capsule.Was, "moved on from the older frames");
            Vector3 beforeThis = capsule.Now;
            FrameSteps(round, 3);
            capsule = _effects.NetCloth.Capsule.Value;
            Assert.AreEqual(beforeThis, capsule.Was, "last frame, not last step");
            Assert.AreEqual(_effects.Shot.Puck.position, capsule.Now);
            Assert.AreEqual(1, _effects.NetCloth.Cloth.sphereColliders.Length);
            foreach (Collider sphere in new Collider[] { _effects.NetCloth.Cloth.sphereColliders[0].first, _effects.NetCloth.Cloth.sphereColliders[0].second })
                foreach (Collider part in _effects.Shot.Puck.GetComponentsInChildren<Collider>())
                    Assert.IsTrue(Physics.GetIgnoreCollision(sphere, part), "the puck never hits the cloth's spheres");
        }

        [Test]
        public void The_netting_is_met_at_the_catch_contact_once_and_sounds_then()
        {
            Round round = Shoot(50, 50, 100);
            int sounded = 0, firstTouch = -1;
            for (int i = 0; i < 3.0 / Step; i++)
            {
                float before = _effects.SecondsSinceNetting;
                Frame(round);
                if (_effects.HitSound == SoundCue.NetRustle)
                {
                    sounded++;
                    firstTouch = i;
                    Assert.Less(before, 0f, "not met before");
                    Assert.AreEqual(Step, _effects.SecondsSinceNetting, 1e-4f, "it counts from this frame");
                    Assert.AreSame(ShotColliders.Catch, _effects.Shot.FirstTouched.sharedMaterial, "the catch contact");
                }
                else if (firstTouch < 0) Assert.Less(_effects.SecondsSinceNetting, 0f, "not met yet, step " + i);
                else Assert.AreEqual((i - firstTouch + 1) * Step, _effects.SecondsSinceNetting, 1e-3f, "counts on from the touch");
            }
            Assert.AreEqual(1, sounded, "one netting sound a Shot");
            Assert.IsFalse(round.AwaitingWhistle);
            Assert.AreEqual(ShotResultKind.Goal, round.LastResult.Value.Kind);
        }

        [Test]
        public void A_Shot_that_never_meets_the_netting_has_no_touch_and_no_netting_sound()
        {
            Round round = Shoot(130, 50, 100); // wide
            for (int i = 0; i < 3.0 / Step; i++)
            {
                Frame(round);
                Assert.AreNotEqual(SoundCue.NetRustle, _effects.HitSound);
                Assert.Less(_effects.SecondsSinceNetting, 0f, "the gesture falls back to AfterShot.LatestNetting");
            }
        }

        [Test]
        public void Disposing_removes_the_cloth_and_brings_the_model_netting_back()
        {
            var netting = new GameObject(NetBuilder.NettingName, typeof(MeshRenderer));
            netting.transform.SetParent(_root.transform, false);
            var net = new NetCloth(_root.transform);
            Assert.IsFalse(netting.GetComponent<MeshRenderer>().enabled, "hidden while the cloth shows");
            net.Dispose();
            Assert.IsTrue(netting.GetComponent<MeshRenderer>().enabled);
            int cloths = 0;
            foreach (Transform t in _root.transform) if (t.name == NetCloth.ObjectName) cloths++;
            Assert.AreEqual(1, cloths, "only the Shot effects' own cloth is left");
        }

        [Test]
        public void Disposing_frees_the_material_texture_and_mesh_the_cloth_made()
        {
            int textures = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int materials = Resources.FindObjectsOfTypeAll<Material>().Length;
            int meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            new NetCloth(_root.transform).Dispose();
            Assert.AreEqual(textures, Resources.FindObjectsOfTypeAll<Texture2D>().Length, "Net holes is freed");
            Assert.AreEqual(materials, Resources.FindObjectsOfTypeAll<Material>().Length, "Cloth Netting is freed");
            Assert.AreEqual(meshes, Resources.FindObjectsOfTypeAll<Mesh>().Length, "the netting mesh is freed");
        }
    }
}
