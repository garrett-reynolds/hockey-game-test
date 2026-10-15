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
    /// is finished before the puck gets there on a Save; on a Goal the race was lost, so it can still be on its way. All of
    /// this starts only once the puck has got there.
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
        /// <summary>How far through the gesture; 1 is its end pose, shown until he recovers.</summary>
        public readonly double Gesture;
        /// <summary>A Save: the glove held up with the puck in it, or the blocker followed through, once the puck is off it.</summary>
        public readonly double FollowThrough;
        /// <summary>How far he has glided back across the crease to his spot (from where his reach pushed him).</summary>
        public readonly double Return;
        /// <summary>How far he has recovered into the Ready Stance: 1 is the Ready Stance, which he shows to the end of the hold.</summary>
        public readonly double Recover;

        public AfterShotMoment(double lateReach, double relax, double watch, double glance, double gesture,
            double followThrough = 0, double @return = 0, double recover = 0)
        {
            LateReach = lateReach; Relax = relax; Watch = watch; Glance = glance; Gesture = gesture;
            FollowThrough = followThrough; Return = @return; Recover = recover;
        }
    }

    /// <summary>
    /// The goalie's motion after the Shot, as a pure function of the time since the puck got there (the Whistle: on a GOAL!
    /// the line crossing) and, on a GOAL!, since it met the netting. The result hold always runs ResultHoldSeconds (2.5 s;
    /// 4.5 s with a Replay) after the Whistle (Round.Whistle), so it starts on the same clock as this motion. Every reaction plays out, glides back to the goalie's spot (on a GOAL! before the Annoyed Gesture,
    /// so it reads on its own) and recovers into the Ready Stance by RecoveredBy (#44), so the hold ends, and the next Shot
    /// starts, in the Ready Stance with no snap.
    /// </summary>
    public static class AfterShot
    {
        /// <summary>A GOAL!'s hand is there this long after the puck.</summary>
        public const double LateReachSeconds = 0.25;
        /// <summary>The latest a GOAL! is taken to meet the netting (s after the Whistle, the line crossing): the look back starts by then.</summary>
        public const double LatestNetting = 0.5;
        /// <summary>A GOAL!, after the netting: the look back turns in over (0.05..0.45 s), the arm drops (0.1..0.6 s).</summary>
        public const double LookBackFrom = 0.05, LookBackTo = 0.45, RelaxFrom = 0.1, RelaxTo = 0.6;
        /// <summary>A GOAL!, after the netting: glides back to his spot while he looks back and his arm drops (0.1..0.6 s), so the gesture reads on its own.</summary>
        public const double GoalReturnFrom = 0.1, GoalReturnTo = 0.6;
        /// <summary>A GOAL!, after the netting: the annoyed gesture (0.6..1.25 s); the head comes forward (0.6..0.9 s).</summary>
        public const double AnnoyedFrom = 0.6, AnnoyedTo = 1.25, LookForwardTo = 0.9;
        /// <summary>A GOAL!: the gesture's end pose holds still this long (s) before he recovers (GLOSSARY: Annoyed Gesture).</summary>
        public const double EndPoseHold = 0.3;
        /// <summary>A GOAL!, after the netting: recovers once the end pose has held (1.55..1.9 s).</summary>
        public const double GoalRecoverFrom = AnnoyedTo + EndPoseHold, GoalRecoverTo = 1.9;
        /// <summary>Miss or Save: the head turns to the puck over this long after it gets there.</summary>
        public const double FollowSeconds = 0.15;
        /// <summary>
        /// Once the puck has touched something (a Save, a post, the boards) the head follows it live: after a short human delay, over a
        /// smooth turn, while the puck is in front of him (full within FrontFullDegrees of his face, none from FrontNoneDegrees).
        /// </summary>
        public const double HeadFollowDelay = 0.15, HeadTurnSeconds = 0.15, FrontFullDegrees = 90, FrontNoneDegrees = 100;

        /// <summary>
        /// How far (0..1) the head follows the live puck before the Whistle: sinceContact = seconds since the puck first touched
        /// something (negative: not yet), yawDegrees = the angle from the way his body faces (not his head) to the puck. 0 for a puck behind him (a GOAL! keeps its look back).
        /// </summary>
        public static double HeadFollow(double sinceContact, double yawDegrees) =>
            Ramp(sinceContact, HeadFollowDelay, HeadFollowDelay + HeadTurnSeconds)
            * (1 - Ramp(System.Math.Abs(yawDegrees), FrontFullDegrees, FrontNoneDegrees));

        /// <summary>
        /// The moment with the head also following the live puck: a Save or a Miss turns the head by the larger of its own Watch and
        /// `follow` (HeadFollow). A GOAL! is left as it is (its look back keeps its own timing).
        /// </summary>
        public static AfterShotMoment WithHeadFollow(AfterShotMoment moment, AfterShotGesture gesture, double follow) =>
            IsGoal(gesture) || follow <= moment.Watch ? moment
                : new AfterShotMoment(moment.LateReach, moment.Relax, follow, moment.Glance, moment.Gesture,
                    moment.FollowThrough, moment.Return, moment.Recover);

        /// <summary>A Miss: the relief beat (0.8..1.7 s after the puck gets there); the eyes go to the post over 0.8..1.2 s.</summary>
        public const double ReliefFrom = 0.8, ReliefTo = 1.7, GlanceTo = 1.2;
        /// <summary>A Miss: glides back to his spot once a Frame Hit's deflection is clear of him (0.9..1.7 s), then recovers (1.7..2.35 s).</summary>
        public const double MissReturnFrom = 0.9, MissReturnTo = 1.7, MissRecoverFrom = 1.7, MissRecoverTo = 2.35;
        /// <summary>A Save: the follow-through once the puck is off the gear, or has come to rest in it (0.25..0.65 s).</summary>
        public const double FollowThroughFrom = 0.25, FollowThroughTo = 0.65;
        /// <summary>A Save: glides back to his spot (1.0..2.0 s), recovers (1.5..2.3 s).</summary>
        public const double SaveReturnFrom = 1.0, SaveReturnTo = 2.0, SaveRecoverFrom = 1.5, SaveRecoverTo = 2.3;
        /// <summary>Every gesture is in its end pose this long after the puck gets there, at the latest.</summary>
        public const double EndSeconds = LatestNetting + AnnoyedTo;
        /// <summary>Every reaction is back in the Ready Stance, on its spot, this long after the puck gets there, at the latest.</summary>
        public const double RecoveredBy = LatestNetting + GoalRecoverTo;

        /// <summary>
        /// The gesture for a Shot result: a Miss is relieved, a Save has none, a GOAL! gets one of the two Annoyed Gestures by how
        /// close he came: a Late Margin (Reach Time - Flight Time) under the limit is a Stick Slap, otherwise a Slump. No forced variety.
        /// </summary>
        public static AfterShotGesture GestureFor(ShotResultKind kind, double lateMarginSeconds, double lateMarginLimitSeconds)
        {
            switch (kind)
            {
                case ShotResultKind.Save: return AfterShotGesture.None;
                case ShotResultKind.Goal: return lateMarginSeconds < lateMarginLimitSeconds ? AfterShotGesture.StickSlap : AfterShotGesture.Slump;
                default: return AfterShotGesture.Relief;
            }
        }

        /// <summary>
        /// Where the motion is: sinceArrival = seconds since the puck got there, at the Whistle (negative before); sinceNetting =
        /// seconds since a GOAL! met the netting (negative before, or never: then taken as LatestNetting after arrival).
        /// </summary>
        public static AfterShotMoment At(AfterShotGesture gesture, double sinceArrival, double sinceNetting)
        {
            if (sinceArrival < 0) return default;
            if (IsGoal(gesture))
            {
                double n = System.Math.Max(sinceNetting, sinceArrival - LatestNetting);
                double look = Ramp(n, LookBackFrom, LookBackTo) * (1 - Ramp(n, AnnoyedFrom, LookForwardTo));
                return new AfterShotMoment(Ramp(sinceArrival, 0, LateReachSeconds), Ramp(n, RelaxFrom, RelaxTo), look, 0,
                    Numbers.Clamp01((n - AnnoyedFrom) / (AnnoyedTo - AnnoyedFrom)), 0,
                    Ramp(n, GoalReturnFrom, GoalReturnTo), Ramp(n, GoalRecoverFrom, GoalRecoverTo));
            }
            if (gesture == AfterShotGesture.Relief)
                return new AfterShotMoment(0, 0, Ramp(sinceArrival, 0, FollowSeconds), Ramp(sinceArrival, ReliefFrom, GlanceTo),
                    Numbers.Clamp01((sinceArrival - ReliefFrom) / (ReliefTo - ReliefFrom)), 0,
                    Ramp(sinceArrival, MissReturnFrom, MissReturnTo), Ramp(sinceArrival, MissRecoverFrom, MissRecoverTo));
            return new AfterShotMoment(0, 0, Ramp(sinceArrival, 0, FollowSeconds), 0, 0,
                Ramp(sinceArrival, FollowThroughFrom, FollowThroughTo),
                Ramp(sinceArrival, SaveReturnFrom, SaveReturnTo), Ramp(sinceArrival, SaveRecoverFrom, SaveRecoverTo));
        }

        /// <summary>
        /// The moment with no reaction of its own (PROTOTYPE: an AI clip plays it instead): no late reach, arm drop, head turn,
        /// glance, gesture or follow-through. The glide back to his spot and the recovery into the Ready Stance stay, so the
        /// clip blends out into the pose the next Shot starts from.
        /// </summary>
        public static AfterShotMoment WithoutReaction(AfterShotMoment moment) =>
            new AfterShotMoment(0, 0, 0, 0, 0, 0, moment.Return, moment.Recover);

        /// <summary>True for a GOAL!'s gestures (the two Annoyed Gestures).</summary>
        public static bool IsGoal(AfterShotGesture gesture) => gesture == AfterShotGesture.Slump || gesture == AfterShotGesture.StickSlap;

        /// <summary>0 before from, 1 after to, eased in and out between (smoothstep). The goalie's gestures use it too.</summary>
        public static double Ramp(double t, double from, double to) => Numbers.SmoothStep01((t - from) / (to - from));
    }
}
