using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class WhistleRuleTests
    {
        private const double Step = 1.0 / 60.0;
        private const double Fast = 20.0;  // m/s
        private const double Still = 0.0;

        private static WhistleRule NewRule() => new WhistleRule(new RoundSettings());

        private static PuckFacts Facts(double seconds, double speed = Fast, bool inNet = false, bool gear = false, bool gone = false) =>
            new PuckFacts(seconds, inNet, gear, speed, gone);

        // Feeds a puck doing the same thing each step, until the Whistle or maxSeconds. Returns the time of the Whistle, -1 if none.
        private static double Run(WhistleRule rule, double maxSeconds, double speed, bool gear = false, bool gone = false, double from = 0)
        {
            for (double t = from; t <= maxSeconds; t += Step)
                if (rule.Step(Facts(t, speed, false, gear, gone)))
                    return t;
            return -1;
        }

        [Test]
        public void No_Whistle_while_the_puck_is_flying()
        {
            var rule = NewRule();
            Assert.IsFalse(rule.Step(Facts(0.1)));
            Assert.IsFalse(rule.HasWhistled);
            Assert.IsFalse(rule.Result.HasValue);
        }

        [Test]
        public void A_Goal_at_the_first_step_the_whole_puck_is_in_the_net()
        {
            var rule = NewRule();
            Assert.IsFalse(rule.Step(Facts(0.30)));
            Assert.IsTrue(rule.Step(Facts(0.32, inNet: true)));
            Assert.AreEqual(ShotResultKind.Goal, rule.Result);
        }

        [Test]
        public void A_Goal_wins_over_a_gear_touch()
        {
            var rule = NewRule();
            rule.Step(Facts(0.3, gear: true, inNet: true));
            Assert.AreEqual(ShotResultKind.Goal, rule.Result);
        }

        [Test]
        public void A_Goal_that_slides_back_out_stays_a_Goal()
        {
            var rule = NewRule();
            rule.Step(Facts(0.32, inNet: true));
            Assert.IsTrue(rule.Step(Facts(0.40, speed: Still)));
            Assert.IsTrue(rule.Step(Facts(1.0, gone: true)));
            Assert.IsTrue(rule.Step(Facts(5.0)));
            Assert.AreEqual(ShotResultKind.Goal, rule.Result);
        }

        [Test]
        public void A_Save_when_the_gear_was_touched_and_the_puck_goes_dead()
        {
            var rule = NewRule();
            rule.Step(Facts(0.3, gear: true));
            double whistle = Run(rule, 3.0, Still, gear: true, from: 0.32);
            Assert.GreaterOrEqual(whistle, 0);
            Assert.AreEqual(ShotResultKind.Save, rule.Result);
        }

        [Test]
        public void Post_then_out_is_a_Miss()
        {
            // Never touched the gear, never crossed: it bounces off the post and leaves the view.
            var rule = NewRule();
            rule.Step(Facts(0.3));
            Assert.IsTrue(rule.Step(Facts(0.5, gone: true)));
            Assert.AreEqual(ShotResultKind.Miss, rule.Result);
        }

        [Test]
        public void Post_then_gear_is_a_Save()
        {
            var rule = NewRule();
            rule.Step(Facts(0.3));                     // post
            rule.Step(Facts(0.4, gear: true));         // then the goalie
            Assert.IsTrue(rule.Step(Facts(0.5, gear: true, gone: true)));
            Assert.AreEqual(ShotResultKind.Save, rule.Result);
        }

        [Test]
        public void The_gear_touch_counts_even_if_the_puck_has_left_the_gear_since()
        {
            var rule = NewRule();
            rule.Step(Facts(0.3, gear: true));
            rule.Step(Facts(0.4, gear: true));
            Assert.IsTrue(rule.Step(Facts(0.6, gear: true, gone: true)));
            Assert.AreEqual(ShotResultKind.Save, rule.Result);
        }

        [Test]
        public void Dead_only_after_about_0_3_seconds_very_slow_in_a_row()
        {
            var rule = NewRule();
            Assert.IsFalse(rule.Step(Facts(1.00, speed: 0.1)));
            Assert.IsFalse(rule.Step(Facts(1.29, speed: 0.1)));
            Assert.IsTrue(rule.Step(Facts(1.30, speed: 0.1)));
            Assert.AreEqual(ShotResultKind.Miss, rule.Result);
        }

        [Test]
        public void Dead_with_the_gear_touched_is_a_Save()
        {
            var rule = NewRule();
            rule.Step(Facts(0.5, gear: true));
            rule.Step(Facts(1.00, speed: 0.1, gear: true));
            Assert.IsTrue(rule.Step(Facts(1.30, speed: 0.1, gear: true)));
            Assert.AreEqual(ShotResultKind.Save, rule.Result);
        }

        [Test]
        public void A_speed_up_starts_the_slow_time_again()
        {
            var rule = NewRule();
            rule.Step(Facts(1.00, speed: 0.1));
            rule.Step(Facts(1.20, speed: 0.1));
            rule.Step(Facts(1.25, speed: 2.0));        // rolls on: not dead
            rule.Step(Facts(1.30, speed: 0.1));        // slow time starts again here
            Assert.IsFalse(rule.Step(Facts(1.55, speed: 0.1)));
            Assert.IsTrue(rule.Step(Facts(1.60, speed: 0.1)));
        }

        [Test]
        public void A_slow_puck_is_not_dead_at_the_instant_it_is_slow()
        {
            var rule = NewRule();
            Assert.IsFalse(rule.Step(Facts(1.0, speed: 0.1)));
            Assert.IsFalse(rule.HasWhistled);
        }

        [Test]
        public void Gone_behind_the_line_or_out_of_view_whistles_at_once()
        {
            var rule = NewRule();
            Assert.IsTrue(rule.Step(Facts(0.6, gone: true)));
            Assert.AreEqual(ShotResultKind.Miss, rule.Result);
        }

        [Test]
        public void The_cap_is_3_seconds_after_the_Shot()
        {
            var rule = NewRule();
            Assert.IsFalse(rule.Step(Facts(2.99)));
            Assert.IsTrue(rule.Step(Facts(3.00)));
            Assert.AreEqual(ShotResultKind.Miss, rule.Result);
        }

        [Test]
        public void The_cap_gives_a_Save_if_the_gear_was_touched()
        {
            var rule = NewRule();
            rule.Step(Facts(0.4, gear: true));
            Assert.IsTrue(rule.Step(Facts(3.00, gear: true)));
            Assert.AreEqual(ShotResultKind.Save, rule.Result);
        }

        [Test]
        public void Nothing_after_the_Whistle_changes_the_result()
        {
            var rule = NewRule();
            rule.Step(Facts(0.5, gone: true));         // Miss
            Assert.IsTrue(rule.Step(Facts(0.6, inNet: true)));
            Assert.IsTrue(rule.Step(Facts(0.7, gear: true)));
            Assert.AreEqual(ShotResultKind.Miss, rule.Result);

            var saved = NewRule();
            saved.Step(Facts(0.4, gear: true, gone: true)); // Save
            saved.Step(Facts(0.5, inNet: true));       // a late rebound across the line
            Assert.AreEqual(ShotResultKind.Save, saved.Result);
        }

        [Test]
        public void The_values_are_the_settings_ones()
        {
            var settings = new RoundSettings { WhistleCapSeconds = 1.0, WhistleDeadSeconds = 0.1, WhistleVerySlowMetresPerSecond = 2.0 };
            var rule = new WhistleRule(settings);
            Assert.IsFalse(rule.Step(Facts(0.2, speed: 1.5)));
            Assert.IsTrue(rule.Step(Facts(0.35, speed: 1.5)));

            var capped = new WhistleRule(settings);
            Assert.IsTrue(capped.Step(Facts(1.0)));
        }

        [Test]
        public void The_starting_values_are_0_3_seconds_dead_3_seconds_cap_and_0_3_metres_per_second_very_slow()
        {
            var settings = new RoundSettings();
            Assert.AreEqual(0.3, settings.WhistleDeadSeconds);
            Assert.AreEqual(3.0, settings.WhistleCapSeconds);
            Assert.AreEqual(0.3, settings.WhistleVerySlowMetresPerSecond);
        }
    }
}
