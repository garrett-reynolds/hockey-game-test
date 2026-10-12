using System;

namespace HockeyGame.Core
{
    /// <summary>The crowd's clips (Tools/make_crowd.py CLIPS, same order). All loop except Oh, which plays once.</summary>
    public enum CrowdClip
    {
        SitIdle,    // sit and make small moves (no head turns)
        Wave,       // sit, one hand up and waving (only the fans who wave; the rest sit-idle)
        StandCheer, // stand, arms up, cheering
        Oh,         // a short seated "oh!" (hands to the head), ending seated
        Clap        // sit and clap
    }

    /// <summary>
    /// What one zone's fans play, from its Arena Show crowd state: the clip, where its clock is, and the clip it fades in
    /// from. Sit and Still play sit-idle, Wave wave, Cheer stand-cheer, Clap clap; Oh plays the oh once (each fan up to
    /// OhSpreadSeconds late) and then sits. A new clip fades in over FadeSeconds. Still stops every clock where it is (and
    /// is always the seated pose: from any other clip it starts sit-idle at once). The shader adds each fan's own phase.
    /// </summary>
    public sealed class CrowdMotion
    {
        /// <summary>How long a new clip takes to fade in from the last (seconds).</summary>
        public const double FadeSeconds = 0.4;
        /// <summary>Each fan starts its oh up to this late (seconds; its phase picks how late), so the oh is loose.</summary>
        public const double OhSpreadSeconds = 0.3;

        readonly double[] _seconds;
        bool _ohDone;

        public CrowdClip Clip { get; private set; } = CrowdClip.SitIdle;
        /// <summary>Seconds played of Clip (wrapped at LoopPeriod for the loops).</summary>
        public double Clock { get; private set; }
        public CrowdClip FromClip { get; private set; } = CrowdClip.SitIdle;
        public double FromClock { get; private set; }
        /// <summary>How far Clip has faded in over FromClip (0..1; 1 = Clip alone).</summary>
        public double Blend { get; private set; } = 1;
        /// <summary>Every loop clock wraps at this (seconds): the longest loop, which every loop fits a whole number of
        /// times. One zone clock plays different loops for different fans (the fans who do not wave sit-idle on the wave's
        /// clock), so wrapping at one clip's own length would jump the others.</summary>
        public double LoopPeriod { get; }

        /// <param name="clipSeconds">Each clip's length (seconds), indexed by CrowdClip.</param>
        public CrowdMotion(double[] clipSeconds)
        {
            if (clipSeconds == null || clipSeconds.Length != Enum.GetValues(typeof(CrowdClip)).Length)
                throw new ArgumentException("one length per CrowdClip", nameof(clipSeconds));
            _seconds = clipSeconds;
            for (int c = 0; c < clipSeconds.Length; c++)
                if (Loops((CrowdClip)c)) LoopPeriod = Math.Max(LoopPeriod, clipSeconds[c]);
            for (int c = 0; c < clipSeconds.Length; c++)
            {
                double fits = LoopPeriod / clipSeconds[c];
                if (Loops((CrowdClip)c) && Math.Abs(fits - Math.Round(fits)) > 1e-6)
                    throw new ArgumentException((CrowdClip)c + " (" + clipSeconds[c] + " s) does not fit a whole number of times in " + LoopPeriod + " s", nameof(clipSeconds));
            }
        }

        public static bool Loops(CrowdClip clip) => clip != CrowdClip.Oh;

        /// <summary>The clip a state plays (Oh: before it is done).</summary>
        public static CrowdClip ClipFor(CrowdState state)
        {
            switch (state)
            {
                case CrowdState.Wave: return CrowdClip.Wave;
                case CrowdState.Cheer: return CrowdClip.StandCheer;
                case CrowdState.Oh: return CrowdClip.Oh;
                case CrowdState.Clap: return CrowdClip.Clap;
                default: return CrowdClip.SitIdle; // Sit and Still
            }
        }

        /// <summary>Moves on by step seconds in this state.</summary>
        public void Step(CrowdState state, double step)
        {
            if (state != CrowdState.Oh) _ohDone = false;
            if (state == CrowdState.Still)
            {
                if (Clip != CrowdClip.SitIdle || Blend < 1) { Clip = FromClip = CrowdClip.SitIdle; Clock = FromClock = 0; Blend = 1; }
                return;
            }

            CrowdClip target = state == CrowdState.Oh && _ohDone ? CrowdClip.SitIdle : ClipFor(state);
            if (target != Clip) Start(target);

            double run = step * CrowdLooks.Motion(state);
            Clock = Advance(Clip, Clock, run);
            FromClock = Advance(FromClip, FromClock, run);
            Blend = Math.Min(1, Blend + run / FadeSeconds);

            if (Clip == CrowdClip.Oh && Clock >= _seconds[(int)CrowdClip.Oh] + OhSpreadSeconds - 1e-9)
            {
                _ohDone = true;
                Start(CrowdClip.SitIdle);
            }
        }

        /// <summary>Starts straight in this state's clip, with no fade (the crowd's first moment).</summary>
        public void Snap(CrowdState state)
        {
            _ohDone = false;
            Clip = FromClip = ClipFor(state);
            Clock = FromClock = 0;
            Blend = 1;
        }

        void Start(CrowdClip clip)
        {
            FromClip = Clip;
            FromClock = Clock;
            Clip = clip;
            Clock = 0;
            Blend = 0;
        }

        double Advance(CrowdClip clip, double clock, double run)
        {
            double t = clock + run;
            return Loops(clip) ? t % LoopPeriod : t;
        }
    }
}
