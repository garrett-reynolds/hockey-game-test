using System.Collections.Generic;
using HockeyGame.Editor;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The picture test's math (#85): the Calm Area measures over a pixel buffer and a mask, and the fail rule (any measure
    /// more than 10% worse than the baseline). The rendering is checked by running the tool (PictureTestTool), not here.
    /// </summary>
    public class PictureTestTests
    {
        const int W = 8, H = 8;

        static float[] Flat(float value)
        {
            var luma = new float[W * H];
            for (int i = 0; i < luma.Length; i++) luma[i] = value;
            return luma;
        }

        static bool[] All(bool value = true)
        {
            var mask = new bool[W * H];
            for (int i = 0; i < mask.Length; i++) mask[i] = value;
            return mask;
        }

        /// <summary>Black on the left half (x 0 to 3), white on the right half (x 4 to 7).</summary>
        static float[] HalfAndHalf()
        {
            var luma = new float[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                luma[y * W + x] = x < W / 2 ? 0f : 1f;
            return luma;
        }

        // ---- Measures ----

        [Test]
        public void Luma_is_0_for_black_1_for_white_and_weights_green_most()
        {
            float[] luma = PictureMeasures.Luma(new[]
            {
                new Color32(0, 0, 0, 255), new Color32(255, 255, 255, 255), new Color32(0, 255, 0, 255), new Color32(0, 0, 255, 255)
            });
            Assert.AreEqual(0f, luma[0], 1e-6f);
            Assert.AreEqual(1f, luma[1], 1e-5f);
            Assert.AreEqual(0.7152f, luma[2], 1e-4f);
            Assert.AreEqual(0.0722f, luma[3], 1e-4f);
        }

        [Test]
        public void Contrast_is_0_on_a_flat_area_and_the_spread_of_brightness_otherwise()
        {
            Assert.AreEqual(0.0, PictureMeasures.Contrast(Flat(0.4f), All()), 1e-9);
            Assert.AreEqual(0.5, PictureMeasures.Contrast(HalfAndHalf(), All()), 1e-9, "half black, half white");
        }

        [Test]
        public void Contrast_counts_only_pixels_in_the_mask()
        {
            bool[] leftHalf = All(false);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W / 2; x++)
                leftHalf[y * W + x] = true;
            Assert.AreEqual(0.0, PictureMeasures.Contrast(HalfAndHalf(), leftHalf), 1e-9);
            Assert.AreEqual(0.0, PictureMeasures.Contrast(HalfAndHalf(), All(false)), 1e-9, "an empty mask has no contrast");
        }

        [Test]
        public void Edges_are_0_on_a_flat_area()
        {
            Assert.AreEqual(0.0, PictureMeasures.Edges(Flat(0.7f), All(), W, H), 1e-9);
        }

        [Test]
        public void Edges_are_the_share_of_mask_pixels_on_an_edge()
        {
            // Pixels with all 8 neighbours in the mask: x and y 1 to 6, 36 of them. Those on the step: x = 3 and x = 4, 12 of them.
            Assert.AreEqual(12.0 / 36.0, PictureMeasures.Edges(HalfAndHalf(), All(), W, H), 1e-9);
        }

        [Test]
        public void Edges_ignore_the_mask_border_so_a_cut_out_shape_adds_none()
        {
            // The step lies on the mask's edge (mask: x 0 to 3): every pixel next to it has a neighbour outside the mask.
            bool[] leftHalf = All(false);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W / 2; x++)
                leftHalf[y * W + x] = true;
            Assert.AreEqual(0.0, PictureMeasures.Edges(HalfAndHalf(), leftHalf, W, H), 1e-9);
        }

        [Test]
        public void A_faint_step_is_not_an_edge()
        {
            float[] luma = HalfAndHalf();
            for (int i = 0; i < luma.Length; i++) luma[i] = 0.5f + luma[i] * 0.02f; // a 2% step
            Assert.AreEqual(0.0, PictureMeasures.Edges(luma, All(), W, H), 1e-9);
        }

        [Test]
        public void Gap_stand_out_is_the_brightness_difference_between_Gap_and_gear_pixels()
        {
            float[] luma = HalfAndHalf();
            bool[] gaps = All(false), gear = All(false);
            gaps[0] = gaps[1] = true;              // black
            gear[5] = gear[6] = gear[W + 7] = true; // white
            gear[W + 1] = true;                     // and one black: gear mean 0.75
            Assert.AreEqual(0.75, PictureMeasures.GapStandOut(luma, gaps, gear), 1e-9);
            Assert.AreEqual(0.75, PictureMeasures.GapStandOut(luma, gear, gaps), 1e-9, "brighter or darker, the size counts");
        }

        [Test]
        public void Gap_stand_out_is_not_a_number_without_Gap_or_gear_pixels()
        {
            bool[] some = All(false);
            some[0] = true;
            Assert.IsNaN(PictureMeasures.GapStandOut(Flat(0.5f), All(false), some));
            Assert.IsNaN(PictureMeasures.GapStandOut(Flat(0.5f), some, All(false)));
        }

        [Test]
        public void A_polygon_covers_the_pixels_whose_centres_lie_inside_it_in_any_point_order()
        {
            // A square from (2, 2) to (6, 5): pixel centres x 2.5 to 5.5, y 2.5 to 4.5: 4 x 3 pixels. Inside points do not matter.
            var points = new List<Vector2> { new Vector2(6, 5), new Vector2(2, 2), new Vector2(4, 3), new Vector2(2, 5), new Vector2(6, 2) };
            bool[] mask = PictureMeasures.Polygon(points, W, H);
            int count = 0;
            for (int i = 0; i < mask.Length; i++) if (mask[i]) count++;
            Assert.AreEqual(12, count);
            Assert.IsTrue(mask[2 * W + 2]);
            Assert.IsTrue(mask[4 * W + 5]);
            Assert.IsFalse(mask[5 * W + 5]);
            Assert.IsFalse(mask[2 * W + 6]);
        }

        [Test]
        public void A_polygon_running_off_the_picture_is_cut_at_its_edge()
        {
            var points = new List<Vector2> { new Vector2(-10, -10), new Vector2(100, -10), new Vector2(100, 100), new Vector2(-10, 100) };
            bool[] mask = PictureMeasures.Polygon(points, W, H);
            foreach (bool inside in mask) Assert.IsTrue(inside);
        }

        [Test]
        public void Changed_share_is_the_share_of_pixels_that_differ()
        {
            var a = new[] { new Color32(1, 2, 3, 255), new Color32(1, 2, 3, 255), new Color32(1, 2, 3, 255), new Color32(1, 2, 3, 255) };
            var b = (Color32[])a.Clone();
            Assert.AreEqual(0.0, PictureMeasures.ChangedShare(a, b), 1e-9);
            b[2] = new Color32(1, 2, 4, 255);
            Assert.AreEqual(0.25, PictureMeasures.ChangedShare(a, b), 1e-9);
        }

        [Test]
        public void Differs_marks_every_pixel_that_is_not_the_clear_colour()
        {
            var clear = new Color32(255, 0, 255, 255);
            bool[] mask = PictureMeasures.Differs(new[] { clear, new Color32(254, 0, 255, 255), clear }, clear);
            CollectionAssert.AreEqual(new[] { false, true, false }, mask);
        }

        [Test]
        public void Inner_pixels_are_the_mask_pixels_whose_8_neighbours_are_in_the_mask_too()
        {
            Assert.AreEqual(36, PictureMeasures.InnerPixels(All(), W, H), "x and y 1 to 6");
            Assert.AreEqual(0, PictureMeasures.InnerPixels(All(false), W, H));
        }

        [Test]
        public void Each_Gap_is_a_group_of_touching_Gap_cells_named_by_its_first_cell()
        {
            // 4 columns x 3 rows, row 0 at the bottom. Cells (0,0) (1,0) (1,1) touch; (3,0) is alone; (3,2) is alone.
            var cells = new bool[12];
            cells[0] = cells[1] = cells[4 + 1] = true;
            cells[3] = true;
            cells[8 + 3] = true;
            List<List<int>> gaps = PictureMeasures.Groups(cells, 4, 3);
            Assert.AreEqual(3, gaps.Count);
            CollectionAssert.AreEqual(new[] { 0, 1, 5 }, gaps[0]);
            CollectionAssert.AreEqual(new[] { 3 }, gaps[1]);
            CollectionAssert.AreEqual(new[] { 11 }, gaps[2]);
            Assert.AreEqual("Gap c0r0", PictureMeasures.GapName(gaps[0], 4));
            Assert.AreEqual("Gap c3r2", PictureMeasures.GapName(gaps[2], 4));
        }

        [Test]
        public void Cells_touching_only_at_a_corner_are_two_Gaps()
        {
            var cells = new bool[4]; // 2 x 2
            cells[0] = cells[3] = true;
            Assert.AreEqual(2, PictureMeasures.Groups(cells, 2, 2).Count);
        }

        // ---- The fail rule ----

        static bool Fails(double baseline, double now, PictureCompare.Worse worse) => PictureCompare.Fails(baseline, now, worse);

        [Test]
        public void Exactly_10_percent_worse_passes_and_more_fails()
        {
            Assert.AreEqual(10.0, PictureCompare.WorsePercent(1.0, 1.1, PictureCompare.Worse.Higher), 1e-9);
            Assert.IsFalse(Fails(1.0, 1.1, PictureCompare.Worse.Higher), "exactly 10% worse passes");
            Assert.IsTrue(Fails(1.0, 1.1001, PictureCompare.Worse.Higher));

            Assert.AreEqual(10.0, PictureCompare.WorsePercent(0.5, 0.45, PictureCompare.Worse.Lower), 1e-9);
            Assert.IsFalse(Fails(0.5, 0.45, PictureCompare.Worse.Lower));
            Assert.IsTrue(Fails(0.5, 0.4499, PictureCompare.Worse.Lower));
        }

        [Test]
        public void Better_than_the_baseline_passes_however_far()
        {
            Assert.AreEqual(-80.0, PictureCompare.WorsePercent(1.0, 0.2, PictureCompare.Worse.Higher), 1e-9);
            Assert.IsFalse(Fails(1.0, 0.2, PictureCompare.Worse.Higher));
            Assert.IsFalse(Fails(0.5, 2.0, PictureCompare.Worse.Lower));
        }

        [Test]
        public void From_a_baseline_of_0_any_worse_change_fails_and_the_percent_stays_a_number()
        {
            Assert.AreEqual(0.0, PictureCompare.WorsePercent(0, 0.01, PictureCompare.Worse.Higher), "no percent of 0: never infinity");
            Assert.IsFalse(Fails(0, 0, PictureCompare.Worse.Higher));
            Assert.IsTrue(Fails(0, 0.01, PictureCompare.Worse.Higher));
            Assert.IsFalse(Fails(0, 0.01, PictureCompare.Worse.Lower));

            PictureTestResult result = PictureCompare.Compare(Set(View("a", edges: 0)), Set(View("a", edges: 0.01)));
            Assert.IsFalse(result.Pass);
            MeasureCheck failed = result.Checks.Find(c => c.Fails);
            Assert.AreEqual(PictureCompare.EdgesName, failed.Measure);
            Assert.IsFalse(failed.PercentApplies);
            foreach (MeasureCheck check in result.Checks)
            {
                Assert.IsFalse(double.IsInfinity(check.ChangePercent) || double.IsNaN(check.ChangePercent));
                Assert.IsFalse(double.IsInfinity(check.WorsePercent) || double.IsNaN(check.WorsePercent));
            }
        }

        [Test]
        public void A_measure_that_is_not_a_number_fails_as_a_problem_and_is_not_written_as_a_check()
        {
            PictureTestResult result = PictureCompare.Compare(Set(View("a")), Set(View("a", contrast: double.NaN)));
            Assert.IsFalse(result.Pass);
            StringAssert.Contains(PictureCompare.ContrastName, result.Problems[0]);
            Assert.IsFalse(result.Checks.Exists(c => c.Measure == PictureCompare.ContrastName));

            PictureTestResult infinite = PictureCompare.Compare(Set(View("a", standOut: double.PositiveInfinity)), Set(View("a")));
            Assert.IsFalse(infinite.Pass);
        }

        static PictureMeasureSet Set(params ViewMeasures[] views)
        {
            var set = new PictureMeasureSet { Width = 1280, Height = 720 };
            set.Views.AddRange(views);
            return set;
        }

        /// <summary>A view with two Gaps.</summary>
        static ViewMeasures View(string name, double contrast = 0.2, double edges = 0.05, double standOut = 0.3, double secondStandOut = 0.3)
        {
            var view = new ViewMeasures { View = name, Contrast = contrast, Edges = edges };
            view.Gaps.Add(new GapMeasure { Gap = "Gap c1r1", Cells = 3, StandOut = standOut });
            view.Gaps.Add(new GapMeasure { Gap = "Gap c7r4", Cells = 2, StandOut = secondStandOut });
            return view;
        }

        [Test]
        public void An_unchanged_rink_passes_with_0_percent_change_on_every_measure()
        {
            PictureTestResult result = PictureCompare.Compare(Set(View("a"), View("b")), Set(View("a"), View("b")));
            Assert.IsTrue(result.Pass);
            Assert.AreEqual(8, result.Checks.Count, "contrast, edges and 2 Gaps for each of 2 views");
            foreach (MeasureCheck check in result.Checks)
            {
                Assert.AreEqual(0.0, check.ChangePercent, 0.0);
                Assert.IsFalse(check.Fails);
            }
            CollectionAssert.IsEmpty(result.Problems);
        }

        [Test]
        public void One_measure_more_than_10_percent_worse_fails_the_test_and_names_it()
        {
            PictureTestResult result = PictureCompare.Compare(Set(View("a"), View("b")), Set(View("a"), View("b", edges: 0.0556)));
            Assert.IsFalse(result.Pass);
            MeasureCheck failed = result.Checks.Find(c => c.Fails);
            Assert.AreEqual("b", failed.View);
            Assert.AreEqual(PictureCompare.EdgesName, failed.Measure);
            Assert.AreEqual(11.2, failed.WorsePercent, 1e-6);
            Assert.AreEqual(1, result.Checks.FindAll(c => c.Fails).Count);
        }

        [Test]
        public void Less_Gap_stand_out_is_worse()
        {
            PictureTestResult result = PictureCompare.Compare(Set(View("a")), Set(View("a", standOut: 0.26)));
            Assert.IsFalse(result.Pass);
            MeasureCheck failed = result.Checks.Find(c => c.Fails);
            Assert.AreEqual(PictureCompare.GapStandOutName + " Gap c1r1", failed.Measure);
            Assert.AreEqual(-13.333333, failed.ChangePercent, 1e-5);
            Assert.AreEqual(13.333333, failed.WorsePercent, 1e-5);
        }

        [Test]
        public void Each_Gap_is_checked_so_one_fading_Gap_fails_even_when_the_average_holds()
        {
            // Mean stand-out 0.3 -> 0.305: better on average, but the second Gap is 16.7% worse.
            PictureTestResult result = PictureCompare.Compare(Set(View("a")), Set(View("a", standOut: 0.36, secondStandOut: 0.25)));
            Assert.IsFalse(result.Pass);
            MeasureCheck failed = result.Checks.Find(c => c.Fails);
            Assert.AreEqual(PictureCompare.GapStandOutName + " Gap c7r4", failed.Measure);
            Assert.AreEqual(1, result.Checks.FindAll(c => c.Fails).Count);
        }

        [Test]
        public void A_Gap_missing_on_either_side_fails()
        {
            ViewMeasures oneGap = View("a");
            oneGap.Gaps.RemoveAt(1);
            PictureTestResult gone = PictureCompare.Compare(Set(View("a")), Set(oneGap));
            Assert.IsFalse(gone.Pass);
            StringAssert.Contains("Gap c7r4", gone.Problems[0]);

            PictureTestResult noBaseline = PictureCompare.Compare(Set(oneGap), Set(View("a")));
            Assert.IsFalse(noBaseline.Pass);
            StringAssert.Contains("Gap c7r4", noBaseline.Problems[0]);
        }

        [Test]
        public void A_view_missing_on_either_side_fails()
        {
            PictureTestResult noBaseline = PictureCompare.Compare(Set(View("a")), Set(View("a"), View("new")));
            Assert.IsFalse(noBaseline.Pass);
            StringAssert.Contains("new", noBaseline.Problems[0]);

            PictureTestResult notRendered = PictureCompare.Compare(Set(View("a"), View("gone")), Set(View("a")));
            Assert.IsFalse(notRendered.Pass);
            StringAssert.Contains("gone", notRendered.Problems[0]);
        }

        [Test]
        public void A_different_picture_size_fails()
        {
            PictureMeasureSet now = Set(View("a"));
            now.Width = 640;
            PictureTestResult result = PictureCompare.Compare(Set(View("a")), now);
            Assert.IsFalse(result.Pass);
            CollectionAssert.IsNotEmpty(result.Problems);
        }

        [Test]
        public void A_problem_added_after_comparing_fails_the_result()
        {
            PictureTestResult result = PictureCompare.Compare(Set(View("a")), Set(View("a")));
            Assert.IsTrue(result.Pass);
            PictureCompare.AddProblem(result, "Baseline picture a.png is missing.");
            Assert.IsFalse(result.Pass);
        }
    }
}
