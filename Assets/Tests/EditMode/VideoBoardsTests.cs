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
        public void The_Video_Board_is_off_the_top_of_the_view_from_goalie_set_to_the_Shot_from_every_Attack_Side()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            Vector3[] corners = Corners(boards.VideoBoardBounds); // its whole box, frame too (the cables only rise above it)
            var settings = new RoundSettings();
            float rollPeriod = (float)(2 * System.Math.PI / Breakaway.RollRadiansPerSecond);
            foreach (AttackSide side in Sides)
            {
                Breakaway.SkatePoint(side, 1.0, out double x, out double distance);
                for (double window = 0; window <= settings.ShootingWindowSeconds + 1e-9; window += 0.1)
                {
                    float drift = (float)Breakaway.Drift(side, settings.SlowDownSeconds, window, settings);
                    // Every stride bob and roll: one roll period holds both swings of each.
                    for (float stride = 0f; stride < rollPeriod; stride += 0.02f)
                    {
                        BreakawayView.PlaceShooterCamera(_eye.transform, (float)x, (float)distance, drift, stride);
                        foreach (Vector3 corner in corners)
                        {
                            Vector3 v = _camera.WorldToViewportPoint(corner);
                            Assert.That(v.y, Is.GreaterThan(1f), side + " " + window.ToString("0.0") + " s into the window: above the view");
                        }
                    }
                }
            }
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
                    Assert.AreSame(boards.PictureOn(0), boards.PictureOn(i), "one picture for every screen");
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
            Assert.IsTrue(RibbonBoards.IsBrand(first), first);
            Assert.AreEqual("", boards.DetailsOn(0));
            boards.Show(ad, VideoBoards.AdSeconds);
            Assert.IsTrue(RibbonBoards.IsBrand(boards.HeadlineOn(0)));
            Assert.AreNotEqual(first, boards.HeadlineOn(0));
        }

        [Test]
        public void A_new_best_flashes_the_frame_with_the_Goal_Show_lights_and_nothing_else_does()
        {
            VideoBoards boards = VideoBoards.Build(_rink.transform);
            boards.Show(At(RoundPhase.RoundOver, 0.1), 0.0);
            Color dark = boards.FrameColour;
            ArenaState lit = At(RoundPhase.RoundOver, 0.1, newBest: true), unlit = At(RoundPhase.RoundOver, 0.35, newBest: true);
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
                    Assert.AreEqual("", boards.HeadlineOn(i), "no words over the picture");
                }
                Assert.AreEqual(Color.white, boards.BackColour, "the picture untinted");

                boards.ShowPicture(null);
                Assert.IsNull(boards.PictureOn(0).mainTexture);
                Assert.AreEqual(At(RoundPhase.SkateIn).VideoBoardWords.Headline, boards.HeadlineOn(4), "the words come back");
                Assert.AreNotEqual(Color.white, boards.BackColour);
            }
            finally
            {
                Object.DestroyImmediate(replay);
            }
        }
    }
}
