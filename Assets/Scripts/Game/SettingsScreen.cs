using System.Collections.Generic;
using HockeyGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HockeyGame
{
    /// <summary>
    /// The Settings page and the start screen's Settings button, drawn with UI Toolkit in look C (Resources/UI/LookC.uss).
    /// A thin view: a SettingsMenu holds every value and rule; this builds the cards for its current part, shows its values,
    /// and turns keys and the mouse into SettingsMenu calls. The mouse is read here from Mouse.current and hit-tested against
    /// the elements (no EventSystem needed), the same way RoundDriver reads its clicks.
    /// Open() makes a fresh SettingsMenu each time, so the serial port list is current.
    /// </summary>
    public sealed class SettingsScreen
    {
        const string SheetPath = "UI/LookC";
        const float PanelWidth = 1920f, PanelHeight = 1080f;

        enum HitKind { Row, Slider, ToggleOn, ToggleOff, Cell }

        sealed class Hit
        {
            public VisualElement El;
            public HitKind Kind;
            public int Row;
        }

        sealed class RowView
        {
            public VisualElement Row, Fill, Thumb, Track;
            public Label Value, OnCell, OffCell;
        }

        readonly SaveData _save;
        readonly GameObject _object;
        readonly VisualElement _root;
        readonly VisualElement _page, _content, _startButton;
        const float StripeDegrees = -18f, TabSlant = 24f; // the header stripes' turn and the tabs' lean (tan 18 degrees x 76 px)
        readonly VisualElement _tabPlayer, _tabDev;
        readonly Label _reset, _hints;
        readonly List<SlantShape> _tabShapes = new List<SlantShape>();
        readonly List<RowView> _rows = new List<RowView>();
        readonly List<Hit> _hits = new List<Hit>();
        SettingsMenu _menu;
        int _dragRow = -1;
        SettingsPart _shownPart;

        public SettingsScreen(SaveData save)
        {
            _save = save;
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int((int)PanelWidth, (int)PanelHeight);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            settings.sortingOrder = 10;
            settings.clearColor = false;

            _object = new GameObject("Settings page");
            var document = _object.AddComponent<UIDocument>();
            document.panelSettings = settings;
            _root = document.rootVisualElement;
            _root.AddToClassList("lc-root");
            _root.pickingMode = PickingMode.Ignore;
            var sheet = Resources.Load<StyleSheet>(SheetPath);
            if (sheet != null) _root.styleSheets.Add(sheet);
            else Debug.LogWarning("Settings page: style sheet Resources/" + SheetPath + " not found.");

            _page = new VisualElement();
            _page.AddToClassList("lc-page");
            _page.pickingMode = PickingMode.Ignore;
            var top = Add(_page, "lc-topbar");
            // The header stripes turn -18 degrees about (900, 60) in the 1920 x 120 header, as in the #137 prototype.
            var stripes = new VisualElement();
            stripes.AddToClassList("lc-stripes");
            stripes.pickingMode = PickingMode.Ignore;
            var pivot = new Vector2(900f, 60f);
            stripes.Add(SlantShape.Stripe("lc-stripe-red", 8f, 14f, StripeDegrees, pivot));
            stripes.Add(SlantShape.Stripe("lc-stripe-white", 26f, 4f, StripeDegrees, pivot));
            stripes.Add(SlantShape.Stripe("lc-stripe-gold", 100f, 10f, StripeDegrees, pivot));
            top.Add(stripes);
            Add(top, "lc-title", "SETTINGS");
            Add(top, "lc-title-stripe");
            _tabPlayer = MakeTab(top, "lc-tab-player", "PLAYER");
            _tabDev = MakeTab(top, "lc-tab-dev", "DEV");
            _content = Add(_page, "lc-content");
            _content.style.position = Position.Absolute;
            var bottom = Add(_page, "lc-bottombar");
            _reset = Add(bottom, "lc-reset", "");
            _hints = Add(bottom, "lc-hints", "ESC  CLOSE      TAB  PLAYER / DEV      R  RESET");
            _page.AddToClassList("lc-hidden");
            _root.Add(_page);

            _startButton = new VisualElement();
            _startButton.AddToClassList("lc-start-button");
            Add(_startButton, "lc-start-text", "SETTINGS");
            Add(_startButton, "lc-start-key", "ESC");
            _startButton.AddToClassList("lc-hidden");
            _root.Add(_startButton);
        }

        // A tab: a slanted white outline (shown when the tab is on), a slanted fill, and upright text on top.
        VisualElement MakeTab(VisualElement parent, string side, string text)
        {
            var tab = new VisualElement();
            tab.AddToClassList("lc-tab");
            tab.AddToClassList(side);
            tab.pickingMode = PickingMode.Ignore;
            var border = SlantShape.Parallelogram("lc-tab-border", TabSlant + 6f);
            var fill = SlantShape.Parallelogram("lc-tab-fill", TabSlant);
            tab.Add(border);
            tab.Add(fill);
            Add(tab, "lc-tab-text", text);
            _tabShapes.Add(border);
            _tabShapes.Add(fill);
            parent.Add(tab);
            return tab;
        }

        static Label Add(VisualElement parent, string classes, string text = null)
        {
            var label = new Label(text ?? "");
            foreach (var c in classes.Split(' ')) label.AddToClassList(c);
            label.pickingMode = PickingMode.Ignore;
            parent.Add(label);
            return label;
        }

        /// <summary>The page's root (the Settings page and the start button), for tests and tools.</summary>
        public VisualElement Root => _root;

        public SettingsMenu Menu => _menu;

        /// <summary>Raised when the Dev part's Test Mode button is pressed (#156). The game closes the page and opens Test Mode.</summary>
        public System.Action TestModeRequested;

        /// <summary>Opens the page on the Player part, with a fresh port list.</summary>
        public void Open()
        {
            _menu = new SettingsMenu(_save, PortNames(), () => TestModeRequested?.Invoke());
            _dragRow = -1;
            Rebuild();
            _page.RemoveFromClassList("lc-hidden");
        }

        public void Close()
        {
            _dragRow = -1;
            _page.AddToClassList("lc-hidden");
        }

        /// <summary>The start screen's Settings button shows only on the start screen with the page closed.</summary>
        public void ShowStartButton(bool shown)
        {
            if (shown) _startButton.RemoveFromClassList("lc-hidden");
            else _startButton.AddToClassList("lc-hidden");
        }

        /// <summary>True when the pointer is over the visible start-screen Settings button.</summary>
        public bool PointerOverStartButton(Vector2 screenPosition)
        {
            return !_startButton.ClassListContains("lc-hidden") && Over(_startButton, screenPosition);
        }

        /// <summary>
        /// Keys and the mouse on the open page. True when a setting changed (save it).
        /// Tab, R, Up/Down, Left/Right and Enter go to the SettingsMenu; a click picks a tab, the Reset button, a row's control.
        /// </summary>
        public bool UpdateInput(Keyboard keyboard, Mouse mouse)
        {
            bool changed = false;
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.wasPressedThisFrame) changed = _menu.Press(SettingsKey.Up);
                else if (keyboard.downArrowKey.wasPressedThisFrame) changed = _menu.Press(SettingsKey.Down);
                else if (keyboard.leftArrowKey.wasPressedThisFrame) changed = _menu.Press(SettingsKey.Left);
                else if (keyboard.rightArrowKey.wasPressedThisFrame) changed = _menu.Press(SettingsKey.Right);
                else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                    changed = _menu.Press(SettingsKey.Enter);
                else if (keyboard.tabKey.wasPressedThisFrame) _menu.Press(SettingsKey.Tab);
                else if (keyboard.rKey.wasPressedThisFrame) changed = _menu.Press(SettingsKey.Reset);
            }
            if (mouse != null) changed |= UpdateMouse(mouse);
            Sync();
            return changed;
        }

        bool UpdateMouse(Mouse mouse) =>
            Pointer(mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame, mouse.leftButton.isPressed);

        /// <summary>
        /// The pointer on the open page, in screen pixels (y up). pressed: the button went down this frame; down: it is held.
        /// True when a setting changed. UpdateInput feeds it from the mouse; a tool can feed it directly.
        /// </summary>
        public bool Pointer(Vector2 at, bool pressed, bool down)
        {
            bool changed = false;
            if (pressed)
            {
                if (Over(_tabPlayer, at)) _menu.ShowPart(SettingsPart.Player);
                else if (Over(_tabDev, at)) _menu.ShowPart(SettingsPart.Dev);
                else if (Over(_reset, at)) changed = _menu.ResetPart();
                else changed = Click(at);
            }
            if (_dragRow >= 0)
            {
                if (down)
                    changed |= DragTo(_dragRow, at);
                else
                    _dragRow = -1;
            }
            Sync();
            return changed;
        }

        // A press on a row: the control under the pointer wins over the row itself.
        bool Click(Vector2 at)
        {
            for (int i = _hits.Count - 1; i >= 0; i--)
            {
                var hit = _hits[i];
                if (!Over(hit.El, at)) continue;
                _menu.Select(hit.Row);
                switch (hit.Kind)
                {
                    case HitKind.Slider:
                        _dragRow = hit.Row;
                        return DragTo(hit.Row, at);
                    case HitKind.ToggleOn:
                        return _menu.Value(hit.Row) != "ON" && _menu.Activate();
                    case HitKind.ToggleOff:
                        return _menu.Value(hit.Row) != "OFF" && _menu.Activate();
                    case HitKind.Cell:
                        return _menu.Kind(hit.Row) == SettingsRowKind.Choice ? _menu.Change(1) : _menu.Activate();
                }
                return false; // the row itself: selected
            }
            return false;
        }

        bool DragTo(int row, Vector2 at)
        {
            Rect track = _rows[row].Track.worldBound;
            if (!(track.width > 0f)) return false;
            return _menu.SetFraction(row, (ToPanel(at).x - track.x) / track.width);
        }

        Vector2 ToPanel(Vector2 screen)
        {
            // Screen y counts up from the bottom; the panel's counts down from the top.
            return RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
        }

        bool Over(VisualElement el, Vector2 screen)
        {
            if (_root.panel == null) return false;
            return el.worldBound.Contains(ToPanel(screen));
        }

        // After input: a switched part is built again; otherwise the values and the selected row are refreshed.
        void Sync()
        {
            if (_menu.Part != _shownPart) Rebuild();
            else Refresh();
        }

        void Rebuild()
        {
            _shownPart = _menu.Part;
            _content.Clear();
            _rows.Clear();
            _hits.Clear();
            _tabPlayer.EnableInClassList("lc-tab-on", _menu.Part == SettingsPart.Player);
            _tabDev.EnableInClassList("lc-tab-on", _menu.Part == SettingsPart.Dev);
            foreach (var shape in _tabShapes) shape.MarkDirtyRepaint(); // the fill colour follows the tab's class
            _reset.text = _menu.ResetLabel;

            // Consecutive rows with one heading make one card. The last card goes in the right-hand column, the rest in the left.
            var groups = new List<(string name, int first, int count)>();
            for (int i = 0; i < _menu.RowCount; i++)
            {
                if (groups.Count > 0 && groups[groups.Count - 1].name == _menu.Group(i))
                    groups[groups.Count - 1] = (groups[groups.Count - 1].name, groups[groups.Count - 1].first, groups[groups.Count - 1].count + 1);
                else
                    groups.Add((_menu.Group(i), i, 1));
            }
            var left = new VisualElement();
            left.AddToClassList("lc-column");
            left.AddToClassList("lc-left");
            var right = new VisualElement();
            right.AddToClassList("lc-column");
            right.AddToClassList("lc-right");
            _content.Add(left);
            _content.Add(right);
            for (int g = 0; g < groups.Count; g++)
            {
                var card = new VisualElement();
                card.AddToClassList("lc-card");
                card.AddToClassList("lc-c" + (g % 3));
                Add(card, "lc-card-head", groups[g].name);
                for (int r = 0; r < groups[g].count; r++)
                    card.Add(BuildRow(groups[g].first + r));
                (g == groups.Count - 1 ? right : left).Add(card);
            }
            Refresh();
        }

        VisualElement BuildRow(int i)
        {
            var view = new RowView { Row = new VisualElement() };
            view.Row.AddToClassList("lc-row");
            Add(view.Row, "lc-row-label", _menu.Label(i));
            var control = new VisualElement();
            control.AddToClassList("lc-control");
            control.pickingMode = PickingMode.Ignore;
            view.Row.Add(control);
            _hits.Add(new Hit { El = view.Row, Kind = HitKind.Row, Row = i });
            switch (_menu.Kind(i))
            {
                case SettingsRowKind.Slider:
                    view.Track = new VisualElement();
                    view.Track.AddToClassList("lc-track");
                    view.Fill = new VisualElement();
                    view.Fill.AddToClassList("lc-fill");
                    view.Thumb = new VisualElement();
                    view.Thumb.AddToClassList("lc-thumb");
                    view.Track.Add(view.Fill);
                    view.Track.Add(view.Thumb);
                    control.Add(view.Track);
                    view.Value = Add(control, "lc-valuebox");
                    // The pick area is the track with room above and below, so a thin bar is easy to grab.
                    _hits.Add(new Hit { El = control, Kind = HitKind.Slider, Row = i });
                    _hits.Add(new Hit { El = view.Value, Kind = HitKind.Row, Row = i }); // the number box: selects the row only
                    break;
                case SettingsRowKind.Toggle:
                    if (_menu.Hint(i) != null) Add(control, "lc-hint", _menu.Hint(i));
                    view.OnCell = Add(control, "lc-cell", "ON");
                    view.OffCell = Add(control, "lc-cell", "OFF");
                    _hits.Add(new Hit { El = view.OnCell, Kind = HitKind.ToggleOn, Row = i });
                    _hits.Add(new Hit { El = view.OffCell, Kind = HitKind.ToggleOff, Row = i });
                    break;
                default:
                    if (_menu.Hint(i) != null) Add(control, "lc-hint", _menu.Hint(i)); // "Reset best score" says it clears every Shots per Round number
                    view.Value = Add(control, "lc-wide-cell");
                    _hits.Add(new Hit { El = view.Value, Kind = HitKind.Cell, Row = i });
                    break;
            }
            _rows.Add(view);
            return view.Row;
        }

        /// <summary>Shows the current values, slider places and the selected row.</summary>
        void Refresh()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                var v = _rows[i];
                v.Row.EnableInClassList("lc-selected", i == _menu.Selected);
                string value = _menu.Value(i);
                switch (_menu.Kind(i))
                {
                    case SettingsRowKind.Slider:
                        float f = (float)_menu.Fraction(i);
                        v.Fill.style.width = Length.Percent(f * 100f);
                        v.Thumb.style.left = Length.Percent(f * 100f);
                        v.Value.text = value;
                        break;
                    case SettingsRowKind.Toggle:
                        bool on = value == "ON";
                        v.OnCell.EnableInClassList("lc-cell-on", on);
                        v.OffCell.EnableInClassList("lc-cell-off", !on);
                        break;
                    case SettingsRowKind.Button:
                        v.Value.text = "RESET  (" + value + ")";
                        break;
                    default:
                        v.Value.text = value;
                        break;
                }
            }
        }

        /// <summary>Serial ports on this PC now, sorted. Empty if they cannot be listed.</summary>
        static string[] PortNames()
        {
            if (!UsbPorts.TryList(out var names))
                Debug.LogWarning("Could not list serial ports.");
            return names;
        }
    }
}
