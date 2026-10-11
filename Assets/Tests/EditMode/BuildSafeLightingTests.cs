using System.Collections.Generic;
using System.IO;
using System.Linq;
using HockeyGame;
using HockeyGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// A player build keeps only the shader variants that materials and scenes in the build use. Materials the game makes
    /// in code (glass, glow, unlit lamps) and fog set in code would silently lose their look in a build, so each must have
    /// a twin asset in the build.
    /// </summary>
    public class BuildSafeLightingTests
    {
        static string Variant(Material m) =>
            m.shader.name + " [" + string.Join(" ", m.shaderKeywords.OrderBy(k => k)) + "]";

        [Test]
        public void Every_material_the_rink_shows_has_a_twin_in_Resources_so_the_build_keeps_its_shader_variant()
        {
            var inBuild = new HashSet<string>(Resources.LoadAll<Material>("").Select(Variant));
            Camera camera = null;
            try
            {
                RinkBuilder.Build(out camera);
                Transform rink = GameObject.Find("Rink").transform;
                var goalie = new GoalieView(rink, new GoalieCharacter(rink));
                new ShotEffects(rink, camera, null, goalie);
                new GoalLight(rink).SetOn(true);
                goalie.Show(true, GoaliePose.RightButterfly);
                goalie.ShowGaps(NetFaceGaps.Where((x, y) => true)); // Easy mode glow on

                var missing = new SortedSet<string>();
                foreach (Renderer r in rink.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials)
                        if (m != null && !inBuild.Contains(Variant(m))) missing.Add(r.name + ": " + Variant(m));
                Assert.IsEmpty(missing, "made in code with no twin asset:\n" + string.Join("\n", missing));
            }
            finally
            {
                Object.DestroyImmediate(GameObject.Find("Rink"));
                if (camera != null) Object.DestroyImmediate(camera.gameObject);
            }
        }

        [Test]
        public void The_rink_scene_has_linear_fog_on_so_the_build_keeps_the_fog_shaders()
        {
            // Unity strips fog variants for fog modes no scene in the build uses (Graphics settings: Fog Modes Automatic).
            string scene = File.ReadAllText("Assets/Scenes/Rink.unity");
            StringAssert.Contains("m_Fog: 1", scene);
            StringAssert.Contains("m_FogMode: 1", scene); // 1 = Linear, as ArenaLighting sets
        }
    }
}
