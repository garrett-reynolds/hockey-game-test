using System;
using System.Linq;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Builds the net around the Net Face: the model in Assets/Resources/Models/net.*, or today's box net when there is none.
    /// The Net Face is the opening at z = 0, centred on x = 0, spanning y = 0..H. The posts and crossbar stand just outside it,
    /// centred on z = 0, and the back of the net is at z = +NetDepth.
    /// </summary>
    public static class NetBuilder
    {
        const float W = (float)NetFaceMapping.WidthMetres;
        const float H = (float)NetFaceMapping.HeightMetres;
        /// <summary>Thickness of the posts and crossbar (metres). The opening is inside them.</summary>
        public const float Post = (float)Core.NetFrame.ThicknessMetres;

        /// <summary>Name of the part that shakes on a goal (the netting).</summary>
        public const string NettingName = "Netting";

        /// <summary>
        /// Adds the posts and crossbar to what a puck can hit, as round tubes Post across, centred on z = 0 just outside the
        /// Net Face (where Build fits them). Pieces "Left Post", "Right Post", "Crossbar".
        /// </summary>
        public static void AddFrame(PuckObstacles obstacles)
        {
            float x = W / 2f + Post / 2f, top = H + Post / 2f;
            obstacles.AddTube(new Vector3(-x, 0f, 0f), new Vector3(-x, top, 0f), Post / 2f, PuckSurface.Frame, "Left Post");
            obstacles.AddTube(new Vector3(x, 0f, 0f), new Vector3(x, top, 0f), Post / 2f, PuckSurface.Frame, "Right Post");
            obstacles.AddTube(new Vector3(-x, top, 0f), new Vector3(x, top, 0f), Post / 2f, PuckSurface.Frame, "Crossbar");
        }

        /// <summary>A new "Net" object under the rink. findFile is for tests; see ModelFiles.Load.</summary>
        public static GameObject Build(Transform rink, Func<string, GameObject> findFile = null)
        {
            var net = new GameObject("Net");
            net.transform.SetParent(rink, false);
            GameObject model = ModelFiles.Place(net.transform, ModelFiles.Net, PlaceholderNet, findFile, null);
            Fit(model.transform);
            return net;
        }

        /// <summary>
        /// Fits the model so its posts and crossbar (LeftPost, RightPost, Crossbar) stand just outside the Net Face:
        /// their outer box is the posts' outer edges by the crossbar top, from the ice. The depth runs from the posts' front
        /// to the back of the whole model at the net depth. Anything wider (a base flaring out past the posts) keeps its
        /// shape. A model with no such parts is fitted by its whole outer box instead.
        /// </summary>
        static void Fit(Transform model)
        {
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one;
            model.localPosition = Vector3.zero;
            if (!ModelFiles.TryMeshBounds(model, null, out Bounds all)) return;
            if (!ModelFiles.TryMeshBounds(model, f => IsFrame(f.transform, model), out Bounds frame)) frame = all;
            float depth = RinkBuilder.NetDepth;
            var scale = new Vector3(
                (W + 2f * Post) / frame.size.x,
                (H + Post) / frame.size.y,
                (depth + Post / 2f) / (all.max.z - frame.min.z));
            model.localScale = scale;
            model.localPosition = new Vector3(-frame.center.x * scale.x, -frame.min.y * scale.y, -Post / 2f - frame.min.z * scale.z);
        }

        static bool IsFrame(Transform part, Transform model)
        {
            for (Transform t = part; t != null && t != model; t = t.parent)
                if (t.name == "LeftPost" || t.name == "RightPost" || t.name == "Crossbar") return true;
            return false;
        }

        /// <summary>The part of the net under the rink that shakes on a goal, or null.</summary>
        public static Transform FindNetting(Transform rink) =>
            rink.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == NettingName);

        /// <summary>Today's box net: red posts and crossbar, white box netting.</summary>
        static GameObject PlaceholderNet()
        {
            var root = new GameObject("Net Placeholder").transform;
            var white = new Color(0.9f, 0.9f, 0.9f);
            Box(root, "LeftPost", new Vector3(-W / 2f - Post / 2f, (H + Post) / 2f, 0f), new Vector3(Post, H + Post, Post), Color.red);
            Box(root, "RightPost", new Vector3(W / 2f + Post / 2f, (H + Post) / 2f, 0f), new Vector3(Post, H + Post, Post), Color.red);
            Box(root, "Crossbar", new Vector3(0f, H + Post / 2f, 0f), new Vector3(W + 2f * Post, Post, Post), Color.red);
            var netting = new GameObject(NettingName).transform;
            netting.SetParent(root, false);
            float depth = RinkBuilder.NetDepth;
            Box(netting, "Back", new Vector3(0f, H / 2f, depth - 0.015f), new Vector3(W, H, 0.03f), white);
            Box(netting, "Left Side", new Vector3(-W / 2f, H / 2f, depth / 2f), new Vector3(0.03f, H, depth), white);
            Box(netting, "Right Side", new Vector3(W / 2f, H / 2f, depth / 2f), new Vector3(0.03f, H, depth), white);
            Box(netting, "Roof", new Vector3(0f, H, depth / 2f), new Vector3(W, 0.03f, depth), white);
            return root.gameObject;
        }

        static void Box(Transform parent, string name, Vector3 localPosition, Vector3 scale, Color colour) =>
            RinkBuilder.Cube(parent, name, parent.TransformPoint(localPosition), scale, colour);
    }
}
