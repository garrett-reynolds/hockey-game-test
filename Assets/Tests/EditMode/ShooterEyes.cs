using System.Collections.Generic;
using HockeyGame;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>Where the shooter's eye is when the Shot can be taken, per Attack Side (shared by the goalie tests).</summary>
    internal static class ShooterEyes
    {
        /// <summary>
        /// The shooter's eye when the shooting window opens (the camera has already drifted toward the net through the
        /// slow-down) and where the drift ends, 3 m from the net. Same drift as BreakawayView: scaled time
        /// (Breakaway.TimeScale) times the skate speed, capped by Breakaway.CappedDrift.
        /// </summary>
        internal static IEnumerable<(string, Vector3)> Of(AttackSide side)
        {
            var settings = new RoundSettings();
            double scaled = 0, dt = 0.001;
            for (double t = 0; t < settings.SlowDownSeconds; t += dt)
                scaled += Breakaway.TimeScale(RoundPhase.SlowDown, t) * dt;
            double drift = scaled * Breakaway.SkateSpeed(side, settings);

            Breakaway.SkatePoint(side, 1.0, out double x, out double distance);
            var stop = new Vector3((float)x, 0f, -(float)distance);
            Vector3 toNet = -stop;
            Vector3 open = stop + toNet.normalized * (float)Breakaway.CappedDrift(drift, toNet.magnitude);
            Vector3 near = stop + toNet.normalized * (float)Breakaway.CappedDrift(100.0, toNet.magnitude);
            float eye = (float)Breakaway.EyeHeight;
            yield return ("window opens", new Vector3(open.x, eye, open.z));
            yield return ("near", new Vector3(near.x, eye, near.z));
        }
    }
}
