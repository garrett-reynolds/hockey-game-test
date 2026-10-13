namespace HockeyGame.Core
{
    /// <summary>The Shootout logo (#140): when it shows. The art is StartLogoArt; the places are StartScreenLayout.</summary>
    public static class StartLogo
    {
        /// <summary>True on the start screen with the settings screen closed; false in a Round, on the end screen and behind the settings.</summary>
        public static bool Shown(ScreenFlow flow) => flow.Screen == FlowScreen.Start && !flow.SettingsOpen;
    }
}
