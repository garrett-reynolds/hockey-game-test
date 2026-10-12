using System.Linq;
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

        static ArenaState In(RoundPhase phase, ArenaResult result = ArenaResult.None) =>
            ArenaShow.At(new ArenaShowInput { Phase = phase, Result = result, ShotNumber = 1 });

        [Test]
        public void Near_bodies_are_about_one_and_a_half_thousand_triangles_and_far_bodies_about_four_hundred_with_all_five_clips()
        {
            for (int b = 0; b < 2; b++)
                for (int level = 0; level < 2; level++)
                {
                    string file = Crowd.BodyFiles[b, level];
                    CrowdBody body = CrowdBody.Load(file);
                    try
                    {
                        Assert.IsNotNull(body, "Assets/Resources/Crowd/" + file + ".bytes (Tools/make_crowd.sh)");
                        int triangles = body.Mesh.triangles.Length / 3;
                        Assert.That(triangles, level == 0 ? Is.InRange(1300, 1700) : Is.InRange(300, 500), file);
                        Assert.That(body.Clips.Select(c => c.Name), Is.EqualTo(Crowd.ClipNames), file);
                        Assert.That(body.Clips.All(c => c.Seconds >= 0.5f && c.Seconds <= 8f), file);
                        Assert.That(body.Shapes, Is.EqualTo(3), file + ": regular, heavy and slim");
                        Assert.That(body.Cards != null, Is.EqualTo(level == 1), file + ": far bodies have card pictures");
                        if (level == 1) Assert.That(body.CardSeatedTop, Is.InRange(0.45f, 0.8f), file + ": a seated card is cut to the seated fan");
                    }
                    finally { body?.Destroy(); }
                }
        }

        [Test]
        public void The_seated_bodies_are_seated_adults_on_the_floor_facing_plus_z()
        {
            for (int b = 0; b < 2; b++)
            {
                CrowdBody body = CrowdBody.Load(Crowd.BodyFiles[b, 0]);
                try
                {
                    Bounds box = body.Mesh.bounds;
                    Assert.AreEqual(0f, box.min.y, 0.02f, "the feet on the floor");
                    Assert.That(box.size.y, Is.InRange(1.2f, 1.5f), "a seated adult's height");
                    Assert.That(box.max.z, Is.GreaterThan(0.4f), "the knees and feet reach forward (+z) of the hips");
                    Assert.That(box.min.z, Is.GreaterThan(-0.4f), "nothing far behind the hips");
                }
                finally { body?.Destroy(); }
            }
        }

        [Test]
        public void A_fan_sits_in_every_seat_of_the_bowl()
        {
            Crowd crowd = Crowd.Build(_rink.transform);
            Assert.IsNotNull(crowd);
            Assert.AreEqual(_rink.transform, crowd.transform.parent);
            FanSeat[] seats = CrowdSeats.All();
            Assert.AreEqual(seats.Length, crowd.FanCount);
            var mine = Enumerable.Range(0, crowd.FanCount).Select(i => crowd.SeatOf(i)).Select(s => (s.X, s.Y, s.Z)).ToHashSet();
            Assert.That(seats.All(s => mine.Contains((s.X, s.Y, s.Z))));
        }

        [Test]
        public void Calm_Area_fans_are_still_and_dim_from_goalie_set_to_the_Shot()
        {
            Crowd crowd = Crowd.Build(_rink.transform);
            crowd.Show(In(RoundPhase.SlowDown), 9.0);
            crowd.Show(In(RoundPhase.SlowDown), 9.5);
            crowd.Show(In(RoundPhase.ShootingWindow), 10.0);
            double held = crowd.ClockIn(ArenaZone.CalmArea);
            crowd.Show(In(RoundPhase.ShootingWindow), 10.2);
            crowd.Show(In(RoundPhase.ShootingWindow), 10.4);

            Assert.AreEqual(CrowdClip.SitIdle, crowd.ClipIn(ArenaZone.CalmArea));
            Assert.AreEqual(held, crowd.ClockIn(ArenaZone.CalmArea), 1e-12, "the pose holds: no motion");
            Assert.AreEqual((float)CrowdLooks.StillLight, crowd.LightIn(ArenaZone.CalmArea), 1e-6f);
            Assert.AreEqual((float)CrowdLooks.StillContrast, crowd.ContrastIn(ArenaZone.CalmArea), 1e-6f);
        }

        [Test]
        public void Outside_the_shooting_window_the_Calm_Area_fans_move_at_full_light()
        {
            Crowd crowd = Crowd.Build(_rink.transform);
            crowd.Show(In(RoundPhase.SkateIn), 10.0);
            double before = crowd.ClockIn(ArenaZone.CalmArea);
            crowd.Show(In(RoundPhase.SkateIn), 10.1);

            Assert.AreEqual(0.1, crowd.ClockIn(ArenaZone.CalmArea) - before, 1e-9);
            Assert.AreEqual(1f, crowd.LightIn(ArenaZone.CalmArea));
        }

        [Test]
        public void On_a_Goal_every_zone_stands_and_cheers()
        {
            Crowd crowd = Crowd.Build(_rink.transform);
            crowd.Show(In(RoundPhase.SkateIn), 10.0);
            crowd.Show(In(RoundPhase.ResultHold, ArenaResult.Goal), 10.1);
            foreach (ArenaZone z in new[] { ArenaZone.CalmArea, ArenaZone.Near, ArenaZone.Far })
                Assert.AreEqual(CrowdClip.StandCheer, crowd.ClipIn(z), z.ToString());
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
        public void A_camera_draws_only_the_fans_it_can_see_with_bodies_or_far_cards_and_again_after_a_disable()
        {
            Crowd crowd = Crowd.Build(_rink.transform);
            crowd.Show(In(RoundPhase.SkateIn), 0.0);
            var target = new RenderTexture(320, 180, 24);
            var cam = new GameObject("Camera").AddComponent<Camera>();
            try
            {
                cam.targetTexture = target;
                cam.transform.SetPositionAndRotation(new Vector3(0, 1.5f, -10f), Quaternion.identity); // looking at the near net
                cam.Render();
                int bodies = crowd.LastDrawnFans;
                Assert.That(bodies, Is.GreaterThan(1000).And.LessThan(crowd.FanCount), "the stands behind the near net, not behind the camera");

                crowd.FarCards = true;
                cam.Render();
                Assert.AreEqual(bodies, crowd.LastDrawnFans, "the same fans, the far ones as cards");

                crowd.gameObject.SetActive(false);
                crowd.gameObject.SetActive(true);
                cam.Render();
                Assert.AreEqual(bodies, crowd.LastDrawnFans);
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(cam.gameObject);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void The_arena_model_has_no_flat_crowd_picture_left()
        {
            GameObject arena = ArenaStands.Build(_rink.transform);
            foreach (Renderer r in arena.GetComponentsInChildren<Renderer>())
            {
                Assert.AreNotEqual("Crowd", r.name);
                foreach (Material m in r.sharedMaterials) Assert.IsNull(m.mainTexture, r.name + " / " + m.name);
            }
        }
    }
}
