using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// The arena round the boards: stands full of fans, an LED ribbon board and a roof, from the arena model
    /// (Assets/Resources/Models/arena.obj) when there is one, so the shooter never sees the edge of the world.
    /// With no model the dark backdrop stays as it was. The arena casts no shadows (its roof would shade the ice), and
    /// the model's part named "Ribbon" glows in its own colour. The other parts are dimmed, so the lit ice pops.
    /// </summary>
    public static class ArenaStands
    {
        /// <summary>How far the arena reaches past the boards' outer box on every side (metres).</summary>
        public const float Reach = 31f;
        /// <summary>Height of the roof above the ice (metres).</summary>
        public const float RoofHeight = 24f;
        /// <summary>Name of the glowing LED ribbon board part in the arena model.</summary>
        public const string GlowPart = "Ribbon";
        /// <summary>How much of its own colour each non-glowing part shows: the stands sit in the dark, the ice is lit.</summary>
        const float StandsLight = 0.4f;

        /// <summary>
        /// The model's outer box in rink space: the boards' box plus Reach all round, from the ice to the roof. The sizes are
        /// copied by hand in Tools/make_arena.py: change both together.
        /// </summary>
        public static Bounds Box
        {
            get
            {
                Bounds boards = RinkBoards.Box;
                var min = new Vector3(boards.min.x - Reach, 0f, boards.min.z - Reach);
                var max = new Vector3(boards.max.x + Reach, RoofHeight, boards.max.z + Reach);
                return new Bounds((min + max) / 2f, max - min);
            }
        }

        /// <summary>Adds the arena under rink. findFile is for tests; by default the model comes from Assets/Resources/Models.</summary>
        public static GameObject Build(Transform rink, Func<string, GameObject> findFile = null)
        {
            var arena = new GameObject("Arena");
            arena.transform.SetParent(rink, false);
            arena.transform.localPosition = Box.center;

            GameObject model = ModelFiles.Place(arena.transform, ModelFiles.Arena, () => new GameObject("No Arena Model"), findFile, Box.size);

            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                if (r.name == GlowPart) Glow(r);
                else Dim(r);
            }
            return arena;
        }

        static void Dim(Renderer renderer)
        {
            var m = new Material(renderer.sharedMaterial);
            Color c = m.color;
            m.color = new Color(c.r * StandsLight, c.g * StandsLight, c.b * StandsLight, c.a);
            renderer.sharedMaterial = m;
        }

        static void Glow(Renderer renderer)
        {
            Color colour = renderer.sharedMaterial.color;
            renderer.sharedMaterial = ArenaLighting.Glow(colour, colour);
        }
    }
}
