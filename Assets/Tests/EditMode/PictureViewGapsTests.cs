using System.Collections.Generic;
using HockeyGame.Editor;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The picture test's Gap views (#150): the Gap measure leaves fewer Gaps than before #134, so a view of a Set-up Mistake may
    /// show none (right-late-settle: settled by the time of the picture), and PictureTestTool used to throw there. A view with no
    /// Gaps is a picture only; the other Mistake views are still measured.
    /// </summary>
    public class PictureViewGapsTests
    {
        GameObject _parent;

        [TearDown]
        public void TearDown()
        {
            if (_parent != null) Object.DestroyImmediate(_parent);
        }

        [Test]
        public void A_Set_up_Mistake_view_with_no_Gaps_is_a_picture_only_not_an_error()
        {
            Assert.IsFalse(PictureTestTool.MeasuresGaps(SetUp.Depth, NetFaceGaps.None), "a Mistake with no Gap has nothing to measure");
            Assert.IsTrue(PictureTestTool.MeasuresGaps(SetUp.Depth, NetFaceGaps.Where((x, y) => true)));
            Assert.IsFalse(PictureTestTool.MeasuresGaps(SetUp.Clean, NetFaceGaps.Where((x, y) => true)), "a Clean Set-up is never measured");
        }

        [Test]
        public void A_view_with_no_Gaps_is_never_measured_and_most_Mistake_views_still_are()
        {
            _parent = new GameObject("Rink (test)");
            var settings = new RoundSettings();
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "needs the goalie model");
            var reach = new GoalieCharacterReach(character, settings);
            var counts = new List<string>();
            int measured = 0;
            foreach (PictureTestTool.PicturePoint point in PictureTestTool.Points)
            {
                if (!point.SetUp.IsMistake) continue;
                NetFaceGaps gaps = reach.Gaps(point.SetUp, point.Side, PictureTestTool.WindowSeconds,
                    Breakaway.ShotStart(point.Side, PictureTestTool.WindowSeconds, settings), 0, NetFaceGaps.Cells);
                counts.Add(point.Name + "=" + gaps.Count);
                if (PictureTestTool.MeasuresGaps(point.SetUp, gaps)) measured++;
                else Assert.AreEqual(0, gaps.Count, point.Name + " is skipped although it has Gaps");
                // Only the one named view may have none: a Mistake view that loses its Gaps for another reason fails here (and throws in the tool).
                if (gaps.Count == 0) Assert.AreEqual(PictureTestTool.NoGapsViewAllowed, point.Name, "a Mistake view with no Gaps (" + string.Join(", ", counts) + ")");
            }
            // A Late Settle that has settled has no Gap left by design: its view is a picture only. The others are measured.
            Assert.GreaterOrEqual(measured, 2, "the Mistake views measured (" + string.Join(", ", counts) + ")");
        }
    }
}
