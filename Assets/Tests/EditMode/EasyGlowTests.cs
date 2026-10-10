using System.Collections.Generic;
using System.Linq;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// Easy mode's green glow on the Open Zones shows before the Shot, though the goalie in its Ready Stance (or the
    /// neutral blocks) stands in front of them: seen from the shooter's eye, the glow draws over whatever is in front of
    /// it. Once the goalie reacts it is depth-tested again, so the flying puck stays in front of it.
    /// </summary>
    public class EasyGlowTests
    {
        GameObject _parent;
        readonly List<Object> _made = new List<Object>();
        readonly Dictionary<Collider, Renderer> _bakedFor = new Dictionary<Collider, Renderer>(); // a baked body collider: its renderer

        [SetUp]
        public void SetUp() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
            Object.DestroyImmediate(_parent);
        }

        static CompareFunction DepthTest(Material m) =>
            m.HasProperty("_ZTest") ? (CompareFunction)Mathf.RoundToInt(m.GetFloat("_ZTest")) : CompareFunction.LessEqual;

        /// <summary>Colliders matching what is drawn under the parent now (the skinned body baked as posed).</summary>
        void AddColliders()
        {
            foreach (Renderer r in _parent.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                if (r is SkinnedMeshRenderer skinned)
                {
                    var baked = new Mesh();
                    skinned.BakeMesh(baked, true);
                    var holder = new GameObject(r.name + " (collider)");
                    holder.transform.SetPositionAndRotation(skinned.transform.position, skinned.transform.rotation);
                    var collider = holder.AddComponent<MeshCollider>();
                    collider.sharedMesh = baked;
                    _bakedFor[collider] = r;
                    _made.Add(holder);
                    _made.Add(baked);
                }
                else
                {
                    r.gameObject.AddComponent<MeshCollider>().sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                }
            }
            Physics.SyncTransforms();
        }

        Renderer RendererOf(Collider c) => _bakedFor.TryGetValue(c, out Renderer r) ? r : c.GetComponent<Renderer>();

        [TestCase(false, TestName = "With the goalie character")]
        [TestCase(true, TestName = "With the goalie blocks")]
        public void Before_the_Shot_the_glow_draws_over_the_goalie_in_front_of_every_Open_Zone(bool blocks)
        {
            var character = blocks ? new GoalieCharacter(_parent.transform, name => null) : new GoalieCharacter(_parent.transform);
            if (!blocks) Assert.IsTrue(character.Present, "the goalie model loads");
            var view = new GoalieView(_parent.transform, character);
            int covered = 0, open = 0;
            var problems = new List<string>();
            foreach (GoaliePose pose in System.Enum.GetValues(typeof(GoaliePose)))
            {
                Vector3 eye = GoalieCoverageTests.Eyes(GoaliePoses.SideOf(pose)).First().Item2; // the window opens
                float facing = GoalieCharacter.FacingToward(eye);
                view.Show(true, pose, true);
                view.Face(facing, facing);
                view.SetProgress(0f); // Ready Stance: no Shot yet
                AddColliders();
                Transform glows = _parent.transform.Find("Glow " + pose);
                for (int column = 0; column < 3; column++)
                for (int row = 0; row < 3; row++)
                {
                    if (GoaliePoses.CoverAt(pose, column, row) != ZoneCover.Open) continue;
                    open++;
                    Renderer glow = glows.Find("Zone " + column + "," + row).GetComponent<Renderer>();
                    Vector3 ray = glow.bounds.center - eye;
                    RaycastHit[] hits = Physics.RaycastAll(eye, ray.normalized, ray.magnitude + 0.1f).OrderBy(h => h.distance).ToArray();
                    int at = System.Array.FindIndex(hits, h => RendererOf(h.collider) == glow);
                    string where = pose + " Zone " + column + "," + row;
                    if (at < 0) { problems.Add(where + ": the glow is not on the line from the eye"); continue; }
                    if (at > 0) covered++;
                    Material glowMaterial = glow.sharedMaterial;
                    for (int i = 0; i < at; i++)
                    {
                        Renderer front = RendererOf(hits[i].collider);
                        foreach (Material m in front.sharedMaterials)
                            if (DepthTest(glowMaterial) != CompareFunction.Always || m.renderQueue >= glowMaterial.renderQueue)
                                problems.Add(where + ": hidden behind " + front.name + " (glow depth test " + DepthTest(glowMaterial)
                                    + ", queue " + glowMaterial.renderQueue + " vs " + m.renderQueue + ")");
                    }
                }
                foreach (MeshCollider c in _parent.GetComponentsInChildren<MeshCollider>(true)) Object.DestroyImmediate(c);
                foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
                _made.Clear();
                _bakedFor.Clear();
            }
            Assert.IsEmpty(problems, string.Join("\n", problems.Distinct()));
            Assert.Greater(open, 0);
            Assert.Greater(covered, 0, "the Ready Stance stands in front of some Open Zones (else this test proves nothing)");
        }

        [Test]
        public void Once_the_goalie_reacts_the_glow_is_depth_tested_so_the_puck_stays_in_front()
        {
            var view = new GoalieView(_parent.transform, new GoalieCharacter(_parent.transform));
            view.Show(true, GoaliePose.MiddleButterfly, true);
            Renderer glow = _parent.transform.Find("Glow MiddleButterfly").GetComponentInChildren<Renderer>();
            view.SetProgress(0f);
            Assert.AreEqual(CompareFunction.Always, DepthTest(glow.sharedMaterial), "before the Shot");
            view.SetProgress(0.1f);
            Assert.AreEqual(CompareFunction.LessEqual, DepthTest(glow.sharedMaterial), "reacting");
            view.SetProgress(1f);
            Assert.AreEqual(CompareFunction.LessEqual, DepthTest(glow.sharedMaterial), "posed");
        }
    }
}
