using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using Triangle = HockeyGame.Core.Tests.GoalieTriangles.Triangle;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The puck flies at the Shot's real speed (#46), played through as the game runs a frame: the Round, then ShotEffects,
    /// then the goalie's reaction from Breakaway.GoalieSet, timed by the race (#63). On the fastest Test Shot a Save still
    /// meets the gear, gear already in the way included, and a Goal's gear is still on its way as the puck passes.
    /// </summary>
    public class ShotSpeedTests
    {
        const float Frame = 1f / 60f;
        GameObject _rink, _camera;

        [SetUp]
        public void SetUp()
        {
            _rink = new GameObject("Rink (test)");
            _camera = new GameObject("Camera (test)", typeof(Camera));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_rink);
            Object.DestroyImmediate(_camera);
        }

        // The middle of each of the nine Zones, and Misses wide left, wide right and over the crossbar.
        static readonly (double x, double y)[] Spots =
        {
            (17, 17), (50, 17), (83, 17), (17, 50), (50, 50), (83, 50), (17, 83), (50, 83), (83, 83),
            (-10, 50), (110, 50), (50, 110),
        };

        // Both ways a Shot comes in. A Test Shot (a click) is taken after ShotEffects has run that frame, and RoundDriver then
        // runs it again with no time. A USB Shot is taken before ShotEffects runs, which then runs with the frame's real time.
        [TestCase(false, TestName = "At_200_kmh_a_Test_Shot_save_meets_the_gear_and_a_goal_or_miss_passes_clear_of_all_gear_from_every_Attack_Side")]
        [TestCase(true, TestName = "At_200_kmh_a_USB_Shot_save_meets_the_gear_and_a_goal_or_miss_passes_clear_of_all_gear_from_every_Attack_Side")]
        public void At_200_kmh_a_save_meets_the_gear_and_a_goal_or_miss_passes_clear_of_all_gear_from_every_Attack_Side(bool usb)
        {
            var failures = new List<string>();
            int saves = 0, clear = 0, inTheWay = 0, late = 0;
            var goalsBySide = new Dictionary<AttackSide, int>();
            foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
            foreach (var (eyeName, eye) in ShooterEyes.Of(side))
            foreach (var (x, y) in Spots)
            foreach (double reaction in new[] { 0.0, 0.12 }) // no Reaction Time (some Shots saved in the race), and today's (real Goals)
            {
                TearDown();
                SetUp();
                GoaliePose pose = GoaliePoses.PosesOf(side)[0]; // picks the Attack Side only: the real race decides the Shot
                string where = side + " " + eyeName + " (" + x + ", " + y + ") reaction " + reaction + " s ";
                // The Round's shot start is this eye's puck: the window opening, or drifted in as close as it goes (2.9 s in).
                double windowSeconds = eyeName == "near" ? 2.9 : 0.0;
                string failure = Shoot(eye, windowSeconds, reaction, pose, x, y, 200, usb, ref saves, ref clear, ref inTheWay, ref late, out bool goal);
                if (failure != null) failures.Add(where + failure);
                if (goal) goalsBySide[side] = (goalsBySide.TryGetValue(side, out int g) ? g : 0) + 1;
            }
            TestContext.Out.WriteLine(saves + " race-won saves, " + clear + " goals or misses clear of the gear, " + inTheWay + " in-the-way saves meeting the gear, " + late + " goals with the gear still on its way");
            Assert.IsEmpty(failures, failures.Count + " of " + (saves + clear + inTheWay + failures.Count) + ":\n" + string.Join("\n", failures));
            Assert.Greater(saves, 0, "some saves won in the race");
            Assert.Greater(inTheWay, 0, "some saves with gear already in the way");
            Assert.Greater(late, 0, "some goals the gear got to too late");
            foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
                Assert.IsTrue(goalsBySide.TryGetValue(side, out int goals) && goals > 0, side + ": some real Goals (gear not in the way, the race lost)");
        }

        /// <summary>One Shot played through to the puck's arrival. Null when it meets the gear (a save) or passes clear of it.</summary>
        string Shoot(Vector3 eye, double windowSeconds, double reactionSeconds, GoaliePose pose, double x, double y, double kmh, bool usb, ref int saves, ref int clear,
            ref int inTheWay, ref int late, out bool goal)
        {
            NetBuilder.Build(_rink.transform);
            _camera.transform.position = eye;
            _camera.transform.LookAt(new Vector3(0f, 0.6f, 0f));
            var character = new GoalieCharacter(_rink.transform);
            var view = new GoalieView(_rink.transform, character);
            var effects = new ShotEffects(_rink.transform, _camera.GetComponent<Camera>(), null, view);
            Transform puck = _rink.transform.Find("Puck");
            // The real race (Reach in time) on the goalie standing on his Set-up spot (#64).
            goal = false;
            var settings = new RoundSettings { ReactionSeconds = reactionSeconds, ReactionWobbleSeconds = 0 };
            var round = new Round(ScriptedRandom.Forcing(GoaliePoses.SideOf(pose)), settings, new GoalieCharacterReach(character, settings), 0);
            round.StartRound();
            round.Tick(settings.SkateInSeconds + settings.SlowDownSeconds + windowSeconds);
            RinkPoint shotStart = Breakaway.ShotStart(round.AttackSide, windowSeconds, settings);
            float facing = (float)SetUps.Facing(SetUps.Spot(round.SetUp, round.AttackSide, windowSeconds, shotStart, settings), shotStart);
            Transform cam = _camera.transform;
            Vector3 puckStart = cam.position + cam.forward * (float)Breakaway.PuckAheadMetres - cam.up * (float)Breakaway.PuckBelowMetres; // as ShotEffects
            if (Vector3.Distance(puckStart, new Vector3((float)shotStart.X, (float)shotStart.Y, (float)shotStart.Z)) > 0.02f)
                return "the race's shot start " + shotStart + " is not this eye's puck " + puckStart.ToString("F3");
            view.Show(true, round.GoaliePose, false);
            view.Face(facing, facing);
            view.SetProgress(0f);
            round.SubmitShot(x, y, kmh, false);
            ShotResultKind kind = round.LastResult.Value.Kind;
            goal = kind == ShotResultKind.Goal;
            // A Save with gear already in the way shows the Shot point's Save Reaction, still with gear on the puck's line.
            bool blocked = kind == ShotResultKind.Save && round.LastRace.Value.InTheWay;
            float spotZ = round.LastRace.HasValue ? (float)round.LastRace.Value.Spot.Z : 0f;
            bool lateGear = kind == ShotResultKind.Goal && !double.IsPositiveInfinity(round.LastRace.Value.MoveSeconds); // some gear reaches, too late
            // The Shot frame (the Round has ticked already): the puck starts and the Round learns its flight.
            effects.Update(round, usb ? Frame : 0f);
            view.SetProgress((float)Breakaway.GoalieSet(round.Phase, round.PhaseSecondsElapsed, true, round.ResultDelaySeconds, round.LastRace));
            Vector3 last = puck.position;
            float flown = 0f;
            for (int i = 0; i < 120; i++)
            {
                // The last frame ends just as the puck gets there, so it shows the puck at its contact, before any rebound.
                float left = (float)round.ResultDelaySeconds - flown;
                float dt = left > Frame ? Frame : left + 1e-5f;
                flown += dt;
                round.Tick(dt);
                effects.Update(round, dt);
                float set = (float)Breakaway.GoalieSet(round.Phase, round.PhaseSecondsElapsed, true, round.ResultDelaySeconds, round.LastRace);
                view.SetProgress(set);
                Vector3 now = puck.position;
                // A Goal: the race was lost, so his gear is still on its way when the puck passes him (at his spot's depth).
                if (lateGear && last.z < spotZ && now.z >= spotZ)
                {
                    if (set >= 0.999f) return "goal: the gear is already on the puck's line as the puck passes the goalie";
                    late++;
                }
                if (effects.PuckArrivedThisFrame)
                {
                    if (kind != ShotResultKind.Goal && set < 0.999f)
                        return kind + ": the goalie is only " + set.ToString("F2") + " into the reaction when the puck gets there";
                    if (kind != ShotResultKind.Save) return Clear(character, last, now, ref clear, "arriving");
                    // Touching the gear as shown when the puck gets there: not short of it, not where it is still going.
                    List<Triangle> near = GoalieTriangles.Near(GoalieTriangles.Of(character), new Bounds(now, Vector3.zero), 0.1f);
                    float nearest = float.MaxValue;
                    foreach (Triangle t in near) nearest = Mathf.Min(nearest, Vector3.Distance(now, ClosestPoint(t, now)));
                    if (nearest > 0.06f) return (blocked ? "in-the-way " : "") + "save: the gear is " + nearest.ToString("F3") + " m from the puck when it gets there";
                    if (blocked) inTheWay++;
                    else saves++;
                    return null;
                }
                if (kind != ShotResultKind.Save && now.z > -2.5f && GoalieTriangles.PuckTouches(GoalieTriangles.Of(character), last, now))
                    return kind + ": the puck goes through the gear on its way in (frame " + i + ")";
                last = now;
            }
            return "the puck never arrived";
        }

        /// <summary>The point of a triangle nearest p (Ericson, Real-Time Collision Detection 5.1.5).</summary>
        internal static Vector3 ClosestPoint(Triangle t, Vector3 p)
        {
            Vector3 ab = t.B - t.A, ac = t.C - t.A, ap = p - t.A;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return t.A;
            Vector3 bp = p - t.B;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return t.B;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return t.A + ab * (d1 / (d1 - d3));
            Vector3 cp = p - t.C;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return t.C;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return t.A + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f) return t.B + (t.C - t.B) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            float denom = 1f / (va + vb + vc);
            return t.A + ab * (vb * denom) + ac * (vc * denom);
        }

        static string Clear(GoalieCharacter character, Vector3 from, Vector3 to, ref int clear, string when)
        {
            if (GoalieTriangles.PuckTouches(GoalieTriangles.Of(character), from, to)) return "goal or miss: the puck touches the gear " + when;
            clear++;
            return null;
        }
    }
}
