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
        public void RemoveRink() => RinkTeardown.Destroy(_rink);

        static ArenaState In(RoundPhase phase, ArenaResult result = ArenaResult.None) =>
            ArenaShow.At(new ArenaShowInput { Phase = phase, Result = result, ShotNumber = 1 });

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void Every_outfit_has_a_near_body_of_about_1500_triangles_and_a_far_one_of_about_400_with_all_eight_clips()
        {
            for (int o = 0; o < Crowd.BodyFiles.GetLength(0); o++)
                for (int level = 0; level < 2; level++)
                {
                    string file = Crowd.BodyFiles[o, level];
                    CrowdBody body = CrowdBody.Load(file);
                    try
                    {
                        Assert.IsNotNull(body, "Assets/Resources/Crowd/" + file + ".bytes (Tools/make_crowd.sh)");
                        Assert.That(body.Mesh.triangles.Length / 3, level == 0 ? Is.InRange(1300, 1700) : Is.InRange(300, 500), file);
                        Assert.That(body.Clips.Select(c => c.Name), Is.EqualTo(Crowd.ClipNames), file);
                        Assert.That(body.Shapes, Is.EqualTo(1), file);
                        Assert.That(body.Palette.Length, Is.InRange(3, 16), file);
                        Assert.That(body.Palette.Count(p => p.Kind == CrowdBody.PaintKind.TeamTop), Is.GreaterThanOrEqualTo(1), file + ": a team top");
                        foreach (var p in body.Palette.Where(p => p.Kind == CrowdBody.PaintKind.Own))
                            Assert.That(ReservedColours.IsSafe(CrowdLooks.SafeOutfitColour(new ArenaColour(p.Colour.r, p.Colour.g, p.Colour.b))), file);
                        Assert.That(body.ClipNamed("oh").Seconds, Is.EqualTo(body.ClipNamed("oh-back").Seconds), file);
                        Assert.That(body.ClipNamed("stand-cheer").Seconds, Is.EqualTo(body.ClipNamed("stand-cheer-right").Seconds), file);
                        Assert.That(body.Cards != null, Is.EqualTo(level == 1), file);
                        if (level == 1) Assert.That(body.CardSeatedTop, Is.InRange(0.45f, 0.8f), file);
                    }
                    finally { body?.Destroy(); }
                }
        }

        [Test]
        public void The_seated_bodies_are_seated_adults_on_the_floor_facing_plus_z()
        {
            for (int o = 0; o < Crowd.BodyFiles.GetLength(0); o++)
            {
                CrowdBody body = CrowdBody.Load(Crowd.BodyFiles[o, 0]);
                try
                {
                    Bounds box = body.Mesh.bounds;
                    Assert.AreEqual(0f, box.min.y, 0.02f, "the feet on the floor");
                    Assert.That(box.size.y, Is.InRange(1.2f, 1.6f), "a seated adult's height (the punks' tall hair reaches 1.58)");
                    Assert.That(box.max.z, Is.GreaterThan(0.4f), "the knees and feet reach forward (+z) of the hips");
                    Assert.That(box.min.z, Is.GreaterThan(-0.4f), "nothing far behind the hips");
                }
                finally { body?.Destroy(); }
            }
        }

        [Test]
        public void The_crowd_keeps_its_materials_and_card_pictures_after_Unity_unloads_unused_assets()
        {
            Crowd crowd = Crowd.Build(_rink.transform);
            Assert.IsNotNull(crowd);
            int materials = crowd.Materials.Count();
            Assert.That(materials, Is.GreaterThan(0));
            UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();
            Assert.AreEqual(materials, crowd.Materials.Count(), "Unity unloaded a crowd material as unused (#203)");
            Assert.IsTrue(crowd.BodyOf(0, 1).Cards != null, "Unity unloaded the far body's card picture as unused (#203)");
            Assert.IsTrue(crowd.BodyOf(0, 0).Vat != null, "Unity unloaded a VAT picture as unused (#203)");
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
        public void Moving_the_parent_and_camera_together_keeps_the_same_visible_fans()
        {
            CheckMovedCrowd(false);
        }

        [Test]
        public void Changed_parent_scale_and_sheared_crowd_match_a_fresh_crowd_after_enable()
        {
            CheckMovedCrowd(true);
        }

        void CheckMovedCrowd(bool scaleAndShear)
        {
            Crowd crowd = Crowd.Build(_rink.transform);
            crowd.Show(In(RoundPhase.SkateIn), 0.0);
            var target = new RenderTexture(320, 180, 24);
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.SetParent(_rink.transform, false);
            cam.transform.localPosition = new Vector3(0, 1.5f, -10f);
            try
            {
                cam.targetTexture = target;
                cam.Render(); // Warm the original bounds before changing the parent.
                AssertCrowdMaterialMatrices(crowd);
                int before = crowd.LastDrawnFans;
                Assert.That(before, Is.GreaterThan(1000).And.LessThan(crowd.FanCount));
                _rink.transform.SetPositionAndRotation(new Vector3(5000, 20, -4000),
                    Quaternion.Euler(0, scaleAndShear ? 37 : 180, 0));
                if (scaleAndShear)
                {
                    _rink.transform.localScale = new Vector3(-1.5f, 0.75f, 2f);
                    crowd.transform.localRotation = Quaternion.Euler(0, 23, 0);
                    crowd.transform.localScale = new Vector3(0.8f, 1.3f, 1.1f);
                }
                cam.Render();
                AssertCrowdMaterialMatrices(crowd);
                int moved = crowd.LastDrawnFans;
                if (!scaleAndShear) Assert.AreEqual(before, moved, "a rigid move with the camera keeps the visible fans");

                // A new crowd at the same transform supplies a cold-cache reference.
                Crowd reference = Crowd.Build(_rink.transform);
                reference.transform.localPosition = crowd.transform.localPosition;
                reference.transform.localRotation = crowd.transform.localRotation;
                reference.transform.localScale = crowd.transform.localScale;
                reference.Show(In(RoundPhase.SkateIn), 0.0);
                cam.Render();
                Assert.That(reference.LastDrawnFans, Is.GreaterThan(0).And.LessThan(reference.FanCount));
                Assert.AreEqual(reference.LastDrawnFans, moved, "changed transforms must match fresh bounds");
                crowd.gameObject.SetActive(false);
                crowd.gameObject.SetActive(true);
                cam.Render();
                Assert.AreEqual(reference.LastDrawnFans, crowd.LastDrawnFans, "enable must retain correct bounds");
                AssertCrowdMaterialMatrices(crowd);
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(cam.gameObject);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        static void AssertCrowdMaterialMatrices(Crowd crowd)
        {
            Matrix4x4 expected = crowd.transform.localToWorldMatrix;
            int checkedMaterials = 0;
            foreach (Material material in crowd.Materials)
            {
                Assert.That(material.GetMatrix("_CrowdToWorld").Equals(expected), Is.True,
                    material.name + " must draw at the current crowd transform");
                checkedMaterials++;
            }
            Assert.That(checkedMaterials, Is.GreaterThan(0));
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
