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
        GoalieView _goalie; // the goalie character, or the block goalie with no model or the forceGoalieBlocks developer flag
        readonly EasyGlow _easyGlow = new EasyGlow(); // the Gaps Easy mode glows, a few cells measured each frame
        ShotEffects _effects;
        UsbShotSource _usb; // real Shots from the electronics
        RoundSound _sound; // reads tick on/off, master volume and mute from _save each frame
        Camera _camera;
        RibbonBoards _ribbons; // follow the Arena Show
        Crowd _crowd; // follows the Arena Show (null without the crowd body file)
        Plane _netFacePlane;
        bool _testShotsOn;
        int _testShotSpeedKmh = TestShotSpeed.DefaultKmh;
        GUIStyle _big, _mid, _small, _scoreBox;
        const float ScoreBoxWidth = 330f;
        /// <summary>The Score Box's colours: placeholders until the Team Skin holds them.</summary>
        static readonly Color ScoreBoxBack = new Color(0.05f, 0.08f, 0.18f, 0.85f), ScoreBoxText = Color.white;

        void Awake()
        {
            FrameRate.Lock(); // 60 fps, VSync on (#85)
            FrameReadout.AddTo(gameObject); // dev-only GPU and CPU ms (Editor and development builds)
            RinkBuilder.Build(out _camera);
            _save = SaveStore.Current;
            Transform rink = GameObject.Find("Rink").transform;
            _ribbons = rink.GetComponentInChildren<RibbonBoards>();
            _crowd = rink.GetComponentInChildren<Crowd>();
            GoalieCharacter character = _save.ForceGoalieBlocks ? null : new GoalieCharacter(rink);
            _goalie = new GoalieView(rink, character);
            _effects = new ShotEffects(rink, _camera, null, _goalie);
            _netFacePlane = new Plane(Vector3.back, Vector3.zero); // Net Face plane: z = 0
            _testShotsOn = _save.TestShotOnAtStart;
            var settings = new RoundSettings(); // shared: the Round runs on it, the goalie-reach port reads the gear speeds from it
            _flow = new ScreenFlow(new SystemRandomSource(), _save, new GoalieCharacterReach(character, settings), settings);
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

            // The goalie character stands in the net on the start and end screens too, alive in the Ready Stance (#44); the block
            // goalie shows only for the Shot (SkateIn to ResultHold). Easy mode glows the Gaps from when he is set until the Shot
            // (EasyGlow), a hint only: the Round's Gaps, from the same goalie-reach port as the race.
            var phaseNow = _round.Phase;
            bool inShot = phaseNow != RoundPhase.Idle && phaseNow != RoundPhase.RoundOver;
            _goalie.Show(inShot || _goalie.HasCharacter);
            _goalie.ShowGaps(_easyGlow.Update(_save.EasyMode, _round));
            _effects.Update(_round, Time.unscaledDeltaTime);
            _sound.Update(_round, _effects.PuckArrivedThisFrame, Time.unscaledDeltaTime, _effects.ContactSound);
            ArenaState arena = ArenaShow.From(_flow); // the arena follows the Arena Show
            _ribbons.Show(arena, Time.unscaledTimeAsDouble);
            if (_crowd != null) _crowd.Show(arena, Time.unscaledTimeAsDouble);

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
                    {
                        SubmitTestShot(mouse.position.ReadValue());
                        _effects.Update(_round, 0f); // start the puck now so the result stays hidden in this frame's OnGUI
                    }
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
                DrawDots(w);

            ScoreBox scoreBox = ArenaShow.From(_flow).ScoreBox;
            if (scoreBox.Shown) // all Round, in the top-right corner like on TV
                GUI.Label(new Rect(w - ScoreBoxWidth - 10f, 10f, ScoreBoxWidth, 44f), scoreBox.Text, _scoreBox);

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
            var back = new Texture2D(1, 1);
            back.SetPixel(0, 0, ScoreBoxBack);
            back.Apply();
            _scoreBox = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _scoreBox.normal.background = back;
            _scoreBox.normal.textColor = ScoreBoxText;
        }
    }
}
