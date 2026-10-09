namespace HockeyGame.Core
{
    public enum FlowScreen
    {
        Start,
        Playing,
        End
    }

    /// <summary>
    /// The screens around a Round: start screen, the Round, end screen. Pure logic: time only enters
    /// through Tick(realSeconds). Settings and the best score are read from the SaveData each time a
    /// Round starts, so changes made on the start screen apply to the next Round.
    /// <para>
    /// Start: a key, click (Press) or Shot starts a Round; that Shot does not count.
    /// Playing: Shots go to the Round; keys and clicks do nothing (no pause). The Round ending shows End.
    /// End: all input is ignored until InputIgnoreSeconds; from then a key, click or Shot goes to Start.
    /// At AutoReturnSeconds it goes to Start by itself.
    /// </para>
    /// </summary>
    public sealed class ScreenFlow
    {
        private readonly IRandomSource _random;
        private readonly SaveData _save;

        public ScreenFlow(IRandomSource random, SaveData save)
        {
            _random = random;
            _save = save;
            Round = NewRound();
        }

        public FlowScreen Screen { get; private set; }

        /// <summary>The current Round: idle on the start screen, finished on the end screen.</summary>
        public Round Round { get; private set; }

        /// <summary>Real seconds spent on the end screen so far; 0 on other screens.</summary>
        public double EndSecondsElapsed { get; private set; }

        /// <summary>True on the end screen once the input-ignore time has passed.</summary>
        public bool EndAcceptsInput => Screen == FlowScreen.End && EndSecondsElapsed >= _save.InputIgnoreSeconds;

        /// <summary>A key or a mouse click.</summary>
        public void Press()
        {
            switch (Screen)
            {
                case FlowScreen.Start:
                    Round = NewRound();
                    Round.StartRound();
                    Screen = FlowScreen.Playing;
                    break;
                case FlowScreen.End:
                    if (EndAcceptsInput)
                        GoToStart();
                    break;
            }
        }

        /// <summary>A real Shot or Test Shot. Counts only while Playing; on other screens it acts as a Press.</summary>
        public void SubmitShot(double x, double y, double speedKmh, bool isTestShot)
        {
            if (Screen == FlowScreen.Playing)
                Round.SubmitShot(x, y, speedKmh, isTestShot);
            else
                Press();
        }

        public void Tick(double realSeconds)
        {
            switch (Screen)
            {
                case FlowScreen.Playing:
                    Round.Tick(realSeconds);
                    if (Round.Phase == RoundPhase.RoundOver)
                        EndRound();
                    break;
                case FlowScreen.End:
                    EndSecondsElapsed += realSeconds;
                    if (EndSecondsElapsed >= _save.AutoReturnSeconds)
                        GoToStart();
                    break;
            }
        }

        private void EndRound()
        {
            Screen = FlowScreen.End;
            EndSecondsElapsed = 0;
        }

        private void GoToStart()
        {
            Screen = FlowScreen.Start;
            EndSecondsElapsed = 0;
            Round = NewRound();
        }

        private Round NewRound() => new Round(_random, _save.ToRoundSettings(), _save.BestScore);
    }
}
