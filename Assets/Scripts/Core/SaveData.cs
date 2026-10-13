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
        public static readonly NumberRange ShotsPerRoundRange = new NumberRange(3, 10, 1);
        public const int DefaultShotsPerRound = 6;

        // Dev part, goalie values (#147): ranges and steps from research/dev-setting-ranges.md (#138). Linked limits (the partner
        // bounds the value being changed): Wobble <= Reaction, Leg <= Hand, ShuffleMax <= MaxPush.
        public static readonly NumberRange ReactionRange = new NumberRange(0.04, 0.30, 0.01);
        public static readonly NumberRange WobbleRange = new NumberRange(0, 0.10, 0.01);
        public static readonly NumberRange HandRange = new NumberRange(3, 12, 0.5);
        public static readonly NumberRange LegRange = new NumberRange(3, 12, 0.5);
        public static readonly NumberRange MaxPushRange = new NumberRange(0.1, 0.9, 0.05);
        public static readonly NumberRange ShuffleMaxRange = new NumberRange(0.1, 0.9, 0.05);

        // Dev part, Round timing and Set-up Mistakes (#148), same source. Linked limits: Late Settle time <= min(2.0, window - 1.0)
        // (so it settles inside the window), Late Settle size <= Max push.
        public static readonly NumberRange SkateInRange = new NumberRange(1.5, 5.0, 0.5);
        public static readonly NumberRange ShootingWindowRange = new NumberRange(1.5, 6.0, 0.5);
        public static readonly NumberRange ResultHoldRange = new NumberRange(2.4, 5.0, 0.1);
        public static readonly NumberRange CheatRange = new NumberRange(0.1, 0.6, 0.05);
        public static readonly NumberRange LateSettleMetresRange = new NumberRange(0.2, 0.6, 0.05);
        public static readonly NumberRange LateSettleSecondsRange = new NumberRange(0.4, 2.0, 0.1);

        /// <summary>Dev part, Gaps (#160): how near thin gear must be to block a Gap (metres). 0 measures like the old ring did.</summary>
        public static readonly NumberRange GapThinGearReachRange = new NumberRange(0, 0.08, 0.01);

        /// <summary>The window must stay longer than the Late Settle time by 1 s (at the longest Late Settle time that is 3.0 s).</summary>
        public NumberRange ShootingWindowAllowed => new NumberRange(Math.Max(ShootingWindowRange.Min, LateSettleSeconds + 1.0), ShootingWindowRange.Max, ShootingWindowRange.Step);
        public NumberRange LateSettleSecondsAllowed => new NumberRange(LateSettleSecondsRange.Min, Math.Min(LateSettleSecondsRange.Max, ShootingWindowSeconds - 1.0), LateSettleSecondsRange.Step);
        /// <summary>Never above Max push (when Max push is under the range's floor, it is Max push alone).</summary>
        public NumberRange LateSettleMetresAllowed
        {
            get
            {
                double max = Math.Min(LateSettleMetresRange.Max, MaxPushMetres);
                return new NumberRange(Math.Min(LateSettleMetresRange.Min, max), max, LateSettleMetresRange.Step);
            }
        }

        // What each linked partner allows right now (a range inside its fixed one).
        public NumberRange ReactionAllowed => new NumberRange(Math.Max(ReactionRange.Min, ReactionWobbleSeconds), ReactionRange.Max, ReactionRange.Step);
        public NumberRange WobbleAllowed => new NumberRange(WobbleRange.Min, Math.Min(WobbleRange.Max, ReactionSeconds), WobbleRange.Step);
        public NumberRange HandAllowed => new NumberRange(Math.Max(HandRange.Min, LegMetresPerSecond), HandRange.Max, HandRange.Step);
        public NumberRange LegAllowed => new NumberRange(LegRange.Min, Math.Min(LegRange.Max, HandMetresPerSecond), LegRange.Step);
        public NumberRange MaxPushAllowed => new NumberRange(Math.Max(MaxPushRange.Min, ShuffleMaxMetres), MaxPushRange.Max, MaxPushRange.Step);
        public NumberRange ShuffleMaxAllowed => new NumberRange(ShuffleMaxRange.Min, Math.Min(ShuffleMaxRange.Max, MaxPushMetres), ShuffleMaxRange.Step);

        // File keys, one per setting, shared by Load and Save.
        private const string BestScoreKey = "bestScore"; // the 6-Shot best (the old single best score); other numbers: bestScore3, bestScore8 ...
        private const string ShotsPerRoundKey = "shotsPerRound";
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
        private const string CrowdVolumeKey = "crowdVolume";
        private const string GameVolumeKey = "gameVolume";
        private const string MusicVolumeKey = "musicVolume";
        private const string TeamSkinKey = "teamSkin";
        private const string ForceGoalieBlocksKey = "forceGoalieBlocks"; // replaces goalieCharacterOn (#27), which old files hold as false
        private const string ReactionSecondsKey = "goalieReactionSeconds";
        private const string ReactionWobbleSecondsKey = "goalieReactionWobbleSeconds";
        private const string HandMetresPerSecondKey = "goalieHandMetresPerSecond";
        private const string LegMetresPerSecondKey = "goalieLegMetresPerSecond";
        private const string MaxPushMetresKey = "goalieMaxPushMetres";
        private const string ShuffleMaxMetresKey = "goalieShuffleMaxMetres";
        private const string SkateInSecondsKey = "roundSkateInSeconds";
        private const string ShootingWindowSecondsKey = "roundShootingWindowSeconds";
        private const string ResultHoldSecondsKey = "roundResultHoldSeconds";
        private const string CheatMetresKey = "setUpCheatMetres";
        private const string LateSettleMetresKey = "setUpLateSettleMetres";
        private const string LateSettleSecondsKey = "setUpLateSettleSeconds";
        private const string GapThinGearReachKey = "gapThinGearReachMetres";

        private int _shotsPerRound = DefaultShotsPerRound;
        private readonly Dictionary<int, int> _bests = new Dictionary<int, int>();

        /// <summary>
        /// Shots in a Round, 3 to 10 (Player part of the Settings page). The next Round plays this many. Setting it clamps to the range.
        /// </summary>
        public int ShotsPerRound
        {
            get => _shotsPerRound;
            set => _shotsPerRound = (int)ShotsPerRoundRange.Clamp(value);
        }

        /// <summary>
        /// The best score for the chosen Shots per Round (what the start and end screens show). Setting it sets that number's best.
        /// The old single bestScore key is the 6-Shot best.
        /// </summary>
        public int BestScore
        {
            get => BestFor(ShotsPerRound);
            set => SetBest(ShotsPerRound, value);
        }

        /// <summary>The best score for a Round of this many Shots; 0 when none yet.</summary>
        public int BestFor(int shots) => _bests.TryGetValue(shots, out var best) ? best : 0;

        /// <summary>Sets the best score for a Round of this many Shots (negative becomes 0). Other numbers' bests are untouched.</summary>
        public void SetBest(int shots, int goals)
        {
            if (goals <= 0) _bests.Remove(shots);
            else _bests[shots] = goals;
        }

        /// <summary>"Reset best score": clears the best of every Shots-per-Round number.</summary>
        public void ClearBestScores() => _bests.Clear();

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
        /// <summary>Crowd sounds (crowd loop, cheers, groans, Clap Chant), 0..1, under the master volume.</summary>
        public double CrowdVolume = 1.0;
        /// <summary>Game sounds (horn, buzzer, tick, puck, posts, goalie, strides), 0..1, under the master volume.</summary>
        public double GameVolume = 1.0;
        /// <summary>Music (the goal song), 0..1, under the master volume.</summary>
        public double MusicVolume = 1.0;
        /// <summary>
        /// Developer flag, a toggle in the Settings page's Dev part: shows the block goalie instead of the goalie character
        /// (read when the game starts). Or set forceGoalieBlocks=true in the settings file. The old goalieCharacterOn key is ignored.
        /// </summary>
        public bool ForceGoalieBlocks = false;

        // Dev part, goalie values (#147). The defaults are the code defaults of RoundSettings (a test pins that). They reach a
        // Round only through ApplyGoalieTo, when a Round starts (ScreenFlow); tests and editor tools never read the file.
        public double ReactionSeconds = 0.12;
        public double ReactionWobbleSeconds = 0.04;
        public double HandMetresPerSecond = 8;
        public double LegMetresPerSecond = 6;
        public double MaxPushMetres = 0.6;
        public double ShuffleMaxMetres = 0.3;

        /// <summary>
        /// Puts the Dev part's goalie values on a RoundSettings (clamped to their ranges and linked limits). Called when a Round
        /// starts, never mid-Round. Everything else in the RoundSettings stays as the code made it.
        /// Side effect: it first tidies THIS SaveData's six goalie values in memory (KeepGoalieLimits: clamp, then hold the links).
        /// Nothing is saved to the file by that.
        /// </summary>
        public void ApplyGoalieTo(RoundSettings settings)
        {
            KeepGoalieLimits();
            settings.ReactionSeconds = ReactionSeconds;
            settings.ReactionWobbleSeconds = ReactionWobbleSeconds;
            settings.HandMetresPerSecond = HandMetresPerSecond;
            settings.LegMetresPerSecond = LegMetresPerSecond;
            settings.MaxPushMetres = MaxPushMetres;
            settings.ShuffleMaxMetres = ShuffleMaxMetres;
        }

        /// <summary>Clamps each goalie value to its range, then holds the links: Wobble &lt;= Reaction, Leg &lt;= Hand, ShuffleMax &lt;= MaxPush (the lower partner wins).</summary>
        private void KeepGoalieLimits()
        {
            ReactionSeconds = ReactionRange.Clamp(ReactionSeconds);
            ReactionWobbleSeconds = Math.Min(WobbleRange.Clamp(ReactionWobbleSeconds), ReactionSeconds);
            HandMetresPerSecond = HandRange.Clamp(HandMetresPerSecond);
            LegMetresPerSecond = Math.Min(LegRange.Clamp(LegMetresPerSecond), HandMetresPerSecond);
            MaxPushMetres = MaxPushRange.Clamp(MaxPushMetres);
            ShuffleMaxMetres = Math.Min(ShuffleMaxRange.Clamp(ShuffleMaxMetres), MaxPushMetres);
        }

        // Dev part, Round timing and Set-up Mistakes (#148). Defaults are the code defaults of RoundSettings (a test pins that).
        public double SkateInSeconds = 2.5;
        public double ShootingWindowSeconds = 3.0;
        public double ResultHoldSeconds = 2.5;
        public double CheatMetres = 0.3;
        public double LateSettleMetres = 0.4;
        public double LateSettleSeconds = 1.0;

        /// <summary>Dev part, Gaps (#160). The default is the code default of RoundSettings (a test pins that); an old file with no value loads it.</summary>
        public double GapThinGearReachMetres = 0.04;

        /// <summary>
        /// Puts the Dev part's Round timing and Set-up Mistake values on a RoundSettings (clamped to their ranges and linked
        /// limits, including the goalie's Max push). Called when a Round starts, never mid-Round. Like ApplyGoalieTo it first
        /// tidies this SaveData in memory and saves nothing.
        /// </summary>
        public void ApplyRoundTo(RoundSettings settings)
        {
            KeepGoalieLimits();
            KeepRoundLimits();
            settings.SkateInSeconds = SkateInSeconds;
            settings.ShootingWindowSeconds = ShootingWindowSeconds;
            settings.ResultHoldSeconds = ResultHoldSeconds;
            settings.CheatMetres = CheatMetres;
            settings.LateSettleMetres = LateSettleMetres;
            settings.LateSettleSeconds = LateSettleSeconds;
            settings.GapThinGearReachMetres = GapThinGearReachMetres;
        }

        /// <summary>Holds Late Settle size down to Max push (used when Max push is lowered on the page).</summary>
        public void HoldLateSettleToMaxPush() => LateSettleMetres = Math.Min(LateSettleMetres, MaxPushMetres);

        /// <summary>Clamps each value to its range, then holds the links: Late Settle time &lt;= window - 1.0, Late Settle size &lt;= Max push (the lower partner wins).</summary>
        private void KeepRoundLimits()
        {
            SkateInSeconds = SkateInRange.Clamp(SkateInSeconds);
            ShootingWindowSeconds = ShootingWindowRange.Clamp(ShootingWindowSeconds);
            ResultHoldSeconds = ResultHoldRange.Clamp(ResultHoldSeconds);
            CheatMetres = CheatRange.Clamp(CheatMetres);
            LateSettleSeconds = Math.Min(LateSettleSecondsRange.Clamp(LateSettleSeconds), ShootingWindowSeconds - 1.0);
            LateSettleMetres = Math.Min(LateSettleMetresRange.Clamp(LateSettleMetres), MaxPushMetres);
            GapThinGearReachMetres = GapThinGearReachRange.Clamp(GapThinGearReachMetres);
        }
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

            for (int n = (int)ShotsPerRoundRange.Min; n <= (int)ShotsPerRoundRange.Max; n++)
                data.SetBest(n, ReadInt(values, BestKey(n), 0)); // SetBest drops a negative
            data.ShotsPerRound = ReadInt(values, ShotsPerRoundKey, DefaultShotsPerRound);
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
            data.CrowdVolume = ReadDouble(values, CrowdVolumeKey, data.CrowdVolume, VolumeRange);
            data.GameVolume = ReadDouble(values, GameVolumeKey, data.GameVolume, VolumeRange);
            data.MusicVolume = ReadDouble(values, MusicVolumeKey, data.MusicVolume, VolumeRange);
            data.ForceGoalieBlocks = ReadBool(values, ForceGoalieBlocksKey, data.ForceGoalieBlocks);
            data.ReactionSeconds = ReadDouble(values, ReactionSecondsKey, data.ReactionSeconds, ReactionRange);
            data.ReactionWobbleSeconds = ReadDouble(values, ReactionWobbleSecondsKey, data.ReactionWobbleSeconds, WobbleRange);
            data.HandMetresPerSecond = ReadDouble(values, HandMetresPerSecondKey, data.HandMetresPerSecond, HandRange);
            data.LegMetresPerSecond = ReadDouble(values, LegMetresPerSecondKey, data.LegMetresPerSecond, LegRange);
            data.MaxPushMetres = ReadDouble(values, MaxPushMetresKey, data.MaxPushMetres, MaxPushRange);
            data.ShuffleMaxMetres = ReadDouble(values, ShuffleMaxMetresKey, data.ShuffleMaxMetres, ShuffleMaxRange);
            data.KeepGoalieLimits();
            data.SkateInSeconds = ReadDouble(values, SkateInSecondsKey, data.SkateInSeconds, SkateInRange);
            data.ShootingWindowSeconds = ReadDouble(values, ShootingWindowSecondsKey, data.ShootingWindowSeconds, ShootingWindowRange);
            data.ResultHoldSeconds = ReadDouble(values, ResultHoldSecondsKey, data.ResultHoldSeconds, ResultHoldRange);
            data.CheatMetres = ReadDouble(values, CheatMetresKey, data.CheatMetres, CheatRange);
            data.LateSettleMetres = ReadDouble(values, LateSettleMetresKey, data.LateSettleMetres, LateSettleMetresRange);
            data.LateSettleSeconds = ReadDouble(values, LateSettleSecondsKey, data.LateSettleSeconds, LateSettleSecondsRange);
            data.GapThinGearReachMetres = ReadDouble(values, GapThinGearReachKey, data.GapThinGearReachMetres, GapThinGearReachRange);
            data.KeepRoundLimits();
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
                sb.Append(BestScoreKey).Append('=').Append(BestFor(DefaultShotsPerRound).ToString(inv)).Append('\n');
                sb.Append(ShotsPerRoundKey).Append('=').Append(ShotsPerRound.ToString(inv)).Append('\n');
                for (int n = (int)ShotsPerRoundRange.Min; n <= (int)ShotsPerRoundRange.Max; n++)
                    if (n != DefaultShotsPerRound && BestFor(n) > 0) sb.Append(BestKey(n)).Append('=').Append(BestFor(n).ToString(inv)).Append('\n');
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
                sb.Append(CrowdVolumeKey).Append('=').Append(CrowdVolume.ToString("R", inv)).Append('\n');
                sb.Append(GameVolumeKey).Append('=').Append(GameVolume.ToString("R", inv)).Append('\n');
                sb.Append(MusicVolumeKey).Append('=').Append(MusicVolume.ToString("R", inv)).Append('\n');
                sb.Append(ForceGoalieBlocksKey).Append('=').Append(ForceGoalieBlocks ? "true" : "false").Append('\n');
                sb.Append(ReactionSecondsKey).Append('=').Append(ReactionSeconds.ToString("R", inv)).Append('\n');
                sb.Append(ReactionWobbleSecondsKey).Append('=').Append(ReactionWobbleSeconds.ToString("R", inv)).Append('\n');
                sb.Append(HandMetresPerSecondKey).Append('=').Append(HandMetresPerSecond.ToString("R", inv)).Append('\n');
                sb.Append(LegMetresPerSecondKey).Append('=').Append(LegMetresPerSecond.ToString("R", inv)).Append('\n');
                sb.Append(MaxPushMetresKey).Append('=').Append(MaxPushMetres.ToString("R", inv)).Append('\n');
                sb.Append(ShuffleMaxMetresKey).Append('=').Append(ShuffleMaxMetres.ToString("R", inv)).Append('\n');
                sb.Append(SkateInSecondsKey).Append('=').Append(SkateInSeconds.ToString("R", inv)).Append('\n');
                sb.Append(ShootingWindowSecondsKey).Append('=').Append(ShootingWindowSeconds.ToString("R", inv)).Append('\n');
                sb.Append(ResultHoldSecondsKey).Append('=').Append(ResultHoldSeconds.ToString("R", inv)).Append('\n');
                sb.Append(CheatMetresKey).Append('=').Append(CheatMetres.ToString("R", inv)).Append('\n');
                sb.Append(LateSettleMetresKey).Append('=').Append(LateSettleMetres.ToString("R", inv)).Append('\n');
                sb.Append(LateSettleSecondsKey).Append('=').Append(LateSettleSeconds.ToString("R", inv)).Append('\n');
                sb.Append(GapThinGearReachKey).Append('=').Append(GapThinGearReachMetres.ToString("R", inv)).Append('\n');
                sb.Append(TeamSkinKey).Append('=').Append(TeamSkin).Append('\n');
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string BestKey(int shots) =>
            shots == DefaultShotsPerRound ? BestScoreKey : BestScoreKey + shots.ToString(CultureInfo.InvariantCulture);

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
