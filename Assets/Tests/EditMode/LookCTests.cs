using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// Look C's five colours live in C# (LookC). The Settings page's style sheet cannot read C#, so it repeats them as USS text (#151):
    /// these tests read the .uss file and fail when the two lists drift apart.
    /// </summary>
    public class LookCTests
    {
        static string Uss => File.ReadAllText(Path.Combine(Application.dataPath, "Resources", "UI", "LookC.uss"));

        static string Rgb(Color32 c) => "rgb(" + c.r + ", " + c.g + ", " + c.b + ")";

        // The text of one rule: from "selector {" to its "}". The selector must be at the start of a line.
        static string Rule(string uss, string selector)
        {
            Match m = Regex.Match(uss, @"(?m)^" + Regex.Escape(selector) + @"\s*\{([^}]*)\}");
            Assert.IsTrue(m.Success, "no rule " + selector + " in LookC.uss");
            return m.Groups[1].Value;
        }

        static void AssertRuleHas(string uss, string selector, string property, Color32 colour)
        {
            string body = Rule(uss, selector);
            Assert.IsTrue(Regex.IsMatch(body, @"(?<![\w-])" + Regex.Escape(property) + @":\s*" + Regex.Escape(Rgb(colour))),
                selector + " { " + property + " } should be " + Rgb(colour) + " (LookC), got: " + body.Trim());
        }

        [Test]
        public void The_five_colours_are_the_League_Skin_colours_of_look_C()
        {
            Assert.AreEqual(new Color32(10, 16, 34, 255), LookC.Navy);
            Assert.AreEqual(new Color32(20, 60, 150, 255), LookC.Blue);
            Assert.AreEqual(new Color32(232, 190, 40, 255), LookC.Gold);
            Assert.AreEqual(new Color32(200, 30, 45, 255), LookC.Red);
            Assert.AreEqual(new Color32(255, 255, 255, 255), LookC.White);
        }

        [Test]
        public void The_style_sheet_uses_the_same_values_for_each_colour_it_names()
        {
            string uss = Uss;
            AssertRuleHas(uss, ".lc-topbar", "background-color", LookC.Navy);
            AssertRuleHas(uss, ".lc-title", "background-color", LookC.Red);
            AssertRuleHas(uss, ".lc-title", "color", LookC.White);
            AssertRuleHas(uss, ".lc-title-stripe", "background-color", LookC.Gold);
            AssertRuleHas(uss, ".lc-stripe-red", "color", LookC.Red);
            AssertRuleHas(uss, ".lc-stripe-white", "color", LookC.White);
            AssertRuleHas(uss, ".lc-stripe-gold", "color", LookC.Gold);
            AssertRuleHas(uss, ".lc-tab-fill", "color", LookC.Blue);
        }

        [Test]
        public void The_style_sheets_header_comment_lists_the_same_five_colours()
        {
            string uss = Uss;
            string header = uss.Substring(0, uss.IndexOf("*/"));
            foreach (var named in new (string Name, Color32 Colour)[] { ("navy", LookC.Navy), ("rail blue", LookC.Blue), ("gold", LookC.Gold), ("red", LookC.Red) })
                StringAssert.Contains(named.Name + " rgb(" + named.Colour.r + "," + named.Colour.g + "," + named.Colour.b + ")", header, "header comment: " + named.Name);
        }

        [Test]
        public void Every_colour_of_look_C_shows_up_in_the_style_sheet()
        {
            string uss = Uss;
            foreach (Color32 c in new[] { LookC.Navy, LookC.Blue, LookC.Gold, LookC.Red, LookC.White })
                StringAssert.Contains(Rgb(c), uss);
        }
    }
}
