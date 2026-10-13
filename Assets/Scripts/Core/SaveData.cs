using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace HockeyGame.Core
{
    /// <summary>
    /// Best score and operator settings, kept in a plain key=value text file.
    /// Numbers use the invariant culture. A missing file, missing key or bad value gives the default;
    /// an out-of-range number is clamped to its NumberRange.
    /// </summary>
    public sealed class SaveData
    {
        public const string AutoUsbPort = "Auto";
        /// <summary>The League Skin's name: the Team Skin used when the file names none.</summary>
        public const string LeagueSkin = "league";

        /// <summary>Allowed values and step for one number setting. The settings screen steps by these; Load clamps to them.</summary>
        public readonly struct NumberRange
        {
            public readonly double Min, Max, Step;
            public NumberRange(double min, double max, double step) { Min = min; Max = max; Step = step; }
            public double Clamp(double value) => Math.Max(Min, Math.Min(Max, value));
        }

        public static readonly NumberRange InputIgnoreRange = new NumberRange(0, 30, 0.5);
        public static readonly NumberRange AutoReturnRange = new NumberRange(1, 120, 1);
        public static readonly NumberRange VolumeRange = new NumberRange(0, 1, 0.1);

        // File keys, one per setting, shared by Load and Save.
        private const string BestScoreKey = "bestScore";
        private const string UsbPortKey = "usbPort";
        private const string LastUsbPortKey = "lastUsbPort";
        private const string EasyModeKey = "easyMode";
        private const string TestShotOnAtStartKey = "testShotOnAtStart";
        private const string InputIgnoreSecondsKey = "inputIgnoreSeconds";
        private const string AutoReturnSecondsKey = "autoReturnSeconds";
        private const string TickOnKey = "tickOn";
        private const string ReplayOnKey = "replayOn";
        private const string MasterVolumeKey = "masterVolume";
        private const string MuteKey = "mute";
        private const string TeamSkinKey = "teamSkin";
        private const string ForceGoalieBlocksKey = "forceGoalieBlocks"; // replaces goalieCharacterOn (#27), which old files hold as false

        public int BestScore = 0;
        public string UsbPort = AutoUsbPort;
        /// <summary>In Auto mode, the last port that sent READY; tried first next time. Blank if none yet.</summary>
        public string LastUsbPort = "";
        public bool EasyMode = false;
        public bool TestShotOnAtStart = true;
        public double InputIgnoreSeconds = 2;
        public double AutoReturnSeconds = 15;
        public bool TickOn = true;
        /// <summary>The Replay after a Goal, a Save or a Frame Hit (#145). Off: the result word holds on the Video Board instead.</summary>
        public bool ReplayOn = true;
        public double MasterVolume = 1.0;
        public bool Mute = false;
        /// <summary>
        /// Developer flag, a toggle in the Settings page's Dev part: shows the block goalie instead of the goalie character
        /// (read when the game starts). Or set forceGoalieBlocks=true in the settings file. The old goalieCharacterOn key is ignored.
        /// </summary>
        public bool ForceGoalieBlocks = false;
        /// <summary>
        /// The Team Skin picked before play: its asset name in Assets/Resources/Skins (TeamSkin). Not on the settings
        /// screen (no menu yet): set teamSkin=&lt;name&gt; in the settings file. An unknown name plays the League Skin.
        /// </summary>
        public string TeamSkin = LeagueSkin;

        /// <summary>Loads from a path. Never throws: any problem leaves defaults for what could not be read.</summary>
        public static SaveData Load(string path)
        {
            var data = new SaveData();
            string[] lines;
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return data;
                lines = File.ReadAllLines(path, Encoding.UTF8);
            }
            catch (Exception)
            {
                return data;
            }

            var values = new Dictionary<string, string>();
            foreach (var line in lines)
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }

            data.BestScore = ReadInt(values, BestScoreKey, data.BestScore);
            if (data.BestScore < 0) data.BestScore = 0;
            if (values.TryGetValue(UsbPortKey, out var port) && port.Length > 0) data.UsbPort = port;
            if (values.TryGetValue(LastUsbPortKey, out var lastPort)) data.LastUsbPort = lastPort;
            data.EasyMode = ReadBool(values, EasyModeKey, data.EasyMode);
            data.TestShotOnAtStart = ReadBool(values, TestShotOnAtStartKey, data.TestShotOnAtStart);
            data.InputIgnoreSeconds = ReadDouble(values, InputIgnoreSecondsKey, data.InputIgnoreSeconds, InputIgnoreRange);
            data.AutoReturnSeconds = ReadDouble(values, AutoReturnSecondsKey, data.AutoReturnSeconds, AutoReturnRange);
            data.TickOn = ReadBool(values, TickOnKey, data.TickOn);
            data.ReplayOn = ReadBool(values, ReplayOnKey, data.ReplayOn);
            data.MasterVolume = ReadDouble(values, MasterVolumeKey, data.MasterVolume, VolumeRange);
            data.Mute = ReadBool(values, MuteKey, data.Mute);
            data.ForceGoalieBlocks = ReadBool(values, ForceGoalieBlocksKey, data.ForceGoalieBlocks);
            if (values.TryGetValue(TeamSkinKey, out var skin) && skin.Length > 0) data.TeamSkin = skin;
            return data;
        }

        /// <summary>Saves to a path, creating the folder if needed. Returns false (never throws) if writing fails.</summary>
        public bool Save(string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder();
                sb.Append(BestScoreKey).Append('=').Append(BestScore.ToString(inv)).Append('\n');
                sb.Append(UsbPortKey).Append('=').Append(UsbPort).Append('\n');
                sb.Append(LastUsbPortKey).Append('=').Append(LastUsbPort).Append('\n');
                sb.Append(EasyModeKey).Append('=').Append(EasyMode ? "true" : "false").Append('\n');
                sb.Append(TestShotOnAtStartKey).Append('=').Append(TestShotOnAtStart ? "true" : "false").Append('\n');
                sb.Append(InputIgnoreSecondsKey).Append('=').Append(InputIgnoreSeconds.ToString("R", inv)).Append('\n');
                sb.Append(AutoReturnSecondsKey).Append('=').Append(AutoReturnSeconds.ToString("R", inv)).Append('\n');
                sb.Append(TickOnKey).Append('=').Append(TickOn ? "true" : "false").Append('\n');
                sb.Append(ReplayOnKey).Append('=').Append(ReplayOn ? "true" : "false").Append('\n');
                sb.Append(MasterVolumeKey).Append('=').Append(MasterVolume.ToString("R", inv)).Append('\n');
                sb.Append(MuteKey).Append('=').Append(Mute ? "true" : "false").Append('\n');
                sb.Append(ForceGoalieBlocksKey).Append('=').Append(ForceGoalieBlocks ? "true" : "false").Append('\n');
                sb.Append(TeamSkinKey).Append('=').Append(TeamSkin).Append('\n');
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static int ReadInt(Dictionary<string, string> values, string key, int fallback)
        {
            return values.TryGetValue(key, out var text)
                && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
        }

        private static double ReadDouble(Dictionary<string, string> values, string key, double fallback, NumberRange range)
        {
            return values.TryGetValue(key, out var text)
                && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                && !double.IsNaN(d) && !double.IsInfinity(d) ? range.Clamp(d) : fallback;
        }

        private static bool ReadBool(Dictionary<string, string> values, string key, bool fallback)
        {
            return values.TryGetValue(key, out var text) && bool.TryParse(text, out var b) ? b : fallback;
        }
    }
}
