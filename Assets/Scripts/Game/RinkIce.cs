using System;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The ice sheet: a square centred on the centre line (x = 0, z = RinkLayout.IceCentreZ) with its top at y = 0, past
    /// the boards at both ends of the full rink. Uses the ice model (Assets/Resources/Models/ice.obj, textured with the rink
    /// lines and creases) when there is one, otherwise a plain pale-blue plane.
    /// </summary>
    public static class RinkIce
    {
        /// <summary>Side of the ice square (metres).</summary>
        public const float Size = (float)RinkLayout.IceSize;
        /// <summary>z of the middle of the ice square: the centre line (metres).</summary>
        public const float CentreZ = (float)RinkLayout.IceCentreZ;

        /// <summary>Height an ice model with some thickness is fitted to (metres). The flat ice sheet keeps no height.</summary>
        const float Thickness = 0.05f;

        /// <summary>URP Lit smoothness of the ice: glossy, so it mirrors the light banks, boards and net (ArenaLighting's reflection probe).</summary>
        public const float Gloss = 0.8f;
        /// <summary>
        /// Tint on the ice texture: a touch below pure white and cool, so the lit ice stays under the tonemapper's
        /// white and the highlights from the light banks still show on it.
        /// </summary>
        static readonly Color Tint = new Color(0.8f, 0.85f, 0.9f);

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

            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                var m = new Material(r.sharedMaterial); // never change the imported model's material asset
                m.SetFloat("_Smoothness", Gloss);
                m.color *= Tint;
                r.sharedMaterial = m;
            }
            return ice;
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
