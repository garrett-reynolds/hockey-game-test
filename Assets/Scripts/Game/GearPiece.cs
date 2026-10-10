namespace HockeyGame
{
    /// <summary>
    /// The names of the goalie character's pieces a puck can hit. Each piece of gear is an object named "Gear " + its
    /// name in goalie-dressing.fbx (Tools/make_lifelike_goalie.py names them); Body is the skinned body and clothes.
    /// GoalieCharacter.GearBounds, PuckObstacles pieces and PuckMotion.SurfaceOf all use these names.
    /// </summary>
    public static class GearPiece
    {
        public const string LeftPad = "Left Pad";
        public const string RightPad = "Right Pad";
        public const string Blocker = "Blocker";
        public const string Catcher = "Catcher";
        public const string Stick = "Stick";
        public const string LeftSkate = "Left Skate";
        public const string RightSkate = "Right Skate";
        public const string Mask = "Mask";
        public const string Visor = "Visor";
        public const string Body = "Body";

        /// <summary>Every piece of gear (not Body), as the model file holds them.</summary>
        public static readonly string[] Gear = { LeftPad, RightPad, Blocker, Catcher, Stick, Mask, Visor, LeftSkate, RightSkate };

        const string Prefix = "Gear ";

        /// <summary>The model object's name for a piece of gear ("Gear Left Pad").</summary>
        public static string ObjectName(string piece) => Prefix + piece;

        /// <summary>The piece a model object is ("Gear Left Pad" -> "Left Pad"), or null when it is not gear.</summary>
        public static string PieceOf(string objectName) =>
            objectName.StartsWith(Prefix) ? objectName.Substring(Prefix.Length) : null;
    }
}
