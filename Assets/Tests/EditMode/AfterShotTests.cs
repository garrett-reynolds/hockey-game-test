using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// What the goalie character does after the puck gets there (#43): on a GOAL! a late reach, a look back into the net and
    /// an Annoyed Gesture; on a Miss (wide or a Frame Hit) the head follows the puck, then a Relief beat; on a Save the head
    /// follows the rebound. Times are seconds after the puck reaches the Net Face (and after it meets the netting).
    /// </summary>
    public class AfterShotTests
    {
        [TestCase(15, 85, AfterShotGesture.Slump, TestName = "Goal top left: slumps and shakes his head")]
        [TestCase(88, 82, AfterShotGesture.Slump, TestName = "Goal top right: slumps and shakes his head")]
        [TestCase(50, 50, AfterShotGesture.Slump, TestName = "Goal through the middle: slumps")]
        [TestCase(50, 90, AfterShotGesture.StickSlap, TestName = "Goal top middle: slaps the stick on the ice")]
        [TestCase(10, 50, AfterShotGesture.StickSlap, TestName = "Goal mid left: slaps the stick on the ice")]
        [TestCase(85, 50, AfterShotGesture.StickSlap, TestName = "Goal mid right: slaps the stick on the ice")]
        public void A_goal_s_annoyed_gesture_comes_from_its_Net_Face_point(double x, double y, AfterShotGesture expected)
        {
            Assert.AreEqual(expected, AfterShot.GestureFor(ShotResultKind.Goal, x, y));
            Assert.AreEqual(expected, AfterShot.GestureFor(ShotResultKind.Goal, x, y), "the same point always gets the same gesture");
        }

        [TestCase(-8, 50, TestName = "Wide left")]
        [TestCase(103, 50, TestName = "Off the right post (a Frame Hit)")]
        [TestCase(50, 120, TestName = "Over the crossbar")]
        public void A_miss_is_followed_by_relief(double x, double y)
        {
            Assert.AreEqual(AfterShotGesture.Relief, AfterShot.GestureFor(ShotResultKind.Miss, x, y));
        }

        [Test]
        public void A_save_has_no_gesture()
        {
            Assert.AreEqual(AfterShotGesture.None, AfterShot.GestureFor(ShotResultKind.Save, 88, 82));
        }

        [Test]
        public void Before_the_puck_gets_there_nothing_moves_after_the_reach()
        {
            foreach (AfterShotGesture g in new[] { AfterShotGesture.None, AfterShotGesture.Slump, AfterShotGesture.StickSlap, AfterShotGesture.Relief })
            {
                AfterShotMoment m = AfterShot.At(g, -0.05, -1);
                Assert.AreEqual(0, m.LateReach, g.ToString());
                Assert.AreEqual(0, m.Relax, g.ToString());
                Assert.AreEqual(0, m.Watch, g.ToString());
                Assert.AreEqual(0, m.Gesture, g.ToString());
            }
        }

        [Test]
        public void On_a_goal_the_hand_reaches_late_then_he_looks_back_into_the_net_then_gestures_and_holds_the_end_pose()
        {
            const AfterShotGesture g = AfterShotGesture.Slump;
            Assert.AreEqual(1, AfterShot.At(g, 0.3, -1).LateReach, 1e-9, "the hand gets there a quarter second after the puck");
            Assert.AreEqual(0, AfterShot.At(g, 0.3, -1).Watch, "no look back before the puck meets the netting");
            AfterShotMoment looking = AfterShot.At(g, 0.95, 0.65);
            Assert.AreEqual(1, looking.Watch, 1e-9, "looking back into the net after the netting");
            Assert.AreEqual(1, looking.Relax, 1e-9, "the arm has dropped");
            Assert.AreEqual(0, looking.Gesture);
            Assert.Greater(AfterShot.At(g, 1.6, 1.3).Gesture, 0.0, "the gesture under way");
            AfterShotMoment end = AfterShot.At(g, 2.2, 1.9);
            Assert.AreEqual(1, end.Gesture, 1e-9, "the end pose");
            Assert.AreEqual(0, end.Watch, 1e-9, "head forward again for the end pose");
            Assert.AreEqual(1, AfterShot.At(g, 30, 29.7).Gesture, 1e-9, "the end pose is held");
        }

        [Test]
        public void A_goal_s_gesture_ends_before_the_result_hold_does_even_when_the_netting_is_late()
        {
            // The result hold runs 2.5 s after the puck gets there (Round.DelayResult). A deep netting is met about 0.4 s in.
            Assert.AreEqual(2.5, new RoundSettings().ResultHoldSeconds);
            Assert.AreEqual(1, AfterShot.At(AfterShotGesture.StickSlap, 2.3, 1.9).Gesture, 1e-9);
            Assert.AreEqual(1, AfterShot.At(AfterShotGesture.StickSlap, 2.3, -1).Gesture, 1e-9, "netting never reported: started anyway");
        }

        [Test]
        public void On_a_miss_the_head_follows_the_puck_then_relief_glances_at_the_post_and_holds()
        {
            const AfterShotGesture g = AfterShotGesture.Relief;
            Assert.AreEqual(1, AfterShot.At(g, 0.2, -1).Watch, 1e-9, "following the puck going by");
            Assert.AreEqual(0, AfterShot.At(g, 0.2, -1).Glance);
            Assert.AreEqual(0, AfterShot.At(g, 0.2, -1).Relax, "a miss keeps the reach (the deflection is measured on it)");
            Assert.Greater(AfterShot.At(g, 1.4, -1).Gesture, 0.0, "the relief beat");
            AfterShotMoment end = AfterShot.At(g, 2.2, -1);
            Assert.AreEqual(1, end.Gesture, 1e-9);
            Assert.AreEqual(1, end.Glance, 1e-9, "looking at the post");
        }

        [Test]
        public void On_a_save_the_head_follows_the_rebound_for_the_rest_of_the_hold()
        {
            Assert.AreEqual(1, AfterShot.At(AfterShotGesture.None, 0.2, -1).Watch, 1e-9);
            Assert.AreEqual(1, AfterShot.At(AfterShotGesture.None, 2.4, -1).Watch, 1e-9);
            Assert.AreEqual(0, AfterShot.At(AfterShotGesture.None, 2.4, -1).Gesture);
            Assert.AreEqual(0, AfterShot.At(AfterShotGesture.None, 2.4, -1).Relax);
        }
    }
}
