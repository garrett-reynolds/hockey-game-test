using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace HockeyGame.Core
{
    /// <summary>
    /// Best score and operator settings, kept in a plain key=value text file.
    /// Numbers use the invariant culture. A missing file, missing key or bad value gives the default.
    /// </summary>
    public sealed class SaveData
    {
        public const string AutoUsbPort = "Auto";

        public int BestScore = 0;
        public string UsbPort = AutoUsbPort;
        /// <summary>In Auto mode, the last port that sent READY; tried first next time. Blank if none yet.</summary>
        public string LastUsbPort = "";
        public bool EasyMode = false;
        public double ContestedLowChance = 0.50;
        public double ContestedTopChance = 0.85;
        public double TopSpeedKmh = 120;
        public bool TestShotOnAtStart = true;
        public double InputIgnoreSeconds = 2;
        public double AutoReturnSeconds = 15;
        public bool TickOn = true;
        public double MasterVolume = 1.0;
        public bool Mute = false;

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

            data.BestScore = ReadInt(values, "bestScore", data.BestScore);
            if (data.BestScore < 0) data.BestScore = 0;
            if (values.TryGetValue("usbPort", out var port) && port.Length > 0) data.UsbPort = port;
            if (values.TryGetValue("lastUsbPort", out var lastPort)) data.LastUsbPort = lastPort;
            data.EasyMode = ReadBool(values, "easyMode", data.EasyMode);
            data.ContestedLowChance = ReadDouble(values, "contestedLowChance", data.ContestedLowChance);
            data.ContestedTopChance = ReadDouble(values, "contestedTopChance", data.ContestedTopChance);
            data.TopSpeedKmh = ReadDouble(values, "topSpeedKmh", data.TopSpeedKmh);
            data.TestShotOnAtStart = ReadBool(values, "testShotOnAtStart", data.TestShotOnAtStart);
            data.InputIgnoreSeconds = ReadDouble(values, "inputIgnoreSeconds", data.InputIgnoreSeconds);
            data.AutoReturnSeconds = ReadDouble(values, "autoReturnSeconds", data.AutoReturnSeconds);
            data.TickOn = ReadBool(values, "tickOn", data.TickOn);
            data.MasterVolume = ReadDouble(values, "masterVolume", data.MasterVolume);
            data.Mute = ReadBool(values, "mute", data.Mute);
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
                sb.Append("bestScore=").Append(BestScore.ToString(inv)).Append('\n');
                sb.Append("usbPort=").Append(UsbPort).Append('\n');
                sb.Append("lastUsbPort=").Append(LastUsbPort).Append('\n');
                sb.Append("easyMode=").Append(EasyMode ? "true" : "false").Append('\n');
                sb.Append("contestedLowChance=").Append(ContestedLowChance.ToString("R", inv)).Append('\n');
                sb.Append("contestedTopChance=").Append(ContestedTopChance.ToString("R", inv)).Append('\n');
                sb.Append("topSpeedKmh=").Append(TopSpeedKmh.ToString("R", inv)).Append('\n');
                sb.Append("testShotOnAtStart=").Append(TestShotOnAtStart ? "true" : "false").Append('\n');
                sb.Append("inputIgnoreSeconds=").Append(InputIgnoreSeconds.ToString("R", inv)).Append('\n');
                sb.Append("autoReturnSeconds=").Append(AutoReturnSeconds.ToString("R", inv)).Append('\n');
                sb.Append("tickOn=").Append(TickOn ? "true" : "false").Append('\n');
                sb.Append("masterVolume=").Append(MasterVolume.ToString("R", inv)).Append('\n');
                sb.Append("mute=").Append(Mute ? "true" : "false").Append('\n');
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Copies the saved tuning values into the Round core's settings. Phase lengths are left as they are.</summary>
        public void ApplyTo(RoundSettings settings)
        {
            settings.ContestedLowChance = ContestedLowChance;
            settings.ContestedTopChance = ContestedTopChance;
            settings.TopSpeedKmh = TopSpeedKmh;
        }

        private static int ReadInt(Dictionary<string, string> values, string key, int fallback)
        {
            return values.TryGetValue(key, out var text)
                && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
        }

        private static double ReadDouble(Dictionary<string, string> values, string key, double fallback)
        {
            return values.TryGetValue(key, out var text)
                && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                && !double.IsNaN(d) && !double.IsInfinity(d) ? d : fallback;
        }

        private static bool ReadBool(Dictionary<string, string> values, string key, bool fallback)
        {
            return values.TryGetValue(key, out var text) && bool.TryParse(text, out var b) ? b : fallback;
        }
    }
}
