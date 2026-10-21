using System;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The Core art maker (#226): one picked picture in, the colour board panel out, in the look of Tools/make_ads.py.</summary>
    public class LogoArtTests
    {
        public delegate (byte r, byte g, byte b, byte a) Pixel(int x, int y);

        public static RgbaPicture Make(int w, int h, Pixel pixel)
        {
            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = pixel(x, y);
                    int i = (y * w + x) * 4;
                    rgba[i] = p.r; rgba[i + 1] = p.g; rgba[i + 2] = p.b; rgba[i + 3] = p.a;
                }
            return new RgbaPicture(w, h, rgba);
        }

        /// <summary>A two-colour logo of logoW x logoH on a white box (opaque) of picW x picH; a white dot sits inside the logo.</summary>
        public static RgbaPicture LogoOnWhite(int picW, int picH, int logoW, int logoH)
        {
            int x0 = (picW - logoW) / 2, y0 = (picH - logoH) / 2;
            return Make(picW, picH, (x, y) =>
            {
                bool inside = x >= x0 && x < x0 + logoW && y >= y0 && y < y0 + logoH;
                if (!inside) return (255, 255, 255, 255);
                if (x == x0 + logoW / 2 && y == y0 + logoH / 2) return (255, 255, 255, 255); // a white dot inside the logo
                return x < x0 + logoW / 2 ? ((byte)200, (byte)20, (byte)30, (byte)255) : ((byte)20, (byte)40, (byte)200, (byte)255);
            });
        }

        static (int x0, int y0, int x1, int y1) VisibleBox(RgbaPicture p)
        {
            int x0 = p.Width, y0 = p.Height, x1 = -1, y1 = -1;
            for (int y = 0; y < p.Height; y++)
                for (int x = 0; x < p.Width; x++)
                    if (p.Rgba[(y * p.Width + x) * 4 + 3] > 8)
                    {
                        if (x < x0) x0 = x;
                        if (x > x1) x1 = x;
                        if (y < y0) y0 = y;
                        if (y > y1) y1 = y;
                    }
            return (x0, y0, x1, y1);
        }

        [Test]
        public void The_panel_is_768_by_256_which_is_3_to_1()
        {
            RgbaPicture panel = LogoArt.MakePanel(LogoArt.MakeLogo(LogoOnWhite(300, 200, 120, 50)));
            Assert.AreEqual(768, panel.Width);
            Assert.AreEqual(256, panel.Height);
            Assert.AreEqual(768 * 256 * 4, panel.Rgba.Length);
        }

        [Test]
        public void A_wide_logo_fits_84_percent_of_the_width_and_a_tall_one_70_percent_of_the_height()
        {
            var wide = VisibleBox(LogoArt.MakePanel(LogoArt.MakeLogo(LogoOnWhite(900, 200, 800, 100))));
            Assert.That(wide.x1 - wide.x0 + 1, Is.EqualTo(645).Within(3), "0.84 x 768 wide");
            Assert.LessOrEqual(wide.y1 - wide.y0 + 1, 180);

            var tall = VisibleBox(LogoArt.MakePanel(LogoArt.MakeLogo(LogoOnWhite(200, 900, 100, 800))));
            Assert.That(tall.y1 - tall.y0 + 1, Is.EqualTo(179).Within(3), "0.70 x 256 high");
            Assert.LessOrEqual(tall.x1 - tall.x0 + 1, 646);
        }

        [Test]
        public void The_logo_sits_in_the_middle_and_the_pixels_round_it_are_clear()
        {
            RgbaPicture panel = LogoArt.MakePanel(LogoArt.MakeLogo(LogoOnWhite(900, 200, 800, 100)));
            var box = VisibleBox(panel);
            Assert.That((box.x0 + box.x1) / 2.0, Is.EqualTo(384).Within(2));
            Assert.That((box.y0 + box.y1) / 2.0, Is.EqualTo(128).Within(2));
            foreach (var (x, y) in new[] { (0, 0), (767, 0), (0, 255), (767, 255), (10, 128), (757, 128) })
                Assert.AreEqual(0, panel.Rgba[(y * 768 + x) * 4 + 3], "clear at " + x + "," + y);
            int c = (128 * 768 + 30) * 4; // a clear pixel left of the logo still holds board white under it, as the art tool's panels do
            Assert.AreEqual(LogoArt.BoardWhiteR, panel.Rgba[c]);
            Assert.AreEqual(LogoArt.BoardWhiteG, panel.Rgba[c + 1]);
            Assert.AreEqual(LogoArt.BoardWhiteB, panel.Rgba[c + 2]);
        }

        [Test]
        public void A_white_box_round_the_logo_is_cleared_and_white_inside_the_logo_stays()
        {
            RgbaPicture logo = LogoArt.MakeLogo(LogoOnWhite(200, 120, 60, 30));
            Assert.AreEqual(60, logo.Width, "cropped to the logo");
            Assert.AreEqual(30, logo.Height);
            // A logo with a notch at its top left: the notch is white box joined to the edge, so it clears.
            RgbaPicture notched = LogoArt.MakeLogo(Make(100, 80, (x, y) =>
                x >= 20 && x < 80 && y >= 20 && y < 50 && !(x < 30 && y < 30) ? ((byte)200, (byte)20, (byte)30, (byte)255) : ((byte)255, (byte)255, (byte)255, (byte)255)));
            Assert.AreEqual(60, notched.Width);
            Assert.AreEqual(30, notched.Height);
            Assert.AreEqual(0, notched.Rgba[3], "the notch was white box: clear");
            Assert.AreEqual(255, notched.Rgba[(15 * 60 + 40) * 4 + 3], "the logo itself stays");
            int dot = (15 * 60 + 30) * 4;
            Assert.AreEqual(255, logo.Rgba[dot + 3], "the white dot inside the logo stays");
            Assert.AreEqual(255, logo.Rgba[dot]);
        }

        [Test]
        public void A_logo_on_a_clear_picture_is_cropped_and_a_one_colour_shape_is_a_logo()
        {
            RgbaPicture picked = LogoOnWhite(200, 120, 60, 30);
            for (int i = 0; i < picked.Width * picked.Height; i++)
                if (picked.Rgba[i * 4] == 255) picked.Rgba[i * 4 + 3] = 0; // clear where it was white
            Assert.IsNotNull(LogoArt.MakeLogo(picked));

            RgbaPicture shape = LogoArt.MakeLogo(LogoOnWhite(100, 100, 40, 40));
            RgbaPicture oneColourShape = LogoArt.MakeLogo(Make(100, 60, (x, y) => (x - 50) * (x - 50) + (y - 30) * (y - 30) < 400 ? ((byte)10, (byte)90, (byte)200, (byte)255) : ((byte)0, (byte)0, (byte)0, (byte)0)));
            Assert.IsNotNull(shape);
            Assert.IsNotNull(oneColourShape, "a flat one-colour logo on clear is a logo");
        }

        [Test]
        public void An_all_clear_picture_an_all_white_one_and_a_plain_one_colour_box_are_refused()
        {
            Assert.IsNull(LogoArt.MakeLogo(Make(50, 40, (x, y) => (10, 20, 30, 0))), "all clear");
            Assert.IsNull(LogoArt.MakeLogo(Make(50, 40, (x, y) => (255, 255, 255, 255))), "all white: the white box clears everything");
            Assert.IsNull(LogoArt.MakeLogo(Make(50, 40, (x, y) => (200, 30, 30, 255))), "one colour, no shape");
        }

        [Test]
        public void Shrink_brings_the_long_side_down_and_leaves_a_small_picture_alone()
        {
            RgbaPicture big = Make(3000, 300, (x, y) => (200, 30, 30, 255));
            RgbaPicture small = LogoArt.Shrink(big, 2048);
            Assert.AreEqual(2048, small.Width);
            Assert.That(small.Height, Is.EqualTo(205).Within(1));
            RgbaPicture fine = Make(100, 50, (x, y) => (1, 2, 3, 255));
            Assert.AreSame(fine, LogoArt.Shrink(fine, 2048));
        }

        // #227: the soft panel, the round ice logo, the Ribbon strip and the centre art.

        /// <summary>A w x h logo, 70% red on the left and blue on the right, no white box.</summary>
        static RgbaPicture RedBlue(int w, int h) =>
            Make(w, h, (x, y) => x < w * 7 / 10 ? ((byte)200, (byte)20, (byte)30, (byte)255) : ((byte)20, (byte)40, (byte)200, (byte)255));

        static (byte r, byte g, byte b, byte a) At(RgbaPicture p, int x, int y)
        {
            int i = (y * p.Width + x) * 4;
            return (p.Rgba[i], p.Rgba[i + 1], p.Rgba[i + 2], p.Rgba[i + 3]);
        }

        [Test]
        public void The_main_colour_is_the_commonest_strong_colour_else_the_commonest_dark_one()
        {
            Assert.AreEqual(((byte)200, (byte)24, (byte)24), LogoArt.MainColour(RedBlue(100, 50)));
            RgbaPicture dark = Make(60, 40, (x, y) => x < 40 ? ((byte)30, (byte)30, (byte)40, (byte)255) : ((byte)120, (byte)120, (byte)120, (byte)255));
            Assert.AreEqual(((byte)24, (byte)24, (byte)40), LogoArt.MainColour(dark));
            Assert.AreEqual(((byte)40, (byte)40, (byte)45), LogoArt.MainColour(Make(10, 10, (x, y) => (250, 250, 250, 255))), "nothing strong or dark: the tool's fallback");
        }

        [Test]
        public void The_soft_panel_is_768_by_256_opaque_and_has_no_colour()
        {
            RgbaPicture soft = LogoArt.MakeSoftPanel(LogoArt.MakeLogo(RedBlue(300, 100)));
            Assert.AreEqual(768, soft.Width);
            Assert.AreEqual(256, soft.Height);
            int most = 0;
            for (int i = 0; i < soft.Rgba.Length; i += 4)
            {
                Assert.AreEqual(255, soft.Rgba[i + 3], "opaque, as the tool's soft panels are");
                int spread = System.Math.Max(soft.Rgba[i], System.Math.Max(soft.Rgba[i + 1], soft.Rgba[i + 2])) - System.Math.Min(soft.Rgba[i], System.Math.Min(soft.Rgba[i + 1], soft.Rgba[i + 2]));
                if (spread > most) most = spread;
                Assert.That(soft.Rgba[i], Is.InRange(LogoArt.SoftGreyR, LogoArt.BoardWhiteR), "between the soft grey and board white");
            }
            Assert.LessOrEqual(most, 6, "the red and blue of the logo leave no colour");
        }

        [Test]
        public void The_soft_panel_has_the_logo_shape_in_soft_grey_on_board_white_with_soft_edges()
        {
            RgbaPicture soft = LogoArt.MakeSoftPanel(LogoArt.MakeLogo(RedBlue(900, 200)));
            Assert.AreEqual((LogoArt.SoftGreyR, LogoArt.SoftGreyG, LogoArt.SoftGreyB), (At(soft, 384, 128).r, At(soft, 384, 128).g, At(soft, 384, 128).b), "the logo");
            Assert.AreEqual((LogoArt.BoardWhiteR, LogoArt.BoardWhiteG, LogoArt.BoardWhiteB), (At(soft, 5, 5).r, At(soft, 5, 5).g, At(soft, 5, 5).b), "board white round it");
            int row = 128, edge = -1;
            for (int x = 0; x < 384; x++) if (At(soft, x, row).r <= (LogoArt.BoardWhiteR + LogoArt.SoftGreyR) / 2) { edge = x; break; } // halfway between white and grey
            Assert.That(edge, Is.EqualTo(61).Within(3), "the shape starts where the colour panel's logo starts (0.84 of 768 wide)");
            int soften = 0;
            for (int x = edge - 8; x < edge + 8; x++) { byte r = At(soft, x, row).r; if (r > LogoArt.SoftGreyR && r < LogoArt.BoardWhiteR) soften++; }
            Assert.GreaterOrEqual(soften, 2, "the edge is blurred, not a hard step");
        }

        [Test]
        public void The_round_logo_is_a_512_disc_clear_outside_with_a_ring_in_the_main_colour()
        {
            RgbaPicture logo = LogoArt.MakeLogo(RedBlue(200, 100));
            RgbaPicture round = LogoArt.MakeRound(logo);
            Assert.AreEqual(512, round.Width);
            Assert.AreEqual(512, round.Height);
            foreach (var (x, y) in new[] { (0, 0), (511, 0), (0, 511), (511, 511), (20, 20), (491, 20) })
                Assert.AreEqual(0, At(round, x, y).a, "clear outside the disc at " + x + "," + y);
            var main = LogoArt.MainColour(logo);
            var ring = At(round, 256, 21); // 0.46 of the width above the centre: in the ring (0.44 to 0.48)
            Assert.AreEqual((main.r, main.g, main.b, (byte)255), ring, "the ring is the logo's main colour");
            var inside = At(round, 256 + 150, 256 - 120);
            Assert.AreEqual(((byte)LogoArt.BoardWhiteR, (byte)LogoArt.BoardWhiteG, (byte)LogoArt.BoardWhiteB, (byte)255), inside, "board white inside the ring, round the logo");
        }

        [Test]
        public void The_round_logo_is_fitted_so_its_corners_stay_36_percent_of_the_disc_from_the_centre()
        {
            RgbaPicture round = LogoArt.MakeRound(LogoArt.MakeLogo(RedBlue(200, 100)));
            int x0 = 512, x1 = -1, y0 = 512, y1 = -1;
            for (int y = 0; y < 512; y++)
                for (int x = 0; x < 512; x++)
                {
                    if ((x - 256) * (x - 256) + (y - 256) * (y - 256) > 215 * 215) continue; // inside the ring
                    var p = At(round, x, y);
                    if (p.r == LogoArt.BoardWhiteR && p.g == LogoArt.BoardWhiteG && p.b == LogoArt.BoardWhiteB) continue;
                    if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
                }
            double k = 512 * 0.36 / System.Math.Sqrt(100 * 100 + 50 * 50);
            Assert.That(x1 - x0 + 1, Is.EqualTo(200 * k).Within(3), "width");
            Assert.That(y1 - y0 + 1, Is.EqualTo(100 * k).Within(3), "height");
            Assert.That((x0 + x1) / 2.0, Is.EqualTo(256).Within(2));
        }

        [Test]
        public void The_centre_art_is_the_round_look_at_1024()
        {
            RgbaPicture logo = LogoArt.MakeLogo(RedBlue(200, 100));
            RgbaPicture centre = LogoArt.MakeCentre(logo);
            Assert.AreEqual(1024, centre.Width);
            Assert.AreEqual(1024, centre.Height);
            Assert.AreEqual(0, At(centre, 0, 0).a, "clear outside the disc");
            var main = LogoArt.MainColour(logo);
            Assert.AreEqual((main.r, main.g, main.b, (byte)255), At(centre, 512, 512 - 470), "the ring (0.46 of the width out) is the main colour");
        }

        [Test]
        public void The_ribbon_strip_is_24_high_black_with_the_logo_18_high_and_33_and_34_px_margins()
        {
            RgbaPicture strip = LogoArt.MakeRibbon(LogoArt.MakeLogo(RedBlue(200, 100)));
            Assert.AreEqual(24, strip.Height);
            Assert.AreEqual(33 + 36 + 34, strip.Width, "the logo is 18 high, so 36 wide");
            for (int i = 3; i < strip.Rgba.Length; i += 4) Assert.AreEqual(255, strip.Rgba[i], "opaque");
            Assert.AreEqual(((byte)0, (byte)0, (byte)0, (byte)255), At(strip, 5, 12), "black margin");
            Assert.AreEqual(((byte)0, (byte)0, (byte)0, (byte)255), At(strip, 33 + 36 + 20, 12), "black margin");
            Assert.AreEqual(((byte)0, (byte)0, (byte)0, (byte)255), At(strip, 50, 1), "black above the logo");
            Assert.AreEqual(((byte)200, (byte)20, (byte)30, (byte)255), At(strip, 40, 12), "the logo");
            Assert.AreEqual(((byte)20, (byte)40, (byte)200, (byte)255), At(strip, 33 + 34, 12), "the logo");
            Assert.AreEqual(((byte)200, (byte)20, (byte)30, (byte)255), At(strip, 33, 3), "the logo's top left corner");
            Assert.AreEqual(((byte)0, (byte)0, (byte)0, (byte)255), At(strip, 32, 3));
            Assert.AreEqual(((byte)0, (byte)0, (byte)0, (byte)255), At(strip, 33, 21), "the logo ends at row 20");
        }

        [Test]
        public void A_very_wide_logo_on_the_ribbon_is_held_to_216_px_and_shortened_to_keep_its_shape()
        {
            RgbaPicture strip = LogoArt.MakeRibbon(LogoArt.MakeLogo(RedBlue(3000, 100)));
            Assert.AreEqual(33 + 216 + 34, strip.Width);
            Assert.AreEqual(((byte)0, (byte)0, (byte)0, (byte)255), At(strip, 100, 2), "black above the shortened logo");
            Assert.AreEqual(((byte)200, (byte)20, (byte)30, (byte)255), At(strip, 100, 12), "the logo");
        }

        // #224: the CENTRE ICE row's picture is used as picked: cropped, fitted in a square, clear round it; no disc, no ring, no white-box clear.
        [Test]
        public void Centre_ice_as_picked_is_cropped_and_fitted_in_a_clear_square_with_its_white_kept()
        {
            RgbaPicture picked = Make(200, 100, (x, y) =>
                x >= 50 && x < 150 && y >= 25 && y < 75 ? ((byte)255, (byte)255, (byte)255, (byte)255) : ((byte)0, (byte)0, (byte)0, (byte)0));
            RgbaPicture art = LogoArt.MakeCentrePicked(picked);
            Assert.AreEqual(LogoArt.CentreSize, art.Width);
            Assert.AreEqual(LogoArt.CentreSize, art.Height);
            Assert.AreEqual(0, At(art, 2, 2).a, "clear in the corner");
            Assert.AreEqual(0, At(art, 512, 20).a, "clear above the picture, no disc");
            Assert.AreEqual(((byte)255, (byte)255, (byte)255, (byte)255), At(art, 512, 512), "the white box is not cleared");
            Assert.AreEqual(((byte)255, (byte)255, (byte)255, (byte)255), At(art, 512, 512 - 220), "no ring near the top edge of the picture");
            (int x0, int y0, int x1, int y1) = VisibleBox(art);
            Assert.That(x1 - x0 + 1, Is.InRange(900, 940), "the cropped picture fills about 90% of the width");
            Assert.That(Math.Abs((x0 + x1) / 2 - 512), Is.LessThanOrEqualTo(2), "centred across");
            Assert.That(Math.Abs((y0 + y1) / 2 - 512), Is.LessThanOrEqualTo(2), "centred down");
        }

        [Test]
        public void Centre_ice_as_picked_of_an_all_clear_picture_is_null()
        {
            Assert.IsNull(LogoArt.MakeCentrePicked(Make(10, 10, (x, y) => ((byte)0, (byte)0, (byte)0, (byte)0))));
        }

        [Test]
        public void A_logo_with_clear_parts_shows_board_white_in_them_on_the_ribbon_and_the_round_logo()
        {
            RgbaPicture ring = LogoArt.MakeLogo(Make(100, 100, (x, y) =>
                (x - 50) * (x - 50) + (y - 50) * (y - 50) < 2500 && (x - 50) * (x - 50) + (y - 50) * (y - 50) > 900 ? ((byte)10, (byte)90, (byte)200, (byte)255) : ((byte)0, (byte)0, (byte)0, (byte)0)));
            RgbaPicture strip = LogoArt.MakeRibbon(ring);
            Assert.AreEqual(((byte)LogoArt.BoardWhiteR, (byte)LogoArt.BoardWhiteG, (byte)LogoArt.BoardWhiteB, (byte)255), At(strip, 33 + 9, 12), "the hole of the ring: a board white plate, so a dark logo shows on the black strip");
        }
    }
}
