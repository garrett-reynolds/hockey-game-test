using System.Collections;
using HockeyGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HockeyGame
{
    /// <summary>
    /// Owns the screen flow (start screen, Round, end screen), ticks it with unscaled time, builds the rink,
    /// takes key / click / Test Shot input and draws the HUD. Shots fly by Unity physics (PhysicsShotEffects): after each
    /// physics step the puck's facts go to the Whistle rule, which gives the Round its Whistle. The scene only needs this
    /// component on an empty GameObject.
    /// </summary>
    public sealed class RoundDriver : MonoBehaviour
    {
        const float NetCentreHeight = (float)(NetFaceMapping.HeightMetres / 2.0);

        sealed class SystemRandomSource : IRandomSource
        {
            readonly System.Random _random = new System.Random();
            public double Next() { return _random.NextDouble(); }
        }

        ScreenFlow _flow;
        Round _round; // the flow's one Round; each start reuses it
        SaveData _save;
        SettingsScreen _settingsScreen; // the Settings page and start-screen button (UI Toolkit); Open() makes a fresh menu (port list refreshed)
        GoalieView _goalie; // the goalie character, or the block goalie with no model or the forceGoalieBlocks developer flag
        readonly EasyGlow _easyGlow = new EasyGlow(); // the Gaps Easy mode glows, a few cells measured each frame
        PhysicsShotEffects _effects; // the physics puck of each Shot, and the Round's Whistle
        UsbShotSource _usb; // real Shots from the electronics
        RoundSound _sound; // reads tick on/off, master volume and mute from _save each frame
        Camera _camera;
        RibbonBoards _ribbons; // follow the Arena Show
        RoofLights _roofLights; // pulse in the Goal Show and the new-best show
        WaterBottle _bottle; // knocked off the net by a hard Goal (#164)
        readonly BottleKnock _bottleKnock = new BottleKnock();
        Crowd _crowd; // follows the Arena Show (null without the crowd body file)
        VideoBoards _boards;   // follow the Arena Show
        StartShotsView _startShots; // the start screen's wide shots
        BreakawayView _breakaway; // the camera: looks up at the Video Board for the Replay
        ReplayView _replay;    // the Replay on both boards
        Plane _netFacePlane;
        bool _testShotsOn;
        int _testShotSpeedKmh = TestShotSpeed.DefaultKmh;
        GUIStyle _big, _mid, _small, _scoreBox;
        Texture2D _scoreBoxBack; // made with the styles; freed in OnDestroy
        Texture2D _logo; // the Shootout logo, made with the styles; freed in OnDestroy
        readonly LowerThirdView _lowerThird = new LowerThirdView(); // the TV bar along the bottom, on the whole Round (#141, look A)
        readonly LowerThirdWords _lowerThirdWords = new LowerThirdWords(); // its words, built only when the Round's numbers change (#151)
        ArenaState _show; // this frame's Arena Show, worked out once in Update and read by everything that frame, OnGUI too
        const float ScoreBoxWidth = 330f;
        TeamSkin _skin; // picked by the save file's teamSkin setting before the rink is built

        void Awake()
        {
            FrameRate.Lock(); // 60 fps, VSync on (#85)
            FrameReadout.AddTo(gameObject); // dev-only GPU and CPU ms (Editor and development builds)
            _save = SaveStore.Current;
            _skin = TeamSkin.Load(_save.TeamSkin);
            RinkBuilder.Build(out _camera, _skin);
            Transform rink = GameObject.Find("Rink").transform;
            _ribbons = rink.GetComponentInChildren<RibbonBoards>();
            _roofLights = new RoofLights(rink);
            _crowd = rink.GetComponentInChildren<Crowd>();
            _bottle = WaterBottle.In(rink);
            _boards = rink.GetComponentInChildren<VideoBoards>();
            GoalieCharacter character = _save.ForceGoalieBlocks ? null : new GoalieCharacter(rink);
            _goalie = new GoalieView(rink, character);
            _effects = new PhysicsShotEffects(rink, _camera, new SystemRandomSource(), _goalie);
            _netFacePlane = new Plane(Vector3.back, Vector3.zero); // Net Face plane: z = 0
            _testShotsOn = _save.TestShotOnAtStart;
            _settingsScreen = new SettingsScreen(_save);
            _settingsScreen.TestModeRequested = OpenTestMode;
            var settings = new RoundSettings(); // shared: the Round runs on it, the goalie-reach port reads the gear speeds from it
            settings.ViewFieldOfViewDegrees = _camera.fieldOfView; // the Shot starts just below this camera's view (Breakaway.ShotStart)
            _flow = new ScreenFlow(new SystemRandomSource(), _save, new GoalieCharacterReach(character, settings), settings);
            _flow.BestScoreChanged += SaveStore.Save;
            _round = _flow.Round;
            _sound = new RoundSound(_camera.gameObject, _save, _round.Settings, _skin);
            _breakaway = gameObject.AddComponent<BreakawayView>();
            _breakaway.Init(_round, _camera, _goalie, () => _effects.FlightSeconds); // the Shot's Flight Time, before and after the Whistle
            _startShots = new StartShotsView(_camera, (float)settings.ViewFieldOfViewDegrees);
            _replay = gameObject.AddComponent<ReplayView>();
            _replay.Init(_round, _boards, _camera, _goalie.Body);
            _replay.ReplayPhysicsPuck(() => _effects.Shot?.Puck); // a new Rigidbody each Shot: replayed on a stand-in, not in the track
            if (_bottle != null) _replay.ReplayBottle(_bottle); // the bottle falling off is in the Replay too (#164), on a stand-in
            _usb = gameObject.AddComponent<UsbShotSource>();
            _usb.Init(_save);
            StartCoroutine(AfterPhysicsSteps());
        }

        /// <summary>After each physics step (FixedUpdate's, at the Fixed Timestep, 1/120 s): the puck's facts to the Whistle rule.</summary>
        IEnumerator AfterPhysicsSteps()
        {
            var afterStep = new WaitForFixedUpdate(); // resumes after the step's physics, not before it as FixedUpdate does
            while (true)
            {
                yield return afterStep;
                _effects.AfterPhysicsStep(Time.fixedDeltaTime);
            }
        }

        /// <summary>PROTOTYPE keys (K: AI clips on/off, B: clip blend) that are never "any key" on the start and end screens.</summary>
        public static bool IsPrototypeKey(UnityEngine.InputSystem.Key key) =>
            key == UnityEngine.InputSystem.Key.K || key == UnityEngine.InputSystem.Key.B;

        /// <summary>True when a key other than a prototype key went down this frame.</summary>
        static bool PressedOtherThanPrototypeKeys(UnityEngine.InputSystem.Keyboard keyboard)
        {
            foreach (var control in keyboard.allKeys)
                if (control != null && control.wasPressedThisFrame && !IsPrototypeKey(control.keyCode)) return true;
            return false;
        }

        void Update()
        {
            _replay.RestoreLive(); // last frame's Replay poses go back before anything moves
            _flow.Tick(Time.unscaledDeltaTime);

            // Real Shots count whether Test Shots are on or off; on the start or end screen one acts as a Press.
            while (_usb.TryTakeShot(out var shot))
                _flow.SubmitShot(shot.X, shot.Y, shot.SpeedKmh, false);

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            // The goalie character stands in the net on the start and end screens too, alive in the Ready Stance (#44); the block
            // goalie shows only for the Shot (SkateIn to ResultHold). Easy mode glows the Gaps from when he is set until the Shot
            // (EasyGlow), a hint only: the Round's Gaps, from the same goalie-reach port as the race.
            _round.Settings.ApplySavedReplay(_save.ReplayOn); // the Replay setting (#145), read from the saved settings each frame; never on in Test Mode (#156)
            var phaseNow = _round.Phase;
            bool inShot = phaseNow != RoundPhase.Idle && phaseNow != RoundPhase.RoundOver;
            _goalie.Show(inShot || _goalie.HasCharacter);
            _goalie.ShowGaps(_easyGlow.Update(_save.EasyMode, _round));
            _effects.Update(_round, Time.unscaledDeltaTime);
            _show = ArenaShow.From(_flow); // the arena follows the Arena Show: once a frame, for every part
            ArenaState show = _show;
            _sound.Update(_flow, Time.unscaledDeltaTime, _effects.HitSound);
            _ribbons.Show(show, Time.unscaledTimeAsDouble);
            _boards.Show(show, Time.unscaledTimeAsDouble);
            _effects.GoalLight.SetOn(show.GoalShow.GoalLight); // the goal light's one driver: the Goal Show, from a GOAL!'s Whistle
            _roofLights.Show(show.GoalShow);
            if (_crowd != null) _crowd.Show(show, Time.unscaledTimeAsDouble);
            _breakaway.LookUp(show.CameraLookUp);
            _effects.CameraLookUp = show.CameraLookUp; // no hiding the puck while the camera looks up (the next physics steps see this look)
            _replay.Show(show);
            if (_bottle != null)
            {
                // A hard Goal knocks the bottle off; the next Round puts it back (#164). Both Dev settings are read here each frame: a change acts on the next Shot.
                _bottleKnock.SpeedKmh = _save.BottleKnockSpeedKmh;
                _bottle.Follow(_bottleKnock.Follow(_flow), (float)(_save.BottleJumpPercent / 100.0));
            }

            bool playing = _flow.Screen == FlowScreen.Playing;
            bool pressed = false; // key or click outside a Round: at most one Press per frame

            bool openedNow = false; // the page opened this frame: the click or key that opened it goes no further
            if (keyboard != null)
            {
                // Esc is a Settings key on the start screen only; on the end screen it is "any key" again. F2 never starts anything.
                if (_flow.TestMode && keyboard.escapeKey.wasPressedThisFrame)
                {
                    _flow.LeaveTestMode(); // Esc in Test Mode: back to the start screen (#156)
                    _show = ArenaShow.From(_flow);
                    return;
                }
                if (keyboard.f2Key.wasPressedThisFrame || (keyboard.escapeKey.wasPressedThisFrame && _flow.EscapeIsSettingsKey))
                {
                    _flow.ToggleSettings(); // does nothing during a Round or on the end screen
                    openedNow = _flow.SettingsOpen;
                    if (openedNow) _settingsScreen.Open(); else _settingsScreen.Close();
                }
                else if (keyboard.f1Key.wasPressedThisFrame)
                    _testShotsOn = !_testShotsOn;
                else if (!_flow.SettingsOpen && !playing && keyboard.anyKey.wasPressedThisFrame && PressedOtherThanPrototypeKeys(keyboard))
                    pressed = true;
            }

            // The Settings button on the start screen (a click on it never starts a Round).
            if (!_flow.SettingsOpen && mouse != null && mouse.leftButton.wasPressedThisFrame
                && _flow.Screen == FlowScreen.Start && _settingsScreen.PointerOverStartButton(mouse.position.ReadValue()))
            {
                _flow.OpenSettings();
                _settingsScreen.Open();
                openedNow = true;
            }
            _settingsScreen.ShowStartButton(_flow.Screen == FlowScreen.Start && !_flow.SettingsOpen);

            if (_flow.SettingsOpen)
            {
                // The page takes the keys and the mouse; no clicks, wheel or Shots reach the Round.
                if (!openedNow && _settingsScreen.UpdateInput(keyboard, mouse))
                    SaveStore.Save();
                return;
            }

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
                        _effects.Update(_round, 0f); // launch the puck on the Shot frame, as a real Shot (this one came in after the effects updated above)
                        _show = ArenaShow.From(_flow); // only on a Shot frame: OnGUI shows the Round as it is now
                    }
                    else if (!playing)
                        pressed = true; // a click starts or leaves, even with Test Shots off
                }
            }

            if (pressed)
            {
                _flow.Press();
                _show = ArenaShow.From(_flow); // only on a Press frame: OnGUI shows the new screen's Score Box at once
            }

            ShowStartShots();
        }

        // The Dev part's Test Mode button (#156): the page closes and the first Shot is in the shooting window at once.
        void OpenTestMode()
        {
            _flow.StartTestMode();
            _settingsScreen.Close();
        }

        // After the Press: on the press frame the screen is already Playing, so the camera is handed back and BreakawayView places it.
        void ShowStartShots() => _startShots.Step(_flow.Screen == FlowScreen.Start, Time.unscaledDeltaTime);

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
            if (_flow.SettingsOpen)
                return; // the Settings page (UI Toolkit) draws over the arena; no HUD text on top of it
            float w = Screen.width, h = Screen.height;

            if (TestModeHud.DotsShown(_flow)) // not on the start screen (no old Round), not in Test Mode (#157: clean view)
                DrawDots(w);

            ScoreBox scoreBox = _show.ScoreBox; // this frame's, from Update
            if (scoreBox.Shown) // all Round, in the top-right corner like on TV
                GUI.Label(ScoreBoxArea(w), scoreBox.Text, _scoreBox);

            string testShots = _testShotsOn
                ? "Test Shots: ON  (" + _testShotSpeedKmh + " km/h, wheel to change, F1 off)"
                : "Test Shots: OFF  (F1 on)";
            GUI.Label(StartScreenLayout.TestShots(w, h), testShots, _small);

            if (StartLogo.Shown(_flow)) // the Shootout logo (#140) and the dark strip its text lines sit on; not behind the settings screen
            {
                GUI.DrawTexture(StartScreenLayout.Logo(w, h), _logo, ScaleMode.StretchToFill, true);
                Rect strip = StartScreenLayout.TextStrip(w, h);
                Color was = GUI.color;
                GUI.color = StartLogoArt.StripBack;
                GUI.DrawTexture(strip, Texture2D.whiteTexture);
                GUI.color = LookC.Gold;
                GUI.DrawTexture(new Rect(strip.x, strip.y, strip.width, StartLogoArt.StripEdgeHeight), Texture2D.whiteTexture);
                GUI.color = was;
            }

            string phase = _flow.Screen == FlowScreen.Playing ? _round.Phase.ToString() : _flow.Screen + " screen";
            if (_flow.Screen == FlowScreen.Playing)
                phase += "  -  Shot " + _round.ShotNumber + " of " + _round.ShotCount;
            if (TestModeHud.LabelShown(_flow)) phase = TestModeHud.LabelText; // Test Mode (#157): the small label, no phase or Round Shot number
            GUI.Label(new Rect(10, 10, w / 3f, 30), phase, _small);

            switch (_round.Phase)
            {
                case RoundPhase.ShootingWindow:
                    if (!_round.TestMode) // Test Mode has no clock (#156)
                        GUI.Label(new Rect(0, h * 0.2f, w, 60), _round.WindowSecondsLeft.ToString("0.0") + " s", _big);
                    break;
                case RoundPhase.ResultHold:
                    if (_round.LastResult.HasValue && _round.ResultShowing) // from the Whistle
                    {
                        GUI.Label(new Rect(0, h * 0.2f, w, 60), ResultText(_round.LastResult.Value.Kind), _big);
                        string speed = ShotResultText.Speed(_round.LastResult.Value);
                        if (speed != null) GUI.Label(new Rect(0, h * 0.2f + 60, w, 60), speed, _big);
                    }
                    break;
            }

            if (LowerThird.Shown(_flow)) // the whole Round (aim, flight, result, Replay); not on the Start or End screen, not in Test Mode
                _lowerThird.Draw(w, h, _lowerThirdWords.Get(_round));

            switch (_flow.Screen)
            {
                case FlowScreen.Start:
                    GUI.Label(StartScreenLayout.BestScore(w, h), "Best score: " + _save.BestScore + " / " + _flow.ShotsPerRound, _mid);
                    GUI.Label(StartScreenLayout.Prompt(w, h), "Shoot or press any key", _mid);
                    GUI.Label(StartScreenLayout.UsbStatus(w, h), _usb.StatusText, _mid);
                    break;
                case FlowScreen.End: // the score and best score are on the Video Board
                    if (_flow.EndAcceptsInput)
                        GUI.Label(new Rect(0, h * 0.2f + 120, w, 40), "Shoot or press any key", _mid);
                    break;
            }
        }

        void DrawDots(float width)
        {
            const float size = 24f, gap = 12f;
            int count = _round.ShotCount;
            float total = count * size + (count - 1) * gap;
            float left = (width - total) / 2f;
            var results = _round.Results;
            int shown = results.Count; // the Shot in flight has no result yet: it shows at the Whistle
            for (int i = 0; i < count; i++)
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
            var back = _scoreBoxBack = new Texture2D(1, 1);
            (Color boxBack, Color boxText) = ScoreBoxColours(_skin);
            back.SetPixel(0, 0, boxBack);
            back.Apply();
            _scoreBox = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _scoreBox.normal.background = back;
            _scoreBox.normal.textColor = boxText;
            _logo = StartLogoArt.Make(); // about 0.35 s once, with the styles, not in the middle of play
        }

        void OnDestroy()
        {
            _effects?.Dispose();
            _goalie?.Dispose();
            if (_scoreBoxBack != null) SceneObjects.DestroySafely(_scoreBoxBack);
            if (_logo != null) SceneObjects.DestroySafely(_logo);
        }

        /// <summary>Where the Score Box is drawn on a screen this wide: the top-right corner.</summary>
        public static Rect ScoreBoxArea(float width) => new Rect(width - ScoreBoxWidth - 10f, 10f, ScoreBoxWidth, 44f);

        /// <summary>The Score Box's background and words colours: the skin's (null: the League Skin's).</summary>
        public static (Color Back, Color Text) ScoreBoxColours(TeamSkin skin)
        {
            skin = TeamSkin.OrLeague(skin);
            return (skin.ScoreBoxBack, skin.ScoreBoxText);
        }
    }
}
