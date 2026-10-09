using System.Globalization;

namespace HockeyGame.Core
{
    public enum UsbLineKind { Ignore, Shot, Ready }

    /// <summary>
    /// One text line from the tracking electronics, read as a Shot (x, y, speed), Ready, or Ignore.
    /// Format: ASCII, comma-separated, no spaces: <c>SHOT,x,y,speed</c> or <c>READY</c>.
    /// The line is trimmed first, so <c>\r\n</c> endings and stray spaces at the ends are fine.
    /// Numbers use a dot for decimals, read with the invariant culture.
    /// Wrong tag, wrong field count or a bad number gives Ignore. Out-of-range x or y is still a Shot.
    /// </summary>
    public readonly struct UsbLine
    {
        public UsbLineKind Kind { get; }
        public double X { get; }
        public double Y { get; }
        public double SpeedKmh { get; }

        private UsbLine(UsbLineKind kind, double x, double y, double speedKmh)
        {
            Kind = kind; X = x; Y = y; SpeedKmh = speedKmh;
        }

        private static readonly UsbLine Ignore = new UsbLine(UsbLineKind.Ignore, 0, 0, 0);

        public static UsbLine Read(string text)
        {
            if (text == null) return Ignore;
            var fields = text.Trim().Split(',');

            if (fields[0] == "SHOT" && fields.Length == 4
                && TryNumber(fields[1], out var x) && TryNumber(fields[2], out var y) && TryNumber(fields[3], out var speed))
                return new UsbLine(UsbLineKind.Shot, x, y, speed);

            if (fields[0] == "READY" && fields.Length == 1)
                return new UsbLine(UsbLineKind.Ready, 0, 0, 0);

            return Ignore;
        }

        private static bool TryNumber(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                       CultureInfo.InvariantCulture, out value)
                   && !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
