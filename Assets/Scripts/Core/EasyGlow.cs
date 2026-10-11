using System;

namespace HockeyGame.Core
{
    /// <summary>
    /// Easy mode's glow on the Gaps: from when the goalie is set (the shooting window opens; a Late Settle once he has settled,
    /// LateSettleSeconds in) until the Shot. A hint only: it asks the Round for its Gaps and changes no numbers. The Gaps change
    /// as the shooter skates in, so each Update measures the next few cells (CellsPerUpdate) and keeps the rest, sweeping the
    /// whole Net Face every Cells / CellsPerUpdate updates: measuring the whole Net Face at once takes too long for one frame.
    /// </summary>
    public sealed class EasyGlow
    {
        /// <summary>Cells measured each Update: on the real port 3 cells take about 1.5 ms, at worst 4 ms (EditMode, #67), and 32 Updates sweep the Net Face.</summary>
        public const int CellsPerUpdate = 3;

        private NetFaceGaps _shown = NetFaceGaps.None;
        private int _next;

        /// <summary>True when the Gaps glow, phaseSeconds (real) into this phase of a Shot with this Set-up.</summary>
        public static bool Shows(bool easyMode, RoundPhase phase, double phaseSeconds, SetUp setUp, RoundSettings settings)
        {
            if (!easyMode || phase != RoundPhase.ShootingWindow) return false;
            return setUp.Kind != SetUpKind.LateSettle || phaseSeconds >= settings.LateSettleSeconds;
        }

        /// <summary>
        /// Each frame: the Gaps to glow now (NetFaceGaps.None when the glow is off). Turning on, it starts from no Gaps and
        /// sweeps them in from the bottom row.
        /// </summary>
        public NetFaceGaps Update(bool easyMode, Round round)
        {
            if (!Shows(easyMode, round.Phase, round.PhaseSecondsElapsed, round.SetUp, round.Settings))
            {
                _shown = NetFaceGaps.None;
                _next = 0;
                return _shown;
            }
            int count = Math.Min(CellsPerUpdate, NetFaceGaps.Cells - _next);
            _shown = _shown.With(round.GapsNow(_next, count), _next, count);
            _next = (_next + count) % NetFaceGaps.Cells;
            return _shown;
        }
    }
}
