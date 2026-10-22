using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Tests
{
    public class NetFaceGapsRefreshTests
    {
        private static NetFaceGaps Gaps(params int[] indices)
        {
            var cells = new bool[NetFaceGaps.Cells];
            foreach (int index in indices) cells[index] = true;
            return new NetFaceGaps(cells);
        }

        private static bool At(NetFaceGaps gaps, int index) => gaps.IsGap(index % NetFaceGaps.Columns, index / NetFaceGaps.Columns);

        [Test]
        public void UnchangedSelectedCellsKeepReferenceDespiteOutsideDifferences()
        {
            var source = Gaps(4, 50);
            Assert.That(source.With(Gaps(4, 90), 3, 3), Is.SameAs(source));
        }

        [Test]
        public void ChangedTrueAndFalseCellsCopyOnlySelectedRun()
        {
            var source = Gaps(4, 50);
            var measured = Gaps(3, 90);
            var result = source.With(measured, 3, 3);
            Assert.That(result, Is.Not.SameAs(source));
            Assert.That(result.Count, Is.EqualTo(2));
            for (int cell = 0; cell < NetFaceGaps.Cells; cell++)
            {
                Assert.That(At(result, cell), Is.EqualTo(cell == 3 || cell == 50));
                Assert.That(At(source, cell), Is.EqualTo(cell == 4 || cell == 50));
                Assert.That(At(measured, cell), Is.EqualTo(cell == 3 || cell == 90));
            }
        }

        [Test]
        public void ConstructorCopiesInput()
        {
            var input = new bool[NetFaceGaps.Cells];
            input[4] = true;
            var source = new NetFaceGaps(input);
            input[4] = false;
            input[5] = true;
            Assert.That(At(source, 4), Is.True);
            Assert.That(At(source, 5), Is.False);
            Assert.That(source.Count, Is.EqualTo(1));
        }

        [Test]
        public void ClampedRangesRetainValuesAndCounts()
        {
            var source = Gaps(4, 50);
            var measured = Gaps(0, 95);
            var low = source.With(measured, -2, 3);
            Assert.That(low.Count, Is.EqualTo(3));
            Assert.That(At(low, 0), Is.True);
            Assert.That(At(low, 4), Is.True);
            Assert.That(At(low, 95), Is.False);
            var high = source.With(measured, 95, 8);
            Assert.That(high.Count, Is.EqualTo(3));
            Assert.That(At(high, 95), Is.True);
            Assert.That(At(high, 0), Is.False);
        }

        [Test]
        public void EmptyAndOutsideRangesKeepReference()
        {
            var source = Gaps(4);
            var measured = Gaps(0, 95);
            Assert.That(source.With(measured, 4, 0), Is.SameAs(source));
            Assert.That(source.With(measured, 4, -1), Is.SameAs(source));
            Assert.That(source.With(measured, -5, 2), Is.SameAs(source));
            Assert.That(source.With(measured, 96, 2), Is.SameAs(source));
            Assert.That(source.With(source, 0, NetFaceGaps.Cells), Is.SameAs(source));
        }
    }
}
