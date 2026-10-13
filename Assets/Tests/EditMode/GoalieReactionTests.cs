using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The goalie character's Save Reactions (GoalieCharacter.React): what they look like, and that what the Shot measures
    /// (once, on the Shot frame, from the gear and body measured at the Round start) is the goalie as it ends up.
    /// </summary>
    public class GoalieReactionTests
    {
        const float NearIce = 0.12f;
        GameObject _parent;

        [SetUp]
        public void SetUp() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_parent); }

        static Vector3 NetPoint(double x, double y)
        {
            NetFaceMapping.ToMetres(x, y, out double right, out double up);
            return new Vector3((float)right, (float)up, 0f);
        }

        /// <summary>Save points for every reaction: corners, edges and middles (Net Face units).</summary>
        static readonly (double x, double y)[] Saves =
        {
            (95, 95), (85, 60), (60, 90), (5, 95), (15, 50), (40, 90), // glove, blocker
            (5, 5), (20, 30), (95, 8), (80, 35), (45, 25), // pads, low and a little higher
            (50, 4), (40, 9), (60, 6), // stick
            (50, 45), (40, 70), (60, 60), // body
        };

        static readonly Vector3[] Eyes = { new Vector3(0f, 1.5f, -9f), new Vector3(-2.7f, 1.5f, -8.6f), new Vector3(2.7f, 1.5f, -8.6f), new Vector3(0f, 1.5f, -3f) };

        [Test]
        public void Every_save_reaction_keeps_the_goalie_on_the_ice_in_front_of_the_net_looking_like_a_real_goalie()
        {
            var character = new GoalieCharacter(_parent.transform);
            character.Show(true);
            Transform root = character.Root.transform;
            foreach (Vector3 eye in Eyes)
            foreach (var (x, y) in Saves)
            {
                float facing = GoalieCharacter.FacingToward(eye);
                SaveReaction reaction = SaveReactions.For(x, y);
                string where = reaction + " (" + x + ", " + y + ") from " + eye + ": ";
                character.React(reaction, eye, NetPoint(x, y), facing, true);
                character.ShowReaction(1f, facing);

                Assert.Less(character.BodyBounds().max.z, 0f, where + "in front of the Net Face (a butterfly pushed to a post comes right back to it)");
                float lowest = float.MaxValue;
                foreach (Renderer r in character.Root.GetComponentsInChildren<Renderer>(true))
                    if (r.name.Contains("Pad") || r.name.Contains("Skate"))
                        lowest = Mathf.Min(lowest, LowestY(r));
                // On the ice: the pads' rounded bottoms may sink a little into it, as in the Ready Stance (skates on the ice).
                Assert.That(lowest, Is.InRange(-0.035f, 0.03f), where + "pads or skates on the ice");
                // The stick save keeps its blade on the ice; a pad save keeps it low, over the five-hole.
                if (reaction == SaveReaction.Stick || reaction == SaveReaction.Pads)
                    Assert.That(LowestY(Gear(character, GearPiece.Stick)), Is.InRange(-0.03f, reaction == SaveReaction.Stick ? NearIce : 0.2f), where + "stick blade on or near the ice");
                Vector3 neck = character.Bone(HumanBodyBones.Neck).position, head = character.Bone(HumanBodyBones.Head).position;
                Assert.Less(Vector3.Angle(head - neck, Vector3.up), 45f, where + "head up, at most tipped to watch the puck");
                // In the goalie's own frame its left (the catcher) is +x. Neither hand reaches across the chest (in a pad save the
                // blocker hand may come in a little further, holding the stick blade over the five-hole).
                float chest = root.InverseTransformPoint(character.Bone(HumanBodyBones.Chest).position).x;
                float across = reaction == SaveReaction.Pads ? 0.1f : 0.05f;
                Assert.Less(root.InverseTransformPoint(character.Bone(HumanBodyBones.RightHand).position).x, chest + across, where + "blocker stays on the goalie's right");
                Assert.Greater(root.InverseTransformPoint(character.Bone(HumanBodyBones.LeftHand).position).x, chest - 0.05f, where + "catcher stays on the goalie's left");
            }
        }

        [Test]
        public void A_low_pad_save_drops_into_the_butterfly_and_a_higher_one_is_made_standing()
        {
            var character = new GoalieCharacter(_parent.transform);
            character.Show(true);
            Vector3 eye = Eyes[0];
            character.React(SaveReaction.Pads, eye, NetPoint(15, 8), 0f, true);
            character.ShowReaction(1f);
            float lowHips = character.Bone(HumanBodyBones.Hips).position.y;
            character.React(SaveReaction.Pads, eye, NetPoint(15, 35), 0f, true);
            character.ShowReaction(1f);
            float highHips = character.Bone(HumanBodyBones.Hips).position.y;
            Assert.Less(lowHips, highHips - 0.15f, "down on the knees for the low one");
            Assert.That(LowestY(Gear(character, GearPiece.RightPad)), Is.InRange(-0.035f, 0.03f), "the standing pad is on the ice");
        }

        // The pieces that are hard gear; the rest (body, clothes, mask, visor) is "soft" and measured stiff on each bone.
        static readonly string[] HardGear =
            { GearPiece.LeftPad, GearPiece.RightPad, GearPiece.Blocker, GearPiece.Catcher, GearPiece.Stick, GearPiece.LeftSkate, GearPiece.RightSkate };

        /// <summary>True when the first thing the line meets (at `at`) is not hard gear, so Body, mask or visor is nearer.</summary>
        static bool FirstHitIsSoft(GoalieShape shape, Vector3 from, Vector3 to, Vector3 at)
        {
            return !shape.Cast(from, to, 0.01f, out Vector3 atGear, out _, HardGear)
                || Vector3.Distance(from, atGear) > Vector3.Distance(from, at) + 1e-4f;
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void What_the_Shot_measures_is_the_goalie_as_the_reaction_ends_at_every_facing()
        {
            var character = new GoalieCharacter(_parent.transform);
            character.Show(true);
            var failures = new List<string>();
            var bodyOff = new List<string>();
            int lines = 0;
            foreach (Vector3 eye in Eyes)
            foreach (var (x, y) in Saves)
            {
                float facing = GoalieCharacter.FacingToward(eye);
                Vector3 net = NetPoint(x, y);
                character.ShowReaction(0f, facing * 0.5f); // the Ready Stance, part turned, on show when the Shot comes
                character.React(SaveReactions.For(x, y), eye, net, facing, true);
                var measured = new GoalieShape();
                character.AddReaction(measured);
                var placed = new Bounds[GearPiece.Gear.Length];
                for (int i = 0; i < placed.Length; i++) Assert.IsTrue(character.ReactionGearBounds(GearPiece.Gear[i], out placed[i]));

                character.ShowReaction(1f, facing);
                var fresh = new GoalieShape();
                character.AddTo(fresh);
                string where = "(" + x + ", " + y + ") from " + eye + ": ";
                for (int i = 0; i < placed.Length; i++)
                {
                    Assert.IsTrue(character.GearBounds(GearPiece.Gear[i], out Bounds b));
                    if (Vector3.Distance(b.min, placed[i].min) + Vector3.Distance(b.max, placed[i].max) > 0.002f)
                        failures.Add(where + GearPiece.Gear[i] + " measured at " + placed[i].center.ToString("F3") + ", shown at " + b.center.ToString("F3"));
                }
                // Lines through the goalie around the Shot point meet the measured goalie where they meet the real one: the
                // gear exactly. The body is measured stiff on each bone, so where a reaching arm bends at the elbow or shoulder
                // its sleeve is a few centimetres off: within 6 cm, or off on at most 1% of the lines.
                for (float dx = -0.3f; dx <= 0.31f; dx += 0.15f)
                for (float dy = -0.3f; dy <= 0.31f; dy += 0.15f)
                {
                    lines++;
                    Vector3 to = net + new Vector3(dx, dy, 0f);
                    bool a = measured.Cast(eye, to, 0.01f, out Vector3 atMeasured, out _);
                    bool b = fresh.Cast(eye, to, 0.01f, out Vector3 atFresh, out _);
                    string line = where + "line to " + to.ToString("F2") + ": ";
                    bool bodyM = a && FirstHitIsSoft(measured, eye, to, atMeasured), bodyF = b && FirstHitIsSoft(fresh, eye, to, atFresh);
                    bool body = bodyF || bodyM;
                    if (a != b) { (bodyM || bodyF ? bodyOff : failures).Add(line + "hit " + a + " measured, " + b + " shown"); continue; }
                    if (!a) continue;
                    float off = Vector3.Distance(atMeasured, atFresh);
                    if (off > 0.002f && !body) failures.Add(line + "gear " + off.ToString("F3") + " m apart");
                    else if (off > 0.06f) bodyOff.Add(line + "body " + off.ToString("F3") + " m apart");
                }
            }
            Assert.IsEmpty(failures, failures.Count + ":\n" + string.Join("\n", failures));
            Assert.LessOrEqual(bodyOff.Count, lines / 100, bodyOff.Count + " of " + lines + " lines:\n" + string.Join("\n", bodyOff));
        }

        [Test]
        public void Reacting_leaves_the_character_as_shown()
        {
            var character = new GoalieCharacter(_parent.transform);
            character.Show(true);
            character.ShowReaction(0.25f, 12f);
            var before = Snapshot(character);
            character.React(SaveReaction.Pads, Eyes[1], NetPoint(10, 10), 17f, true);
            character.React(SaveReaction.Glove, Eyes[2], NetPoint(90, 50), -17f, false);
            foreach (var kv in Snapshot(character))
            {
                Assert.AreEqual(0f, Vector3.Distance(before[kv.Key].position, kv.Value.position), 1e-5f, kv.Key.name);
                Assert.AreEqual(0f, Quaternion.Angle(before[kv.Key].rotation, kv.Value.rotation), 0.01f, kv.Key.name);
            }
        }

        [Test]
        public void The_pads_blocker_and_catcher_are_close_to_their_real_size_in_the_model_file()
        {
            var character = new GoalieCharacter(_parent.transform);
            GameObject file = ModelFiles.Load(ModelFiles.GoalieDressing, () => null);
            try
            {
                foreach (string piece in new[] { GearPiece.LeftPad, GearPiece.RightPad, GearPiece.Blocker, GearPiece.Catcher })
                {
                    Vector3 posed = Gear(character, piece).transform.localScale, real = Find(file.transform, GearPiece.ObjectName(piece)).localScale;
                    for (int axis = 0; axis < 3; axis++)
                        Assert.LessOrEqual(posed[axis] / real[axis], 1.15f + 1e-4f, piece + " is at most 15% bigger than in the model file");
                }
            }
            finally { Object.DestroyImmediate(file); }
        }

        [Test]
        public void With_no_model_reacting_and_posing_do_nothing()
        {
            var character = new GoalieCharacter(_parent.transform, name => null);
            Assert.DoesNotThrow(() => character.React(SaveReaction.Glove, Eyes[0], NetPoint(90, 90), 0f, true));
            Assert.DoesNotThrow(() => character.ShowReaction(1f));
            Assert.IsNull(character.ReactionPiece);
            Assert.IsFalse(character.ReactionGearBounds(GearPiece.Catcher, out _));
        }

        static Renderer Gear(GoalieCharacter character, string name) =>
            Find(character.Root.transform, GearPiece.ObjectName(name)).GetComponent<Renderer>();

        static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            Assert.Fail("no " + name);
            return null;
        }

        static float LowestY(Renderer r)
        {
            Matrix4x4 m = r.transform.localToWorldMatrix;
            float lowest = float.MaxValue;
            foreach (Vector3 v in r.GetComponent<MeshFilter>().sharedMesh.vertices) lowest = Mathf.Min(lowest, m.MultiplyPoint3x4(v).y);
            return lowest;
        }

        static Dictionary<Transform, (Vector3 position, Quaternion rotation)> Snapshot(GoalieCharacter character)
        {
            var snap = new Dictionary<Transform, (Vector3, Quaternion)>();
            foreach (Transform t in character.Root.GetComponentsInChildren<Transform>(true))
                snap[t] = (t.position, t.rotation);
            return snap;
        }
    }
}
