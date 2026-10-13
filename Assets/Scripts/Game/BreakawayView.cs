using System;
using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HockeyGame
{
    /// <summary>
    /// The breakaway feel, driven by what the Round core reports: the camera skates in and curls toward the net,
    /// the world slows down with a grey, high-contrast, vignetted look while the goalie waits in its Ready Stance, and a
    /// red ring around the net drains over the shooting window. The goalie plays the angle and turns to face the shooter as they come in. A Shot
    /// snaps time and look back to normal, and the goalie reacts to the Shot (its Save Reaction) at the shooter's angle.
    /// The goalie's Stance Life runs on real time throughout, so he stays alive through the slow-down. Sets Time.timeScale; the Round core keeps ticking on unscaled real time.
    /// </summary>
    public sealed class BreakawayView : MonoBehaviour
    {
        const float RingDotSize = 7f;
        static readonly Vector3 NetCentre = new Vector3(0f, (float)(NetFaceMapping.HeightMetres / 2.0), 0f);
        static readonly Vector3 NetRightEdge = new Vector3((float)(NetFaceMapping.WidthMetres / 2.0), (float)(NetFaceMapping.HeightMetres / 2.0), 0f);
        static readonly Color RingColour = new Color(1f, 0.2f, 0.2f);

        Round _round;
        Func<float> _flight; // PhysicsShotEffects.FlightSeconds: the Shot's Flight Time, which the goalie eases over
        Camera _camera;
        GoalieView _goalie;
        Volume _look;
        RoundPhase _lastPhase = RoundPhase.Idle;
        readonly ShotFade _fade = new ShotFade(); // the black over the cut between Shots (#129)
        float _drift;      // metres drifted toward the net since the skate-in ended (scaled time)
        float _gameTime;   // game seconds since the skate-in began (stride bob and roll; the stride sounds use the same clock)
        readonly GoalieTurn _turn = new GoalieTurn(0.0); // the goalie squaring up to the shooter
        GoalieShuffle _shuffle;  // the goalie playing the angle across the crease
        SkateInShuffles _steps;  // ... in shuffles on the skate-in (#70)
        RinkPoint _puckOnStick;  // the puck his head follows before the Shot (#70)
        float _lastTarget;       // the facing that squares him up to the puck, as last worked out
        float _life;       // the goalie's Stance Life clock: real seconds, never reset, so it never jumps
        float _frameSeconds; // Time.unscaledTime at the last frame's LateUpdate, where the Round's clock (PhaseSecondsElapsed) is read
        float _lookUp;     // how far the camera turns up to the Video Board for the Replay (the Arena Show's), 0..1

        /// <param name="flightSeconds">The current Shot's Flight Time (PhysicsShotEffects.FlightSeconds), the same before and
        /// after the Whistle, so nothing jumps on the Whistle frame. Required.</param>
        public void Init(Round round, Camera shooterCamera, GoalieView goalie, Func<float> flightSeconds)
        {
            _round = round;
            _flight = flightSeconds ?? throw new ArgumentNullException(nameof(flightSeconds));
            _camera = shooterCamera;
            _goalie = goalie;
            _camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            _look = MakeLookVolume(transform);
            _shuffle = new GoalieShuffle(new RinkPoint(0, 0, -round.Settings.NormalDepthMetres), round.Settings); // centred, at his normal depth
            _steps = new SkateInShuffles(_shuffle.Shown);
            _pushFrom = _shuffle.Shown;
            _goalie.Stand(ToVector(_shuffle.Shown));
        }

        /// <summary>How far the camera turns up to the Video Board this frame (ArenaState.CameraLookUp): 0 on the net, 1 on the board.</summary>
        public void LookUp(double amount) => _lookUp = (float)amount;

        void LateUpdate()
        {
            if (_round == null) return;
            _frameSeconds = Time.unscaledTime;
            Step(Time.deltaTime, Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Each physics step (#111, before Unity simulates it): poses the goalie for the end of the step, then moves the hit
        /// shapes to his gear, so physics sees the gear's true speed. LateUpdate poses him again for the picture each frame.
        /// Between frames only his reaction moves (GoalieSet, on the result hold's clock); where he stands and faces holds.
        /// </summary>
        void FixedUpdate()
        {
            if (_round == null) return;
            float ahead = Mathf.Clamp(Time.fixedUnscaledTime + Time.fixedUnscaledDeltaTime - _frameSeconds, 0f, MaxAheadSeconds);
            PoseForPhysics(ahead);
        }

        /// <summary>The longest the pose is taken ahead of the Round's clock (seconds): a long frame is not a long guess.</summary>
        const float MaxAheadSeconds = 0.1f;

        /// <summary>Poses the goalie as the Round's clock will have him ahead seconds from the last frame, and moves the hit shapes to his gear.</summary>
        public void PoseForPhysics(float ahead)
        {
            float set = (float)Breakaway.GoalieSet(_round.Phase, _round.PhaseSecondsElapsed + ahead, _round.LastShot.HasValue,
                _flight(), _round.LastRace);
            _goalie.SetProgress(set);
            _goalie.MoveHitShapes();
        }

        /// <summary>One frame: dt is the game (scaled) seconds since the last one, realDt the real seconds. LateUpdate runs it each frame.</summary>
        public void Step(float dt, float realDt)
        {
            RoundPhase phase = _round.Phase;
            double phaseSeconds = _round.PhaseSecondsElapsed;
            _fade.Step(_round, realDt); // the frame the camera cuts to the next skate-in is the frame the screen is black (#129)
            bool skateInStarts = phase == RoundPhase.SkateIn && _lastPhase != RoundPhase.SkateIn;
            if (skateInStarts)
            {
                _drift = 0f;
                _gameTime = 0f;
                _goalie.NewShot(); // under the cut's full black: the last Shot's legs-only push ends (#131)
            }
            // The goalie carries on from the facing he was shown at (after a Shot: squared up to that shooter), then turns to
            // the next shooter as they skate in: no snap between Shots (#44).
            if (phase != _lastPhase && (phase == RoundPhase.SkateIn || phase == RoundPhase.Idle || phase == RoundPhase.RoundOver))
                _turn.Reset(_goalie.ShownFacing);
            _lastPhase = phase;
            // Real time: at the slow-down's quarter speed the 4 s he waits would be 1 game-second, a third of a breath.
            _life += realDt;
            _goalie.Live(_life);

            // Time and look come from the phase every frame, so a Shot (window -> result hold) snaps both back at once.
            float timeScale = (float)Breakaway.TimeScale(phase, phaseSeconds);
            Time.timeScale = timeScale;
            _look.weight = (float)Breakaway.LookAmount(timeScale);

            // Before the Shot (#70): his head follows the puck (from the Shot on, where it started) and he gets set (BeforeTheShot).
            RoundSettings settings = _round.Settings;
            if (phase == RoundPhase.SkateIn || phase == RoundPhase.SlowDown || phase == RoundPhase.ShootingWindow)
                _puckOnStick = Breakaway.Puck(_round.AttackSide, phase, phaseSeconds, settings);
            else if (phase == RoundPhase.ResultHold && _round.LastRace.HasValue)
                _puckOnStick = _round.LastRace.Value.ShotStart;
            _goalie.ShowBeforeTheShot(ToVector(_puckOnStick), (float)BeforeTheShot.Watching(phase, phaseSeconds, settings),
                (float)BeforeTheShot.Settled(phase, phaseSeconds, settings), settings);

            // On the Shot he moves as the Round's race timed him (Reach in time): what is seen agrees with the result (#63).
            float set = (float)Breakaway.GoalieSet(phase, phaseSeconds, _round.LastShot.HasValue,
                _flight(), _round.LastRace);
            if (phase == RoundPhase.Idle || phase == RoundPhase.RoundOver)
            {
                _goalie.Face((float)_turn.Shown, (float)_turn.Shown); // keeps facing the last shooter
                _goalie.ShowPush(PushShown.None, dt); // a pose part-way out fades (it never freezes)
                _pushFrom = _shuffle.Shown; // so the next skate-in's first frame reads no stale move
                _pushPeak = 0;
                _goalie.SetProgress(set);
                return; // camera stays where it is
            }
            // In the skate-in game time is real time: follow the Round's clock exactly, so each stride sound
            // (RoundSoundCues, from the same phase seconds) lands on the bob's low point. Afterwards it runs on scaled.
            if (phase == RoundPhase.SkateIn) _gameTime = (float)phaseSeconds;
            else _gameTime += dt;
            AttackSide side = _round.AttackSide;
            // The drift on toward the net, from the same numbers the Round starts the puck from (Breakaway.ShotStart); it holds from the Shot on.
            if (phase == RoundPhase.SlowDown)
                _drift = (float)Breakaway.Drift(side, phaseSeconds, 0, _round.Settings);
            else if (phase == RoundPhase.ShootingWindow)
                _drift = (float)Breakaway.Drift(side, _round.Settings.SlowDownSeconds, phaseSeconds, _round.Settings);

            double u = phase == RoundPhase.SkateIn ? Breakaway.SkateProgress(phaseSeconds, _round.Settings) : 1.0;
            Breakaway.SkatePoint(side, u, out double x, out double distance);
            PlaceCamera((float)x, (float)distance);
            LookUpTo(_camera.transform, ReplayLookTarget, _lookUp); // the Replay: up at the Video Board and back

            // The goalie plays the angle (SetUps.Approach): he shuffles across on the line from the puck to the net centre through
            // the skate-in, settles into his Set-up through the slow-down, and stands on his Set-up spot through the window. He
            // squares up to the puck: turning round during the skate-in, following closely through the slow-down and window
            // (game time). From the Shot on he holds still where the race measured him (the camera stops too), reacting at that
            // angle to where the Shot goes.
            float target;
            if (phase == RoundPhase.ResultHold)
            {
                if (_round.LastRace.HasValue)
                {
                    ShotRace race = _round.LastRace.Value;
                    _shuffle.Reset(race.Spot);
                    _lastTarget = (float)SetUps.Facing(race.Spot, race.ShotStart);
                }
                target = _lastTarget; // no Shot in time: he stays as the window ended
            }
            else
            {
                RinkPoint headedFor = SetUps.Approach(_round.SetUp, side, phase, phaseSeconds, _round.Settings);
                if (phase == RoundPhase.ShootingWindow) _shuffle.Reset(headedFor);
                else
                {
                    // On the skate-in he plays the angle in shuffles (#70); the slow-down finishes any shuffle, then he settles.
                    if (skateInStarts) _steps.Reset(headedFor);
                    _shuffle.Step(_steps.Step(headedFor, dt, phase == RoundPhase.SkateIn && settings.SkateInShuffles, settings), dt);
                }
                target = _lastTarget = (float)SetUps.Facing(_shuffle.Shown, Breakaway.Puck(side, phase, phaseSeconds, _round.Settings));
                _turn.Step(target, dt, phase != RoundPhase.SkateIn);
            }
            _goalie.Stand(ToVector(_shuffle.Shown));
            _goalie.Face((float)_turn.Shown, target);
            FeedPush(phase, phaseSeconds, set, dt, realDt);
            _goalie.SetProgress(set);
        }

        RinkPoint _pushFrom;         // where the shuffle stood at the last FeedPush (its X change picks the pushing skate)
        double _pushPeak;            // the shuffle's top speed since it last left rest (the Speed share is against it)
        Skate _pushSkate = Skate.Right;

        /// <summary>
        /// The skate push (#126) the goalie shows this frame, once per frame (Step): on the skate-in and slow-down from the shuffle
        /// (the skate from the sign of the frame's X change, the kind from the shuffle); in a Late Settle's window from the Spot's
        /// own shape (SetUps.Spot: a push then a glide over LateSettleSeconds); in the result hold from the Save Reaction's slide, none
        /// for Pads (the pose sets the legs itself). Seen in the goalie view as a pose, never as a change of where he is.
        /// </summary>
        void FeedPush(RoundPhase phase, double phaseSeconds, float set, float dt, float realDt)
        {
            RoundSettings settings = _round.Settings;
            PushShown shown = PushShown.None;
            if (phase == RoundPhase.SkateIn || phase == RoundPhase.SlowDown)
            {
                RinkPoint now = _shuffle.Shown;
                double dx = now.X - _pushFrom.X, dz = now.Z - _pushFrom.Z;
                _pushFrom = now;
                if (Math.Abs(dx) > 1e-6) _pushSkate = dx < 0 ? Skate.Right : Skate.Left;
                _pushPeak = _shuffle.Phase == PushPhase.Done ? 0 : Math.Max(_pushPeak, _shuffle.Speed);
                // Only a move that goes sideways (not a depth move along Z), and only a real one: a micro-move or the slow drift with
                // the angle is under the peak speed of the smallest push, and shows none.
                bool sideways = Math.Abs(dx) >= 0.5 * Math.Sqrt(dx * dx + dz * dz);
                if (_shuffle.Phase != PushPhase.Done && sideways && _pushPeak >= new SkatePush(PushShown.MinMetres, settings).PeakSpeed)
                    shown = new PushShown(_shuffle.Kind, _pushSkate, _shuffle.Phase, _shuffle.Speed / _pushPeak);
            }
            else
            {
                _pushFrom = _shuffle.Shown;
                _pushPeak = 0;
                if (phase == RoundPhase.ShootingWindow)
                {
                    // Only a Late Settle slides in the window: he comes back toward the angle from his post's side (the Spot's shape).
                    SetUp setUp = _round.SetUp;
                    if (setUp.Kind == SetUpKind.LateSettle && setUp.Toward.HasValue)
                    {
                        double move = setUp.Toward.Value == Post.Left ? settings.LateSettleMetres : -settings.LateSettleMetres;
                        shown = PushShown.AtProgress(move, phaseSeconds / Math.Max(1e-6, settings.LateSettleSeconds), settings);
                    }
                }
                else if (phase == RoundPhase.ResultHold && _round.LastRace.HasValue && _round.LastRace.Value.SaveReaction != SaveReaction.Pads)
                    shown = PushShown.AtProgress(_goalie.ReactionSlideMetres, set, settings);
            }
            // Each push is stepped on the clock of the move that feeds it. The shuffle moves on game (scaled) time, so its pose does too.
            // The Late Settle's Spot and the Save Reaction's set run on the Round's real clock (the window is slowed to a quarter by
            // Time.timeScale), so those poses step on real seconds.
            _goalie.ShowPush(shown, phase == RoundPhase.SkateIn || phase == RoundPhase.SlowDown ? dt : realDt);
        }

        static Vector3 ToVector(RinkPoint p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);

        /// <summary>Places the camera: the shooter's eye on the skate-in at (x, distance out), drifted on toward the net.</summary>
        void PlaceCamera(float x, float distance) => PlaceShooterCamera(_camera.transform, x, distance, _drift, _gameTime);

        /// <summary>
        /// Places the shooter's camera: the eye on the skate-in at (x, distance out), drifted on toward the net by drift metres
        /// (capped short of the net), with the stride bob and roll at gameSeconds. The picture test uses it too (PictureTestTool).
        /// </summary>
        public static void PlaceShooterCamera(Transform camera, float x, float distance, float drift, float gameSeconds)
        {
            // Unity: the Net Face is at z = 0 and the shooter stands at -z.
            var ground = new Vector3(x, 0f, -distance);
            var toNet = new Vector3(-x, 0f, distance);
            float along = (float)Breakaway.CappedDrift(drift, toNet.magnitude);
            ground += toNet.normalized * along;

            float bob = (float)Breakaway.Bob(gameSeconds);
            camera.position = new Vector3(ground.x, (float)Breakaway.EyeHeight + bob, ground.z);
            camera.LookAt(NetCentre);
            float rollDegrees = (float)Breakaway.Roll(gameSeconds) * Mathf.Rad2Deg;
            camera.Rotate(0f, 0f, rollDegrees, Space.Self);
        }

        /// <summary>Metres above the Video Board's centre the Replay's look-up aims (7 m was right for the old board; the tight window is 6.5 to 7 m for this one: lower and the net and goalie show in the view, higher and the board's lower edge leaves it).</summary>
        public const float ReplayLookLift = 6.75f;

        /// <summary>
        /// Where the camera looks for the Replay: ReplayLookLift above the Video Board's centre, so the whole board shows with the
        /// net and the goalie (who plays the Replay back) kept below the view from every Attack Side.
        /// </summary>
        public static readonly Vector3 ReplayLookTarget =
            new Vector3((float)RinkLayout.VideoBoard.X, (float)RinkLayout.VideoBoard.Y + ReplayLookLift, (float)RinkLayout.VideoBoard.Z);

        /// <summary>Turns the camera from where it looks (the net) toward target (the Replay look-up's aim point: ReplayLookTarget) by amount (0..1).</summary>
        public static void LookUpTo(Transform camera, Vector3 target, float amount)
        {
            if (amount <= 0f) return;
            camera.rotation = Quaternion.Slerp(camera.rotation, Quaternion.LookRotation(target - camera.position), Mathf.Clamp01(amount));
        }

        /// <summary>The slow-down's grey, high-contrast, vignetted look: a global Volume under parent, weight 0 (off). The picture test uses it too.</summary>
        public static Volume MakeLookVolume(Transform parent)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var colour = profile.Add<ColorAdjustments>(true);
            colour.saturation.Override(-70f);
            colour.contrast.Override(15f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.45f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(Color.black);

            var volumeObject = new GameObject("Slow-mo Look");
            volumeObject.transform.SetParent(parent);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            volume.weight = 0f;
            return volume;
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
        }

        // ---- Shooting window ring (IMGUI, drawn after post-processing so it stays red) ----

        /// <summary>The black fade over the camera cut between Shots (#129), drawn over the HUD too (a low GUI depth draws last).</summary>
        void DrawFade()
        {
            GUI.depth = -1000;
            if (Event.current.type != EventType.Repaint || _fade.Amount <= 0) return;
            Color before = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, (float)_fade.Amount);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = before;
        }

        void OnGUI()
        {
            if (_round == null) return;
            DrawFade();
            if (_round.Phase != RoundPhase.ShootingWindow || Event.current.type != EventType.Repaint) return;

            float fraction = (float)Breakaway.RingFraction(_round.WindowSecondsLeft, _round.Settings);
            Vector3 centre = _camera.WorldToScreenPoint(NetCentre);
            Vector3 edge = _camera.WorldToScreenPoint(NetRightEdge);
            if (centre.z <= 0f) return;
            float radius = Mathf.Abs(edge.x - centre.x) * 1.5f;
            var guiCentre = new Vector2(centre.x, Screen.height - centre.y);

            // Drains clockwise from 12 o'clock: the dots that remain run from the top round to the fraction left.
            int dots = Mathf.Max(60, Mathf.CeilToInt(2f * Mathf.PI * radius / (RingDotSize * 0.6f)));
            int shown = Mathf.CeilToInt(dots * fraction);
            var old = GUI.color;
            GUI.color = RingColour;
            for (int i = 0; i < shown; i++)
            {
                float angle = (i / (float)dots) * Mathf.PI * 2f;
                var p = guiCentre + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * radius;
                GUI.DrawTexture(new Rect(p.x - RingDotSize / 2f, p.y - RingDotSize / 2f, RingDotSize, RingDotSize), Texture2D.whiteTexture);
            }
            GUI.color = old;
        }
    }
}
