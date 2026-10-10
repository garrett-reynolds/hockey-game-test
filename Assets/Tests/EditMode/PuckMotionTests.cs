using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>The puck after it hits something: PuckMotion (pure code, no GameObjects).</summary>
    public class PuckMotionTests
    {
        static readonly Vector3 Incoming = new Vector3(0f, 0f, 20f);      // 72 km/h straight at the net
        static readonly Vector3 Contact = new Vector3(0f, 0.6f, -0.8f);

        static PuckMotion HitAt(Vector3 normal, PuckSurface surface, Vector3? incoming = null)
        {
            Vector3 v = incoming ?? Incoming;
            var motion = new PuckMotion(Contact, Quaternion.LookRotation(Vector3.up, -v.normalized), v);
            motion.Hit(normal.normalized, surface);
            return motion;
        }

        [Test]
        public void A_puck_leaves_a_surface_turned_to_one_side_toward_that_side_and_back_toward_the_shooter()
        {
            Vector3 right = HitAt(new Vector3(0.4f, 0f, -1f), PuckSurface.Pad).Velocity;
            Vector3 left = HitAt(new Vector3(-0.4f, 0f, -1f), PuckSurface.Pad).Velocity;
            Vector3 square = HitAt(new Vector3(0f, 0f, -1f), PuckSurface.Pad).Velocity;

            Assert.Greater(right.x, 1f, "a face turned to the right sends it right");
            Assert.Less(left.x, -1f, "a face turned to the left sends it left");
            Assert.AreEqual(0f, square.x, 0.01f, "a square face sends it straight back");
            foreach (Vector3 v in new[] { right, left, square })
                Assert.Less(v.z, 0f, "back toward the shooter");
            Assert.AreEqual(right.x, -left.x, 0.001f, "mirror images");
        }

        [Test]
        public void A_glancing_hit_still_leaves_the_surface_back_toward_the_shooter()
        {
            Vector3 n = new Vector3(0.9f, 0f, -0.3f).normalized;
            Vector3 v = HitAt(n, PuckSurface.Pad).Velocity;
            Assert.Less(v.z, -1f, "never on into the goalie or the net");
            Assert.Greater(Vector3.Dot(v, n), 1f, "away from the surface");
        }

        [TestCase(PuckSurface.Pad)]
        [TestCase(PuckSurface.Blocker)]
        [TestCase(PuckSurface.Stick)]
        [TestCase(PuckSurface.Skate)]
        [TestCase(PuckSurface.Body)]
        [TestCase(PuckSurface.Frame)]
        public void The_rebound_off_gear_or_the_frame_is_as_much_faster_as_the_Shot(PuckSurface surface)
        {
            Vector3 n = new Vector3(0.3f, 0f, -1f).normalized; // level: MaxUp caps only the upward part, the same at any speed
            float Off(float kmh) => HitAt(n, surface, new Vector3(0f, 0f, kmh / 3.6f)).Velocity.magnitude;
            Assert.AreEqual(2.5f, Off(200f) / Off(80f), 0.01f, "200 km/h comes off 2.5 times faster than 80 km/h");
            Assert.AreEqual(0.5f, Off(40f) / Off(80f), 0.01f, "40 km/h comes off half as fast");
        }

        [Test]
        public void Each_surface_feels_different()
        {
            Vector3 n = new Vector3(0.2f, 0.1f, -1f).normalized;
            Vector3 pad = HitAt(n, PuckSurface.Pad).Velocity;
            Vector3 frame = HitAt(n, PuckSurface.Frame).Velocity;
            Vector3 stick = HitAt(n, PuckSurface.Stick).Velocity;
            Vector3 blocker = HitAt(n, PuckSurface.Blocker).Velocity;

            Assert.Greater(frame.magnitude, pad.magnitude, "the frame pings it away faster than a pad");
            Assert.Greater(pad.magnitude, stick.magnitude, "a pad rebounds harder than the stick");
            Assert.Greater(pad.magnitude, blocker.magnitude, "a pad rebounds harder than the blocker");
            Assert.Less(blocker.x, -0.5f * blocker.magnitude, "the blocker kicks it hard to the side (the shooter's left, the blocker's outside)");
            Assert.LessOrEqual(stick.y, 0.3f, "the stick keeps it low");
        }

        static float LowestPoint(PuckMotion m)
        {
            Vector3 axis = m.Rotation * Vector3.up;
            float sin = Mathf.Sqrt(Mathf.Max(0f, 1f - axis.y * axis.y));
            return m.Position.y - PuckMotion.Diameter / 2f * sin - PuckMotion.Thickness / 2f * Mathf.Abs(axis.y);
        }

        [Test]
        public void An_off_centre_hit_tumbles_the_puck_end_over_end_and_a_square_hit_does_not()
        {
            PuckMotion square = HitAt(new Vector3(0f, 0f, -1f), PuckSurface.Pad);
            PuckMotion edge = HitAt(new Vector3(0.5f, 0.2f, -1f), PuckSurface.Pad);
            Vector3 squareAxis = square.Rotation * Vector3.up, edgeAxis = edge.Rotation * Vector3.up;

            Assert.Less(Vector3.ProjectOnPlane(square.AngularVelocity, squareAxis).magnitude, 0.5f, "a square hit: no tumble");
            Assert.Greater(Vector3.ProjectOnPlane(edge.AngularVelocity, edgeAxis).magnitude, 10f, "an off-centre hit tumbles it");

            float turned = 0f;
            for (int i = 0; i < 12; i++)
            {
                edge.Advance(1f / 60f);
                turned = Mathf.Max(turned, Vector3.Angle(edgeAxis, edge.Rotation * Vector3.up));
            }
            Assert.Greater(turned, 90f, "flips over within 0.2 s");
        }

        [TestCase(0f, 0f, -1f)]
        [TestCase(0.5f, 0.2f, -1f)]
        [TestCase(-0.7f, 0.6f, -1f)]
        public void After_a_hit_the_puck_falls_bounces_settles_flat_on_the_ice_and_slides_to_a_stop(float nx, float ny, float nz)
        {
            PuckMotion motion = HitAt(new Vector3(nx, ny, nz), PuckSurface.Pad);
            float lowest = float.PositiveInfinity;
            bool bounced = false;
            float lastVy = motion.Velocity.y;
            for (float t = 0f; t < 2.5f; t += 1f / 60f)
            {
                motion.Advance(1f / 60f);
                lowest = Mathf.Min(lowest, LowestPoint(motion));
                if (lastVy < -0.5f && motion.Velocity.y > 0.2f) bounced = true;
                lastVy = motion.Velocity.y;
            }
            Assert.IsTrue(bounced, "it bounces off the ice");
            Assert.GreaterOrEqual(lowest, -0.001f, "never into the ice");
            Assert.IsTrue(motion.Settled, "at rest within the 2.5 s result hold");
            Assert.AreEqual(Vector3.zero, motion.Velocity);
            Assert.Greater(Mathf.Abs(Vector3.Dot(motion.Rotation * Vector3.up, Vector3.up)), 0.9995f, "flat, on either face");
            Assert.AreEqual(PuckMotion.Thickness / 2f, motion.Position.y, 0.001f, "lying on the ice");
        }

        [Test]
        public void A_landing_puck_wobbles_before_it_lies_flat()
        {
            PuckMotion motion = HitAt(new Vector3(0.5f, 0.2f, -1f), PuckSurface.Pad);
            int wobbles = 0;
            float lastTilt = 0f, lastChange = 0f;
            for (float t = 0f; t < 2.5f; t += 1f / 120f)
            {
                motion.Advance(1f / 120f);
                if (!motion.OnIce) continue;
                Vector3 axis = motion.Rotation * Vector3.up;
                float tiltX = axis.x; // a wobbling puck's tilt rocks from side to side as it precesses
                float change = tiltX - lastTilt;
                if (change * lastChange < 0f && Mathf.Abs(tiltX) > 0.005f) wobbles++;
                lastChange = change;
                lastTilt = tiltX;
            }
            Assert.GreaterOrEqual(wobbles, 3, "rocks a few times as it settles");
        }

        [Test]
        public void The_same_hit_moves_the_same_whatever_the_frame_rate()
        {
            Vector3 n = new Vector3(0.5f, 0.2f, -1f);
            PuckMotion a = HitAt(n, PuckSurface.Blocker), b = HitAt(n, PuckSurface.Blocker), c = HitAt(n, PuckSurface.Blocker);
            for (int i = 0; i < 90; i++) a.Advance(1f / 60f);
            for (int i = 0; i < 45; i++) b.Advance(1f / 30f);
            float t = 0f;
            for (int i = 0; t < 1.5f - 1e-4f; i++)
            {
                float dt = Mathf.Min(i % 3 == 0 ? 0.031f : 0.007f, 1.5f - t);
                c.Advance(dt);
                t += dt;
            }
            Assert.AreEqual(0f, Vector3.Distance(a.Position, b.Position), 1e-4f);
            Assert.AreEqual(0f, Vector3.Distance(a.Position, c.Position), 1e-4f);
            Assert.AreEqual(0f, Quaternion.Angle(a.Rotation, c.Rotation), 0.05f);
        }

        [Test]
        public void The_catching_glove_holds_the_puck()
        {
            PuckMotion motion = HitAt(new Vector3(0.3f, 0.2f, -1f), PuckSurface.Catcher);
            Assert.IsTrue(motion.Held);
            Quaternion caught = motion.Rotation;
            motion.Advance(3f);
            Assert.AreEqual(Vector3.zero, motion.Velocity);
            Assert.AreEqual(0f, Vector3.Distance(Contact, motion.Position), 1e-6f, "stays in the glove");
            Assert.AreEqual(0f, Quaternion.Angle(caught, motion.Rotation), 1e-3f);
        }
    }
}
