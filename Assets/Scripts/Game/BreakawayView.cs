using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HockeyGame
{
    /// <summary>
    /// The breakaway feel, driven by what the Round core reports: the camera skates in and curls toward the net,
    /// the world slows down with a grey, high-contrast, vignetted look while the goalie sets, and a red ring
    /// around the net drains over the shooting window. A Shot snaps time and look back to normal.
    /// Sets Time.timeScale; the Round core keeps ticking on unscaled real time.
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
        float _gameTime;   // scaled seconds since the skate-in began (stride bob and roll)

        public void Init(Round round, Camera shooterCamera, GoalieView goalie)
        {
            _round = round;
            _camera = shooterCamera;
            _goalie = goalie;
            _camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            _look = MakeLookVolume();
        }

        void LateUpdate()
        {
            if (_round == null) return;

            RoundPhase phase = _round.Phase;
            double phaseSeconds = _round.PhaseSecondsElapsed;
            if (phase == RoundPhase.SkateIn && _lastPhase != RoundPhase.SkateIn)
            {
                _drift = 0f;
                _gameTime = 0f;
            }
            _lastPhase = phase;

            // Time and look come from the phase every frame, so a Shot (window -> result hold) snaps both back at once.
            float timeScale = (float)Breakaway.TimeScale(phase, phaseSeconds);
            Time.timeScale = timeScale;
            _look.weight = (float)Breakaway.LookAmount(timeScale);

            _goalie.SetProgress((float)Breakaway.GoalieSet(phase, phaseSeconds));

            if (phase == RoundPhase.Idle || phase == RoundPhase.RoundOver) return; // camera stays where it is

            float dt = Time.deltaTime; // scaled
            _gameTime += dt;
            AttackSide side = _round.AttackSide;
            if (phase == RoundPhase.SlowDown || phase == RoundPhase.ShootingWindow)
                _drift += dt * (float)Breakaway.SkateSpeed(side);

            double u = phase == RoundPhase.SkateIn ? Breakaway.SkateProgress(phaseSeconds) : 1.0;
            Breakaway.SkatePoint(side, u, out double x, out double distance);
            PlaceCamera((float)x, (float)distance);
        }

        void PlaceCamera(float x, float distance)
        {
            // Unity: the Net Face is at z = 0 and the shooter stands at -z.
            var ground = new Vector3(x, 0f, -distance);
            var toNet = new Vector3(-x, 0f, distance);
            float along = (float)Breakaway.CappedDrift(_drift, toNet.magnitude);
            ground += toNet.normalized * along;

            float bob = (float)Breakaway.StrideBob * Mathf.Sin(_gameTime * 10f);
            var t = _camera.transform;
            t.position = new Vector3(ground.x, (float)Breakaway.EyeHeight + bob, ground.z);
            t.LookAt(NetCentre);
            float rollDegrees = (float)Breakaway.RollRadians * Mathf.Sin(_gameTime * 5f) * Mathf.Rad2Deg;
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

            float fraction = (float)Breakaway.RingFraction(_round.WindowSecondsLeft);
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
