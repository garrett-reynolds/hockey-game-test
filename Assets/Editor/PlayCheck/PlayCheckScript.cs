using System;
using System.Collections.Generic;
using System.Globalization;
using HockeyGame.Core;

namespace HockeyGame.PlayCheck
{
    /// <summary>One kind of Play-check event (see PlayCheckScript).</summary>
    public enum PlayCheckEventKind { Goal, Save, Miss, TooLate, Replay, Idle }

    /// <summary>One event of a Play-check script: Reaction counts for a Save, On for Replay, Seconds for Idle.</summary>
    public readonly struct PlayCheckEvent
    {
        public PlayCheckEventKind Kind { get; }
        public SaveReaction Reaction { get; }
        public bool On { get; }
        public double Seconds { get; }
        /// <summary>The event as the script names it, in the one written form (e.g. "save:Glove", "idle:10").</summary>
        public string Text { get; }

        public PlayCheckEvent(PlayCheckEventKind kind, SaveReaction reaction, bool on, double seconds, string text)
        {
            Kind = kind;
            Reaction = reaction;
            On = on;
            Seconds = seconds;
            Text = text;
        }

        /// <summary>True for the events that are one Shot of the Round (goal, save, miss, toolate).</summary>
        public bool IsShot => Kind == PlayCheckEventKind.Goal || Kind == PlayCheckEventKind.Save || Kind == PlayCheckEventKind.Miss || Kind == PlayCheckEventKind.TooLate;
    }

    /// <summary>
    /// The Play-check event script and probe list (pure; no Play needed). Script: events split by "," or a new line:
    /// goal, save:&lt;SaveReaction&gt;, miss, toolate, replay:on|off, idle:&lt;seconds&gt;. Probes: clip, bone:&lt;HumanBodyBones&gt;,
    /// pos:&lt;GameObject name&gt;. A wrong word throws ArgumentException with a clear message, before Play starts.
    /// </summary>
    public static class PlayCheckScript
    {
        public const string EventWords = "goal, save:<Glove|Blocker|Pads|Stick|Body>, miss, toolate, replay:on|off, idle:<seconds>";
        public const string ProbeWords = "clip, bone:<HumanBodyBones>, pos:<GameObject name>";

        public static IReadOnlyList<PlayCheckEvent> Parse(string script)
        {
            if (string.IsNullOrWhiteSpace(script))
                throw new ArgumentException("Play-check script is empty. Use: " + EventWords);
            var events = new List<PlayCheckEvent>();
            // empty lines are skipped; an empty item between commas on one line is an error
            foreach (string line in script.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                if (line.Trim().Length == 0) continue;
                foreach (string item in line.Split(','))
                {
                    string raw = item.Trim();
                    if (raw.Length == 0) throw Wrong("(an empty item between commas)");
                    events.Add(One(raw));
                }
            }
            if (events.Count == 0)
                throw new ArgumentException("Play-check script has no events. Use: " + EventWords);
            return events;
        }

        static PlayCheckEvent One(string raw)
        {
            string word = raw, arg = null;
            int colon = raw.IndexOf(':');
            if (colon >= 0)
            {
                word = raw.Substring(0, colon).Trim();
                arg = raw.Substring(colon + 1).Trim();
            }
            switch (word.ToLowerInvariant())
            {
                case "goal" when arg == null: return new PlayCheckEvent(PlayCheckEventKind.Goal, default, false, 0, "goal");
                case "miss" when arg == null: return new PlayCheckEvent(PlayCheckEventKind.Miss, default, false, 0, "miss");
                case "toolate" when arg == null: return new PlayCheckEvent(PlayCheckEventKind.TooLate, default, false, 0, "toolate");
                case "save" when arg != null:
                    foreach (SaveReaction r in Enum.GetValues(typeof(SaveReaction)))
                        if (string.Equals(r.ToString(), arg, StringComparison.OrdinalIgnoreCase))
                            return new PlayCheckEvent(PlayCheckEventKind.Save, r, false, 0, "save:" + r);
                    break;
                case "replay" when arg != null:
                    if (string.Equals(arg, "on", StringComparison.OrdinalIgnoreCase)) return new PlayCheckEvent(PlayCheckEventKind.Replay, default, true, 0, "replay:on");
                    if (string.Equals(arg, "off", StringComparison.OrdinalIgnoreCase)) return new PlayCheckEvent(PlayCheckEventKind.Replay, default, false, 0, "replay:off");
                    break;
                case "idle" when arg != null:
                    if (double.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out double s) && s >= 0 && !double.IsInfinity(s))
                        return new PlayCheckEvent(PlayCheckEventKind.Idle, default, false, s, "idle:" + s.ToString("R", CultureInfo.InvariantCulture));
                    break;
            }
            throw Wrong("'" + raw + "'");
        }

        static ArgumentException Wrong(string what) =>
            new ArgumentException("Play-check script: unknown event " + what + ". Use: " + EventWords);

        public static IReadOnlyList<string> ParseProbes(string probes)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(probes)) return list;
            foreach (string item in probes.Split(','))
            {
                string raw = item.Trim();
                if (raw.Length == 0) continue;
                list.Add(OneProbe(raw));
            }
            return list;
        }

        static string OneProbe(string raw)
        {
            if (string.Equals(raw, "clip", StringComparison.OrdinalIgnoreCase)) return "clip";
            int colon = raw.IndexOf(':');
            if (colon > 0)
            {
                string word = raw.Substring(0, colon).Trim().ToLowerInvariant();
                string arg = raw.Substring(colon + 1).Trim();
                if (word == "pos" && arg.Length > 0) return "pos:" + arg;
                if (word == "bone" && arg.Length > 0)
                    foreach (string b in Enum.GetNames(typeof(UnityEngine.HumanBodyBones)))
                        if (b != "LastBone" && string.Equals(b, arg, StringComparison.OrdinalIgnoreCase))
                            return "bone:" + b;
            }
            throw new ArgumentException("Play-check probe: unknown probe '" + raw + "'. Use: " + ProbeWords);
        }
    }
}
