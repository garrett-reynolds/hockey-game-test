using System;
using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// The boards and glass around the whole rink, from the boards model (Assets/Resources/Models/boards.obj) when there is
    /// one. With no model the rink stays open, as it was before. Sizes from the Rink Layout: NHL 200 x 85 ft, end boards
    /// 11 ft behind each goal line; the near goal line is the Net Face plane and the far end boards are behind the shooter.
    /// The model's parts named "Glass" are made see-through here (OBJ materials import opaque).
    /// </summary>
    public static class RinkBoards
    {
        /// <summary>Inside face of the side boards: x = ±HalfWidth (metres).</summary>
        public const float HalfWidth = (float)RinkLayout.HalfWidth;
        /// <summary>Inside face of the end boards behind the near net (metres).</summary>
        public const float EndZ = (float)RinkLayout.NearEndBoardsZ;
        /// <summary>Inside face of the far end boards, behind the shooter (metres).</summary>
        public const float FarZ = (float)RinkLayout.FarEndBoardsZ;
        /// <summary>Thickness of the boards (metres); the model's outer box is the inside outline plus this all round.</summary>
        public const float Thickness = 0.2f;
        /// <summary>Top of the highest glass, behind the net (metres above the ice).</summary>
        public const float GlassTop = (float)RinkLayout.EndGlassTop;

        /// <summary>Name of the see-through parts in the boards model.</summary>
        public const string GlassPart = "Glass";

        const float GlassAlpha = 0.15f;
        /// <summary>Share of its speed into the boards a puck keeps coming back off them.</summary>
        const float BoardsBounce = 0.3f;

        /// <summary>
        /// The model's outer box in rink space: the boards' outside faces, from the ice to the top of the glass. The sizes are
        /// copied by hand in Tools/make_arena.py: change both together.
        /// </summary>
        public static Bounds Box
        {
            get
            {
                var min = new Vector3(-HalfWidth - Thickness, 0f, FarZ - Thickness);
                var max = new Vector3(HalfWidth + Thickness, GlassTop, EndZ + Thickness);
                return new Bounds((min + max) / 2f, max - min);
            }
        }

        /// <summary>Adds the boards under rink. findFile is for tests; by default the model comes from Assets/Resources/Models.</summary>
        public static GameObject Build(Transform rink, Func<string, GameObject> findFile = null)
        {
            var boards = new GameObject("Boards");
            boards.transform.SetParent(rink, false);
            boards.transform.localPosition = Box.center;

            GameObject model = ModelFiles.Place(boards.transform, ModelFiles.Boards, () => new GameObject("No Boards Model"), findFile, Box.size);

            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                if (r.name == GlassPart) MakeGlass(r);
            return boards;
        }

        /// <summary>True when the rink got the boards model (not the empty stand-in), so pucks can hit them.</summary>
        public static bool Present(Transform rink)
        {
            Transform boards = rink.Find("Boards");
            return boards != null && boards.GetComponentInChildren<Renderer>() != null;
        }

        /// <summary>A puck (of this radius) that has gone into the end boards behind the net comes back off them, slower.</summary>
        public static void BounceOffEndBoards(ref Vector3 position, ref Vector3 velocity, float radius)
        {
            float face = EndZ - radius;
            if (position.z <= face || position.y > GlassTop || velocity.z <= 0f) return; // short of them, or over the glass
            position.z = face;
            velocity.z = -velocity.z * BoardsBounce;
        }

        static void MakeGlass(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            // A copy of the glass asset (transparent URP Lit), so the player build keeps the transparent shader variant.
            var m = new Material(Resources.Load<Material>(ArenaLighting.AssetFolder + "/" + ArenaLighting.GlassMaterial));
            Color c = renderer.sharedMaterial.color;
            c.a = GlassAlpha;
            m.color = c;
            renderer.sharedMaterial = m;
        }
    }
}
