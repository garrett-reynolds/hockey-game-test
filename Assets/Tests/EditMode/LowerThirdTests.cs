using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class LowerThirdTests
    {
        const ShotResultKind G = ShotResultKind.Goal, S = ShotResultKind.Save, M = ShotResultKind.Miss, T = ShotResultKind.TooLate;

        static List<double?> Speeds(int n, double kmh = 100)
        {
            var speeds = new List<double?>();
            for (int i = 0; i < n; i++) speeds.Add(kmh);
            return speeds;
        }

        static LowerThirdText Text(IReadOnlyList<double?> speeds, params ShotResultKind[] results) =>
            LowerThird.Choose(new List<ShotResultKind>(results), speeds).Value;

        static string Headline(params ShotResultKind[] results) => Text(Speeds(results.Length), results).Headline;

        // ---- number choice ----

        [Test]
        public void No_Shots_yet_has_nothing_to_show()
        {
            Assert.IsNull(LowerThird.Choose(new List<ShotResultKind>(), new List<double?>()));
        }

        [Test]
        public void A_streak_of_3_Goals_in_a_row_wins()
        {
            Assert.AreEqual("3 in a row!", Headline(G, G, G));
            Assert.AreEqual("4 in a row!", Headline(S, G, G, G, G));
        }

        [Test]
        public void A_streak_must_be_running_now_and_2_in_a_row_is_not_one()
        {
            Assert.AreEqual("2 Goals from 2 Shots", Headline(G, G));
            Assert.AreEqual("3 Goals from 4 Shots", Headline(G, G, S, G), "the streak is 1 now");
            Assert.AreEqual("3 Goals from 4 Shots", Headline(G, G, G, S), "a streak that ended is not shown");
        }

        [Test]
        public void Without_a_streak_it_is_the_tally()
        {
            Assert.AreEqual("0 Goals from 1 Shot", Headline(S));
            Assert.AreEqual("1 Goal from 1 Shot", Headline(G));
            Assert.AreEqual("2 Goals from 3 Shots", Headline(G, M, G));
        }

        [Test]
        public void A_Too_Late_Shot_counts_as_a_Shot_in_the_tally()
        {
            Assert.AreEqual("0 Goals from 1 Shot", Text(new List<double?> { null }, T).Headline);
        }

        [Test]
        public void No_line_mentions_the_best_score()
        {
            // The chase for the best score is gone (#151): people play once. Every kind of line, every Round length, says nothing of it.
            var kinds = new[] { G, S, M, T };
            var seen = new List<string>();
            for (int n = 1; n <= Round.ShotsPerRound; n++)
                for (int pattern = 0; pattern < 40; pattern++)
                {
                    var results = new ShotResultKind[n];
                    for (int i = 0; i < n; i++) results[i] = kinds[(pattern * (i + 3) + i * i) % 4];
                    LowerThirdText text = Text(Speeds(n), results);
                    seen.Add(text.Kicker);
                    seen.Add(text.Headline);
                    seen.Add(text.SpeedLine);
                }
            Assert.Greater(seen.Count, 100);
            foreach (string line in seen)
            {
                StringAssert.DoesNotContain("best", line.ToLowerInvariant());
                StringAssert.DoesNotContain("beat", line.ToLowerInvariant());
                StringAssert.DoesNotContain("chasing", line.ToLowerInvariant());
            }
        }

        // ---- the speed line ----

        [Test]
        public void The_first_Shots_average_is_its_own_speed()
        {
            Assert.AreEqual("92 km/h  |  average 92 km/h", Text(new List<double?> { 92 }, G).SpeedLine);
        }

        [Test]
        public void The_speed_line_is_the_last_Shots_speed_and_the_average_of_the_Shots_so_far()
        {
            Assert.AreEqual("100 km/h  |  average 90 km/h", Text(new List<double?> { 80, 100 }, G, S).SpeedLine);
            Assert.AreEqual("60 km/h  |  average 80 km/h", Text(new List<double?> { 80, 100, 60 }, G, S, G).SpeedLine);
        }

        [Test]
        public void Speeds_are_rounded_like_the_result_words_speed()
        {
            // 92.5 rounds away from zero to 93 (ShotResultText.Speed); the average 85.25 shows as 85.
            Assert.AreEqual("93 km/h", ShotResultText.Speed(92.5));
            Assert.AreEqual("93 km/h  |  average 85 km/h", Text(new List<double?> { 78, 92.5 }, G, G).SpeedLine);
        }

        [Test]
        public void A_Shot_with_no_speed_is_left_out_of_the_average_and_shows_no_speed()
        {
            // Too Late has no puck: no speed of its own, and it does not pull the average down.
            Assert.AreEqual("average 90 km/h", Text(new List<double?> { 80, 100, null }, G, S, T).SpeedLine);
            Assert.AreEqual("100 km/h  |  average 90 km/h", Text(new List<double?> { 80, null, 100 }, G, T, S).SpeedLine);
        }

        [Test]
        public void With_no_speed_at_all_the_speed_line_is_empty()
        {
            Assert.AreEqual("", Text(new List<double?> { null }, T).SpeedLine);
        }

        // ---- the Round keeps every Shot's speed ----

        static Round InWindow()
        {
            var round = new Round(ScriptedRandom.Halves(), new RoundSettings(), FakeReach.NeverReaches(), 0);
            round.StartRound();
            round.Tick(3.7);
            return round;
        }

        [Test]
        public void The_Round_keeps_each_Shots_speed_in_order()
        {
            var round = InWindow();
            round.SubmitShotAndWhistle(50, 50, 100, false);
            round.Tick(round.ResultHoldLength);
            round.Tick(3.7);
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
            round.SubmitShotAndWhistle(50, 50, 80, false);
            CollectionAssert.AreEqual(new List<double?> { 100, 80 }, new List<double?>(round.ShotSpeedsKmh));
            Assert.AreEqual(round.Results.Count, round.ShotSpeedsKmh.Count);
        }

        [Test]
        public void A_Too_Late_Shot_has_no_speed_in_the_Rounds_list()
        {
            var round = InWindow();
            round.Tick(3.0); // the window runs out
            Assert.AreEqual(1, round.ShotSpeedsKmh.Count);
            Assert.IsNull(round.ShotSpeedsKmh[0]);
        }

        [Test]
        public void A_new_Round_starts_with_no_speeds()
        {
            var round = InWindow();
            round.SubmitShotAndWhistle(50, 50, 100, false);
            round.StartRound();
            Assert.AreEqual(0, round.ShotSpeedsKmh.Count);
        }

        // ---- the words are built once per Shot ----

        [Test]
        public void The_words_are_built_once_per_Shot_and_the_same_strings_come_back()
        {
            var words = new LowerThirdWords();
            var results = new List<ShotResultKind> { G };
            var speeds = new List<double?> { 90 };
            LowerThirdText a = words.Get(results, speeds).Value;
            LowerThirdText b = words.Get(results, speeds).Value;
            Assert.AreEqual(1, words.Builds);
            Assert.AreSame(a.Headline, b.Headline);
            Assert.AreSame(a.Kicker, b.Kicker);
            Assert.AreSame(a.SpeedLine, b.SpeedLine);
            results.Add(S);
            speeds.Add(70);
            LowerThirdText c = words.Get(results, speeds).Value;
            Assert.AreEqual(2, words.Builds, "a new Shot builds again");
            Assert.AreEqual("1 Goal from 2 Shots", c.Headline);
            Assert.AreEqual("70 km/h  |  average 80 km/h", c.SpeedLine);
        }

        [Test]
        public void A_new_Round_with_as_many_results_still_builds_new_words()
        {
            var words = new LowerThirdWords();
            Assert.AreEqual("1 Goal from 1 Shot", words.Get(new List<ShotResultKind> { G }, new List<double?> { 90 }).Value.Headline);
            Assert.AreEqual("0 Goals from 1 Shot", words.Get(new List<ShotResultKind> { S }, new List<double?> { 90 }).Value.Headline);
            Assert.AreEqual("0 Goals from 1 Shot", words.Get(new List<ShotResultKind> { S }, new List<double?> { 91 }).Value.Headline);
            Assert.AreEqual("91 km/h  |  average 91 km/h", words.Get(new List<ShotResultKind> { S }, new List<double?> { 91 }).Value.SpeedLine);
            Assert.AreEqual(3, words.Builds);
        }

        [Test]
        public void Asking_for_the_same_words_every_frame_makes_no_garbage()
        {
            var words = new LowerThirdWords();
            var results = new List<ShotResultKind> { G, S, G };
            var speeds = new List<double?> { 90, 80, null };
            words.Get(results, speeds);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) words.Get(results, speeds);
            long garbage = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, garbage, "bytes allocated in 1000 frames");
        }

        [Test]
        public void The_bar_draws_from_the_cache_while_the_Round_runs()
        {
            var round = InWindow();
            round.SubmitShotAndWhistle(50, 50, 100, false);
            round.Tick(0.8);
            var words = new LowerThirdWords();
            LowerThirdShow a = LowerThird.Of(round, words);
            LowerThirdShow b = LowerThird.Of(round, words);
            Assert.AreEqual(1, words.Builds);
            Assert.AreSame(a.Text.Headline, b.Text.Headline);
            Assert.AreEqual("1 Goal from 1 Shot", a.Text.Headline);
            Assert.AreEqual("100 km/h  |  average 100 km/h", a.Text.SpeedLine);
        }

        // ---- when it is on screen ----

        [Test]
        public void It_slides_in_after_the_Whistle_holds_then_slides_out_before_the_Replay_looks_up()
        {
            Assert.AreEqual(0, LowerThird.SlideAmount(0), 1e-9);
            Assert.AreEqual(0, LowerThird.SlideAmount(LowerThird.InDelaySeconds), 1e-9);
            Assert.AreEqual(1, LowerThird.SlideAmount(LowerThird.InDelaySeconds + LowerThird.SlideSeconds), 1e-9);
            Assert.AreEqual(1, LowerThird.SlideAmount(1.0), 1e-9);
            Assert.AreEqual(0, LowerThird.SlideAmount(ReplayTiming.OnNetSeconds), 1e-9, "out when the camera looks up: the Arena Show's time");
            Assert.AreEqual(0, LowerThird.SlideAmount(ReplayTiming.OnNetSeconds + ReplayTiming.LookUpSeconds), 1e-9);
            Assert.AreEqual(0, LowerThird.SlideAmount(ReplayTiming.OnNetSeconds + ReplayTiming.LookUpSeconds + 1.0), 1e-9, "the Replay itself");
            Assert.AreEqual(0, LowerThird.SlideAmount(-1), 1e-9);
        }

        [Test]
        public void It_is_off_the_screen_before_the_camera_looks_up_at_the_Video_Board()
        {
            for (double t = ReplayTiming.OnNetSeconds; t < ReplayTiming.HoldSeconds; t += 0.05)
                Assert.AreEqual(0, LowerThird.SlideAmount(t), 1e-9, "at " + t);
        }

        [Test]
        public void It_is_off_while_the_player_aims_and_while_the_puck_flies()
        {
            var round = new Round(ScriptedRandom.Halves(), new RoundSettings(), FakeReach.NeverReaches(), 0);
            round.StartRound();
            Assert.AreEqual(0, LowerThird.Of(round).Amount, "skating in");
            round.Tick(3.7);
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
            Assert.AreEqual(0, LowerThird.Of(round).Amount, "aiming");
            round.SubmitShot(50, 50, 100, false);
            round.Tick(1.0);
            Assert.IsTrue(round.AwaitingWhistle);
            Assert.AreEqual(0, LowerThird.Of(round).Amount, "the puck flies");
        }

        [Test]
        public void After_the_Whistle_of_a_Goal_it_shows_the_Round_numbers()
        {
            var round = InWindow();
            round.SubmitShotAndWhistle(50, 50, 100, false);
            round.Tick(0.8);
            LowerThirdShow show = LowerThird.Of(round);
            Assert.AreEqual(1, show.Amount, 1e-9);
            Assert.AreEqual("1 Goal from 1 Shot", show.Text.Headline);
        }

        [Test]
        public void It_counts_from_the_Whistle_not_from_the_Shot()
        {
            var round = InWindow();
            round.SubmitShot(50, 50, 100, false);
            round.Tick(2.0); // a long flight
            round.WhistleNow();
            Assert.AreEqual(0, LowerThird.Of(round).Amount, 1e-9, "just whistled");
            round.Tick(0.8);
            Assert.AreEqual(1, LowerThird.Of(round).Amount, 1e-9);
        }

        [Test]
        public void It_is_gone_by_the_end_of_the_hold_and_in_the_next_Shot()
        {
            var round = InWindow();
            round.SubmitShotAndWhistle(50, 50, 100, false);
            round.Tick(2.0);
            Assert.AreEqual(0, LowerThird.Of(round).Amount, 1e-9);
            round.Tick(round.ResultHoldLength);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
            Assert.AreEqual(0, LowerThird.Of(round).Amount, 1e-9);
        }

        [Test]
        public void It_still_shows_when_no_Replay_plays()
        {
            // The Replay on/off setting is not built (#145). A Wide Miss has no Replay today: the same case the setting will make.
            var round = InWindow();
            round.SubmitShotAndWhistle(-20, 50, 100, false);
            Assert.IsFalse(ReplayTiming.Follows(round.LastResult), "a Wide Miss has no Replay");
            Assert.AreEqual(ShotResultKind.Miss, round.LastResult.Value.Kind);
            Assert.AreEqual(2.5, round.ResultHoldLength, 1e-9, "the hold without a Replay");
            round.Tick(0.8);
            LowerThirdShow show = LowerThird.Of(round);
            Assert.AreEqual(1, show.Amount, 1e-9);
            Assert.AreEqual("0 Goals from 1 Shot", show.Text.Headline);
        }

        [Test]
        public void It_shows_the_same_with_and_without_a_Replay()
        {
            double[] ts = { 0.1, 0.3, 0.45, 0.8, 1.2, 1.4, 1.6, 2.2 };
            foreach (double t in ts)
            {
                var goal = InWindow();
                goal.SubmitShotAndWhistle(50, 50, 100, false);
                goal.Tick(t);
                var wide = InWindow();
                wide.SubmitShotAndWhistle(-20, 50, 100, false);
                wide.Tick(t);
                Assert.AreEqual(LowerThird.Of(goal).Amount, LowerThird.Of(wide).Amount, 1e-9, "at " + t);
            }
        }

        [Test]
        public void It_shows_after_a_Too_Late_Shot()
        {
            var round = InWindow();
            round.Tick(3.0); // the window runs out
            Assert.AreEqual(RoundPhase.ResultHold, round.Phase);
            round.Tick(0.8);
            LowerThirdShow show = LowerThird.Of(round);
            Assert.AreEqual(1, show.Amount, 1e-9);
            Assert.AreEqual("0 Goals from 1 Shot", show.Text.Headline);
            Assert.AreEqual("", show.Text.SpeedLine, "no Shot had a speed");
        }
    }
}
