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
        Camera _camera;
        GoalieView _goalie;
        Volume _look;
        RoundPhase _lastPhase = RoundPhase.Idle;
        float _drift;      // metres drifted toward the net since the skate-in ended (scaled time)
        float _gameTime;   // game seconds since the skate-in began (stride bob and roll; the stride sounds use the same clock)
        readonly GoalieTurn _turn = new GoalieTurn(0.0); // the goalie squaring up to the shooter
        GoalieShuffle _shuffle;  // the goalie playing the angle across the crease
        SkateInShuffles _steps;  // ... in shuffles on the skate-in (#70)
        RinkPoint _puckOnStick;  // the puck his head follows before the Shot (#70)
        float _lastTarget;       // the facing that squares him up to the puck, as last worked out
        float _life;       // the goalie's Stance Life clock: real seconds, never reset, so it never jumps

        public void Init(Round round, Camera shooterCamera, GoalieView goalie)
        {
            _round = round;
            _camera = shooterCamera;
            _goalie = goalie;
            _camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            _look = MakeLookVolume();
            _shuffle = new GoalieShuffle(new RinkPoint(0, 0, -round.Settings.NormalDepthMetres)); // centred, at his normal depth
            _steps = new SkateInShuffles(_shuffle.Shown);
            _goalie.Stand(ToVector(_shuffle.Shown));
        }

        void LateUpdate()
        {
            if (_round != null) Step(Time.deltaTime, Time.unscaledDeltaTime);
        }

        /// <summary>One frame: dt is the game (scaled) seconds since the last one, realDt the real seconds. LateUpdate runs it each frame.</summary>
        public void Step(float dt, float realDt)
        {
            RoundPhase phase = _round.Phase;
            double phaseSeconds = _round.PhaseSecondsElapsed;
            bool skateInStarts = phase == RoundPhase.SkateIn && _lastPhase != RoundPhase.SkateIn;
            if (skateInStarts)
            {
                _drift = 0f;
                _gameTime = 0f;
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
                _round.AwaitingWhistle ? double.PositiveInfinity : _round.ResultDelaySeconds, _round.LastRace); // the flight is not known before the Whistle
            if (phase == RoundPhase.Idle || phase == RoundPhase.RoundOver)
            {
                _goalie.Face((float)_turn.Shown, (float)_turn.Shown); // keeps facing the last shooter
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
            _goalie.SetProgress(set);
        }

        static Vector3 ToVector(RinkPoint p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);

        /// <summary>Places the camera: the shooter's eye on the skate-in at (x, distance out), drifted on toward the net.</summary>
        void PlaceCamera(float x, float distance)
        {
            // Unity: the Net Face is at z = 0 and the shooter stands at -z.
            var ground = new Vector3(x, 0f, -distance);
            var toNet = new Vector3(-x, 0f, distance);
            float along = (float)Breakaway.CappedDrift(_drift, toNet.magnitude);
            ground += toNet.normalized * along;

            float bob = (float)Breakaway.Bob(_gameTime);
            var t = _camera.transform;
            t.position = new Vector3(ground.x, (float)Breakaway.EyeHeight + bob, ground.z);
            t.LookAt(NetCentre);
            float rollDegrees = (float)Breakaway.Roll(_gameTime) * Mathf.Rad2Deg;
            t.Rotate(0f, 0f, rollDegrees, Space.Self);
        }

        Volume MakeLookVolume()
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
            volumeObject.transform.SetParent(transform);
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

        void OnGUI()
        {
            if (_round == null || _round.Phase != RoundPhase.ShootingWindow || Event.current.type != EventType.Repaint) return;

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
