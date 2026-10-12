using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// What the physics puck can hit, with the "C Dead" feel (#103): the ice, the posts and crossbar, and the hidden catch boxes
    /// behind the netting (#102), which stop the puck in the net (the netting itself is looks only). Hidden, static colliders.
    /// No boards or glass yet (#110) and no goalie hit shapes (#111).
    /// </summary>
    public static class ShotColliders
    {
        public const string RootName = "Shot Colliders";
        public const string IceName = "Ice Collider";

        const float W = (float)NetFaceMapping.WidthMetres;
        const float H = (float)NetFaceMapping.HeightMetres;
        const float Post = NetBuilder.Post;
        /// <summary>How deep the netting goes behind the Net Face (metres), as RinkBuilder.NetDepth.</summary>
        public const float NetDepth = RinkBuilder.NetDepth;

        /// <summary>How far behind the netting's back, sides and roof the catch boxes stand (metres).</summary>
        public const float CatchGap = 0.04f;
        /// <summary>How thick each catch box is (metres): thick enough that no fast puck skips through it in one step.</summary>
        public const float CatchThickness = 0.6f;

        // Static friction is a little over the grip (dynamic friction), as in the prototypes.
        static PhysicsMaterial _ice, _frame, _catch;

        /// <summary>Ice: grip 0.08, no bounce.</summary>
        public static PhysicsMaterial Ice => _ice ??= new PhysicsMaterial("Ice")
        {
            bounciness = 0f, dynamicFriction = 0.08f, staticFriction = 0.08f * 1.2f,
            frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum,
        };

        /// <summary>Posts and crossbar ("C Dead"): bounce 0.4, grip 0.3. A wall: its own bounce wins (Maximum), its own grip too (Minimum).</summary>
        public static PhysicsMaterial Frame => _frame ??= new PhysicsMaterial("Posts and Crossbar")
        {
            bounciness = 0.4f, dynamicFriction = 0.3f, staticFriction = 0.3f * 1.2f,
            frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Maximum,
        };

        /// <summary>The catch: no bounce (Minimum beats the puck's), grip 0.6, so the puck dies in the net.</summary>
        public static PhysicsMaterial Catch => _catch ??= new PhysicsMaterial("Net Catch")
        {
            bounciness = 0f, dynamicFriction = 0.6f, staticFriction = 0.6f * 1.2f,
            frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum,
        };

        /// <summary>Adds the colliders under parent, in a new RootName object. Returns that root.</summary>
        public static GameObject Build(Transform parent)
        {
            var root = new GameObject(RootName);
            root.transform.SetParent(parent, false);

            // The ice: a thick slab with its top at y = 0, as wide as the ice sheet.
            Box(root, IceName, new Vector3(0f, -0.5f, 0f), new Vector3(RinkIce.Size, 1f, RinkIce.Size), Ice);

            // Posts and crossbar: round tubes Post across, centred on z = 0 just outside the Net Face (as NetBuilder fits them).
            float x = W / 2f + Post / 2f, top = H + Post / 2f;
            Tube(root, "Left Post", new Vector3(-x, 0f, 0f), new Vector3(-x, top, 0f));
            Tube(root, "Right Post", new Vector3(x, 0f, 0f), new Vector3(x, top, 0f));
            Tube(root, "Crossbar", new Vector3(-x, top, 0f), new Vector3(x, top, 0f));

            // The catch: CatchGap behind the netting's sides, roof and back. The sides and roof start at the Net Face (z = 0),
            // so they never touch a puck still on its way in, and run to the back of the back box.
            float g = CatchGap, t = CatchThickness, outer = W / 2f + g, depth = NetDepth + g + t;
            Box(root, "Catch Left", new Vector3(-outer - t / 2f, (H + g) / 2f, depth / 2f), new Vector3(t, H + g + 0.2f, depth), Catch);
            Box(root, "Catch Right", new Vector3(outer + t / 2f, (H + g) / 2f, depth / 2f), new Vector3(t, H + g + 0.2f, depth), Catch);
            Box(root, "Catch Roof", new Vector3(0f, H + g + t / 2f, depth / 2f), new Vector3(2f * outer + 2f * t, t, depth), Catch);
            Box(root, "Catch Back", new Vector3(0f, (H + g) / 2f, NetDepth + g + t / 2f), new Vector3(2f * outer, H + g + 0.2f, t), Catch);
            return root;
        }

        /// <summary>Inside the catch: past the Net Face, between the catch's sides, under its roof and in front of its back.</summary>
        public static bool InsideCatch(Vector3 p) =>
            p.z > 0f && p.z < NetDepth + CatchGap && Mathf.Abs(p.x) < W / 2f + CatchGap && p.y < H + CatchGap;

        static void Box(GameObject root, string name, Vector3 centre, Vector3 size, PhysicsMaterial material)
        {
            var box = new GameObject(name);
            box.transform.SetParent(root.transform, false);
            box.transform.localPosition = centre;
            var collider = box.AddComponent<BoxCollider>();
            collider.size = size;
            collider.sharedMaterial = material;
        }

        static void Tube(GameObject root, string name, Vector3 a, Vector3 b)
        {
            var tube = new GameObject(name);
            tube.transform.SetParent(root.transform, false);
            tube.transform.localPosition = (a + b) / 2f;
            tube.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            var capsule = tube.AddComponent<CapsuleCollider>();
            capsule.radius = Post / 2f;
            capsule.height = (b - a).magnitude + Post;
            capsule.direction = 1;
            capsule.sharedMaterial = Frame;
        }
    }
}
