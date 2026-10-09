using System;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame
{
    /// <summary>
    /// The red goal light above the net: the model in Assets/Resources/Models/goal-light.*, or today's box light when there is none.
    /// On a goal its lamp (the model part named "Dome", or the box) glows red and a red point light shines on the net.
    /// </summary>
    public sealed class GoalLight
    {
        const float H = (float)NetFaceMapping.HeightMetres;
        /// <summary>Name of the model part that lights up. A model with no such part lights its first part.</summary>
        public const string LampName = "Dome";
        static readonly Vector3 Size = new Vector3(0.26f, 0.31f, 0.26f);
        static readonly Color Off = new Color(0.25f, 0.05f, 0.05f);
        static readonly Color On = new Color(1f, 0.1f, 0.1f);

        readonly Material _lamp;
        readonly Light _glow;

        /// <summary>Builds the goal light under the rink, off. findFile is for tests; see ModelFiles.Load.</summary>
        public GoalLight(Transform rink, Func<string, GameObject> findFile = null)
        {
            var root = new GameObject("Goal Light").transform;
            root.SetParent(rink, false);
            root.localPosition = new Vector3(0f, H + 0.3f, RinkBuilder.NetDepth / 2f);

            GameObject model = ModelFiles.Load(ModelFiles.GoalLight, PlaceholderLight, findFile);
            model.transform.SetParent(root, false);
            ModelFiles.FitTo(model.transform, Size);
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) DestroyNow(c);

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            Renderer lamp = renderers.FirstOrDefault(r => r.name == LampName) ?? renderers.FirstOrDefault();
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (lamp != null)
            {
                _lamp = unlit != null ? new Material(unlit) : new Material(lamp.sharedMaterial);
                lamp.sharedMaterial = _lamp;
            }

            _glow = new GameObject("Goal Light Glow").AddComponent<Light>();
            _glow.transform.SetParent(root, false);
            _glow.transform.localPosition = new Vector3(0f, 0f, -0.4f);
            _glow.type = LightType.Point;
            _glow.color = On;
            _glow.range = 4f;
            _glow.intensity = 3f;
            SetOn(false);
        }

        public void SetOn(bool on)
        {
            if (_lamp != null) _lamp.color = on ? On : Off;
            _glow.enabled = on;
        }

        /// <summary>Today's box light, used when Assets/Resources/Models has no goal light model.</summary>
        static GameObject PlaceholderLight()
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Goal Light Placeholder";
            return box;
        }

        static void DestroyNow(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
