using System;
using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// The arena round the whole rink: stands full of fans, the empty benches and penalty boxes, a Ribbon Board and a
    /// roof, from the arena model (Assets/Resources/Models/arena.obj) when there is one, so the shooter never sees the edge
    /// of the world.
    /// With no model the dark backdrop stays as it was. The arena casts no shadows (its roof would shade the ice), and
    /// the model's part named "Ribbon" glows in its own colour. The seats (the "Risers" part) take the Team Skin's colour.
    /// The other parts are dimmed, so the lit ice pops; the seats too.
    /// </summary>
    public static class ArenaStands
    {
        /// <summary>How far the arena reaches past the boards' outer box on every side (metres).</summary>
        public const float Reach = 31f;
        /// <summary>Height of the roof above the ice (metres), from the Rink Layout.</summary>
        public const float RoofHeight = (float)RinkLayout.RoofHeight;
        /// <summary>Name of the glowing Ribbon Board part in the arena model.</summary>
        public const string GlowPart = "Ribbon";
        /// <summary>Name of the seats part in the arena model (the stepped treads the 3D fans sit on).</summary>
        public const string SeatsPart = "Risers";
        /// <summary>How much of its own colour each non-glowing part shows: the stands sit in the dark, the ice is lit.</summary>
        public const float StandsLight = 0.4f;

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

        /// <summary>Adds the arena under rink, with the skin's seats (null: the League Skin's). findFile is for tests; by
        /// default the model comes from Assets/Resources/Models.</summary>
        public static GameObject Build(Transform rink, Func<string, GameObject> findFile = null, TeamSkin skin = null)
        {
            skin = TeamSkin.OrLeague(skin);
            var arena = new GameObject("Arena");
            arena.transform.SetParent(rink, false);
            arena.transform.localPosition = Box.center;

            GameObject model = ModelFiles.Place(arena.transform, ModelFiles.Arena, () => new GameObject("No Arena Model"), findFile, Box.size);

            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                if (r.name == GlowPart) Glow(r);
                else
                {
                    if (r.name == SeatsPart) RinkBuilder.Recolour(r.gameObject, skin.Seats);
                    Dim(r);
                }
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
