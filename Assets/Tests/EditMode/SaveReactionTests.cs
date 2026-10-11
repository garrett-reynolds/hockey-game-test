using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The Save Reaction is chosen from where the Shot meets the Net Face (shooter's view, 0..100 from the bottom-left).
    /// The catching glove is on the shooter's right.
    /// </summary>
    public class SaveReactionTests
    {
        [TestCase(85, 85, SaveReaction.Glove, TestName = "High on the shooter's right: glove")]
        [TestCase(80, 50, SaveReaction.Glove, TestName = "Mid height on the shooter's right: glove")]
        [TestCase(55, 90, SaveReaction.Glove, TestName = "Top centre, right of middle: glove")]
        [TestCase(15, 85, SaveReaction.Blocker, TestName = "High on the shooter's left: blocker")]
        [TestCase(20, 50, SaveReaction.Blocker, TestName = "Mid height on the shooter's left: blocker")]
        [TestCase(45, 90, SaveReaction.Blocker, TestName = "Top centre, left of middle: blocker")]
        [TestCase(10, 15, SaveReaction.Pads, TestName = "Low on the shooter's left: pads")]
        [TestCase(90, 5, SaveReaction.Pads, TestName = "Along the ice by the right post: pads")]
        [TestCase(50, 30, SaveReaction.Pads, TestName = "Low centre, above the ice: pads")]
        [TestCase(50, 55, SaveReaction.Body, TestName = "Centre: body")]
        [TestCase(40, 70, SaveReaction.Body, TestName = "Centre, chest high: body")]
        [TestCase(50, 5, SaveReaction.Stick, TestName = "Along the ice through the middle: stick")]
        [TestCase(62, 8, SaveReaction.Stick, TestName = "Along the ice, right of middle: stick")]
        public void A_save_reaction_is_chosen_from_the_Net_Face_point(double x, double y, SaveReaction expected)
        {
            Assert.AreEqual(expected, SaveReactions.For(x, y));
        }

        [TestCase(SaveReaction.Glove, SoundCue.GloveCatch)]
        [TestCase(SaveReaction.Blocker, SoundCue.BlockerHit)]
        [TestCase(SaveReaction.Stick, SoundCue.BlockerHit)]
        [TestCase(SaveReaction.Pads, SoundCue.SaveImpact)]
        [TestCase(SaveReaction.Body, SoundCue.SaveImpact)]
        public void The_save_sound_is_the_reaction_s_gear(SaveReaction reaction, SoundCue sound)
        {
            Assert.AreEqual(sound, SaveReactions.Sound(reaction));
        }

        [TestCase(SaveReaction.Glove, true)]
        [TestCase(SaveReaction.Blocker, true)]
        [TestCase(SaveReaction.Pads, false)]
        [TestCase(SaveReaction.Stick, false)]
        [TestCase(SaveReaction.Body, false)]
        public void Only_the_glove_and_blocker_saves_are_made_with_a_hand(SaveReaction reaction, bool hand)
        {
            Assert.AreEqual(hand, SaveReactions.IsHand(reaction));
        }

        [Test]
        public void A_glove_save_is_made_with_the_catcher_and_a_blocker_save_with_the_blocker()
        {
            Assert.AreEqual("Catcher", GearPiece.HandOf(SaveReaction.Glove));
            Assert.AreEqual("Blocker", GearPiece.HandOf(SaveReaction.Blocker));
        }
    }
}
