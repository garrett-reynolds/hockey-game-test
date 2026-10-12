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
        public void RemoveRink() => Object.DestroyImmediate(_rink);

        static ArenaState At(RoundPhase phase, ArenaResult result = ArenaResult.None) =>
            ArenaShow.At(new ArenaShowInput { Phase = phase, PhaseSeconds = 1.0, Result = result, ShotNumber = 1 });

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
            Assert.AreEqual(RinkBoards.HalfWidth + ring, maxX, 0.2f);
            Assert.AreEqual(-RinkBoards.HalfWidth - ring, minX, 0.2f);
            Assert.AreEqual(RinkBoards.EndZ + ring, maxZ, 0.2f);
            Assert.AreEqual(RinkBoards.NearZ - ring, minZ, 0.2f);
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
                else Assert.IsTrue(RibbonBoards.IsBrand(ribbons.TextOn(i)), "panel " + i + ": " + ribbons.TextOn(i));
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
        public void Ads_cycle_and_neighbouring_panels_show_different_brands()
        {
            RibbonBoards ribbons = RibbonBoards.Build(_rink.transform);
            ArenaState skateIn = At(RoundPhase.SkateIn);
            ribbons.Show(skateIn, 0.0);
            string first = ribbons.TextOn(0);
            Assert.AreNotEqual(first, ribbons.TextOn(1));
            ribbons.Show(skateIn, RibbonBoards.AdSeconds + 0.01);
            Assert.AreNotEqual(first, ribbons.TextOn(0));
            Assert.IsTrue(RibbonBoards.IsBrand(ribbons.TextOn(0)));
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
