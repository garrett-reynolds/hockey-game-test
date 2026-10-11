using System;
using System.Collections.Generic;
using HockeyGame.Core;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// Test goalie-reach port with fixed answers, so a test decides the race without a goalie body. Records what Round asked.
    /// </summary>
    internal sealed class FakeReach : IGoalieReach
    {
        private readonly Func<double, double, GoalieReachAnswer> _answer;

        /// <summary>answer: the answer for a Shot at Net Face point (x, y), 0-100 from the bottom-left.</summary>
        public FakeReach(Func<double, double, GoalieReachAnswer> answer) { _answer = answer; }

        /// <summary>The same answer for every Shot.</summary>
        public FakeReach(GoalieReachAnswer answer) : this((x, y) => answer) { }

        /// <summary>Gear always already in the puck's line: every Shot on the Net Face is a Save.</summary>
        public static FakeReach InTheWay() => new FakeReach(GoalieReachAnswer.AlreadyInTheWay());

        /// <summary>No gear ever reaches: every Shot on the Net Face is a Goal.</summary>
        public static FakeReach NeverReaches() => new FakeReach(GoalieReachAnswer.NoGearReaches);

        /// <summary>A Save (gear in the way) where saved(x, y) is true, else a Goal (no gear reaches).</summary>
        public static FakeReach Saving(Func<double, double, bool> saved) =>
            new FakeReach((x, y) => saved(x, y) ? GoalieReachAnswer.AlreadyInTheWay() : GoalieReachAnswer.NoGearReaches);

        /// <summary>Every question Round asked, in order.</summary>
        public readonly List<Ask> Asks = new List<Ask>();

        public GoalieReachAnswer Reach(SetUp setUp, AttackSide side, double secondsSinceWindowOpened, RinkPoint shotStart, RinkPoint netPoint)
        {
            Asks.Add(new Ask(setUp, side, secondsSinceWindowOpened, shotStart, netPoint));
            double x = (netPoint.X / NetFrame.FaceWidthMetres + 0.5) * 100.0, y = netPoint.Y / NetFrame.FaceHeightMetres * 100.0;
            return _answer(x, y);
        }

        public readonly struct Ask
        {
            public readonly SetUp SetUp;
            public readonly AttackSide Side;
            public readonly double SecondsSinceWindowOpened;
            public readonly RinkPoint ShotStart, NetPoint;

            public Ask(SetUp setUp, AttackSide side, double secondsSinceWindowOpened, RinkPoint shotStart, RinkPoint netPoint)
            {
                SetUp = setUp;
                Side = side;
                SecondsSinceWindowOpened = secondsSinceWindowOpened;
                ShotStart = shotStart;
                NetPoint = netPoint;
            }
        }
    }
}
