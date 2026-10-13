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
            foreach (Renderer r in rink.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null || AssetDatabase.Contains(m)) continue;
                    made.Add(m);
                    // A texture on any shader slot of a made material that is not a project asset (for example "Net holes") was made by the build.
                    foreach (string slot in m.GetTexturePropertyNames())
                    {
                        Texture t = m.GetTexture(slot);
                        if (t != null && !EditorUtility.IsPersistent(t)) madeTextures.Add(t); // persistent = project asset or built-in, never freed
                    }
                }
            Object.DestroyImmediate(rink);
            foreach (Material m in made) Object.DestroyImmediate(m);
            foreach (Texture t in madeTextures) Object.DestroyImmediate(t);
        }
    }
}
