using System;
using System.Collections.Generic;
using System.Globalization;

namespace HockeyGame.Core
{
    /// <summary>The two parts of the Settings page: the Player part (open to all) and the Dev part (Garrett's tuning).</summary>
    public enum SettingsPart
    {
        Player,
        Dev
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
        Reset
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
    /// selected row per part, and changes made straight on the shared SaveData. Up/Down moves the selection (MoveSelection),
    /// Left/Right changes the selected value (Change), Enter flips an on/off row or presses "Reset best score" (Activate),
    /// Tab switches the part (SwitchPart), and ResetPart sets every value of the current part back to its default.
    /// Press(SettingsKey) maps the keys onto these. The mouse works on any row: Select(row), then Change / Activate, or
    /// SetFraction(row, f) for a slider.
    /// Change, Activate, SetFraction, ResetPart and Press return true when SaveData changed, so the caller can save it.
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
            public Func<string> Value;
            public Func<int, bool> Change;           // Left/Right; null = does nothing
            public Func<bool> Activate;              // Enter; null = does nothing
            public Func<double> Fraction;            // sliders: 0..1 along the range
            public Func<double, bool> SetFraction;   // sliders: set from a 0..1 position (mouse)
        }

        private readonly SaveData _save;
        private readonly List<string> _ports = new List<string>();
        private readonly List<Row> _all = new List<Row>();
        private readonly List<Row> _rows = new List<Row>(); // the current part's
        private readonly int[] _selected = new int[2];

        /// <param name="ports">Port names found now (e.g. COM3). Auto is always offered first.</param>
        public SettingsMenu(SaveData save, IList<string> ports)
        {
            _save = save;
            _ports.Add(SaveData.AutoUsbPort);
            if (ports != null)
                foreach (var p in ports)
                    if (!string.IsNullOrEmpty(p) && !_ports.Contains(p)) _ports.Add(p);
            if (!_ports.Contains(save.UsbPort)) _ports.Add(save.UsbPort); // keep a saved port that is unplugged

            const SettingsPart player = SettingsPart.Player;
            AddNumber(player, "SOUND", "Master volume", () => _save.MasterVolume, v => _save.MasterVolume = v, SaveData.VolumeRange, Percent);
            AddNumber(player, "SOUND", "Crowd", () => _save.CrowdVolume, v => _save.CrowdVolume = v, SaveData.VolumeRange, Percent);
            AddNumber(player, "SOUND", "Game sounds", () => _save.GameVolume, v => _save.GameVolume = v, SaveData.VolumeRange, Percent);
            AddNumber(player, "SOUND", "Music", () => _save.MusicVolume, v => _save.MusicVolume = v, SaveData.VolumeRange, Percent);
            AddToggle(player, "SOUND", "Mute", () => _save.Mute, v => _save.Mute = v);
            AddToggle(player, "SOUND", "Tick", () => _save.TickOn, v => _save.TickOn = v);
            AddToggle(player, "ROUND", "Easy mode", () => _save.EasyMode, v => _save.EasyMode = v);
            AddToggle(player, "ROUND", "Test Shots on at start", () => _save.TestShotOnAtStart, v => _save.TestShotOnAtStart = v);
            AddRow(player, "OPERATOR", SettingsRowKind.Choice, "USB port", () => _save.UsbPort, ChangePort, null);
            AddNumber(player, "OPERATOR", "End screen input-ignore time", () => _save.InputIgnoreSeconds, v => _save.InputIgnoreSeconds = v, SaveData.InputIgnoreRange, Seconds);
            AddNumber(player, "OPERATOR", "End screen auto-return time", () => _save.AutoReturnSeconds, v => _save.AutoReturnSeconds = v, SaveData.AutoReturnRange, Seconds);
            AddRow(player, "OPERATOR", SettingsRowKind.Button, "Reset best score", () => "best " + _save.BestScore.ToString(CultureInfo.InvariantCulture), null, ResetBestScore);

            AddToggle(SettingsPart.Dev, "SHOW", "Show the block goalie", () => _save.ForceGoalieBlocks, v => _save.ForceGoalieBlocks = v);
            _all[_all.Count - 1].Hint = "(next launch)"; // RoundDriver builds the goalie when the game starts

            FillRows();
        }

        /// <summary>The part showing now.</summary>
        public SettingsPart Part { get; private set; }

        /// <summary>The "Reset to default" button's words for the current part (the view prints them).</summary>
        public string ResetLabel => Part == SettingsPart.Player ? "RESET PLAYER TO DEFAULT" : "RESET DEV TO DEFAULT";

        /// <summary>Rows in the current part.</summary>
        public int RowCount => _rows.Count;

        /// <summary>The selected row of the current part (each part remembers its own).</summary>
        public int Selected => _selected[(int)Part];

        public string Label(int row) => _rows[row].Label;

        public string Value(int row) => _rows[row].Value();

        /// <summary>The heading the row sits under (consecutive rows with one heading make one card).</summary>
        public string Group(int row) => _rows[row].Group;

        /// <summary>A short note after the row's label (for example "(next launch)"), or null.</summary>
        public string Hint(int row) => _rows[row].Hint;

        public SettingsRowKind Kind(int row) => _rows[row].Kind;

        /// <summary>A slider row's place along its range, 0 to 1. 0 for other rows.</summary>
        public double Fraction(int row) => _rows[row].Fraction != null ? _rows[row].Fraction() : 0;

        /// <summary>Tab: Player to Dev and back. Each part keeps its selected row.</summary>
        public void SwitchPart() => ShowPart(Part == SettingsPart.Player ? SettingsPart.Dev : SettingsPart.Player);

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
                _save.EasyMode = defaults.EasyMode;
                _save.TestShotOnAtStart = defaults.TestShotOnAtStart;
                _save.UsbPort = defaults.UsbPort;
                _save.InputIgnoreSeconds = defaults.InputIgnoreSeconds;
                _save.AutoReturnSeconds = defaults.AutoReturnSeconds;
            }
            else
            {
                _save.ForceGoalieBlocks = defaults.ForceGoalieBlocks;
            }
            return true;
        }

        /// <summary>A key on the page. True when a setting changed (save it).</summary>
        public bool Press(SettingsKey key)
        {
            switch (key)
            {
                case SettingsKey.Up: MoveSelection(-1); return false;
                case SettingsKey.Down: MoveSelection(1); return false;
                case SettingsKey.Left: return Change(-1);
                case SettingsKey.Right: return Change(1);
                case SettingsKey.Enter: return Activate();
                case SettingsKey.Tab: SwitchPart(); return false;
                case SettingsKey.Reset: return ResetPart();
            }
            return false;
        }

        private void FillRows()
        {
            _rows.Clear();
            foreach (var r in _all)
                if (r.Part == Part) _rows.Add(r);
        }

        private void AddRow(SettingsPart part, string group, SettingsRowKind kind, string label, Func<string> value,
            Func<int, bool> change, Func<bool> activate)
        {
            _all.Add(new Row { Part = part, Group = group, Kind = kind, Label = label, Value = value, Change = change, Activate = activate });
        }

        private void AddToggle(SettingsPart part, string group, string label, Func<bool> get, Action<bool> set)
        {
            Func<bool> flip = () => { set(!get()); return true; };
            AddRow(part, group, SettingsRowKind.Toggle, label, () => get() ? "ON" : "OFF", step => flip(), flip);
        }

        private void AddNumber(SettingsPart part, string group, string label, Func<double> get, Action<double> set,
            SaveData.NumberRange range, Func<double, string> format)
        {
            AddRow(part, group, SettingsRowKind.Slider, label, () => format(get()), dir =>
            {
                double old = get();
                // Snap to the step grid so repeated steps do not drift (0.1 + 0.2 ...).
                double next = Math.Round((old + dir * range.Step) / range.Step) * range.Step;
                return Store(get, set, range, next);
            }, null);
            var row = _all[_all.Count - 1];
            row.Fraction = () => (range.Clamp(get()) - range.Min) / (range.Max - range.Min);
            row.SetFraction = f =>
            {
                double next = range.Min + Math.Round(f * (range.Max - range.Min) / range.Step) * range.Step;
                return Store(get, set, range, next);
            };
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

        private bool ResetBestScore()
        {
            _save.BestScore = 0;
            return true;
        }

        private static string Format(string pattern, double v) => v.ToString(pattern, CultureInfo.InvariantCulture);

        private static string Percent(double v) => Format("0", v * 100) + "%";

        private static string Seconds(double v) => Format("0.0", v) + " s";
    }
}
