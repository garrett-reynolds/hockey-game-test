namespace HockeyGame.Core
{
    /// <summary>
    /// The race's result for a Shot: a Shot on the Net Face is a Save when the goalie's race says so, else a Goal; off the Net
    /// Face (a Frame Hit too) is a Miss. Not the Round's result, which comes from the Whistle rule (WhistleRule) since #109.
    /// It picks the goalie's reach (PhysicsShotEffects: GoalieView.React) and is what the scripted flight (ShotEffects, until
    /// #113) plays.
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
