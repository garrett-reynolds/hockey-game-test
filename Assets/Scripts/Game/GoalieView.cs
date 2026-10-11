using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// The goalie: the rigged goalie character when there is one, otherwise the block goalie, plus the Easy mode glow on the
    /// Gaps (ShowGaps: one glow cell per Net Face cell of NetFaceGaps).
    /// All six Goalie Poses are built once as blocks from the pose table (Covered Zone = block filling the Zone,
    /// Contested Zone = smaller block, Open Zone = nothing); showing a pose enables its group and disables the rest.
    /// The blocks stay in the scene as the rules reference; with the character they are not drawn. The character never shows
    /// the Goalie Pose: it holds the Ready Stance and on the Shot moves into a Save Reaction chosen from where the Shot goes
    /// (React), set by the same blend (docs/adr/0002); after it he recovers into the Ready Stance before the result hold ends,
    /// and in the Ready Stance he shows Stance Life (Live, #44). No colliders.
    /// </summary>
    public sealed class GoalieView
    {
        const float CoveredFraction = 1f;
        internal const float BlockDepth = 0.1f;
        internal const float BlockZ = -0.08f;   // centre; block spans z -0.13..-0.03, in front of the Net Face
        const float PostGlanceHeight = 0.5f;   // how high up the post a Miss glances back at (metres)
        const float GlowZ = 0.06f;     // just behind the Net Face
        /// <summary>The glow's shader: unlit, with its depth test set per material (see SetProgress).</summary>
        public const string EasyGlowShader = "Hockey Game/Easy Glow";
        static readonly Color GoalieColour = new Color(0.15f, 0.25f, 0.6f);
        static readonly Color GlowColour = new Color(0.3f, 1f, 0.3f);

        const float GlowFraction = 0.96f; // a glow cell's share of its Net Face cell, so neighbouring Gaps show as cells

        readonly GameObject[] _blocks = new GameObject[6];
        readonly GameObject _gapGlow;                                                   // Easy mode: one glow cell per Net Face cell
        readonly GameObject[] _gapCells = new GameObject[NetFaceGaps.Columns * NetFaceGaps.Rows];
        readonly Material _glow; // shared by every glow cell
        readonly System.Collections.Generic.List<SetBlock>[] _setBlocks = new System.Collections.Generic.List<SetBlock>[6];
        float _set = 1f;
        float _ready, _target; // facing (degrees): turned so far in the Ready Stance; squared up to the shooter
        NetFaceGaps _gaps = NetFaceGaps.None; // what the glow shows (ShowGaps)
        int _shown = -1;
        bool _visible;
        readonly GoalieCharacter _character; // null or without a model: the blocks are drawn
        float _life;                             // Stance Life clock (Live)
        Transform _carried, _carrier;            // Carry: the resting puck, and what it moves with
        Vector3 _carriedAt;
        Quaternion _carriedTurn;
        bool _reacted;
        readonly PuckObstacles _reaction = new PuckObstacles(); // the reaction's gear and body, from React (refilled each Shot)
        readonly string[] _reactionPiece = new string[1];       // SaveStop's cast: the reaction's piece alone
        AfterShotGesture _gesture;               // what the goalie does once the puck has got there (AfterShot)
        float _sinceArrival = -1f, _sinceNetting = -1f;
        Vector3 _puck, _post;                    // where the puck is now; the post a Miss glances back at

        /// <param name="character">The goalie character; null (or no model) draws the blocks instead.</param>
        public GoalieView(Transform parent, GoalieCharacter character = null)
        {
            _character = character != null && character.Present ? character : null;
            _glow = GlowMaterial();
            foreach (GoaliePose pose in System.Enum.GetValues(typeof(GoaliePose)))
            {
                int i = (int)pose;
                _blocks[i] = new GameObject("Goalie " + pose);
                _blocks[i].transform.SetParent(parent);
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
                            }
                            break;
                    }
                }
            }
            _gapGlow = new GameObject("Gap glow");
            _gapGlow.transform.SetParent(parent);
            for (int column = 0; column < NetFaceGaps.Columns; column++)
            for (int row = 0; row < NetFaceGaps.Rows; row++)
            {
                NetFaceGaps.Cell(column, row, out double minX, out double minY, out double maxX, out double maxY);
                NetFaceMapping.ToMetres(minX, minY, out double left, out double bottom);
                NetFaceMapping.ToMetres(maxX, maxY, out double right, out double top);
                var cell = RinkBuilder.Cube(_gapGlow.transform, "Gap " + column + "," + row,
                    new Vector3((float)(left + right) / 2f, (float)(bottom + top) / 2f, GlowZ),
                    new Vector3((float)(right - left) * GlowFraction, (float)(top - bottom) * GlowFraction, 0.02f), GlowColour);
                cell.GetComponent<Renderer>().sharedMaterial = _glow;
                _gapCells[NetFaceGaps.Index(column, row)] = cell;
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
        /// Before the Shot the goalie's Ready Stance (or the neutral blocks) stands in front of some Gaps seen from the shooter's
        /// eye (the Gaps are measured from the puck, below and ahead of it), so the glow
        /// draws over it (no depth test); once the goalie reacts it is depth-tested again, so the flying puck stays in front.
        /// </summary>
        void SetGlowDepth()
        {
            _glow.SetFloat("_ZTest", (float)(_set > 0f ? CompareFunction.LessEqual : CompareFunction.Always));
        }

        /// <summary>
        /// How far the goalie has moved out of the Ready Stance: 0 = Ready Stance / neutral block shapes, 1 = fully into the
        /// reaction (the character) or the pose's blocks (the default).
        /// </summary>
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
        /// `ready` is where it has turned so far in the Ready Stance; `target` squares up to the shooter. Reacting, it
        /// finishes the turn to the target, so it stays squared up to the shooter (#39); React builds the reaction there. The
        /// blocks never turn.
        /// </summary>
        public void Face(float ready, float target)
        {
            _ready = ready;
            _target = target;
            PoseCharacter();
        }

        /// <summary>The facing the reaction ends at, and React builds it at: the target, within the turn cap (GoalieTurn.MaxDegrees).</summary>
        public float PosedFacing() => Mathf.Clamp(_target, -(float)GoalieTurn.MaxDegrees, (float)GoalieTurn.MaxDegrees);

        /// <summary>
        /// Where the goalie character stands in the crease (rink space, on the ice): he plays the angle and gets set by moving it
        /// (BreakawayView). Shown by the next SetProgress; the next React builds the reaction there. The blocks never move.
        /// </summary>
        public void Stand(Vector3 spot)
        {
            if (_character != null) _character.Spot = spot;
        }

        /// <summary>Where the goalie character stands (see Stand); zero without a character.</summary>
        public Vector3 ShownSpot => _character != null ? _character.Spot : Vector3.zero;

        /// <summary>
        /// On the Shot, before React: the goalie stands where the Round's race measured him (its Set-up spot) and is squared up
        /// to where the puck started, so the reaction shown is the one that decided the Shot.
        /// </summary>
        public void StandFor(ShotRace race)
        {
            Stand(new Vector3((float)race.Spot.X, 0f, (float)race.Spot.Z));
            _target = (float)SetUps.Facing(race.Spot, race.ShotStart);
        }

        void PoseCharacter()
        {
            if (_character == null || !_visible) return;
            float facing = Mathf.Lerp(_ready, PosedFacing(), _set);
            ShownFacing = facing;
            float recovered = 0f;
            if (!_reacted) _character.ShowReaction(_set, facing);
            else
            {
                AfterShotMoment moment = AfterShot.At(_gesture, _sinceArrival, _sinceNetting);
                _character.ShowReaction(_set, facing, _gesture, moment, Vector3.Lerp(_puck, _post, (float)moment.Glance));
                recovered = (float)moment.Recover;
            }
            // Stance Life fades out as the reaction comes in (none at all fully reacted: what the Shot measured is what shows)
            // and back in as he recovers into the Ready Stance.
            _character.Live(StanceLife.At(_life), 1f - _set * (1f - recovered));
            if (_carried != null)
            {
                _carried.position = _carrier.TransformPoint(_carriedAt);
                _carried.rotation = _carrier.rotation * _carriedTurn;
            }
        }

        /// <summary>
        /// The Stance Life clock (seconds; BreakawayView runs it on real time, so he stays alive through the slow-down): the goalie's
        /// breathing, weight shifts and stick taps in the Ready Stance (StanceLife). Shown by the next SetProgress.
        /// </summary>
        public void Live(float seconds) => _life = seconds;

        /// <summary>The facing (degrees, see Face) the goalie character was last shown at.</summary>
        public float ShownFacing { get; private set; }

        /// <summary>True when the rigged goalie character is drawn (not the blocks).</summary>
        public bool HasCharacter => _character != null;

        /// <summary>
        /// A puck held in the catching glove (the only gear that keeps it, #48) moves with the glove as he follows through and
        /// recovers: placed by each SetProgress. Null lets go. Nothing without the character.
        /// </summary>
        public void Carry(Transform puck)
        {
            _carried = null;
            if (puck == null || _character == null || !_visible) return;
            // The catch was measured on the reaction as it ends (SaveStop): the puck is held where that glove has it, even when
            // the puck gets there in the frame before he is shown fully in it (gear already in the way, a very short flight, #63).
            _character.ShowReaction(1f, PosedFacing());
            _carrier = _character.Carrier(puck.position);
            if (_carrier != null)
            {
                _carried = puck;
                _carriedAt = _carrier.InverseTransformPoint(puck.position);
                _carriedTurn = Quaternion.Inverse(_carrier.rotation) * puck.rotation;
            }
            PoseCharacter();
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

        /// <summary>Show the goalie in the given pose (or hide it, and the Easy mode glow, when visible is false).</summary>
        public void Show(bool visible, GoaliePose pose)
        {
            if (visible == _visible && (int)pose == _shown) return;
            _visible = visible;
            _shown = (int)pose;
            Apply();
        }

        /// <summary>
        /// Easy mode: glow these Gaps on the Net Face, each Gap's cell (while the goalie is shown); NetFaceGaps.None glows
        /// nothing. A hint only (RoundDriver passes the Round's Gaps while EasyGlow.Shows).
        /// </summary>
        public void ShowGaps(NetFaceGaps gaps)
        {
            gaps = gaps ?? NetFaceGaps.None;
            if (ReferenceEquals(gaps, _gaps)) return;
            _gaps = gaps;
            ApplyGaps();
        }

        void ApplyGaps()
        {
            _gapGlow.SetActive(_visible && _gaps.Count > 0);
            for (int column = 0; column < NetFaceGaps.Columns; column++)
            for (int row = 0; row < NetFaceGaps.Rows; row++)
                _gapCells[NetFaceGaps.Index(column, row)].SetActive(_gaps.IsGap(column, row));
        }

        void Apply()
        {
            for (int i = 0; i < _blocks.Length; i++)
                _blocks[i].SetActive(_visible && i == _shown);
            ApplyGaps();
            if (_character == null) return;
            _character.Show(_visible);
            if (_visible) _character.Prepare(); // measured now, at the Round start, not on the Shot
            PoseCharacter();
        }

        /// <summary>
        /// On the Shot: the goalie character's reaction to this result, the puck flying from `from` (world space) to the
        /// Shot's Net Face point. A Save gets the Save Reaction to show (the Round's race: reaction; with none, the Shot point's,
        /// SaveReactions), its gear reaching the puck's line. A Goal the race timed some gear for (a reaction and a finite Late
        /// Margin) gets that gear reaching the puck's line too: it gets there too late, after the puck has passed (Breakaway.GoalieSet,
        /// #63). A Miss, or a Goal no gear could reach, gets a reach that stays off the puck (what follows: Watch). Built at the
        /// facing the reaction ends at; shown by SetProgress. Nothing for no Shot or no character shown.
        /// </summary>
        public void React(ShotResult result, Vector3 from, SaveReaction? reaction = null, double lateMarginSeconds = double.PositiveInfinity,
            double lateMarginLimitSeconds = 0)
        {
            _reacted = false;
            _carried = null;
            if (_character == null || !_visible || !result.HasShot) return;
            NetFaceMapping.ToMetres(result.X, result.Y, out double right, out double up);
            var netPoint = new Vector3((float)right, (float)up, 0f);
            bool lateReach = result.Kind == ShotResultKind.Goal && reaction.HasValue && !double.IsInfinity(lateMarginSeconds);
            _character.React(reaction ?? SaveReactions.For(result.X, result.Y), from, netPoint, PosedFacing(), result.Kind == ShotResultKind.Save || lateReach);
            _reacted = true;
            _gesture = AfterShot.GestureFor(result.Kind, lateMarginSeconds, lateMarginLimitSeconds);
            _sinceArrival = _sinceNetting = -1f;
            _puck = netPoint;
            _post = new Vector3((netPoint.x >= 0f ? 1f : -1f) * (float)NetFaceMapping.WidthMetres / 2f, PostGlanceHeight, 0f);
            _reaction.Clear();
            _character.AddReaction(_reaction);
            PoseCharacter();
        }

        /// <summary>
        /// Each frame of the result hold, from ShotEffects: where the puck is (shown or not), the seconds since it reached the
        /// Net Face and since a GOAL! met the netting (negative: not yet). From these the goalie carries on after its reach
        /// (AfterShot, #43): its head follows the puck, then a GOAL! looks back into the net and shows an Annoyed Gesture, and
        /// a Miss shows Relief. Shown by the next SetProgress.
        /// </summary>
        public void Watch(Vector3 puck, float sinceArrival, float sinceNetting)
        {
            _puck = puck;
            _sinceArrival = sinceArrival;
            _sinceNetting = sinceNetting;
        }

        /// <summary>What the goalie does after the last React's Shot (None before any).</summary>
        public AfterShotGesture Gesture => _reacted ? _gesture : AfterShotGesture.None;

        /// <summary>The piece of gear the Save Reaction put on the puck's line (GearPiece), or null.</summary>
        public string ReactionPiece => _reacted ? _character.ReactionPiece : null;

        /// <summary>
        /// Where a saved puck flying from `from` to `netPoint` stops when the goalie character is shown: where it first meets
        /// the Save Reaction's gear (React) on its line, as the goalie stands once the reaction ends (it is asked for on the
        /// Shot, before the goalie has moved). False with no character shown or no Save Reaction (the save block is used instead).
        /// </summary>
        public bool SaveStop(Vector3 from, Vector3 netPoint, float puckThickness, out Vector3 stop)
        {
            stop = default;
            string piece = ReactionPiece;
            if (piece == null) return false;
            _reactionPiece[0] = piece;
            if (_reaction.Cast(GoalieCharacter.NearGoalie(from, netPoint), netPoint, puckThickness / 2f, out stop, out _, out _, _reactionPiece)) return true;
            // The gear stops short of the line (out of reach): just in front of it, on the line.
            if (piece == GearPiece.Body || !_character.ReactionGearBounds(piece, out Bounds gear)) return false;
            float z = gear.min.z - puckThickness / 2f;
            float t = Mathf.Approximately(netPoint.z, from.z) ? 1f : Mathf.Clamp01((z - from.z) / (netPoint.z - from.z));
            stop = Vector3.Lerp(from, netPoint, t);
            return true;
        }

        /// <summary>
        /// Adds the goalie character's gear and body to what the puck can hit, as the reaction ends (like SaveStop).
        /// False (nothing added) with no character shown or no reaction yet.
        /// </summary>
        public bool AddObstacles(PuckObstacles obstacles)
        {
            if (_character == null || !_visible || !_reacted) return false;
            _character.AddReaction(obstacles);
            return true;
        }

        /// <summary>
        /// The surface a save at `stop` (from SaveStop) is made on, and its outward normal there: the nearest real surface
        /// of the Save Reaction's gear. obstacles: from AddObstacles.
        /// </summary>
        public bool SaveSurface(PuckObstacles obstacles, Vector3 stop, out Vector3 normal, out PuckSurface surface)
        {
            normal = Vector3.back;
            surface = PuckSurface.Pad;
            string piece = ReactionPiece;
            return piece != null && obstacles.Nearest(stop, piece, out normal, out surface);
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
