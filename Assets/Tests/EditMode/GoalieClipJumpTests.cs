using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// #212: the play-check "jump" is the fastest the goalie's hips move between two frames (limit 6 m/s; the goalie walks about
    /// 2 m/s). A Save clip popped the hips 23 cm in one frame (12 m/s) twice in a played Shot. Here the real goalie is posed by
    /// the real GoalieClipReactions.Post, driven by a real Round into each Save and Goal moment, at 60 fps; the code pose is put
    /// back before every frame. No clip may move the hips faster than 6 m/s.
    /// </summary>
    public class GoalieClipJumpTests
    {
        const float Frame = 1f / 60f;
        const float LimitMetresPerSecond = 6f;

        GameObject _parent;

        [SetUp]
        public void SetUp() { _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void TearDown() { if (_parent != null) Object.DestroyImmediate(_parent); }

        [Test, Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void No_Save_or_Goal_clip_moves_the_hips_faster_than_the_play_check_limit()
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/AIClips")
                || UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(GoalieClipReactions.ControllerPath) == null)
                Assert.Ignore("Assets/AIClips is missing (the AI clips are never committed): nothing to measure.");

            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "needs the goalie model");
            character.Show(true);
            Animator animator = character.Animator;
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var settings = new RoundSettings { ReplayOn = false };
            var clips = new GoalieClipReactions(animator);
            clips.UseRandom(new SeededRandom(1));
            var bones = new List<Transform>();
            var rot = new List<Quaternion>();
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                Transform t = animator.GetBoneTransform((HumanBodyBones)i);
                if (t == null) continue;
                bones.Add(t);
                rot.Add(t.localRotation);
            }
            Vector3 hipsLocal = hips.localPosition;
            var worst = new Dictionary<string, float>();
            var worstAt = new Dictionary<string, string>();

            void Play(Round round)
            {
                int starts = clips.Starts;
                bool have = false;
                Vector3 last = Vector3.zero;
                for (int i = 0; i < 900; i++)
                {
                    for (int b = 0; b < bones.Count; b++) bones[b].localRotation = rot[b];
                    hips.localPosition = hipsLocal;
                    round.Tick(Frame);
                    clips.Pre();
                    clips.Post(round, true, Frame);
                    if (clips.Starts == starts) continue;
                    if (!clips.Active) return;
                    Vector3 p = hips.position;
                    if (have)
                    {
                        float speed = Vector3.Distance(p, last) / Frame;
                        string name = clips.LastClipName;
                        if (!worst.TryGetValue(name, out float old) || speed > old)
                        {
                            worst[name] = speed;
                            worstAt[name] = $"frame {i}, hips y {last.y:F3} -> {p.y:F3}";
                        }
                    }
                    else if (!worst.ContainsKey(clips.LastClipName)) worst[clips.LastClipName] = 0f;
                    last = p;
                    have = true;
                }
            }

            Round NewRound()
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

            for (int g = 0; g < 60; g++)
            {
                bool all = true;
                foreach (string name in GoalieClipReactions.GoalClipFiles) all &= worst.ContainsKey(name);
                if (all) break;
                Round round = NewRound();
                round.SubmitShot(g % 2 == 0 ? 80 : 20, 50, 90, false);
                round.Whistle(ShotResultKind.Goal);
                Play(round);
            }
            double[,] spots = { { 10, 90 }, { 90, 90 }, { 50, 50 }, { 50, 10 }, { 10, 10 }, { 90, 10 }, { 50, 90 }, { 25, 70 }, { 75, 70 } };
            for (int s = 0; s < 100; s++)
            {
                bool all = true;
                foreach (string name in GoalieClipReactions.SaveClips) all &= worst.ContainsKey(name);
                if (all) break;
                int spot = s % spots.GetLength(0);
                Round round = NewRound();
                round.SubmitShot(spots[spot, 0], spots[spot, 1], 90, false);
                round.Whistle(ShotResultKind.Save);
                Play(round);
            }

            var problems = new List<string>();
            var report = new System.Text.StringBuilder();
            var expected = new List<string>(GoalieClipReactions.GoalClipFiles);
            expected.AddRange(GoalieClipReactions.SaveClips);
            foreach (string name in expected)
            {
                if (!worst.TryGetValue(name, out float speed)) { problems.Add(name + ": never played"); continue; }
                report.AppendLine($"{name}: fastest hips {speed:F2} m/s ({(worstAt.TryGetValue(name, out string at) ? at : "-")})");
                if (speed > LimitMetresPerSecond) problems.Add($"{name}: hips {speed:F2} m/s ({worstAt[name]}), limit {LimitMetresPerSecond}");
            }
            Debug.Log("[ClipJump]\n" + report);
            Assert.IsEmpty(problems, string.Join("\n", problems) + "\n" + report);
        }
    }
}
