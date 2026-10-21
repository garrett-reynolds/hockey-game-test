using System;
using System.Collections.Generic;
using System.Globalization;

namespace HockeyGame.Core
{
    /// <summary>The two parts of the Settings page: the Player part (open to all) and the Dev part (Garrett's tuning).</summary>
    public enum SettingsPart
    {
        Player,
        Dev,
        Brands // one tick box per pool brand (#223)
    }

    /// <summary>The keys the Settings page answers to (the view turns real keys into these).</summary>
    public enum SettingsKey
    {
        Up,
        Down,
        Left,
        Right,
        Enter,
        Tab,
        Reset,
        Save,
        Escape
    }

    /// <summary>The two buttons of the "Save changes?" box.</summary>
    public enum ConfirmButton
    {
        Save,
        Discard
    }

    /// <summary>How a row is drawn and worked: a slider (drag), an on/off toggle (click), a choice (click for the next), a button (click).</summary>
    public enum SettingsRowKind
    {
        Slider,
        Toggle,
        Choice,
        Button
    }

    /// <summary>
    /// The Settings page as plain logic: two parts (Player, Dev), the rows of the current part grouped under headings, a
    /// selected row per part, and changes made on a working copy of the SaveData (#197): nothing reaches the SaveData until
    /// Save (the SAVE button); Discard drops the waiting changes; IsRowUnsaved / HasUnsavedChanges say what waits. The action
    /// rows (Reset best score, Test Mode) act at once. Up/Down moves the selection (MoveSelection),
    /// Left/Right changes the selected value (Change), Enter flips an on/off row or presses "Reset best score" (Activate),
    /// Tab switches the part (SwitchPart), and ResetPart sets every value of the current part back to its default.
    /// Press(SettingsKey) maps the keys onto these. The mouse works on any row: Select(row), then Change / Activate, or
    /// SetFraction(row, f) for a slider.
    /// Change, Activate, SetFraction, ResetPart and Press return true when the working copy changed (redraw). The file is
    /// written when SaveDataChanged is raised, not on these.
    /// Numbers step and stay in the ranges SaveData defines (the same limits Load clamps to).
    /// The row index everywhere is the index within the current part.
    /// </summary>
    public sealed class SettingsMenu
    {
        private sealed class Row
        {
            public SettingsPart Part;
            public string Group;
            public SettingsRowKind Kind;
            public string Label;
            public string Hint;                      // null, or a note such as "(next launch)"
            public string Tip;                       // the hover tip box text (#199)
            public string LogoId;                    // a brand row: the id its CHANGE LOGO / USE DEFAULT buttons work on (#226)
            public string Message;                   // a refusal note for the Hint, until the next pick, USE DEFAULT or Discard
            public Func<string> Value;
            public Func<int, bool> Change;           // Left/Right; null = does nothing
            public Func<bool> Activate;              // Enter; null = does nothing
            public Func<double> Fraction;            // sliders: 0..1 along the range
            public Func<double, bool> SetFraction;   // sliders: set from a 0..1 position (mouse)
        }

        private readonly SaveData _live;   // the saved data: only Save and the action rows change it
        private SaveData _save;            // the working copy every row reads and changes (swapped for _live only inside IsUnsaved)
        private readonly Action _openTestMode;
        private readonly List<string> _ports = new List<string>();
        private readonly List<Row> _all = new List<Row>();
        private readonly List<Row> _rows = new List<Row>(); // the current part's
        private readonly int[] _selected = new int[3];
        private readonly IPicturePicker _picker;
        private readonly IPictureCodec _codec;
        private readonly ILogoFiles _files;
        private readonly Dictionary<string, byte[]> _pending = new Dictionary<string, byte[]>(); // picked pictures (PNG bytes) waiting for SAVE, by brand id

        /// <param name="ports">Port names found now (e.g. COM3). Auto is always offered first.</param>
        /// <param name="openTestMode">What the Dev part's Test Mode button does (#156); null = nothing. It changes no setting.</param>
        public SettingsMenu(SaveData save, IList<string> ports, Action openTestMode = null,
            IPicturePicker picker = null, IPictureCodec codec = null, ILogoFiles files = null)
        {
            _live = save;
            _save = new SaveData();
            _save.CopyFrom(save);
            _openTestMode = openTestMode;
            _picker = picker;
            _codec = codec;
            _files = files;
            _ports.Add(SaveData.AutoUsbPort);
            if (ports != null)
                foreach (var p in ports)
                    if (!string.IsNullOrEmpty(p) && !_ports.Contains(p)) _ports.Add(p);
            if (!_ports.Contains(save.UsbPort)) _ports.Add(save.UsbPort); // keep a saved port that is unplugged

            const SettingsPart player = SettingsPart.Player;
            // Each row's last text is its hover tip (#199): what the row does, then when a change takes effect. Every row but the two
            // action rows acts when you press SAVE.
            AddNumber(player, "SOUND", "Master volume", "The loudness of everything. Acts when you press SAVE.", () => _save.MasterVolume, v => _save.MasterVolume = v, SaveData.VolumeRange, Percent);
            AddNumber(player, "SOUND", "Crowd", "How loud the crowd is. Acts when you press SAVE.", () => _save.CrowdVolume, v => _save.CrowdVolume = v, SaveData.VolumeRange, Percent);
            AddNumber(player, "SOUND", "Game sounds", "How loud the puck, posts, whistle and goal horn are. Acts when you press SAVE.", () => _save.GameVolume, v => _save.GameVolume = v, SaveData.VolumeRange, Percent);
            AddNumber(player, "SOUND", "Music", "How loud the music is. Acts when you press SAVE.", () => _save.MusicVolume, v => _save.MusicVolume = v, SaveData.VolumeRange, Percent);
            AddToggle(player, "SOUND", "Mute", "Turns every sound off; the volumes stay as they are. Acts when you press SAVE.", () => _save.Mute, v => _save.Mute = v);
            AddToggle(player, "SOUND", "Tick", "Plays a tick each second of the Shooting window. Acts when you press SAVE.", () => _save.TickOn, v => _save.TickOn = v);
            AddNumber(player, "ROUND", "Shots per Round", "How many Shots one Round has. The next Round uses it, after SAVE.", () => _save.ShotsPerRound, v => _save.ShotsPerRound = (int)v, SaveData.ShotsPerRoundRange, v => Format("0", v));
            AddToggle(player, "ROUND", "Easy mode", "Glows the Gaps the goalie leaves open, as a hint to aim at. Acts when you press SAVE.", () => _save.EasyMode, v => _save.EasyMode = v);
            AddToggle(player, "ROUND", "Test Shots on at start", "Starts the game with Test Shots on. Takes effect next launch.", () => _save.TestShotOnAtStart, v => _save.TestShotOnAtStart = v);
            AddToggle(player, "ROUND", "Replay", "Shows a short slow-motion Replay after a Goal, Save or Frame Hit. Acts when you press SAVE.", () => _save.ReplayOn, v => _save.ReplayOn = v);
            AddRow(player, "OPERATOR", SettingsRowKind.Choice, "USB port", "The port the shot sensor is read from. Auto picks one. The sensor reconnects when you press SAVE.", () => _save.UsbPort, ChangePort, null);
            AddNumber(player, "OPERATOR", "End screen input-ignore time", "How long the end screen ignores presses, so a late Shot does not skip it. Next end screen, after SAVE.", () => _save.InputIgnoreSeconds, v => _save.InputIgnoreSeconds = v, SaveData.InputIgnoreRange, Seconds);
            AddNumber(player, "OPERATOR", "End screen auto-return time", "How long the end screen waits before it goes back to the start. Next end screen, after SAVE.", () => _save.AutoReturnSeconds, v => _save.AutoReturnSeconds = v, SaveData.AutoReturnRange, Seconds);
            AddRow(player, "OPERATOR", SettingsRowKind.Button, "Reset best score", "Clears the best score for every Shots per Round number. Changes at once.", () => "best " + _save.BestScore.ToString(CultureInfo.InvariantCulture), null, ResetBestScore);
            _all[_all.Count - 1].Hint = "(all numbers)"; // the best of every Shots per Round number; short, as the hint box is narrow

            AddToggle(SettingsPart.Dev, "SHOW", "Show the block goalie", "Uses the plain block goalie instead of the player model. Takes effect next launch.", () => _save.ForceGoalieBlocks, v => _save.ForceGoalieBlocks = v);
            _all[_all.Count - 1].Hint = "(next launch)"; // RoundDriver builds the goalie when the game starts

            // The goalie's values (#147): they act from the next Round (ScreenFlow applies them when a Round starts).
            AddGoalie("Reaction time", "How long the goalie waits after the Shot before he moves. Next Round, after SAVE.", () => _save.ReactionSeconds, v => _save.ReactionSeconds = v, SaveData.ReactionRange, Seconds2, () => _save.ReactionAllowed);
            AddGoalie("Reaction wobble", "How much the goalie's reaction time varies from Shot to Shot. Next Round, after SAVE.", () => _save.ReactionWobbleSeconds, v => _save.ReactionWobbleSeconds = v, SaveData.WobbleRange, Seconds2, () => _save.WobbleAllowed);
            AddGoalie("Hand speed", "How fast the goalie's hands move, in metres per second. Next Round, after SAVE.", () => _save.HandMetresPerSecond, v => _save.HandMetresPerSecond = v, SaveData.HandRange, Speed, () => _save.HandAllowed);
            AddGoalie("Leg speed", "How fast the goalie's legs move, in metres per second. Next Round, after SAVE.", () => _save.LegMetresPerSecond, v => _save.LegMetresPerSecond = v, SaveData.LegRange, Speed, () => _save.LegAllowed);
            // Lowering Max push also brings the Late Settle size down with it (the size never goes above the push).
            AddGoalie("Max push", "The furthest the goalie moves across to reach a Shot. Next Round, after SAVE.", () => _save.MaxPushMetres, v => { _save.MaxPushMetres = v; _save.HoldLateSettleToMaxPush(); }, SaveData.MaxPushRange, Metres, () => _save.MaxPushAllowed);
            AddGoalie("Shuffle max", "A move across up to this far is a shuffle; a longer one is a T-push. Next Round, after SAVE.", () => _save.ShuffleMaxMetres, v => _save.ShuffleMaxMetres = v, SaveData.ShuffleMaxRange, Metres, () => _save.ShuffleMaxAllowed);

            // Round timing and Set-up Mistakes (#148): they also act from the next Round.
            AddDevRound("ROUND TIMING", "Skate-in time", "How long the shooter skates in before the Shooting window. Next Round, after SAVE.", () => _save.SkateInSeconds, v => _save.SkateInSeconds = v, SaveData.SkateInRange, Seconds, null);
            AddDevRound("ROUND TIMING", "Shooting window", "How long the player has to take the Shot. Next Round, after SAVE.", () => _save.ShootingWindowSeconds, v => _save.ShootingWindowSeconds = v, SaveData.ShootingWindowRange, Seconds, () => _save.ShootingWindowAllowed);
            AddDevRound("ROUND TIMING", "Result hold", "How long a result with no Replay stays on screen. Next Round, after SAVE.", () => _save.ResultHoldSeconds, v => _save.ResultHoldSeconds = v, SaveData.ResultHoldRange, Seconds, null);
            _all[_all.Count - 1].Hint = "(no Replay)"; // a Goal, Save or Frame Hit holds the fixed Replay time; only results with no Replay use this
            AddDevRound("SET-UP MISTAKES", "Cheat size", "How far toward a post the goalie sets when he cheats. Next Round, after SAVE.", () => _save.CheatMetres, v => _save.CheatMetres = v, SaveData.CheatRange, Metres, null);
            AddDevRound("SET-UP MISTAKES", "Late Settle size", "How far short of his spot the goalie is when the window opens. Next Round, after SAVE.", () => _save.LateSettleMetres, v => _save.LateSettleMetres = v, SaveData.LateSettleMetresRange, Metres, () => _save.LateSettleMetresAllowed);
            AddDevRound("SET-UP MISTAKES", "Late Settle time", "How long the goalie takes to close onto his spot after the window opens. Next Round, after SAVE.", () => _save.LateSettleSeconds, v => _save.LateSettleSeconds = v, SaveData.LateSettleSecondsRange, Seconds, () => _save.LateSettleSecondsAllowed);

            // Reach Limit (#194): the most any gear moves from its Set-up place in one Shot; the race reads it from the Round's settings.
            AddDevRound("GOALIE REACH", "Reach limit", "The most any piece of the goalie's gear moves from its Set-up place during one Shot. Next Round, after SAVE.", () => _save.ReachLimitMetres, v => _save.ReachLimitMetres = v, SaveData.ReachLimitRange, Metres, null);

            // Gaps (#160): how near thin gear must be to block a Gap; the Gap measure reads it from the Round's settings.
            AddDevRound("GAPS", "Gap thin-gear reach", "How near thin gear must be to block a Gap. Next Round, after SAVE.", () => _save.GapThinGearReachMetres, v => _save.GapThinGearReachMetres = v, SaveData.GapThinGearReachRange, Metres, null);

            // Water Bottle (#164): the game reads both on each Shot (RoundDriver), so a change acts from the next Shot, no restart.
            AddDevRound("WATER BOTTLE", "Bottle knock speed", "A Goal at least this fast knocks the water bottle off the net. Next Shot, after SAVE.", () => _save.BottleKnockSpeedKmh, v => _save.BottleKnockSpeedKmh = v, SaveData.BottleKnockSpeedRange, v => Format("0", v) + " km/h", null);
            _all[_all.Count - 1].Hint = "(next Shot)";
            AddDevRound("WATER BOTTLE", "Bottle jump", "How high the water bottle jumps when the puck hits it. Next Shot, after SAVE.", () => _save.BottleJumpPercent, v => _save.BottleJumpPercent = v, SaveData.BottleJumpRange, v => Format("0", v) + "%", null);
            _all[_all.Count - 1].Hint = "(next Shot)";

            // Test Mode (#156), kept the last Dev row: endless Shots from the shooter's spot. A button that opens it; it changes no setting, so Activate says false (nothing to save).
            AddRow(SettingsPart.Dev, "TEST", SettingsRowKind.Button, "Test Mode", "Opens endless Shots from the shooter's spot. It changes no setting.", () => "open", null, () => { RequestLeave(Leave.TestMode); return false; });

            // Brands (#223): one tick box per pool brand (the Leafs centre-ice logo is not one). An unticked brand leaves the
            // boards, the ribbon and the ice logos round centre ice. The last ticked brand cannot be unticked (the flip says false).
            foreach (Brands.Brand b in Brands.All)
            {
                string id = b.Id;
                Func<bool> flip = () => _save.SetBrandOn(id, !_save.IsBrandOn(id));
                AddRow(SettingsPart.Brands, "BRANDS", SettingsRowKind.Toggle, b.Name,
                    "Shows " + b.Name + " in the ads on the boards, the ribbon and the ice. At least one brand stays on. Next Round, after SAVE.",
                    () => _save.IsBrandOn(id) ? "ON" : "OFF", step => flip(), flip);
                _all[_all.Count - 1].LogoId = id; // CHANGE LOGO and USE DEFAULT (#226)
            }

            FillRows();
        }

        /// <summary>True when the row has CHANGE LOGO and USE DEFAULT buttons (a brand row).</summary>
        public bool HasLogoButtons(int row) => _rows[row].LogoId != null;

        /// <summary>True when the row's brand has a custom logo now (saved, or picked and waiting for SAVE).</summary>
        public bool IsLogoCustom(int row) => _rows[row].LogoId != null && _save.IsLogoCustom(_rows[row].LogoId);

        /// <summary>The longest side a picked picture may have; a longer one is refused. One over MaxKeptSide is shrunk first.</summary>
        public const int MaxPictureSide = 4096, MaxKeptSide = 2048;

        /// <summary>
        /// CHANGE LOGO on a row (#226): opens the file box. A picture that passes the checks is a pending change (the unsaved
        /// state is on) until SAVE; Discard drops it; Cancel changes nothing; a refusal shows in the row's Hint and changes nothing.
        /// </summary>
        public void ChangeLogo(int row)
        {
            Select(row);
            Row r = _rows[row];
            if (r.LogoId == null || _picker == null) return;
            _picker.Ask(file => LogoPicked(r, file));
        }

        private void LogoPicked(Row r, byte[] file)
        {
            if (file == null) return; // Cancel
            if (_codec == null || !_codec.TryDecode(file, out int width, out int height, out byte[] rgba) || width <= 0 || height <= 0)
            {
                r.Message = "(not a picture)";
                return;
            }
            if (Math.Max(width, height) > MaxPictureSide)
            {
                r.Message = "(over " + MaxPictureSide + " px)";
                return;
            }
            RgbaPicture picture = LogoArt.Shrink(new RgbaPicture(width, height, rgba), MaxKeptSide);
            if (LogoArt.MakeLogo(picture) == null)
            {
                r.Message = "(no logo found)";
                return;
            }
            _pending[r.LogoId] = _codec.EncodePng(picture.Width, picture.Height, picture.Rgba);
            _save.SetLogoCustom(r.LogoId, true);
            r.Message = null;
        }

        /// <summary>USE DEFAULT on a row: the shipped art comes back at SAVE. True when something changed.</summary>
        public bool UseDefaultLogo(int row)
        {
            Select(row);
            Row r = _rows[row];
            if (r.LogoId == null) return false;
            r.Message = null;
            bool hadPick = _pending.Remove(r.LogoId);
            return _save.SetLogoCustom(r.LogoId, false) || hadPick;
        }

        /// <summary>Raised once per brand id whose logo SAVE changed (the art lookup forgets its pictures then).</summary>
        public event Action<string> LogoSaved;

        /// <summary>The part showing now.</summary>
        public SettingsPart Part { get; private set; }

        /// <summary>The "Reset to default" button's words for the current part (the view prints them).</summary>
        public string ResetLabel => Part == SettingsPart.Player ? "RESET PLAYER TO DEFAULT" : Part == SettingsPart.Brands ? "TICK EVERY BRAND" : "RESET DEV TO DEFAULT";

        /// <summary>Rows in the current part.</summary>
        public int RowCount => _rows.Count;

        /// <summary>The selected row of the current part (each part remembers its own).</summary>
        public int Selected => _selected[(int)Part];

        public string Label(int row) => _rows[row].Label;

        public string Value(int row) => _rows[row].Value();

        /// <summary>The words an action row's button shows (#200).</summary>
        public string ButtonText(int row) => _rows[row].Label == "Test Mode" ? "OPEN TEST MODE" : "RESET  (" + _rows[row].Value() + ")";

        /// <summary>The heading the row sits under (consecutive rows with one heading make one card).</summary>
        public string Group(int row) => _rows[row].Group;

        /// <summary>A short note after the row's label (for example "(next launch)"), or null.</summary>
        public string Hint(int row) => _rows[row].Message ?? _rows[row].Hint;

        /// <summary>One or two short lines for the tip box (#199): what the row does and when a change takes effect.</summary>
        public string Tip(int row) => _rows[row].Tip;

        public SettingsRowKind Kind(int row) => _rows[row].Kind;

        /// <summary>A slider row's place along its range, 0 to 1. 0 for other rows.</summary>
        public double Fraction(int row) => _rows[row].Fraction != null ? _rows[row].Fraction() : 0;

        /// <summary>Tab: Player, Dev, Brands, and round again. Each part keeps its selected row.</summary>
        public void SwitchPart() => ShowPart(Part == SettingsPart.Player ? SettingsPart.Dev : Part == SettingsPart.Dev ? SettingsPart.Brands : SettingsPart.Player);

        /// <summary>Shows a part (the tabs click this).</summary>
        public void ShowPart(SettingsPart part)
        {
            Part = part;
            FillRows();
        }

        /// <summary>Up (-1) or Down (+1). Wraps at both ends.</summary>
        public void MoveSelection(int step)
        {
            _selected[(int)Part] = ((Selected + step) % _rows.Count + _rows.Count) % _rows.Count;
        }

        /// <summary>Selects a row of the current part (the mouse). Out-of-range rows are ignored.</summary>
        public void Select(int row)
        {
            if (row >= 0 && row < _rows.Count) _selected[(int)Part] = row;
        }

        /// <summary>Left (-1) or Right (+1) on the selected row. True when a setting changed.</summary>
        public bool Change(int step)
        {
            var change = _rows[Selected].Change;
            return change != null && change(Math.Sign(step));
        }

        /// <summary>Enter on the selected row. True when a setting changed.</summary>
        public bool Activate()
        {
            var activate = _rows[Selected].Activate;
            return activate != null && activate();
        }

        /// <summary>The mouse on a slider row: 0 to 1 along its track. True when the value changed. Selects the row.</summary>
        public bool SetFraction(int row, double fraction)
        {
            Select(row);
            var set = _rows[row].SetFraction;
            return set != null && set(Math.Max(0, Math.Min(1, fraction)));
        }

        /// <summary>
        /// "Reset to default": every value of the current part goes back to the default a new SaveData holds. The Player part
        /// leaves the best score alone (it has its own "Reset best score" row). Always true: the file is saved.
        /// </summary>
        public bool ResetPart()
        {
            var defaults = new SaveData();
            if (Part == SettingsPart.Player)
            {
                _save.MasterVolume = defaults.MasterVolume;
                _save.CrowdVolume = defaults.CrowdVolume;
                _save.GameVolume = defaults.GameVolume;
                _save.MusicVolume = defaults.MusicVolume;
                _save.Mute = defaults.Mute;
                _save.TickOn = defaults.TickOn;
                _save.ShotsPerRound = defaults.ShotsPerRound;
                _save.EasyMode = defaults.EasyMode;
                _save.TestShotOnAtStart = defaults.TestShotOnAtStart;
                _save.ReplayOn = defaults.ReplayOn;
                _save.UsbPort = defaults.UsbPort;
                _save.InputIgnoreSeconds = defaults.InputIgnoreSeconds;
                _save.AutoReturnSeconds = defaults.AutoReturnSeconds;
            }
            else if (Part == SettingsPart.Brands)
            {
                _save.BrandsOff = defaults.BrandsOff; // every brand ticked again
            }
            else
            {
                _save.ForceGoalieBlocks = defaults.ForceGoalieBlocks;
                _save.ReactionSeconds = defaults.ReactionSeconds;
                _save.ReactionWobbleSeconds = defaults.ReactionWobbleSeconds;
                _save.HandMetresPerSecond = defaults.HandMetresPerSecond;
                _save.LegMetresPerSecond = defaults.LegMetresPerSecond;
                _save.MaxPushMetres = defaults.MaxPushMetres;
                _save.ShuffleMaxMetres = defaults.ShuffleMaxMetres;
                _save.SkateInSeconds = defaults.SkateInSeconds;
                _save.ShootingWindowSeconds = defaults.ShootingWindowSeconds;
                _save.ResultHoldSeconds = defaults.ResultHoldSeconds;
                _save.CheatMetres = defaults.CheatMetres;
                _save.LateSettleMetres = defaults.LateSettleMetres;
                _save.LateSettleSeconds = defaults.LateSettleSeconds;
                _save.GapThinGearReachMetres = defaults.GapThinGearReachMetres;
                _save.ReachLimitMetres = defaults.ReachLimitMetres;
                _save.BottleKnockSpeedKmh = defaults.BottleKnockSpeedKmh;
                _save.BottleJumpPercent = defaults.BottleJumpPercent;
            }
            return true;
        }

        /// <summary>True when any row, in either part, differs from the saved data.</summary>
        public bool HasUnsavedChanges
        {
            get
            {
                foreach (var r in _all)
                    if (IsUnsaved(r)) return true;
                return false;
            }
        }

        /// <summary>True when the row (of the current part) holds a change that Save has not applied yet. Button rows never do.</summary>
        public bool IsRowUnsaved(int row) => IsUnsaved(_rows[row]);

        /// <summary>
        /// SAVE: applies every waiting change to the saved data at once (both parts). True when something was applied;
        /// SaveDataChanged is raised so the caller writes the file.
        /// </summary>
        public bool Save()
        {
            if (!HasUnsavedChanges) return false;
            var logosChanged = SaveLogos();
            _live.CopySettingsFrom(_save); // only the values with a row: the port the sensor found, the bests and the skin stay as they are now
            _save.CopyFrom(_live);         // and the working copy takes those back
            foreach (string id in logosChanged) LogoSaved?.Invoke(id);
            SaveDataChanged?.Invoke();
            return true;
        }

        // Writes the picked pictures and deletes the ones USE DEFAULT dropped. A picture that cannot be written leaves that brand as it was.
        private List<string> SaveLogos()
        {
            var changed = new List<string>();
            foreach (var pair in _pending)
            {
                if (_files != null && _files.Write(pair.Key, pair.Value)) { changed.Add(pair.Key); continue; }
                _save.SetLogoCustom(pair.Key, _live.IsLogoCustom(pair.Key));
                foreach (var r in _all) if (r.LogoId == pair.Key) r.Message = "(could not save)";
            }
            _pending.Clear();
            foreach (var r in _all)
            {
                if (r.LogoId == null || !_live.IsLogoCustom(r.LogoId) || _save.IsLogoCustom(r.LogoId)) continue;
                _files?.Delete(r.LogoId);
                changed.Add(r.LogoId);
            }
            return changed;
        }

        /// <summary>Drops every waiting change: the working copy goes back to the saved data.</summary>
        public void Discard()
        {
            _pending.Clear();
            foreach (var r in _all) r.Message = null;
            _save.CopyFrom(_live);
        }

        private enum Leave { Close, TestMode }

        private Leave _pendingLeave;

        /// <summary>True while the "Save changes?" box is up (#198). The page keys then belong to the box.</summary>
        public bool ConfirmOpen { get; private set; }

        /// <summary>The box's chosen button.</summary>
        public ConfirmButton ConfirmChoice { get; private set; }

        /// <summary>Raised when leaving the page is allowed: at once with nothing unsaved, else after Save or Discard in the box.</summary>
        public event Action CloseApproved;

        /// <summary>Esc, F2 or ESC CLOSE: closes at once with nothing unsaved, else opens the "Save changes?" box.</summary>
        public void RequestClose() => RequestLeave(Leave.Close);

        /// <summary>The box's Save: saves every waiting change, then goes on with what was asked (close or Test Mode).</summary>
        public void ConfirmSave()
        {
            if (!ConfirmOpen) return;
            Save();
            Proceed();
        }

        /// <summary>The box's Discard: every row goes back to its saved value, then goes on with what was asked.</summary>
        public void ConfirmDiscard()
        {
            if (!ConfirmOpen) return;
            Discard();
            Proceed();
        }

        /// <summary>Esc in the box: back to the page, nothing changed.</summary>
        public void ConfirmBack() => ConfirmOpen = false;

        /// <summary>The mouse moves over a box button, or a key moves between them.</summary>
        public void SetConfirmChoice(ConfirmButton choice) => ConfirmChoice = choice;

        /// <summary>Presses the chosen button of the box (Enter, or a click on a button the pointer chose).</summary>
        public void ConfirmPress()
        {
            if (ConfirmChoice == ConfirmButton.Save) ConfirmSave();
            else ConfirmDiscard();
        }

        private void RequestLeave(Leave leave)
        {
            _pendingLeave = leave;
            if (!HasUnsavedChanges)
            {
                Proceed();
                return;
            }
            ConfirmChoice = ConfirmButton.Save;
            ConfirmOpen = true;
        }

        private void Proceed()
        {
            ConfirmOpen = false;
            if (_pendingLeave == Leave.TestMode) _openTestMode?.Invoke();
            else CloseApproved?.Invoke();
        }

        /// <summary>
        /// Raised when the saved data itself changed: by Save, and by "Reset best score" (an action row, it acts at once).
        /// The caller writes the file then. Ordinary changes never raise it.
        /// </summary>
        public event Action SaveDataChanged;

        private bool IsUnsaved(Row r)
        {
            if (r.Kind == SettingsRowKind.Button) return false; // action rows act at once; nothing waits
            if (r.LogoId != null && (_pending.ContainsKey(r.LogoId) || _save.IsLogoCustom(r.LogoId) != _live.IsLogoCustom(r.LogoId))) return true; // a logo waits (#226)
            var working = _save;
            string saved;
            try
            {
                _save = _live; // the row's reader looks at _save: read it off the saved data for a moment
                saved = r.Value();
            }
            finally
            {
                _save = working;
            }
            return saved != r.Value();
        }

        /// <summary>A key on the page. True when a setting changed (save it).</summary>
        public bool Press(SettingsKey key)
        {
            if (ConfirmOpen)
            {
                PressInBox(key);
                return false;
            }
            switch (key)
            {
                case SettingsKey.Up: MoveSelection(-1); return false;
                case SettingsKey.Down: MoveSelection(1); return false;
                case SettingsKey.Left: return Change(-1);
                case SettingsKey.Right: return Change(1);
                case SettingsKey.Enter: return Activate();
                case SettingsKey.Tab: SwitchPart(); return false;
                case SettingsKey.Reset: return ResetPart();
                case SettingsKey.Save: Save(); return false;
                case SettingsKey.Escape: RequestClose(); return false;
            }
            return false;
        }

        // Keys while the "Save changes?" box is up: Left/Up and Right/Down move between Save and Discard, Enter picks, S saves, Esc goes back.
        private void PressInBox(SettingsKey key)
        {
            switch (key)
            {
                case SettingsKey.Left:
                case SettingsKey.Up: ConfirmChoice = ConfirmButton.Save; break;
                case SettingsKey.Right:
                case SettingsKey.Down: ConfirmChoice = ConfirmButton.Discard; break;
                case SettingsKey.Enter: ConfirmPress(); break;
                case SettingsKey.Save: ConfirmSave(); break;
                case SettingsKey.Escape: ConfirmBack(); break;
            }
        }

        private void FillRows()
        {
            _rows.Clear();
            foreach (var r in _all)
                if (r.Part == Part) _rows.Add(r);
        }

        private void AddRow(SettingsPart part, string group, SettingsRowKind kind, string label, string tip, Func<string> value,
            Func<int, bool> change, Func<bool> activate)
        {
            _all.Add(new Row { Part = part, Group = group, Kind = kind, Label = label, Tip = tip, Value = value, Change = change, Activate = activate });
        }

        private void AddToggle(SettingsPart part, string group, string label, string tip, Func<bool> get, Action<bool> set)
        {
            Func<bool> flip = () => { set(!get()); return true; };
            AddRow(part, group, SettingsRowKind.Toggle, label, tip, () => get() ? "ON" : "OFF", step => flip(), flip);
        }

        // range: the slider's track. allowed (optional): what a linked partner allows now, inside the track (#147); values clamp to it.
        private void AddNumber(SettingsPart part, string group, string label, string tip, Func<double> get, Action<double> set,
            SaveData.NumberRange range, Func<double, string> format, Func<SaveData.NumberRange> allowed = null)
        {
            AddRow(part, group, SettingsRowKind.Slider, label, tip, () => format(get()), dir =>
            {
                double old = get();
                // Snap to the step grid so repeated steps do not drift (0.1 + 0.2 ...).
                double next = Math.Round((old + dir * range.Step) / range.Step) * range.Step;
                return Store(get, set, allowed != null ? allowed() : range, next);
            }, null);
            var row = _all[_all.Count - 1];
            row.Fraction = () => (range.Clamp(get()) - range.Min) / (range.Max - range.Min);
            row.SetFraction = f =>
            {
                double next = range.Min + Math.Round(f * (range.Max - range.Min) / range.Step) * range.Step;
                return Store(get, set, allowed != null ? allowed() : range, next);
            };
        }

        private void AddGoalie(string label, string tip, Func<double> get, Action<double> set, SaveData.NumberRange range,
            Func<double, string> format, Func<SaveData.NumberRange> allowed)
        {
            AddDevRound("GOALIE", label, tip, get, set, range, format, allowed);
        }

        private void AddDevRound(string group, string label, string tip, Func<double> get, Action<double> set, SaveData.NumberRange range,
            Func<double, string> format, Func<SaveData.NumberRange> allowed)
        {
            AddNumber(SettingsPart.Dev, group, label, tip, get, set, range, format, allowed);
            _all[_all.Count - 1].Hint = "(next Round)";
        }

        private static bool Store(Func<double> get, Action<double> set, SaveData.NumberRange range, double next)
        {
            double old = get();
            next = Math.Round(range.Clamp(next), 6);
            if (next == old) return false;
            set(next);
            return true;
        }

        private bool ChangePort(int step)
        {
            if (_ports.Count < 2) return false;
            int i = _ports.IndexOf(_save.UsbPort);
            i = ((i + step) % _ports.Count + _ports.Count) % _ports.Count;
            _save.UsbPort = _ports[i];
            return true;
        }

        // An action row: it acts at once on the saved data (and the working copy), not on Save.
        private bool ResetBestScore()
        {
            _save.ClearBestScores();
            _live.ClearBestScores();
            SaveDataChanged?.Invoke();
            return true;
        }

        private static string Format(string pattern, double v) => v.ToString(pattern, CultureInfo.InvariantCulture);

        private static string Percent(double v) => Format("0", v * 100) + "%";

        private static string Seconds(double v) => Format("0.0", v) + " s";

        private static string Seconds2(double v) => Format("0.00", v) + " s";

        private static string Speed(double v) => Format("0.0", v) + " m/s";

        private static string Metres(double v) => Format("0.00", v) + " m";
    }
}
