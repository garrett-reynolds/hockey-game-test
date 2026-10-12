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
        static readonly double Limit = new RoundSettings().LateMarginLimitSeconds;

        [Test]
        public void The_Late_Margin_limit_is_a_setting_of_0_05_seconds()
        {
            Assert.AreEqual(0.05, Limit, 1e-12);
        }

        [TestCase(0.049, AfterShotGesture.StickSlap, TestName = "Goal by just under the limit: slaps the stick on the ice")]
        [TestCase(0.0, AfterShotGesture.StickSlap, TestName = "Goal by nothing: slaps the stick on the ice")]
        [TestCase(0.05, AfterShotGesture.Slump, TestName = "Goal by exactly the limit: slumps")]
        [TestCase(0.051, AfterShotGesture.Slump, TestName = "Goal by just over the limit: slumps")]
        [TestCase(0.4, AfterShotGesture.Slump, TestName = "Goal by a lot: slumps")]
        [TestCase(double.PositiveInfinity, AfterShotGesture.Slump, TestName = "Goal with no gear able to reach: slumps")]
        public void A_goal_s_annoyed_gesture_comes_from_its_Late_Margin(double lateMargin, AfterShotGesture expected)
        {
            Assert.AreEqual(expected, AfterShot.GestureFor(ShotResultKind.Goal, lateMargin, Limit));
        }

        [Test]
        public void The_limit_is_the_one_given_so_it_can_be_tuned()
        {
            Assert.AreEqual(AfterShotGesture.StickSlap, AfterShot.GestureFor(ShotResultKind.Goal, 0.08, 0.1));
            Assert.AreEqual(AfterShotGesture.Slump, AfterShot.GestureFor(ShotResultKind.Goal, 0.08, 0.06));
        }

        [TestCase(-8, 50, TestName = "Wide left")]
        [TestCase(103, 50, TestName = "Off the right post (a Frame Hit)")]
        [TestCase(50, 120, TestName = "Over the crossbar")]
        public void A_miss_is_followed_by_relief(double x, double y)
        {
            Assert.AreEqual(AfterShotGesture.Relief, AfterShot.GestureFor(ShotResultKind.Miss, 0.2, Limit));
        }

        [Test]
        public void A_save_has_no_gesture()
        {
            Assert.AreEqual(AfterShotGesture.None, AfterShot.GestureFor(ShotResultKind.Save, 0.2, Limit));
        }

        [TestCase(AfterShotGesture.Slump, true)]
        [TestCase(AfterShotGesture.StickSlap, true)]
        [TestCase(AfterShotGesture.Relief, false)]
        [TestCase(AfterShotGesture.None, false)]
        public void Only_the_annoyed_gestures_follow_a_goal(AfterShotGesture gesture, bool goal)
        {
            Assert.AreEqual(goal, AfterShot.IsGoal(gesture));
        }

        [TestCase(-1.0, 0.0)]
        [TestCase(1.0, 0.0)]
        [TestCase(1.5, 0.15625)]
        [TestCase(2.0, 0.5)]
        [TestCase(2.5, 0.84375)]
        [TestCase(3.0, 1.0)]
        [TestCase(9.0, 1.0)]
        public void A_ramp_eases_in_and_out_between_its_ends_and_holds_outside_them(double t, double expected)
        {
            Assert.AreEqual(expected, AfterShot.Ramp(t, 1.0, 3.0), 1e-12);
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
                Assert.AreEqual(0, m.FollowThrough, g.ToString());
                Assert.AreEqual(0, m.Return, g.ToString());
                Assert.AreEqual(0, m.Recover, g.ToString());
            }
        }

        [Test]
        public void On_a_goal_the_hand_reaches_late_then_he_looks_back_into_the_net_then_gestures_then_recovers()
        {
            const AfterShotGesture g = AfterShotGesture.Slump;
            Assert.AreEqual(1, AfterShot.At(g, 0.3, -1).LateReach, 1e-9, "the hand gets there a quarter second after the puck");
            Assert.AreEqual(0, AfterShot.At(g, 0.3, -1).Watch, "no look back before the puck meets the netting");
            AfterShotMoment looking = AfterShot.At(g, 0.75, 0.45);
            Assert.AreEqual(1, looking.Watch, 1e-9, "looking back into the net after the netting");
            Assert.AreEqual(0, looking.Gesture);
            Assert.AreEqual(1, AfterShot.At(g, 0.9, 0.6).Relax, 1e-9, "the arm has dropped");
            Assert.Greater(AfterShot.At(g, 1.3, 1.0).Gesture, 0.0, "the gesture under way");
            Assert.AreEqual(1, AfterShot.At(g, 1.3, 1.0).Return, 1e-9, "back on his spot before the gesture, not gliding through it");
            AfterShotMoment end = AfterShot.At(g, 1.7, 1.4);
            Assert.AreEqual(1, end.Gesture, 1e-9, "the end pose");
            Assert.AreEqual(0, end.Watch, 1e-9, "head forward again for the end pose");
            Assert.AreEqual(0, end.Recover, 1e-9, "the end pose shows before he recovers");
            Assert.Greater(AfterShot.At(g, 2.05, 1.75).Recover, 0.2, "recovering");
            Assert.AreEqual(1, AfterShot.At(g, 2.3, 2.0).Recover, 1e-9, "back in the Ready Stance");
            Assert.AreEqual(1, AfterShot.At(g, 2.3, 2.0).Return, 1e-9, "back on his spot");
            Assert.AreEqual(1, AfterShot.At(g, 30, 29.7).Recover, 1e-9, "stays recovered");
        }

        [TestCase(AfterShotGesture.Slump, 0.3)]
        [TestCase(AfterShotGesture.StickSlap, 0.3)]
        [TestCase(AfterShotGesture.Slump, -1.0)]
        [TestCase(AfterShotGesture.StickSlap, -1.0)]
        public void A_goal_s_annoyed_gesture_holds_its_end_pose_a_moment_before_he_recovers(AfterShotGesture g, double netting)
        {
            // The glide back is over before the gesture starts, so the gesture reads on its own; then its end pose holds still
            // for a quarter second at least before the Recovery.
            double nettedAt = netting < 0 ? AfterShot.LatestNetting : netting;
            AfterShotMoment glided = AfterShot.At(g, nettedAt + 0.6, netting < 0 ? -1 : 0.6);
            Assert.AreEqual(0, glided.Gesture, 1e-9, "the gesture has not started");
            Assert.AreEqual(1, glided.Return, 1e-9, "back on his spot already");
            AfterShotMoment first = AfterShot.At(g, nettedAt + 1.25, netting < 0 ? -1 : 1.25);
            AfterShotMoment last = AfterShot.At(g, nettedAt + 1.5, netting < 0 ? -1 : 1.5);
            foreach (AfterShotMoment m in new[] { first, last })
            {
                Assert.AreEqual(1, m.Gesture, 1e-9, "in the end pose");
                Assert.AreEqual(0, m.Recover, 1e-9, "not recovering yet");
                Assert.AreEqual(1, m.Return, 1e-9, "on his spot");
                Assert.AreEqual(1, m.LateReach, 1e-9);
                Assert.AreEqual(1, m.Relax, 1e-9);
                Assert.AreEqual(0, m.Watch, 1e-9, "head forward");
            }
            Assert.AreEqual(1, AfterShot.At(g, 2.4, netting < 0 ? -1 : 2.4 - nettedAt).Recover, 1e-9, "still back in the Ready Stance by 2.4 s");
        }

        [TestCase(AfterShotGesture.Slump, 0.5)]
        [TestCase(AfterShotGesture.StickSlap, 0.5)]
        [TestCase(AfterShotGesture.StickSlap, -1.0)]
        [TestCase(AfterShotGesture.Relief, -1.0)]
        [TestCase(AfterShotGesture.None, -1.0)]
        public void After_every_result_the_goalie_is_back_in_the_Ready_Stance_before_the_result_hold_ends(AfterShotGesture g, double netting)
        {
            // The result hold runs 2.5 s after the puck gets there (Round.Whistle); the netting is met at the latest 0.5 s in.
            Assert.AreEqual(2.5, new RoundSettings().ResultHoldSeconds);
            AfterShotMoment m = AfterShot.At(g, 2.4, netting < 0 ? -1 : 2.4 - netting);
            Assert.AreEqual(1, m.Recover, 1e-9, "recovered by 2.4 s");
            Assert.AreEqual(1, m.Return, 1e-9, "back on his spot by 2.4 s");
            Assert.AreEqual(0, AfterShot.At(g, 1.0, netting < 0 ? -1 : 1.0 - netting).Recover, 1e-9, "not before the reaction has played");
        }

        [Test]
        public void A_goal_s_gesture_ends_before_he_recovers_even_when_the_netting_is_late()
        {
            Assert.AreEqual(1, AfterShot.At(AfterShotGesture.StickSlap, 1.9, 1.4).Gesture, 1e-9);
            Assert.AreEqual(0, AfterShot.At(AfterShotGesture.StickSlap, 1.75, -1).Recover, 1e-9);
            Assert.AreEqual(1, AfterShot.At(AfterShotGesture.StickSlap, 1.9, -1).Gesture, 1e-9, "netting never reported: started anyway");
        }

        [Test]
        public void On_a_miss_the_head_follows_the_puck_then_relief_glances_at_the_post_then_he_recovers()
        {
            const AfterShotGesture g = AfterShotGesture.Relief;
            Assert.AreEqual(1, AfterShot.At(g, 0.2, -1).Watch, 1e-9, "following the puck going by");
            Assert.AreEqual(0, AfterShot.At(g, 0.2, -1).Glance);
            Assert.AreEqual(0, AfterShot.At(g, 0.2, -1).Relax, "a miss keeps the reach (the deflection is measured on it)");
            Assert.AreEqual(0, AfterShot.At(g, 0.5, -1).Return, "still where he reached while the puck goes by");
            Assert.Greater(AfterShot.At(g, 1.3, -1).Gesture, 0.0, "the relief beat");
            AfterShotMoment end = AfterShot.At(g, 1.7, -1);
            Assert.AreEqual(1, end.Gesture, 1e-9);
            Assert.AreEqual(1, end.Glance, 1e-9, "looking at the post");
            Assert.AreEqual(0, end.Recover, 1e-9);
        }

        [Test]
        public void On_a_save_the_goalie_follows_through_watches_the_rebound_then_recovers()
        {
            Assert.AreEqual(0, AfterShot.At(AfterShotGesture.None, 0.1, -1).FollowThrough, 1e-9, "the gear holds while the puck comes off it");
            Assert.AreEqual(1, AfterShot.At(AfterShotGesture.None, 0.7, -1).FollowThrough, 1e-9, "glove held up, blocker followed through");
            Assert.AreEqual(1, AfterShot.At(AfterShotGesture.None, 0.2, -1).Watch, 1e-9);
            Assert.AreEqual(1, AfterShot.At(AfterShotGesture.None, 1.4, -1).Watch, 1e-9, "watching the rebound");
            Assert.AreEqual(0, AfterShot.At(AfterShotGesture.None, 1.4, -1).Recover, 1e-9);
            Assert.AreEqual(0, AfterShot.At(AfterShotGesture.None, 2.4, -1).Gesture);
            Assert.AreEqual(0, AfterShot.At(AfterShotGesture.None, 2.4, -1).Relax);
            Assert.AreEqual(0, AfterShot.At(AfterShotGesture.Slump, 0.6, 0.3).FollowThrough, "only a save follows through");
        }
    }
}
