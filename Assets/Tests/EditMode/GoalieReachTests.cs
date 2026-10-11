using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>The Set-up spot, the gear speeds, and the real goalie-reach port on a goalie character built in EditMode.</summary>
    public class GoalieReachTests
    {
        const double Tol = 1e-9;
        static readonly RoundSettings Defaults = new RoundSettings();

        GameObject _parent;

        [SetUp]
        public void MakeRink() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void RemoveRink() { Object.DestroyImmediate(_parent); }

        // ---- The Set-up spot (Core) ----

        [Test]
        public void A_Clean_Set_up_stands_on_the_line_from_the_puck_to_the_net_centre_0_6_m_out()
        {
            var start = new RinkPoint(2.0, 1.1, -6.0);
            RinkPoint spot = SetUps.Spot(SetUp.Clean, AttackSide.Right, 0.5, start, Defaults);
            Assert.AreEqual(-0.6, spot.Z, Tol);
            Assert.AreEqual(0.2, spot.X, Tol, "on the line: 0.6 / 6 of the way across");
            Assert.AreEqual(0.0, spot.Y, Tol);

            RinkPoint middle = SetUps.Spot(SetUp.Clean, AttackSide.Middle, 0, new RinkPoint(0, 1.1, -5), Defaults);
            Assert.AreEqual(0.0, middle.X, Tol);
            Assert.AreEqual(-0.6, middle.Z, Tol);

            var deeper = new RoundSettings { NormalDepthMetres = 0.4 };
            Assert.AreEqual(-0.4, SetUps.Spot(SetUp.Clean, AttackSide.Right, 0, start, deeper).Z, Tol);
        }

        [Test]
        public void The_Set_up_squares_the_goalie_up_to_the_puck()
        {
            Assert.AreEqual(0.0, SetUps.Facing(new RinkPoint(0, 0, -0.6), new RinkPoint(0, 1, -5)), Tol);
            double facing = SetUps.Facing(new RinkPoint(0.2, 0, -0.6), new RinkPoint(2.0, 1.1, -6.0));
            Assert.AreEqual(GoalieTurn.Facing(1.8, 5.4), facing, Tol);
            Assert.Less(facing, 0, "a puck on the shooter's right turns him toward it");
        }

        // ---- Gear speeds (Core) ----

        [Test]
        public void Hands_move_at_8_legs_at_6_and_a_push_across_at_4_m_per_s_up_to_0_6_m()
        {
            Assert.AreEqual(0.05, GearSpeeds.MoveSeconds(SaveReaction.Glove, 0.4, 0, Defaults), Tol);
            Assert.AreEqual(0.05, GearSpeeds.MoveSeconds(SaveReaction.Blocker, 0.4, 0, Defaults), Tol);
            Assert.AreEqual(0.1, GearSpeeds.MoveSeconds(SaveReaction.Pads, 0.6, 0, Defaults), Tol);
            Assert.AreEqual(0.1, GearSpeeds.MoveSeconds(SaveReaction.Stick, 0.6, 0, Defaults), Tol);
            Assert.AreEqual(0.1, GearSpeeds.MoveSeconds(SaveReaction.Body, 0.4, 0, Defaults), Tol);
            // A push across first, then the rest of the glove's travel at hand speed.
            Assert.AreEqual(0.3 / 4 + 0.2 / 8, GearSpeeds.MoveSeconds(SaveReaction.Glove, 0.5, 0.3, Defaults), Tol);
            Assert.AreEqual(0.6 / 4, GearSpeeds.MoveSeconds(SaveReaction.Glove, 0.6, 0.6, Defaults), Tol);
            Assert.IsTrue(double.IsPositiveInfinity(GearSpeeds.MoveSeconds(SaveReaction.Glove, 0.8, 0.7, Defaults)), "past the push limit");
        }

        // ---- The real port (Game) ----

        /// <summary>A goalie character shown in the Ready Stance, and the real port on it.</summary>
        GoalieCharacterReach Port(out GoalieCharacter character)
        {
            character = new GoalieCharacter(_parent.transform);
            Assert.IsTrue(character.Present, "Assets/Resources/Models/goalie-dressing.fbx is missing");
            character.Show(true);
            character.Prepare();
            character.ShowReaction(0f);
            return new GoalieCharacterReach(character, Defaults);
        }

        /// <summary>The Net Face point (0-100) where a puck from `from` through `through` meets the Net Face (z = 0).</summary>
        static RinkPoint NetPointThrough(RinkPoint from, Vector3 through)
        {
            double t = -from.Z / (through.z - from.Z);
            return new RinkPoint(from.X + (through.x - from.X) * t, from.Y + (through.y - from.Y) * t, 0);
        }

        [Test]
        public void A_Shot_at_his_chest_is_already_in_the_way()
        {
            GoalieCharacterReach port = Port(out GoalieCharacter character);
            RinkPoint start = Breakaway.ShotStart(AttackSide.Middle, 1.0, Defaults);
            RinkPoint spot = SetUps.Spot(SetUp.Clean, AttackSide.Middle, 1.0, start, Defaults);
            // His chest as he stands in the Set-up: the stance's chest, moved from where he stands now to the Set-up spot.
            Vector3 chest = character.Bone(HumanBodyBones.Chest).position
                            + new Vector3((float)spot.X, 0f, (float)spot.Z) - new Vector3(0f, 0f, GoalieCharacter.StandZ);
            RinkPoint netPoint = NetPointThrough(start, chest);
            Assert.That(netPoint.Y, Is.InRange(0.0, NetFrame.FaceHeightMetres), "the chest Shot is on the Net Face");

            GoalieReachAnswer answer = port.Reach(SetUp.Clean, AttackSide.Middle, 1.0, start, netPoint);
            Assert.IsTrue(answer.InTheWay, answer.ToString());
        }

        [Test]
        public void A_high_glove_side_corner_is_won_by_the_glove()
        {
            // At 0.6 m out a Clean Set-up covers nearly all the Net Face: only the very corner is left to reach (from the
            // window's opening, farthest out: nearer in, he covers it too).
            GoalieCharacterReach port = Port(out _);
            foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
            {
                RinkPoint start = Breakaway.ShotStart(side, 0, Defaults);
                GoalieReachAnswer answer = port.Reach(SetUp.Clean, side, 0, start, RinkPoint.OnNetFace(99, 99));
                Assert.IsFalse(answer.InTheWay, side + ": " + answer);
                Assert.AreEqual(SaveReaction.Glove, answer.QuickestGear, side + ": " + answer);
                Assert.That(answer.MoveSeconds, Is.GreaterThan(0).And.LessThan(0.3), side + ": " + answer);
            }
        }

        [Test]
        public void A_gear_wins_the_race_only_when_its_own_piece_reaches_the_puck_s_line()
        {
            // The high blocker-side corner: the stick (held in the blocker hand) must not win it because the hand is on the line.
            GoalieCharacterReach port = Port(out _);
            foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
            {
                RinkPoint start = Breakaway.ShotStart(side, 0, Defaults);
                GoalieReachAnswer answer = port.Reach(SetUp.Clean, side, 0, start, RinkPoint.OnNetFace(1, 99));
                Assert.AreNotEqual(SaveReaction.Stick, answer.QuickestGear, side + ": " + answer);
                if (!answer.InTheWay && answer.QuickestGear.HasValue)
                    Assert.IsTrue(SaveReactions.IsHand(answer.QuickestGear.Value) || answer.QuickestGear == SaveReaction.Body, side + ": " + answer);
            }
        }

        [Test]
        public void Measuring_the_race_leaves_the_goalie_as_he_was_shown()
        {
            GoalieCharacterReach port = Port(out GoalieCharacter character);
            Vector3 root = character.Root.transform.position;
            Assert.IsTrue(character.GearBounds(GearPiece.Catcher, out Bounds before));
            Bounds body = character.BodyBounds();

            port.Reach(SetUp.Clean, AttackSide.Right, 1.0, Breakaway.ShotStart(AttackSide.Right, 1.0, Defaults), RinkPoint.OnNetFace(92, 90));

            Assert.IsTrue(character.GearBounds(GearPiece.Catcher, out Bounds after));
            Assert.Less(Vector3.Distance(before.center, after.center), 1e-5f);
            Assert.Less(Vector3.Distance(body.center, character.BodyBounds().center), 1e-5f);
            Assert.Less(Vector3.Distance(root, character.Root.transform.position), 1e-5f);
            Assert.IsNull(character.ReactionPiece);
        }

        [Test]
        public void With_no_goalie_character_no_gear_reaches()
        {
            var port = new GoalieCharacterReach(null, Defaults);
            GoalieReachAnswer answer = port.Reach(SetUp.Clean, AttackSide.Middle, 1.0, Breakaway.ShotStart(AttackSide.Middle, 1.0, Defaults),
                RinkPoint.OnNetFace(50, 50));
            Assert.IsFalse(answer.InTheWay);
            Assert.IsNull(answer.QuickestGear);
            Assert.IsTrue(double.IsPositiveInfinity(answer.MoveSeconds));
        }

        // ---- The Gaps (Game, real port) ----

        static readonly AttackSide[] Sides = { AttackSide.Left, AttackSide.Middle, AttackSide.Right };

        /// <summary>The real port's Gaps for this Set-up and side, windowSeconds in, from where the shooter has skated to.</summary>
        static NetFaceGaps GapsOf(GoalieCharacterReach port, SetUp setUp, AttackSide side, double windowSeconds = 0) =>
            port.Gaps(setUp, side, windowSeconds, Breakaway.ShotStart(side, windowSeconds, Defaults), 0, NetFaceGaps.Cells);

        /// <summary>How many Gaps lie in these columns.</summary>
        static int GapsIn(NetFaceGaps gaps, int fromColumn, int toColumn)
        {
            int count = 0;
            for (int column = fromColumn; column <= toColumn; column++)
            for (int row = 0; row < NetFaceGaps.Rows; row++)
                if (gaps.IsGap(column, row)) count++;
            return count;
        }

        [TestCase(SetUpKind.Cheat, AttackSide.Middle, 0.0)]
        [TestCase(SetUpKind.Depth, AttackSide.Left, 0.5)]
        [TestCase(SetUpKind.LateSettle, AttackSide.Right, 0.3)]
        public void A_cell_is_a_Gap_exactly_when_the_race_finds_no_gear_in_the_way_at_its_centre(SetUpKind kind, AttackSide side, double windowSeconds)
        {
            GoalieCharacterReach port = Port(out _);
            SetUp setUp = SetUp.Of(kind, Post.Left);
            RinkPoint start = Breakaway.ShotStart(side, windowSeconds, Defaults);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            NetFaceGaps gaps = port.Gaps(setUp, side, windowSeconds, start, 0, NetFaceGaps.Cells);
            TestContext.WriteLine("Gaps took " + watch.Elapsed.TotalMilliseconds.ToString("0.0") + " ms; " + gaps.Count + " Gaps");
            Assert.Greater(gaps.Count, 0, "a Set-up Mistake leaves some Gap (else this test proves nothing)");
            for (int column = 0; column < NetFaceGaps.Columns; column++)
            for (int row = 0; row < NetFaceGaps.Rows; row++)
            {
                NetFaceGaps.CellCentre(column, row, out double x, out double y);
                GoalieReachAnswer answer = port.Reach(setUp, side, windowSeconds, start, RinkPoint.OnNetFace(x, y));
                Assert.AreEqual(!answer.InTheWay, gaps.IsGap(column, row), "cell " + column + "," + row + ": " + answer);
            }
        }

        [Test]
        public void A_Clean_Set_up_leaves_fewer_Gaps_than_a_Depth_and_no_more_than_a_Cheat()
        {
            GoalieCharacterReach port = Port(out _);
            foreach (AttackSide side in Sides)
            foreach (double window in new[] { 0.0, 1.0, 2.0 })
            {
                int clean = GapsOf(port, SetUp.Clean, side, window).Count, depth = GapsOf(port, SetUp.Depth, side, window).Count;
                string at = side + " " + window + " s: Clean " + clean;
                Assert.Less(clean, depth, at + ", Depth " + depth);
                foreach (Post post in new[] { Post.Left, Post.Right })
                {
                    int cheat = GapsOf(port, SetUp.Cheat(post), side, window).Count;
                    Assert.LessOrEqual(clean, cheat, at + ", Cheat " + post + " " + cheat);
                }
            }
        }

        [Test]
        public void Gaps_can_be_measured_a_few_cells_at_a_time()
        {
            GoalieCharacterReach port = Port(out _);
            RinkPoint start = Breakaway.ShotStart(AttackSide.Left, 0.4, Defaults);
            NetFaceGaps whole = port.Gaps(SetUp.Depth, AttackSide.Left, 0.4, start, 0, NetFaceGaps.Cells);
            const int first = 40, count = 30;
            NetFaceGaps run = port.Gaps(SetUp.Depth, AttackSide.Left, 0.4, start, first, count);
            for (int column = 0; column < NetFaceGaps.Columns; column++)
            for (int row = 0; row < NetFaceGaps.Rows; row++)
            {
                int cell = NetFaceGaps.Index(column, row);
                bool inRun = cell >= first && cell < first + count;
                Assert.AreEqual(inRun && whole.IsGap(column, row), run.IsGap(column, row), "cell " + column + "," + row);
            }
            Assert.Greater(run.Count, 0, "the run holds some Gap (else this test proves nothing)");
        }

        [TestCase(Post.Left, 6, NetFaceGaps.Columns - 1)]
        [TestCase(Post.Right, 0, NetFaceGaps.Columns / 2 - 1)]
        public void A_Cheat_makes_the_far_post_Gap_bigger_than_a_Clean_Set_up(Post toward, int farFrom, int farTo)
        {
            GoalieCharacterReach port = Port(out _);
            int clean = GapsIn(GapsOf(port, SetUp.Clean, AttackSide.Middle), farFrom, farTo);
            int cheat = GapsIn(GapsOf(port, SetUp.Cheat(toward), AttackSide.Middle), farFrom, farTo);
            Assert.Greater(cheat, clean, "far-post half: Cheat " + cheat + ", Clean " + clean);
        }

        [Test]
        public void A_Depth_makes_the_Gaps_bigger()
        {
            GoalieCharacterReach port = Port(out _);
            foreach (AttackSide side in Sides)
            {
                int clean = GapsOf(port, SetUp.Clean, side).Count, depth = GapsOf(port, SetUp.Depth, side).Count;
                Assert.Greater(depth, clean, side + ": Depth " + depth + ", Clean " + clean);
            }
        }

        [Test]
        public void Finding_the_Gaps_leaves_the_goalie_as_he_was_shown()
        {
            GoalieCharacterReach port = Port(out GoalieCharacter character);
            Vector3 root = character.Root.transform.position;
            Assert.IsTrue(character.GearBounds(GearPiece.Catcher, out Bounds before));
            Bounds body = character.BodyBounds();

            GapsOf(port, SetUp.Cheat(Post.Right), AttackSide.Right, 0.5);

            Assert.IsTrue(character.GearBounds(GearPiece.Catcher, out Bounds after));
            Assert.Less(Vector3.Distance(before.center, after.center), 1e-5f);
            Assert.Less(Vector3.Distance(body.center, character.BodyBounds().center), 1e-5f);
            Assert.Less(Vector3.Distance(root, character.Root.transform.position), 1e-5f);
            Assert.IsNull(character.ReactionPiece);
        }

        [Test]
        public void With_no_goalie_character_the_whole_Net_Face_is_a_Gap()
        {
            var port = new GoalieCharacterReach(null, Defaults);
            Assert.AreEqual(NetFaceGaps.Columns * NetFaceGaps.Rows, GapsOf(port, SetUp.Clean, AttackSide.Middle).Count);
        }

        // ---- The shot start (Core) matches the camera (Game) ----

        [Test]
        public void The_shot_start_is_where_the_camera_starts_the_puck()
        {
            var camera = new GameObject("Camera (test)");
            camera.transform.SetParent(_parent.transform, false);
            foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
            foreach (double window in new[] { 0.0, 1.0, 3.0 })
            {
                Breakaway.SkatePoint(side, 1.0, out double x, out double distance);
                var ground = new Vector3((float)x, 0f, (float)-distance);
                Vector3 toNet = -ground;
                ground += toNet.normalized * (float)Breakaway.CappedDrift(Breakaway.Drift(side, Defaults.SlowDownSeconds, window, Defaults), toNet.magnitude);
                camera.transform.position = new Vector3(ground.x, (float)Breakaway.EyeHeight, ground.z);
                camera.transform.LookAt(new Vector3(0f, (float)(NetFrame.FaceHeightMetres / 2.0), 0f));
                Transform cam = camera.transform;
                Vector3 expected = cam.position + cam.forward * (float)Breakaway.PuckAheadMetres - cam.up * (float)Breakaway.PuckBelowMetres;

                RinkPoint start = Breakaway.ShotStart(side, window, Defaults);
                Assert.Less(Vector3.Distance(expected, new Vector3((float)start.X, (float)start.Y, (float)start.Z)), 1e-4f, side + " " + window);
            }
        }
    }
}
