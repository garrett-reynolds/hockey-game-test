using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// The goalie: the rigged goalie character when there is one, otherwise the block goalie, plus the Easy mode glow.
    /// All six Goalie Poses are built once as blocks from the pose table (Covered Zone = block filling the Zone,
    /// Contested Zone = smaller block, Open Zone = nothing); showing a pose enables its group and disables the rest.
    /// The blocks stay in the scene as the rules reference; with the character they are not drawn, and the character
    /// takes the pose instead, set by the same blend. No colliders.
    /// </summary>
    public sealed class GoalieView
    {
        const float CoveredFraction = 1f;
        internal const float BlockDepth = 0.1f;
        internal const float BlockZ = -0.08f;   // centre; block spans z -0.13..-0.03, in front of the Net Face
        const float GlowZ = 0.06f;     // just behind the Net Face
        /// <summary>The glow's shader: unlit, with its depth test set per material (see SetProgress).</summary>
        public const string EasyGlowShader = "Hockey Game/Easy Glow";
        static readonly Color GoalieColour = new Color(0.15f, 0.25f, 0.6f);
        static readonly Color GlowColour = new Color(0.3f, 1f, 0.3f);

        readonly GameObject[] _blocks = new GameObject[6];
        readonly GameObject[] _glows = new GameObject[6];
        readonly Material _glow; // shared by every glow cube
        readonly System.Collections.Generic.List<SetBlock>[] _setBlocks = new System.Collections.Generic.List<SetBlock>[6];
        float _set = 1f;
        float _ready, _target; // facing (degrees): turned so far in the Ready Stance; squared up to the shooter
        bool _glowOn;
        int _shown = -1;
        bool _visible;
        readonly GoalieCharacter _character; // null or without a model: the blocks are drawn

        /// <param name="character">The goalie character to pose; null (or no model) draws the blocks instead.</param>
        public GoalieView(Transform parent, GoalieCharacter character = null)
        {
            _character = character != null && character.Present ? character : null;
            _glow = GlowMaterial();
            foreach (GoaliePose pose in System.Enum.GetValues(typeof(GoaliePose)))
            {
                int i = (int)pose;
                _blocks[i] = new GameObject("Goalie " + pose);
                _blocks[i].transform.SetParent(parent);
                _glows[i] = new GameObject("Glow " + pose);
                _glows[i].transform.SetParent(parent);
                _setBlocks[i] = new System.Collections.Generic.List<SetBlock>();

                for (int column = 0; column < 3; column++)
                for (int row = 0; row < 3; row++)
                {
                    switch (GoaliePoses.CoverAt(pose, column, row))
                    {
                        case ZoneCover.Covered:
                            {
                                NetFaceMapping.ZoneRectangle(column, row, CoveredFraction, out float a, out float b, out float c, out float d);
                                var block = Block(_blocks[i].transform, column, row, a, b, c, d, BlockDepth, BlockZ, GoalieColour, null);
                                _setBlocks[i].Add(new SetBlock(block, GoaliePoseBlend.Neutral(column, row), new GoaliePoseBlend.Shape(a, b, c, d)));
                            }
                            break;
                        case ZoneCover.Contested:
                            {
                                NetFaceMapping.ContestedRectangle(column, row,
                                    IsOpen(pose, column - 1, row), IsOpen(pose, column + 1, row),
                                    IsOpen(pose, column, row - 1), IsOpen(pose, column, row + 1),
                                    out float a, out float b, out float c, out float d);
                                var block = Block(_blocks[i].transform, column, row, a, b, c, d, BlockDepth, BlockZ, GoalieColour, null);
                                _setBlocks[i].Add(new SetBlock(block, GoaliePoseBlend.Neutral(column, row), new GoaliePoseBlend.Shape(a, b, c, d)));
                            }
                            break;
                        case ZoneCover.Open:
                            {
                                // Neutral-shape block that shrinks away as the goalie sets; hidden once set.
                                var n = GoaliePoseBlend.Neutral(column, row);
                                var settle = Block(_blocks[i].transform, column, row, n.MinX, n.MinY, n.MaxX, n.MaxY, BlockDepth, BlockZ, GoalieColour, null);
                                settle.name += " (setting)";
                                _setBlocks[i].Add(new SetBlock(settle, n, GoaliePoseBlend.OpenTarget(column, row)));
                                NetFaceMapping.ZoneRectangle(column, row, 0.96f, out float a, out float b, out float c, out float d);
                                Block(_glows[i].transform, column, row, a, b, c, d, 0.02f, GlowZ, GlowColour, _glow);
                            }
                            break;
                    }
                }
            }
            if (_character != null)
                foreach (GameObject group in _blocks)
                    foreach (Renderer r in group.GetComponentsInChildren<Renderer>(true))
                        r.enabled = false;
            Apply();
            for (int i = 0; i < _setBlocks.Length; i++) PlaceSetBlocks(i);
            SetGlowDepth();
        }

        /// <summary>A copy of the Resources twin (so a build keeps the shader), or URP Unlit if it is missing.</summary>
        static Material GlowMaterial()
        {
            var template = Resources.Load<Material>(ArenaLighting.AssetFolder + "/" + ArenaLighting.EasyGlowMaterial);
            var material = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.color = GlowColour;
            return material;
        }

        /// <summary>
        /// Before the Shot the goalie's Ready Stance (or the neutral blocks) stands in front of the Open Zones, so the glow
        /// draws over it (no depth test); once the goalie reacts it is depth-tested again, so the flying puck stays in front.
        /// </summary>
        void SetGlowDepth()
        {
            _glow.SetFloat("_ZTest", (float)(_set > 0f ? CompareFunction.LessEqual : CompareFunction.Always));
        }

        /// <summary>How far the goalie has moved into the pose: 0 = Ready Stance / neutral shape, 1 = fully posed (the default).</summary>
        public void SetProgress(float progress)
        {
            if (!Mathf.Approximately(progress, _set))
            {
                _set = Mathf.Clamp01(progress);
                for (int i = 0; i < _setBlocks.Length; i++) PlaceSetBlocks(i);
                SetGlowDepth();
            }
            PoseCharacter(); // every call, so nothing else left on the bones wins
        }

        /// <summary>
        /// Which way the goalie character faces (degrees about the vertical, + toward the shooter's left; see GoalieTurn):
        /// `ready` is where it has turned so far in the Ready Stance; `target` squares up to the shooter. Reacting into the
        /// pose, it finishes the turn to the target, so it stays squared up to the shooter (every pose is shaped to read as the
        /// pose table turned, #40). The blocks never turn.
        /// </summary>
        public void Face(float ready, float target)
        {
            _ready = ready;
            _target = target;
            PoseCharacter();
        }

        /// <summary>The facing the pose ends at: the target, within the turn cap (GoalieTurn.MaxDegrees).</summary>
        float PosedFacing() => Mathf.Clamp(_target, -(float)GoalieTurn.MaxDegrees, (float)GoalieTurn.MaxDegrees);

        /// <summary>The character fully posed and turned, as it stands once the reaction ends (saves are measured on this).</summary>
        void PoseFull() => _character.Pose((GoaliePose)_shown, 1f, PosedFacing());

        void PoseCharacter()
        {
            if (_character != null && _visible) _character.Pose((GoaliePose)_shown, _set, Mathf.Lerp(_ready, PosedFacing(), _set));
        }

        void PlaceSetBlocks(int pose)
        {
            foreach (var block in _setBlocks[pose])
            {
                var r = GoaliePoseBlend.Lerp(block.Neutral, block.Pose, _set);
                float w = r.MaxX - r.MinX, h = r.MaxY - r.MinY;
                bool show = w > 0.005f && h > 0.005f;
                block.Cube.SetActive(show);
                if (!show) continue;
                block.Cube.transform.position = new Vector3((r.MinX + r.MaxX) / 2f, (r.MinY + r.MaxY) / 2f, BlockZ);
                block.Cube.transform.localScale = new Vector3(w, h, BlockDepth);
            }
        }

        readonly struct SetBlock
        {
            public readonly GameObject Cube;
            public readonly GoaliePoseBlend.Shape Neutral, Pose;
            public SetBlock(GameObject cube, GoaliePoseBlend.Shape neutral, GoaliePoseBlend.Shape pose) { Cube = cube; Neutral = neutral; Pose = pose; }
        }

        /// <summary>Show the goalie in the given pose (or hide it when visible is false) and set the Easy mode glow.</summary>
        public void Show(bool visible, GoaliePose pose, bool easyMode)
        {
            if (visible == _visible && (int)pose == _shown && easyMode == _glowOn) return;
            _visible = visible;
            _shown = (int)pose;
            _glowOn = easyMode;
            Apply();
        }

        void Apply()
        {
            for (int i = 0; i < _blocks.Length; i++)
            {
                _blocks[i].SetActive(_visible && i == _shown);
                _glows[i].SetActive(_visible && _glowOn && i == _shown);
            }
            if (_character == null) return;
            _character.Show(_visible);
            PoseCharacter();
        }

        /// <summary>
        /// Where a saved puck flying from `from` to `netPoint` stops when the goalie character is shown: just in front of the
        /// gear that made the save (the same pad, catcher or blocker the save sound picks), on the puck's line. The gear is
        /// where the Goalie Pose and the turn to the shooter put it, even while the goalie is still in the Ready Stance or reacting.
        /// False with no character shown (the save block is used instead).
        /// </summary>
        public bool SaveStop(Vector3 from, Vector3 netPoint, SoundCue contact, float puckThickness, out Vector3 stop)
        {
            stop = default;
            if (_character == null || !_visible) return false;
            // Measured on the full pose and turn: the save is asked for on the Shot, before the goalie has reacted.
            PoseFull();
            bool found = PosedGear(contact, netPoint, out Bounds gear);
            PoseCharacter();
            if (!found) return false;
            float z = gear.min.z - puckThickness / 2f;
            float t = Mathf.Approximately(netPoint.z, from.z) ? 1f : Mathf.Clamp01((z - from.z) / (netPoint.z - from.z));
            stop = Vector3.Lerp(from, netPoint, t);
            return true;
        }

        bool PosedGear(SoundCue contact, Vector3 netPoint, out Bounds gear)
        {
            gear = default;
            string piece = SavePiece(contact, netPoint);
            return piece != null && _character.GearBounds(piece, out gear);
        }

        /// <summary>The gear piece that makes a save (the same one the save sound picks); null if it is missing.</summary>
        string SavePiece(SoundCue contact, Vector3 netPoint)
        {
            if (contact == SoundCue.GloveCatch) return GearPiece.Catcher;
            if (contact == SoundCue.BlockerHit) return GearPiece.Blocker;
            // A pad save: the pad nearer the puck's spot.
            if (!_character.GearBounds(GearPiece.LeftPad, out Bounds a) || !_character.GearBounds(GearPiece.RightPad, out Bounds b)) return null;
            return Mathf.Abs(a.center.x - netPoint.x) < Mathf.Abs(b.center.x - netPoint.x) ? GearPiece.LeftPad : GearPiece.RightPad;
        }

        /// <summary>
        /// Adds the goalie character's gear and body to what the puck can hit, measured on the full pose (like SaveStop).
        /// False (nothing added) with no character shown.
        /// </summary>
        public bool AddObstacles(PuckObstacles obstacles)
        {
            if (_character == null || !_visible) return false;
            PoseFull();
            _character.AddTo(obstacles);
            PoseCharacter();
            return true;
        }

        /// <summary>
        /// The surface a save at `stop` (from SaveStop) is made on, and its outward normal there: the nearest real surface
        /// of the gear that made the save. A pad save is made on the stick instead when the stick is nearer the stop than
        /// the pad (the blade lies across the pads near the ice). obstacles: from AddObstacles.
        /// </summary>
        public bool SaveSurface(PuckObstacles obstacles, Vector3 stop, Vector3 netPoint, SoundCue contact, out Vector3 normal, out PuckSurface surface)
        {
            normal = Vector3.back;
            surface = PuckSurface.Pad;
            if (_character == null || !_visible) return false;
            PoseFull();
            string piece = SavePiece(contact, netPoint);
            PoseCharacter();
            if (piece == null || !obstacles.Nearest(stop, piece, out normal, out surface, out float distance)) return false;
            if (contact == SoundCue.SaveImpact && obstacles.Nearest(stop, GearPiece.Stick, out Vector3 blade, out PuckSurface stick, out float toStick) && toStick < distance)
            {
                normal = blade;
                surface = stick;
            }
            return true;
        }

        static bool IsOpen(GoaliePose pose, int column, int row)
        {
            return column >= 0 && column < 3 && row >= 0 && row < 3 && GoaliePoses.CoverAt(pose, column, row) == ZoneCover.Open;
        }

        static GameObject Block(Transform parent, int column, int row, float minX, float minY, float maxX, float maxY, float depth, float z, Color colour, Material material)
        {
            var cube = RinkBuilder.Cube(parent, "Zone " + column + "," + row,
                new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, z),
                new Vector3(maxX - minX, maxY - minY, depth), colour);
            if (material != null) cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }
    }
}
