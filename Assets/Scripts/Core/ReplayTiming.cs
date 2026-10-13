using System;

namespace HockeyGame.Core
{
    /// <summary>
    /// When the Replay runs and what it shows. A Goal, a Save or a Frame Hit holds its result about 4.5 s from when the
    /// puck lands: OnNetSeconds on the net, LookUpSeconds for the camera to look up at the Video Board, ShowSeconds of
    /// slow-motion Replay, LookBackSeconds back down. A Wide Miss or no Shot in time has no Replay and keeps the 2.5 s
    /// result hold (RoundSettings.ResultHoldSeconds). The Round times its result hold from this; the Arena Show its camera.
    /// The Replay plays back what was seen of the Shot (it never decides it again): from the Shot itself (the release,
    /// the puck on the stick, #132) to TailSeconds after the puck lands, slowed to fill ShowSeconds.
    /// </summary>
    public static class ReplayTiming
    {
        public const double OnNetSeconds = 1.5, LookUpSeconds = 0.5, ShowSeconds = 2.0, LookBackSeconds = 0.5;
        /// <summary>The result hold with a Replay, from when the puck lands.</summary>
        public const double HoldSeconds = OnNetSeconds + LookUpSeconds + ShowSeconds + LookBackSeconds;

        /// <summary>The Replay's clip ends this long after the puck lands. It starts at the Shot (ClipStart).</summary>
        public const double TailSeconds = 0.3;

        /// <summary>True when a Replay follows this result: a Goal, a Save or a Frame Hit.</summary>
        public static bool Follows(ArenaResult result) =>
            result == ArenaResult.Goal || result == ArenaResult.Save || result == ArenaResult.FrameHitPost || result == ArenaResult.FrameHitBar;

        /// <summary>True when a Replay follows this Shot result (null: no Shot yet).</summary>
        public static bool Follows(ShotResult? result) => Follows(ArenaShow.ResultOf(result));

        /// <summary>The camera's part of a result hold, landedSeconds after the puck landed, when a Replay follows.</summary>
        public static CameraMode CameraAt(double landedSeconds)
        {
            if (landedSeconds < OnNetSeconds) return CameraMode.OnNet;
            if (landedSeconds < OnNetSeconds + LookUpSeconds) return CameraMode.LookUp;
            if (landedSeconds < OnNetSeconds + LookUpSeconds + ShowSeconds) return CameraMode.Replay;
            return CameraMode.LookBack;
        }

        /// <summary>How far the camera has turned up to the Video Board (0 on the net, 1 on the board), eased in and out.</summary>
        public static double LookUpAt(double landedSeconds)
        {
            double up = (landedSeconds - OnNetSeconds) / LookUpSeconds;
            double back = (landedSeconds - OnNetSeconds - LookUpSeconds - ShowSeconds) / LookBackSeconds;
            return Numbers.SmoothStep01(up) * (1 - Numbers.SmoothStep01(back));
        }

        /// <summary>Seconds into the Replay itself (0 at its start), landedSeconds after the puck landed.</summary>
        public static double ShowSecondsAt(double landedSeconds) =>
            Math.Max(0, Math.Min(ShowSeconds, landedSeconds - OnNetSeconds - LookUpSeconds));

        /// <summary>When the Replay's clip starts, in seconds after the Shot: the release, whatever the flight (#132; it began 0.45 s before the puck landed).</summary>
        public static double ClipStart(double flightSeconds) => 0;

        /// <summary>When the Replay's clip ends, in seconds after the Shot.</summary>
        public static double ClipEnd(double flightSeconds) => Math.Max(0, flightSeconds) + TailSeconds;

        /// <summary>The moment of the Shot (seconds after it) the Replay shows showSeconds into it: the clip, slowed to ShowSeconds.</summary>
        public static double ShotSecondsAt(double showSeconds, double flightSeconds)
        {
            double start = ClipStart(flightSeconds);
            return start + (ClipEnd(flightSeconds) - start) * Numbers.Clamp01(showSeconds / ShowSeconds);
        }
    }
}
