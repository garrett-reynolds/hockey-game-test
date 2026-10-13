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
        /// <summary>Destroys rink and every material under it that is not a project asset (assets are shared; the rest were made by the build).</summary>
        public static void Destroy(GameObject rink)
        {
            if (rink == null) return;
            var made = new HashSet<Material>();
            foreach (Renderer r in rink.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    if (m != null && !AssetDatabase.Contains(m)) made.Add(m);
            Object.DestroyImmediate(rink);
            foreach (Material m in made) Object.DestroyImmediate(m);
        }
    }
}
