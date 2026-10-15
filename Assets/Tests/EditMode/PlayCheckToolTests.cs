using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using HockeyGame.PlayCheck;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The agent Play-check tool (#172), the parts that need no Play: the event script parser, the probe list and the log line format.
    /// </summary>
    public class PlayCheckToolTests
    {
        [Test]
        public void The_done_when_script_reads_as_five_events_in_order()
        {
            var events = PlayCheckScript.Parse("goal, save:Glove, save:Pads, miss, idle:10");

            Assert.AreEqual(5, events.Count);
            Assert.AreEqual(PlayCheckEventKind.Goal, events[0].Kind);
            Assert.AreEqual(PlayCheckEventKind.Save, events[1].Kind);
            Assert.AreEqual(SaveReaction.Glove, events[1].Reaction);
            Assert.AreEqual(SaveReaction.Pads, events[2].Reaction);
            Assert.AreEqual(PlayCheckEventKind.Miss, events[3].Kind);
            Assert.AreEqual(PlayCheckEventKind.Idle, events[4].Kind);
            Assert.AreEqual(10.0, events[4].Seconds, 1e-12);
        }

        [Test]
        public void Every_event_word_reads_and_lines_split_like_commas()
        {
            var events = PlayCheckScript.Parse("toolate\nreplay:on\r\nreplay:off, save:Blocker,save:Stick\n save:Body ,idle:2.5");

            Assert.AreEqual(7, events.Count);
            Assert.AreEqual(PlayCheckEventKind.TooLate, events[0].Kind);
            Assert.AreEqual(PlayCheckEventKind.Replay, events[1].Kind);
            Assert.IsTrue(events[1].On);
            Assert.IsFalse(events[2].On);
            Assert.AreEqual(SaveReaction.Blocker, events[3].Reaction);
            Assert.AreEqual(SaveReaction.Stick, events[4].Reaction);
            Assert.AreEqual(SaveReaction.Body, events[5].Reaction);
            Assert.AreEqual(2.5, events[6].Seconds, 1e-12);
        }

        [Test]
        public void Words_read_in_any_case_and_keep_one_written_form()
        {
            var events = PlayCheckScript.Parse("GOAL, Save:glove, Idle:10, Replay:OFF");

            Assert.AreEqual("goal", events[0].Text);
            Assert.AreEqual("save:Glove", events[1].Text);
            Assert.AreEqual("idle:10", events[2].Text);
            Assert.AreEqual("replay:off", events[3].Text);
        }

        [Test]
        public void Only_goal_save_miss_and_toolate_are_shots()
        {
            var events = PlayCheckScript.Parse("goal, save:Pads, miss, toolate, replay:on, idle:1");

            Assert.AreEqual(new[] { true, true, true, true, false, false }, new[] { events[0].IsShot, events[1].IsShot, events[2].IsShot, events[3].IsShot, events[4].IsShot, events[5].IsShot });
        }

        [TestCase("goal, shoot", "shoot")]
        [TestCase("save:Elbow", "save:Elbow")]
        [TestCase("save", "save")]
        [TestCase("replay:maybe", "replay:maybe")]
        [TestCase("idle:abc", "idle:abc")]
        [TestCase("idle:-1", "idle:-1")]
        [TestCase("goal,,miss", "empty")]
        public void A_wrong_event_fails_with_a_message_naming_it(string script, string named)
        {
            var e = Assert.Throws<ArgumentException>(() => PlayCheckScript.Parse(script));

            StringAssert.Contains(named, e.Message);
            StringAssert.Contains("goal, save:<Glove|Blocker|Pads|Stick|Body>, miss, toolate, replay:on|off, idle:<seconds>", e.Message);
        }

        [Test]
        public void An_empty_script_fails()
        {
            Assert.Throws<ArgumentException>(() => PlayCheckScript.Parse("  "));
            Assert.Throws<ArgumentException>(() => PlayCheckScript.Parse(null));
        }

        [Test]
        public void Seconds_read_with_a_dot_whatever_the_machine_culture()
        {
            var old = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                Assert.AreEqual(2.5, PlayCheckScript.Parse("idle:2.5")[0].Seconds, 1e-12);
            }
            finally { Thread.CurrentThread.CurrentCulture = old; }
        }

        [Test]
        public void Probes_read_clip_bones_and_positions()
        {
            var probes = PlayCheckScript.ParseProbes("clip, bone:head ,pos:Puck Physics");

            Assert.AreEqual(new[] { "clip", "bone:Head", "pos:Puck Physics" }, probes);
        }

        [Test]
        public void No_probes_is_an_empty_list()
        {
            Assert.AreEqual(0, PlayCheckScript.ParseProbes(null).Count);
            Assert.AreEqual(0, PlayCheckScript.ParseProbes(" ").Count);
        }

        [TestCase("speed")]
        [TestCase("bone:Tail")]
        [TestCase("pos:")]
        public void A_wrong_probe_fails_with_a_message_naming_it(string probe)
        {
            var e = Assert.Throws<ArgumentException>(() => PlayCheckScript.ParseProbes(probe));

            StringAssert.Contains(probe, e.Message);
            StringAssert.Contains("clip, bone:<HumanBodyBones>, pos:<GameObject name>", e.Message);
        }

        [Test]
        public void A_log_line_is_one_json_object_with_its_keys_in_a_fixed_order()
        {
            string line = PlayCheckLog.Line(12.3456, 340, "event", "save:Glove", "ResultHold", "Save", 12.1, null,
                new[] { new KeyValuePair<string, string>("clip", "Idle"), new KeyValuePair<string, string>("bone:Head", "1.00,2.00,3.00") });

            Assert.AreEqual("{\"t\":12.346,\"frame\":340,\"type\":\"event\",\"event\":\"save:Glove\",\"phase\":\"ResultHold\",\"result\":\"Save\","
                            + "\"holdStart\":12.100,\"holdEnd\":null,\"probes\":{\"clip\":\"Idle\",\"bone:Head\":\"1.00,2.00,3.00\"}}", line);
        }

        [Test]
        public void A_log_line_with_nothing_known_writes_nulls_and_empty_probes()
        {
            string line = PlayCheckLog.Line(0, 1, "frame", null, "Idle", null, null, null, null);

            Assert.AreEqual("{\"t\":0.000,\"frame\":1,\"type\":\"frame\",\"event\":null,\"phase\":\"Idle\",\"result\":null,\"holdStart\":null,\"holdEnd\":null,\"probes\":{}}", line);
        }

        [Test]
        public void A_log_line_escapes_quotes_backslashes_and_new_lines()
        {
            string line = PlayCheckLog.Line(1, 2, "error", "a \"b\"\\c\nd", null, null, null, null, null);

            StringAssert.Contains("\"event\":\"a \\\"b\\\"\\\\c\\nd\"", line);
            Assert.IsFalse(line.Contains("\n"));
        }

        [Test]
        public void A_log_line_writes_a_dot_whatever_the_machine_culture()
        {
            var old = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                StringAssert.StartsWith("{\"t\":1.500,", PlayCheckLog.Line(1.5, 1, "frame", null, null, null, null, null, null));
            }
            finally { Thread.CurrentThread.CurrentCulture = old; }
        }
    }
}
