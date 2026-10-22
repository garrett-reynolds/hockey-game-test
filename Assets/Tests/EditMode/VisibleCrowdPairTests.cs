using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class VisibleCrowdPairTests
    {
        [Test]
        public void Visible_contiguous_adjacent_pair_can_merge()
        {
            Assert.IsTrue(VisibleCrowdPair.CanMerge(14, 0, 10, 4, 15, 0, 14, true, true));
        }
        [Test]
        public void Either_invisible_slice_blocks_the_merge()
        {
            Assert.IsFalse(VisibleCrowdPair.CanMerge(14, 0, 10, 4, 15, 0, 14, false, true));
            Assert.IsFalse(VisibleCrowdPair.CanMerge(14, 0, 10, 4, 15, 0, 14, true, false));
        }
        [Test]
        public void Group_tier_pair_and_range_boundaries_stay_separate()
        {
            Assert.IsFalse(VisibleCrowdPair.CanMerge(15, 0, 10, 4, 16, 0, 14, true, true));
            Assert.IsFalse(VisibleCrowdPair.CanMerge(14, 0, 10, 4, 15, 1, 14, true, true));
            Assert.IsFalse(VisibleCrowdPair.CanMerge(14, 0, 10, 4, 16, 0, 14, true, true));
            Assert.IsFalse(VisibleCrowdPair.CanMerge(14, 0, 10, 4, 15, 0, 15, true, true));
            Assert.IsFalse(VisibleCrowdPair.CanMerge(14, 0, 10, 4, 14, 0, 14, true, true));
        }
        [Test]
        public void Invalid_or_wrapped_ranges_cannot_merge()
        {
            Assert.IsFalse(VisibleCrowdPair.CanMerge(14, 0, 10, 0, 15, 0, 10, true, true));
            Assert.IsFalse(VisibleCrowdPair.CanMerge(14, 0, -1, 4, 15, 0, 3, true, true));
            Assert.IsFalse(VisibleCrowdPair.CanMerge(14, 0, int.MaxValue, 1, 15, 0, int.MinValue, true, true));
        }
    }
}
