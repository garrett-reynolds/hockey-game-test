using System.Collections.Generic;
using System.Linq;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The Set-up deck and the Attack Side pick through Round: ScriptedRandom / SeededRandom and a fake goalie-reach port.</summary>
    public class RoundSetUpTests
    {
        const double Tol = 1e-9;

        static RoundSettings Fixed() => new RoundSettings { ReactionSeconds = 0.12, ReactionWobbleSeconds = 0 };

        /// <summary>Plays a whole Round (no Shots), giving each Shot's Set-up and Attack Side.</summary>
        static List<(SetUp setUp, AttackSide side)> PlayRound(Round round)
        {
            var shots = new List<(SetUp, AttackSide)>();
            for (int shot = 1; shot <= Round.ShotsPerRound; shot++)
            {
                Assert.AreEqual(shot, round.ShotNumber);
                shots.Add((round.SetUp, round.AttackSide));
                round.Tick(round.Settings.SkateInSeconds + round.Settings.SlowDownSeconds + round.Settings.ShootingWindowSeconds + round.Settings.ResultHoldSeconds + 0.001);
            }
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            return shots;
        }

        [Test]
        public void Every_Round_deals_4_Set_up_Mistakes_each_type_at_least_once_and_2_Clean_from_left_middle_and_right_twice_each()
        {
            for (int seed = 1; seed <= 60; seed++)
            {
                var round = new Round(new SeededRandom(seed), Fixed(), FakeReach.NeverReaches(), 0);
                for (int roundNumber = 1; roundNumber <= 2; roundNumber++) // StartRound again deals a new deck and side pick
                {
                    round.StartRound();
                    var shots = PlayRound(round);
                    string because = "seed " + seed + " Round " + roundNumber;
                    Assert.AreEqual(2, shots.Count(s => s.setUp == SetUp.Clean), because);
                    foreach (SetUpKind mistake in new[] { SetUpKind.Cheat, SetUpKind.Depth, SetUpKind.LateSettle })
                        Assert.That(shots.Count(s => s.setUp.Kind == mistake), Is.GreaterThanOrEqualTo(1), because + ": " + mistake);
                    foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
                        Assert.AreEqual(2, shots.Count(s => s.side == side), because + ": " + side);
                }
            }
        }

        [Test]
        public void The_Round_rolls_the_deck_then_the_Attack_Sides_then_per_Shot_the_post_side_then_the_wobble()
        {
            var settings = new RoundSettings { ReactionSeconds = 0.12, ReactionWobbleSeconds = 0.04 };
            var random = new ScriptedRandom(new[] { 0.5 },
                0.9, 0.9, 0, 0, 0, 0,   // deck: a 4th Late Settle; Cheat swaps to the end: Clean, Depth, Late Settle, Late Settle, Clean, Cheat
                0.9, 0, 0, 0, 0,        // sides: Right, Left, Middle, Middle, Right, Left
                0.75, 0.0,              // Shot 1: right post (unused by a Clean Set-up), wobble -0.04
                0.25, 1.0,              // Shot 2: left post (unused by a Depth), wobble +0.04
                0.25, 0.5);             // Shot 3: Late Settle toward the left post, no wobble
            var round = new Round(random, settings, FakeReach.NeverReaches(), 0);
            round.StartRound();
            var expected = new[]
            {
                (SetUp.Clean, AttackSide.Right, 0.08), (SetUp.Depth, AttackSide.Left, 0.16), (SetUp.LateSettle(Post.Left), AttackSide.Middle, 0.12),
            };
            foreach (var (setUp, side, reaction) in expected)
            {
                Assert.AreEqual(setUp, round.SetUp, "Shot " + round.ShotNumber);
                Assert.AreEqual(side, round.AttackSide, "Shot " + round.ShotNumber);
                Assert.AreEqual(reaction, round.ReactionSeconds, Tol, "Shot " + round.ShotNumber);
                round.Tick(settings.SkateInSeconds + settings.SlowDownSeconds + settings.ShootingWindowSeconds + settings.ResultHoldSeconds + 0.001);
            }
        }

        [TestCase(Post.Left)]
        [TestCase(Post.Right)]
        public void The_Cheat_post_side_is_rolled_on_a_middle_attack_too(Post post)
        {
            var round = new Round(ScriptedRandom.Forcing(AttackSide.Middle, SetUp.Cheat(post)), Fixed(), FakeReach.NeverReaches(), 0);
            round.StartRound();
            Assert.AreEqual(AttackSide.Middle, round.AttackSide);
            Assert.AreEqual(SetUp.Cheat(post), round.SetUp);
        }

        /// <summary>A Round in its shooting window, windowSeconds in, on this Attack Side and Set-up.</summary>
        static Round InWindow(IGoalieReach reach, RoundSettings settings, AttackSide side, SetUp setUp, double windowSeconds)
        {
            var round = new Round(ScriptedRandom.Forcing(side, setUp), settings, reach, 0);
            round.StartRound();
            round.Tick(settings.SkateInSeconds + settings.SlowDownSeconds + windowSeconds);
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
            return round;
        }

        [TestCase(0.25)]
        [TestCase(0.5)]
        [TestCase(1.5)]
        public void A_Late_Settle_is_measured_where_he_is_at_the_Shot_moment(double windowSeconds)
        {
            var settings = Fixed();
            // 0.4 m short in 1.0 s (the defaults), closing in a push then a glide (#125).
            double shortBy = 0.4 * (1 - SkatePush.Fraction(windowSeconds / 1.0, settings));
            var reach = FakeReach.NeverReaches();
            var round = InWindow(reach, settings, AttackSide.Right, SetUp.LateSettle(Post.Left), windowSeconds);
            round.SubmitShotAndWhistle(40, 60, 100, false);

            FakeReach.Ask ask = reach.Asks.Single();
            Assert.AreEqual(SetUp.LateSettle(Post.Left), ask.SetUp);
            Assert.AreEqual(windowSeconds, ask.SecondsSinceWindowOpened, 1e-6, "the port is asked at the time since the window opened");
            ShotRace race = round.LastRace.Value;
            Assert.AreEqual(SetUp.LateSettle(Post.Left), race.SetUp);
            RinkPoint angle = SetUps.OnTheAngle(race.ShotStart, settings);
            Assert.AreEqual(angle.X - shortBy, race.Spot.X, 1e-6, "his spot at the Shot: still short by " + shortBy + " m");
        }

        [Test]
        public void A_Depth_s_Flight_Time_runs_to_where_the_puck_passes_him_on_the_goal_line()
        {
            var settings = Fixed();
            var clean = InWindow(FakeReach.NeverReaches(), settings, AttackSide.Middle, SetUp.Clean, 1.0);
            var depth = InWindow(FakeReach.NeverReaches(), settings, AttackSide.Middle, SetUp.Depth, 1.0);
            clean.SubmitShotAndWhistle(50, 50, 100, false);
            depth.SubmitShotAndWhistle(50, 50, 100, false);
            Assert.AreEqual(0.0, depth.LastRace.Value.Spot.Z, 1e-9);
            Assert.Greater(depth.LastRace.Value.FlightSeconds, clean.LastRace.Value.FlightSeconds);
        }
    }
}
