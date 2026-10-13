using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>The lower third's picture (#141): look C's League colours, the same as the Shootout logo's.</summary>
    public class LowerThirdViewTests
    {
        static void AssertColour(Color32 want, Color32 got, string where)
        {
            Assert.AreEqual(want.r, got.r, where + " r");
            Assert.AreEqual(want.g, got.g, where + " g");
            Assert.AreEqual(want.b, got.b, where + " b");
            Assert.AreEqual(255, got.a, where + " opaque");
        }

        [Test]
        public void The_picture_has_a_red_tab_a_navy_bar_a_gold_stripe_and_a_clear_corner()
        {
            Texture2D art = LowerThirdView.MakeArt();
            try
            {
                int top = LowerThirdView.Height; // texture rows count from the bottom; picture y counts from the top
                AssertColour(LowerThirdView.Red, art.GetPixel(230, top - 80), "tab");
                AssertColour(LowerThirdView.Navy, art.GetPixel(760, top - 80), "bar");
                AssertColour(LowerThirdView.Gold, art.GetPixel(600, top - 146), "stripe");
                AssertColour(LowerThirdView.White, art.GetPixel(600, top - 20), "streak");
                Assert.AreEqual(0, art.GetPixel(1220, top - 80).a, 0.001f, "outside the bar's slanted end");
            }
            finally
            {
                Object.DestroyImmediate(art);
            }
        }

        [Test]
        public void The_five_colours_are_the_League_Skin_colours_of_the_Shootout_logo()
        {
            // The same values as StartLogoArt (#140, on its own branch); join them in one list once both are in main.
            Assert.AreEqual(new Color32(10, 16, 34, 255), LowerThirdView.Navy);
            Assert.AreEqual(new Color32(20, 60, 150, 255), LowerThirdView.Blue);
            Assert.AreEqual(new Color32(232, 190, 40, 255), LowerThirdView.Gold);
            Assert.AreEqual(new Color32(200, 30, 45, 255), LowerThirdView.Red);
            Assert.AreEqual(new Color32(255, 255, 255, 255), LowerThirdView.White);
        }
    }
}
