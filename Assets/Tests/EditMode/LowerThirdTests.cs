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
        public void Test_Shots_stay_out_of_the_Rounds_average_speed()
        {
            var round = InWindow();
            round.SubmitShotAndWhistle(50, 50, 150, true); // a Test Shot (dev), very fast
            Assert.AreEqual(1, round.Results.Count, "a Test Shot is still a Shot in the Results");
            Assert.IsNull(round.ShotSpeedsKmh[0], "but it has no speed in the Round's list");
            Assert.AreEqual("", LowerThird.Choose(round.Results, round.ShotSpeedsKmh).Value.SpeedLine, "no speed to average yet");
            round.Tick(round.ResultHoldLength);
            round.Tick(3.7);
            round.SubmitShotAndWhistle(50, 50, 80, false);
            LowerThirdText text = LowerThird.Choose(round.Results, round.ShotSpeedsKmh).Value;
            Assert.AreEqual("80 km/h  |  average 80 km/h", text.SpeedLine, "the average is the real Shot's, not 115");
        }

        [Test]
        public void No_lower_third_the_Round_gives_mentions_the_best_score_even_with_a_best_score_set()
        {
            // The Round holds a best score of 5; the lower third reads the Round, so a line that used it would show here.
            var round = new Round(ScriptedRandom.Halves(), new RoundSettings(), FakeReach.NeverReaches(), 5);
            round.StartRound();
            round.Tick(3.7);
            round.SubmitShotAndWhistle(50, 50, 100, false);
            Assert.AreEqual(5, round.BestScore);
            LowerThirdShow show = default;
            for (int i = 0; i < 100 && show.Amount <= 0; i++)
            {
                round.Tick(0.1);
                show = LowerThird.Of(round, new LowerThirdWords());
            }
            Assert.Greater(show.Amount, 0, "the lower third is on screen, so its words are real");
            foreach (string line in new[] { show.Text.Kicker, show.Text.Headline, show.Text.SpeedLine })
            {
                StringAssert.DoesNotContain("best", line.ToLowerInvariant());
                StringAssert.DoesNotContain("beat", line.ToLowerInvariant());
                StringAssert.DoesNotContain("chasing", line.ToLowerInvariant());
                StringAssert.DoesNotContain("5", line, "the best score's number");
            }
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

        // Garrett, 2026-10-13: "There is a bottom thirds graphic that whips on and off screen. It should always be on the screen in the
        // bottom left corner." In a game it never slides: every frame of a Round has it fully in place; only its words change.
        [Test]
        public void It_is_on_screen_every_frame_of_a_Round_from_the_first_skate_in_to_the_last_hold()
        {
            var round = new Round(ScriptedRandom.Halves(), new RoundSettings(), FakeReach.NeverReaches(), 0);
            round.StartRound(0, 3);
            var phases = new HashSet<RoundPhase>();
            int frames = 0;
            while (round.Phase != RoundPhase.RoundOver && frames++ < 20000)
            {
                if (round.Phase == RoundPhase.ShootingWindow && round.PhaseSecondsElapsed > 0.5) round.SubmitShot(50, 50, 100, false);
                if (round.AwaitingWhistle && round.Phase == RoundPhase.ResultHold && round.PhaseSecondsElapsed > 1.0) round.WhistleNow();
                phases.Add(round.Phase);
                LowerThirdShow show = LowerThird.Of(round, new LowerThirdWords());
                Assert.AreEqual(1, show.Amount, 1e-9, "fully in place, never sliding, in " + round.Phase + " at " + round.PhaseSecondsElapsed);
                Assert.IsNotEmpty(show.Text.Headline, "words in " + round.Phase);
                round.Tick(0.02);
            }
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            foreach (RoundPhase p in new[] { RoundPhase.SkateIn, RoundPhase.SlowDown, RoundPhase.ShootingWindow, RoundPhase.ResultHold })
                Assert.IsTrue(phases.Contains(p), "the run covered " + p);
        }

        [Test]
        public void It_is_on_while_the_player_aims_and_while_the_puck_flies_with_the_tally_so_far()
        {
            var round = new Round(ScriptedRandom.Halves(), new RoundSettings(), FakeReach.NeverReaches(), 0);
            round.StartRound();
            Assert.AreEqual(1, LowerThird.Of(round).Amount, "skating in");
            Assert.AreEqual(LowerThird.Fresh.Headline, LowerThird.Of(round).Text.Headline, "no Shot yet: an empty tally");
            round.Tick(3.7);
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
            Assert.AreEqual(1, LowerThird.Of(round).Amount, "aiming");
            round.SubmitShot(50, 50, 100, false);
            round.Tick(1.0);
            Assert.IsTrue(round.AwaitingWhistle);
            Assert.AreEqual(1, LowerThird.Of(round).Amount, "the puck flies");
            Assert.AreEqual("0 Goals from 0 Shots", LowerThird.Of(round).Text.Headline, "the result is not known yet");
        }

        [Test]
        public void It_is_off_on_the_start_screen_and_the_end_screen()
        {
            var round = new Round(ScriptedRandom.Halves(), new RoundSettings(), FakeReach.NeverReaches(), 0);
            Assert.AreEqual(RoundPhase.Idle, round.Phase);
            Assert.AreEqual(0, LowerThird.Of(round).Amount, "the start screen");
            round.StartRound(0, 3);
            for (int i = 0; i < 20000 && round.Phase != RoundPhase.RoundOver; i++)
            {
                if (round.Phase == RoundPhase.ShootingWindow) round.SubmitShotAndWhistle(50, 50, 100, false);
                round.Tick(0.1);
            }
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            Assert.AreEqual(0, LowerThird.Of(round).Amount, "the end screen");
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
        public void Its_words_change_at_the_Whistle_and_it_does_not_move()
        {
            var round = InWindow();
            round.SubmitShot(50, 50, 100, false);
            round.Tick(2.0); // a long flight
            Assert.AreEqual("0 Goals from 0 Shots", LowerThird.Of(round).Text.Headline, "before the Whistle");
            round.WhistleNow();
            Assert.AreEqual(1, LowerThird.Of(round).Amount, 1e-9, "just whistled: in place already");
            Assert.AreEqual("1 Goal from 1 Shot", LowerThird.Of(round).Text.Headline, "the words change");
            round.Tick(0.8);
            Assert.AreEqual(1, LowerThird.Of(round).Amount, 1e-9);
        }

        [Test]
        public void It_stays_through_the_end_of_the_hold_and_the_next_Shot()
        {
            var round = InWindow();
            round.SubmitShotAndWhistle(50, 50, 100, false);
            round.Tick(2.0);
            Assert.AreEqual(1, LowerThird.Of(round).Amount, 1e-9, "over the Replay's look-up and the Replay");
            round.Tick(round.ResultHoldLength);
            Assert.AreEqual(RoundPhase.SkateIn, round.Phase);
            Assert.AreEqual(1, LowerThird.Of(round).Amount, 1e-9, "the next turn");
            Assert.AreEqual("1 Goal from 1 Shot", LowerThird.Of(round).Text.Headline, "still the tally so far");
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
