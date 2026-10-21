using System.Linq;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class RibbonBoardsTests
    {
        GameObject _rink;

        [SetUp]
        public void MakeRink() => _rink = new GameObject("Rink");

        [TearDown]
        public void RemoveRink() => RinkTeardown.Destroy(_rink);

        static ArenaState At(RoundPhase phase, ArenaResult result = ArenaResult.None) =>
            ArenaShow.At(new ArenaShowInput { Phase = phase, PhaseSeconds = 1.0, Result = result, ShotNumber = 1 });

        [Test]
        public void Removing_the_Ribbon_Boards_frees_the_materials_they_made()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            var made = new System.Collections.Generic.HashSet<Material>();
            var textures = new System.Collections.Generic.List<Texture2D>();
            foreach (Renderer r in ribbons.GetComponentsInChildren<Renderer>()) made.Add(r.sharedMaterial);
            for (int i = 0; i < ribbons.PanelCount; i++) textures.Add(ribbons.WordsTextureOn(i));
            Assert.That(made.Count, Is.GreaterThan(30), "one LED material per panel");
            Assert.AreEqual(ribbons.PanelCount, textures.Count);
            foreach (Texture2D t in textures) Assert.IsTrue(t != null, "each panel has its own words texture");

            Object.DestroyImmediate(ribbons.gameObject);

            foreach (Material m in made) Assert.IsTrue(m == null, m + " freed");
            foreach (Texture2D t in textures) Assert.IsTrue(t == null, "a panel's words texture freed");
        }

        static Renderer PanelRenderer(RibbonBoards ribbons, int panel) => ribbons.transform.Find("Ribbon Panel " + panel).GetComponent<Renderer>();

        /// <summary>The states the Ribbon Boards show: ads, GOAL!, SAVE and the Calm Area's still (the shooting window).</summary>
        static (string Name, ArenaState State)[] States() => new[]
        {
            ("ads", At(RoundPhase.Idle)),
            ("GOAL!", At(RoundPhase.ResultHold, ArenaResult.Goal)),
            ("SAVE", At(RoundPhase.ResultHold, ArenaResult.Save)),
            ("Calm still", At(RoundPhase.ShootingWindow)),
        };

        [Test]
        public void Every_Ribbon_Board_panel_uses_the_LED_shader_with_the_Ribbon_Board_cell_size()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            foreach ((string name, ArenaState state) in States())
            {
                ribbons.Show(state, 0.0);
                for (int i = 0; i < ribbons.PanelCount; i++)
                {
                    Material m = PanelRenderer(ribbons, i).sharedMaterial;
                    Assert.AreEqual(LedBoardMaterial.ShaderName, m.shader.name, name + ": panel " + i);
                    Assert.AreEqual((float)RinkLayout.RibbonBoardLedCell, LedBoardMaterial.CellOf(m), name + ": panel " + i + " cell size");
                }
                Assert.AreEqual(0, ribbons.GetComponentsInChildren<TextMesh>(true).Length, name + ": no drawn text on a Ribbon Board");
            }
        }

        [Test]
        public void The_Ribbon_Board_LED_grid_is_anchored_at_each_panels_lower_left_corner()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            ribbons.Show(At(RoundPhase.ResultHold, ArenaResult.Goal), 0.0); // the text changed after Build
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < ribbons.PanelCount; i++)
            {
                Renderer r = PanelRenderer(ribbons, i);
                r.GetPropertyBlock(block);
                Vector3 corner = ribbons.CentreOf(i) - r.transform.right * ribbons.LengthOf(i) / 2f - Vector3.up * RibbonBoards.Height / 2f;
                Vector3 origin = block.GetVector("_GridOrigin");
                Assert.AreEqual(0f, Vector3.Distance(corner, origin), 1e-3f, "panel " + i + " grid origin " + origin + " vs corner " + corner);
            }
        }

        [Test]
        public void Ribbon_Board_panels_are_whole_cells_tall_and_their_words_texture_has_one_texel_per_whole_cell()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            double cells = RibbonBoards.Height / RinkLayout.RibbonBoardLedCell;
            Assert.AreEqual(System.Math.Round(cells), cells, 1e-6, "the panel height is a whole number of cells");
            for (int i = 0; i < ribbons.PanelCount; i++)
            {
                Texture2D t = ribbons.WordsTextureOn(i);
                Assert.AreEqual(Mathf.FloorToInt(ribbons.LengthOf(i) / (float)RinkLayout.RibbonBoardLedCell), t.width, "panel " + i + " texels across");
                Assert.AreEqual(ribbons.CellsAcross(i), t.width, "panel " + i);
                Assert.AreEqual((int)System.Math.Round(cells), t.height, "panel " + i + " texels up");
                Assert.AreEqual(FilterMode.Point, t.filterMode);
            }
        }

        [Test]
        public void Ribbon_Board_words_are_the_LED_font_cell_for_cell()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            var failures = new System.Collections.Generic.List<string>();
            var table = new System.Collections.Generic.SortedDictionary<string, string>();
            void Check(string message, string requested)
            {
                for (int i = 0; i < ribbons.PanelCount; i++)
                {
                    if (ribbons.TextOn(i) != requested) continue;
                    LedWords.Fitted fit = ribbons.FitOn(i);
                    int cx = ribbons.CellsAcross(i), cy = ribbons.WordsTextureOn(i).height;
                    string who = message + ", panel " + i + " (" + cx + " cells), scale " + fit.Scale + ", clipped " + (fit.Clipped ? "yes" : "no");
                    var expected = new bool[cx * cy];
                    LedWords.Plot(expected, cx, cy, fit);
                    Unity.Collections.NativeArray<byte> texels = ribbons.WordsTextureOn(i).GetRawTextureData<byte>();
                    int wrong = 0, lit = 0, margin = 0;
                    for (int c = 0; c < expected.Length; c++)
                    {
                        bool on = texels[c] != 0;
                        if (on != expected[c]) wrong++;
                        if (on) { lit++; int x = c % cx; if (x < LedWords.SideMargin || x >= cx - LedWords.SideMargin) margin++; }
                    }
                    if (wrong > 0) failures.Add(who + ": " + wrong + " cells differ from the font table");
                    if (margin > 0) failures.Add(who + ": " + margin + " lit cells in the side margin");
                    if (fit.Scale < 1) failures.Add(who + ": scale below 1");
                    // GOAL! is at least scale 2 wherever the panel is wide enough for it (a bench cut is only 48 cells: too narrow, scale 1).
                    bool holdsTwo = cx >= LedFont.LineWidth("GOAL!") * 2 + 2 * LedWords.SideMargin;
                    if (message == "GOAL!" && holdsTwo && fit.Scale < 2) failures.Add(who + ": GOAL! below scale 2");
                    if (!requested.StartsWith(fit.Shown) || (!fit.Clipped && fit.Shown != requested)) failures.Add(who + ": kept '" + fit.Shown + "' is not whole letters of '" + requested + "'");
                    if (fit.Second != "" && LedFont.LineWidth(fit.Second) * fit.Scale > cx - 2 * LedWords.SideMargin) failures.Add(who + ": second line too wide");
                    if (fit.Clipped && fit.Text.Length > 0 && LedFont.LineWidth(fit.Text) * fit.Scale > cx - 2 * LedWords.SideMargin) failures.Add(who + ": clipped line still too wide");
                    if (lit == 0) failures.Add(who + ": nothing lit");
                    if (message.StartsWith("OFF THE") && fit.Clipped) failures.Add(who + ": the result words are cut");
                    if (message.StartsWith("ad ") && cx >= 73 && fit.Clipped) failures.Add(who + ": an ad is cut on a 73-cell panel");
                    string key = message + " / scale " + fit.Scale + (fit.Clipped ? " clipped to '" + fit.Text + "'" : "");
                    table[key] = (table.TryGetValue(key, out string s) ? s + "," : "") + i;
                }
            }

            ribbons.Show(At(RoundPhase.ResultHold, ArenaResult.Goal), 0.0);
            Check("GOAL!", "GOAL!");
            ribbons.Show(At(RoundPhase.ResultHold, ArenaResult.Save), 0.0);
            Check("SAVE", "SAVE");
            ribbons.Show(At(RoundPhase.ResultHold, ArenaResult.FrameHitPost), 0.0);
            Check("OFF THE POST!", "OFF THE POST!");
            ribbons.Show(At(RoundPhase.ResultHold, ArenaResult.FrameHitBar), 0.0);
            Check("OFF THE BAR!", "OFF THE BAR!");
            TestContext.Out.WriteLine("RIBBON SCALE TABLE (message / scale: panels)\n" + string.Join("\n", System.Linq.Enumerable.Select(table, kv => kv.Key + ": " + kv.Value)));
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void Every_Ribbon_ad_panel_shows_the_Rounds_brand_picture_and_a_new_Round_switches_it()
        {
            Brands.Brand before = Brands.Current;
            try
            {
                RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
                foreach (Brands.Brand brand in new[] { Brands.All[2], Brands.All[3] })
                {
                    Brands.Use(brand);
                    ribbons.Show(At(RoundPhase.Idle), 0.0);
                    for (int i = 0; i < ribbons.PanelCount; i++)
                    {
                        Assert.AreEqual(brand.AdWords, ribbons.TextOn(i), "panel " + i + " shows the Round's brand");
                        Texture shown = PanelRenderer(ribbons, i).sharedMaterial.mainTexture;
                        Assert.IsNotNull(shown, "panel " + i);
                        Assert.AreSame(RibbonBoards.PictureOf(brand), shown, "panel " + i + " shows the brand's ribbon picture");
                        StringAssert.Contains(brand.Id, shown.name);
                    }
                }
                // Words still come back after an ad (the result words).
                ribbons.Show(At(RoundPhase.ResultHold, ArenaResult.Goal), 0.0);
                Assert.AreEqual("GOAL!", ribbons.TextOn(0));
                Assert.AreSame(ribbons.WordsTextureOn(0), PanelRenderer(ribbons, 0).sharedMaterial.mainTexture);
            }
            finally { Brands.Use(before); }
        }

        [Test]
        public void The_Ribbon_ad_scrolls_left_all_the_time_in_an_endless_seamless_loop()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            Assert.That(RibbonBoards.ScrollMetresPerSecond, Is.InRange(0.5f, 3f), "a moderate, readable speed");
            ribbons.Show(At(RoundPhase.Idle), 10.0);
            Vector4[] a = Enumerable.Range(0, ribbons.PanelCount).Select(ribbons.PictureSTOn).ToArray();
            ribbons.Show(At(RoundPhase.Idle), 10.5);
            Vector4[] b = Enumerable.Range(0, ribbons.PanelCount).Select(ribbons.PictureSTOn).ToArray();
            double loop = ribbons.LoopMetres;
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].x, b[i].x, 1e-6f, "panel " + i + ": the same tiling");
                // Moving left: the picture's offset grows by speed x time / loop length (mod 1).
                double step = (b[i].z - a[i].z + 1.0) % 1.0;
                Assert.AreEqual(RibbonBoards.ScrollMetresPerSecond * 0.5 / loop, step, 1e-4, "panel " + i + " moved on");
            }
            // Seamless: the ring is a whole number of loops, and each panel starts where the one before it ended.
            double ring = ribbons.RingMetres;
            Assert.AreEqual(System.Math.Round(ring / loop), ring / loop, 1e-6, "a whole number of loops round the ring");
            int joins = 0;
            for (int i = 0; i < a.Length; i++)
            {
                int right = ribbons.RightNeighbourOf(i);
                if (right < 0) continue;
                joins++;
                double end = (a[i].z + a[i].x + 1.0) % 1.0, start = (a[right].z + 1.0) % 1.0;
                double gap = System.Math.Min(System.Math.Abs(end - start), 1.0 - System.Math.Abs(end - start));
                Assert.Less(gap, 1e-4, "panel " + i + " runs on into panel " + right);
                // The right-hand neighbour is to the viewer's right: along the panel's own right.
                Vector3 toRight = ribbons.CentreOf(right) - ribbons.CentreOf(i);
                Assert.Greater(Vector3.Dot(toRight, PanelRenderer(ribbons, i).transform.right), 0f, "panel " + right + " is to the right of " + i);
            }
            Assert.That(joins, Is.GreaterThanOrEqualTo(a.Length - 2), "every panel but the ones by the Video Board gap runs on into the next");
            Assert.IsTrue(PanelRenderer(ribbons, 0).sharedMaterial.GetFloat("_Wrap") > 0.5f, "the picture wraps round");
        }

        [Test]
        public void Ribbon_Board_panels_draw_as_opaque_depth_tested_front_face_only_screens()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            for (int i = 0; i < ribbons.PanelCount; i++)
            {
                Material m = PanelRenderer(ribbons, i).sharedMaterial;
                Assert.IsTrue(m.shader.isSupported, "panel " + i + " shader works");
                BoardWordsAssert.AssertSolid(m, "panel " + i, (int)UnityEngine.Rendering.RenderQueue.Geometry);
            }
        }

        [Test]
        public void A_static_frame_uploads_nothing_and_a_text_change_uploads_once_per_changed_panel()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            ArenaState ads = At(RoundPhase.Idle);
            ribbons.Show(ads, 0.0);
            int before = ribbons.WordsUploads;
            for (int frame = 1; frame <= 5; frame++) ribbons.Show(ads, 0.0 + frame * 0.016);
            Assert.AreEqual(before, ribbons.WordsUploads, "no text change: no upload");
            ribbons.Show(ads, RibbonBoards.AdSeconds + 0.01);
            Assert.AreEqual(before, ribbons.WordsUploads, "the ad scrolls by a material value: no upload");

            // A colour change alone is a material value: no upload.
            int afterAds = ribbons.WordsUploads;
            ribbons.Show(ArenaShow.At(new ArenaShowInput { Phase = RoundPhase.ResultHold, PhaseSeconds = 1.0, Result = ArenaResult.Goal, ShotNumber = 1 }), 0.0);
            Assert.AreEqual(afterAds + ribbons.PanelCount, ribbons.WordsUploads, "GOAL! on every panel: one upload each");
            ribbons.Show(At(RoundPhase.ResultHold, ArenaResult.Goal), 0.5);
            Assert.AreEqual(afterAds + ribbons.PanelCount, ribbons.WordsUploads, "the same GOAL! again: none");
        }

        [Test]
        public void A_Round_of_6_Shots_uploads_only_when_a_panels_words_change()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            var s = new RoundSettings();
            (RoundPhase phase, double length)[] phases =
            {
                (RoundPhase.SkateIn, s.SkateInSeconds), (RoundPhase.SlowDown, s.SlowDownSeconds),
                (RoundPhase.ShootingWindow, s.ShootingWindowSeconds), (RoundPhase.ResultHold, s.ResultHoldSeconds)
            };
            int start = ribbons.WordsUploads, frames = 0;
            double clock = 0.0;
            for (int shot = 1; shot <= 6; shot++)
                foreach ((RoundPhase phase, double length) in phases)
                    for (double t = 0.0; t < length; t += 1.0 / 60.0, clock += 1.0 / 60.0, frames++)
                    {
                        ArenaResult result = phase == RoundPhase.ResultHold ? (shot % 2 == 0 ? ArenaResult.Save : ArenaResult.Goal) : ArenaResult.None;
                        ribbons.Show(ArenaShow.At(new ArenaShowInput { Phase = phase, PhaseSeconds = t, Result = result, ShotNumber = shot }), clock);
                    }
            int uploads = ribbons.WordsUploads - start;
            TestContext.Out.WriteLine("RIBBON UPLOADS: " + uploads + " uploads over " + frames + " frames of a Round of 6 Shots (" + ribbons.PanelCount + " panels)");
            Assert.Greater(uploads, ribbons.PanelCount, "the ads and the results do change the words");
            Assert.Less(uploads, frames,"far fewer uploads than frames (one per panel per change, never one per frame)");
            Assert.Less(uploads, ribbons.PanelCount * 40, "a bounded handful per panel in a Round");
        }

        [Test]
        public void Ribbon_Boards_run_all_round_the_stands_on_the_arena_models_ribbon_ring()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            Assert.Greater(ribbons.PanelCount, 30);
            Bounds boards = RinkBoards.Box;
            float minX = 0f, maxX = 0f, minZ = 0f, maxZ = 0f;
            for (int i = 0; i < ribbons.PanelCount; i++)
            {
                Vector3 c = ribbons.CentreOf(i);
                Assert.AreEqual(RibbonBoards.Bottom + RibbonBoards.Height / 2f, c.y, 1e-4f);
                Assert.IsFalse(boards.Contains(new Vector3(c.x, 1f, c.z)), "panel " + i + " stands in the stands, past the boards");
                minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                minZ = Mathf.Min(minZ, c.z); maxZ = Mathf.Max(maxZ, c.z);
            }
            float ring = RibbonBoards.Offset;
            float side = ring + (float)RinkLayout.BenchDepth; // the stands step back beside the benches and boxes, which reach the side extremes
            Assert.AreEqual(RinkBoards.HalfWidth + side, maxX, 0.2f);
            Assert.AreEqual(-RinkBoards.HalfWidth - side, minX, 0.2f);
            // The near end's own panels are wholly behind the Video Board and left out: a corner's panel reaches furthest back.
            Assert.That(maxZ, Is.InRange(RinkBoards.EndZ + ring - 1f, RinkBoards.EndZ + ring + 0.2f));
            Assert.AreEqual(RinkBoards.FarZ - ring, minZ, 0.2f);
        }

        [Test]
        public void The_ring_follows_the_Rink_Layout_all_round_the_full_rink_and_steps_back_beside_the_benches()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            Assert.AreEqual(RinkLayout.RibbonOffset, RibbonBoards.Offset, 1e-4);
            Assert.AreEqual(RinkLayout.RibbonBottom, RibbonBoards.Bottom, 1e-4);
            Assert.AreEqual(RinkLayout.RibbonHeight, RibbonBoards.Height, 1e-4);
            // Panels stand at the Offset from the boards (a rounded rectangle pushed out), except beside a bench or box.
            int nearFarEnd = 0, farEnd = 0, steppedBack = 0;
            for (int i = 0; i < ribbons.PanelCount; i++)
            {
                Vector3 c = ribbons.CentreOf(i);
                bool beside = false;
                foreach (RinkLayout.Opening o in System.Linq.Enumerable.Concat(RinkLayout.Benches, RinkLayout.PenaltyBoxes))
                    if (Mathf.Sign(c.x) == o.Side && c.z >= o.FromZ && c.z <= o.ToZ && Mathf.Abs(c.x) > RinkLayout.HalfWidth + 1f) beside = true;
                float cx = (float)RinkLayout.CornerX, nearCz = (float)RinkLayout.NearCornerZ, farCz = (float)RinkLayout.FarCornerZ;
                var core = new Vector2(Mathf.Clamp(c.x, -cx, cx), Mathf.Clamp(c.z, farCz, nearCz));
                float away = Vector2.Distance(core, new Vector2(c.x, c.z)) - (float)RinkLayout.CornerRadius;
                float expected = RibbonBoards.Offset - 0.05f + (beside ? (float)RinkLayout.BenchDepth : 0f);
                // Chords on a corner sit a little inside the arc; straight runs are exact.
                Assert.AreEqual(expected, away, beside ? 0.2f : 0.3f, "panel " + i + " at " + c);
                if (beside) steppedBack++;
                if (c.z > RinkLayout.NearEndBoardsZ) nearFarEnd++;
                if (c.z < RinkLayout.FarEndBoardsZ) farEnd++;
            }
            Assert.Greater(nearFarEnd, 0, "panels behind the near net");
            Assert.Greater(farEnd, 0, "panels behind the far end");
            Assert.Greater(steppedBack, 0, "panels beside the benches and boxes");
        }

        [Test]
        public void The_Calm_Area_panels_straight_behind_the_near_net_are_all_there_since_the_Video_Board_hangs_above_the_ribbon()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            int behindNet = 0;
            for (int i = 0; i < ribbons.PanelCount; i++)
            {
                Vector3 c = ribbons.CentreOf(i);
                if (Mathf.Abs(c.x) > RinkLayout.CalmAreaHalfWidth) continue;
                if (c.z > RinkLayout.NearEndBoardsZ)
                {
                    Assert.AreEqual(ArenaZone.CalmArea, ribbons.ZoneOf(i), "panel " + i);
                    behindNet++;
                }
                else Assert.AreNotEqual(ArenaZone.CalmArea, ribbons.ZoneOf(i), "panel " + i);
            }
            Assert.Greater(behindNet, 0, "#165: the board is above the ribbon, so the panels straight behind the net are not left out");
        }

        [Test]
        public void The_Calm_Area_panels_are_those_behind_the_near_end_boards()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            int calm = 0, near = 0, far = 0;
            for (int i = 0; i < ribbons.PanelCount; i++)
            {
                Vector3 c = ribbons.CentreOf(i);
                ArenaZone zone = ribbons.ZoneOf(i);
                Assert.AreEqual(c.z > RinkBoards.EndZ, zone == ArenaZone.CalmArea, "panel " + i + " at " + c);
                if (zone == ArenaZone.CalmArea) calm++;
                else if (zone == ArenaZone.Near) near++;
                else far++;
            }
            Assert.Greater(calm, 0);
            Assert.Greater(near, 0);
            Assert.Greater(far, 0);
        }

        [Test]
        public void In_the_shooting_window_the_Calm_Area_panels_hold_one_dim_still_colour_and_the_rest_show_ads()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            ArenaState window = At(RoundPhase.ShootingWindow);
            ribbons.Show(window, 0.0);
            Color dim = RibbonBoards.ColourOf(ArenaShow.CalmRibbonColour);
            for (int i = 0; i < ribbons.PanelCount; i++)
            {
                if (ribbons.ZoneOf(i) == ArenaZone.CalmArea)
                {
                    Assert.AreEqual("", ribbons.TextOn(i), "panel " + i);
                    Assert.AreEqual((Color32)dim, (Color32)ribbons.BackOn(i), "panel " + i);
                }
                else Assert.IsTrue(Brands.IsAdWords(ribbons.TextOn(i)), "panel " + i + ": " + ribbons.TextOn(i));
            }

            // Still: the ads move on elsewhere, the Calm Area does not.
            ribbons.Show(window, RibbonBoards.AdSeconds * 1.5);
            for (int i = 0; i < ribbons.PanelCount; i++)
                if (ribbons.ZoneOf(i) == ArenaZone.CalmArea)
                {
                    Assert.AreEqual("", ribbons.TextOn(i));
                    Assert.AreEqual((Color32)dim, (Color32)ribbons.BackOn(i));
                }
        }

        [Test]
        public void Ads_show_one_brand_on_every_panel_all_Round()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            ArenaState skateIn = At(RoundPhase.SkateIn);
            ribbons.Show(skateIn, 0.0);
            string first = ribbons.TextOn(0);
            Assert.AreEqual(Brands.Current.AdWords, first);
            Assert.AreEqual(first, ribbons.TextOn(1));
            ribbons.Show(skateIn, RibbonBoards.AdSeconds + 0.01);
            Assert.AreEqual(first, ribbons.TextOn(0), "the same brand all Round; the picture scrolls");
        }

        [Test]
        public void Every_panel_shows_the_result_message_in_its_colour()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            ribbons.Show(At(RoundPhase.ResultHold, ArenaResult.Goal), 0.0);
            float goalScale = 0f;
            for (int i = 0; i < ribbons.PanelCount; i++)
            {
                Assert.AreEqual("GOAL!", ribbons.TextOn(i), "panel " + i);
                Assert.AreEqual((Color32)RibbonBoards.ColourOf(ArenaShow.GoalRed), (Color32)ribbons.TextColourOn(i), "panel " + i);
                goalScale = ribbons.TextScaleOn(i);
            }

            ribbons.Show(At(RoundPhase.ResultHold, ArenaResult.Save), 0.0);
            for (int i = 0; i < ribbons.PanelCount; i++)
            {
                Assert.AreEqual("SAVE", ribbons.TextOn(i), "panel " + i);
                Assert.AreEqual((Color32)RibbonBoards.ColourOf(ArenaShow.SaveWhite), (Color32)ribbons.TextColourOn(i), "panel " + i);
                Assert.Less(ribbons.TextScaleOn(i), goalScale, "SAVE is small");
            }

            ribbons.Show(At(RoundPhase.ResultHold, ArenaResult.FrameHitBar), 0.0);
            Assert.AreEqual("OFF THE BAR!", ribbons.TextOn(0));
        }

        [Test]
        public void Building_the_Ribbon_Boards_hides_the_arena_models_own_ribbon()
        {
            var modelRibbon = GameObject.CreatePrimitive(PrimitiveType.Quad);
            modelRibbon.name = ArenaStands.GlowPart;
            modelRibbon.transform.SetParent(_rink.transform);
            RibbonBoards.Build(_rink.transform);
            Assert.IsFalse(modelRibbon.GetComponent<Renderer>().enabled);
        }

        [Test]
        public void Ribbon_Boards_cast_no_shadows_and_have_no_colliders()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            Assert.AreEqual(0, ribbons.GetComponentsInChildren<Collider>().Length);
            foreach (Renderer r in ribbons.GetComponentsInChildren<Renderer>())
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, r.shadowCastingMode, r.name);
        }
    }
}
