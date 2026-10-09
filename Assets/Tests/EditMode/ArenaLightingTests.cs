using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame.Core.Tests
{
    public class ArenaLightingTests
    {
        GameObject _rink;
        AmbientMode _ambientMode;
        Color _sky, _equator, _ground;
        bool _fog;

        [SetUp]
        public void Save()
        {
            _rink = new GameObject("Rink");
            _ambientMode = RenderSettings.ambientMode;
            _sky = RenderSettings.ambientSkyColor;
            _equator = RenderSettings.ambientEquatorColor;
            _ground = RenderSettings.ambientGroundColor;
            _fog = RenderSettings.fog;
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
        }

        [Test]
        public void The_main_arena_light_shines_steeply_down_from_the_shooters_side_with_soft_shadows()
        {
            Light main = ArenaLighting.Build(_rink.transform);
            Assert.AreEqual(LightType.Directional, main.type);
            Assert.AreEqual(LightShadows.Soft, main.shadows, "shadows on the ice show where the goalie and puck are");
            Vector3 dir = main.transform.forward;
            Assert.That(Vector3.Angle(dir, Vector3.down), Is.LessThan(40f), "overhead arena light");
            Assert.That(dir.z, Is.GreaterThan(0f), "lights the goalie's front, as the shooter sees it");
        }

        [Test]
        public void The_arena_adds_at_most_three_more_lights_so_the_goal_light_glow_still_reaches_the_ice()
        {
            ArenaLighting.Build(_rink.transform);
            int extra = 0;
            foreach (Light l in _rink.GetComponentsInChildren<Light>())
                if (l.type != LightType.Directional) extra++;
            Assert.That(extra, Is.InRange(1, 3)); // URP Forward lights each object with at most 4 extra lights
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
    }
}
