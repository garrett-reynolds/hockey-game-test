using System;

namespace HockeyGame.Core
{
    /// <summary>A short move across is a shuffle (he stays square); a longer one is a T-push (the lead skate turns toward the move).</summary>
    public enum PushKind { Shuffle, TPush }
    /// <summary>The goalie's skates, as the shooter sees them: Left is toward -X (the Post.Left side).</summary>
    public enum Skate { Left, Right }
    /// <summary>The skate pushes (he speeds up), then he glides (slows to a stop), then he is still.</summary>
    public enum PushPhase { Push, Glide, Done }

    /// <summary>
    /// A move across from rest (the skate-push model, #125). The skate on the far side of the move pushes at
    /// PushAccelMetresPerSecondSquared up to a peak speed, then he glides, slowing at GlideDecelMetresPerSecondSquared, and
    /// stops exactly on the spot. There is no steady-speed stretch. A pure function of the distance and the time.
    /// </summary>
    public sealed class SkatePush
    {
        readonly double _sign, _ap, _ag;

        public double Metres { get; }
        public PushKind Kind { get; }
        public Skate PushingSkate { get; }
        public double PeakSpeed { get; }
        public double PushSeconds { get; }
        public double GlideSeconds { get; }
        public double TotalSeconds => PushSeconds + GlideSeconds;

        /// <summary>signedMetres is the move along X: negative is toward the left (-X).</summary>
        public SkatePush(double signedMetres, RoundSettings settings)
        {
            _sign = signedMetres < 0 ? -1 : 1;
            Metres = Math.Abs(signedMetres);
            _ap = settings.PushAccelMetresPerSecondSquared;
            _ag = settings.GlideDecelMetresPerSecondSquared;
            Kind = Metres <= settings.ShuffleMaxMetres + 1e-9 ? PushKind.Shuffle : PushKind.TPush;
            PushingSkate = _sign < 0 ? Skate.Right : Skate.Left;
            double k = 1 / _ap + 1 / _ag;
            PeakSpeed = Math.Sqrt(2 * Metres / k);
            PushSeconds = PeakSpeed / _ap;
            GlideSeconds = PeakSpeed / _ag;
        }

        /// <summary>Seconds to cover metres from rest and stop on the spot: sqrt(2 d (1/push + 1/glide)).</summary>
        public static double SecondsFor(double metres, RoundSettings settings)
            => Math.Sqrt(2 * Math.Abs(metres) * (1 / settings.PushAccelMetresPerSecondSquared + 1 / settings.GlideDecelMetresPerSecondSquared));

        /// <summary>How far through the move he is (0 to 1) at u (0 to 1) of its time; the push is the first share of both.</summary>
        public static double Fraction(double u, RoundSettings settings)
        {
            u = Numbers.Clamp01(u);
            double ap = settings.PushAccelMetresPerSecondSquared, ag = settings.GlideDecelMetresPerSecondSquared;
            double share = ag / (ap + ag); // the push's share of both the time and the distance
            return u <= share ? u * u / share : 1 - (1 - u) * (1 - u) / (1 - share);
        }

        /// <summary>Where he is t seconds into the move (metres along X from where he started; ends exactly on the spot).</summary>
        public double Position(double t)
        {
            if (t <= 0) return 0;
            if (t >= TotalSeconds) return _sign * Metres;
            if (t <= PushSeconds) return _sign * 0.5 * _ap * t * t;
            double g = t - PushSeconds;
            return _sign * (0.5 * _ap * PushSeconds * PushSeconds + PeakSpeed * g - 0.5 * _ag * g * g);
        }

        /// <summary>How fast he moves t seconds into the move (m/s along X, signed; zero at the ends).</summary>
        public double Speed(double t)
        {
            if (t <= 0 || t >= TotalSeconds) return 0;
            return _sign * (t <= PushSeconds ? _ap * t : PeakSpeed - _ag * (t - PushSeconds));
        }

        public PushPhase PhaseAt(double t) => t >= TotalSeconds ? PushPhase.Done : t < PushSeconds ? PushPhase.Push : PushPhase.Glide;
    }
}
