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

        /// <summary>Whether a triangle's corners say bare left hand (the skin's, by the first corner's heaviest bone) or foot (a corner at least half on a foot bone).</summary>
        static bool IsHandOrFoot(GoalieCharacter character, SkinnedMeshRenderer r, BoneWeight[] w, int a, int b, int c, out bool hand)
        {
            Transform[] bones = r.bones;
            Transform leftHand = character.Bone(HumanBodyBones.LeftHand), lf = character.Bone(HumanBodyBones.LeftFoot), rf = character.Bone(HumanBodyBones.RightFoot);
            Transform main = bones[w[a].boneIndex0];
            hand = r.name == "Skin" && main != null && main.IsChildOf(leftHand);
            foreach (int v in new[] { a, b, c })
            {
                float foot = 0f;
                int[] index = { w[v].boneIndex0, w[v].boneIndex1, w[v].boneIndex2, w[v].boneIndex3 };
                float[] weight = { w[v].weight0, w[v].weight1, w[v].weight2, w[v].weight3 };
                for (int k = 0; k < 4; k++)
                    if (weight[k] > 0f && bones[index[k]] != null && (bones[index[k]].IsChildOf(lf) || bones[index[k]].IsChildOf(rf))) foot += weight[k];
                if (foot >= 0.5f) return true;
            }
            return hand;
        }

        [Test]
        public void The_glove_hand_and_feet_are_hidden_so_they_never_show_through_the_gear()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "needs the goalie model");
            int shownDrawn = 0, hiddenDrawn = 0, handRemoved = 0, footRemoved = 0;
            foreach (bool show in new[] { true, false })
            {
                character.ShowUnderGear(show);
                foreach (var r in character.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    BoneWeight[] w = r.sharedMesh.boneWeights;
                    int[] t = r.sharedMesh.triangles;
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        if (!IsHandOrFoot(character, r, w, t[i], t[i + 1], t[i + 2], out bool hand)) continue;
                        if (show) { if (hand) handRemoved++; else footRemoved++; shownDrawn++; }
                        else hiddenDrawn++;
                    }
                }
            }
            character.ShowUnderGear(false);
            Assert.AreEqual(0, hiddenDrawn, "no hand or foot triangle is drawn by default");
            Assert.Greater(handRemoved, 0, "the left hand was there to hide");
            Assert.Greater(footRemoved, 0, "the feet were there to hide (not just the hand)");
            Assert.AreEqual(shownDrawn, handRemoved + footRemoved);
        }

        /// <summary>Every hit shape's kind, place and size, then the first hits of five lines through the reaction's goalie as measured.</summary>
        static List<float> HitsAndReach(GoalieCharacter character, GoalieView view, Vector3 eye, Vector3 net, bool reactionShape)
        {
            view.MoveHitShapes();
            var values = new List<float>();
            Transform root = character.Root.transform.parent.Find(GoalieHitShapes.RootName);
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                values.Add(c.GetType().GetHashCode());
                values.AddRange(new[] { c.transform.position.x, c.transform.position.y, c.transform.position.z });
                Quaternion q = c.transform.rotation;
                values.AddRange(new[] { q.x, q.y, q.z, q.w });
                if (c is BoxCollider box) { values.AddRange(new[] { box.size.x, box.size.y, box.size.z, box.center.x, box.center.y, box.center.z }); }
                else if (c is SphereCollider s) { values.AddRange(new[] { s.radius, s.center.x, s.center.y, s.center.z }); }
                else if (c is CapsuleCollider cap) { values.AddRange(new[] { cap.radius, cap.height, cap.center.x, cap.center.y, cap.center.z }); }
            }
            var shape = new GoalieShape();
            if (reactionShape) character.AddReaction(shape); else character.AddTo(shape);
            foreach (Vector2 d in new[] { new Vector2(0f, 0f), new Vector2(0.3f, 0.2f), new Vector2(-0.3f, 0.2f), new Vector2(0.2f, -0.2f), new Vector2(-0.2f, -0.2f) })
            {
                bool hit = shape.Cast(eye, net + new Vector3(d.x, d.y, 0f), 0.01f, out Vector3 at, out _);
                values.AddRange(new[] { hit ? 1f : 0f, hit ? at.x : 0f, hit ? at.y : 0f, hit ? at.z : 0f });
            }
            return values;
        }

        [Test]
        public void Hiding_the_hands_and_feet_changes_no_gear_hit_shape_or_reach()
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "needs the goalie model");
            var view = new GoalieView(_parent.transform, character);
            try
            {
                view.Show(true);
                Vector3 eye = Eyes[0];
                var done = new HashSet<SaveReaction>();
                character.ShowReaction(0f);
                var failures = new List<string>();
                void Compare(string name, Vector3 net, bool reaction)
                {
                    character.ShowUnderGear(false);
                    List<float> hidden = HitsAndReach(character, view, eye, net, reaction);
                    character.ShowUnderGear(true);
                    List<float> shown = HitsAndReach(character, view, eye, net, reaction);
                    character.ShowUnderGear(false);
                    if (hidden.Count != shown.Count) { failures.Add(name + ": " + hidden.Count + " values hidden, " + shown.Count + " shown"); return; }
                    for (int i = 0; i < hidden.Count; i++)
                        if (Mathf.Abs(hidden[i] - shown[i]) > 1e-5f) { failures.Add(name + ": value " + i + " differs " + hidden[i] + " vs " + shown[i]); return; }
                }
                Compare("Ready Stance", NetPoint(50, 50), false);
                foreach (var (x, y) in Saves)
                {
                    SaveReaction reaction = SaveReactions.For(x, y);
                    if (!done.Add(reaction)) continue;
                    character.React(reaction, eye, NetPoint(x, y), 0f, true);
                    character.ShowReaction(1f);
                    Compare(reaction.ToString(), NetPoint(x, y), true);
                }
                Assert.IsEmpty(failures, string.Join("\n", failures));
                Assert.Greater(done.Count, 3, "several Save Reactions were compared");
            }
            finally { view.Dispose(); }
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
