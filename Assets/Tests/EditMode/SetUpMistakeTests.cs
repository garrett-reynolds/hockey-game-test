using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The Set-up Mistakes: where SetUps.Spot puts the goalie (Core), and what the real goalie-reach port measures from there on a
    /// goalie character built in EditMode (a Gap is measured as Net Face points his set gear is not already in the way of).
    /// </summary>
    public class SetUpMistakeTests
    {
        const double Tol = 1e-9;
        static readonly AttackSide[] Sides = { AttackSide.Left, AttackSide.Middle, AttackSide.Right };

        /// <summary>Own numbers, unlike the defaults, so the test shows the sizes come from RoundSettings.</summary>
        static RoundSettings Sized() => new RoundSettings
        {
            NormalDepthMetres = 0.7, CheatMetres = 0.25,LateSettleMetres = 0.5, LateSettleSeconds = 0.8
        };

        static readonly RinkPoint Start = new RinkPoint(1.4, 1.1, -7.0);

        // ---- Where he stands (Core) ----

        [TestCase(Post.Left, -1)]
        [TestCase(Post.Right, 1)]
        public void A_Cheat_sets_across_toward_its_post_at_his_normal_depth_the_whole_window(Post post, int sign)
        {
            var settings = Sized();
            RinkPoint clean = SetUps.Spot(SetUp.Clean, AttackSide.Right, 0, Start, settings);
            foreach (double t in new[] { 0.0, 1.0, 2.9 })
            {
                RinkPoint cheat = SetUps.Spot(SetUp.Cheat(post), AttackSide.Right, t, Start, settings);
                Assert.AreEqual(clean.X + sign * 0.25, cheat.X, Tol, "at " + t + " s");
                Assert.AreEqual(-0.7, cheat.Z, Tol);
            }
        }

        [Test]
        public void A_Depth_sets_on_the_goal_line_the_whole_window()
        {
            var settings = Sized();
            foreach (double t in new[] { 0.0, 2.9 })
            {
                RinkPoint depth = SetUps.Spot(SetUp.Depth, AttackSide.Right, t, Start, settings);
                Assert.AreEqual(0.0, depth.Z, Tol, "on the goal line, whatever the normal depth is");
            }
            settings.NormalDepthMetres = 0.3;
            Assert.AreEqual(0.0, SetUps.Spot(SetUp.Depth, AttackSide.Right, 0, Start, settings).Z, Tol, "tuning the normal depth keeps it on the line");
        }

        [TestCase(0.0)]
        [TestCase(0.4)]
        [TestCase(0.8)]
        [TestCase(2.0)]
        public void A_Late_Settle_starts_short_toward_its_post_and_closes_onto_the_angle_over_its_time(double t)
        {
            var settings = Sized();
            double shortBy = 0.5 * (1 - SkatePush.Fraction(t / 0.8, settings));
            RinkPoint clean = SetUps.Spot(SetUp.Clean, AttackSide.Right, t, Start, settings);
            RinkPoint late = SetUps.Spot(SetUp.LateSettle(Post.Left), AttackSide.Right, t, Start, settings);
            Assert.AreEqual(clean.X - shortBy, late.X, Tol);
            Assert.AreEqual(clean.Z, late.Z, Tol);
            if (t == 0.0) Assert.AreEqual(0.5, shortBy, Tol);
            if (t >= 0.8) Assert.AreEqual(0.0, shortBy, 0.0);
        }

        [Test]
        public void A_Late_Settle_still_closes_in_LateSettleSeconds()
        {
            var settings = Sized();
            double Gap(double t) => SetUps.Spot(SetUp.Clean, AttackSide.Right, t, Start, settings).X - SetUps.Spot(SetUp.LateSettle(Post.Left), AttackSide.Right, t, Start, settings).X;
            Assert.Less(Gap(0.8 - 1e-6), 0.001, "under 1 mm just before");
            Assert.AreEqual(0.0, Gap(0.8), 0.0, "exactly closed at LateSettleSeconds");
            double previous = Gap(0);
            for (int i = 1; i <= 100; i++)
            {
                double gap = Gap(0.8 * i / 100.0);
                Assert.LessOrEqual(gap, previous + 1e-12, "never grows, sample " + i);
                Assert.GreaterOrEqual(gap, 0.0, "never goes past the angle, sample " + i);
                previous = gap;
            }
        }

        [Test]
        public void A_Late_Settle_speeds_up_then_slows()
        {
            var settings = Sized();
            double Gap(double t) => SetUps.Spot(SetUp.Clean, AttackSide.Right, t, Start, settings).X - SetUps.Spot(SetUp.LateSettle(Post.Left), AttackSide.Right, t, Start, settings).X;
            const int n = 80;
            double dt = 0.8 / n;
            var speed = new double[n];
            for (int i = 0; i < n; i++) speed[i] = (Gap(i * dt) - Gap((i + 1) * dt)) / dt;
            int peak = 0;
            for (int i = 1; i < n; i++) if (speed[i] > speed[peak]) peak = i;
            Assert.Less(peak, n / 3 + 1, "peak speed comes in the first third");
            for (int i = 1; i <= peak; i++) Assert.GreaterOrEqual(speed[i], speed[i - 1] - 1e-9, "rises at step " + i);
            for (int i = peak + 1; i < n; i++) Assert.LessOrEqual(speed[i], speed[i - 1] + 1e-9, "falls at step " + i);
            Assert.Less(speed[n - 1], speed[peak] * 0.2, "slow at the glide end");
        }

        [Test]
        public void Mistakes_mirror_left_and_right()
        {
            var settings = new RoundSettings();
            foreach (double t in new[] { 0.0, 0.5, 1.5 })
            {
                RinkPoint leftStart = Breakaway.ShotStart(AttackSide.Left, t, settings), rightStart = Breakaway.ShotStart(AttackSide.Right, t, settings);
                foreach (SetUp[] pair in new[]
                {
                    new[] { SetUp.Clean, SetUp.Clean }, new[] { SetUp.Depth, SetUp.Depth },
                    new[] { SetUp.Cheat(Post.Left), SetUp.Cheat(Post.Right) }, new[] { SetUp.Cheat(Post.Right), SetUp.Cheat(Post.Left) },
                    new[] { SetUp.LateSettle(Post.Left), SetUp.LateSettle(Post.Right) },
                })
                {
                    RinkPoint left = SetUps.Spot(pair[0], AttackSide.Left, t, leftStart, settings);
                    RinkPoint right = SetUps.Spot(pair[1], AttackSide.Right, t, rightStart, settings);
                    Assert.AreEqual(-right.X, left.X, 1e-9, pair[0] + " at " + t + " s");
                    Assert.AreEqual(right.Z, left.Z, 1e-9, pair[0] + " at " + t + " s");
                }
            }
        }

        [Test]
        public void The_goalie_s_motion_shows_the_Late_Settle_sliding_onto_his_spot_and_holds_the_other_mistakes()
        {
            // SetUps.Approach is what the goalie's motion follows (BreakawayView); in the window it is the Spot the port measures.
            var settings = new RoundSettings();
            foreach (AttackSide side in Sides)
            {
                foreach (SetUp setUp in new[] { SetUp.Cheat(Post.Right), SetUp.Depth, SetUp.LateSettle(Post.Left) })
                {
                    RinkPoint atOpen = SetUps.Approach(setUp, side, RoundPhase.SlowDown, settings.SlowDownSeconds, settings);
                    Assert.AreEqual(0, RinkPoint.Distance(atOpen, SetUps.Spot(setUp, side, 0, Breakaway.ShotStart(side, 0, settings), settings)), 1e-6,
                        side + " " + setUp + ": set on his Set-up spot as the window opens");
                    foreach (double t in new[] { 0.0, 0.3, 0.7, 1.2 })
                    {
                        RinkPoint shown = SetUps.Approach(setUp, side, RoundPhase.ShootingWindow, t, settings);
                        RinkPoint spot = SetUps.Spot(setUp, side, t, Breakaway.ShotStart(side, t, settings), settings);
                        Assert.AreEqual(0, RinkPoint.Distance(shown, spot), 1e-9, side + " " + setUp + " at " + t + " s");
                    }
                }
                RinkPoint clean = SetUps.Spot(SetUp.Clean, side, 0.5, Breakaway.ShotStart(side, 0.5, settings), settings);
                RinkPoint sliding = SetUps.Approach(SetUp.LateSettle(Post.Left), side, RoundPhase.ShootingWindow, 0.5, settings);
                Assert.AreEqual(clean.X - 0.4 * (1 - SkatePush.Fraction(0.5 / 1.0, settings)), sliding.X, 1e-9, side + ": the push-then-glide share across at 0.5 s");
            }
        }

        // ---- What the real port measures from there ----

        GameObject _parent;

        [SetUp]
        public void MakeRink() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void RemoveRink() { Object.DestroyImmediate(_parent); }

        GoalieCharacterReach Port()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assert.IsTrue(character.Present, "Assets/Resources/Models/goalie-dressing.fbx is missing");
            character.Show(true);
            character.Prepare();
            character.ShowReaction(0f);
            return new GoalieCharacterReach(character, new RoundSettings());
        }

        /// <summary>Net Face points on a 10 x 10 grid (x, y from 5 to 95) that his set gear is not already in the way of.</summary>
        static int OpenPoints(GoalieCharacterReach port, SetUp setUp, AttackSide side, double t, System.Func<double, double, bool> where)
        {
            RinkPoint start = Breakaway.ShotStart(side, t, new RoundSettings());
            int open = 0;
            for (int x = 5; x < 100; x += 10)
                for (int y = 5; y < 100; y += 10)
                    if (where(x, y) && !port.Reach(setUp, side, t, start, RinkPoint.OnNetFace(x, y)).InTheWay)
                        open++;
            return open;
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void A_Cheat_makes_the_Gap_away_from_its_post_bigger_than_a_Clean_Set_up()
        {
            GoalieCharacterReach port = Port();
            foreach (AttackSide side in Sides)
            {
                int cleanFarLeft = OpenPoints(port, SetUp.Clean, side, 0.5, (x, y) => x < 50);
                int cheatFarLeft = OpenPoints(port, SetUp.Cheat(Post.Right), side, 0.5, (x, y) => x < 50);
                Assert.Greater(cheatFarLeft, cleanFarLeft, side + ": cheating to the right post opens the left side");

                int cleanFarRight = OpenPoints(port, SetUp.Clean, side, 0.5, (x, y) => x > 50);
                int cheatFarRight = OpenPoints(port, SetUp.Cheat(Post.Left), side, 0.5, (x, y) => x > 50);
                Assert.Greater(cheatFarRight, cleanFarRight, side + ": cheating to the left post opens the right side");
            }
        }

        [Test]
        public void A_Cheat_leaves_a_far_corner_open_that_a_Clean_Set_up_has_gear_in_the_way_of()
        {
            // A right attack, the goalie cheating to the near (right) post: the far (left) low corner opens.
            GoalieCharacterReach port = Port();
            RinkPoint start = Breakaway.ShotStart(AttackSide.Right, 0.5, new RoundSettings());
            int corners = 0;
            foreach (var (x, y) in new[] { (8.0, 8.0), (8.0, 20.0), (15.0, 8.0), (8.0, 50.0), (8.0, 80.0), (8.0, 92.0) })
            {
                bool clean = port.Reach(SetUp.Clean, AttackSide.Right, 0.5, start, RinkPoint.OnNetFace(x, y)).InTheWay;
                bool cheat = port.Reach(SetUp.Cheat(Post.Right), AttackSide.Right, 0.5, start, RinkPoint.OnNetFace(x, y)).InTheWay;
                Assert.IsFalse(cheat && !clean, $"({x}, {y}): in the way in the Cheat but not when Clean");
                if (clean && !cheat) corners++;
            }
            Assert.Greater(corners, 0, "at least one far-side point is in the way when Clean and open in the Cheat");
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void A_Depth_makes_the_Gaps_bigger_than_a_Clean_Set_up()
        {
            GoalieCharacterReach port = Port();
            foreach (AttackSide side in Sides)
            {
                int clean = OpenPoints(port, SetUp.Clean, side, 0.5, (x, y) => true);
                int depth = OpenPoints(port, SetUp.Depth, side, 0.5, (x, y) => true);
                Assert.Greater(depth, clean, side + ": more of the Net Face open");

                int cleanCorners = OpenPoints(port, SetUp.Clean, side, 0.5, (x, y) => (x < 20 || x > 80) && (y < 20 || y > 80));
                int depthCorners = OpenPoints(port, SetUp.Depth, side, 0.5, (x, y) => (x < 20 || x > 80) && (y < 20 || y > 80));
                Assert.GreaterOrEqual(depthCorners, cleanCorners, side + ": the corners open too");
            }
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void A_Late_Settle_has_a_bigger_Gap_than_a_Clean_Set_up_when_the_window_opens_and_none_once_settled()
        {
            GoalieCharacterReach port = Port();
            foreach (AttackSide side in Sides)
            {
                // Still coming across from the right post: the left side is open as the window opens, closed once settled.
                int early = OpenPoints(port, SetUp.LateSettle(Post.Right), side, 0.0, (x, y) => x < 50);
                int cleanEarly = OpenPoints(port, SetUp.Clean, side, 0.0, (x, y) => x < 50);
                Assert.Greater(early, cleanEarly, side + ": short of his spot at the window's opening");
                int settled = OpenPoints(port, SetUp.LateSettle(Post.Right), side, 1.0, (x, y) => x < 50);
                int clean = OpenPoints(port, SetUp.Clean, side, 1.0, (x, y) => x < 50);
                Assert.AreEqual(clean, settled, side + ": settled after 1 s, as a Clean Set-up");
            }
        }
    }
}
