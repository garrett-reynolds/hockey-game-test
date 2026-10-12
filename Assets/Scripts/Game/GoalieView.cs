using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// The goalie: the rigged goalie character when there is one, otherwise the block goalie, plus the Easy mode glow on the
    /// Gaps (ShowGaps: one glow cell per Net Face cell of NetFaceGaps).
    /// The block goalie (no character model, or the forceGoalieBlocks developer flag) is one block just in front of the Net
    /// Face, standing across from where the goalie stands (Stand); with the character it is not drawn. The character holds
    /// the Ready Stance and on the Shot moves into the Save Reaction the Round's race gives (React), set by the set blend
    /// (docs/adr/0003); after it he recovers into the Ready Stance before the result hold ends, and in the Ready Stance he
    /// shows Stance Life (Live, #44). No colliders.
    /// </summary>
    public sealed class GoalieView
    {
        /// <summary>The block goalie's name in the scene.</summary>
        public const string BlockName = "Goalie block";
        const float BlockWidth = 0.9f, BlockHeight = 0.9f; // the block goalie: a crouched goalie's front, from the ice
        internal const float BlockDepth = 0.1f;
        internal const float BlockZ = -0.08f;   // centre; block spans z -0.13..-0.03, in front of the Net Face
        const float PostGlanceHeight = 0.5f;   // how high up the post a Miss glances back at (metres)
        const float GlowZ = 0.06f;     // just behind the Net Face
        /// <summary>The glow's shader: unlit, with its depth test set per material (see SetProgress).</summary>
        public const string EasyGlowShader = "Hockey Game/Easy Glow";
        static readonly Color GoalieColour = new Color(0.15f, 0.25f, 0.6f);
        static readonly Color GlowColour = new Color(0.3f, 1f, 0.3f);

        const float GlowFraction = 0.96f; // a glow cell's share of its Net Face cell, so neighbouring Gaps show as cells

        readonly GameObject _block; // the block goalie
        readonly GameObject _gapGlow;                                                   // Easy mode: one glow cell per Net Face cell
        readonly GameObject[] _gapCells = new GameObject[NetFaceGaps.Columns * NetFaceGaps.Rows];
        readonly Material _glow; // shared by every glow cell
        float _set = 1f;
        float _ready, _target; // facing (degrees): turned so far in the Ready Stance; squared up to the shooter
        NetFaceGaps _gaps = NetFaceGaps.None; // what the glow shows (ShowGaps)
        bool _visible;
        Vector3 _spot;                       // where the goalie stands (Stand)
        readonly GoalieCharacter _character; // null or without a model: the block goalie is drawn
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
        RoundSettings _before;                   // Before the Shot (#70, BeforeTheShot): its switches and sizes; null: none of it
        Vector3 _puckOnStick;
        float _watching, _settled;

        /// <param name="character">The goalie character; null (or no model) draws the block goalie instead.</param>
        public GoalieView(Transform parent, GoalieCharacter character = null)
        {
            _character = character != null && character.Present ? character : null;
            _glow = GlowMaterial();
            _block = RinkBuilder.Cube(parent, BlockName, new Vector3(0f, BlockHeight / 2f, BlockZ),
                new Vector3(BlockWidth, BlockHeight, BlockDepth), GoalieColour);
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
            if (_character != null) _block.GetComponent<Renderer>().enabled = false;
            Apply();
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
        /// Before the Shot the goalie's Ready Stance (or the block goalie) stands in front of some Gaps seen from the shooter's
        /// eye (the Gaps are measured from the puck, below and ahead of it), so the glow
        /// draws over it (no depth test); once the goalie reacts it is depth-tested again, so the flying puck stays in front.
        /// </summary>
        void SetGlowDepth()
        {
            _glow.SetFloat("_ZTest", (float)(_set > 0f ? CompareFunction.LessEqual : CompareFunction.Always));
        }

        /// <summary>
        /// How far the goalie has moved out of the Ready Stance: 0 = Ready Stance, 1 = fully into the reaction (the default).
        /// The block goalie does not move with it.
        /// </summary>
        public void SetProgress(float progress)
        {
            if (!Mathf.Approximately(progress, _set))
            {
                _set = Mathf.Clamp01(progress);
                SetGlowDepth();
            }
            PoseCharacter(); // every call, so nothing else left on the bones wins
        }

        /// <summary>
        /// Which way the goalie character faces (degrees about the vertical, + toward the shooter's left; see GoalieTurn):
        /// `ready` is where it has turned so far in the Ready Stance; `target` squares up to the shooter. Reacting, it
        /// finishes the turn to the target, so it stays squared up to the shooter (#39); React builds the reaction there. The
        /// block goalie never turns.
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
        /// Where the goalie stands in the crease (rink space, on the ice): he plays the angle and gets set by moving it
        /// (BreakawayView). The character is shown there by the next SetProgress, and the next React builds the reaction there;
        /// the block goalie moves across the Net Face with it at once.
        /// </summary>
        public void Stand(Vector3 spot)
        {
            _spot = spot;
            if (_character != null) _character.Spot = spot;
            Vector3 at = _block.transform.position;
            _block.transform.position = new Vector3(spot.x, at.y, at.z);
        }

        /// <summary>Where the goalie stands (see Stand).</summary>
        public Vector3 ShownSpot => _character != null ? _character.Spot : _spot;

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
            // Before the Shot (#70): once set he sits lower and quieter, his head on the puck; each fades as the reaction comes in.
            float lifeShare = 1f;
            if (_before != null)
            {
                _character.SitLower((float)BeforeTheShot.DropMetres(_settled, _set, _before));
                lifeShare = (float)BeforeTheShot.LifeShare(_settled, _set, _before);
            }
            // Stance Life fades out as the reaction comes in (none at all fully reacted: what the Shot measured is what shows)
            // and back in as he recovers into the Ready Stance.
            _character.Live(StanceLife.At(_life), (1f - _set * (1f - recovered)) * lifeShare);
            if (_before != null)
                _character.WatchPuck(_puckOnStick, (float)BeforeTheShot.HeadWeight(_watching, _set, _before), (float)_before.HeadShoulderShare);
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

        /// <summary>
        /// Before the Shot (#70, BeforeTheShot): the puck on the shooter's stick (rink space) his head follows, `watching` of the
        /// way (BeforeTheShot.Watching); how set he is (BeforeTheShot.Settled); and the RoundSettings switches and sizes of each
        /// piece (null: none of it). Shown by the next SetProgress. Never reaches the race: it measures the Ready Stance itself.
        /// </summary>
        public void ShowBeforeTheShot(Vector3 puck, float watching, float settled, RoundSettings settings)
        {
            _puckOnStick = puck;
            _watching = watching;
            _settled = settled;
            _before = settings;
        }

        /// <summary>The facing (degrees, see Face) the goalie character was last shown at.</summary>
        public float ShownFacing { get; private set; }

        /// <summary>True when the rigged goalie character is drawn (not the block goalie).</summary>
        public bool HasCharacter => _character != null;

        /// <summary>The parent of Easy mode's glow cells (the picture test tells the glow apart from the arena).</summary>
        public GameObject GapGlow => _gapGlow;

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

        /// <summary>Show the goalie (or hide it, and the Easy mode glow, when visible is false).</summary>
        public void Show(bool visible)
        {
            if (visible == _visible) return;
            _visible = visible;
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
            _block.SetActive(_visible);
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
        /// reaction: null for none. lateMarginSeconds: infinity when no gear was timed. lateMarginLimitSeconds: the Round's
        /// RoundSettings.LateMarginLimitSeconds, which picks the Annoyed Gesture on a Goal; required, so it is never silently 0.
        /// </summary>
        public void React(ShotResult result, Vector3 from, SaveReaction? reaction, double lateMarginSeconds, double lateMarginLimitSeconds)
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
    }
}
