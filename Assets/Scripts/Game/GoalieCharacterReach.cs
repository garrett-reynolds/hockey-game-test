using System;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The real goalie-reach port: measures the goalie character's body, set in the Shot's Set-up (SetUps.Spot, squared up
    /// to the puck), for each Save Reaction's gear reaching the puck's line, and times each move with the gear speeds and the
    /// push limit (RoundSettings). The puck's edges count (GoalieCharacter.Touches). With no goalie character (no model, or
    /// the block goalie developer flag) no gear can reach: every Shot on the Net Face is a Goal. The Gaps use the same body,
    /// spot and facing; a Gap is a cell with room all round the puck (GoalieCharacter.GapRoomMetres), so it is never a cell Reach finds gear in the way of.
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

        /// <summary>Whether gear or body is in the way of a puck with a Gap's room all round it, at this one Net Face point (what makes a Gap, cell by cell).</summary>
        public bool InTheWayWithRoom(SetUp setUp, AttackSide side, double secondsSinceWindowOpened, RinkPoint shotStart, RinkPoint netPoint)
        {
            if (_character == null || !_character.Present) return false;
            RinkPoint spot = SetUps.Spot(setUp, side, secondsSinceWindowOpened, shotStart, _settings);
            return _character.MeasureOneInTheWay(ToVector(spot), (float)SetUps.Facing(spot, shotStart), ToVector(shotStart), ToVector(netPoint), (float)_settings.GapThinGearReachMetres);
        }

        /// <summary>
        /// Each Net Face cell whose centre has none of his gear or body already in the puck's line (Reach's in the way, with the
        /// puck's edges), measured at the same spot and facing as Reach, for the run of cells asked. With no goalie character
        /// every cell of the run is a Gap.
        /// </summary>
        public NetFaceGaps Gaps(SetUp setUp, AttackSide side, double secondsSinceWindowOpened, RinkPoint shotStart, int firstCell, int cellCount)
        {
            int first = Math.Max(0, firstCell), count = Math.Max(0, Math.Min(NetFaceGaps.Cells, firstCell + cellCount) - first);
            if (_character == null || !_character.Present) return NetFaceGaps.Where((x, y) => true, first, count);
            RinkPoint spot = SetUps.Spot(setUp, side, secondsSinceWindowOpened, shotStart, _settings);
            float facing = (float)SetUps.Facing(spot, shotStart);
            var centres = new Vector3[count];
            var inTheWay = new bool[count];
            for (int i = 0; i < count; i++)
            {
                int cell = first + i;
                NetFaceGaps.CellCentre(cell % NetFaceGaps.Columns, cell / NetFaceGaps.Columns, out double x, out double y);
                centres[i] = ToVector(RinkPoint.OnNetFace(x, y));
            }
            _character.MeasureInTheWay(ToVector(spot), facing, ToVector(shotStart), centres, inTheWay, (float)_settings.GapThinGearReachMetres);
            var isGap = new bool[NetFaceGaps.Cells];
            for (int i = 0; i < count; i++) isGap[first + i] = !inTheWay[i];
            return new NetFaceGaps(isGap);
        }

        static Vector3 ToVector(RinkPoint p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);
    }
}
