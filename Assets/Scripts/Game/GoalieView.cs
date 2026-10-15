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
        readonly GoalieHitShapes _hits;      // the physics puck's hit shapes on his gear and body (#111); null for the block goalie
        float _life;                             // Stance Life clock (Live)
        bool _reacted;
        AfterShotGesture _gesture;               // what the goalie does once the puck has got there (AfterShot)
        double _lateMargin = double.PositiveInfinity, _lateMarginLimit; // from React: they pick the Annoyed Gesture at Judge
        float _sinceArrival = -1f, _sinceNetting = -1f, _sinceContact = -1f;
        Vector3 _puck, _post;                    // where the puck is now; the post a Miss glances back at
        double _followAtWhistle;                 // PROTOTYPE: the head follow and its target just before the Whistle, held while an AI clip replaces the reaction
        Vector3 _watchAtWhistle;
        RoundSettings _before;                   // Before the Shot (#70, BeforeTheShot): its switches and sizes; null: none of it
        Vector3 _puckOnStick;
        float _watching, _settled;
        readonly PushPoseTracker _pushTracker = new PushPoseTracker(); // the skate push pose (#126), stepped by ShowPush only
        PushShown _push = PushShown.None;
        PushPoseMoment _pushMoment;

        /// <param name="character">The goalie character; null (or no model) draws the block goalie instead.</param>
        public GoalieView(Transform parent, GoalieCharacter character = null)
        {
            _character = character != null && character.Present ? character : null;
            _glow = GlowMaterial();
            if (_character != null) Clips = new GoalieClipReactions(_character.Animator) { Rink = parent }; // PROTOTYPE: AI clips (BreakawayView drives it)
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
            if (_character != null)
            {
                _block.GetComponent<Renderer>().enabled = false;
                _hits = new GoalieHitShapes(_character, parent);
            }
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
            // PROTOTYPE: while an AI clip owns the bones, no procedural writer runs (ShowReaction, ShowPush, SitLower, Live and
            // WatchPuck, and through them ReadyStance, Stance Life, gestures, skate push, butterfly and shape).
            if (Clips != null && Clips.Owns) return;
            float facing = Mathf.Lerp(_ready, PosedFacing(), _set);
            ShownFacing = facing;
            float recovered = 0f;
            if (!_reacted) _character.ShowReaction(_set, facing);
            else
            {
                AfterShotMoment moment = AfterShot.At(_gesture, _sinceArrival, _sinceNetting);
                Vector3 watch;
                if (_sinceArrival >= 0f && Clips != null && Clips.ReplacesReaction(_gesture))
                {
                    // PROTOTYPE: an AI clip plays this Goal or Save: from the Whistle the code adds no reaction of its own, and the
                    // head keeps the follow it had at the Whistle (no turn after the puck), so nothing stacks on the clip.
                    moment = AfterShot.WithHeadFollow(AfterShot.WithoutReaction(moment), _gesture, _followAtWhistle);
                    watch = _watchAtWhistle;
                }
                else
                {
                    // The head follows the live puck from just after it first touches something, not only once it stops (the Whistle).
                    double follow = AfterShot.HeadFollow(_sinceContact, _character.YawTo(_puck));
                    moment = AfterShot.WithHeadFollow(moment, _gesture, follow);
                    watch = Vector3.Lerp(_puck, _post, (float)moment.Glance);
                    if (_sinceArrival < 0f) { _followAtWhistle = follow; _watchAtWhistle = _puck; }
                }
                _character.ShowReaction(_set, facing, _gesture, moment, watch);
                recovered = (float)moment.Recover;
            }
            // The skate push (#126) goes on right after the reaction (it sets the legs) and before SitLower and Live, which plant from
            // the feet as they are. PoseForPhysics poses again with the same push: only ShowPush steps it.
            _character.ShowPush(_pushTracker.Acting, _pushMoment, _bodyShare); // a Save Reaction's push is legs only, as in #126 (#130); the push the pose is acting on: a Done fades the same foot
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
        }

        /// <summary>
        /// The Stance Life clock (seconds; BreakawayView runs it on real time, so he stays alive through the slow-down): the goalie's
        /// breathing, weight shifts and stick taps in the Ready Stance (StanceLife). Shown by the next SetProgress.
        /// </summary>
        public void Live(float seconds) => _life = seconds;

        /// <summary>
        /// The skate push (#126) shown now, once per frame: steps the push pose by dt (game seconds) toward `shown` (PushPoseTracker:
        /// it never snaps) and keeps it for the next SetProgress, which lays it on the pose after the reaction. A pose made again in
        /// the same frame (BreakawayView.PoseForPhysics) uses it as it is.
        /// </summary>
        public void ShowPush(PushShown shown, double dt)
        {
            _push = shown;
            _pushMoment = _pushTracker.Step(shown, dt);
            // A Save Reaction's push is legs only at the #126 size: it blends there (and back) over BlendSeconds, never in one frame.
            float step = (float)(dt / GoalieSkatePush.BlendSeconds), goal = _reacted ? 0f : 1f;
            _bodyShare = _bodyShare < goal ? Mathf.Min(goal, _bodyShare + step) : Mathf.Max(goal, _bodyShare - step);
        }

        float _bodyShare = 1f; // 1 the full push (hips lean, full reach) .. 0 a Save Reaction's push; stepped by ShowPush; 1 again when hidden

        /// <summary>How much of the full push the pose shows (1 full, 0 a Save Reaction's legs-only push); back to 1 when hidden (#131).</summary>
        public float BodyShare => _bodyShare;

        /// <summary>
        /// The cut into a new Shot's skate-in, under full black (#131): the last Shot's reaction state ends (so its legs-only push does
        /// not carry into this Shot) and the push is the full one at once. Not called in the Result Hold or recovery.
        /// </summary>
        public void NewShot()
        {
            _reacted = false;
            _bodyShare = 1f;
        }

        /// <summary>The push last given to ShowPush (PushShown.None before any, and after the goalie is hidden).</summary>
        public PushShown Push => _push;

        /// <summary>The push pose last worked out by ShowPush.</summary>
        public PushPoseMoment PushMoment => _pushMoment;

        /// <summary>How far the last React slid the body across the crease (metres, + toward +X; 0 before any React and for the block goalie): the move a Save Reaction's push shows.</summary>
        public float ReactionSlideMetres => _character != null && _reacted ? _character.ReactionSlideMetres : 0f;

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

        /// <summary>PROTOTYPE: the AI clips on this goalie; null for the block goalie.</summary>
        public GoalieClipReactions Clips { get; }

        /// <summary>The facing (degrees, see Face) the goalie character was last shown at.</summary>
        public float ShownFacing { get; private set; }

        /// <summary>True when the rigged goalie character is drawn (not the block goalie).</summary>
        public bool HasCharacter => _character != null;

        /// <summary>
        /// Call in each physics step, once the goalie is posed for the end of the step (SetProgress) and before physics simulates:
        /// moves the hit shapes to his gear (#111). Nothing without the character or while he is hidden.
        /// </summary>
        public void MoveHitShapes() => _hits?.Move();

        /// <summary>The goalie as drawn: the character's root, or the block goalie (the Replay records it).</summary>
        public Transform Body => _character != null ? _character.Root.transform : _block.transform;

        /// <summary>The parent of Easy mode's glow cells (the picture test tells the glow apart from the arena).</summary>
        public GameObject GapGlow => _gapGlow;

        /// <summary>Show the goalie (or hide it, and the Easy mode glow, when visible is false).</summary>
        public void Show(bool visible)
        {
            if (visible == _visible) return;
            _visible = visible;
            if (!visible)
            {
                _pushTracker.Reset();
                _bodyShare = 1f;
                _push = PushShown.None;
                _pushMoment = _pushTracker.Now;
            }
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

        /// <summary>Takes the hit shapes away (and tells PuckReach to stop keeping their contacts). The view is not used after.</summary>
        public void Dispose() => _hits?.Dispose();

        void Apply()
        {
            _block.SetActive(_visible);
            ApplyGaps();
            if (_character == null) return;
            _character.Show(_visible);
            _hits.SetActive(_visible);
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
            if (_character == null || !_visible || !result.HasShot) return;
            NetFaceMapping.ToMetres(result.X, result.Y, out double right, out double up);
            var netPoint = new Vector3((float)right, (float)up, 0f);
            bool lateReach = result.Kind == ShotResultKind.Goal && reaction.HasValue && !double.IsInfinity(lateMarginSeconds);
            _character.React(reaction ?? SaveReactions.For(result.X, result.Y), from, netPoint, PosedFacing(), result.Kind == ShotResultKind.Save || lateReach);
            _reacted = true;
            _gesture = AfterShot.GestureFor(result.Kind, lateMarginSeconds, lateMarginLimitSeconds);
            _lateMargin = lateMarginSeconds;
            _lateMarginLimit = lateMarginLimitSeconds;
            _sinceArrival = _sinceNetting = _sinceContact = -1f;
            _puck = netPoint;
            _followAtWhistle = 0;
            _watchAtWhistle = netPoint;
            _post =new Vector3((netPoint.x >= 0f ? 1f : -1f) * (float)NetFaceMapping.WidthMetres / 2f, PostGlanceHeight, 0f);
            PoseCharacter();
        }

        /// <summary>
        /// At the Whistle: what the goalie does after his reach follows the Shot's real result, not the race's (React). A GOAL!
        /// gets its Annoyed Gesture (picked by the race's Late Margin, as React), a Miss gets Relief, a Save none.
        /// </summary>
        public void Judge(ShotResultKind kind)
        {
            _gesture = AfterShot.GestureFor(kind, _lateMargin, _lateMarginLimit);
        }

        /// <summary>The seconds since the puck got there, as last given to Watch (negative: not yet).</summary>
        public float SecondsSinceArrival => _sinceArrival;

        /// <summary>The seconds since the puck met the netting, as last given to Watch (negative: not yet, so the gesture waits for AfterShot.LatestNetting).</summary>
        public float SecondsSinceNetting => _sinceNetting;

        /// <summary>
        /// Each frame of the result hold, from PhysicsShotEffects: where the puck is (shown or not), the seconds since it got
        /// there (the Whistle) and since a GOAL! met the netting (negative: not
        /// yet). From these the goalie carries on after its reach (AfterShot, #43): its head follows the puck, then a GOAL!
        /// looks back into the net and shows an Annoyed Gesture, and a Miss shows Relief. Shown by the next SetProgress.
        /// </summary>
        public void Watch(Vector3 puck, float sinceArrival, float sinceNetting, float sinceContact = -1f)
        {
            _puck = puck;
            _sinceContact = sinceContact;
            _sinceArrival = sinceArrival;
            _sinceNetting = sinceNetting;
        }

        /// <summary>What the goalie does after the last React's Shot (None before any).</summary>
        public AfterShotGesture Gesture => _reacted ? _gesture : AfterShotGesture.None;

        /// <summary>The piece of gear the Save Reaction put on the puck's line (GearPiece), or null.</summary>
        public string ReactionPiece => _reacted ? _character.ReactionPiece : null;
    }
}
