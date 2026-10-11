using System;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The real goalie-reach port: measures the goalie character's body, set in the Shot's Set-up (SetUps.Spot, squared up
    /// to the puck), for each Save Reaction's gear reaching the puck's line, and times each move with the gear speeds and the
    /// push limit (RoundSettings). The puck's edges count (GoalieCharacter.Touches). With no goalie character (no model, or
    /// the block goalie developer flag) no gear can reach: every Shot on the Net Face is a Goal.
    /// </summary>
    public sealed class GoalieCharacterReach : IGoalieReach
    {
        static readonly SaveReaction[] Gear = (SaveReaction[])Enum.GetValues(typeof(SaveReaction));

        readonly GoalieCharacter _character;
        readonly RoundSettings _settings;

        public GoalieCharacterReach(GoalieCharacter character, RoundSettings settings)
        {
            _character = character;
            _settings = settings;
        }

        public GoalieReachAnswer Reach(SetUp setUp, AttackSide side, double secondsSinceWindowOpened, RinkPoint shotStart, RinkPoint netPoint)
        {
            if (_character == null || !_character.Present) return GoalieReachAnswer.NoGearReaches;
            RinkPoint spot = SetUps.Spot(setUp, side, secondsSinceWindowOpened, shotStart, _settings);
            float facing = (float)SetUps.Facing(spot, shotStart);
            Vector3 from = ToVector(shotStart), to = ToVector(netPoint), stand = ToVector(spot);

            bool inTheWay = false;
            SaveReaction? quickest = null;
            double quickestSeconds = double.PositiveInfinity;
            foreach (SaveReaction gear in Gear)
            {
                bool reaches = _character.MeasureReach(gear, stand, from, to, facing, out float gearMetres, out float pushMetres, out bool blocked);
                inTheWay |= blocked;
                if (!reaches) continue;
                double seconds = GearSpeeds.MoveSeconds(gear, gearMetres, pushMetres, _settings);
                if (seconds < quickestSeconds)
                {
                    quickestSeconds = seconds;
                    quickest = gear;
                }
            }
            if (inTheWay) return GoalieReachAnswer.AlreadyInTheWay(quickest);
            return quickest.HasValue ? GoalieReachAnswer.Reaches(quickest.Value, quickestSeconds) : GoalieReachAnswer.NoGearReaches;
        }

        static Vector3 ToVector(RinkPoint p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);
    }
}
