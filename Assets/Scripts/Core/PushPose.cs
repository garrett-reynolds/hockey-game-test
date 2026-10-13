using System;

namespace HockeyGame.Core
{
    /// <summary>
    /// The skate push as it is shown right now (#126): which kind of push, which skate pushes, which phase, and how fast he moves
    /// as a share of the move's peak speed (0..1; it rises through the Push and falls through the Glide). Made by the shuffle feeder
    /// (from GoalieShuffle) or by AtProgress (for a move told as progress, Late Settle and the Save Reaction slide).
    /// </summary>
    public readonly struct PushShown
    {
        /// <summary>A move under this many metres is not a push (the same snap as SkatePush and GoalieShuffle).</summary>
        public const double MinMetres = 0.01;

        public readonly PushKind Kind;
        public readonly Skate Skate;
        public readonly PushPhase Phase;
        /// <summary>Speed / the move's peak speed, 0..1.</summary>
        public readonly double SpeedShare;

        public PushShown(PushKind kind, Skate skate, PushPhase phase, double speedShare)
        {
            Kind = kind;
            Skate = skate;
            Phase = phase;
            SpeedShare = speedShare < 0 ? 0 : speedShare > 1 ? 1 : speedShare;
        }

        /// <summary>No push: Done, share 0.</summary>
        public static PushShown None => new PushShown(PushKind.Shuffle, Skate.Left, PushPhase.Done, 0);

        /// <summary>
        /// A move told as progress: u (0..1) of the way through its time. signedMetres is the move along X (negative toward -X). Under
        /// MinMetres, or at u 1 or more, it is Done. The shape is SkatePush's, so it matches the model that moves the goalie.
        /// </summary>
        public static PushShown AtProgress(double signedMetres, double u, RoundSettings settings)
        {
            if (Math.Abs(signedMetres) < MinMetres || u >= 1) return None;
            var push = new SkatePush(signedMetres, settings);
            double seconds = (u < 0 ? 0 : u) * push.TotalSeconds;
            double share = push.PeakSpeed > 0 ? Math.Abs(push.Speed(seconds)) / push.PeakSpeed : 0;
            return new PushShown(push.Kind, push.PushingSkate, push.PhaseAt(seconds), share);
        }
    }

    /// <summary>The push pose at one moment: both numbers are 0..1 shares of the full pose.</summary>
    public readonly struct PushPoseMoment
    {
        /// <summary>0 the plain stance .. 1 the pushing skate fully out to the side.</summary>
        public readonly double PushOut;
        /// <summary>0 square .. 1 the lead skate fully turned toward the move (a T-push only).</summary>
        public readonly double LeadTurn;
        /// <summary>The skate that pushed last, still coming back after the push moved to the other skate (#130): 0..1 shares, FadeSkate pushed.</summary>
        public readonly double FadeOut, FadeTurn;
        public readonly Skate FadeSkate;

        public PushPoseMoment(double pushOut, double leadTurn) : this(pushOut, leadTurn, 0, 0, Skate.Left) { }

        public PushPoseMoment(double pushOut, double leadTurn, double fadeOut, double fadeTurn, Skate fadeSkate)
        {
            PushOut = pushOut; LeadTurn = leadTurn; FadeOut = fadeOut; FadeTurn = fadeTurn; FadeSkate = fadeSkate;
        }
    }

    /// <summary>The numbers behind the push pose (#126, the skate-push model #125); tuned in Play.</summary>
    public static class PushPose
    {
        /// <summary>The lead skate turns in this long (a T-push), the pushing skate goes out in this long.</summary>
        public const double TurnInSeconds = 0.05, PushInSeconds = 0.06;
        /// <summary>The pose comes back to the plain stance in this long (#130: it outlasts the move, so the push is seen).</summary>
        public const double ReturnSeconds = 0.35;
        /// <summary>When the push changes skate, the old skate's pose fades out in this long (#130: no leg snap).</summary>
        public const double SwitchSeconds = 0.12;
        /// <summary>A T-push squares up (the lead skate turns back) as his speed falls under this share of the peak.</summary>
        public const double SquareBelowShare = 0.5;
        /// <summary>The pushing skate goes out only once the lead skate has turned this far (a T-push).</summary>
        public const double TurnedEnough = 0.8;
        /// <summary>A Done must last this long before the pose lets go, so one Done frame between two pushes does not flicker.</summary>
        public const double HoldSeconds = 0.10;
        /// <summary>The tracker steps in pieces no longer than this.</summary>
        public const double MaxStepSeconds = 1.0 / 120;
        /// <summary>Float fuzz: a step that is an exact number of pieces, or a hold that ends exactly, must not gain a piece.</summary>
        public const double Fuzz = 1e-9;

        /// <summary>The push's share of a move's time (and distance): GlideDecel / (PushAccel + GlideDecel), as SkatePush.Fraction.</summary>
        public static double PushShare(RoundSettings settings)
        {
            double ap = settings.PushAccelMetresPerSecondSquared, ag = settings.GlideDecelMetresPerSecondSquared;
            return ag / (ap + ag);
        }
    }

    /// <summary>
    /// Follows a PushShown through time and gives the push pose (#126): the pose never snaps, it always moves from where it is, and
    /// it shows only what the skates are doing, never where he is. Steps in pieces of at most PushPose.MaxStepSeconds, so a long
    /// step and several short ones end the same. A Done is held for PushPose.HoldSeconds after a Push or Glide.
    /// </summary>
    public sealed class PushPoseTracker
    {
        double _pushOut, _leadTurn;
        PushShown _last = PushShown.None;
        double _holdLeft;

        /// <summary>The pose after the last Step.</summary>
        public PushPoseMoment Now => new PushPoseMoment(_pushOut, _leadTurn, _fadeOut, _fadeTurn, _fadeSkate);

        double _fadeOut, _fadeTurn;
        Skate _fadeSkate;

        /// <summary>
        /// The push the pose is still acting on: the last Push or Glide given to Step, which the pose keeps (kind and skate) through
        /// the Done hold and the return, so the same foot fades out. PushShown.None before any and after Reset. Only meaningful while
        /// Now has a PushOut or LeadTurn above 0.
        /// </summary>
        public PushShown Acting => _last;

        public void Reset()
        {
            _pushOut = 0;
            _leadTurn = 0;
            _last = PushShown.None;
            _holdLeft = 0;
            _fadeOut = 0;
            _fadeTurn = 0;
        }

        public PushPoseMoment Step(PushShown shown, double dt)
        {
            if (!(dt > 0)) return Now;
            int pieces = (int)Math.Ceiling(dt / PushPose.MaxStepSeconds - PushPose.Fuzz);
            if (pieces < 1) pieces = 1;
            double h = dt / pieces;
            for (int i = 0; i < pieces; i++) Piece(shown, h);
            return Now;
        }

        void Piece(PushShown shown, double h)
        {
            _fadeOut = Math.Max(0, _fadeOut - h / PushPose.SwitchSeconds);
            _fadeTurn = Math.Max(0, _fadeTurn - h / PushPose.SwitchSeconds);
            if (shown.Phase != PushPhase.Done)
            {
                // The push moves to the other skate while the pose is out on this one: the old skate fades out on its own (never snaps
                // back) while the new one starts from the plain stance (#130).
                if (shown.Skate != _last.Skate && (_pushOut > 0 || _leadTurn > 0))
                {
                    _fadeSkate = _last.Skate;
                    _fadeOut = _pushOut;
                    _fadeTurn = _leadTurn;
                    _pushOut = 0;
                    _leadTurn = 0;
                }
                _last = shown;
                _holdLeft = PushPose.HoldSeconds;
            }
            else if (_holdLeft > PushPose.Fuzz) // not > 0: a hold of exactly HoldSeconds in pieces must end on the last piece
            {
                shown = _last;
                _holdLeft -= h;
            }

            switch (shown.Phase)
            {
                case PushPhase.Done:
                    _pushOut = Math.Max(0, _pushOut - h / PushPose.ReturnSeconds);
                    _leadTurn = Math.Max(0, _leadTurn - h / PushPose.ReturnSeconds);
                    break;
                case PushPhase.Push:
                    if (shown.Kind == PushKind.Shuffle)
                    {
                        _leadTurn = Toward(_leadTurn, 0, h / PushPose.TurnInSeconds);
                        _pushOut = Math.Min(1, _pushOut + h / PushPose.PushInSeconds);
                    }
                    else
                    {
                        // The lead skate turns first; the pushing skate goes out for the rest of the piece once it is turned enough.
                        double pushTime = h;
                        if (_leadTurn < PushPose.TurnedEnough)
                        {
                            double turning = (PushPose.TurnedEnough - _leadTurn) * PushPose.TurnInSeconds;
                            pushTime = Math.Max(0, h - turning);
                        }
                        _leadTurn = Toward(_leadTurn, 1, h / PushPose.TurnInSeconds);
                        _pushOut = Math.Min(1, _pushOut + pushTime / PushPose.PushInSeconds);
                    }
                    break;
                case PushPhase.Glide:
                    _pushOut = Math.Max(0, _pushOut - h / PushPose.ReturnSeconds);
                    double target = shown.Kind == PushKind.Shuffle ? 0
                        : shown.SpeedShare >= PushPose.SquareBelowShare ? 1 : shown.SpeedShare / PushPose.SquareBelowShare;
                    _leadTurn = Toward(_leadTurn, target, h / PushPose.TurnInSeconds);
                    break;
            }
        }

        static double Toward(double value, double target, double maxChange)
            => value < target ? Math.Min(target, value + maxChange) : Math.Max(target, value - maxChange);
    }
}
