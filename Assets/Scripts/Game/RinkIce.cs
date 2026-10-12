using System;
using System.Collections.Generic;
using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// The ice sheet: a square centred on the centre line (x = 0, z = RinkLayout.IceCentreZ) with its top at y = 0, past
    /// the boards at both ends of the full rink. Uses the ice model (Assets/Resources/Models/ice.obj, textured with the rink
    /// lines and creases) when there is one, otherwise a plain pale-blue plane.
    /// The model's ice is game worn (#88, prototype look D): TV-bright white with a soft sheen that shows the arena
    /// (ArenaLighting's reflection cubemap), matte in the Calm Area (its smoothness map, Tools/make_ice_wear.py), and under
    /// a see-through layer of faint skate marks and snow along the boards (none in the Calm Area), below the ice logos.
    /// </summary>
    public static class RinkIce
    {
        /// <summary>Side of the ice square (metres).</summary>
        public const float Size = (float)RinkLayout.IceSize;
        /// <summary>z of the middle of the ice square: the centre line (metres).</summary>
        public const float CentreZ = (float)RinkLayout.IceCentreZ;

        /// <summary>Height an ice model with some thickness is fitted to (metres). The flat ice sheet keeps no height.</summary>
        const float Thickness = 0.05f;

        /// <summary>The see-through skate-mark and snow layer over the ice.</summary>
        public const string WearPart = "Ice Wear";
        /// <summary>
        /// The ice material template in Assets/Resources/Lighting (Hockey Game > Build Arena Lighting Assets): URP Lit with
        /// the smoothness map (Models/ice-smoothness), an asset so a player build keeps the smoothness-map shader variant.
        /// </summary>
        public const string IceMaterial = "Ice";
        /// <summary>The skate-mark and snow art in Assets/Resources/Models (Tools/make_ice_wear.py).</summary>
        public const string WearArt = "Models/ice-wear";

        /// <summary>
        /// URP Lit smoothness of the ice: a soft sheen that shows the light banks, boards and ads (ArenaLighting's reflection
        /// cubemap) without turning into a mirror. The smoothness map takes the Calm Area down to about 30% of it.
        /// </summary>
        public const float Gloss = 0.7f;
        /// <summary>The skate marks and snow are rougher than the ice.</summary>
        const float WearGloss = 0.15f;
        /// <summary>How far the skate-mark and snow layer lies over the ice: under the ice logos (RinkAds, 6 mm).</summary>
        const float WearLift = 0.003f;
        /// <summary>TV-bright white, a touch cool, still under the tonemapper's white so highlights show on it.</summary>
        static readonly Color Tint = new Color(0.88f, 0.91f, 0.95f);

        /// <summary>Adds the ice under rink. findFile is for tests; by default the model comes from Assets/Resources/Models.</summary>
        public static GameObject Build(Transform rink, Func<string, GameObject> findFile = null)
        {
            var ice = new GameObject("Ice");
            ice.transform.SetParent(rink, false);
            ice.transform.localPosition = new Vector3(0f, 0f, CentreZ);

            GameObject model = ModelFiles.Place(ice.transform, ModelFiles.Ice, Placeholder, findFile, new Vector3(Size, Thickness, Size));
            // FitTo centres the model on y = 0; a model with thickness is lifted so its top is at y = 0.
            float top = Bounds(model).max.y - ice.transform.position.y;
            model.transform.localPosition -= Vector3.up * top;

            var template = Resources.Load<Material>(ArenaLighting.AssetFolder + "/" + IceMaterial);
            bool worn = false;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                // Never change the imported model's material asset. The textured model takes the template (with the
                // smoothness map); the plain placeholder keeps a copy of its own material.
                Texture markings = r.sharedMaterial.mainTexture;
                bool textured = markings != null && template != null;
                Material m;
                if (textured)
                {
                    m = new Material(template) { name = "Ice" };
                    m.mainTexture = markings;
                    m.mainTextureScale = r.sharedMaterial.mainTextureScale;
                    m.mainTextureOffset = r.sharedMaterial.mainTextureOffset;
                    m.color = Tint;
                }
                else
                {
                    m = new Material(r.sharedMaterial);
                    m.color *= Tint;
                }
                m.SetFloat("_Smoothness", Gloss);
                r.sharedMaterial = m;
                worn |= textured;
            }
            if (worn) AddWear(ice.transform);
            return ice;
        }

        /// <summary>
        /// The skate-mark and snow layer: a flat sheet over the rink inside the boards, WearLift over the ice, see-through,
        /// rougher than the ice and drawn before the ice logos (one step earlier in the transparent queue) so they lie on it.
        /// </summary>
        static void AddWear(Transform ice)
        {
            var art = Resources.Load<Texture2D>(WearArt);
            var glass = Resources.Load<Material>(ArenaLighting.AssetFolder + "/" + ArenaLighting.GlassMaterial);
            if (art == null || glass == null) return;

            float x = (float)RinkLayout.HalfWidth, near = (float)RinkLayout.NearEndBoardsZ - CentreZ, far = (float)RinkLayout.FarEndBoardsZ - CentreZ;
            var mesh = new Mesh { name = WearPart };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(x, WearLift, near), new Vector3(x, WearLift, far),
                new Vector3(-x, WearLift, far), new Vector3(-x, WearLift, near),
            });
            // The art's left edge is -x and its top edge the near end boards.
            mesh.SetUVs(0, new List<Vector2> { new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0); // clockwise from above: faces up
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var wear = new GameObject(WearPart);
            wear.transform.SetParent(ice, false);
            wear.AddComponent<MeshFilter>().sharedMesh = mesh;
            var m = new Material(glass) { name = WearPart }; // a copy of the glass asset, so the build keeps the see-through variant
            m.mainTexture = art;
            m.color = Color.white; // the art's alpha alone sets how see-through it is
            m.SetFloat("_Smoothness", WearGloss);
            m.renderQueue = (int)RenderQueue.Transparent - 1;
            var r = wear.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Plain ice: a Unity plane, fitted to Size.</summary>
        static GameObject Placeholder()
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Ice Placeholder";
            RinkBuilder.Paint(plane, new Color(0.85f, 0.93f, 1f));
            return plane;
        }

        static Bounds Bounds(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }
    }
}
