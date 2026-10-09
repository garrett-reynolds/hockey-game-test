using System;
using System.Collections.Generic;
using System.Globalization;

namespace HockeyGame.Core
{
    /// <summary>
    /// The operator settings screen as plain logic: one row per setting, a selected row, and changes made
    /// straight on the shared SaveData. Up/Down moves the selection (MoveSelection), Left/Right changes the
    /// selected value (Change), Enter flips an on/off row or presses "Reset best score" (Activate).
    /// Change and Activate return true when SaveData changed, so the caller can save it.
    /// Numbers step and stay in the ranges SaveData defines (the same limits Load clamps to).
    /// </summary>
    public sealed class SettingsMenu
    {
        private sealed class Row
        {
            public string Label;
            public Func<string> Value;
            public Func<int, bool> Change;   // Left/Right; null = does nothing
            public Func<bool> Activate;      // Enter; null = does nothing
        }

        private readonly SaveData _save;
        private readonly List<string> _ports = new List<string>();
        private readonly List<Row> _rows = new List<Row>();

        /// <param name="ports">Port names found now (e.g. COM3). Auto is always offered first.</param>
        public SettingsMenu(SaveData save, IList<string> ports)
        {
            _save = save;
            _ports.Add(SaveData.AutoUsbPort);
            if (ports != null)
                foreach (var p in ports)
                    if (!string.IsNullOrEmpty(p) && !_ports.Contains(p)) _ports.Add(p);
            if (!_ports.Contains(save.UsbPort)) _ports.Add(save.UsbPort); // keep a saved port that is unplugged

            AddRow("USB port", () => _save.UsbPort, ChangePort, null);
            AddToggle("Easy mode", () => _save.EasyMode, v => _save.EasyMode = v);
            AddNumber("Contested chance at 0 km/h", () => _save.ContestedLowChance, v => _save.ContestedLowChance = v, SaveData.ChanceRange, Percent);
            AddNumber("Contested chance at top speed", () => _save.ContestedTopChance, v => _save.ContestedTopChance = v, SaveData.ChanceRange, Percent);
            AddNumber("Top speed", () => _save.TopSpeedKmh, v => _save.TopSpeedKmh = v, SaveData.TopSpeedRange, v => Format("0", v) + " km/h");
            AddToggle("Test Shots on at start", () => _save.TestShotOnAtStart, v => _save.TestShotOnAtStart = v);
            AddNumber("End screen input-ignore time", () => _save.InputIgnoreSeconds, v => _save.InputIgnoreSeconds = v, SaveData.InputIgnoreRange, Seconds);
            AddNumber("End screen auto-return time", () => _save.AutoReturnSeconds, v => _save.AutoReturnSeconds = v, SaveData.AutoReturnRange, Seconds);
            AddToggle("Tick", () => _save.TickOn, v => _save.TickOn = v);
            AddNumber("Master volume", () => _save.MasterVolume, v => _save.MasterVolume = v, SaveData.VolumeRange, Percent);
            AddToggle("Mute", () => _save.Mute, v => _save.Mute = v);
            AddRow("Reset best score", () => "best " + _save.BestScore.ToString(CultureInfo.InvariantCulture), null, ResetBestScore);
        }

        public int RowCount => _rows.Count;

        public int Selected { get; private set; }

        public string Label(int row) => _rows[row].Label;

        public string Value(int row) => _rows[row].Value();

        /// <summary>Up (-1) or Down (+1). Wraps at both ends.</summary>
        public void MoveSelection(int step)
        {
            Selected = ((Selected + step) % _rows.Count + _rows.Count) % _rows.Count;
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

        private void AddRow(string label, Func<string> value, Func<int, bool> change, Func<bool> activate)
        {
            _rows.Add(new Row { Label = label, Value = value, Change = change, Activate = activate });
        }

        private void AddToggle(string label, Func<bool> get, Action<bool> set)
        {
            Func<bool> flip = () => { set(!get()); return true; };
            AddRow(label, () => get() ? "ON" : "OFF", step => flip(), flip);
        }

        private void AddNumber(string label, Func<double> get, Action<double> set,
            SaveData.NumberRange range, Func<double, string> format)
        {
            AddRow(label, () => format(get()), dir =>
            {
                double old = get();
                // Snap to the step grid so repeated steps do not drift (0.1 + 0.2 ...).
                double next = Math.Round((old + dir * range.Step) / range.Step) * range.Step;
                next = Math.Round(range.Clamp(next), 6);
                if (next == old) return false;
                set(next);
                return true;
            }, null);
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
