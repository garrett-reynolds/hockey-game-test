using System;

namespace HockeyGame.Core
{
    /// <summary>
    /// The goalie squaring up to the shooter: a body turn about its own spot, in degrees about the vertical
    /// (0 = straight out of the net, + = toward the shooter's left, the goalie's right). Steps on scaled (game) time, so it
    /// slows down with the world. While the shooter skates in the goalie shuffles: it waits until the shooter is a couple of
    /// degrees off, then turns smoothly round to face them. Settling (slow-down and window) it follows every change.
    /// </summary>
    public sealed class GoalieTurn
    {
        /// <summary>The widest turn (degrees either way). The skate-in curl peaks at about 28.4 degrees from the goalie.</summary>
        public const double MaxDegrees = 30.0;
        /// <summary>The fastest the goalie turns (degrees per game second).</summary>
        public const double TopSpeedDegrees = 90.0;
        /// <summary>While skating in, a shuffle starts once the shooter is this far off (degrees) ...</summary>
        public const double ShuffleStartDegrees = 2.0;
        /// <summary>... and ends once the goalie is this close.</summary>
        public const double ShuffleEndDegrees = 0.1;
        /// <summary>How quickly a shuffle closes the gap: the remaining angle falls e-fold in this time (game seconds), until the top speed caps it.</summary>
        public const double EaseSeconds = 0.12;

        bool _turning;

        /// <summary>
        /// The facing that squares up to a shooter `right` metres to the right of the goalie (shooter's view) and `ahead`
        /// metres out in front of it, capped at MaxDegrees either way.
        /// </summary>
        public static double Facing(double right, double ahead)
        {
            double degrees = Math.Atan2(-right, ahead) * 180.0 / Math.PI;
            return Math.Max(-MaxDegrees, Math.Min(MaxDegrees, degrees));
        }

        /// <summary>The facing the goalie shows now.</summary>
        public double Shown { get; private set; }

        public GoalieTurn(double facing) { Reset(facing); }

        /// <summary>Faces this way at once (a new Round's skate-in).</summary>
        public void Reset(double facing)
        {
            Shown = facing;
            _turning = false;
        }

        /// <summary>One step of dt game seconds toward the target facing. settle: follow even a small change (no shuffle wait).</summary>
        public void Step(double target, double dt, bool settle)
        {
            double off = target - Shown;
            double size = Math.Abs(off);
            if (!_turning && (settle ? size > 1e-9 : size > ShuffleStartDegrees)) _turning = true;
            if (!_turning || dt <= 0) return;
            double move = Math.Min(size * (1.0 - Math.Exp(-dt / EaseSeconds)), TopSpeedDegrees * dt);
            Shown += Math.Sign(off) * move;
            if (Math.Abs(target - Shown) < ShuffleEndDegrees && !settle) _turning = false;
        }
    }
}
