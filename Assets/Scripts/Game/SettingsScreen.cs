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

        enum HitKind { Row, Slider, ToggleOn, ToggleOff, Cell, ChangeLogo, UseDefaultLogo }

        sealed class Hit
        {
            public VisualElement El;
            public HitKind Kind;
            public int Row;
        }

        sealed class RowView
        {
            public VisualElement Row, Fill, Thumb, Track, Mark;
            public Label Value, OnCell, OffCell, ChangeLogo, UseDefaultLogo;
            public Label Preview, Tag;       // the logo in use, and the CUSTOM tag (#231)
            public int PreviewVersion = -1;  // ArtLookup.Version when the preview was last set
        }

        readonly SaveData _save;
        readonly GameObject _object;
        readonly VisualElement _root;
        readonly VisualElement _page, _content, _startButton;
        const float StripeDegrees = -18f, TabSlant = 24f; // the header stripes' turn and the tabs' lean (tan 18 degrees x 76 px)
        readonly VisualElement _tabPlayer, _tabDev, _tabBrands;
        readonly Label _reset, _hints, _saveButton, _escClose;
        readonly VisualElement _bottomBar;
        double _savedUntil;                // Time.unscaledTimeAsDouble when "SAVED" goes away (SavedFade: solid, then fading out)
        static readonly Color SavedGreen = new Color32(40, 170, 80, 255), SavedWords = Color.white;       // the SAVED look, as .lc-save-done
        static readonly Color SaveDimGreen = new Color32(30, 40, 78, 255), SaveDimWords = new Color32(150, 160, 185, 255); // and what it fades to, as .lc-save-dim
        readonly VisualElement _confirm;   // the "Save changes?" box (#198) over the page
        readonly Label _confirmSave, _confirmDiscard;
        bool _closeApproved;               // the menu allowed leaving: the caller closes the page
        bool _persist;                   // the SaveData changed (Save or Reset best score): UpdateInput says so once, the caller writes the file
        readonly List<SlantShape> _tabShapes = new List<SlantShape>();
        readonly List<RowView> _rows = new List<RowView>();
        readonly List<Hit> _hits = new List<Hit>();
        SettingsMenu _menu;
        int _dragRow = -1;
        // Hover tip box (#199): the row under the mouse, or -1. A tool may set ForceHoverRow to show a tip without a mouse.
        // The tip box's size is set here, in one place (the sheet gives it colours and type only): the width, the padding and the borders.
        const float TipBoxWidth = 640f, TipPadX = 18f, TipPadY = 10f, TipBorder = 3f, TipBorderLeft = 12f;
        readonly Label _tipBox;
        int _hoverRow = -1;
        /// <summary>For the picture tool: shows the tip of this row without a mouse over it. The game never sets it.</summary>
        public int ForceHoverRow = -1;
        int _hoverButton = -1; // 0 = CHANGE LOGO, 1 = USE DEFAULT of the hovered row, or -1: the tip is then the button's (#231)
        /// <summary>For the picture tool: with ForceHoverRow, shows the tip of one logo button (0 or 1) without a mouse. The game never sets it.</summary>
        public int ForceHoverButton = -1;
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
            _tabBrands = MakeTab(top, "lc-tab-brands", "BRANDS"); // #223
            _content = Add(_page, "lc-content");
            _content.style.position = Position.Absolute;
            var bottom = _bottomBar = Add(_page, "lc-bottombar");
            _reset = Add(bottom, "lc-reset", "");
            _saveButton = Add(bottom, "lc-save", "SAVE");
            _escClose = Add(bottom, "lc-esc", "ESC  CLOSE"); // a button: a click acts like Esc (CloseKey)
            _hints = Add(bottom, "lc-hints", "TAB  PLAYER / DEV / BRANDS      R  RESET      S  SAVE");
            _page.AddToClassList("lc-hidden");
            _tipBox = Add(_page, "lc-tipbox lc-hidden", ""); // last, so it draws over the cards (#199)
            _tipBox.style.width = TipBoxWidth;
            _tipBox.style.paddingLeft = _tipBox.style.paddingRight = TipPadX;
            _tipBox.style.paddingTop = _tipBox.style.paddingBottom = TipPadY;
            _tipBox.style.borderTopWidth = _tipBox.style.borderRightWidth = _tipBox.style.borderBottomWidth = TipBorder;
            _tipBox.style.borderLeftWidth = TipBorderLeft;
            _root.Add(_page);

            _confirm = Add(_root, "lc-confirm lc-hidden"); // over the page; its look is in the sheet
            var box = Add(_confirm, "lc-confirm-box");
            Add(box, "lc-confirm-stripe");
            Add(box, "lc-confirm-head", "Save changes?");
            Add(box, "lc-confirm-text", "Some settings are changed but not saved.");
            _confirmSave = Add(box, "lc-confirm-button lc-confirm-save", "Save");
            _confirmDiscard = Add(box, "lc-confirm-button lc-confirm-discard", "Discard");

            _startButton = new VisualElement();
            _startButton.AddToClassList("lc-start-button");
            Add(_startButton, "lc-start-text", "SETTINGS");
            Add(_startButton, "lc-start-key", "ESC");
            _startButton.AddToClassList("lc-hidden");
            _root.Add(_startButton);
        }

        // The "Save changes?" box (#198, look C "TV graphic", as the PAUSED box): built in the constructor from the sheet's lc-confirm-*
        // classes. This shows or hides it and marks the chosen button (gold with navy words; the other is blue with white).
        void SyncConfirm()
        {
            bool open = _menu != null && _menu.ConfirmOpen;
            _confirm.EnableInClassList("lc-hidden", !open);
            if (!open) return;
            _confirmSave.EnableInClassList("lc-confirm-chosen", _menu.ConfirmChoice == ConfirmButton.Save);
            _confirmDiscard.EnableInClassList("lc-confirm-chosen", _menu.ConfirmChoice == ConfirmButton.Discard);
        }

        /// <summary>True when the "Save changes?" box is up (for tests and tools).</summary>
        public bool ConfirmOpen => _menu != null && _menu.ConfirmOpen;

        /// <summary>
        /// Esc, F2 or a click on ESC CLOSE while the page is open: closes at once with nothing unsaved, else shows the box; with the box
        /// up it goes back to the page. The caller then asks TakeCloseApproved.
        /// </summary>
        public void CloseKey()
        {
            _menu.Press(SettingsKey.Escape);
            Sync();
        }

        /// <summary>True once when leaving the page was allowed (nothing unsaved, or Save / Discard picked in the box): the caller closes it.</summary>
        public bool TakeCloseApproved()
        {
            bool approved = _closeApproved;
            _closeApproved = false;
            return approved;
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

        /// <summary>The middle of the ESC CLOSE button in screen pixels (y up), for tests and tools that fake a click.</summary>
        public Vector2 EscClosePoint() => ScreenCentreOf(_escClose);

        /// <summary>The middle of the SAVE button in screen pixels (y up), for tests and tools that fake a click.</summary>
        public Vector2 SavePoint() => ScreenCentreOf(_saveButton);

        Vector2 ScreenCentreOf(VisualElement el)
        {
            // ScreenToPanel is a plain scale and shift, so two points give the way back (panel y counts down, screen y counts up).
            Vector2 p0 = RuntimePanelUtils.ScreenToPanel(_root.panel, Vector2.zero), p1 = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(100f, 100f));
            Vector2 perPixel = (p1 - p0) / 100f;
            Vector2 centre = el.worldBound.center;
            return new Vector2((centre.x - p0.x) / perPixel.x, Screen.height - (centre.y - p0.y) / perPixel.y);
        }

        /// <summary>The page's root (the Settings page and the start button), for tests and tools.</summary>
        public VisualElement Root => _root;

        public SettingsMenu Menu => _menu;

        /// <summary>Raised when the Dev part's Test Mode button is pressed (#156). The game closes the page and opens Test Mode.</summary>
        public System.Action TestModeRequested;

        /// <summary>Opens the page on the Player part, with a fresh port list.</summary>
        public void Open()
        {
            _menu = new SettingsMenu(_save, PortNames(), () => TestModeRequested?.Invoke(), CustomLogos.Picker, CustomLogos.Codec, CustomLogos.Files);
            _menu.SaveDataChanged += () => { _persist = true; Brands.UseOff(_save.BrandsOff); }; // unticked brands leave the rotation (#223)
            _menu.LogoSaved += id => { CustomLogos.Forget(id); ArtLookup.Clear(id); };         // the lookup makes the brand's pictures again (#226)
            _menu.CloseApproved += () => _closeApproved = true;
            CustomLogos.WriteFailed -= OnLogoWriteFailed;
            CustomLogos.WriteFailed += OnLogoWriteFailed;                                       // the browser could not store a picture (#224)
            _persist = false;
            _closeApproved = false;
            _savedUntil = 0f;
            _dragRow = -1;
            Rebuild();
            _page.RemoveFromClassList("lc-hidden");
        }

        // The old picture was put back on disk: show the note, take the saved flag back, and make the art again.
        void OnLogoWriteFailed(string id, bool hadOld)
        {
            _menu.LogoWriteFailed(id, hadOld);
            CustomLogos.Forget(id);
            ArtLookup.Clear(id);
        }

        public void Close()
        {
            _menu?.Abandon(); // a file box answer that comes after the page closed is dropped (#224)
            _dragRow = -1;
            _page.AddToClassList("lc-hidden");
            _confirm.AddToClassList("lc-hidden");
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
        /// Keys and the mouse on the open page. True once when the SaveData changed (SAVE, Reset best score): write the file.
        /// Tab, R, Up/Down, Left/Right and Enter go to the SettingsMenu; a click picks a tab, the Reset button, SAVE, ESC CLOSE, a row's control.
        /// </summary>
        public bool UpdateInput(Keyboard keyboard, Mouse mouse)
        {
            if (_menu.ConfirmOpen)
            {
                // The "Save changes?" box owns the keys and the mouse (Esc and F2 go back through CloseKey).
                if (keyboard != null)
                {
                    if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) _menu.Press(SettingsKey.Left);
                    else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) _menu.Press(SettingsKey.Right);
                    else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) _menu.Press(SettingsKey.Enter);
                    else if (keyboard.sKey.wasPressedThisFrame) _menu.Press(SettingsKey.Save);
                }
                bool boxPersisted = mouse != null && Pointer(mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame, mouse.leftButton.isPressed);
                Sync();
                return TakePersist() || boxPersisted;
            }
            if (keyboard != null)
            {
                // Row changes wait for SAVE, so a key never says "write the file" itself: the SaveDataChanged event does (TakePersist).
                if (keyboard.upArrowKey.wasPressedThisFrame) _menu.Press(SettingsKey.Up);
                else if (keyboard.downArrowKey.wasPressedThisFrame) _menu.Press(SettingsKey.Down);
                else if (keyboard.leftArrowKey.wasPressedThisFrame) _menu.Press(SettingsKey.Left);
                else if (keyboard.rightArrowKey.wasPressedThisFrame) _menu.Press(SettingsKey.Right);
                else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                    _menu.Press(SettingsKey.Enter);
                else if (keyboard.tabKey.wasPressedThisFrame) _menu.Press(SettingsKey.Tab);
                else if (keyboard.rKey.wasPressedThisFrame) _menu.Press(SettingsKey.Reset);
                else if (keyboard.sKey.wasPressedThisFrame) SaveNow();
            }
            // The pointer's call hands the persist flag back itself (it clears it), so keep what it says.
            bool persisted = mouse != null && Pointer(mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame, mouse.leftButton.isPressed);
            Sync();
            return TakePersist() || persisted;
        }

        // SAVE (the button or the S key): applies the waiting changes; the green "SAVED" shows for a moment.
        void SaveNow()
        {
            if (_menu.Save()) _savedUntil = Time.unscaledTimeAsDouble + SavedFade.SavedSeconds;
        }

        // True once after the SaveData changed (SAVE, Reset best score): the caller writes the file. Row changes alone never do.
        bool TakePersist()
        {
            bool persist = _persist;
            _persist = false;
            return persist;
        }

        /// <summary>
        /// The pointer on the open page, in screen pixels (y up). pressed: the button went down this frame; down: it is held.
        /// True once when the SaveData changed (SAVE, Reset best score): write the file. UpdateInput feeds it from the mouse; a tool or test can feed it directly.
        /// </summary>
        public bool Pointer(Vector2 at, bool pressed, bool down)
        {
            if (_menu.ConfirmOpen)
            {
                // The box takes the pointer: a button under it is chosen (and picked on a press); anything else does nothing.
                if (Over(_confirmSave, at)) _menu.SetConfirmChoice(ConfirmButton.Save);
                else if (Over(_confirmDiscard, at)) _menu.SetConfirmChoice(ConfirmButton.Discard);
                else pressed = false;
                if (pressed) _menu.ConfirmPress();
                _dragRow = -1;
                Sync();
                return TakePersist();
            }
            if (pressed)
            {
                if (Over(_tabPlayer, at)) _menu.ShowPart(SettingsPart.Player);
                else if (Over(_tabDev, at)) _menu.ShowPart(SettingsPart.Dev);
                else if (Over(_tabBrands, at)) _menu.ShowPart(SettingsPart.Brands);
                else if (Over(_reset, at)) _menu.ResetPart();
                else if (Over(_saveButton, at)) SaveNow();
                else if (Over(_escClose, at)) CloseKey(); // as Esc: closes, or asks "Save changes?"
                else Click(at);
            }
            _hoverRow = RowAt(at);
            _hoverButton = ButtonAt(at);
            if (_dragRow >= 0)
            {
                if (down) DragTo(_dragRow, at);
                else _dragRow = -1;
            }
            Sync();
            return TakePersist();
        }

        // A press on a row: the control under the pointer wins over the row itself.
        void Click(Vector2 at)
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
                        DragTo(hit.Row, at);
                        break;
                    case HitKind.ToggleOn:
                        if (_menu.Value(hit.Row) != "ON") _menu.Activate();
                        break;
                    case HitKind.ToggleOff:
                        if (_menu.Value(hit.Row) != "OFF") _menu.Activate();
                        break;
                    case HitKind.Cell:
                        if (_menu.Kind(hit.Row) == SettingsRowKind.Choice) _menu.Change(1);
                        else _menu.Activate();
                        break;
                    case HitKind.ChangeLogo:
                        _menu.ChangeLogo(hit.Row); // the file box (the Editor's); the pick waits for SAVE (#226)
                        break;
                    case HitKind.UseDefaultLogo:
                        _menu.UseDefaultLogo(hit.Row);
                        break;
                }
                return; // the row itself: selected
            }
        }

        // The row under the pointer, or -1 (#199).
        int RowAt(Vector2 at)
        {
            for (int i = _hits.Count - 1; i >= 0; i--)
                if (Over(_hits[i].El, at)) return _hits[i].Row;
            return -1;
        }

        // The logo button under the pointer (0 = CHANGE LOGO, 1 = USE DEFAULT), or -1 (#231).
        int ButtonAt(Vector2 at)
        {
            for (int i = _hits.Count - 1; i >= 0; i--)
            {
                if (!Over(_hits[i].El, at)) continue;
                return _hits[i].Kind == HitKind.ChangeLogo ? 0 : _hits[i].Kind == HitKind.UseDefaultLogo ? 1 : -1;
            }
            return -1;
        }

        // Shows the tip box under the hovered row (above it near the bottom bar), in the row's column; hides it when the mouse is on no row (#199).
        // On a logo button the tip says what that button does (#231).
        void RefreshTip()
        {
            int hover = ForceHoverRow >= 0 ? ForceHoverRow : _hoverRow;
            int button = ForceHoverRow >= 0 ? ForceHoverButton : _hoverButton;
            bool show = hover >= 0 && hover < _rows.Count && !_menu.ConfirmOpen && _page.resolvedStyle.display != DisplayStyle.None;
            string tip = show ? (button >= 0 ? _menu.LogoButtonTip(hover, button) : null) ?? _menu.Tip(hover) : null;
            show = !string.IsNullOrEmpty(tip);
            _tipBox.EnableInClassList("lc-hidden", !show);
            if (!show) return;
            _tipBox.text = tip;
            Rect row = _rows[hover].Row.worldBound, page = _page.worldBound;
            float boxWidth = Mathf.Min(TipBoxWidth, row.width); // inside the row's column (#202)
            _tipBox.style.width = boxWidth;
            float textWidth = boxWidth - 2 * TipPadX - TipBorderLeft - TipBorder;
            float textHeight = _tipBox.MeasureTextSize(tip, textWidth, VisualElement.MeasureMode.Exactly,
                0, VisualElement.MeasureMode.Undefined).y;
            float boxHeight = textHeight + 2 * TipPadY + 2 * TipBorder;
            var at = TipPlacement.Place(row.x - page.x, row.y - page.y, row.width, row.height, page.width, page.height, boxWidth, boxHeight,
                _bottomBar.worldBound.y - page.y);
            _tipBox.style.left = (float)at.X;
            _tipBox.style.top = (float)at.Y;
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
            SyncConfirm();
        }

        void Rebuild()
        {
            _shownPart = _menu.Part;
            _hoverRow = -1; // row numbers belong to the part shown
            _hoverButton = -1;
            _content.Clear();
            _rows.Clear();
            _hits.Clear();
            _tabPlayer.EnableInClassList("lc-tab-on", _menu.Part == SettingsPart.Player);
            _tabDev.EnableInClassList("lc-tab-on", _menu.Part == SettingsPart.Dev);
            _tabBrands.EnableInClassList("lc-tab-on", _menu.Part == SettingsPart.Brands);
            foreach (var shape in _tabShapes) shape.MarkDirtyRepaint(); // the fill colour follows the tab's class
            _reset.text = _menu.ResetLabel;

            if (_menu.Part == SettingsPart.Brands) { BuildBrandsGrid(); Refresh(); return; }

            // Consecutive rows with one heading make one card. The last card goes in the right-hand column, the rest in the left;
            // the Dev part has 17 rows (#148, #156, #160, #164), so its last four cards (Set-up Mistakes, Gaps, Water Bottle, Test) go right.
            int rightCards = _menu.Part == SettingsPart.Dev ? 4 : 1;
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
                // A slider row has no room for its hint, so a card of sliders carries it once in its title (the goalie values: "next Round").
                int first = groups[g].first;
                string head = _menu.Kind(first) == SettingsRowKind.Slider && _menu.Hint(first) != null
                    ? groups[g].name + "   " + _menu.Hint(first) : groups[g].name;
                Add(card, "lc-card-head", head);
                for (int r = 0; r < groups[g].count; r++)
                    card.Add(BuildRow(groups[g].first + r));
                (g >= groups.Count - rightCards ? right : left).Add(card);
            }
            Refresh();
        }

        // The Brands part (#223): one tick box per pool brand, too many for two columns, so four narrow cards of compact rows.
        void BuildBrandsGrid()
        {
            const int columns = 3; // 43 brands and the three logo rows (#231); each row also holds a preview and two buttons
            int perColumn = (_menu.RowCount + columns - 1) / columns;
            for (int c = 0; c < columns; c++)
            {
                int first = c * perColumn, count = System.Math.Min(perColumn, _menu.RowCount - first);
                if (count <= 0) break;
                var column = new VisualElement();
                column.AddToClassList("lc-column");
                column.AddToClassList("lc-brands-column");
                column.style.left = 30 + c * 630;
                _content.Add(column);
                var card = new VisualElement();
                card.AddToClassList("lc-card");
                card.AddToClassList("lc-c" + (c % 3));
                card.AddToClassList("lc-brands");
                Add(card, "lc-card-head", c == 0 ? "BRANDS" : "BRANDS  " + (c + 1) + "/" + columns);
                for (int r = 0; r < count; r++) card.Add(BuildRow(first + r));
                column.Add(card);
            }
        }

        VisualElement BuildRow(int i)
        {
            var view = new RowView { Row = new VisualElement() };
            view.Row.AddToClassList("lc-row");
            view.Mark = new VisualElement(); // the amber bar of an unsaved row (#197)
            view.Mark.AddToClassList("lc-mark");
            view.Mark.pickingMode = PickingMode.Ignore;
            view.Mark.style.display = DisplayStyle.None;
            view.Row.Add(view.Mark);
            // A slider's usual hint "(next Round)" is in its card's title; a different one (Result hold: "(no Replay)") goes after the label.
            string label = _menu.Label(i);
            if (_menu.Kind(i) == SettingsRowKind.Slider && _menu.Hint(i) != null && _menu.Hint(i) != "(next Round)" && _menu.Hint(i) != "(next Shot)")
                label += "  " + _menu.Hint(i);
            Label name = Add(view.Row, "lc-row-label", label);
            if (_menu.Kind(i) == SettingsRowKind.Button && _menu.Part == SettingsPart.Brands) name.style.width = 330; // USE EVERY DEFAULT LOGO is a long name (#231)
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
                    if (_menu.HasLogoButtons(i) && CustomLogos.Picker != null) AddLogoButtons(control, view, i); // beside the tick box (#226)
                    else if (_menu.Hint(i) != null) Add(control, "lc-hint", _menu.Hint(i));
                    view.OnCell = Add(control, "lc-cell", "ON");
                    view.OffCell = Add(control, "lc-cell", "OFF");
                    _hits.Add(new Hit { El = view.OnCell, Kind = HitKind.ToggleOn, Row = i });
                    _hits.Add(new Hit { El = view.OffCell, Kind = HitKind.ToggleOff, Row = i });
                    break;
                case SettingsRowKind.Picture: // CENTRE ICE and GAME LOGO (#230): the two buttons, no tick box
                    if (CustomLogos.Picker != null) AddLogoButtons(control, view, i);
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

        // CHANGE LOGO and USE DEFAULT (#226); a refusal note shows before them.
        void AddLogoButtons(VisualElement control, RowView view, int i)
        {
            view.Preview = Add(control, "lc-preview"); // the logo in use (#231)
            view.Tag = Add(view.Preview, "lc-tag", "CUSTOM"); // on its corner; a refusal note ("(not a picture)") takes its place
            view.ChangeLogo = Add(control, "lc-cell", "CHANGE LOGO");
            view.UseDefaultLogo = Add(control, "lc-cell", "USE DEFAULT");
            foreach (Label b in new[] { view.ChangeLogo, view.UseDefaultLogo })
            {
                b.style.width = 112;
                b.style.fontSize = 14;
                b.style.marginRight = 6;
                b.style.whiteSpace = WhiteSpace.NoWrap;
            }
            _hits.Add(new Hit { El = view.ChangeLogo, Kind = HitKind.ChangeLogo, Row = i });
            _hits.Add(new Hit { El = view.UseDefaultLogo, Kind = HitKind.UseDefaultLogo, Row = i });
        }

        /// <summary>Shows the current values, slider places and the selected row.</summary>
        void Refresh()
        {
            RefreshTip();
            bool waiting = _menu.HasUnsavedChanges;
            double savedLeft = _savedUntil - Time.unscaledTimeAsDouble;
            bool justSaved = !waiting && savedLeft > 0;
            _saveButton.text = justSaved ? "SAVED" : "SAVE";
            _saveButton.EnableInClassList("lc-save-done", justSaved);
            _saveButton.EnableInClassList("lc-save-dim", !waiting && !justSaved);
            // SAVED stays green, then fades over its last moments into the dim SAVE (SavedFade); the sheet's colours return when it is over.
            if (justSaved)
            {
                float solid = (float)SavedFade.Opacity(savedLeft);
                _saveButton.style.backgroundColor = Color.Lerp(SaveDimGreen, SavedGreen, solid);
                _saveButton.style.color = Color.Lerp(SaveDimWords, SavedWords, solid);
            }
            else
            {
                _saveButton.style.backgroundColor = StyleKeyword.Null;
                _saveButton.style.color = StyleKeyword.Null;
            }
            for (int i = 0; i < _rows.Count; i++)
            {
                var v = _rows[i];
                v.Row.EnableInClassList("lc-selected", i == _menu.Selected);
                bool unsaved = _menu.IsRowUnsaved(i);
                v.Row.EnableInClassList("lc-unsaved", unsaved);
                v.Mark.style.display = unsaved ? DisplayStyle.Flex : DisplayStyle.None;
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
                        RefreshLogo(v, i);
                        break;
                    case SettingsRowKind.Picture:
                        RefreshLogo(v, i);
                        break;
                    case SettingsRowKind.Button:
                        v.Value.text = _menu.ButtonText(i);
                        break;
                    default:
                        v.Value.text = value;
                        break;
                }
            }
        }

        // The logo controls of a row: the note, the preview, the CUSTOM tag, the dimmed USE DEFAULT, and the button the keyboard chose (#231).
        void RefreshLogo(RowView v, int i)
        {
            if (v.Tag == null) return;
            string note = _menu.Hint(i);
            string tag = note ?? _menu.CustomTag(i);
            bool custom = _menu.IsLogoCustom(i);
            v.UseDefaultLogo.style.opacity = custom ? 1f : 0.45f; // nothing to put back on the shipped art
            v.Tag.text = tag ?? "";
            v.Tag.style.display = tag != null ? DisplayStyle.Flex : DisplayStyle.None;
            v.Tag.EnableInClassList("lc-tag-note", note != null);
            bool keys = i == _menu.Selected; // the keyboard picks a button on the picture rows and a stop on the brand rows (the tick is the third, #224)
            v.ChangeLogo.EnableInClassList("lc-btn-focus", keys && _menu.LogoButton(i) == 0);
            v.UseDefaultLogo.EnableInClassList("lc-btn-focus", keys && _menu.LogoButton(i) == 1);
            if (v.OnCell != null)
            {
                bool tickOn = _menu.Value(i) == "ON", tick = keys && _menu.LogoButton(i) == SettingsMenu.TickStop;
                v.OnCell.EnableInClassList("lc-btn-focus", tick && tickOn);
                v.OffCell.EnableInClassList("lc-btn-focus", tick && !tickOn);
            }
            if (v.PreviewVersion == ArtLookup.Version) return;
            v.PreviewVersion = ArtLookup.Version;
            Texture2D art = ArtLookup.Preview(_menu.LogoId(i));
            v.Preview.style.backgroundImage = art != null ? new StyleBackground(art) : new StyleBackground(StyleKeyword.None);
            v.Preview.text = art != null ? "" : "THE SHOOTOUT"; // the start screen's own drawn logo has no picture file
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
