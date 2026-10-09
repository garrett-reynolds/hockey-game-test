using System;
using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>
    /// Which serial ports to try for the tracking electronics, and the start screen's USB status text.
    /// The port setting is <see cref="SaveData.AutoUsbPort"/> (Auto) or one port name (Manual).
    /// </summary>
    public static class UsbPorts
    {
        /// <summary>True for "Auto" (any case) or a blank setting.</summary>
        public static bool IsAuto(string setting)
        {
            return string.IsNullOrWhiteSpace(setting)
                   || string.Equals(setting.Trim(), SaveData.AutoUsbPort, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ports to try, in order. Manual: only the chosen port, even if the PC does not list it.
        /// Auto: every listed port once, sorted, with the remembered port (last one that sent READY) first.
        /// </summary>
        public static IReadOnlyList<string> ToTry(string setting, string remembered, IEnumerable<string> available)
        {
            if (!IsAuto(setting))
                return new[] { setting.Trim() };

            var ports = new List<string>();
            if (available != null)
                foreach (var name in available)
                    if (!string.IsNullOrWhiteSpace(name) && !ports.Contains(name))
                        ports.Add(name);
            ports.Sort(StringComparer.Ordinal);

            if (!string.IsNullOrEmpty(remembered) && ports.Remove(remembered))
                ports.Insert(0, remembered);
            return ports;
        }

        /// <summary>"Connected (COM3)", or "No electronics found" when no port is connected.</summary>
        public static string StatusText(string connectedPort)
        {
            return string.IsNullOrEmpty(connectedPort) ? "No electronics found" : "Connected (" + connectedPort + ")";
        }
    }
}
