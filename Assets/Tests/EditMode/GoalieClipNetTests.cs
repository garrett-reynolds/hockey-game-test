using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// Garrett, 2026-10-14: "after the goalie starts its animation after the shot, there is a possibility the goalie clips the
    /// net which we need to avoid." Every Goal and Save clip plays through the real Round and GoalieClipReactions.Post with the
    /// goalie on the spots he has after a Shot: the deepest (a Depth Set-up, on the goal line) and near each post (a Cheat or
    /// Late Settle plus the Save slide). The hips, spine, chest, head, both hands, both skates and the stick blade must not go
    /// behind the goal line inside the net, or into a post or the crossbar, by more than 2 cm. The net is measured here from
    /// NetFrame's sizes (Net Face at z = 0, the posts and crossbar centred on it), not from the class under test.
    /// </summary>
    public class GoalieClipNetTests
    {
        const float Frame = 1f / 30f;
        const float Tolerance = 0.02f;
        const float HalfW = (float)NetFrame.FaceWidthMetres / 2f, H = (float)NetFrame.FaceHeightMetres, T = (float)NetFrame.ThicknessMetres;

        static readonly HumanBodyBones[] Checked =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head, HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes
        };

        // The goalie's hips spot after a Shot (rink metres, x across, z out from the Net Face; -z is toward the shooter).
        static readonly Vector2[] Spots = { new Vector2(0f, 0f), new Vector2(-0.6f, 0f), new Vector2(0.6f, 0f), new Vector2(-0.9f, -0.2f), new Vector2(0.9f, -0.2f) };

        GameObject _parent;

        [SetUp]
        public void SetUp() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void TearDown() { if (_parent != null) Object.DestroyImmediate(_parent); }

        /// <summary>How far the point is into the net (behind the goal line inside the opening) or into a post or the
        /// crossbar (metres; 0 = clear).</summary>
        internal static float IntoNet(Vector3 p)
        {
            float into = 0f;
            float ax = Mathf.Abs(p.x);
            if (ax <= HalfW && p.y >= 0f && p.y <= H) into = Mathf.Max(into, p.z);
            // a post (|x| HalfW..HalfW+T, up to the crossbar top) or the crossbar (y H..H+T), each z -T/2..T/2
            if (p.z > -T / 2f && p.z < T / 2f && p.y >= 0f && p.y < H + T && ax < HalfW + T)
            {
                bool inPost = ax > HalfW, inBar = p.y > H;
                if (inPost || inBar)
                {
                    float d = Mathf.Min(p.z + T / 2f, T / 2f - p.z, HalfW + T - ax, H + T - p.y);
                    if (inPost && !inBar) d = Mathf.Min(d, ax - HalfW);
                    if (inBar && !inPost) d = Mathf.Min(d, p.y - H);
                    into = Mathf.Max(into, d);
                }
            }
            return Mathf.Max(0f, into);
        }

        sealed class Body
        {
            readonly Animator _animator;
            readonly List<Transform> _bones = new List<Transform>();
            readonly List<Quaternion> _rot = new List<Quaternion>();
            readonly Transform _hips;
            readonly Vector3 _hipsPos;
            readonly Transform _stick;
            readonly List<Vector3> _blade = new List<Vector3>(); // stick-local: its lowest 4 cm in the code pose

            public Body(Animator animator, GameObject root)
            {
                _animator = animator;
                _hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                {
                    Transform t = animator.GetBoneTransform((HumanBodyBones)i);
                    if (t == null) continue;
                    _bones.Add(t);
                    _rot.Add(t.localRotation);
                }
                _hipsPos = _hips.localPosition;
                foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
                    if (GearPiece.PieceOf(r.name) == GearPiece.Stick) _stick = r.transform;
                if (_stick == null) return;
                Vector3[] v = _stick.GetComponent<MeshFilter>().sharedMesh.vertices;
                float lowest = float.MaxValue;
                foreach (Vector3 a in v) lowest = Mathf.Min(lowest, _stick.TransformPoint(a).y);
                foreach (Vector3 a in v) if (_stick.TransformPoint(a).y < lowest + 0.04f) _blade.Add(a);
            }

            public bool HasStick => _stick != null;

            public void PutBack()
            {
                for (int i = 0; i < _bones.Count; i++) _bones[i].localRotation = _rot[i];
                _hips.localPosition = _hipsPos;
            }

            /// <summary>The deepest point into the net this frame, and its name.</summary>
            public (float into, string what) Worst()
            {
                float worst = 0f;
                string what = "";
                foreach (HumanBodyBones b in Checked)
                {
                    Transform t = _animator.GetBoneTransform(b);
                    if (t == null) continue;
                    float d = IntoNet(t.position);
                    if (d > worst) { worst = d; what = b.ToString(); }
                }
                if (_stick != null)
                    foreach (Vector3 a in _blade)
                    {
                        float d = IntoNet(_stick.TransformPoint(a));
                        if (d > worst) { worst = d; what = "stick blade"; }
                    }
                return (worst, what);
            }
        }

        [Test]
        public void The_net_test_measures_a_point_behind_the_line_and_in_a_post()
        {
            Assert.AreEqual(0f, IntoNet(new Vector3(0f, 0.5f, -0.1f)), 1e-5, "in front of the goal line");
            Assert.AreEqual(0.1f, IntoNet(new Vector3(0f, 0.5f, 0.1f)), 1e-5, "10 cm behind the goal line, in the opening");
            Assert.AreEqual(0f, IntoNet(new Vector3(1.5f, 0.5f, 0.1f)), 1e-5, "beside the net");
            Assert.Greater(IntoNet(new Vector3(HalfW + T / 2f, 0.5f, 0f)), 0.03f, "in the middle of a post");
            Assert.Greater(IntoNet(new Vector3(0f, H + T / 2f, 0f)), 0.03f, "in the middle of the crossbar");
        }

        [Test, Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void No_Goal_or_Save_clip_puts_the_goalie_into_the_net()
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/AIClips")
                || UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(GoalieClipReactions.ControllerPath) == null)
                Assert.Ignore("Assets/AIClips is missing (the AI clips are never committed): nothing to measure.");

            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "needs the goalie model");
            character.Show(true);
            Animator animator = character.Animator;
            var settings = new RoundSettings { ReplayOn = false };
            var clips = new GoalieClipReactions(animator) { Rink = _parent.transform };
            clips.UseRandom(new SeededRandom(1));
            var body = new Body(animator, character.Root);
            Assume.That(body.HasStick, "needs the goalie's stick");
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);

            var problems = new List<string>();
            var report = new System.Text.StringBuilder();
            foreach (Vector2 spot in Spots)
            {
                // Hips over the spot, as GoalieCharacter.PlaceRoot puts them.
                Transform root = character.Root.transform;
                Vector3 off = hips.position - root.position;
                root.position = new Vector3(spot.x - off.x, 0f, spot.y - off.z);
                body.PutBack();
                (float codeInto, string codeWhat) = body.Worst();
                report.AppendLine($"spot ({spot.x:F1}, {spot.y:F1}): code pose {codeInto * 100f:F1} cm into the net ({codeWhat})");

                var worst = new Dictionary<string, (float into, string what, float at)>();
                foreach (bool goal in new[] { true, false })
                {
                    string[] pool = goal ? GoalieClipReactions.GoalClips : GoalieClipReactions.SaveClips;
                    for (int n = 0; n < 12; n++)
                    {
                        bool all = true;
                        foreach (string name in pool) all &= worst.ContainsKey(name);
                        if (all) break;
                        Round round = NewRound(character, settings, clips);
                        round.SubmitShot(goal ? 50 : 10, goal ? 50 : 90, 90, false);
                        round.Whistle(goal ? ShotResultKind.Goal : ShotResultKind.Save);
                        Play(clips, round, body, worst);
                    }
                    foreach (string name in pool)
                    {
                        if (!worst.TryGetValue(name, out var w)) { problems.Add($"spot ({spot.x:F1}, {spot.y:F1}) {name}: never played"); continue; }
                        report.AppendLine($"  {name}: {w.into * 100f:F1} cm into the net ({w.what}, at {w.at:F2} s)");
                        if (w.into > Tolerance) problems.Add($"spot ({spot.x:F1}, {spot.y:F1}) {name}: {w.what} {w.into * 100f:F1} cm into the net at {w.at:F2} s (limit {Tolerance * 100f:F0})");
                    }
                }
            }
            Debug.Log("[ClipNet]\n" + report);
            Assert.IsEmpty(problems, string.Join("\n", problems) + "\n" + report);
        }

        static void Play(GoalieClipReactions clips, Round round, Body body, Dictionary<string, (float into, string what, float at)> worst)
        {
            int starts = clips.Starts;
            float clock = 0f;
            for (int i = 0; i < 600; i++)
            {
                body.PutBack();
                round.Tick(Frame);
                clips.Pre();
                clips.Post(round, true, Frame);
                if (clips.Starts == starts) { if (clock > 5f) return; clock += Frame; continue; }
                if (!clips.Active) return;
                (float into, string what) = body.Worst();
                var w = worst.TryGetValue(clips.LastClipName, out var old) ? old : (0f, "", 0f);
                if (into >= w.Item1 && (into > 0f || w.Item2 == "")) w = (into, what, (i + 1) * Frame);
                worst[clips.LastClipName] = w;
            }
        }

        static Round NewRound(GoalieCharacter character, RoundSettings settings, GoalieClipReactions clips)
        {
            var round = new Round(new SeededRandom(11), settings, new GoalieCharacterReach(character, settings), 0);
            round.StartRound();
            for (int i = 0; i < 4000 && round.Phase != RoundPhase.ShootingWindow; i++)
            {
                round.Tick(0.05);
                clips.Post(round, true, 0.05f);
            }
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase, "the Round never reached the shooting window");
            return round;
        }
    }
}
