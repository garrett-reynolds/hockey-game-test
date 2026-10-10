namespace HockeyGame.Core
{
    /// <summary>What the goalie character does once the puck has got past or onto it (see GLOSSARY: Annoyed Gesture, Relief).</summary>
    public enum AfterShotGesture
    {
        /// <summary>A Save: no gesture, the head follows the rebound.</summary>
        None,
        /// <summary>A GOAL!: slumps forward, drops his head and shakes it.</summary>
        Slump,
        /// <summary>A GOAL!: lifts the stick and slaps it down on the ice, then drops his head.</summary>
        StickSlap,
        /// <summary>A Miss (wide or a Frame Hit): a small exhale and a glance back at the post.</summary>
        Relief,
    }

    /// <summary>
    /// How far along each part of the goalie's after-the-Shot motion is (each 0..1). The reach itself (Breakaway.GoalieSet)
    /// is finished before the puck gets there; all of this starts only once it has.
    /// </summary>
    public readonly struct AfterShotMoment
    {
        /// <summary>A GOAL!: the beaten hand carries on toward where the puck went, too late.</summary>
        public readonly double LateReach;
        /// <summary>A GOAL!: the reaching arms drop back to the stance (the push across the crease is kept).</summary>
        public readonly double Relax;
        /// <summary>How much the head (on a GOAL! the shoulders too) turns to follow the puck: a look back into the net on a GOAL!.</summary>
        public readonly double Watch;
        /// <summary>A Miss: the eyes go from the puck to the post it passed.</summary>
        public readonly double Glance;
        /// <summary>How far through the gesture; 1 is its end pose, held until the result hold ends.</summary>
        public readonly double Gesture;

        public AfterShotMoment(double lateReach, double relax, double watch, double glance, double gesture)
        {
            LateReach = lateReach; Relax = relax; Watch = watch; Glance = glance; Gesture = gesture;
        }
    }

    /// <summary>
    /// The goalie's motion after the Shot, as a pure function of the time since the puck reached the Net Face (and, on a
    /// GOAL!, since it met the netting). The result hold always runs ResultHoldSeconds (2.5 s) after the puck gets there
    /// (Round.DelayResult), and every gesture ends in its held end pose by EndSeconds after it (#44 blends out of that pose).
    /// </summary>
    public static class AfterShot
    {
        /// <summary>A GOAL!'s hand is there this long after the puck.</summary>
        public const double LateReachSeconds = 0.25;
        /// <summary>The latest a GOAL! is taken to meet the netting (s after the Net Face): the look back starts by then.</summary>
        public const double LatestNetting = 0.5;
        /// <summary>A GOAL!, after the netting: the look back turns in over (0.05..0.45 s), the arm drops (0.1..0.6 s).</summary>
        public const double LookBackFrom = 0.05, LookBackTo = 0.45, RelaxFrom = 0.1, RelaxTo = 0.6;
        /// <summary>A GOAL!, after the netting: the annoyed gesture (0.8..1.8 s); the head comes forward (0.8..1.1 s).</summary>
        public const double AnnoyedFrom = 0.8, AnnoyedTo = 1.8, LookForwardTo = 1.1;
        /// <summary>Miss or Save: the head turns to the puck over this long after it gets there.</summary>
        public const double FollowSeconds = 0.15;
        /// <summary>A Miss: the relief beat (0.9..1.9 s after the puck gets there); the eyes go to the post over 0.9..1.3 s.</summary>
        public const double ReliefFrom = 0.9, ReliefTo = 1.9, GlanceTo = 1.3;
        /// <summary>Every gesture is in its end pose this long after the puck gets there, at the latest.</summary>
        public const double EndSeconds = LatestNetting + AnnoyedTo;

        /// <summary>
        /// The gesture for a Shot result, from its Net Face point alone: a Miss is relieved, a Save has none, a GOAL! gets one of
        /// the two Annoyed Gestures by its Zone (column + row even: Slump, odd: StickSlap), so neighbouring Zones differ.
        /// </summary>
        public static AfterShotGesture GestureFor(ShotResultKind kind, double x, double y)
        {
            switch (kind)
            {
                case ShotResultKind.Save: return AfterShotGesture.None;
                case ShotResultKind.Goal: return (Zones.Column(x) + Zones.Row(y)) % 2 == 0 ? AfterShotGesture.Slump : AfterShotGesture.StickSlap;
                default: return AfterShotGesture.Relief;
            }
        }

        /// <summary>
        /// Where the motion is: sinceArrival = seconds since the puck reached the Net Face (negative before); sinceNetting =
        /// seconds since a GOAL! met the netting (negative before, or never: then taken as LatestNetting after arrival).
        /// </summary>
        public static AfterShotMoment At(AfterShotGesture gesture, double sinceArrival, double sinceNetting)
        {
            if (sinceArrival < 0) return default;
            switch (gesture)
            {
                case AfterShotGesture.Slump:
                case AfterShotGesture.StickSlap:
                    {
                        double n = System.Math.Max(sinceNetting, sinceArrival - LatestNetting);
                        double look = Ramp(n, LookBackFrom, LookBackTo) * (1 - Ramp(n, AnnoyedFrom, LookForwardTo));
                        return new AfterShotMoment(Ramp(sinceArrival, 0, LateReachSeconds), Ramp(n, RelaxFrom, RelaxTo), look, 0,
                            Numbers.Clamp01((n - AnnoyedFrom) / (AnnoyedTo - AnnoyedFrom)));
                    }
                case AfterShotGesture.Relief:
                    return new AfterShotMoment(0, 0, Ramp(sinceArrival, 0, FollowSeconds), Ramp(sinceArrival, ReliefFrom, GlanceTo),
                        Numbers.Clamp01((sinceArrival - ReliefFrom) / (ReliefTo - ReliefFrom)));
                default:
                    return new AfterShotMoment(0, 0, Ramp(sinceArrival, 0, FollowSeconds), 0, 0);
            }
        }

        /// <summary>0 before from, 1 after to, eased in and out between.</summary>
        static double Ramp(double t, double from, double to)
        {
            double u = Numbers.Clamp01((t - from) / (to - from));
            return u * u * (3 - 2 * u);
        }
    }
}
