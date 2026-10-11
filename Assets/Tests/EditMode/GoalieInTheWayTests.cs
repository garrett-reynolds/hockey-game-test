using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using Triangle = HockeyGame.Core.Tests.GoalieTriangles.Triangle;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// A Save with gear already in the way (Reach in time, move time 0) shows the Shot point's Save Reaction (#61), and the
    /// puck still meets gear: the reaction keeps gear on, or ends with gear on, the puck's line (#63). Played on the real race
    /// and the built goalie character, over the Net Face from each shooter's eye.
    /// </summary>
    public class GoalieInTheWayTests
    {
        GameObject _rink;

        [SetUp]
        public void SetUp() => _rink = new GameObject("Rink (test)");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_rink);

        [Test]
        public void An_in_the_way_Save_stops_the_puck_on_gear()
        {
            var character = new GoalieCharacter(_rink.transform);
            var view = new GoalieView(_rink.transform, character);
            var settings = new RoundSettings { ReactionWobbleSeconds = 0 };
            var failures = new List<string>();
            int checkedSaves = 0;
            foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
            foreach (var (eyeName, _) in ShooterEyes.Of(side))
            for (int x = 5; x <= 95; x += 15)
            for (int y = 5; y <= 95; y += 15)
            {
                double windowSeconds = eyeName == "near" ? 2.9 : 0.0;
                var round = new Round(ScriptedRandom.Forcing(side), settings, new GoalieCharacterReach(character, settings), 0);
                round.StartRound();
                round.Tick(settings.SkateInSeconds + settings.SlowDownSeconds + windowSeconds);
                view.Show(true, round.GoaliePose);
                round.SubmitShot(x, y, 100, false);
                ShotRace race = round.LastRace.Value;
                if (!race.InTheWay) continue;
                Vector3 from = new Vector3((float)race.ShotStart.X, (float)race.ShotStart.Y, (float)race.ShotStart.Z);
                NetFaceMapping.ToMetres(x, y, out double right, out double up);
                var netPoint = new Vector3((float)right, (float)up, 0f);
                view.StandFor(race);
                view.React(round.LastResult.Value, from, race.SaveReaction, race.LateMarginSeconds, settings.LateMarginLimitSeconds);
                view.SetProgress(1f); // fully in the reaction: where the stop was measured
                string where = side + " " + eyeName + " (" + x + ", " + y + ") " + race.SaveReaction + ": ";
                if (!view.SaveStop(from, netPoint, PuckMotion.Thickness, out Vector3 stop)) { failures.Add(where + "no stop on the gear"); continue; }
                float nearest = float.MaxValue;
                foreach (Triangle t in GoalieTriangles.Near(GoalieTriangles.Of(character), new Bounds(stop, Vector3.zero), 0.1f))
                    nearest = Mathf.Min(nearest, Vector3.Distance(stop, ShotSpeedTests.ClosestPoint(t, stop)));
                if (nearest > 0.06f) failures.Add(where + "the puck stops " + nearest.ToString("F3") + " m from the gear (" + view.ReactionPiece + ")");
                else checkedSaves++;
            }
            Assert.IsEmpty(failures, failures.Count + " of " + (failures.Count + checkedSaves) + ":\n" + string.Join("\n", failures));
            Assert.Greater(checkedSaves, 0, "some Shots had gear already in the way");
        }
    }
}
