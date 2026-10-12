using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class CrowdViewTests
    {
        GameObject _rink;

        [SetUp]
        public void MakeRink() => _rink = new GameObject("Rink");

        [TearDown]
        public void RemoveRink() => Object.DestroyImmediate(_rink);

        static ArenaState In(RoundPhase phase) => ArenaShow.At(new ArenaShowInput { Phase = phase, ShotNumber = 1 });

        [Test]
        public void The_first_body_is_cut_to_about_one_and_a_half_thousand_triangles_with_a_sit_idle_clip()
        {
            CrowdBody body = CrowdBody.Load(Crowd.BodyFile);
            try
            {
                Assert.IsNotNull(body, "Assets/Resources/Crowd/" + Crowd.BodyFile + ".bytes (Tools/make_crowd.py)");
                int triangles = body.Mesh.triangles.Length / 3;
                Assert.That(triangles, Is.InRange(1200, 1700));
                CrowdBody.Clip clip = body.ClipNamed(Crowd.SitIdle);
                Assert.IsNotNull(clip);
                Assert.AreEqual(body.Mesh.vertexCount, clip.Texture.width, "one VAT column per vertex");
                Assert.AreEqual(clip.Frames, clip.Texture.height, "one VAT row per frame");
                Assert.That(clip.Seconds, Is.InRange(2f, 8f));
            }
            finally { body?.Destroy(); }
        }

        [Test]
        public void The_seated_body_is_a_seated_adult_on_the_floor_facing_plus_z()
        {
            CrowdBody body = CrowdBody.Load(Crowd.BodyFile);
            try
            {
                Bounds b = body.Mesh.bounds;
                Assert.AreEqual(0f, b.min.y, 0.02f, "the feet on the floor");
                Assert.That(b.size.y, Is.InRange(1.2f, 1.5f), "a seated adult's height");
                Assert.That(b.max.z, Is.GreaterThan(0.4f), "the knees and feet reach forward (+z) of the hips");
                Assert.That(b.min.z, Is.GreaterThan(-0.4f), "nothing far behind the hips");
            }
            finally { body?.Destroy(); }
        }

        [Test]
        public void Real_fans_fill_the_lower_stand_behind_the_near_net()
        {
            Crowd crowd = Crowd.Build(_rink.transform);
            Assert.IsNotNull(crowd);
            Assert.AreEqual(_rink.transform, crowd.transform.parent);
            Assert.AreEqual(CrowdSeats.BehindNearNet().Length, crowd.FanCount);
            Assert.That(crowd.Bounds.min.z, Is.GreaterThan(RinkLayout.NearEndBoardsZ));
        }

        [Test]
        public void Calm_Area_fans_are_still_and_dim_from_goalie_set_to_the_Shot()
        {
            Crowd crowd = Crowd.Build(_rink.transform);
            crowd.Show(In(RoundPhase.ShootingWindow), 10.0);
            double held = crowd.ClockIn(ArenaZone.CalmArea);
            crowd.Show(In(RoundPhase.ShootingWindow), 10.2);
            crowd.Show(In(RoundPhase.ShootingWindow), 10.4);

            Assert.AreEqual(held, crowd.ClockIn(ArenaZone.CalmArea), 1e-12, "the pose holds: no motion");
            Assert.AreEqual((float)CrowdLooks.StillLight, crowd.LightIn(ArenaZone.CalmArea), 1e-6f);
        }

        [Test]
        public void Outside_the_shooting_window_the_Calm_Area_fans_sit_and_make_small_moves_at_full_light()
        {
            Crowd crowd = Crowd.Build(_rink.transform);
            crowd.Show(In(RoundPhase.SkateIn), 10.0);
            double before = crowd.ClockIn(ArenaZone.CalmArea);
            crowd.Show(In(RoundPhase.SkateIn), 10.1);

            Assert.AreEqual(0.1, crowd.ClockIn(ArenaZone.CalmArea) - before, 1e-9);
            Assert.AreEqual(1f, crowd.LightIn(ArenaZone.CalmArea));
        }

        [Test]
        public void A_long_pause_does_not_jump_the_fans()
        {
            Crowd crowd = Crowd.Build(_rink.transform);
            crowd.Show(In(RoundPhase.SkateIn), 10.0);
            double before = crowd.ClockIn(ArenaZone.CalmArea);
            crowd.Show(In(RoundPhase.SkateIn), 70.0);

            double step = crowd.ClockIn(ArenaZone.CalmArea) - before;
            Assert.That(step, Is.InRange(0.0, 0.25 + 1e-9));
        }

        [Test]
        public void The_arena_model_has_no_flat_crowd_picture_in_the_lower_stand_behind_the_near_net()
        {
            GameObject arena = ArenaStands.Build(_rink.transform);
            MeshFilter picture = null;
            foreach (MeshFilter f in arena.GetComponentsInChildren<MeshFilter>())
                if (f.name == "Crowd") picture = f;
            Assert.IsNotNull(picture, "the flat crowd picture still fills the rest of the stands");

            Assert.That(picture.sharedMesh.vertexCount, Is.GreaterThan(0));
            float lowerTop = (float)RinkLayout.RibbonBottom;
            foreach (Vector3 v in picture.sharedMesh.vertices)
            {
                Vector3 w = picture.transform.TransformPoint(v);
                bool behindNearNet = w.z > RinkLayout.NearEndBoardsZ + 0.5f && Mathf.Abs(w.x) < RinkLayout.CornerX - 0.5f;
                Assert.IsFalse(behindNearNet && w.y < lowerTop - 0.05f, "crowd picture at " + w);
            }
        }
    }
}
