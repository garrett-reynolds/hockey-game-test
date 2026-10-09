using HockeyGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HockeyGame
{
    /// <summary>
    /// Owns the screen flow (start screen, Round, end screen), ticks it with unscaled time, builds the rink,
    /// takes key / click / Test Shot input and draws the HUD. The scene only needs this component on an
    /// empty GameObject.
    /// </summary>
    public sealed class RoundDriver : MonoBehaviour
    {
        const float NetCentreHeight = (float)(NetFaceMapping.HeightMetres / 2.0);

        sealed class SystemRandomSource : IRandomSource
        {
            readonly System.Random _random = new System.Random();
            public double Next() { return _random.NextDouble(); }
        }

        const string GameName = "HOCKEY GAME";

        ScreenFlow _flow;
        Round _round; // the flow's one Round; each start reuses it
        SaveData _save;
        SettingsScreen _settingsScreen; // made fresh each time F2 opens it (port list refreshed); null when closed
        GoalieView _goalie;
        ShotEffects _effects;
        UsbShotSource _usb; // real Shots from the electronics
        RoundSound _sound; // reads tick on/off, master volume and mute from _save each frame
        Camera _camera;
        Plane _netFacePlane;
        bool _testShotsOn;
        int _testShotSpeedKmh = TestShotSpeed.DefaultKmh;
        GUIStyle _big, _mid, _small;

        void Awake()
        {
            RinkBuilder.Build(out _camera);
            _goalie = new GoalieView(GameObject.Find("Rink").transform);
            _effects = new ShotEffects(GameObject.Find("Rink").transform, _camera);
            _netFacePlane = new Plane(Vector3.back, Vector3.zero); // Net Face plane: z = 0
            _save = SaveStore.Current;
            _testShotsOn = _save.TestShotOnAtStart;
            _flow = new ScreenFlow(new SystemRandomSource(), _save);
            _flow.BestScoreChanged += SaveStore.Save;
            _round = _flow.Round;
            _sound = new RoundSound(_camera.gameObject, _save, _round.Settings);
            gameObject.AddComponent<BreakawayView>().Init(_round, _camera, _goalie);
            _usb = gameObject.AddComponent<UsbShotSource>();
            _usb.Init(_save);
        }

        void Update()
        {
            _flow.Tick(Time.unscaledDeltaTime);

            // Real Shots count whether Test Shots are on or off; on the start or end screen one acts as a Press.
            while (_usb.TryTakeShot(out var shot))
                _flow.SubmitShot(shot.X, shot.Y, shot.SpeedKmh, false);

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            // Goalie shows for the whole Shot (SkateIn to ResultHold); hidden in Idle and RoundOver.
            var phaseNow = _round.Phase;
            _goalie.Show(phaseNow != RoundPhase.Idle && phaseNow != RoundPhase.RoundOver, _round.GoaliePose, _save.EasyMode);
            _effects.Update(_round, Time.unscaledDeltaTime);
            _sound.Update(_round, _effects.PuckArrivedThisFrame, Time.unscaledDeltaTime);

            bool playing = _flow.Screen == FlowScreen.Playing;
            bool pressed = false; // key or click outside a Round: at most one Press per frame

            if (keyboard != null)
            {
                if (keyboard.f2Key.wasPressedThisFrame)
                {
                    _flow.ToggleSettings(); // does nothing during a Round or on the end screen
                    _settingsScreen = _flow.SettingsOpen ? new SettingsScreen(_save) : null;
                }
                else if (keyboard.f1Key.wasPressedThisFrame)
                    _testShotsOn = !_testShotsOn;
                else if (_flow.SettingsOpen)
                {
                    if (_settingsScreen.UpdateKeys(keyboard))
                        SaveStore.Save();
                }
                else if (!playing && keyboard.anyKey.wasPressedThisFrame)
                    pressed = true;
            }

            if (_flow.SettingsOpen)
                return; // the settings screen takes no clicks, wheel or Shots

            if (mouse != null)
            {
                if (_testShotsOn)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (scroll != 0f)
                        _testShotSpeedKmh = TestShotSpeed.Step(_testShotSpeedKmh, scroll > 0f ? 1 : -1);
                }

                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (playing && _testShotsOn)
                        SubmitTestShot(mouse.position.ReadValue());
                    else if (!playing)
                        pressed = true; // a click starts or leaves, even with Test Shots off
                }
            }

            if (pressed)
                _flow.Press();
        }

        void SubmitTestShot(Vector2 screenPosition)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            if (!_netFacePlane.Raycast(ray, out float distance)) return;
            Vector3 hit = ray.GetPoint(distance);
            NetFaceMapping.FromOffsetFromCentre(hit.x, hit.y - NetCentreHeight, out double x, out double y);
            _flow.SubmitShot(x, y, _testShotSpeedKmh, true);
        }

        // ---- HUD (IMGUI) ----

        void OnGUI()
        {
            if (_flow == null) return;
            if (_big == null) MakeStyles();
            float w = Screen.width, h = Screen.height;

            if (_flow.Screen != FlowScreen.Start) // the start screen shows no old Round
            {
                DrawDots(w);
                GUI.Label(new Rect(0, 70, w, 40), "Goals: " + ShownGoals(), _mid);
            }

            string testShots = _testShotsOn
                ? "Test Shots: ON  (" + _testShotSpeedKmh + " km/h, wheel to change, F1 off)"
                : "Test Shots: OFF  (F1 on)";
            GUI.Label(new Rect(10, h - 34, w, 30), testShots, _small);

            if (_flow.SettingsOpen)
            {
                _settingsScreen.Draw(w, h, _big, _small);
                return;
            }

            string phase = _flow.Screen == FlowScreen.Playing ? _round.Phase.ToString() : _flow.Screen + " screen";
            if (_flow.Screen == FlowScreen.Playing)
                phase += "  -  Shot " + _round.ShotNumber + " of " + Round.ShotsPerRound;
            GUI.Label(new Rect(10, 10, w / 3f, 30), phase, _small);

            switch (_round.Phase)
            {
                case RoundPhase.ShootingWindow:
                    GUI.Label(new Rect(0, h * 0.2f, w, 60), _round.WindowSecondsLeft.ToString("0.0") + " s", _big);
                    break;
                case RoundPhase.ResultHold:
                    if (_round.LastResult.HasValue && _round.ResultShowing) // lands with the puck
                    {
                        GUI.Label(new Rect(0, h * 0.2f, w, 60), ResultText(_round.LastResult.Value.Kind), _big);
                        string speed = ShotResultText.Speed(_round.LastResult.Value);
                        if (speed != null) GUI.Label(new Rect(0, h * 0.2f + 60, w, 60), speed, _big);
                    }
                    break;
            }

            switch (_flow.Screen)
            {
                case FlowScreen.Start:
                    GUI.Label(new Rect(0, h * 0.2f, w, 60), GameName, _big);
                    GUI.Label(new Rect(0, h * 0.2f + 70, w, 40), "Best score: " + _save.BestScore + " / " + Round.ShotsPerRound, _mid);
                    GUI.Label(new Rect(0, h * 0.2f + 120, w, 40), "Shoot or press any key", _mid);
                    GUI.Label(new Rect(0, h * 0.2f + 170, w, 40), _usb.StatusText, _mid);
                    GUI.Label(new Rect(10, h - 64, w, 30), "F2: settings", _small);
                    break;
                case FlowScreen.End:
                    GUI.Label(new Rect(0, h * 0.2f, w, 60), _round.Goals + " / " + Round.ShotsPerRound + " goals", _big);
                    string best = "Best score: " + _save.BestScore + " / " + Round.ShotsPerRound;
                    if (_round.IsNewBest) best = "New best! " + best;
                    GUI.Label(new Rect(0, h * 0.2f + 70, w, 40), best, _mid);
                    if (_flow.EndAcceptsInput)
                        GUI.Label(new Rect(0, h * 0.2f + 120, w, 40), "Shoot or press any key", _mid);
                    break;
            }
        }

        void DrawDots(float width)
        {
            const float size = 24f, gap = 12f;
            float total = Round.ShotsPerRound * size + (Round.ShotsPerRound - 1) * gap;
            float left = (width - total) / 2f;
            var results = _round.Results;
            int shown = results.Count - (_round.ResultShowing ? 0 : 1); // the Shot in flight shows on landing
            for (int i = 0; i < Round.ShotsPerRound; i++)
            {
                Color colour = i < shown ? DotColour(results[i]) : new Color(1f, 1f, 1f, 0.15f);
                var rect = new Rect(left + i * (size + gap), 36, size, size);
                var old = GUI.color;
                GUI.color = colour;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = old;
            }
        }

        /// <summary>Goals so far, not counting a Goal whose puck is still in flight.</summary>
        int ShownGoals()
        {
            bool goalInFlight = !_round.ResultShowing && _round.LastResult.HasValue
                                && _round.LastResult.Value.Kind == ShotResultKind.Goal;
            return _round.Goals - (goalInFlight ? 1 : 0);
        }

        static Color DotColour(ShotResultKind kind)
        {
            switch (kind)
            {
                case ShotResultKind.Goal: return new Color(0.2f, 0.8f, 0.2f);
                case ShotResultKind.Save: return new Color(0.9f, 0.2f, 0.2f);
                default: return new Color(0.6f, 0.6f, 0.6f);
            }
        }

        static string ResultText(ShotResultKind kind)
        {
            switch (kind)
            {
                case ShotResultKind.Goal: return "GOAL!";
                case ShotResultKind.Save: return "SAVE";
                case ShotResultKind.Miss: return "MISS";
                default: return "TOO LATE";
            }
        }

        void MakeStyles()
        {
            _big = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _mid = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            _big.normal.textColor = _mid.normal.textColor = _small.normal.textColor = Color.white;
        }
    }
}
