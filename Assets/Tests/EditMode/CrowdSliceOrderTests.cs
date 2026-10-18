using System;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class CrowdSliceOrderTests
    {
        [Test]
        public void Grouping_sorts_by_slice_then_original_fan_id()
        {
            Assert.That(CrowdSliceOrder.Order(new[] { 2, 1, 2, 1 }, new[] { 1, 3, 0, 2 }), Is.EqualTo(new[] { 3, 1, 2, 0 }));
        }
        [Test]
        public void Copies_retain_exact_payload_and_round_trip_original_order()
        {
            var fans = new[] { new object(), new object(), new object(), new object() };
            var order = CrowdSliceOrder.Order(new[] { 2, 1, 2, 1 }, new[] { 0, 1, 2, 3 });
            var moved = CrowdSliceOrder.Copy(fans, order);
            var ids = CrowdSliceOrder.Copy(new[] { 0, 1, 2, 3 }, order);
            var back = CrowdSliceOrder.Order(new[] { 0, 0, 0, 0 }, ids);
            Assert.That(CrowdSliceOrder.Copy(moved, back), Is.EqualTo(fans));
            for (int i = 0; i < moved.Length; i++) Assert.That(moved[i], Is.SameAs(fans[order[i]]));
        }
        [Test]
        public void Sector_keys_keep_outfit_and_tier_and_accept_only_the_experiment_counts()
        {
            foreach (int sectors in new[] { 4, 8, 16, 32 })
            {
                Assert.DoesNotThrow(() => CrowdSliceOrder.Validate(sectors));
                Assert.That(CrowdSliceOrder.Key(3, 1, 1, 0, sectors), Is.EqualTo(7 * sectors + sectors / 2));
                Assert.That(CrowdSliceOrder.Key(0, 0, -1, 0, sectors), Is.EqualTo(sectors - 1));
            }
            foreach (int sectors in new[] { -1, 0, 1, 3, 64 })
                Assert.Throws<ArgumentOutOfRangeException>(() => CrowdSliceOrder.Validate(sectors));
        }
    }
}
