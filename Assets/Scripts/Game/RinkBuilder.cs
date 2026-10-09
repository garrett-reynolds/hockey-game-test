using UnityEngine;

namespace HockeyGame
{
    /// <summary>Builds the ice, the box net around the Net Face, the arena lights and the fixed camera.</summary>
    public static class RinkBuilder
    {
        const float W = (float)NetFaceMapping.WidthMetres;
        const float H = (float)NetFaceMapping.HeightMetres;
        const float Post = 0.08f;
        /// <summary>How deep the net goes behind the Net Face (metres).</summary>
        internal const float NetDepth = 0.6f;

        public static void Build(out Camera camera)
        {
            var root = new GameObject("Rink").transform;

            RinkIce.Build(root);

            // Net: the Net Face is the opening at z = 0, centred on x = 0, spanning y = 0..H. The back of the net is at z = +NetDepth.
            var white = new Color(0.9f, 0.9f, 0.9f);
            Cube(root, "Left Post", new Vector3(-W / 2f - Post / 2f, H / 2f, 0f), new Vector3(Post, H + Post, Post), Color.red);
            Cube(root, "Right Post", new Vector3(W / 2f + Post / 2f, H / 2f, 0f), new Vector3(Post, H + Post, Post), Color.red);
            Cube(root, "Crossbar", new Vector3(0f, H + Post / 2f, 0f), new Vector3(W + 2f * Post, Post, Post), Color.red);
            Cube(root, "Back", new Vector3(0f, H / 2f, NetDepth), new Vector3(W, H, 0.03f), white);
            Cube(root, "Left Side", new Vector3(-W / 2f, H / 2f, NetDepth / 2f), new Vector3(0.03f, H, NetDepth), white);
            Cube(root, "Right Side", new Vector3(W / 2f, H / 2f, NetDepth / 2f), new Vector3(0.03f, H, NetDepth), white);
            Cube(root, "Roof", new Vector3(0f, H, NetDepth / 2f), new Vector3(W, 0.03f, NetDepth), white);

            ArenaLighting.Build(root);

            var cameraObject = new GameObject("Shooter Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ArenaLighting.Backdrop;
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
            Object.Destroy(cube.GetComponent<Collider>());
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
