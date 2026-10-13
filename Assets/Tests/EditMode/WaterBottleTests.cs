using System.Linq;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>The water bottle on top of the net (#163): where it sits, its size and look, that it stays still and that it never meets the puck.</summary>
    public class WaterBottleTests
    {
        // Independent of the code: the net model's top (frame and pad) runs flat from the crossbar to about 0.37 m behind the Net Face
        // (read off a side picture, t163-profile.png), then the netting slopes down to the back at 0.6 m. The crossbar's top, and the
        // top of the catch roof the bottle stands on, are 1.30 m up.
        const float FlatTopBack = 0.37f, FrameTop = 1.30f;
        const float Step = PhysicsShot.StepSeconds;

        SimulationMode _oldMode;
        GameObject _rink;

        [SetUp]
        public void SetUp()
        {
            _oldMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            // Far from the origin: other tests leave a whole Rink in the open scene (with its own bottle on the net, found in a full run
            // pushing this test's free bottle off), and a Rink built at the origin and left behind would sit on this one.
            _rink = new GameObject("Rink (test)");
            _rink.transform.position = Origin;
        }

        /// <summary>Where this test's rink stands (its coordinates below are measured from here).</summary>
        static readonly Vector3 Origin = new Vector3(1000f, 0f, 0f);

        [TearDown]
        public void TearDown()
        {
            RinkTeardown.Destroy(_rink);
            Physics.simulationMode = _oldMode;
        }

        [Test]
        public void The_bottle_lies_on_top_of_the_net_at_the_back_of_its_flat_top_toward_the_middle()
        {
            WaterBottle bottle = WaterBottle.Build(_rink.transform);

            Bounds b = Look(bottle);
            Assert.AreEqual(FrameTop, b.min.y, 0.01f, "its base is on the top of the net");
            Assert.That(b.min.z, Is.GreaterThan(FlatTopBack / 2f), "in the back half of the net's flat top");
            Assert.That(b.max.z, Is.LessThan(FlatTopBack), "not past where the netting slopes away (it would hang in the air)");
            Assert.That(Mathf.Abs(b.center.x - Origin.x), Is.LessThan(0.5f), "toward the middle of the net's width");
        }

        [Test]
        public void The_bottle_is_real_size_about_25_cm_long_and_7_to_8_cm_wide()
        {
            Bounds b = Look(WaterBottle.Build(_rink.transform));

            Assert.AreEqual(WaterBottle.Height, b.size.x, 0.01f, "length (it lies across the net)");
            Assert.That(b.size.y, Is.InRange(0.07f, 0.08f), "thickness");
            Assert.That(b.size.z, Is.InRange(0.07f, 0.08f), "depth");
        }

        [Test]
        public void The_bottle_has_a_picture_body_an_orange_cap_and_a_nozzle()
        {
            WaterBottle bottle = WaterBottle.Build(_rink.transform);

            Renderer[] parts = bottle.Root.GetComponentsInChildren<Renderer>();
            Renderer body = parts.FirstOrDefault(r => r.name == "Body"), cap = parts.FirstOrDefault(r => r.name == "Cap"), nozzle = parts.FirstOrDefault(r => r.name == "Nozzle");
            Assert.IsNotNull(body, "body part");
            Assert.IsNotNull(cap, "cap part");
            Assert.IsNotNull(nozzle, "nozzle part");
            Color orange = cap.sharedMaterial.color;
            Assert.That(orange.r, Is.GreaterThan(0.8f), "orange cap: red");
            Assert.That(orange.g, Is.InRange(0.3f, 0.8f), "orange cap: green (the importer brightens the colour)");
            Assert.That(orange.b, Is.LessThan(0.3f), "orange cap: blue");
            Assert.AreEqual(orange, nozzle.sharedMaterial.color, "the nozzle is the cap's orange");
            Assert.IsNotNull(body.sharedMaterial.mainTexture, "the body wears the swoosh and G picture");
            Assert.That(body.GetComponent<MeshFilter>().sharedMesh.uv.Length, Is.GreaterThan(0), "the body's mesh has UVs for it");
            // Lying with the cap toward the middle (-x): the nozzle is beyond the cap, the cap beyond the body.
            Assert.That(nozzle.bounds.center.x, Is.LessThan(cap.bounds.center.x), "the nozzle is past the cap");
            Assert.That(cap.bounds.center.x, Is.LessThan(body.bounds.center.x), "the cap is at the end of the body");
        }

        [Test]
        public void The_body_picture_is_mostly_green_with_a_grey_swoosh_a_white_G_and_an_orange_bolt()
        {
            string file = System.IO.Path.Combine(Application.dataPath, "Resources", "Models", WaterBottle.BodyPicture + ".png");
            var picture = new Texture2D(2, 2);
            try
            {
                Assert.That(picture.LoadImage(System.IO.File.ReadAllBytes(file)), "the picture file loads");
                int green = 0, grey = 0, white = 0, orange = 0;
                foreach (Color c in picture.GetPixels())
                {
                    if (c.g > c.r + 0.3f && c.g > c.b + 0.3f) green++;
                    else if (Mathf.Abs(c.r - c.g) < 0.06f && Mathf.Abs(c.g - c.b) < 0.08f && c.r > 0.55f && c.r < 0.85f) grey++;
                    else if (c.r > 0.95f && c.g > 0.95f && c.b > 0.95f) white++;
                    else if (c.r > 0.85f && c.g > 0.35f && c.g < 0.65f && c.b < 0.3f) orange++;
                }
                float all = picture.width * picture.height;
                Assert.That(green / all, Is.GreaterThan(0.6f), "green body");
                Assert.That(grey / all, Is.GreaterThan(0.05f), "wide swoosh band");
                Assert.That(white / all, Is.GreaterThan(0.02f), "white G");
                Assert.That(orange / all, Is.GreaterThan(0.005f), "orange bolt");
            }
            finally { Object.DestroyImmediate(picture); }
        }

        [Test]
        public void The_bottle_has_a_simple_collider_and_a_bottle_mass()
        {
            WaterBottle bottle = WaterBottle.Build(_rink.transform);

            Assert.IsNotNull(bottle.Body, "rigidbody");
            Assert.That(bottle.Body.mass, Is.InRange(0.6f, 1f), "a full bottle");
            Collider[] colliders = bottle.Root.GetComponentsInChildren<Collider>();
            Assert.AreEqual(1, colliders.Length, "one simple collider");
            Assert.That(colliders[0], Is.TypeOf<BoxCollider>().Or.TypeOf<CapsuleCollider>());
        }

        [Test]
        public void The_bottle_is_still_while_it_sits_on_the_net()
        {
            ShotColliders.Build(_rink.transform);
            WaterBottle bottle = WaterBottle.Build(_rink.transform);
            Vector3 start = bottle.Root.transform.position;

            Run(3f);

            Assert.That(Vector3.Distance(start, bottle.Root.transform.position), Is.LessThan(0.001f), "it did not move");
            Assert.That(bottle.Body.linearVelocity.magnitude, Is.LessThan(0.001f));
        }

        [Test]
        public void With_nothing_holding_it_the_bottle_still_rests_on_the_net_it_does_not_slide_sink_or_fall()
        {
            ShotColliders.Build(_rink.transform);
            WaterBottle bottle = WaterBottle.Build(_rink.transform);
            Vector3 start = bottle.Root.transform.position;
            bottle.Body.isKinematic = false; // the free bottle (the knock-off, #164): only the net's top holds it

            Run(3f);

            Assert.That(Vector3.Distance(start, bottle.Root.transform.position), Is.LessThan(0.005f), "it stayed where it was put");
            Assert.That(Quaternion.Angle(bottle.RestRotation, bottle.Root.transform.rotation), Is.LessThan(1f), "it did not roll or turn");
        }

        [Test]
        public void Putting_the_bottle_back_returns_it_to_its_place_and_stops_it()
        {
            ShotColliders.Build(_rink.transform);
            WaterBottle bottle = WaterBottle.Build(_rink.transform);
            Vector3 start = bottle.Root.transform.position;
            bottle.Body.isKinematic = false;
            bottle.Body.AddForce(new Vector3(3f, 6f, 1f), ForceMode.Impulse);
            Run(1.5f);
            Assert.That(Vector3.Distance(start, bottle.Root.transform.position), Is.GreaterThan(0.1f), "(setup) it was knocked away");

            bottle.PutBack();
            Run(1f);

            Assert.That(Vector3.Distance(start, bottle.Root.transform.position), Is.LessThan(0.001f), "back in place");
            Assert.That(Quaternion.Angle(bottle.RestRotation, bottle.Root.transform.rotation), Is.LessThan(0.1f), "lying as before");
            Assert.That(bottle.Body.isKinematic, "held still again");
        }

        [Test]
        public void A_knocked_bottle_leaves_the_net_lands_on_the_ice_inside_the_rink_and_PutBack_returns_it()
        {
            ShotColliders.Build(_rink.transform);
            WaterBottle bottle = WaterBottle.Build(_rink.transform);
            bottle.Knock();
            Assert.That(bottle.Knocked, "let go");

            Run(5f);

            Vector3 p = bottle.Root.transform.position - Origin;
            Assert.That(p.z, Is.GreaterThan(0.7f), "past the back of the net (0.6 m)");
            Assert.That(p.y, Is.InRange(0.02f, 0.3f), "on the ice: above its surface, not through it");
            Assert.That(Mathf.Abs(p.x), Is.LessThan(RinkIce.Size / 2f), "inside the rink (side to side)");
            Assert.That(Mathf.Abs(p.z - RinkIce.CentreZ), Is.LessThan(RinkIce.Size / 2f), "inside the rink (end to end)");

            bottle.PutBack();
            Run(1f);

            Assert.That(Vector3.Distance(bottle.RestPosition, bottle.Root.transform.position), Is.LessThan(0.001f), "back in place");
            Assert.That(bottle.Body.isKinematic, "still, held again");
            Assert.That(bottle.Body.linearVelocity.magnitude, Is.LessThan(0.001f));
        }

        [Test]
        public void Follow_knocks_the_bottle_when_the_state_turns_true_and_puts_it_back_when_it_turns_false()
        {
            WaterBottle bottle = WaterBottle.Build(_rink.transform);
            Assert.AreSame(bottle, WaterBottle.In(_rink.transform), "the driver finds it");

            bottle.Follow(false);
            Assert.That(bottle.Body.isKinematic, "still on the net");
            bottle.Follow(true);
            Assert.That(bottle.Knocked, "knocked");
            bottle.Follow(false);
            Assert.That(bottle.Body.isKinematic, "put back");
            Assert.That(Vector3.Distance(bottle.RestPosition, bottle.Root.transform.position), Is.LessThan(0.001f));
        }

        [Test]
        public void A_second_Knock_while_knocked_does_nothing()
        {
            ShotColliders.Build(_rink.transform);
            WaterBottle bottle = WaterBottle.Build(_rink.transform);
            bottle.Knock();
            Run(0.5f);
            Vector3 velocity = bottle.Body.linearVelocity, position = bottle.Root.transform.position;

            bottle.Knock();

            Assert.AreEqual(velocity, bottle.Body.linearVelocity, "the second Knock gave no new push");
            Assert.AreEqual(position, bottle.Root.transform.position, "and did not move it");
        }

        [Test]
        public void The_bottle_and_the_puck_are_on_layers_that_never_collide_and_the_bottle_still_meets_the_ice()
        {
            WaterBottle bottle = WaterBottle.Build(_rink.transform);
            Rigidbody puck = PhysicsPuck.Make(_rink.transform);

            Assert.AreNotEqual(WaterBottle.Layer, PhysicsPuck.Layer, "own layers");
            Assert.That(Physics.GetIgnoreLayerCollision(WaterBottle.Layer, PhysicsPuck.Layer), "the layer rule");
            Assert.AreEqual(WaterBottle.Layer, bottle.Root.layer);
            foreach (Transform t in bottle.Root.GetComponentsInChildren<Transform>()) Assert.AreEqual(WaterBottle.Layer, t.gameObject.layer, t.name);
            foreach (Transform t in puck.GetComponentsInChildren<Transform>()) Assert.AreEqual(PhysicsPuck.Layer, t.gameObject.layer, t.name);
            Assert.That(!Physics.GetIgnoreLayerCollision(WaterBottle.Layer, 0), "the bottle still lands on the ice (layer 0)");
            Assert.That(!Physics.GetIgnoreLayerCollision(PhysicsPuck.Layer, 0), "the puck still meets the ice, boards and net (layer 0)");
        }

        [Test]
        public void A_puck_flies_through_the_bottle_without_touching_it()
        {
            ShotColliders.Build(_rink.transform);
            WaterBottle bottle = WaterBottle.Build(_rink.transform);
            bottle.Body.isKinematic = false;
            Vector3 start = bottle.Root.transform.position;
            Rigidbody puck = PhysicsPuck.Make(_rink.transform);
            puck.useGravity = false;
            Vector3 middle = Look(bottle).center;
            Vector3 from = middle + new Vector3(-0.6f, 0f, 0f); // level with the bottle's middle, 0.6 m to its side
            puck.position = from;
            puck.transform.position = from;
            puck.linearVelocity = new Vector3(30f, 0f, 0f);

            Run(0.03f); // 0.9 m: the puck passes right through where the bottle stands and stops short of the net's side

            Assert.That(puck.position.x, Is.GreaterThan(middle.x + 0.1f), "the puck went through");
            Assert.AreEqual(30f, puck.linearVelocity.x, 0.1f, "the bottle took nothing off the puck");
            Assert.That(Vector3.Distance(start, bottle.Root.transform.position), Is.LessThan(0.001f), "the puck did not move the bottle");
        }

        [Test]
        public void The_bottle_covers_no_part_of_the_Net_Face_from_where_the_shooter_stands()
        {
            WaterBottle bottle = WaterBottle.Build(_rink.transform);
            Collider collider = bottle.Root.GetComponentInChildren<Collider>();
            Physics.SyncTransforms();
            float eyeHeight = (float)Breakaway.EyeHeight;
            var eyes = new[]
            {
                new Vector3(0f, eyeHeight, -9f), new Vector3(-3f, eyeHeight, -9f), new Vector3(3f, eyeHeight, -9f),
                new Vector3(0f, eyeHeight, -3f), new Vector3(-2f, eyeHeight, -3f), new Vector3(2f, eyeHeight, -3f),
                new Vector3(0f, eyeHeight, -15f), new Vector3(0f, 2.2f, -9f),
            };
            int covered = 0;
            foreach (Vector3 eye in eyes)
                for (int ix = 0; ix <= 10; ix++)
                    for (int iy = 0; iy <= 10; iy++)
                    {
                        var onFace = new Vector3(-0.915f + 1.83f * ix / 10f, 1.22f * iy / 10f, 0f);
                        Vector3 toFace = onFace - eye;
                        if (collider.Raycast(new Ray(eye + Origin, toFace.normalized), out RaycastHit hit, toFace.magnitude)) covered++;
                    }
            Assert.AreEqual(0, covered, "face points the bottle hides");
        }

        [Test]
        public void The_bottle_is_part_of_the_rink()
        {
            Transform rink = RinkBuilder.Build(out _);
            try
            {
                Transform bottle = rink.Find("Water Bottle");
                Assert.IsNotNull(bottle, "RinkBuilder builds the bottle under the rink");
                Assert.IsNotNull(bottle.GetComponent<Rigidbody>());
            }
            finally { RinkTeardown.Destroy(rink.gameObject); }
        }

        static Bounds Look(WaterBottle bottle)
        {
            Renderer[] renderers = bottle.Root.GetComponentsInChildren<Renderer>();
            Assert.IsNotEmpty(renderers, "the bottle draws something");
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static void Run(float seconds)
        {
            for (float t = 0f; t < seconds; t += Step) Physics.Simulate(Step);
        }
    }
}
