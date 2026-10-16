namespace HockeyGame.Core
{
    /// <summary>Tunable values the Round core needs. Defaults are the spec values (the goalie's: starting numbers, tuned by feel).</summary>
    public sealed class RoundSettings
    {
        // Phase lengths, in real seconds.
        public double SkateInSeconds = 2.5;
        public double SlowDownSeconds = 1.2;
        public double ShootingWindowSeconds = 3.0;
        public double ResultHoldSeconds = 2.5;
        /// <summary>
        /// Extra seconds on the result hold of a Goal or a Save with no Replay, for the goalie's AI clip (PROTOTYPE): the clip
        /// starts at the Whistle, and a Replay's own hold already fits it. 0 (the default) keeps today's timing; the game sets
        /// it to 1.5 while its AI clips are on. Not with a Replay, not for a Miss, Too Late or Frame Hit. In Test Mode a value above 0 only
        /// lengthens the hold (Round.ResultHoldLength): a Goal or a Save as in normal play, a Miss 2.5 s, a Frame Hit and Too Late stay short.
        /// </summary>
        public double ClipHoldSeconds = 0;
        /// <summary>False: no Replay (#145). The game sets it from the saved Replay setting.</summary>
        public bool ReplayOn = true;

        /// <summary>
        /// True while Test Mode runs (#156): endless full-speed Shots from the shooter's spot. Set and cleared by the Round
        /// (StartTestMode, EndTestMode); never saved. Breakaway reads it: no slow-down, no slow-motion look, a still camera.
        /// </summary>
        public bool TestMode;
        /// <summary>Test Mode's own short hold after a Whistle (real seconds), then the goalie is set for the next Shot (#156).</summary>
        public double TestModeHoldSeconds = 1.0;

        /// <summary>
        /// Takes the saved Replay setting each frame (#145): the Replay is on when the setting is on, and never in Test Mode (#156),
        /// so the game's per-frame copy of the saved setting cannot turn the Replay back on there.
        /// </summary>
        public void ApplySavedReplay(bool savedReplayOn)
        {
            ReplayOn = savedReplayOn && !TestMode;
        }

        // The Whistle (WhistleRule), named starting values to tune: a Shot goes dead once the puck is slower than
        // WhistleVerySlowMetresPerSecond for WhistleDeadSeconds in a row, and the Whistle goes at the latest WhistleCapSeconds after it.
        public double WhistleDeadSeconds = 0.3;
        public double WhistleCapSeconds = 3.0;
        public double WhistleVerySlowMetresPerSecond = 0.3;

        // The Shot start (Breakaway.ShotStart): on the ice just below the bottom edge of the shooter's view. The game sets
        // ViewFieldOfViewDegrees from its camera (vertical, as Camera.fieldOfView; 60 is Unity's default). The puck starts
        // ShotStartMarginMetres nearer than where it would first show.
        public double ViewFieldOfViewDegrees = 60;
        public double ShotStartMarginMetres = 0.05;

        // Reach in time: the goalie's Reaction Time is ReactionSeconds, plus or minus up to ReactionWobbleSeconds at random per Shot.
        public double ReactionSeconds = 0.12;
        public double ReactionWobbleSeconds = 0.04;

        // Gear speeds (m/s): hands (glove, blocker) and legs (pads, stick); a push across takes the skate-push model's time
        // (ShuffleMaxMetres, PushAccelMetresPerSecondSquared, GlideDecelMetresPerSecondSquared), up to MaxPushMetres.
        public double HandMetresPerSecond = 8;
        public double LegMetresPerSecond = 6;
        public double MaxPushMetres = 0.6;
        /// <summary>
        /// The Reach Limit (#194): the most distance any piece of gear (hands, legs/pads, stick, or the body's push) can move from
        /// its Set-up place during one Shot (metres). A puck line that needs more is not reachable (GearSpeeds.MoveSeconds).
        /// </summary>
        public double ReachLimitMetres = 0.595;
        /// <summary>A move across up to this far is a shuffle (he stays square); a longer one is a T-push (metres).</summary>
        public double ShuffleMaxMetres = 0.3;
        /// <summary>How hard the pushing skate speeds him up from rest (m/s squared).</summary>
        public double PushAccelMetresPerSecondSquared = 40;
        /// <summary>How hard he slows to a stop while gliding (m/s squared).</summary>
        public double GlideDecelMetresPerSecondSquared = 20;

        /// <summary>How far out from the Net Face (goal line) the goalie sets in a Clean Set-up (metres).</summary>
        public double NormalDepthMetres = 0.2;

        // Set-up Mistakes (see SetUps.Spot): a Cheat sets CheatMetres across toward a post; a Depth sets him on the goal line
        // (depth 0); a Late Settle is LateSettleMetres across, short of his spot, when the window opens and closes
        // onto it over LateSettleSeconds.
        public double CheatMetres = 0.3;
        public double LateSettleMetres = 0.4;
        public double LateSettleSeconds = 1.0;
        // Off = no Late Settle card in the deck (Garrett 2026-10-14: the slide looked like jumping); on = the old deck.
        public bool LateSettles = false;

        /// <summary>
        /// How near thin gear must be to block a Gap (metres, #160): the width of each cast that measures a Gap's room. The total
        /// room stays the same. 0 measures like the old ring did (rays); more shows fewer Gaps. Range 0 to 0.08 (SaveData).
        /// </summary>
        public double GapThinGearReachMetres = 0.04;

        /// <summary>A Goal he missed by less than this (Late Margin, seconds) gets a Stick Slap; otherwise a Slump.</summary>
        public double LateMarginLimitSeconds = 0.05;

        // Before the Shot (#70): how the goalie looks while he waits. Shown only: the Round, the race and the Gaps never read
        // these, so his spot at the Shot and the result are the same with each piece on or off. Each piece has its own switch.
        /// <summary>His head follows the puck on the shooter's stick; the shoulders take HeadShoulderShare of the turn.</summary>
        public bool HeadOnPuck = true;
        public double HeadShoulderShare = 0.15;
        /// <summary>
        /// On the skate-in he waits until he is ShuffleStartMetres off the angle, then shuffles over in ShuffleSeconds (game
        /// seconds), instead of gliding along with the angle. Only the skate-in: the Set-up (a Late Settle's closing too) is unchanged.
        /// </summary>
        public bool SkateInShuffles = true;
        public double ShuffleStartMetres = 0.08;
        public double ShuffleSeconds = 0.3;
        /// <summary>Once set (through the slow-down), SetStillness of his Stance Life goes and he sits SetDropMetres lower.</summary>
        public bool StillOnceSet = true;
        public double SetStillness = 0.85;
        public double SetDropMetres = 0.03;
    }
}
