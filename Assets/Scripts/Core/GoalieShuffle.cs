using System;

namespace HockeyGame.Core
{
    /// <summary>
    /// The goalie shuffling across the crease (and in or out) in the Ready Stance to the spot he is headed for (SetUps.Approach),
    /// by the skate-push rule (#125): from rest the skate on the far side pushes (he speeds up at PushAccel), then he glides
    /// (slows at GlideDecel) and stops exactly on the spot, with no steady-speed stretch. From rest this is SkatePush exactly.
    /// A spot that moves chains pushes: he pushes while there is more distance left than his glide needs to stop, and glides at
    /// the rate that stops him on the spot otherwise. A spot behind him makes him glide to rest first, then push the other way.
    /// Runs on scaled (game) time in sub-steps of at most 1/120 s, so big and small steps agree.
    /// </summary>
    public sealed class GoalieShuffle
    {
        const double MaxSubStepSeconds = 1.0 / 120.0;
        const double SnapMetres = 0.005;

        readonly double _ap, _ag; // push acceleration, glide deceleration (metres per game second, per game second)
        double _speed;            // along (_dirX, _dirZ); zero at rest
        double _dirX = 1, _dirZ;  // the way he is moving, or moved last
        double _lastSignX = 1;    // the X sign of the last push, for a push with no X in it

        /// <summary>Where he stands now (on the ice).</summary>
        public RinkPoint Shown { get; private set; }
        /// <summary>A shuffle (he stays square) or a T-push, for the push he is on or was last on.</summary>
        public PushKind Kind { get; private set; }
        /// <summary>The skate pushing him (the one on the far side of the move), for the push he is on or was last on.</summary>
        public Skate PushingSkate { get; private set; } = Skate.Right;
        /// <summary>Pushing, gliding, or Done (at rest on the spot).</summary>
        public PushPhase Phase { get; private set; } = PushPhase.Done;
        /// <summary>How fast he moves now (metres per game second); zero at rest.</summary>
        public double Speed => _speed;

        readonly RoundSettings _settings;

        public GoalieShuffle(RinkPoint spot) : this(spot, new RoundSettings()) { }

        public GoalieShuffle(RinkPoint spot, RoundSettings settings)
        {
            _settings = settings;
            _ap = settings.PushAccelMetresPerSecondSquared;
            _ag = settings.GlideDecelMetresPerSecondSquared;
            Reset(spot);
        }

        /// <summary>Stands on this spot at once, at rest.</summary>
        public void Reset(RinkPoint spot)
        {
            Shown = new RinkPoint(spot.X, 0, spot.Z);
            _speed = 0;
            Phase = PushPhase.Done;
            Kind = PushKind.Shuffle;
            PushingSkate = Skate.Right;
            _lastSignX = 1;
        }

        /// <summary>One step of dt game seconds, headed for target.</summary>
        public void Step(RinkPoint target, double dt)
        {
            if (dt <= 0) return;
            int n = Math.Max(1, (int)Math.Ceiling(dt / MaxSubStepSeconds - 1e-9));
            double h = dt / n;
            for (int i = 0; i < n; i++) SubStep(target, h);
        }

        void SubStep(RinkPoint target, double h)
        {
            double dx = target.X - Shown.X, dz = target.Z - Shown.Z, dist = Math.Sqrt(dx * dx + dz * dz);
            if (_speed <= 0 && dist < SnapMetres) { Land(target); return; }
            double ux = dist > 0 ? dx / dist : _dirX, uz = dist > 0 ? dz / dist : _dirZ;
            if (_speed <= 0)
            {
                _dirX = ux; _dirZ = uz;
                var push = new SkatePush(dx, _settings);
                Kind = push.Kind;
                if (Math.Abs(dx) > 1e-6) { PushingSkate = push.PushingSkate; _lastSignX = dx < 0 ? -1 : 1; }
                else PushingSkate = _lastSignX < 0 ? Skate.Right : Skate.Left;
            }
            bool ahead = _dirX * ux + _dirZ * uz >= 0;
            double before = _speed;
            if (!ahead)
            {
                _speed = Math.Max(0, _speed - _ag * h);
                Phase = PushPhase.Glide;
            }
            else
            {
                _dirX = ux; _dirZ = uz;
                double stop = _speed * _speed / (2 * _ag);
                if (dist > stop + 1e-9) { _speed += _ap * h; Phase = PushPhase.Push; }
                else
                {
                    _speed = Math.Max(0, _speed - _speed * _speed / (2 * Math.Max(dist, 1e-9)) * h);
                    Phase = PushPhase.Glide;
                }
            }
            double move = 0.5 * (before + _speed) * h;
            if (ahead && move >= dist - 1e-12) { Land(target); return; }
            Shown = new RinkPoint(Shown.X + _dirX * move, 0, Shown.Z + _dirZ * move);
            if (_speed <= 0)
            {
                double rx = target.X - Shown.X, rz = target.Z - Shown.Z;
                if (rx * rx + rz * rz < SnapMetres * SnapMetres) Land(target); // at rest on the spot
            }
        }

        void Land(RinkPoint target)
        {
            Shown = new RinkPoint(target.X, 0, target.Z);
            _speed = 0;
            Phase = PushPhase.Done;
        }
    }
}
