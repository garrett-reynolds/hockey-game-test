using System.Collections.Generic;
using HockeyGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class StartLogoTests
    {
        static ScreenFlow NewFlow() => new ScreenFlow(ScriptedRandom.Halves(), new SaveData(), FakeReach.NeverReaches());

        [Test]
        public void The_logo_shows_on_the_start_screen_only_and_comes_back_after_a_Round()
        {
            ScreenFlow flow = NewFlow();
            Assert.IsTrue(StartLogo.Shown(flow), "start screen");

            flow.ToggleSettings();
            Assert.IsFalse(StartLogo.Shown(flow), "behind the settings screen");
            flow.ToggleSettings();
            Assert.IsTrue(StartLogo.Shown(flow), "settings closed again");

            flow.Press();
            Assert.AreEqual(FlowScreen.Playing, flow.Screen);
            Assert.IsFalse(StartLogo.Shown(flow), "Round starts");

            for (int shot = 0; shot < Round.ShotsPerRound; shot++)
            {
                flow.Tick(3.7);
                Assert.IsFalse(StartLogo.Shown(flow), "in the Round, shot " + shot);
                flow.SubmitShot(-10, 50, 100, false);
                flow.Round.WhistleNow();
                flow.Tick(flow.Round.ResultHoldLength);
            }
            Assert.AreEqual(FlowScreen.End, flow.Screen);
            Assert.IsFalse(StartLogo.Shown(flow), "end screen");

            flow.Tick(1000);
            Assert.AreEqual(FlowScreen.Start, flow.Screen);
            Assert.IsTrue(StartLogo.Shown(flow), "back at the start screen, the Round still over");
        }

        static IEnumerable<Vector2> Screens()
        {
            yield return new Vector2(1920, 1080);
            yield return new Vector2(1280, 720);
            yield return new Vector2(1024, 768);
        }

        [Test]
        public void The_logo_is_on_screen_and_hides_no_other_start_screen_part()
        {
            foreach (Vector2 s in Screens())
            {
                Rect logo = StartScreenLayout.Logo(s.x, s.y);
                string at = s.x + "x" + s.y;
                Assert.GreaterOrEqual(logo.xMin, 0f, at);
                Assert.GreaterOrEqual(logo.yMin, 0f, at);
                Assert.LessOrEqual(logo.xMax, s.x, at);
                Assert.GreaterOrEqual(logo.height, 0.2f * s.y, at + ": readable from across the room");
                var others = new Dictionary<string, Rect>
                {
                    { "best score", StartScreenLayout.BestScore(s.x, s.y) },
                    { "press to play prompt", StartScreenLayout.Prompt(s.x, s.y) },
                    { "usb status", StartScreenLayout.UsbStatus(s.x, s.y) },
                    { "test shots line", StartScreenLayout.TestShots(s.x, s.y) }
                };
                foreach (var o in others)
                {
                    Assert.IsFalse(logo.Overlaps(o.Value), at + ": the logo covers the " + o.Key);
                    Assert.LessOrEqual(o.Value.yMax, s.y, at + ": " + o.Key + " is on screen");
                }
            }
        }

        [Test]
        public void The_logo_uses_the_League_Skin_colours()
        {
            TeamSkin league = TeamSkin.League;
            Assert.AreEqual((Color32)league.Rail, StartLogoArt.Blue);
            Assert.AreEqual((Color32)league.KickPlate, StartLogoArt.Gold);
            Assert.AreEqual((Color32)league.VideoBoardBack, StartLogoArt.Navy);
            Assert.AreEqual((Color32)league.VideoBoardWords, StartLogoArt.White);
            // The League Skin has no red: this is look C's red, picked in #137 (the prototype's Red, 200 30 45).
            Assert.AreEqual(new Color32(200, 30, 45, 255), StartLogoArt.Red);
        }

        [Test]
        public void The_dark_strip_holds_the_three_text_lines_and_stays_clear_of_the_logo_and_the_corner_hints()
        {
            foreach (Vector2 s in Screens())
            {
                Rect strip = StartScreenLayout.TextStrip(s.x, s.y);
                string at = s.x + "x" + s.y;
                Assert.GreaterOrEqual(strip.xMin, 0f, at);
                Assert.LessOrEqual(strip.xMax, s.x, at);
                Assert.IsFalse(strip.Overlaps(StartScreenLayout.Logo(s.x, s.y)), at + ": strip on the logo");
                Assert.IsFalse(strip.Overlaps(StartScreenLayout.TestShots(s.x, s.y)), at + ": strip on the test shots line");
                foreach (Rect line in new[] { StartScreenLayout.BestScore(s.x, s.y), StartScreenLayout.Prompt(s.x, s.y), StartScreenLayout.UsbStatus(s.x, s.y) })
                {
                    Assert.LessOrEqual(strip.yMin, line.yMin, at);
                    Assert.GreaterOrEqual(strip.yMax, line.yMax, at);
                }
                Assert.Greater(StartLogoArt.StripBack.a, 0.5f, at + ": dark enough to read white text on ice");
            }
        }

        // The picture check: red, blue, gold and white all show, and the lettering band is mostly opaque.
        static List<string> Missing(Texture2D t)
        {
            var missing = new List<string>();
            Color32[] px = t.GetPixels32();
            var wanted = new Dictionary<string, Color32>
            {
                { "red", StartLogoArt.Red }, { "blue", StartLogoArt.Blue }, { "gold", StartLogoArt.Gold }, { "white", StartLogoArt.White }
            };
            foreach (var w in wanted)
            {
                int n = 0;
                foreach (Color32 c in px)
                    if (c.a == 255 && c.r == w.Value.r && c.g == w.Value.g && c.b == w.Value.b) n++;
                if (n < px.Length / 100) missing.Add(w.Key);
            }
            return missing;
        }

        [Test]
        public void The_picture_check_fails_on_a_blank_picture()
        {
            var blank = new Texture2D(StartLogoArt.Width, StartLogoArt.Height, TextureFormat.RGBA32, false);
            blank.SetPixels32(new Color32[StartLogoArt.Width * StartLogoArt.Height]); // all clear
            try { Assert.AreEqual(4, Missing(blank).Count); }
            finally { Object.DestroyImmediate(blank); }
        }

        [Test]
        public void The_logo_picture_has_every_League_colour()
        {
            Texture2D logo = StartLogoArt.Make();
            try { Assert.IsEmpty(Missing(logo), "colours missing from the logo"); }
            finally { Object.DestroyImmediate(logo); }
        }
    }
}
