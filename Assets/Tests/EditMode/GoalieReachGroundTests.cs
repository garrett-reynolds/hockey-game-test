using System.Text;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>#204: during a Save Reaction's reach move no skate sinks more than 3 cm under the ice and the hips never jump.</summary>
    public class GoalieReachGroundTests
    {
        const float SinkLimit = -0.03f, JumpLimit = 0.3f;
        static readonly RoundSettings Defaults = new RoundSettings();
        GameObject _parent;

        [SetUp]
        public void MakeRink() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void RemoveRink() { Object.DestroyImmediate(_parent); }

        static float LowestSkate(GoalieCharacter c)
        {
            float low = float.MaxValue;
            foreach (string s in new[] { GearPiece.LeftSkate, GearPiece.RightSkate })
                if (c.GearBounds(s, out Bounds b)) low = Mathf.Min(low, b.min.y);
            return low;
        }

        static Vector3 V(RinkPoint p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);

        [Test]
        public void No_skate_sinks_and_the_hips_do_not_jump_in_any_reach_move()
        {
            var c = new GoalieCharacter(_parent.transform);
            Assert.IsTrue(c.Present, "Assets/Resources/Models/goalie-dressing.fbx is missing");
            c.Show(true);
            c.Prepare();
            c.ShowReaction(0f);
            float ice = c.Root.transform.position.y;
            float worstSink = 0f, worstJump = 0f, worstRise = 0f;
            string sinkAt = "", jumpAt = "", perReaction = "";
            Vector3 readyHips = c.Bone(HumanBodyBones.Hips).position;
            foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
            {
                Vector3 from = V(Breakaway.ShotStart(side, 1.0, Defaults));
                for (int x = 5; x <= 95; x += 15)
                    for (int y = 5; y <= 95; y += 15)
                        foreach (SaveReaction reaction in new[] { SaveReaction.Glove, SaveReaction.Blocker, SaveReaction.Pads, SaveReaction.Stick, SaveReaction.Body })
                        {
                            Vector3 to = V(RinkPoint.OnNetFace(x, y));
                            float facing = (float)SetUps.Facing(Breakaway.ShotStart(side, 1.0, Defaults), RinkPoint.OnNetFace(x, y));
                            c.React(reaction, from, to, facing, true);
                            Vector3 last = Vector3.zero;
                            for (int i = 0; i <= 20; i++)
                            {
                                c.ShowReaction(i / 20f, facing);
                                float sink = LowestSkate(c) - ice;
                                Vector3 hips = c.Bone(HumanBodyBones.Hips).position;
                                string where = $"{side} {reaction} ({x},{y}) set {i / 20f:F2}";
                                if (sink < worstSink) { worstSink = sink; sinkAt = where; }
                                if (sink < SinkLimit && !perReaction.Contains(reaction + (i == 20 ? " end;" : " mid;"))) perReaction += reaction + (i == 20 ? " end;" : " mid;") + $"[{sink:F3} {where}] ";
                                worstRise = Mathf.Max(worstRise, hips.y - readyHips.y);
                                if (i > 0 && Vector3.Distance(hips, last) > worstJump) { worstJump = Vector3.Distance(hips, last); jumpAt = where; }
                                last = hips;
                            }
                        }
            }
            Assert.That(worstSink >= SinkLimit && worstJump <= JumpLimit, Is.True,
                $"worst skate sink {worstSink:F4} m at {sinkAt}; {perReaction} | worst hips step {worstJump:F4} m at {jumpAt} (rise {worstRise:F3})");
        }
    }
}
