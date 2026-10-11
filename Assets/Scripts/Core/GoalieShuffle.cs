using System;

namespace HockeyGame.Core
{
    /// <summary>
    /// The goalie shuffling across the crease (and in or out) in the Ready Stance to the spot he is headed for (SetUps.Approach).
    /// He moves with the spot as it moves (the angle shifting as the shooter skates in) and closes any gap to it smoothly: he
    /// picks up speed at up to PushMetresPerSecondSquared, never closes faster than TopSpeedMetres, and eases onto the spot, on
    /// scaled (game) time. The gap shows when a new shooter comes in on the other
    /// side from the last Shot: once it is closed he is exactly on the spot.
    /// </summary>
    public sealed class GoalieShuffle
    {
        /// <summary>The fastest he closes a gap (metres per game second).</summary>
        public const double TopSpeedMetres = 0.8;
        /// <summary>How quickly he closes a gap: the rest falls e-fold in this time (game seconds), until the top speed caps it.</summary>
        public const double EaseSeconds = 0.12;
        /// <summary>How quickly he picks up speed closing a gap (metres per game second, per game second).</summary>
        public const double PushMetresPerSecondSquared = 4.0;

        RinkPoint? _headedFor; // the spot of the last Step; none since the last Reset
        double _closing;       // how fast he closed the gap on the last Step (metres per game second)

        /// <summary>Where he stands now (on the ice).</summary>
        public RinkPoint Shown { get; private set; }

        public GoalieShuffle(RinkPoint spot) { Reset(spot); }

        /// <summary>Stands on this spot at once.</summary>
        public void Reset(RinkPoint spot)
        {
            Shown = new RinkPoint(spot.X, 0, spot.Z);
            _headedFor = null;
            _closing = 0;
        }

        /// <summary>One step of dt game seconds, headed for target.</summary>
        public void Step(RinkPoint target, double dt)
        {
            RinkPoint was = _headedFor ?? target;
            _headedFor = target;
            double dx = Shown.X - was.X, dz = Shown.Z - was.Z; // the gap he still has to close
            double gap = Math.Sqrt(dx * dx + dz * dz), left = gap;
            if (dt > 0)
            {
                double speed = Math.Min(TopSpeedMetres, _closing + PushMetresPerSecondSquared * dt);
                double move = Math.Min(gap * (1.0 - Math.Exp(-dt / EaseSeconds)), speed * dt);
                _closing = move / dt;
                left -= move;
            }
            double keep = left < 1e-12 ? 0 : left / gap;
            Shown = new RinkPoint(target.X + dx * keep, 0, target.Z + dz * keep);
        }
    }
}
