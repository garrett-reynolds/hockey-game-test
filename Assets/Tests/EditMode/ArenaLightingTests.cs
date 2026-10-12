using System.Collections.Generic;
using System.Linq;
using HockeyGame;
using HockeyGame.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HockeyGame.Core.Tests
{
    public class ArenaLightingTests
    {
        GameObject _rink;
        AmbientMode _ambientMode;
        Color _sky, _equator, _ground;
        bool _fog;
        DefaultReflectionMode _reflectionMode;
        Texture _reflection;

        [SetUp]
        public void Save()
        {
            _rink = new GameObject("Rink");
            _ambientMode = RenderSettings.ambientMode;
            _sky = RenderSettings.ambientSkyColor;
            _equator = RenderSettings.ambientEquatorColor;
            _ground = RenderSettings.ambientGroundColor;
            _fog = RenderSettings.fog;
            _reflectionMode = RenderSettings.defaultReflectionMode;
            _reflection = RenderSettings.customReflectionTexture;
        }

        [TearDown]
        public void Restore()
        {
            Object.DestroyImmediate(_rink);
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientSkyColor = _sky;
            RenderSettings.ambientEquatorColor = _equator;
            RenderSettings.ambientGroundColor = _ground;
            RenderSettings.fog = _fog;
            RenderSettings.defaultReflectionMode = _reflectionMode;
            RenderSettings.customReflectionTexture = _reflection;
        }

        [Test]
        public void The_main_arena_light_shines_almost_straight_down_with_soft_shadows_so_they_stay_short()
        {
            Light main = ArenaLighting.Build(_rink.transform);
            Assert.AreEqual(LightType.Directional, main.type);
            Assert.AreEqual(LightShadows.Soft, main.shadows, "shadows on the ice show where the goalie and puck are");
            Assert.That(main.shadowStrength, Is.LessThanOrEqualTo(0.6f), "soft arena shadows, not a hard sun");
            Vector3 dir = main.transform.forward;
            Assert.That(Vector3.Angle(dir, Vector3.down), Is.LessThan(20f), "overhead arena light: short shadows");
            Assert.That(dir.z, Is.GreaterThan(0f), "lights the goalie's front, as the shooter sees it");
        }

        static bool Reaches(Light l, Vector3 p)
        {
            Vector3 to = p - l.transform.position;
            return to.magnitude < l.range && Vector3.Angle(l.transform.forward, to) < l.spotAngle / 2f;
        }

        [Test]
        public void At_least_two_soft_shadowed_overhead_lights_reach_the_crease_from_different_sides()
        {
            ArenaLighting.Build(_rink.transform);
            var crease = new Vector3(0f, 0f, -0.5f);
            var shadowing = new List<Light>();
            foreach (Light l in _rink.GetComponentsInChildren<Light>())
                if (l.type == LightType.Spot && l.shadows == LightShadows.Soft && Reaches(l, crease)) shadowing.Add(l);
            Assert.That(shadowing.Count, Is.GreaterThanOrEqualTo(2), "several soft shadows under the goalie and net");
            Assert.That(shadowing.Min(l => l.transform.position.x), Is.LessThan(0f));
            Assert.That(shadowing.Max(l => l.transform.position.x), Is.GreaterThan(0f));
            foreach (Light l in shadowing)
                Assert.That(l.transform.position.y, Is.GreaterThan(10f), "high overhead: short shadows");
        }

        [Test]
        public void The_lights_stay_cheap_a_few_lights_and_at_most_three_cast_shadows()
        {
            ArenaLighting.Build(_rink.transform);
            Light[] lights = _rink.GetComponentsInChildren<Light>();
            Assert.That(lights.Length, Is.LessThanOrEqualTo(8));
            Assert.That(lights.Count(l => l.shadows != LightShadows.None), Is.LessThanOrEqualTo(3), "each shadowed light renders the goalie and net again");
        }

        [Test]
        public void Overhead_light_banks_hang_under_the_roof_over_the_bowl_and_glow_bright_enough_to_bloom()
        {
            ArenaLighting.Build(_rink.transform);
            Volume volume = _rink.GetComponentInChildren<Volume>();
            volume.sharedProfile.TryGet(out Bloom bloom);
            Renderer[] banks = _rink.GetComponentsInChildren<Renderer>().Where(r => r.name == ArenaLighting.LightBank).ToArray();
            Assert.That(banks.Length, Is.GreaterThanOrEqualTo(6));
            Bounds boards = RinkBoards.Box, arena = ArenaStands.Box;
            Assert.That(banks.Count(r => boards.Contains(new Vector3(r.bounds.center.x, 1f, r.bounds.center.z))), Is.GreaterThanOrEqualTo(6), "over the ice");
            foreach (Renderer r in banks)
            {
                Vector3 c = r.bounds.center;
                Assert.That(c.y, Is.InRange(12f, ArenaStands.RoofHeight - 1f), "seen when the camera looks up a little");
                Assert.That(c.x, Is.InRange(arena.min.x, arena.max.x), "under the roof");
                Assert.That(c.z, Is.InRange(arena.min.z, arena.max.z), "under the roof");
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, r.shadowCastingMode, "no shadow on the ice");
                Color glow = r.sharedMaterial.GetColor("_EmissionColor");
                Assert.That(glow.maxColorComponent, Is.GreaterThan(bloom.threshold.value * 2f), "a bright lamp, with bloom");
            }
        }

        [Test]
        public void The_roof_lights_pulse_in_the_Goal_Show_colour_while_lit_and_come_back_exactly_after()
        {
            ArenaLighting.Build(_rink.transform);
            var roof = new RoofLights(_rink.transform);
            Renderer[] banks = _rink.GetComponentsInChildren<Renderer>().Where(r => r.name == ArenaLighting.LightBank).ToArray();
            Light[] lights = _rink.GetComponentsInChildren<Light>();
            Light[] overhead = lights.Where(l => l.name == ArenaLighting.OverheadLight).ToArray();
            Assert.That(overhead.Length, Is.GreaterThanOrEqualTo(2));
            Color[] bankGlows = banks.Select(r => r.sharedMaterial.GetColor("_EmissionColor")).ToArray();
            Color[] lightColours = lights.Select(l => l.color).ToArray();
            float[] intensities = lights.Select(l => l.intensity).ToArray();

            var lit = new GoalShowLights(true, true, ArenaShow.GoalShowFlashesPerSecond, true, ArenaShow.GoalRed);
            var dark = new GoalShowLights(true, true, ArenaShow.GoalShowFlashesPerSecond, false, ArenaShow.GoalRed);

            roof.Show(lit);
            foreach (Renderer r in banks)
            {
                Color glow = r.sharedMaterial.GetColor("_EmissionColor");
                Assert.Greater(glow.r, glow.g * 3f, "the banks glow Goal red");
                Assert.Greater(glow.maxColorComponent, 1f, "still bright enough to bloom");
            }
            foreach (Light l in overhead)
                Assert.Greater(l.color.r, l.color.g * 3f, l.name + " shines Goal red");
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].name != ArenaLighting.OverheadLight)
                    Assert.AreEqual(lightColours[i], lights[i].color, lights[i].name + " never pulses (the goalie's shadows)");

            foreach (GoalShowLights after in new[] { dark, default(GoalShowLights) })
            {
                roof.Show(lit);
                roof.Show(after);
                for (int i = 0; i < banks.Length; i++)
                    Assert.AreEqual(bankGlows[i], banks[i].sharedMaterial.GetColor("_EmissionColor"), "bank back to its white after " + after);
                for (int i = 0; i < lights.Length; i++)
                {
                    Assert.AreEqual(lightColours[i], lights[i].color, lights[i].name + " after " + after);
                    Assert.AreEqual(intensities[i], lights[i].intensity, lights[i].name + " after " + after);
                }
            }
        }

        [Test]
        public void The_ice_and_glass_reflect_a_cubemap_of_the_arena_rendered_once_at_start()
        {
            ArenaLighting.Build(_rink.transform);
            Assert.AreEqual(DefaultReflectionMode.Custom, RenderSettings.defaultReflectionMode);
            Texture cube = RenderSettings.customReflectionTexture;
            Assert.IsNotNull(cube, "the arena is rendered into the scene's reflection");
            Assert.AreEqual(TextureDimension.Cube, cube.dimension);
            Assert.That(cube.width, Is.LessThanOrEqualTo(256), "cheap: one small render at start");
            Assert.IsNull(_rink.GetComponentInChildren<Camera>(), "the reflection camera is gone after the render");
        }

        [Test]
        public void The_arena_is_lit_from_above_and_fades_into_the_dark_stands()
        {
            ArenaLighting.Build(_rink.transform);
            Assert.AreEqual(AmbientMode.Trilight, RenderSettings.ambientMode);
            Assert.That(RenderSettings.ambientSkyColor.grayscale, Is.GreaterThan(RenderSettings.ambientGroundColor.grayscale));
            Assert.IsTrue(RenderSettings.fog);
            Assert.That(RenderSettings.fogStartDistance, Is.GreaterThan(30f), "no fog over the skate-in or the net");
        }
        [Test]
        public void Light_post_processing_comes_from_a_volume_profile_asset_so_the_player_build_keeps_its_shaders()
        {
            ArenaLighting.Build(_rink.transform);
            Volume volume = _rink.GetComponentInChildren<Volume>();
            Assert.IsNotNull(volume, "a global post-processing volume");
            Assert.IsTrue(volume.isGlobal);
            // URP strips bloom and tonemapping from a build unless a profile asset under Assets/ uses them.
            StringAssert.StartsWith("Assets/", AssetDatabase.GetAssetPath(volume.sharedProfile));

            Assert.IsTrue(volume.sharedProfile.TryGet(out Tonemapping tonemapping) && tonemapping.active);
            Assert.AreEqual(TonemappingMode.Neutral, tonemapping.mode.value, "natural colours: the goal light stays red, the Easy glow green");
            Assert.IsTrue(volume.sharedProfile.TryGet(out Bloom bloom) && bloom.IsActive());
            Assert.That(bloom.threshold.value, Is.GreaterThanOrEqualTo(1f), "only lights glow, not the white ice");
            Assert.IsTrue(volume.sharedProfile.TryGet(out Vignette vignette) && vignette.IsActive());
            Assert.That(vignette.intensity.value, Is.LessThanOrEqualTo(0.3f), "a slight vignette");
        }

        [Test]
        public void The_shooter_camera_renders_post_processing()
        {
            RinkBuilder.Build(out Camera camera);
            try
            {
                Assert.IsTrue(camera.GetUniversalAdditionalCameraData().renderPostProcessing);
            }
            finally
            {
                Object.DestroyImmediate(GameObject.Find("Rink"));
                Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
