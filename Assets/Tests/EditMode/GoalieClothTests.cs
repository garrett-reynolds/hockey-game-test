using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The goalie's jersey, pants and socks are matte cloth (#170): no smoothness, no specular highlight, so the arena lights
    /// leave no bright shine. The hard gear (pads, mask, blocker, glove, stick, skates) keeps its look.
    /// </summary>
    public class GoalieClothTests
    {
        const float ClothSmoothnessLimit = 0.1f; // the model imported at 0.4, which shines like plastic
        const float GearSmoothness = 0.4f;       // what the hard gear imports at, and keeps

        GameObject _parent;

        [SetUp]
        public void SetUp() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_parent); }

        static bool IsCloth(string rendererName) => rendererName == "Jersey" || rendererName == "Pants" || rendererName == "Socks";

        static float Smoothness(Material m) => m.GetFloat("_Smoothness");

        [Test]
        public void Jersey_pants_and_socks_are_matte()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "the goalie model loads");
            int seen = 0;
            foreach (Renderer r in character.Root.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsCloth(r.name)) continue;
                foreach (Material m in r.sharedMaterials)
                {
                    seen++;
                    Assert.LessOrEqual(Smoothness(m), ClothSmoothnessLimit, r.name + " / " + m.name + " smoothness");
                    Assert.IsTrue(m.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"), r.name + " / " + m.name + " specular highlights off");
                }
            }
            Assert.AreEqual(5, seen, "jersey (2), pants (1) and socks (2) materials");
        }

        [Test]
        public void The_hard_gear_and_skin_keep_their_look_and_share_no_material_with_the_cloth()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "the goalie model loads");
            var cloth = new HashSet<Material>();
            foreach (Renderer r in character.Root.GetComponentsInChildren<Renderer>(true))
                if (IsCloth(r.name)) cloth.UnionWith(r.sharedMaterials);
            int seen = 0;
            foreach (Renderer r in character.Root.GetComponentsInChildren<Renderer>(true))
            {
                if (IsCloth(r.name)) continue;
                foreach (Material m in r.sharedMaterials)
                {
                    seen++;
                    Assert.IsFalse(cloth.Contains(m), r.name + " / " + m.name + " is shared with the cloth");
                    if (m.name == "Visor") continue; // glossy on purpose
                    Assert.AreEqual(GearSmoothness, Smoothness(m), 0.001f, r.name + " / " + m.name + " smoothness");
                    Assert.IsFalse(m.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"), r.name + " / " + m.name + " keeps its highlight");
                }
            }
            Assert.Greater(seen, 10, "gear materials seen");
        }
    }
}
