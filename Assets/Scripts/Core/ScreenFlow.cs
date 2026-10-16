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
    /// Playing: Shots go to the Round; keys and clicks do nothing. Esc pauses (Paused; #196): Tick, Shots and Press do nothing until Resume or Quit to start. The Round ending shows End.
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

        /// <summary>True while Test Mode runs (#156).</summary>
        public bool TestMode => Round.TestMode;

        /// <summary>
        /// Opens Test Mode from the Settings page (#156): the page closes and the first Shot is in the shooting window at once
        /// (Round.StartTestMode). Only from the start screen. Test Mode is never saved: the best score and the save file do not
        /// change, and a new ScreenFlow starts at the start screen.
        /// </summary>
        public void StartTestMode()
        {
            if (Screen != FlowScreen.Start) return;
            SettingsOpen = false;
            Round.StartTestMode(_save.BestScore);
            Screen = FlowScreen.Playing;
        }

        /// <summary>Esc in Test Mode: back to the start screen (#156). Does nothing outside Test Mode.</summary>
        public void LeaveTestMode()
        {
            if (!Round.TestMode) return;
            Round.EndTestMode();
            GoToStart();
        }

        /// <summary>True while a Round is paused (#196): the screen stays Playing and the Round is untouched.</summary>
        public bool Paused { get; private set; }

        /// <summary>Pauses a Round (not Test Mode). Does nothing on other screens.</summary>
        public void Pause()
        {
            if (Screen == FlowScreen.Playing && !Round.TestMode)
                Paused = true;
        }

        /// <summary>Resume button: carries on from the same moment. Does nothing when not paused.</summary>
        public void Resume() => Paused = false;

        /// <summary>Quit to start button: ends the Round with no score and no best-score change, and shows the start screen. Does nothing when not paused.</summary>
        public void QuitToStart()
        {
            if (!Paused) return;
            Round.Abandon();
            GoToStart();
        }

        /// <summary>
        /// The Esc key during a Round or Test Mode: Test Mode goes straight to the start screen (#156); a Round pauses;
        /// a paused Round resumes. Does nothing on the start and end screens (the caller handles Esc there).
        /// </summary>
        public void PressEscape()
        {
            if (Screen != FlowScreen.Playing) return;
            if (Round.TestMode) LeaveTestMode();
            else if (Paused) Resume();
            else Pause();
        }

        /// <summary>A key or a mouse click.</summary>
        public void Press()
        {
            if (SettingsOpen) return;
            switch (Screen)
            {
                case FlowScreen.Start:
                    _save.ApplyGoalieTo(_settings); // the Dev part's goalie values act from this Round, never mid-Round (#147)
                    _save.ApplyRoundTo(_settings); // so do its Round timing and Set-up Mistake values (#148)
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
            if (SettingsOpen || Paused) return;
            if (Screen == FlowScreen.Playing)
                Round.SubmitShot(x, y, speedKmh, isTestShot);
            else
                Press();
        }

        public void Tick(double realSeconds)
        {
            if (Paused) return; // the whole Round stands still (#196)
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
            Paused = false;
            EndSecondsElapsed = 0;
        }
    }
}
