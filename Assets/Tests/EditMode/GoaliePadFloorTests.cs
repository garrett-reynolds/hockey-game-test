using System.Text;
using HockeyGame;
using HockeyGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// #208: in the skate push, SitLower and Stance Life the pads must not sink under the ice. The lowest point of each pad stays
    /// within 1 cm of where it sits in the Ready Stance (the skates are checked by GoalieSkatePushTests and the #206 play-check).
    /// </summary>
    public class GoaliePadFloorTests
    {
        const float LimitMetres = 0.01f;

        GameObject _rink;
        GoalieCharacter _goalie;
        float _readyLeft, _readyRight;

        [SetUp]
        public void SetUp()
        {
            _rink = new GameObject("Rink (test)");
            _goalie = new GoalieCharacter(_rink.transform);
            Assert.IsTrue(_goalie.Present, "Assets/Resources/Models/goalie-dressing.fbx is missing");
            _goalie.Show(true);
            _goalie.ShowReaction(0f);
            _goalie.GearBounds(GearPiece.LeftPad, out Bounds l);
            _goalie.GearBounds(GearPiece.RightPad, out Bounds r);
            _readyLeft = l.min.y - _goalie.Root.transform.position.y;
            _readyRight = r.min.y - _goalie.Root.transform.position.y;
        }

        [TearDown]
        public void TearDown() { RinkTeardown.Destroy(_rink); }

        /// <summary>How far the lowest pad point sits below the Ready Stance floor now (metres; negative: above it).</summary>
        float Depth()
        {
            _goalie.GearBounds(GearPiece.LeftPad, out Bounds l);
            _goalie.GearBounds(GearPiece.RightPad, out Bounds r);
            float y = _goalie.Root.transform.position.y;
            return Mathf.Max(_readyLeft - (l.min.y - y), _readyRight - (r.min.y - y));
        }

        [Test]
        public void The_pads_stay_within_1_cm_of_the_Ready_Stance_floor_in_every_push_sit_and_live_pose()
        {
            var bad = new StringBuilder();
            int poses = 0, failed = 0;
            float worst = 0f;
            void Check(string what)
            {
                poses++;
                float d = Depth();
                worst = Mathf.Max(worst, d);
                if (d > LimitMetres) { failed++; if (failed <= 12) bad.AppendLine(what + ": " + (d * 100f).ToString("0.0") + " cm under"); }
            }

            foreach (Skate skate in new[] { Skate.Left, Skate.Right })
            foreach (PushKind kind in new[] { PushKind.Shuffle, PushKind.TPush })
            foreach (float push in new[] { 0f, 0.5f, 1f })
            foreach (float turn in new[] { 0f, 0.5f, 1f })
            foreach (float sit in new[] { 0f, 0.03f })
            foreach (float live in new[] { 0f, 1f, -1f })
            {
                _goalie.ShowReaction(0f);
                _goalie.ShowPush(new PushShown(kind, skate, PushPhase.Push, 1), new PushPoseMoment(push, turn));
                _goalie.SitLower(sit);
                _goalie.Live(new StanceLifeMoment(1, live, 0), 1f);
                Check(skate + " " + kind + " out " + push + " turn " + turn + " sit " + sit + " live " + live);
            }
            TestContext.Out.WriteLine("pad floor sweep: " + poses + " poses, " + failed + " too deep, worst " + (worst * 100f).ToString("0.00") + " cm");
            Assert.AreEqual(0, failed, failed + " of " + poses + " poses sink a pad more than 1 cm (worst " + (worst * 100f).ToString("0.0") + " cm):\n" + bad);
        }
    }
}
