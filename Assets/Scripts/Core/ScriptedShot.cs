namespace HockeyGame.Core
{
    /// <summary>
    /// Today's result for a Shot, from the goalie's race: what the scripted puck flight (ShotEffects) plays and reports at its
    /// Whistle. A Shot on the Net Face is a Save when the race says so, else a Goal; off the Net Face (a Frame Hit too) is a Miss.
    /// The physics puck replaces this with WhistleRule.
    /// </summary>
    public static class ScriptedShot
    {
        public static ShotResult Result(SubmittedShot shot, ShotRace? race)
        {
            ShotResultKind kind = !race.HasValue ? ShotResultKind.Miss
                : race.Value.IsSave ? ShotResultKind.Save : ShotResultKind.Goal;
            return new ShotResult(kind, true, shot.X, shot.Y, shot.SpeedKmh, shot.IsTestShot);
        }

        /// <summary>The Round's current Shot, which must have been submitted (LastShot has a value).</summary>
        public static ShotResult Result(Round round) => Result(round.LastShot.Value, round.LastRace);
    }
}
