using System;

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
    /// through Tick(realSeconds). One Round object serves the whole run, so views can hold it. Settings and
    /// the best score are read from the SaveData each time a Round starts, so changes made on the start
    /// screen (settings, "Reset best score") apply to the next Round.
    /// <para>
    /// Start: a key, click (Press) or Shot starts a Round; that Shot does not count.
    /// Playing: Shots go to the Round; keys and clicks do nothing (no pause). The Round ending shows End.
    /// End: all input is ignored until InputIgnoreSeconds; from then a key, click or Shot goes to Start.
    /// At AutoReturnSeconds it goes to Start by itself.
    /// Settings: F2 or Esc (ToggleSettings) and the Settings button (OpenSettings) open the Settings page over the start screen only.
    /// While it is open, keys, clicks and Shots do not start a Round.
    /// </para>
    /// </summary>
    public sealed class ScreenFlow
    {
        private readonly SaveData _save;
        private readonly RoundSettings _settings;

        /// <summary>
        /// reach: the goalie-reach port the Round decides Shots with. settings: the Round's settings object (made here when null);
        /// pass it when the port needs the same settings (the real port reads the gear speeds from it).
        /// </summary>
        public ScreenFlow(IRandomSource random, SaveData save, IGoalieReach reach, RoundSettings settings = null)
        {
            _save = save;
            _settings = settings ?? new RoundSettings();
            Round = new Round(random, _settings, reach, save.BestScore);
        }

        /// <summary>Raised when a Round beats the best score, after SaveData.BestScore is updated. Save then.</summary>
        public event Action BestScoreChanged;

        public FlowScreen Screen { get; private set; }

        /// <summary>The saved best score (the start screen shows it; a reset on the settings screen shows at once).</summary>
        public int BestScore => _save.BestScore;

        /// <summary>Shots in the Round: the chosen number on the start screen (the Settings page changes it there), the Round's own once it has started.</summary>
        public int ShotsPerRound => Screen == FlowScreen.Start ? _save.ShotsPerRound : Round.ShotCount;

        /// <summary>The one Round for the whole run: idle (or the last Round, over) on the start screen.</summary>
        public Round Round { get; }

        /// <summary>Real seconds spent on the end screen so far; 0 on other screens.</summary>
        public double EndSecondsElapsed { get; private set; }

        /// <summary>True on the end screen once the input-ignore time has passed.</summary>
        public bool EndAcceptsInput => Screen == FlowScreen.End && EndSecondsElapsed >= _save.InputIgnoreSeconds;

        /// <summary>True while the Settings page is open (only ever on the start screen).</summary>
        public bool SettingsOpen { get; private set; }

        /// <summary>F2 or Esc: opens or closes the Settings page. Does nothing during a Round or on the end screen.</summary>
        public void ToggleSettings()
        {
            if (Screen == FlowScreen.Start)
                SettingsOpen = !SettingsOpen;
        }

        /// <summary>
        /// True when Esc opens or closes the Settings page: on the start screen only. Elsewhere Esc is an ordinary key
        /// (on the end screen it counts as "any key", as it did before the page). F2 stays a Settings key everywhere.
        /// </summary>
        public bool EscapeIsSettingsKey => Screen == FlowScreen.Start;

        /// <summary>The Settings button on the start screen: opens the page (and leaves it open when it already is). Does nothing during a Round or on the end screen.</summary>
        public void OpenSettings()
        {
            if (Screen == FlowScreen.Start)
                SettingsOpen = true;
        }

        /// <summary>A key or a mouse click.</summary>
        public void Press()
        {
            if (SettingsOpen) return;
            switch (Screen)
            {
                case FlowScreen.Start:
                    _save.ApplyGoalieTo(_settings); // the Dev part's goalie values act from this Round, never mid-Round (#147)
                    Round.StartRound(_save.BestScore, _save.ShotsPerRound);
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
            if (SettingsOpen) return;
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
            if (Round.IsNewBest)
            {
                _save.BestScore = Round.BestScore;
                BestScoreChanged?.Invoke();
            }
        }

        private void GoToStart()
        {
            Screen = FlowScreen.Start;
            EndSecondsElapsed = 0;
        }
    }
}
