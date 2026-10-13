using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HockeyGame
{
    /// <summary>
    /// Builds the full rink (ice, boards, ads, arena, 3D crowd), the net around the Net Face and the still far net, the arena
    /// lights and the fixed camera, in a Team Skin's look (null: the League Skin's). Returns the rink's root ("Rink").
    /// </summary>
    public static class RinkBuilder
    {
        const float W = (float)NetFaceMapping.WidthMetres;
        const float H = (float)NetFaceMapping.HeightMetres;
        const float Post = 0.08f;
        /// <summary>How deep the net goes behind the Net Face (metres). Tools/make_supplied_net.py copies it (DEPTH).</summary>
        internal const float NetDepth = 0.6f;

        public static Transform Build(out Camera camera, TeamSkin skin = null)
        {
            skin = TeamSkin.Guard(skin); // the Calm Area rules: no reserved colours from a skin
            var root = new GameObject("Rink").transform;

            RinkIce.Build(root);
            RinkBoards.Build(root, skin: skin);   // boards and glass round the rink (none without the model)
            RinkAds.Build(root, skin);            // board ads and ice logos
            ArenaStands.Build(root, skin: skin);  // stands and roof round the boards (none without the model)
            RibbonBoards.Build(root, skin);       // on the stands' ribbon ring; they follow the Arena Show
            RoofBanners.Build(root, skin);        // the skin's banners round the near end, beside the Video Board (#149)
            Crowd.Build(root, skin);              // the 3D crowd in every seat (none without the body files); it follows the Arena Show

            NetBuilder.Build(root); // the net around the Net Face, back at z = +NetDepth
            NetBuilder.BuildFar(root); // the still far net, no goalie

            ArenaLighting.Build(root);
            // After the lights: the reflection cubemap is rendered once, so a board in it would shine a frozen picture
            // into the ice behind the net (the Calm Area keeps no ice shine).
            VideoBoards.Build(root, skin); // the Video Board behind the near net and the Centre Board; they follow the Arena Show

            var cameraObject = new GameObject("Shooter Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ArenaLighting.Backdrop;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true; // the arena's tonemapping, bloom and vignette
            cameraObject.transform.position = new Vector3(0f, 1.5f, -9f);
            cameraObject.transform.LookAt(new Vector3(0f, H / 2f, 0f));
            return root;
        }

        internal static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 scale, Color colour)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            SceneObjects.DestroySafely(cube.GetComponent<Collider>());
            Paint(cube, colour);
            return cube;
        }

        /// <summary>Paints target in colour unless it already shows it (Color ==, near enough): the League Skin's team
        /// colours are the models' own, so the League look keeps the imported materials untouched.</summary>
        internal static void Recolour(GameObject target, Color colour)
        {
            if (target.GetComponent<Renderer>().sharedMaterial.color != colour) Paint(target, colour);
        }

        internal static void Paint(GameObject target, Color colour)
        {
            var renderer = target.GetComponent<Renderer>();
            var material = new Material(renderer.sharedMaterial);
            material.color = colour; // maps to _BaseColor in URP Lit
            renderer.sharedMaterial = material;
        }
    }
}
