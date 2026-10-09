using System;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The ice sheet: a square centred on the Net Face (x = 0, z = 0) with its top at y = 0, wide and long enough
    /// for the 22 m skate-in from every Attack Side. Uses the ice model (Assets/Resources/Models/ice.obj, textured with
    /// the rink lines and crease) when there is one, otherwise today's plain pale-blue plane.
    /// </summary>
    public static class RinkIce
    {
        /// <summary>Side of the ice square (metres).</summary>
        public const float Size = 60f;

        /// <summary>Height an ice model with some thickness is fitted to (metres). The flat ice sheet keeps no height.</summary>
        const float Thickness = 0.05f;

        /// <summary>Adds the ice under rink. findFile is for tests; by default the model comes from Assets/Resources/Models.</summary>
        public static GameObject Build(Transform rink, Func<string, GameObject> findFile = null)
        {
            var ice = new GameObject("Ice");
            ice.transform.SetParent(rink, false);

            GameObject model = ModelFiles.Load(ModelFiles.Ice, Placeholder, findFile);
            model.transform.SetParent(ice.transform, false);
            ModelFiles.FitTo(model.transform, new Vector3(Size, Thickness, Size));
            // FitTo centres the model on y = 0; a model with thickness is lifted so its top is at y = 0.
            float top = Bounds(model).max.y - ice.transform.position.y;
            model.transform.localPosition -= Vector3.up * top;
            return ice;
        }

        /// <summary>Today's plain ice: a 60 m Unity plane.</summary>
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
