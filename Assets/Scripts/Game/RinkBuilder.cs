using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HockeyGame
{
    /// <summary>Builds the full rink (ice, boards, ads, arena, 3D crowd), the net around the Net Face and the still far net, the arena lights and the fixed camera.</summary>
    public static class RinkBuilder
    {
        const float W = (float)NetFaceMapping.WidthMetres;
        const float H = (float)NetFaceMapping.HeightMetres;
        const float Post = 0.08f;
        /// <summary>How deep the net goes behind the Net Face (metres). Tools/make_supplied_net.py copies it (DEPTH).</summary>
        internal const float NetDepth = 0.6f;

        public static void Build(out Camera camera)
        {
            var root = new GameObject("Rink").transform;

            RinkIce.Build(root);
            RinkBoards.Build(root);   // boards and glass round the rink (none without the model)
            RinkAds.Build(root);      // board ads and ice logos
            ArenaStands.Build(root);  // stands and roof round the boards (none without the model)
            RibbonBoards.Build(root); // on the stands' ribbon ring; they follow the Arena Show
            Crowd.Build(root);        // the 3D fans behind the near net (none without the body file); they follow the Arena Show

            NetBuilder.Build(root); // the net around the Net Face, back at z = +NetDepth
            NetBuilder.BuildFar(root); // the still far net, no goalie

            ArenaLighting.Build(root);

            var cameraObject = new GameObject("Shooter Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ArenaLighting.Backdrop;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true; // the arena's tonemapping, bloom and vignette
            cameraObject.transform.position = new Vector3(0f, 1.5f, -9f);
            cameraObject.transform.LookAt(new Vector3(0f, H / 2f, 0f));
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

        internal static void Paint(GameObject target, Color colour)
        {
            var renderer = target.GetComponent<Renderer>();
            var material = new Material(renderer.sharedMaterial);
            material.color = colour; // maps to _BaseColor in URP Lit
            renderer.sharedMaterial = material;
        }
    }
}
