using HockeyGame;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class GoalieSetTests
    {
        private const float Tol = 1e-5f;

        [Test]
        public void Neutral_stance_covers_the_middle_column_fully()
        {
            // Middle column, bottom row: x -0.305..0.305, y 0..0.40667
            GoalieSet.NeutralRectangle(1, 0, out float minX, out float minY, out float maxX, out float maxY);
            Assert.AreEqual(-0.305f, minX, Tol);
            Assert.AreEqual(0.305f, maxX, Tol);
            Assert.AreEqual(0f, minY, Tol);
            Assert.AreEqual(1.22f / 3f, maxY, Tol);
        }

        [Test]
        public void Neutral_stance_half_covers_the_side_columns_inset_on_every_side()
        {
            // Left column, bottom row: zone x -0.915..-0.305 (0.61 wide), y 0..0.40667; inset 30% each side.
            GoalieSet.NeutralRectangle(0, 0, out float minX, out float minY, out float maxX, out float maxY);
            Assert.AreEqual(-0.915f + 0.183f, minX, Tol);
            Assert.AreEqual(-0.305f - 0.183f, maxX, Tol);
            Assert.AreEqual(0.122f, minY, Tol);
            Assert.AreEqual(1.22f / 3f - 0.122f, maxY, Tol);
        }

        [Test]
        public void Set_moves_each_edge_from_the_neutral_rectangle_to_the_pose_rectangle()
        {
            var neutral = new GoalieSet.Rect(0f, 0f, 1f, 1f);
            var pose = new GoalieSet.Rect(2f, 4f, 3f, 5f);
            var halfway = GoalieSet.Lerp(neutral, pose, 0.5f);
            Assert.AreEqual(1f, halfway.MinX, Tol);
            Assert.AreEqual(2f, halfway.MinY, Tol);
            Assert.AreEqual(2f, halfway.MaxX, Tol);
            Assert.AreEqual(3f, halfway.MaxY, Tol);
            Assert.AreEqual(pose.MinX, GoalieSet.Lerp(neutral, pose, 1f).MinX, Tol);
        }

        [Test]
        public void An_Open_Zone_shrinks_to_nothing_at_its_centre()
        {
            var open = GoalieSet.OpenTarget(2, 2);
            Assert.AreEqual(0f, open.MaxX - open.MinX, Tol);
            Assert.AreEqual(0f, open.MaxY - open.MinY, Tol);
            Assert.AreEqual(0.61f, open.MinX, Tol);          // right column centre
            Assert.AreEqual(1.22f * 5f / 6f, open.MinY, Tol); // top row centre
        }
    }
}
