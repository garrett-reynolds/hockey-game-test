using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The Team Skin seam: the League Skin is today's look, swapping a skin changes only the skin parts, and no skin can
    /// reach the fixed parts (Calm Area colours, brands, ice lines, Goal Show timing).
    /// </summary>
    public class TeamSkinTests
    {
        static Color C(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        /// <summary>Colours equal as Unity compares them (a material keeps its colour in linear, so a colour read back
        /// can be a float step off).</summary>
        static void AreClose(Color expected, Color actual, string what) =>
            Assert.IsTrue(expected == actual, what + ": expected " + expected.ToString("G9") + " but was " + actual.ToString("G9"));

        static Renderer Part(GameObject model, string name) =>
            model.GetComponentsInChildren<Renderer>(true).Single(r => r.name == name);

        /// <summary>A skin unlike the League Skin in every part.</summary>
        static TeamSkin TestSkin()
        {
            var skin = ScriptableObject.CreateInstance<TeamSkin>();
            skin.name = "test";
            skin.Rail = C(150, 30, 160); // a purple well clear of every reserved colour
            skin.KickPlate = C(10, 200, 10);
            skin.Seats = C(150, 120, 10);
            skin.RibbonEdgesOn = true;
            skin.RibbonEdge = C(240, 200, 0);
            skin.CentreIceLogo = "round-volt-rush";
            skin.RoofBanners = new[] { "round-volt-rush", "round-glacierlink" };
            skin.HomeJersey = new Color32(20, 90, 40, 255);
            skin.VideoBoardBack = C(60, 0, 0);
            skin.VideoBoardWords = C(255, 230, 0);
            skin.VideoBoardFrame = C(80, 0, 0);
            skin.VideoBoardFrameLit = C(255, 255, 255);
            skin.ScoreBoxBack = new Color(0.5f, 0f, 0f, 0.9f);
            skin.ScoreBoxText = Color.yellow;
            skin.GoalHorn = "goal-horn-test";
            skin.GoalSong = "goal-song-test";
            skin.Chant = "chant-test";
            return skin;
        }

        [Test]
        public void The_League_Skin_loads_with_todays_look()
        {
            TeamSkin league = TeamSkin.League;
            Assert.IsNotNull(league);
            Assert.AreEqual(league, TeamSkin.Load(SaveData.LeagueSkin));

            Assert.AreEqual("centre-ice-all-star-hockey", league.CentreIceLogo, "the All-Star Hockey mark");
            Assert.IsNotNull(Resources.Load<Texture2D>(RinkAds.ArtFolder + "/" + league.CentreIceLogo));
            Assert.AreEqual(BannerArt.LeagueNames(), league.RoofBanners, "the League's award and retired jersey banners (#149)");
            Assert.IsFalse(league.RibbonEdgesOn, "no Ribbon Board edges yet");
            Assert.AreEqual(new ArenaColour(112, 30, 46), league.HomeJerseyColour, "the burgundy home jersey");
            // Today's Video Board and Score Box colours (#89, #90).
            Assert.AreEqual(C(10, 16, 34), league.VideoBoardBack);
            Assert.AreEqual(Color.white, league.VideoBoardWords);
            Assert.AreEqual(C(14, 15, 18), league.VideoBoardFrame);
            Assert.AreEqual(C(255, 215, 80), league.VideoBoardFrameLit);
            Assert.AreEqual(new Color(0.05f, 0.08f, 0.18f, 0.85f), league.ScoreBoxBack);
            Assert.AreEqual(Color.white, league.ScoreBoxText);
            // Today's sounds: the horn file and the chant (its placeholder until a file is dropped in); no goal song yet.
            Assert.AreEqual("goal-horn", league.GoalHorn);
            Assert.AreEqual("chant", league.Chant);
            Assert.IsEmpty(league.GoalSong);
        }

        [Test]
        public void The_League_Skins_team_colours_are_the_boards_and_arena_models_own()
        {
            var boards = Resources.Load<GameObject>(ModelFiles.Folder + "/" + ModelFiles.Boards);
            var arena = Resources.Load<GameObject>(ModelFiles.Folder + "/" + ModelFiles.Arena);
            Assert.AreEqual(Part(boards, RinkBoards.RailPart).sharedMaterial.color, TeamSkin.League.Rail, "rail");
            Assert.AreEqual(Part(boards, RinkBoards.KickPlatePart).sharedMaterial.color, TeamSkin.League.KickPlate, "kick plate");
            Assert.AreEqual(Part(arena, ArenaStands.SeatsPart).sharedMaterial.color, TeamSkin.League.Seats, "seats");
        }

        [Test]
        public void A_blank_skin_name_plays_the_League_Skin_with_no_warning()
        {
            Assert.AreEqual(TeamSkin.League, TeamSkin.Load(""));
            Assert.AreEqual(TeamSkin.League, TeamSkin.Load(null));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void A_skin_with_a_post_red_rail_loads_with_the_League_rail_instead()
        {
            TeamSkin skin = TestSkin();
            skin.Rail = Color.red;                                // the posts' red
            skin.HomeJersey = new Color32(13, 13, 13, 255);       // puck black, on the near fans
            skin.VideoBoardWords = new Color32(77, 255, 77, 255); // the Easy mode glow green
            TeamSkin loaded = null;
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("reserved colour.*Rail.*HomeJersey.*VideoBoardWords"));
                loaded = TeamSkin.Guard(skin);
                AreClose(TeamSkin.League.Rail, loaded.Rail, "rail");
                Assert.AreEqual(TeamSkin.League.HomeJerseyColour, loaded.HomeJerseyColour, "jersey");
                AreClose(TeamSkin.League.VideoBoardWords, loaded.VideoBoardWords, "Video Board words");
                // The rest of the skin stays, and the skin asset itself is not changed.
                AreClose(skin.KickPlate, loaded.KickPlate, "kick plate");
                Assert.AreEqual(skin.CentreIceLogo, loaded.CentreIceLogo);
                Assert.AreEqual(Color.red, skin.Rail);
            }
            finally
            {
                if (loaded != null && loaded != skin) Object.DestroyImmediate(loaded);
                Object.DestroyImmediate(skin);
            }
        }

        [Test]
        public void The_League_Skin_and_a_safe_skin_load_unchanged()
        {
            // The League Skin is today's accepted look: kept as it is, though its rail is near the goalie's blue and its
            // dark frame near puck black.
            Assert.AreSame(TeamSkin.League, TeamSkin.Guard(TeamSkin.League));
            TeamSkin skin = TestSkin();
            try { Assert.AreSame(skin, TeamSkin.Guard(skin)); }
            finally { Object.DestroyImmediate(skin); }
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void An_unknown_skin_name_plays_the_League_Skin()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("No Team Skin \"no-such-team\""));
            Assert.AreEqual(TeamSkin.League, TeamSkin.Load("no-such-team"));
            Assert.AreEqual(TeamSkin.League, TeamSkin.OrLeague(null));
        }

        /// <summary>What the rink shows, read after building it with a skin.</summary>
        sealed class Look
        {
            public Color Rail, KickPlate, Seats, BoardBack, Frame, CalmRibbon;
            public float CalmCrowdLight, CalmCrowdContrast;
            public string CentreIce, Ice;
            public string[] AdPanels, BrandIceLogos, RibbonAds;
            public int Edges, Banners;
            public HashSet<ArenaColour> Shirts;
        }

        static ArenaState At(RoundPhase phase) =>
            ArenaShow.At(new ArenaShowInput { Phase = phase, PhaseSeconds = 1.0, ShotNumber = 1 });

        static Look Build(TeamSkin skin)
        {
            Camera camera = null;
            Transform rink = null;
            try
            {
                rink = RinkBuilder.Build(out camera, skin);
                Renderer[] all = rink.GetComponentsInChildren<Renderer>(true);
                Color ColourOf(string part) => all.First(r => r.name == part).sharedMaterial.color;
                string ArtOf(Renderer r) => r.sharedMaterial.mainTexture != null ? r.sharedMaterial.mainTexture.name : "";

                var ribbons = rink.GetComponentInChildren<RibbonBoards>();
                var crowd = rink.GetComponentInChildren<Crowd>();
                var boards = rink.GetComponentInChildren<VideoBoards>();
                Renderer[] iceLogos = rink.Find(RinkAds.Part + "/" + RinkAds.IceLogosPart).GetComponentsInChildren<Renderer>();
                int calmPanel = Enumerable.Range(0, ribbons.PanelCount).First(i => ribbons.ZoneOf(i) == ArenaZone.CalmArea);

                var look = new Look
                {
                    Rail = ColourOf(RinkBoards.RailPart),
                    KickPlate = ColourOf(RinkBoards.KickPlatePart),
                    Seats = ColourOf(ArenaStands.SeatsPart),
                    CentreIce = ArtOf(iceLogos.Single(r => r.name.EndsWith("centre ice"))),
                    BrandIceLogos = iceLogos.Where(r => !r.name.EndsWith("centre ice")).Select(ArtOf).ToArray(),
                    AdPanels = rink.Find(RinkAds.Part + "/" + RinkAds.BoardAdsPart).GetComponentsInChildren<Renderer>().Select(ArtOf).ToArray(),
                    Ice = ArtOf(all.First(r => r.sharedMaterial != null && r.sharedMaterial.name == RinkIce.IceMaterial)),
                    Edges = all.Count(r => r.name == RibbonBoards.EdgePart),
                    Banners = rink.Find(RoofBanners.Part) == null ? 0 : rink.Find(RoofBanners.Part).childCount,
                    Shirts = new HashSet<ArenaColour>(Enumerable.Range(0, crowd.FanCount).Select(i => crowd.LookOf(i).Shirt)),
                };
                ribbons.Show(At(RoundPhase.Idle), 0.0);
                look.RibbonAds = Enumerable.Range(0, ribbons.PanelCount).Select(ribbons.TextOn).ToArray();
                boards.Show(At(RoundPhase.SkateIn), 0.0);
                look.BoardBack = boards.BackColour;
                look.Frame = boards.FrameColour;

                ArenaState window = At(RoundPhase.ShootingWindow); // the Calm Area rules hold, goalie set to the Shot
                ribbons.Show(window, 0.0);
                crowd.Show(window, 0.0);
                look.CalmRibbon = ribbons.BackOn(calmPanel);
                look.CalmCrowdLight = crowd.LightIn(ArenaZone.CalmArea);
                look.CalmCrowdContrast = crowd.ContrastIn(ArenaZone.CalmArea);
                return look;
            }
            finally
            {
                if (rink != null) Object.DestroyImmediate(rink.gameObject);
                if (camera != null) Object.DestroyImmediate(camera.gameObject);
            }
        }

        /// <summary>The Round's sounds play the skin's horn, goal song and chant files.</summary>
        static void AssertSounds(TeamSkin skin)
        {
            var listener = new GameObject("Listener");
            var made = new List<AudioClip>();
            AudioClip File(string name)
            {
                var clip = AudioClip.Create(name, 10, 1, 44100, false);
                made.Add(clip);
                return clip;
            }
            try
            {
                var league = new RoundSound(listener, new SaveData(), new RoundSettings(), new SeededRandom(1), null, File);
                Assert.AreEqual(TeamSkin.League.GoalHorn, league.GoalHornClip.name);
                Assert.AreEqual(TeamSkin.League.Chant, league.ChantClip.name);
                Assert.IsNull(league.GoalSongClip, "the League Skin has no goal song");

                var team = new RoundSound(listener, new SaveData(), new RoundSettings(), new SeededRandom(1), skin, File);
                Assert.AreEqual(skin.GoalHorn, team.GoalHornClip.name);
                Assert.AreEqual(skin.Chant, team.ChantClip.name);
                Assert.AreEqual(skin.GoalSong, team.GoalSongClip.name);
            }
            finally
            {
                Object.DestroyImmediate(listener);
                foreach (AudioClip c in made) Object.DestroyImmediate(c);
            }
        }

        [Test]
        public void Swapping_the_skin_changes_only_the_skin_parts()
        {
            TeamSkin skin = TestSkin();
            try
            {
                Look league = Build(null), team = Build(skin);

                // The skin parts follow the skin.
                AreClose(TeamSkin.League.Rail, league.Rail, "League rail");
                AreClose(skin.Rail, team.Rail, "rail");
                AreClose(skin.KickPlate, team.KickPlate, "kick plate");
                Color dimmed = skin.Seats * ArenaStands.StandsLight; // the stands sit in the dark, seats too
                AreClose(new Color(dimmed.r, dimmed.g, dimmed.b, skin.Seats.a), team.Seats, "seats");
                Assert.AreEqual(TeamSkin.League.CentreIceLogo, league.CentreIce);
                Assert.AreEqual(skin.CentreIceLogo, team.CentreIce);
                Assert.AreEqual(0, league.Edges);
                Assert.Greater(team.Edges, 0, "the skin's Ribbon Board edges");
                Assert.AreEqual(TeamSkin.League.RoofBanners.Length, league.Banners, "the League Skin's own banners (#149)");
                Assert.AreEqual(skin.RoofBanners.Length, team.Banners);
                Assert.That(league.Shirts, Has.Member(TeamSkin.League.HomeJerseyColour));
                Assert.That(team.Shirts, Has.Member(skin.HomeJerseyColour));
                Assert.That(team.Shirts, Has.No.Member(TeamSkin.League.HomeJerseyColour));
                AreClose(TeamSkin.League.VideoBoardBack, league.BoardBack, "League board back");
                AreClose(skin.VideoBoardBack, team.BoardBack, "board back");
                AreClose(TeamSkin.League.VideoBoardFrame, league.Frame, "League frame");
                AreClose(skin.VideoBoardFrame, team.Frame, "frame");
                Assert.AreEqual((TeamSkin.League.ScoreBoxBack, TeamSkin.League.ScoreBoxText), RoundDriver.ScoreBoxColours(null));
                Assert.AreEqual((skin.ScoreBoxBack, skin.ScoreBoxText), RoundDriver.ScoreBoxColours(skin));
                AssertSounds(skin);

                // The fixed parts do not.
                Assert.AreEqual(league.AdPanels, team.AdPanels, "the brands' board ads, soft grey in the Calm Area");
                Assert.AreEqual(league.BrandIceLogos, team.BrandIceLogos, "the brands' ice logos");
                Assert.AreEqual(league.RibbonAds, team.RibbonAds, "the brands' ribbon ads");
                Assert.AreEqual(league.Ice, team.Ice, "the ice lines");
                Assert.AreEqual(league.CalmRibbon, team.CalmRibbon, "the Calm Area's one dim still ribbon colour");
                Assert.AreEqual(league.CalmCrowdLight, team.CalmCrowdLight, "the Calm Area's dim crowd");
                Assert.AreEqual(league.CalmCrowdContrast, team.CalmCrowdContrast, "the Calm Area's low-contrast crowd");
            }
            finally { Object.DestroyImmediate(skin); }
        }

        [Test]
        public void A_skin_holds_only_the_skin_parts()
        {
            string[] parts =
            {
                "Rail", "KickPlate", "Seats", "RibbonEdgesOn", "RibbonEdge", "CentreIceLogo", "RoofBanners", "HomeJersey",
                "VideoBoardBack", "VideoBoardWords", "VideoBoardFrame", "VideoBoardFrameLit", "ScoreBoxBack", "ScoreBoxText",
                "GoalHorn", "GoalSong", "Chant"
            };
            string[] fields = typeof(TeamSkin).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(f => f.Name).ToArray();
            Assert.That(fields, Is.EquivalentTo(parts), "a new skin field must be a skin part, never a fixed one");
        }

        [Test]
        public void No_skin_can_reach_the_Goal_Show_timing_the_Calm_Area_rules_or_the_brands()
        {
            // The Arena Show (Goal Show timing, Calm Area colours), the brands and the Rink Layout (ice lines, rink shape)
            // live in the core assembly, which cannot see the game assembly that holds the Team Skin.
            foreach (Type fixedPart in new[] { typeof(ArenaShow), typeof(Brands), typeof(RinkLayout), typeof(ReservedColours) })
                Assert.That(fixedPart.Assembly.GetReferencedAssemblies().Select(a => a.Name),
                    Does.Not.Contain(typeof(TeamSkin).Assembly.GetName().Name), fixedPart.Name);
        }
    }
}
