using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The one teardown for a test that builds the rink (#152): RinkBuilder makes a material for many of its parts (banners, ribbon
    /// panels, ads, boards...), and destroying the rink's GameObjects does not free them.
    /// </summary>
    public static class RinkTeardown
    {
        /// <summary>Destroys rink and every material and texture under it that is not a project asset (assets are shared; the rest were made by the build, #158).</summary>
        public static void Destroy(GameObject rink)
        {
            if (rink == null) return;
            var made = new HashSet<Material>();
            var madeTextures = new HashSet<Texture>();
            var madeMeshes = new HashSet<Mesh>();
            foreach (MeshFilter f in rink.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh != null && MadeAtRuntime(f.sharedMesh)) madeMeshes.Add(f.sharedMesh);
            foreach (SkinnedMeshRenderer s in rink.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (s.sharedMesh != null && MadeAtRuntime(s.sharedMesh)) madeMeshes.Add(s.sharedMesh);
            foreach (Renderer r in rink.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null || !MadeAtRuntime(m)) continue;
                    made.Add(m);
                    // A texture on any shader slot of a made material that is not a project asset (for example "Net holes") was made by the build.
                    foreach (string slot in m.GetTexturePropertyNames())
                    {
                        Texture t = m.GetTexture(slot);
                        if (t != null && MadeAtRuntime(t)) madeTextures.Add(t);
                    }
                }
            Object.DestroyImmediate(rink);
            foreach (Material m in made) Object.DestroyImmediate(m);
            foreach (Texture t in madeTextures) Object.DestroyImmediate(t);
            foreach (Mesh m in madeMeshes) Object.DestroyImmediate(m);
        }

        /// <summary>True for an object the build made: not a project asset, not a built-in (persistent), not Unity's own hidden don't-save objects (white texture).</summary>
        static bool MadeAtRuntime(Object o) =>
            !EditorUtility.IsPersistent(o) && !AssetDatabase.Contains(o) && (o.hideFlags & (HideFlags.DontSave | HideFlags.DontUnloadUnusedAsset)) == 0;
    }
}
