using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The always-on lower third (#141, #151, look A): its words from the Round's real data, and when it is on screen.</summary>
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
            LowerThird.Choose(new List<ShotResultKind>(results), speeds, results.Length, 10);

        static string Stat(params ShotResultKind[] results) => Text(Speeds(results.Length), results).Stat;

        // ---- the words ----

        [Test]
        public void Before_the_first_Shot_it_says_First_Shot_with_a_zero_tally()
        {
            LowerThirdText text = LowerThird.Choose(new List<ShotResultKind>(), new List<double?>(), 1, 3);
            Assert.AreEqual("First Shot", text.Stat);
            Assert.AreEqual("GOALS 0  ·  SHOTS 0", text.Tally);
            Assert.AreEqual("SHOT 1 OF 3", text.ShotOf);
        }

        [Test]
        public void The_tally_counts_Goals_and_Shots_and_a_Too_Late_Shot_is_a_Shot()
        {
            Assert.AreEqual("GOALS 2  ·  SHOTS 3", Text(Speeds(3), G, M, G).Tally);
            Assert.AreEqual("GOALS 0  ·  SHOTS 1", Text(new List<double?> { null }, T).Tally);
        }

        [Test]
        public void The_cap_reads_the_Shot_of_the_Round_and_never_shot_0()
        {
            var none = new List<ShotResultKind>();
            var noSpeeds = new List<double?>();
            Assert.AreEqual("SHOT 2 OF 3", LowerThird.Choose(none, noSpeeds, 2, 3).ShotOf);
            Assert.AreEqual("SHOT 1 OF 5", LowerThird.Choose(none, noSpeeds, 0, 5).ShotOf, "before StartRound");
            Assert.AreEqual("SHOT 5 OF 5", LowerThird.Choose(none, noSpeeds, 6, 5).ShotOf);
        }

        [Test]
        public void A_streak_of_3_Goals_in_a_row_wins()
        {
            Assert.AreEqual("3 in a row!", Stat(G, G, G));
            Assert.AreEqual("4 in a row!", Stat(S, G, G, G, G));
        }

        [Test]
        public void A_streak_must_be_running_now_and_2_in_a_row_is_not_one()
        {
            Assert.AreEqual("Last Shot 100 km/h  ·  average 100 km/h", Stat(G, G));
            Assert.AreEqual("Last Shot 100 km/h  ·  average 100 km/h", Stat(G, G, G, S), "a streak that ended is not shown");
        }

        [Test]
        public void No_line_mentions_the_best_score()
        {
            // The chase for the best score is gone (#151): people play once. Every kind of line, every Round length, says nothing of it.
            var kinds = new[] { G, S, M, T };
            var seen = new List<string>();
            for (int n = 0; n <= Round.ShotsPerRound; n++)
                for (int pattern = 0; pattern < 40; pattern++)
                {
                    var results = new ShotResultKind[n];
                    var speeds = new List<double?>();
                    for (int i = 0; i < n; i++)
                    {
                        results[i] = kinds[(pattern * (i + 3) + i * i) % 4];
                        speeds.Add(results[i] == T ? (double?)null : 90 + i);
                    }
                    LowerThirdText text = LowerThird.Choose(new List<ShotResultKind>(results), speeds, n, Round.ShotsPerRound);
                    seen.Add(text.Tally);
                    seen.Add(text.Stat);
                    seen.Add(text.ShotOf);
                }
            Assert.Greater(seen.Count, 100);
            foreach (string line in seen)
            {
                StringAssert.DoesNotContain("best", line.ToLowerInvariant());
                StringAssert.DoesNotContain("beat", line.ToLowerInvariant());
                StringAssert.DoesNotContain("chasing", line.ToLowerInvariant());
            }
        }

        // ---- the speed stat ----

        [Test]
        public void After_the_first_Shot_it_is_that_Shots_speed()
        {
            Assert.AreEqual("Last Shot 92 km/h", Text(new List<double?> { 92 }, S).Stat);
        }

        [Test]
        public void Then_it_is_the_last_Shots_speed_and_the_average_of_the_Shots_so_far()
        {
            Assert.AreEqual("Last Shot 100 km/h  ·  average 90 km/h", Text(new List<double?> { 80, 100 }, G, S).Stat);
            Assert.AreEqual("Last Shot 60 km/h  ·  average 80 km/h", Text(new List<double?> { 80, 100, 60 }, G, S, G).Stat);
        }

        [Test]
        public void Speeds_are_rounded_like_the_result_words_speed()
        {
            // 92.5 rounds away from zero to 93 (ShotResultText.Speed); the average 85.25 shows as 85.
            Assert.AreEqual("93 km/h", ShotResultText.Speed(92.5));
            Assert.AreEqual("Last Shot 93 km/h  ·  average 85 km/h", Text(new List<double?> { 78, 92.5 }, G, S).Stat);
        }

        [Test]
        public void A_Shot_with_no_speed_is_left_out_of_the_average_and_shows_no_speed()
        {
            // Too Late has no puck: no speed of its own, and it does not pull the average down.
            Assert.AreEqual("Average 90 km/h", Text(new List<double?> { 80, 100, null }, G, S, T).Stat);
            Assert.AreEqual("Last Shot 100 km/h  ·  average 90 km/h", Text(new List<double?> { 80, null, 100 }, G, T, S).Stat);
        }

        [Test]
        public void With_no_speed_at_all_it_counts_the_Shots_left()
        {
            var none = new List<double?> { null, null, null };
            Assert.AreEqual("2 Shots left", LowerThird.Choose(new List<ShotResultKind> { T }, none, 1, 3).Stat);
            Assert.AreEqual("1 Shot left", LowerThird.Choose(new List<ShotResultKind> { T, T }, none, 2, 3).Stat);
            Assert.AreEqual("Round over", LowerThird.Choose(new List<ShotResultKind> { T, T, T }, none, 3, 3).Stat);
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
            StringAssert.DoesNotContain("km/h", LowerThird.Of(round).Stat, "no speed to average yet");
            round.Tick(round.ResultHoldLength);
            round.Tick(3.7);
            round.SubmitShotAndWhistle(50, 50, 80, false);
            Assert.AreEqual("Last Shot 80 km/h", LowerThird.Of(round).Stat, "the Round's only speed is the real Shot's, not 150");
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
            LowerThirdText text = LowerThird.Of(round);
            foreach (string line in new[] { text.Tally, text.Stat, text.ShotOf })
            {
                StringAssert.DoesNotContain("best", line.ToLowerInvariant());
                StringAssert.DoesNotContain("beat", line.ToLowerInvariant());
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

        // ---- the words are built only when the values change (#151) ----

        [Test]
        public void The_words_are_built_once_per_Shot_and_the_same_strings_come_back()
        {
            var words = new LowerThirdWords();
            var results = new List<ShotResultKind> { G };
            var speeds = new List<double?> { 90 };
            LowerThirdText a = words.Get(results, speeds, 1, 3);
            LowerThirdText b = words.Get(results, speeds, 1, 3);
            Assert.AreEqual(1, words.Builds);
            Assert.AreSame(a.Tally, b.Tally);
            Assert.AreSame(a.Stat, b.Stat);
            Assert.AreSame(a.ShotOf, b.ShotOf);
            results.Add(S);
            speeds.Add(70);
            LowerThirdText c = words.Get(results, speeds, 2, 3);
            Assert.AreEqual(2, words.Builds, "a new Shot builds again");
            Assert.AreEqual("GOALS 1  ·  SHOTS 2", c.Tally);
            Assert.AreEqual("Last Shot 70 km/h  ·  average 80 km/h", c.Stat);
        }

        [Test]
        public void The_next_Shot_number_builds_new_words_for_the_cap()
        {
            var words = new LowerThirdWords();
            var results = new List<ShotResultKind> { G };
            var speeds = new List<double?> { 90 };
            Assert.AreEqual("SHOT 1 OF 3", words.Get(results, speeds, 1, 3).ShotOf);
            Assert.AreEqual("SHOT 2 OF 3", words.Get(results, speeds, 2, 3).ShotOf);
            Assert.AreEqual(2, words.Builds);
        }

        [Test]
        public void A_new_Round_with_as_many_results_still_builds_new_words()
        {
            var words = new LowerThirdWords();
            Assert.AreEqual("GOALS 1  ·  SHOTS 1", words.Get(new List<ShotResultKind> { G }, new List<double?> { 90 }, 1, 3).Tally);
            Assert.AreEqual("GOALS 0  ·  SHOTS 1", words.Get(new List<ShotResultKind> { S }, new List<double?> { 90 }, 1, 3).Tally);
            Assert.AreEqual("Last Shot 91 km/h", words.Get(new List<ShotResultKind> { S }, new List<double?> { 91 }, 1, 3).Stat);
            Assert.AreEqual(3, words.Builds);
        }

        [Test]
        public void Asking_for_the_same_words_every_frame_makes_no_garbage()
        {
            var words = new LowerThirdWords();
            var results = new List<ShotResultKind> { G, S, G };
            var speeds = new List<double?> { 90, 80, null };
            words.Get(results, speeds, 3, 3);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) words.Get(results, speeds, 3, 3);
            long garbage = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, garbage, "bytes allocated in 1000 frames");
        }

        [Test]
        public void The_bar_reads_the_Round_through_the_cache()
        {
            var round = InWindow();
            round.SubmitShotAndWhistle(50, 50, 100, false);
            var words = new LowerThirdWords();
            LowerThirdText a = words.Get(round), b = words.Get(round);
            Assert.AreEqual(1, words.Builds);
            Assert.AreSame(a.Stat, b.Stat);
            Assert.AreEqual("GOALS 1  ·  SHOTS 1", a.Tally);
            Assert.AreEqual("Last Shot 100 km/h", a.Stat);
            Assert.AreEqual("SHOT 1 OF " + round.ShotCount, a.ShotOf);
        }

        // ---- when it is on screen: always, while a Round plays ----

        static ScreenFlow NewFlow(bool replayOn = true) =>
            new ScreenFlow(ScriptedRandom.Halves(), new SaveData(), FakeReach.NeverReaches(), new RoundSettings { ReplayOn = replayOn });

        // Plays a whole Round from the Start screen in 0.05 s steps: each Shot is a Goal, whistled 0.3 s after it leaves.
        // Asserts it shows at every step of the Playing screen; returns what the steps went through.
        static HashSet<string> PlayWholeRound(ScreenFlow flow)
        {
            var seen = new HashSet<string>();
            Assert.IsFalse(LowerThird.Shown(flow), "the Start screen");
            flow.Press();
            double flying = 0;
            for (int step = 0; step < 4000 && flow.Screen == FlowScreen.Playing; step++)
            {
                Round round = flow.Round;
                Assert.IsTrue(LowerThird.Shown(flow), "Playing, " + round.Phase + " at step " + step);
                seen.Add(round.Phase.ToString());
                if (round.Phase == RoundPhase.ShootingWindow) flow.SubmitShot(50, 90, 100, false);
                if (round.AwaitingWhistle)
                {
                    seen.Add("flight");
                    flying += 0.05;
                    if (flying >= 0.3) { round.WhistleNow(); flying = 0; }
                }
                if (ArenaShow.From(flow).Camera == CameraMode.LookUp) seen.Add("Replay");
                flow.Tick(0.05);
            }
            Assert.AreEqual(FlowScreen.End, flow.Screen);
            Assert.IsFalse(LowerThird.Shown(flow), "the End screen");
            return seen;
        }

        [Test]
        public void It_shows_through_the_whole_Round_aim_flight_result_and_Replay_and_not_on_the_Start_or_End_screen()
        {
            HashSet<string> seen = PlayWholeRound(NewFlow());
            foreach (string part in new[] { "SkateIn", "ShootingWindow", "flight", "ResultHold", "Replay" })
                Assert.IsTrue(seen.Contains(part), "the Round went through " + part);
        }

        [Test]
        public void It_shows_through_the_whole_Round_with_the_Replay_off()
        {
            HashSet<string> seen = PlayWholeRound(NewFlow(replayOn: false));
            Assert.IsFalse(seen.Contains("Replay"));
            Assert.IsTrue(seen.Contains("ResultHold"));
        }

        [Test]
        public void It_is_hidden_in_Test_Mode_where_the_Score_Box_already_counts_Goals_and_Shots()
        {
            var flow = new ScreenFlow(ScriptedRandom.Halves(), new SaveData(), FakeReach.NeverReaches(), null);
            flow.OpenSettings();
            flow.StartTestMode();
            Assert.AreEqual(FlowScreen.Playing, flow.Screen);
            Assert.IsFalse(LowerThird.Shown(flow));
        }

        [Test]
        public void The_old_Score_Box_hides_while_the_bar_shows_and_keeps_its_old_behaviour_elsewhere()
        {
            var flow = NewFlow();
            Assert.IsFalse(ArenaShow.From(flow).ScoreBox.Shown, "the Start screen");
            flow.Press();
            double flying = 0;
            for (int step = 0; step < 4000 && flow.Screen == FlowScreen.Playing; step++)
            {
                Assert.IsTrue(LowerThird.Shown(flow));
                Assert.IsFalse(ArenaShow.From(flow).ScoreBox.Shown, "the bar shows, so no Score Box: " + flow.Round.Phase + " at step " + step);
                if (flow.Round.Phase == RoundPhase.ShootingWindow) flow.SubmitShot(50, 90, 100, false);
                if (flow.Round.AwaitingWhistle)
                {
                    flying += 0.05;
                    if (flying >= 0.3) { flow.Round.WhistleNow(); flying = 0; }
                }
                flow.Tick(0.05);
            }
            Assert.AreEqual(FlowScreen.End, flow.Screen);
            Assert.IsTrue(ArenaShow.From(flow).ScoreBox.Shown, "the End screen has no bar, so the Score Box stays");

            var test = new ScreenFlow(ScriptedRandom.Halves(), new SaveData(), FakeReach.NeverReaches(), null);
            test.OpenSettings();
            test.StartTestMode();
            Assert.IsTrue(ArenaShow.From(test).ScoreBox.Shown, "Test Mode has no bar, so the Score Box stays");
        }

        [Test]
        public void The_stat_changes_in_place_at_the_Whistle_of_each_Shot()
        {
            var round = InWindow();
            Assert.AreEqual("First Shot", LowerThird.Of(round).Stat, "aiming the first Shot");
            round.SubmitShot(50, 50, 100, false);
            round.Tick(0.3);
            Assert.AreEqual("First Shot", LowerThird.Of(round).Stat, "the puck flies: no result yet");
            round.WhistleNow();
            LowerThirdText after = LowerThird.Of(round);
            Assert.AreEqual("Last Shot 100 km/h", after.Stat, "the Whistle");
            Assert.AreEqual("GOALS 1  ·  SHOTS 1", after.Tally);
            round.Tick(round.ResultHoldLength);
            round.Tick(3.7);
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
            Assert.AreEqual("SHOT 2 OF " + round.ShotCount, LowerThird.Of(round).ShotOf, "the next Shot");
            Assert.AreEqual("Last Shot 100 km/h", LowerThird.Of(round).Stat, "the stat stays until the next Whistle");
        }
    }
}
