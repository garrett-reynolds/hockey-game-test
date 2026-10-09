using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame
{
    /// <summary>
    /// Finds each visual piece's model. To use a real model, drop a model file (.fbx or .obj) into
    /// Assets/Resources/Models/ named after the piece below, for example Assets/Resources/Models/puck.obj.
    /// A piece with no file uses its code-made placeholder (today's simple shape). No code change is needed.
    /// File names: ice, net, puck, goalie-dressing, boards, arena, goal-light, save-glove.
    /// </summary>
    public static class ModelFiles
    {
        /// <summary>Folder under Assets/Resources that holds the model files.</summary>
        public const string Folder = "Models";

        public const string Ice = "ice";
        public const string Net = "net";
        public const string Puck = "puck";
        public const string GoalieDressing = "goalie-dressing";
        public const string Boards = "boards";
        public const string Arena = "arena";
        public const string GoalLight = "goal-light";
        public const string SaveGlove = "save-glove";

        /// <summary>
        /// A new scene object of the model in the file with this name (no extension), or the placeholder when there is none.
        /// findFile looks a name up and returns a scene object; by default it makes a copy of the model in Assets/Resources/Models.
        /// </summary>
        public static GameObject Load(string fileName, Func<GameObject> placeholder, Func<string, GameObject> findFile = null)
        {
            GameObject model = (findFile ?? FromResources)(fileName);
            return model != null ? model : placeholder(); // Unity null check: no ??
        }

        /// <summary>
        /// Scales and moves the model inside its parent so all its meshes together fill exactly size (parent units)
        /// and sit centred on the parent's origin, unrotated. Each axis is scaled on its own.
        /// </summary>
        public static void FitTo(Transform model, Vector3 size)
        {
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one;
            model.localPosition = Vector3.zero;
            Transform parent = model.parent;
            Matrix4x4 toParent = parent != null ? parent.worldToLocalMatrix : Matrix4x4.identity;

            bool any = false;
            var bounds = new Bounds();
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;
                Matrix4x4 m = toParent * filter.transform.localToWorldMatrix;
                Bounds b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            if (!any) return;

            Vector3 have = bounds.size;
            var scale = new Vector3(
                have.x > 0f ? size.x / have.x : 1f,
                have.y > 0f ? size.y / have.y : 1f,
                have.z > 0f ? size.z / have.z : 1f);
            model.localScale = scale;
            model.localPosition = -Vector3.Scale(bounds.center, scale);
        }

        static GameObject FromResources(string fileName)
        {
            var prefab = Resources.Load<GameObject>(Folder + "/" + fileName);
            if (prefab == null) return null;
            GameObject model = Object.Instantiate(prefab);
            model.name = prefab.name;
            return model;
        }
    }
}
