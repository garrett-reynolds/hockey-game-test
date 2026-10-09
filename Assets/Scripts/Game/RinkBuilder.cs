using UnityEngine;

namespace HockeyGame
{
    /// <summary>Builds the flat rink, the net around the Net Face, a light and the fixed camera.</summary>
    public static class RinkBuilder
    {
        const float W = (float)NetFaceMapping.WidthMetres;
        const float H = (float)NetFaceMapping.HeightMetres;
        /// <summary>How deep the net goes behind the Net Face (metres).</summary>
        internal const float NetDepth = 0.6f;

        public static void Build(out Camera camera)
        {
            var root = new GameObject("Rink").transform;

            var ice = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ice.name = "Ice";
            ice.transform.SetParent(root);
            ice.transform.localScale = new Vector3(6f, 1f, 6f); // 60 m: the skate-in starts 22 m out
            Paint(ice, new Color(0.85f, 0.93f, 1f));

            NetBuilder.Build(root); // the net around the Net Face, back at z = +NetDepth

            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.SetParent(root);
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var cameraObject = new GameObject("Shooter Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.12f, 0.16f);
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
