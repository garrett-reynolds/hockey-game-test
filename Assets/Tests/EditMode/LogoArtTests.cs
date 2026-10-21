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
    }
}
