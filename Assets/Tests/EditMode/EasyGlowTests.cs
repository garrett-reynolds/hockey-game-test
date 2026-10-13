using System.Collections.Generic;
using System.Linq;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// Easy mode glows each Gap a puck can fit through (the goalie-reach port's Gaps, asked by the Round), from when the
    /// goalie is set (a Late Settle once he has settled) until the Shot. The green glow shows before the Shot though the
    /// goalie in its Ready Stance (or the neutral blocks) stands in front of it: seen from the shooter's eye, the glow draws
    /// over whatever is in front of it. Once the goalie reacts it is depth-tested again, so the flying puck stays in front of it.
    /// </summary>
    public class EasyGlowTests
    {
        static readonly RoundSettings Defaults = new RoundSettings();

        GameObject _parent;
        readonly List<Object> _made = new List<Object>();
        readonly List<GoalieView> _views = new List<GoalieView>(); // every view a test built, disposed in TearDown
        readonly Dictionary<Collider, Renderer> _bakedFor = new Dictionary<Collider, Renderer>(); // a baked body collider: its renderer

        [SetUp]
        public void MakeRink() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void RemoveRink()
        {
            foreach (GoalieView v in _views) v?.Dispose();
            _views.Clear();
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
            Object.DestroyImmediate(_parent);
        }

        // ---- When the glow shows (Core, pure) ----

        [Test]
        public void The_glow_shows_once_he_is_set_and_turns_off_at_the_Shot()
        {
            foreach (SetUp setUp in new[] { SetUp.Clean, SetUp.Depth, SetUp.Cheat(Post.Left), SetUp.Cheat(Post.Right) })
            {
                Assert.IsFalse(EasyGlow.Shows(true, RoundPhase.SkateIn, 1.0, setUp, Defaults), setUp + ": skating in");
                Assert.IsFalse(EasyGlow.Shows(true, RoundPhase.SlowDown, Defaults.SlowDownSeconds - 0.01, setUp, Defaults), setUp + ": still settling");
                Assert.IsTrue(EasyGlow.Shows(true, RoundPhase.ShootingWindow, 0, setUp, Defaults), setUp + ": set as the window opens");
                Assert.IsTrue(EasyGlow.Shows(true, RoundPhase.ShootingWindow, 2.9, setUp, Defaults), setUp + ": until the Shot");
                Assert.IsFalse(EasyGlow.Shows(true, RoundPhase.ResultHold, 0, setUp, Defaults), setUp + ": off at the Shot");
                Assert.IsFalse(EasyGlow.Shows(true, RoundPhase.Idle, 0, setUp, Defaults), setUp + ": no Round");
                Assert.IsFalse(EasyGlow.Shows(true, RoundPhase.RoundOver, 0, setUp, Defaults), setUp + ": Round over");
            }
        }

        [Test]
        public void A_Late_Settle_glows_once_he_has_settled()
        {
            var settings = new RoundSettings { LateSettleSeconds = 1.0 };
            foreach (Post post in new[] { Post.Left, Post.Right })
            {
                SetUp late = SetUp.LateSettle(post);
                Assert.IsFalse(EasyGlow.Shows(true, RoundPhase.ShootingWindow, 0, late, settings), post + ": still sliding as the window opens");
                Assert.IsFalse(EasyGlow.Shows(true, RoundPhase.ShootingWindow, 0.99, late, settings), post + ": not settled yet");
                Assert.IsTrue(EasyGlow.Shows(true, RoundPhase.ShootingWindow, 1.0, late, settings), post + ": settled");
                Assert.IsFalse(EasyGlow.Shows(true, RoundPhase.ResultHold, 0, late, settings), post + ": off at the Shot");
            }
            Assert.IsTrue(EasyGlow.Shows(true, RoundPhase.ShootingWindow, 0.5, SetUp.LateSettle(Post.Left), new RoundSettings { LateSettleSeconds = 0.4 }),
                "settles on RoundSettings' time");
        }

        [Test]
        public void With_Easy_mode_off_nothing_glows()
        {
            foreach (RoundPhase phase in System.Enum.GetValues(typeof(RoundPhase)))
                Assert.IsFalse(EasyGlow.Shows(false, phase, 2.0, SetUp.Depth, Defaults), phase.ToString());
        }

        // ---- The Round asks the goalie-reach port for the Gaps (Core) ----

        /// <summary>A Round in its shooting window, windowSeconds in, on this Attack Side and Set-up.</summary>
        static Round InWindow(IGoalieReach reach, AttackSide side, SetUp setUp, double windowSeconds)
        {
            var round = new Round(ScriptedRandom.Forcing(side, setUp), Defaults, reach, 0);
            round.StartRound();
            round.Tick(Defaults.SkateInSeconds + Defaults.SlowDownSeconds + windowSeconds);
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
            return round;
        }

        [Test]
        public void The_Round_asks_the_port_for_the_Gaps_as_a_Shot_now_would_be_raced()
        {
            // Gear in the way only on the left half: the right half is the Gap.
            var reach = FakeReach.Saving((x, y) => x < 50);
            Round round = InWindow(reach, AttackSide.Right, SetUp.Cheat(Post.Left), 0.7);

            NetFaceGaps gaps = round.GapsNow();

            FakeReach.Ask ask = reach.GapAsks.Single().ask;
            Assert.AreEqual(SetUp.Cheat(Post.Left), ask.SetUp);
            Assert.AreEqual(AttackSide.Right, ask.Side);
            Assert.AreEqual(0.7, ask.SecondsSinceWindowOpened, 1e-6);
            RinkPoint start = Breakaway.ShotStart(AttackSide.Right, ask.SecondsSinceWindowOpened, Defaults);
            Assert.AreEqual(start.X, ask.ShotStart.X, 1e-9);
            Assert.AreEqual(start.Y, ask.ShotStart.Y, 1e-9);
            Assert.AreEqual(start.Z, ask.ShotStart.Z, 1e-9);
            Assert.AreEqual(NetFaceGaps.Columns * NetFaceGaps.Rows / 2, gaps.Count);
            Assert.IsTrue(gaps.IsGap(NetFaceGaps.Columns - 1, 0));
            Assert.IsFalse(gaps.IsGap(0, NetFaceGaps.Rows - 1));
        }

        [Test]
        public void Outside_the_shooting_window_there_are_no_Gaps_to_glow()
        {
            var reach = FakeReach.NeverReaches();
            var round = new Round(ScriptedRandom.Forcing(AttackSide.Middle, SetUp.Depth), Defaults, reach, 0);
            Assert.AreEqual(0, round.GapsNow().Count, "Idle");
            round.StartRound();
            Assert.AreEqual(0, round.GapsNow().Count, "skating in");
            round.Tick(Defaults.SkateInSeconds + 0.1);
            Assert.AreEqual(0, round.GapsNow().Count, "slowing down");
            round.Tick(Defaults.SlowDownSeconds);
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase);
            round.SubmitShotAndWhistle(50, 50, 100, false);
            Assert.AreEqual(0, round.GapsNow().Count, "after the Shot");
            Assert.IsEmpty(reach.GapAsks);
        }

        [Test]
        public void Asking_for_the_Gaps_rolls_nothing_and_changes_no_race()
        {
            var settings = new RoundSettings(); // with the Reaction Time wobble

            ShotRace RaceOf(bool askForGaps, out double nextReaction)
            {
                var round = new Round(new SeededRandom(7), settings, FakeReach.Saving((x, y) => y < 50), 0);
                round.StartRound();
                round.Tick(settings.SkateInSeconds + settings.SlowDownSeconds + 1.2);
                if (askForGaps) for (int i = 0; i < 5; i++) round.GapsNow();
                round.SubmitShotAndWhistle(70, 80, 120, false);
                ShotRace race = round.LastRace.Value;
                round.Tick(round.ResultHoldLength + 0.001);
                nextReaction = round.ReactionSeconds;
                return race;
            }

            ShotRace plain = RaceOf(false, out double plainNext), asked = RaceOf(true, out double askedNext);
            Assert.AreEqual(plain.ReactionSeconds, asked.ReactionSeconds);
            Assert.AreEqual(plain.ReachSeconds, asked.ReachSeconds);
            Assert.AreEqual(plain.FlightSeconds, asked.FlightSeconds);
            Assert.AreEqual(plain.IsSave, asked.IsSave);
            Assert.AreEqual(plainNext, askedNext, "the next Shot's wobble roll");
        }

        [Test]
        public void A_run_of_measured_cells_replaces_only_those_cells()
        {
            NetFaceGaps top = NetFaceGaps.Where((x, y) => y > 50), all = NetFaceGaps.Where((x, y) => true);
            NetFaceGaps run = NetFaceGaps.Where((x, y) => true, 10, 5);
            Assert.AreEqual(5, run.Count, "only the run is measured");
            Assert.IsTrue(run.IsGap(10, 0) && run.IsGap(2, 1) && !run.IsGap(9, 0) && !run.IsGap(3, 1));

            NetFaceGaps merged = top.With(all, 0, NetFaceGaps.Columns); // the bottom row from all, the rest kept
            Assert.AreEqual(top.Count + NetFaceGaps.Columns, merged.Count);
            Assert.IsTrue(merged.IsGap(0, 0) && !merged.IsGap(0, 1) && merged.IsGap(0, NetFaceGaps.Rows - 1));
            Assert.AreEqual(top.Count, top.With(all, 0, 0).Count, "an empty run changes nothing");
        }

        [Test]
        public void The_glow_sweeps_the_Gaps_in_a_few_cells_each_frame_and_drops_them_at_the_Shot()
        {
            var reach = FakeReach.Saving((x, y) => y < 50); // the top half is the Gap
            Round round = InWindow(reach, AttackSide.Middle, SetUp.Depth, 0);
            var glow = new EasyGlow();
            NetFaceGaps whole = round.GapsNow();
            reach.GapAsks.Clear();

            int updates = NetFaceGaps.Cells / EasyGlow.CellsPerUpdate;
            NetFaceGaps shown = null;
            for (int i = 0; i < updates; i++)
            {
                shown = glow.Update(true, round);
                Assert.AreEqual(i * EasyGlow.CellsPerUpdate, reach.GapAsks[i].firstCell);
                Assert.AreEqual(EasyGlow.CellsPerUpdate, reach.GapAsks[i].cellCount);
                if (i == updates / 2 - 1) Assert.AreEqual(0, shown.Count, "the bottom half has no Gap: nothing glows yet");
                round.Tick(0.001);
            }
            Assert.AreEqual(whole.Count, shown.Count, "after one sweep the whole Net Face is measured");
            for (int column = 0; column < NetFaceGaps.Columns; column++)
            for (int row = 0; row < NetFaceGaps.Rows; row++)
                Assert.AreEqual(whole.IsGap(column, row), shown.IsGap(column, row), "cell " + column + "," + row);
            glow.Update(true, round);
            Assert.AreEqual(0, reach.GapAsks[updates].firstCell, "the next sweep starts again from the bottom");

            Assert.AreEqual(0, glow.Update(false, round).Count, "Easy mode off");
            round.SubmitShotAndWhistle(50, 80, 100, false);
            int asked = reach.GapAsks.Count;
            Assert.AreEqual(0, glow.Update(true, round).Count, "off at the Shot");
            Assert.AreEqual(asked, reach.GapAsks.Count, "nothing measured once the glow is off");
        }

        [Test]
        public void A_Late_Settle_glows_nothing_until_he_has_settled()
        {
            var reach = FakeReach.NeverReaches(); // the whole Net Face is a Gap
            Round round = InWindow(reach, AttackSide.Left, SetUp.LateSettle(Post.Right), 0.5);
            var glow = new EasyGlow();
            Assert.AreEqual(0, glow.Update(true, round).Count, "still sliding across");
            Assert.IsEmpty(reach.GapAsks);
            round.Tick(Defaults.LateSettleSeconds - 0.5 + 0.01);
            Assert.AreEqual(EasyGlow.CellsPerUpdate, glow.Update(true, round).Count, "settled: the first cells sweep in");
        }

        // ---- The glow (Game) ----

        static CompareFunction DepthTest(Material m) =>
            m.HasProperty("_ZTest") ? (CompareFunction)Mathf.RoundToInt(m.GetFloat("_ZTest")) : CompareFunction.LessEqual;

        /// <summary>The glow cell for a Net Face cell.</summary>
        Renderer GlowCell(int column, int row) => _parent.transform.Find("Gap glow/Gap " + column + "," + row).GetComponent<Renderer>();

        IEnumerable<Renderer> AllGlowCells() => _parent.transform.Find("Gap glow").GetComponentsInChildren<Renderer>(true);

        [Test]
        public void The_glow_lights_exactly_the_Gaps_and_nothing_once_they_are_gone()
        {
            var view = new GoalieView(_parent.transform, new GoalieCharacter(_parent.transform));
            _views.Add(view);
            view.Show(true);
            NetFaceGaps gaps = NetFaceGaps.Where((x, y) => x > 75 && y > 60);
            Assert.Greater(gaps.Count, 0);

            view.ShowGaps(gaps);
            for (int column = 0; column < NetFaceGaps.Columns; column++)
            for (int row = 0; row < NetFaceGaps.Rows; row++)
                Assert.AreEqual(gaps.IsGap(column, row), GlowCell(column, row).gameObject.activeInHierarchy, "cell " + column + "," + row);

            NetFaceGaps.Cell(NetFaceGaps.Columns - 1, NetFaceGaps.Rows - 1, out double minX, out double minY, out double maxX, out double maxY);
            NetFaceMapping.ToMetres(minX, minY, out double left, out double bottom);
            NetFaceMapping.ToMetres(maxX, maxY, out double right, out double top);
            Bounds corner = GlowCell(NetFaceGaps.Columns - 1, NetFaceGaps.Rows - 1).bounds;
            Assert.AreEqual((left + right) / 2, corner.center.x, 1e-4, "on its Net Face cell");
            Assert.AreEqual((bottom + top) / 2, corner.center.y, 1e-4, "on its Net Face cell");
            Assert.LessOrEqual(corner.size.x, right - left + 1e-4, "no wider than its cell");

            view.ShowGaps(NetFaceGaps.None);
            foreach (Renderer r in AllGlowCells()) Assert.IsFalse(r.gameObject.activeInHierarchy, r.name + ": no Gaps");

            view.ShowGaps(gaps);
            view.Show(false);
            foreach (Renderer r in AllGlowCells()) Assert.IsFalse(r.gameObject.activeInHierarchy, r.name + ": goalie hidden");
        }

        /// <summary>Colliders matching what is drawn under the parent now (the skinned body baked as posed).</summary>
        void AddColliders()
        {
            foreach (Renderer r in _parent.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                if (r is SkinnedMeshRenderer skinned)
                {
                    var baked = new Mesh();
                    skinned.BakeMesh(baked, true);
                    var holder = new GameObject(r.name + " (collider)");
                    holder.transform.SetPositionAndRotation(skinned.transform.position, skinned.transform.rotation);
                    var collider = holder.AddComponent<MeshCollider>();
                    collider.sharedMesh = baked;
                    _bakedFor[collider] = r;
                    _made.Add(holder);
                    _made.Add(baked);
                }
                else
                {
                    r.gameObject.AddComponent<MeshCollider>().sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                }
            }
            Physics.SyncTransforms();
        }

        Renderer RendererOf(Collider c) => _bakedFor.TryGetValue(c, out Renderer r) ? r : c.GetComponent<Renderer>();

        [TestCase(false, TestName = "With the goalie character")]
        [TestCase(true, TestName = "With the goalie blocks")]
        public void Before_the_Shot_the_glow_draws_over_the_goalie_in_front_of_every_Gap(bool blocks)
        {
            var character = blocks ? new GoalieCharacter(_parent.transform, name => null) : new GoalieCharacter(_parent.transform);
            if (!blocks) Assert.IsTrue(character.Present, "the goalie model loads");
            var view = new GoalieView(_parent.transform, character);
            _views.Add(view);
            var port = new GoalieCharacterReach(character, Defaults);
            int covered = 0, glowing = 0;
            var problems = new List<string>();
            foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
            foreach (SetUp setUp in new[] { SetUp.Cheat(Post.Left), SetUp.Cheat(Post.Right), SetUp.Depth })
            {
                Vector3 eye = ShooterEyes.Of(side).First().Item2; // the window opens
                RinkPoint start = Breakaway.ShotStart(side, 0, Defaults);
                RinkPoint spot = SetUps.Spot(setUp, side, 0, start, Defaults);
                float facing = (float)SetUps.Facing(spot, start);
                view.Show(true);
                view.Stand(new Vector3((float)spot.X, 0f, (float)spot.Z));
                view.Face(facing, facing);
                view.SetProgress(0f); // Ready Stance: no Shot yet
                NetFaceGaps gaps = port.Gaps(setUp, side, 0, start, 0, NetFaceGaps.Cells);
                view.ShowGaps(gaps);
                AddColliders();
                for (int column = 0; column < NetFaceGaps.Columns; column++)
                for (int row = 0; row < NetFaceGaps.Rows; row++)
                {
                    if (!gaps.IsGap(column, row)) continue;
                    glowing++;
                    Renderer glow = GlowCell(column, row);
                    Bounds b = glow.bounds;
                    // Its centre and its corners (inset): a Gap is clear of his gear from the puck, a little below and ahead of
                    // the eye, so from the eye his gear can stand in front of part of the cell.
                    foreach (Vector3 point in new[]
                             {
                                 b.center, b.center + Vector3.Scale(b.extents, new Vector3(0.8f, 0.8f, 0f)),
                                 b.center + Vector3.Scale(b.extents, new Vector3(-0.8f, 0.8f, 0f)),
                                 b.center + Vector3.Scale(b.extents, new Vector3(0.8f, -0.8f, 0f)),
                                 b.center + Vector3.Scale(b.extents, new Vector3(-0.8f, -0.8f, 0f)),
                             })
                    {
                        Vector3 ray = point - eye;
                        // The physics puck's hit shapes on his gear (#111) draw nothing: only what is drawn can hide the glow.
                        RaycastHit[] hits = Physics.RaycastAll(eye, ray.normalized, ray.magnitude + 0.1f).Where(h => RendererOf(h.collider) != null).OrderBy(h => h.distance).ToArray();
                        int at = System.Array.FindIndex(hits, h => RendererOf(h.collider) == glow);
                        string where = side + " " + setUp + " Gap " + column + "," + row;
                        if (at < 0) { problems.Add(where + ": the glow is not on the line from the eye"); continue; }
                        if (at > 0) covered++;
                        Material glowMaterial = glow.sharedMaterial;
                        for (int i = 0; i < at; i++)
                        {
                            Renderer front = RendererOf(hits[i].collider);
                            foreach (Material m in front.sharedMaterials)
                                if (DepthTest(glowMaterial) != CompareFunction.Always || m.renderQueue >= glowMaterial.renderQueue)
                                    problems.Add(where + ": hidden behind " + front.name + " (glow depth test " + DepthTest(glowMaterial)
                                        + ", queue " + glowMaterial.renderQueue + " vs " + m.renderQueue + ")");
                        }
                    }
                }
                foreach (MeshCollider c in _parent.GetComponentsInChildren<MeshCollider>(true)) Object.DestroyImmediate(c);
                foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
                _made.Clear();
                _bakedFor.Clear();
            }
            Assert.IsEmpty(problems, string.Join("\n", problems.Distinct()));
            Assert.Greater(glowing, 0);
            Assert.Greater(covered, 0, "the Ready Stance stands in front of some glowing Gaps from the eye (else this test proves nothing)");
        }

        [Test]
        public void Once_the_goalie_reacts_the_glow_is_depth_tested_so_the_puck_stays_in_front()
        {
            var view = new GoalieView(_parent.transform, new GoalieCharacter(_parent.transform));
            _views.Add(view);
            view.Show(true);
            view.ShowGaps(NetFaceGaps.Where((x, y) => true));
            Renderer glow = GlowCell(0, 0);
            view.SetProgress(0f);
            Assert.AreEqual(CompareFunction.Always, DepthTest(glow.sharedMaterial), "before the Shot");
            view.SetProgress(0.1f);
            Assert.AreEqual(CompareFunction.LessEqual, DepthTest(glow.sharedMaterial), "reacting");
            view.SetProgress(1f);
            Assert.AreEqual(CompareFunction.LessEqual, DepthTest(glow.sharedMaterial), "posed");
        }
    }
}
