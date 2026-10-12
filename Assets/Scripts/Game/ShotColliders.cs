using System;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// What the physics puck can hit, with the "C Dead" feel (#103): the ice, the posts and crossbar, the boards and glass all
    /// round the rink (#110), and the hidden catch boxes behind the netting (#102), which stop the puck in the net (the
    /// netting itself is looks only). Hidden, static colliders. The goalie's gear has its materials here too (#111), told
    /// apart by feel group; its hit shapes move, so GoalieHitShapes builds them.
    /// </summary>
    public static class ShotColliders
    {
        public const string RootName = "Shot Colliders";
        public const string IceName = "Ice Collider";
        public const string BoardsName = "Boards Collider", GlassName = "Glass Collider";

        /// <summary>How thick the board and glass walls are (metres): their inside face is the rink's boards line, they stand out from it.</summary>
        public const float WallThickness = 0.4f;
        /// <summary>How many straight pieces make each corner arc (a corner is 8.53 m in radius: a piece is 0.84 m long, 1 cm off the arc).</summary>
        public const int CornerSegments = 16;
        /// <summary>How far each wall piece runs past its ends (metres), so neighbouring pieces overlap and leave no crack.</summary>
        const float WallOverlap = 0.05f;

        const float W = (float)NetFaceMapping.WidthMetres;
        const float H = (float)NetFaceMapping.HeightMetres;
        const float Post = NetBuilder.Post;
        /// <summary>How deep the netting goes behind the Net Face (metres), as RinkBuilder.NetDepth.</summary>
        public const float NetDepth = RinkBuilder.NetDepth;

        /// <summary>How far behind the netting's back, sides and roof the catch boxes stand (metres).</summary>
        public const float CatchGap = 0.04f;
        /// <summary>How thick the catch's back box is (metres): thick enough that no fast puck skips through it in one step.</summary>
        public const float CatchThickness = 0.6f;
        /// <summary>
        /// How thick the catch's side and roof boxes are (metres): only out to the posts' and crossbar's outer edge, so from in
        /// front they hide behind the frame and a Shot just wide or just high passes the net as it would a real one.
        /// </summary>
        public const float CatchSideThickness = Post - CatchGap;

        // Static friction is a little over the grip (dynamic friction), as in the prototypes. Made once and kept: never
        // unloaded with unused assets (a Unity null check would not see an unloaded one through ??=).
        static PhysicsMaterial _ice, _frame, _catch, _boards, _glass, _pads, _stick, _body, _glove;

        /// <summary>Ice: grip 0.08, no bounce.</summary>
        public static PhysicsMaterial Ice => _ice != null ? _ice : _ice = new PhysicsMaterial("Ice")
        {
            hideFlags = HideFlags.DontUnloadUnusedAsset,
            bounciness = 0f, dynamicFriction = 0.08f, staticFriction = 0.08f * 1.2f,
            frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum,
        };

        /// <summary>Posts and crossbar ("C Dead"): bounce 0.4, grip 0.3. A wall: its own bounce wins (Maximum), its own grip too (Minimum).</summary>
        public static PhysicsMaterial Frame => _frame != null ? _frame : _frame = new PhysicsMaterial("Posts and Crossbar")
        {
            hideFlags = HideFlags.DontUnloadUnusedAsset,
            bounciness = 0.4f, dynamicFriction = 0.3f, staticFriction = 0.3f * 1.2f,
            frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Maximum,
        };

        /// <summary>The catch: no bounce (Minimum beats the puck's), grip 0.6, so the puck dies in the net.</summary>
        public static PhysicsMaterial Catch => _catch != null ? _catch : _catch = new PhysicsMaterial("Net Catch")
        {
            hideFlags = HideFlags.DontUnloadUnusedAsset,
            bounciness = 0f, dynamicFriction = 0.6f, staticFriction = 0.6f * 1.2f,
            frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum,
        };

        /// <summary>Boards ("C Dead"): bounce 0.35, grip 0.3. A wall: its own bounce wins (Maximum), its own grip too (Minimum).</summary>
        public static PhysicsMaterial Boards => _boards != null ? _boards : _boards = new PhysicsMaterial("Boards")
        {
            hideFlags = HideFlags.DontUnloadUnusedAsset,
            bounciness = 0.35f, dynamicFriction = 0.3f, staticFriction = 0.3f * 1.2f,
            frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Maximum,
        };

        /// <summary>Glass ("C Dead"): bounce 0.3, grip 0.15, combined as the boards.</summary>
        public static PhysicsMaterial Glass => _glass != null ? _glass : _glass = new PhysicsMaterial("Glass")
        {
            hideFlags = HideFlags.DontUnloadUnusedAsset,
            bounciness = 0.3f, dynamicFriction = 0.15f, staticFriction = 0.15f * 1.2f,
            frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Maximum,
        };

        /// <summary>Pads, blocker and skates (gear feel "C Soak 70%", #105): bounce 0.25, grip 0.5. Gear: its own bounce wins (Maximum), its own grip too (Minimum).</summary>
        public static PhysicsMaterial GearPads => _pads != null ? _pads : _pads = Gear("Goalie Pads and Blocker", 0.25f);

        /// <summary>The goalie's stick: bounce 0.2, grip 0.5.</summary>
        public static PhysicsMaterial GearStick => _stick != null ? _stick : _stick = Gear("Goalie Stick", 0.2f);

        /// <summary>The goalie's body and mask: bounce 0.1, grip 0.5.</summary>
        public static PhysicsMaterial GearBody => _body != null ? _body : _body = Gear("Goalie Body", 0.1f);

        /// <summary>The catching glove: bounce 0.02, grip 0.5. It nearly kills the puck.</summary>
        public static PhysicsMaterial GearGlove => _glove != null ? _glove : _glove = Gear("Goalie Glove", 0.02f);

        static PhysicsMaterial Gear(string name, float bounce) => new PhysicsMaterial(name)
        {
            hideFlags = HideFlags.DontUnloadUnusedAsset,
            bounciness = bounce, dynamicFriction = 0.5f, staticFriction = 0.5f * 1.2f,
            frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Maximum,
        };

        /// <summary>The goalie's gear feel for this piece of gear (GearPiece): the glove for the Catcher, the stick, the pads for the pads, blocker and skates, else the body.</summary>
        public static PhysicsMaterial GearOf(string piece) =>
            piece == GearPiece.Catcher ? GearGlove : piece == GearPiece.Stick ? GearStick
            : piece == GearPiece.LeftPad || piece == GearPiece.RightPad || piece == GearPiece.Blocker
              || piece == GearPiece.LeftSkate || piece == GearPiece.RightSkate ? GearPads : GearBody;

        /// <summary>The material is one of the goalie's gear (a gear touch makes a Save, #111).</summary>
        public static bool IsGear(PhysicsMaterial material) =>
            material != null && (material == _pads || material == _stick || material == _body || material == _glove);

        /// <summary>Adds the colliders under parent, in a new RootName object. Returns that root.</summary>
        public static GameObject Build(Transform parent)
        {
            var root = new GameObject(RootName);
            root.transform.SetParent(parent, false);

            // The ice: a thick slab with its top at y = 0, as big as the ice sheet (centred on the centre line, as RinkIce), so a
            // puck coming off the boards at the far end still has ice under it.
            Box(root, IceName, new Vector3(0f, -0.5f, RinkIce.CentreZ), new Vector3(RinkIce.Size, 1f, RinkIce.Size), Ice);

            // Posts and crossbar: round tubes Post across, centred on z = 0 just outside the Net Face (as NetBuilder fits them).
            float x = W / 2f + Post / 2f, top = H + Post / 2f;
            Tube(root, "Left Post", new Vector3(-x, 0f, 0f), new Vector3(-x, top, 0f));
            Tube(root, "Right Post", new Vector3(x, 0f, 0f), new Vector3(x, top, 0f));
            Tube(root, "Crossbar", new Vector3(-x, top, 0f), new Vector3(x, top, 0f));

            // The catch: CatchGap behind the netting's sides, roof and back. The sides and roof start at the Net Face (z = 0),
            // so they never touch a puck still on its way in, and run to the back of the back box. They are only
            // CatchSideThickness thick, out to the frame's outer edge: no hidden wall stands beside or above the frame.
            float g = CatchGap, s = CatchSideThickness, t = CatchThickness, outer = W / 2f + g, depth = NetDepth + g + t;
            const float below = 0.1f; // into the ice, so no puck slips under
            float catchTop = H + g + s, sideHeight = catchTop + below;
            Box(root, "Catch Left", new Vector3(-outer - s / 2f, catchTop - sideHeight / 2f, depth / 2f), new Vector3(s, sideHeight, depth), Catch);
            Box(root, "Catch Right", new Vector3(outer + s / 2f, catchTop - sideHeight / 2f, depth / 2f), new Vector3(s, sideHeight, depth), Catch);
            Box(root, "Catch Roof", new Vector3(0f, H + g + s / 2f, depth / 2f), new Vector3(2f * (outer + s), s, depth), Catch);
            Box(root, "Catch Back", new Vector3(0f, catchTop - sideHeight / 2f, NetDepth + g + t / 2f), new Vector3(2f * (outer + s), sideHeight, t), Catch);

            Walls(root);
            return root;
        }

        /// <summary>
        /// The boards (ice to BoardHeight) and the glass above them (to EndGlassTop round the ends and corners, SideGlassTop along
        /// the sides), all round the inside face of the boards (RinkLayout.OnBoards): the straight runs one box each, the corners
        /// CornerSegments boxes each. Each box is WallThickness thick, standing out from the inside face.
        /// </summary>
        static void Walls(GameObject root)
        {
            double end = RinkLayout.CornerX, side = RinkLayout.NearCornerZ - RinkLayout.FarCornerZ, arc = Math.PI / 2 * RinkLayout.CornerRadius;
            // The runs in OnBoards' order: half the near end, a corner, a side, a corner, the far end, a corner, a side, a corner, half the near end.
            (double length, int pieces, double glassTop)[] runs =
            {
                (end, 1, RinkLayout.EndGlassTop), (arc, CornerSegments, RinkLayout.EndGlassTop), (side, 1, RinkLayout.SideGlassTop),
                (arc, CornerSegments, RinkLayout.EndGlassTop), (2 * end, 1, RinkLayout.EndGlassTop), (arc, CornerSegments, RinkLayout.EndGlassTop),
                (side, 1, RinkLayout.SideGlassTop), (arc, CornerSegments, RinkLayout.EndGlassTop), (end, 1, RinkLayout.EndGlassTop),
            };
            float board = (float)RinkLayout.BoardHeight;
            double s = 0;
            foreach (var run in runs)
            {
                double step = run.length / run.pieces;
                for (int i = 0; i < run.pieces; i++, s += step)
                {
                    RinkLayout.BoardPoint a = RinkLayout.OnBoards(s), b = RinkLayout.OnBoards(s + step), mid = RinkLayout.OnBoards(s + step / 2);
                    var from = new Vector3((float)a.X, 0f, (float)a.Z);
                    var along = new Vector3((float)(b.X - a.X), 0f, (float)(b.Z - a.Z));
                    var inward = new Vector3((float)mid.InX, 0f, (float)mid.InZ);
                    Quaternion turn = Quaternion.LookRotation(along.normalized, Vector3.up); // local z along the wall, x across it
                    Vector3 centre = from + along / 2f - inward * (WallThickness / 2f);
                    float length = along.magnitude + 2f * WallOverlap, top = (float)run.glassTop;
                    Wall(root, BoardsName, centre + Vector3.up * (board / 2f), turn, new Vector3(WallThickness, board, length), Boards);
                    Wall(root, GlassName, centre + Vector3.up * ((board + top) / 2f), turn, new Vector3(WallThickness, top - board, length), Glass);
                }
            }
        }

        static void Wall(GameObject root, string name, Vector3 centre, Quaternion turn, Vector3 size, PhysicsMaterial material)
        {
            var wall = new GameObject(name);
            wall.transform.SetParent(root.transform, false);
            wall.transform.localPosition = centre;
            wall.transform.localRotation = turn;
            var collider = wall.AddComponent<BoxCollider>();
            collider.size = size;
            collider.sharedMaterial = material;
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
