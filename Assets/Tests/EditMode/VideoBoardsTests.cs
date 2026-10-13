using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class VideoBoardsTests
    {
        GameObject _rink, _eye;
        Camera _camera;

        [SetUp]
        public void MakeRink()
        {
            _rink = new GameObject("Rink");
            _eye = new GameObject("Shooter Eye");
            _camera = _eye.AddComponent<Camera>(); // the game's camera: Unity's default 60° tall view
            _camera.aspect = 16f / 9f;
        }

        [TearDown]
        public void RemoveRink()
        {
            Object.DestroyImmediate(_rink);
            Object.DestroyImmediate(_eye);
        }

        static readonly AttackSide[] Sides = { AttackSide.Left, AttackSide.Middle, AttackSide.Right };

        static ArenaState At(RoundPhase phase, double seconds = 1.0, ArenaResult result = ArenaResult.None, bool newBest = false) =>
            ArenaShow.At(new ArenaShowInput
            {
                Phase = phase,
                PhaseSeconds = seconds,
                Result = result,
                IsNewBest = newBest,
                Goals = 2,
                ShotNumber = 4,
                BestScore = 3,
                LastShotSpeedKmh = 92
            });

        static Vector3[] Corners(Bounds b)
        {
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
            return corners;
        }

        // ---- where they hang ----

        [Test]
        public void The_Video_Board_is_on_screen_at_the_start_of_the_skate_in_from_every_Attack_Side()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            foreach (AttackSide side in Sides)
            {
                Breakaway.SkatePoint(side, 0.0, out double x, out double distance);
                BreakawayView.PlaceShooterCamera(_eye.transform, (float)x, (float)distance, 0f, 0f);
                foreach (Vector3 corner in Corners(boards.VideoBoardBounds))
                {
                    Vector3 v = _camera.WorldToViewportPoint(corner);
                    Assert.That(v.z, Is.GreaterThan(0f), side + ": in front of the eye");
                    Assert.That(v.x, Is.InRange(0f, 1f), side + ": the whole board is on screen");
                    Assert.That(v.y, Is.InRange(0f, 1f), side + ": the whole board is on screen");
                }
            }
        }

        [Test]
        public void The_Video_Board_shows_one_dim_still_colour_and_no_words_from_goalie_set_to_the_Shot()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            Color dim = RibbonBoards.ColourOf(ArenaShow.CalmRibbonColour);
            foreach (double seconds in new[] { 0.0, 1.0, 2.99 })
            {
                boards.Show(At(RoundPhase.ShootingWindow, seconds), 12.0 + seconds);
                for (int i = 0; i < boards.ScreenCount; i++)
                {
                    Assert.AreEqual("", boards.HeadlineOn(i), "no words, screen " + i);
                    Assert.AreEqual("", boards.DetailsOn(i), "no words, screen " + i);
                    Assert.AreEqual((Color32)dim, (Color32)boards.PictureOn(i).color, "the Calm Ribbon's colour, screen " + i);
                }
            }
            Color frame = boards.FrameColour;
            boards.Show(At(RoundPhase.ShootingWindow, 2.0), 99.0);
            Assert.AreEqual(frame, boards.FrameColour, "no frame flash");
            boards.Show(At(RoundPhase.SkateIn), 0.0);
            Assert.AreNotEqual("", boards.HeadlineOn(0), "the words come back after the Shot's window");
            Assert.AreNotEqual((Color32)dim, (Color32)boards.PictureOn(0).color);
        }

        [Test]
        public void In_the_shooting_window_the_Video_Board_stays_clear_of_the_net_and_the_goalie_from_every_Attack_Side()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            Vector3[] corners = Corners(boards.VideoBoardBounds); // its whole box, frame too
            // The net's frame, and the highest the goalie reaches in front of it (the points the Replay's look-up test keeps out of view).
            Vector3[] net =
            {
                new Vector3(-0.92f, 0f, 0f), new Vector3(0.92f, 0f, 0f), new Vector3(-0.92f, 1.22f, 0f), new Vector3(0.92f, 1.22f, 0f),
                new Vector3(0f, 2.0f, -1.2f)
            };
            var settings = new RoundSettings();
            float rollPeriod = (float)(2 * System.Math.PI / Breakaway.RollRadiansPerSecond);
            foreach (AttackSide side in Sides)
            {
                Breakaway.SkatePoint(side, 1.0, out double x, out double distance);
                for (double window = 0; window <= settings.ShootingWindowSeconds + 1e-9; window += 0.1)
                {
                    float drift = (float)Breakaway.Drift(side, settings.SlowDownSeconds, window, settings);
                    for (float stride = 0f; stride < rollPeriod; stride += 0.02f)
                    {
                        BreakawayView.PlaceShooterCamera(_eye.transform, (float)x, (float)distance, drift, stride);
                        Rect board = Outline(corners), goalie = Outline(net);
                        string at = side + " " + window.ToString("0.0") + " s into the window";
                        Assert.That(board.Overlaps(goalie), Is.False, at + ": the board " + board + " over the net and goalie " + goalie);
                    }
                }
            }
        }

        /// <summary>The viewport rectangle round the points (in front of the camera).</summary>
        Rect Outline(Vector3[] points)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (Vector3 p in points)
            {
                Vector3 v = _camera.WorldToViewportPoint(p);
                Assert.That(v.z, Is.GreaterThan(0f), "in front of the eye");
                x0 = Mathf.Min(x0, v.x); y0 = Mathf.Min(y0, v.y); x1 = Mathf.Max(x1, v.x); y1 = Mathf.Max(y1, v.y);
            }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        [Test]
        public void The_Video_Board_hangs_clear_in_front_of_the_crowd_and_the_Ribbon_Board()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            // No fan stands in the board's frame, in front of it, or within the clear gap behind it.
            float behind = VideoBoards.Depth + 0.01f + (float)VideoBoardCover.ClearGap;
            foreach (FanSeat s in CrowdSeats.AllIncludingHidden())
                foreach (double dy in new[] { 0.0, VideoBoardCover.FanHeight })
                foreach (double dx in new[] { -VideoBoardCover.FanHalfWidth, VideoBoardCover.FanHalfWidth })
                foreach (double dz in new[] { -VideoBoardCover.FanHalfWidth, VideoBoardCover.FanHalfWidth })
                    Assert.IsFalse(VideoBoardCover.InSlab(s.X + dx, s.Y + dy, s.Z + dz, behind), s + " is in or in front of the board");
            // Nor a Ribbon Board panel.
            for (int i = 0; i < ribbons.PanelCount; i++)
            {
                Vector3 c = ribbons.CentreOf(i);
                if (Mathf.Abs(c.x) > 10f || c.z < (float)RinkLayout.NearEndBoardsZ) continue; // only the near end's panels are near the board
                Assert.IsFalse(VideoBoardCover.InSlab(c.x, c.y, c.z, behind), "ribbon panel " + i + " at " + c + " is in or in front of the board");
                Assert.That(c.z, Is.GreaterThan(boards.VideoBoardBounds.max.z + 0.3f), "ribbon panel " + i + " is behind the board");
            }
        }

        [Test]
        public void No_fan_and_no_Ribbon_Board_panel_sits_wholly_behind_the_Video_Board_and_the_ones_that_did_are_gone()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            Vector3[] frame = FrontOutline(boards);
            FanSeat[] shown = CrowdSeats.All();
            Assert.That(CrowdSeats.AllIncludingHidden().Length - shown.Length, Is.GreaterThan(100), "the fans behind the board are left out");
            // Seen from every eye, with a Unity camera: a fan or panel whose box lies inside the board's outline is hidden.
            var eyes = VideoBoardCover.Eyes;
            foreach (FanSeat s in shown)
            {
                if (Mathf.Abs((float)s.X) > 12f || s.Z < boards.VideoBoardBounds.min.z) continue; // nowhere near the board's back
                Assert.IsFalse(HiddenFromEveryEye(eyes, frame, BoxOf(s.X - 0.25, s.Y, s.Z - 0.25, s.X + 0.25, s.Y + VideoBoardCover.FanHeight, s.Z + 0.25)), s + " is wholly hidden but still there");
            }
            Assert.That(ribbons.PanelCount, Is.GreaterThan(30));
            for (int i = 0; i < ribbons.PanelCount; i++)
            {
                Vector3 c = ribbons.CentreOf(i);
                if (c.z < boards.VideoBoardBounds.min.z || Mathf.Abs(c.x) > 12f) continue;
                float half = ribbons.LengthOf(i) / 2f; // the near end's panels run along x
                Vector3[] box = BoxOf(c.x - half, RinkLayout.RibbonBottom, c.z - 0.05, c.x + half, RinkLayout.RibbonBottom + RinkLayout.RibbonHeight, c.z + 0.05);
                if (Mathf.Abs(c.z - ((float)RinkLayout.NearEndBoardsZ + RibbonBoards.Offset)) > 0.2f) continue; // a corner's panel, not along x
                Assert.IsFalse(HiddenFromEveryEye(eyes, frame, box), "ribbon panel " + i + " at " + c + " is wholly hidden but still there");
            }
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void Every_fan_and_Ribbon_Board_panel_left_out_is_wholly_behind_the_board_from_the_real_camera()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            Vector3[] frame = FrontOutline(boards);
            // The shooter's real camera (BreakawayView.PlaceShooterCamera, bob and drift included) over every Attack Side: the skate-in,
            // the slow-down and the shooting window. The Replay's look-up turns the camera from where it ends but does not move it.
            var settings = new RoundSettings();
            float rollPeriod = (float)(2 * System.Math.PI / Breakaway.RollRadiansPerSecond);
            var eyes = new System.Collections.Generic.List<(double X, double Y, double Z)>();
            void Eye(float x, float distance, float drift, float stride)
            {
                BreakawayView.PlaceShooterCamera(_eye.transform, x, distance, drift, stride);
                Vector3 p = _eye.transform.position;
                eyes.Add((p.x, p.y, p.z));
            }
            foreach (AttackSide side in Sides)
            {
                for (double u = 0.0; u <= 1.0 + 1e-9; u += 0.05)
                {
                    Breakaway.SkatePoint(side, u, out double x, out double distance);
                    for (float stride = 0f; stride < rollPeriod; stride += 0.1f) Eye((float)x, (float)distance, 0f, stride);
                }
                Breakaway.SkatePoint(side, 1.0, out double sx, out double sd);
                for (double t = 0.0; t <= settings.SlowDownSeconds + 1e-9; t += 0.1)
                    for (float stride = 0f; stride < rollPeriod; stride += 0.1f)
                        Eye((float)sx, (float)sd, (float)Breakaway.Drift(side, t, 0.0, settings), stride);
                for (double window = 0; window <= settings.ShootingWindowSeconds + 1e-9; window += 0.1)
                {
                    float drift = (float)Breakaway.Drift(side, settings.SlowDownSeconds, window, settings);
                    for (float stride = 0f; stride < rollPeriod; stride += 0.02f) Eye((float)sx, (float)sd, drift, stride);
                }
            }
            var all = eyes.ToArray();

            FanSeat[] visible = CrowdSeats.All();
            var left = new System.Collections.Generic.HashSet<FanSeat>(CrowdSeats.AllIncludingHidden());
            left.ExceptWith(visible);
            Assert.That(left.Count, Is.GreaterThan(100), "fans are left out");
            foreach (FanSeat s in left)
                Assert.IsTrue(HiddenFromEveryEye(all, frame, BoxOf(s.X - 0.25, s.Y, s.Z - 0.25, s.X + 0.25, s.Y + VideoBoardCover.FanHeight, s.Z + 0.25)),
                              s + " is left out but the real camera can see it");
            Assert.That(ribbons.LeftOut.Count, Is.GreaterThan(0), "panels are left out");
            foreach ((Vector2 from, Vector2 to) in ribbons.LeftOut)
                Assert.IsTrue(HiddenFromEveryEye(all, frame, BoxOf(Mathf.Min(from.x, to.x), RinkLayout.RibbonBottom, Mathf.Min(from.y, to.y) - 0.05f,
                                                                     Mathf.Max(from.x, to.x), RinkLayout.RibbonBottom + RinkLayout.RibbonHeight, Mathf.Max(from.y, to.y) + 0.05f)),
                              "the ribbon panel from " + from + " to " + to + " is left out but the real camera can see it");
        }

        static Vector3[] BoxOf(double x0, double y0, double z0, double x1, double y1, double z1)
        {
            var box = new Vector3[8];
            for (int i = 0; i < 8; i++)
                box[i] = new Vector3((float)((i & 1) == 0 ? x0 : x1), (float)((i & 2) == 0 ? y0 : y1), (float)((i & 4) == 0 ? z0 : z1));
            return box;
        }

        /// <summary>The front outline of the board's frame (its four corners, anticlockwise from bottom left as the player sees it).</summary>
        static Vector3[] FrontOutline(VideoBoards boards)
        {
            Vector3 c = boards.CentreOf(0), facing = boards.FacingOf(0);
            Vector3 right = Vector3.right, up = Vector3.Cross(facing, right).normalized; // facing points at the player; up along the face
            if (up.y < 0f) up = -up;
            float hw = boards.SizeOf(0).x / 2f + VideoBoards.FrameMargin, hh = boards.SizeOf(0).y / 2f + VideoBoards.FrameMargin;
            return new[] { c - right * hw - up * hh, c + right * hw - up * hh, c + right * hw + up * hh, c - right * hw + up * hh };
        }

        // Whether, from every eye, every corner of the box is behind the frame's outline (the ray from the eye crosses the frame's rectangle first).
        static bool HiddenFromEveryEye((double X, double Y, double Z)[] eyes, Vector3[] frame, Vector3[] box)
        {
            Vector3 origin = frame[0], across = frame[1] - frame[0], upward = frame[3] - frame[0];
            Vector3 normal = Vector3.Cross(across, upward).normalized;
            float w = across.magnitude, h = upward.magnitude;
            foreach (var e in eyes)
            {
                var eye = new Vector3((float)e.X, (float)e.Y, (float)e.Z);
                foreach (Vector3 p in box)
                {
                    Vector3 ray = p - eye;
                    float denominator = Vector3.Dot(ray, normal);
                    if (Mathf.Abs(denominator) < 1e-6f) return false;
                    float t = Vector3.Dot(origin - eye, normal) / denominator;
                    if (t <= 0f || t >= 1f) return false; // the plane is not between the eye and the point
                    Vector3 hit = eye + ray * t - origin;
                    float a = Vector3.Dot(hit, across / w), b = Vector3.Dot(hit, upward / h);
                    if (a < 0f || a > w || b < 0f || b > h) return false;
                }
            }
            return true;
        }

        [Test]
        public void The_Centre_Board_has_four_screens_facing_out_over_centre_ice_hung_under_the_roof()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            Assert.AreEqual(5, boards.ScreenCount, "the Video Board's screen and the Centre Board's four");
            var middle = new Vector3(0f, (float)RinkLayout.CentreBoard.Y, (float)RinkLayout.CentreLineZ);
            for (int i = 1; i < 5; i++)
            {
                Vector3 c = boards.CentreOf(i);
                Vector3 outward = new Vector3(c.x - middle.x, 0f, c.z - middle.z).normalized;
                Assert.That(Vector3.Dot(boards.FacingOf(i), outward), Is.GreaterThan(0.999f), "screen " + i + " faces out");
                Assert.AreEqual((float)RinkLayout.CentreBoardBottom, c.y - boards.SizeOf(i).y / 2f, 0.01f, "bottom 20 m up");
                Assert.AreEqual((float)RinkLayout.CentreBoardScreenWidth, boards.SizeOf(i).x, 0.01f);
                Assert.AreEqual((float)RinkLayout.CentreBoardScreenHeight, boards.SizeOf(i).y, 0.01f);
            }
            Bounds all = new Bounds(boards.CentreOf(1), Vector3.zero);
            foreach (Renderer r in boards.CentreBoard.GetComponentsInChildren<Renderer>())
                if (r.name != VideoBoards.CablePart) all.Encapsulate(r.bounds);
            Assert.That(all.max.y, Is.LessThan(ArenaStands.RoofHeight - 3f), "hangs under the roof with room");
            Assert.AreEqual((float)RinkLayout.CentreLineZ, all.center.z, 0.01f, "over centre ice");
            Assert.AreEqual(0f, all.center.x, 0.01f);
        }

        [Test]
        public void The_Video_Board_hangs_behind_the_near_net_facing_the_player()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            Vector3 c = boards.CentreOf(0);
            Assert.AreEqual((float)RinkLayout.VideoBoard.Z, c.z, 0.01f);
            Assert.AreEqual((float)RinkLayout.VideoBoard.Y, c.y, 0.01f);
            Assert.That(boards.FacingOf(0).z, Is.LessThan(-0.9f), "toward the player");
            Assert.That(boards.FacingOf(0).y, Is.LessThan(-0.1f), "tilted down toward him");
            Assert.That(boards.VideoBoardBounds.max.y, Is.LessThan(20f - 0.5f), "under the light banks (ArenaLighting hangs them 20 m up)");
            foreach (Renderer r in boards.VideoBoard.GetComponentsInChildren<Renderer>())
                if (r.name == VideoBoards.CablePart)
                {
                    Assert.AreEqual(ArenaStands.RoofHeight, r.bounds.max.y, 0.01f, "a cable up to the roof");
                    Assert.AreEqual(boards.VideoBoardBounds.max.y, r.bounds.min.y, 0.3f, "from the top of the board");
                }
        }

        /// <summary>The gap between two boxes (metres; 0 when they touch or overlap).</summary>
        static float Gap(Bounds a, Bounds b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
            float dy = Mathf.Max(0f, Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y));
            float dz = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        [Test]
        public void No_light_bank_touches_either_board_or_its_cables()
        {
            ArenaLighting.Build(_rink.transform);
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            Renderer[] banks = System.Array.FindAll(_rink.GetComponentsInChildren<Renderer>(), r => r.name == ArenaLighting.LightBank);
            Assert.That(banks.Length, Is.GreaterThanOrEqualTo(6));
            foreach (Transform board in new[] { boards.CentreBoard, boards.VideoBoard })
            foreach (Renderer part in board.GetComponentsInChildren<Renderer>()) // the frame box and cables, not just the screens
            foreach (Renderer bank in banks)
                Assert.That(Gap(part.bounds, bank.bounds), Is.GreaterThanOrEqualTo(0.3f),
                            part.name + " and the light bank at " + bank.bounds.center);
        }

        [Test]
        public void The_light_banks_by_the_Centre_Board_move_out_beside_it_not_away()
        {
            ArenaLighting.Build(_rink.transform);
            Renderer[] banks = System.Array.FindAll(_rink.GetComponentsInChildren<Renderer>(), r => r.name == ArenaLighting.LightBank);
            Assert.AreEqual(2 * 7 * 4, banks.Length, "every bank still lights the bowl");
            float centre = (float)RinkLayout.CentreLineZ;
            int beside = 0;
            foreach (Renderer bank in banks)
                if (Mathf.Abs(bank.bounds.center.x) < 6f && Mathf.Abs(bank.bounds.center.z - centre) < 8f) beside++;
            Assert.AreEqual(4, beside, "the four inner banks near centre ice hang just outside the board");
        }

        // ---- what they show ----

        [Test]
        public void The_Centre_Board_always_shows_what_the_Video_Board_shows()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            ArenaState[] states =
            {
                At(RoundPhase.Idle), At(RoundPhase.SkateIn), At(RoundPhase.ShootingWindow),
                At(RoundPhase.ResultHold, 1.0, ArenaResult.Goal), At(RoundPhase.ResultHold, 1.0, ArenaResult.Save),
                At(RoundPhase.ResultHold, 1.0, ArenaResult.WideMiss), At(RoundPhase.RoundOver),
                At(RoundPhase.RoundOver, 0.1, newBest: true)
            };
            foreach (ArenaState state in states)
            {
                boards.Show(state, 12.0);
                for (int i = 1; i < boards.ScreenCount; i++)
                {
                    Assert.AreEqual(boards.HeadlineOn(0), boards.HeadlineOn(i), state.VideoBoard + ", screen " + i);
                    Assert.AreEqual(boards.DetailsOn(0), boards.DetailsOn(i), state.VideoBoard + ", screen " + i);
                    Assert.AreEqual(boards.WordsColourOn(0), boards.WordsColourOn(i), state.VideoBoard + ", screen " + i);
                    // Words: each screen has its own material on its board's texels; they must show the same colours.
                    Material a = boards.PictureOn(0), b = boards.PictureOn(i);
                    Assert.AreEqual(a.color, b.color, "the same colour on every screen");
                    Assert.AreEqual(a.GetColor("_Backdrop"), b.GetColor("_Backdrop"), "the same backdrop on every screen");
                    Assert.AreEqual(a.mainTexture == boards.WordsTextureOn(0), b.mainTexture == boards.WordsTextureOn(i), "all screens show words, or none does");
                }
            }
        }

        [Test]
        public void The_boards_show_the_Arena_Shows_words()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            Assert.AreEqual(ArenaShow.GameName, boards.HeadlineOn(0), "the start card as built");
            ArenaState skateIn = At(RoundPhase.SkateIn);
            boards.Show(skateIn, 0.0);
            Assert.AreEqual(skateIn.VideoBoardWords.Headline, boards.HeadlineOn(0));
            Assert.AreEqual("LAST SHOT 92 km/h", boards.DetailsOn(0));
        }

        [Test]
        public void An_ad_slide_shows_a_brand_and_the_next_one_after_a_while()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            ArenaState ad = At(RoundPhase.ResultHold, 1.0, ArenaResult.WideMiss);
            boards.Show(ad, 0.0);
            string first = boards.HeadlineOn(0);
            Assert.IsTrue(Brands.IsAdWords(first), first);
            Assert.AreEqual("", boards.DetailsOn(0));
            boards.Show(ad, VideoBoards.AdSeconds);
            Assert.IsTrue(Brands.IsAdWords(boards.HeadlineOn(0)));
            Assert.AreNotEqual(first, boards.HeadlineOn(0));
        }

        [Test]
        public void A_new_best_flashes_the_frame_with_the_Goal_Show_lights_and_nothing_else_does()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            boards.Show(At(RoundPhase.RoundOver, 0.1), 0.0);
            Color dark = boards.FrameColour;
            // the new-best show starts dark for half a flash (#92)
            ArenaState lit = At(RoundPhase.RoundOver, 0.35, newBest: true), unlit = At(RoundPhase.RoundOver, 0.1, newBest: true);
            Assert.IsTrue(lit.GoalShow.Lit);
            Assert.IsFalse(unlit.GoalShow.Lit);
            boards.Show(lit, 0.0);
            Assert.AreNotEqual(dark, boards.FrameColour);
            boards.Show(unlit, 0.0);
            Assert.AreEqual(dark, boards.FrameColour);
            boards.Show(At(RoundPhase.ResultHold, 0.1, ArenaResult.Goal), 0.0);
            Assert.AreEqual(dark, boards.FrameColour, "the Goal Show's lights are the goal light and the roof's");
        }

        [Test]
        public void A_picture_such_as_the_Replay_shows_on_every_screen_instead_of_the_words_until_cleared()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            var replay = new Texture2D(4, 4);
            try
            {
                boards.ShowPicture(replay);
                boards.Show(At(RoundPhase.SkateIn), 0.0);
                for (int i = 0; i < boards.ScreenCount; i++)
                {
                    Assert.AreSame(replay, boards.PictureOn(i).mainTexture, "screen " + i);
                    Assert.IsTrue(LedBoardMaterial.IsPlain(boards.PictureOn(i)), "screen " + i + " draws the Replay sharp, with no LED cells (#127)");
                    Assert.AreEqual("", boards.HeadlineOn(i), "no words over the picture");
                }
                Assert.AreEqual(Color.white, boards.BackColour, "the picture untinted");

                boards.ShowPicture(null);
                for (int i = 0; i < boards.ScreenCount; i++)
                    Assert.IsFalse(LedBoardMaterial.IsPlain(boards.PictureOn(i)), "screen " + i + " words are LED cells again");
                Assert.AreSame(boards.WordsTextureOn(0), boards.PictureOn(0).mainTexture, "back to the board's words");
                Assert.AreEqual(At(RoundPhase.SkateIn).VideoBoardWords.Headline, boards.HeadlineOn(4), "the words come back");
                Assert.AreNotEqual(Color.white, boards.BackColour);
            }
            finally
            {
                Object.DestroyImmediate(replay);
            }
        }

        [Test]
        public void The_words_are_LED_cells_of_the_screens_own_material_with_no_text_objects()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            boards.Show(At(RoundPhase.SkateIn), 0.0);
            Assert.AreEqual(0, boards.GetComponentsInChildren<TextMesh>(true).Length, "no text objects: the words are cells of the screens' textures");
            for (int i = 0; i < boards.ScreenCount; i++)
            {
                Material m = boards.PictureOn(i);
                Assert.AreEqual(LedBoardMaterial.ShaderName, m.shader.name, "screen " + i);
                Assert.IsTrue(m.shader.isSupported, "screen " + i + " shader works");
                Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Geometry, m.renderQueue, "screen " + i + " is a solid screen, not blended text");
            }
        }

        // ---- LED look (#121) ----

        [Test]
        public void Every_Video_Board_and_Centre_Board_screen_and_words_renderer_uses_the_LED_shader_with_the_board_cell_size()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            var picture = new Texture2D(4, 4);
            try
            {
                var states = new System.Action[]
                {
                    () => boards.Show(At(RoundPhase.SkateIn), 0.0),
                    () => boards.Show(At(RoundPhase.ShootingWindow), 0.0), // the Calm still
                    () => boards.ShowPicture(picture),
                };
                float cell = (float)RinkLayout.VideoBoardLedCell;
                foreach (System.Action show in states)
                {
                    show();
                    Assert.AreEqual(5, boards.ScreenCount);
                    for (int i = 0; i < boards.ScreenCount; i++)
                    {
                        Material m = boards.PictureOn(i);
                        Assert.AreEqual(LedBoardMaterial.ShaderName, m.shader.name, "screen " + i + " " + m.name);
                        Assert.AreEqual(cell, LedBoardMaterial.CellOf(m), 1e-6f, "screen " + i + " cell size");
                        // #127: only the Replay picture is drawn plain (sharp, no cells); words and the Calm still keep the LED cells and gaps.
                        Assert.AreEqual(ReferenceEquals(m.mainTexture, picture), LedBoardMaterial.IsPlain(m), "screen " + i + " " + m.name + " plain only for the Replay picture");
                    }
                    // The words are a picture on the screen: the screen's material reads the board's words texture (one texel per cell) and
                    // lights a cell when its texel is on.
                    if (!ReferenceEquals(boards.PictureOn(0).mainTexture, boards.WordsTextureOn(0))) continue; // the Calm still or the Replay picture
                    for (int i = 0; i < boards.ScreenCount; i++)
                    {
                        Texture2D texels = boards.WordsTextureOn(i);
                        Assert.AreSame(texels, boards.PictureOn(i).mainTexture, "screen " + i + " shows its board's words");
                        Assert.AreEqual(boards.SizeOf(i).x / cell, texels.width, 1e-3f, "screen " + i + " texture width: one texel per cell");
                        Assert.AreEqual(boards.SizeOf(i).y / cell, texels.height, 1e-3f, "screen " + i + " texture height");
                        Assert.AreEqual(0.5f, boards.PictureOn(i).GetFloat("_LitAbove"), "screen " + i + " lights a cell for a texel of 1");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(picture);
            }
        }

        [Test]
        public void The_LED_cell_grid_is_anchored_at_each_screens_corner()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            boards.Show(At(RoundPhase.SkateIn), 0.0);
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < boards.ScreenCount; i++)
            {
                Vector3 facing = boards.FacingOf(i), c = boards.CentreOf(i);
                Vector3 right = Vector3.Cross(Vector3.up, -facing).normalized; // the quad's local x as the player sees it
                Vector3 up = Vector3.Cross(-facing, right).normalized;
                Vector3 expected = c - right * boards.SizeOf(i).x / 2f - up * boards.SizeOf(i).y / 2f;
                int found = 0;
                foreach (Renderer r in boards.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.name.EndsWith("Screen " + i)) continue; // the frame and cables are not on the grid
                    found++;
                    r.GetPropertyBlock(block);
                    Vector4 origin = block.GetVector("_GridOrigin");
                    Assert.AreEqual(0f, Vector3.Distance(expected, origin), 1e-3f, r.name + " grid origin " + origin + " expected " + expected);
                }
                Assert.AreEqual(1, found, "the screen " + i);
            }
        }

        [Test]
        public void The_boards_are_whole_numbers_of_LED_cells_and_the_words_are_tall_enough()
        {
            double cell = RinkLayout.VideoBoardLedCell;
            foreach (double metres in new[] { RinkLayout.VideoBoardWidth, RinkLayout.VideoBoardHeight, RinkLayout.CentreBoardScreenWidth, RinkLayout.CentreBoardScreenHeight })
                Assert.AreEqual(System.Math.Round(metres / cell), metres / cell, 1e-6, metres + " m is a whole number of cells");
            Assert.GreaterOrEqual(RinkLayout.VideoBoardHeight * 0.24 / cell, 14, "Headline cells tall on the Video Board");
            Assert.GreaterOrEqual(RinkLayout.CentreBoardScreenHeight * 0.24 / cell, 14, "Headline cells tall on the Centre Board");
            Assert.GreaterOrEqual(RinkLayout.CentreBoardScreenHeight * 0.11 / cell, 5, "Details cells tall on the smallest Centre Board screen");
            Assert.GreaterOrEqual(RinkLayout.VideoBoardHeight * 0.11 / cell, 5, "Details cells tall on the Video Board");
            // In the LED font a Headline's capitals are 7 x scale rows: a short word is at least 14 rows tall on both boards, and every line is at least 7.
            foreach (int[] size in new[] { new[] { 143, 78 }, new[] { 110, 65 } })
            {
                Assert.GreaterOrEqual(LedWords.Place("GOAL!", size[0], size[1], VideoBoards.HeadlineShare, VideoBoards.HeadlineY).Scale * LedFont.CapHeight, 14, "Headline rows tall, " + size[0] + " cells across");
                Assert.GreaterOrEqual(LedWords.Place("SHOOT TO PLAY", size[0], size[1], VideoBoards.DetailsShare, -VideoBoards.DetailsY).Scale * LedFont.CapHeight, 7, "Details rows tall, " + size[0] + " cells across");
            }
        }

        [Test]
        public void A_static_frame_uploads_nothing_and_a_text_change_uploads_each_board_once()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            int idle = boards.WordsUploads;
            Assert.AreEqual(2, idle, "the Video Board's texture and the Centre Board's, uploaded once when built");
            boards.Show(At(RoundPhase.SkateIn), 0.0);
            int built = boards.WordsUploads;
            Assert.AreEqual(idle + 2, built, "the Skate-in words differ from the start card's: one upload each");
            for (int frame = 1; frame <= 30; frame++) boards.Show(At(RoundPhase.SkateIn), frame / 60.0);
            Assert.AreEqual(built, boards.WordsUploads, "30 frames with the same words: no upload");
            boards.ShowPicture(null);
            Assert.AreEqual(built, boards.WordsUploads, "showing the words again uploads nothing");
            boards.Show(At(RoundPhase.ResultHold, 1.0, ArenaResult.Goal), 1.0);
            Assert.AreEqual(built + 2, boards.WordsUploads, "new text on both boards: one upload each");
            boards.Show(At(RoundPhase.ResultHold, 1.0, ArenaResult.Goal), 1.5);
            Assert.AreEqual(built + 2, boards.WordsUploads, "the same words again: nothing");
            boards.PaintWords(0, Color.green);
            Assert.AreEqual(built + 2, boards.WordsUploads, "a colour is a material value: no upload");
        }

        [Test]
        public void Words_that_equal_the_last_written_ones_cost_no_upload_even_after_the_Calm_Area()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            boards.Show(At(RoundPhase.SkateIn), 0.0);
            int written = boards.WordsUploads;
            boards.Show(At(RoundPhase.ShootingWindow), 1.0); // the Calm Area: empty words, never written
            Assert.AreEqual(written, boards.WordsUploads, "the Calm Area writes nothing");
            boards.Show(At(RoundPhase.SkateIn), 2.0);
            Assert.AreEqual(written, boards.WordsUploads, "the same words as the last written: no upload");
            boards.ShowWords("GOAL!", "");
            Assert.AreEqual(written + 2, boards.WordsUploads, "new words: one upload each");
            boards.ShowWords("GOAL!", "");
            Assert.AreEqual(written + 2, boards.WordsUploads, "ShowWords with the words already written: no upload");
        }

        // Cost (#121 Task 3c): the words textures and how often a Round uploads them. 60 frames of each phase, as the game runs them.
        [Test]
        public void A_Round_of_6_Shots_uploads_the_words_a_few_dozen_times_and_the_textures_take_under_20_KB()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            int start = boards.WordsUploads;
            var results = new[] { ArenaResult.Goal, ArenaResult.Save, ArenaResult.WideMiss, ArenaResult.TooLate, ArenaResult.Goal, ArenaResult.Save };
            double clock = 0.0;
            int goals = 0;
            for (int shot = 1; shot <= 6; shot++)
            {
                foreach (RoundPhase phase in new[] { RoundPhase.SkateIn, RoundPhase.SlowDown, RoundPhase.ShootingWindow, RoundPhase.ResultHold })
                {
                    ArenaResult result = phase == RoundPhase.ResultHold ? results[shot - 1] : ArenaResult.None;
                    // The ResultHold after Too late shows an ad slide for 15 s (3 slides); the others last a second.
                    int frames = phase == RoundPhase.ResultHold && result == ArenaResult.TooLate ? 15 * 60 : 60;
                    for (int frame = 0; frame < frames; frame++)
                    {
                        clock += 1.0 / 60.0;
                        boards.Show(ArenaShow.At(new ArenaShowInput
                        {
                            Phase = phase, PhaseSeconds = frame / 60.0, Result = result, Goals = goals, ShotNumber = shot, BestScore = 3,
                            LastShotSpeedKmh = shot > 1 ? 88 : (double?)null
                        }), clock);
                    }
                }
                if (results[shot - 1] == ArenaResult.Goal) goals++;
            }
            for (int frame = 0; frame < 60; frame++) boards.Show(ArenaShow.At(new ArenaShowInput { Phase = RoundPhase.RoundOver, PhaseSeconds = frame / 60.0, Goals = goals, ShotNumber = 6, BestScore = 3 }), clock += 1.0 / 60.0);
            int round = boards.WordsUploads - start;
            Assert.AreEqual(2, boards.WordsTextureCount, "one texture for the Video Board, one shared by the Centre Board's four screens");
            Assert.AreEqual(143 * 78 + 110 * 65, boards.WordsTextureBytes, "R8, one byte per LED cell: 18,304 bytes");
            Assert.Less(boards.WordsTextureBytes, 20 * 1024);
            Assert.AreEqual(0, round % 2, "the boards change together");
            // Measured: 15 changes of words, each uploaded once on each board (2 x 15). The Calm Area's empty words are never written (the
            // screens show the still colour), and words that repeat the last written ones cost nothing.
            Assert.AreEqual(30, round, "uploads in a Round of 6 Shots (" + boards.WordsTextureBytes + " bytes of textures)");
        }

        [Test]
        public void Removing_the_Video_Boards_frees_the_materials_they_made_but_not_the_shown_picture()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            var replay = new Texture2D(4, 4);
            try
            {
                var made = new System.Collections.Generic.HashSet<Material>();
                var textures = new System.Collections.Generic.List<Texture2D>();
                void Collect()
                {
                    foreach (Renderer r in boards.GetComponentsInChildren<Renderer>()) made.Add(r.sharedMaterial);
                }
                Collect();
                for (int i = 0; i < boards.ScreenCount; i++) textures.Add(boards.WordsTextureOn(i));
                boards.Show(At(RoundPhase.ShootingWindow), 0.0); // the Calm Area's still picture
                Collect();
                boards.ShowPicture(replay);
                Assert.That(made.Count, Is.GreaterThanOrEqualTo(4), "picture, still, frame and cable");

                Object.DestroyImmediate(boards.gameObject);

                foreach (Material m in made) Assert.IsTrue(m == null, m + " freed");
                foreach (Texture2D t in textures) Assert.IsTrue(t == null, "a words texture freed");
                Assert.IsTrue(replay != null, "the Replay picture is the Replay's own");
            }
            finally
            {
                Object.DestroyImmediate(replay);
            }
        }
    }
}
