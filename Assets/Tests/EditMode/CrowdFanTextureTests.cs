using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>The no-compute path (the Web build): the fans' data goes to the shaders in a float texture. The Editor has
    /// compute shaders, so the path is forced here; the no-compute shaders themselves draw only in the Web build.</summary>
    public class CrowdFanTextureTests
    {
        GameObject _rink;

        [SetUp]
        public void MakeRink()
        {
            _rink = new GameObject("Rink");
            Crowd.ForceFanTexture = true;
        }

        [TearDown]
        public void RemoveRink()
        {
            Crowd.ForceFanTexture = null;
            RinkTeardown.Destroy(_rink);
        }

        static void AssertTextureHoldsEveryFan(Crowd crowd)
        {
            Texture2D tex = crowd.FanTexture;
            Assert.IsNotNull(tex);
            Assert.AreEqual(TextureFormat.RGBAFloat, tex.format);
            Assert.AreEqual(Crowd.FansPerTextureRow * 6, tex.width);
            Assert.That(tex.width * tex.height, Is.GreaterThanOrEqualTo(crowd.FanCount * 6));
            var texels = tex.GetPixelData<Vector4>(0);
            for (int i = 0; i < crowd.FanCount; i++)
            {
                Crowd.FanData d = crowd.DataOf(i);
                int t = i * 6;
                Assert.AreEqual(d.Place, texels[t], "fan " + i + " place");
                Assert.AreEqual(d.Shirt, texels[t + 1], "fan " + i + " shirt");
                Assert.AreEqual(d.Skin, texels[t + 2], "fan " + i + " skin");
                Assert.AreEqual(d.Hair, texels[t + 3], "fan " + i + " hair");
                Assert.AreEqual(d.Look, texels[t + 4], "fan " + i + " look");
                Assert.AreEqual(d.Acts, texels[t + 5], "fan " + i + " acts");
            }
        }

        [Test]
        public void Without_compute_shaders_the_crowd_is_built_with_its_fans_in_a_float_texture_and_no_buffer()
        {
            Crowd crowd = Crowd.Build(_rink.transform);
            Assert.IsNotNull(crowd, "the Web build gets a crowd too");
            Assert.IsFalse(crowd.HasFanBuffer, "no structured buffer without compute shaders");
            AssertTextureHoldsEveryFan(crowd);
            foreach (Material m in crowd.Materials)
            {
                Assert.AreSame(crowd.FanTexture, m.GetTexture("_FanTex"), m.name);
                Assert.AreEqual(Crowd.FansPerTextureRow * 6, m.GetFloat("_FanTexWidth"), m.name);
            }
        }

        [Test]
        public void The_fan_texture_follows_a_regroup()
        {
            Crowd crowd = Crowd.Build(_rink.transform);
            crowd.Regroup(8);
            AssertTextureHoldsEveryFan(crowd);
        }

        [Test]
        public void With_compute_shaders_the_crowd_keeps_its_structured_buffer()
        {
            Crowd.ForceFanTexture = false;
            Crowd crowd = Crowd.Build(_rink.transform);
            Assert.IsTrue(crowd.HasFanBuffer);
            Assert.IsNull(crowd.FanTexture);
        }
    }
}
