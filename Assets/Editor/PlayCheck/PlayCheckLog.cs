using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HockeyGame.PlayCheck
{
    /// <summary>
    /// One Play-check log line (JSON lines: one JSON object per line, keys always in this order):
    /// {"t":12.345,"frame":340,"type":"event","event":"save:Glove","phase":"ResultHold","result":"Save","holdStart":12.100,"holdEnd":null,"probes":{"clip":"Idle"}}
    /// t, holdStart and holdEnd are game seconds since the run started (3 decimals, invariant culture); a missing value is null.
    /// </summary>
    public static class PlayCheckLog
    {
        public static string Line(double t, int frame, string type, string evt, string phase, string result,
            double? holdStart, double? holdEnd, IReadOnlyList<KeyValuePair<string, string>> probes)
        {
            var sb = new StringBuilder(160);
            sb.Append("{\"t\":").Append(Seconds(t));
            sb.Append(",\"frame\":").Append(frame.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"type\":").Append(Text(type));
            sb.Append(",\"event\":").Append(Text(evt));
            sb.Append(",\"phase\":").Append(Text(phase));
            sb.Append(",\"result\":").Append(Text(result));
            sb.Append(",\"holdStart\":").Append(holdStart.HasValue ? Seconds(holdStart.Value) : "null");
            sb.Append(",\"holdEnd\":").Append(holdEnd.HasValue ? Seconds(holdEnd.Value) : "null");
            sb.Append(",\"probes\":{");
            if (probes != null)
                for (int i = 0; i < probes.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(Text(probes[i].Key)).Append(':').Append(Text(probes[i].Value));
                }
            return sb.Append("}}").ToString();
        }

        static string Seconds(double s) => s.ToString("F3", CultureInfo.InvariantCulture);

        /// <summary>A JSON string (quoted, escaped), or null.</summary>
        public static string Text(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2).Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
