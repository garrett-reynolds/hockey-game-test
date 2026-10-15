using System.Collections.Generic;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The AI clips (#173/#183) as Garrett sees them, with no Play: the real goalie character is posed by the real
    /// GoalieClipReactions.Post, driven by a real Round into its Goal, Save and Idle moments (the path the game takes). The
    /// code pose is put back before every frame, as the game's own pose code does. At every frame of every clip the body must
    /// face where the code pose faces (within 10 degrees) and the lowest foot must be no more than 1 cm below the code pose's.
    /// The measuring here is its own (upper-leg line and foot bones), not the numbers the class keeps.
    /// 2026-10-14: every picked clip played backwards (175-180 degrees) with the skates 34-52 cm under the ice.
    /// </summary>
    public class GoalieClipFacingTests
    {
        const float Frame = 1f / 30f;
        const float MaxFacingOffDegrees = 10f;
        const float MaxUnderIceMetres = 0.01f;

        GameObject _parent;
        static readonly System.Text.StringBuilder Diag = new System.Text.StringBuilder();

        [SetUp]
        public void SetUp() { Diag.Clear(); _parent = new GameObject("Rink (test)"); }

        [TearDown]
        public void TearDown() { if (_parent != null) Object.DestroyImmediate(_parent); }

        static readonly HumanBodyBones[] FootBones =
            { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes };

        sealed class Pose
        {
            readonly Animator _animator;
            readonly List<Transform> _bones = new List<Transform>();
            readonly List<Quaternion> _rot = new List<Quaternion>();
            readonly Transform _hips;
            readonly Vector3 _hipsPos;

            public Pose(Animator animator)
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
            }

            /// <summary>What the game's pose code does every frame before the clip's Post.</summary>
            public void PutBack()
            {
                for (int i = 0; i < _bones.Count; i++) _bones[i].localRotation = _rot[i];
                _hips.localPosition = _hipsPos;
            }

            public float Facing()
            {
                Vector3 right = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position
                                - _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
                Vector3 forward = Vector3.Cross(right, Vector3.up);
                return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            }

            public float LowestFoot()
            {
                float y = float.MaxValue;
                foreach (HumanBodyBones b in FootBones)
                {
                    Transform t = _animator.GetBoneTransform(b);
                    if (t != null) y = Mathf.Min(y, t.position.y);
                }
                return y;
            }
        }

        struct Worst
        {
            public float FacingOff;       // degrees
            public float FootBelow;       // metres under the code pose's lowest foot (positive = under)
            public int Frames;
            public float FacingAt, FootAt; // clip seconds of the worst frame
        }

        // Plays one moment frame by frame; adds the worst numbers per clip name.
        static void Play(GoalieClipReactions clips, Round round, Pose code, Animator animator, Dictionary<string, Worst> worst)
        {
            float codeFacing = code.Facing();
            float codeFoot = code.LowestFoot();
            int starts = clips.Starts;
            float clock = 0f;
            for (int i = 0; i < 600; i++)
            {
                code.PutBack();
                round.Tick(Frame);
                clips.Pre();
                clips.Post(round, true, Frame);
                if (clips.Starts == starts) { if (clock > 5f) { Diag.AppendLine($"[ClipFacing] no start: phase {round.Phase}, fallbacks {clips.Fallbacks}, left {round.ResultHoldSecondsLeft}, showing {round.ResultShowing}, result {round.LastResult}"); return; } clock += Frame; continue; }
                if (!clips.Active) return;
                Worst w = worst.TryGetValue(clips.LastClipName, out Worst old) ? old : default;
                float off = Mathf.Abs(Mathf.DeltaAngle(code.Facing(), codeFacing));
                float below = codeFoot - code.LowestFoot(); // the Pose reads the live bones
                float at = (i + 1) * Frame;
                w.Frames++;
                if (off > w.FacingOff) { w.FacingOff = off; w.FacingAt = at; }
                if (below > w.FootBelow) { w.FootBelow = below; w.FootAt = at; }
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
                clips.Post(round, true, 0.05f); // the game calls it every frame: it must see each phase change
            }
            Assert.AreEqual(RoundPhase.ShootingWindow, round.Phase, "the Round never reached the shooting window");
            return round;
        }

        [Test]
        public void Every_AI_clip_faces_the_code_pose_and_keeps_the_skates_on_the_ice()
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/AIClips")
                || UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(GoalieClipReactions.ControllerPath) == null)
                Assert.Ignore("Assets/AIClips is missing (the AI clips are never committed): nothing to measure.");

            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "needs the goalie model");
            character.Show(true); // the goalie is drawn (the clip does not run on a hidden one)
            Animator animator = character.Animator;
            var settings = new RoundSettings { ReplayOn = false };
            var clips = new GoalieClipReactions(animator);
            clips.UseRandom(new SeededRandom(1));
            var code = new Pose(animator);
            var worst = new Dictionary<string, Worst>();

            // Goal: Goals until every Goal clip has played (they are picked at random, never twice in a row).
            for (int g = 0; g < 12; g++)
            {
                bool all = true;
                foreach (string name in GoalieClipReactions.GoalClips) all &= worst.ContainsKey(name);
                if (all) break;
                Round round = NewRound(character, settings, clips);
                round.SubmitShot(50, 50, 90, false);
                round.Whistle(ShotResultKind.Goal);
                Play(clips, round, code, animator, worst);
            }
            // Save: shots at several spots until both the Glove and the Cheer clip have played.
            double[,] spots = { { 10, 90 }, { 90, 90 }, { 50, 50 }, { 50, 10 }, { 10, 10 }, { 90, 10 }, { 50, 90 }, { 25, 70 }, { 75, 70 } };
            for (int s = 0; s < spots.GetLength(0)
                 && !(worst.ContainsKey(GoalieClipReactions.GloveClip) && worst.ContainsKey(GoalieClipReactions.CheerClip)); s++)
            {
                Round round = NewRound(character, settings, clips);
                round.SubmitShot(spots[s, 0], spots[s, 1], 90, false);
                round.Whistle(ShotResultKind.Save);
                Play(clips, round, code, animator, worst);
            }
            // Idle: the start screen (the Round is in Idle).
            Play(clips, new Round(new SeededRandom(5), settings, new GoalieCharacterReach(character, settings), 0), code, animator, worst);

            string[] expected = { GoalieClipReactions.SlumpClip, GoalieClipReactions.StickSlapClip, GoalieClipReactions.LookBackClip,
                GoalieClipReactions.GloveClip, GoalieClipReactions.CheerClip, GoalieClipReactions.IdleClip };
            var problems = new List<string>();
            var report = new System.Text.StringBuilder();
            foreach (string name in expected)
            {
                if (!worst.TryGetValue(name, out Worst w) || w.Frames == 0) { problems.Add(name + ": never played"); continue; }
                report.AppendLine($"{name}: frames {w.Frames}, worst facing off {w.FacingOff:F1} deg (at {w.FacingAt:F2} s), lowest foot {w.FootBelow * 100f:F1} cm under (at {w.FootAt:F2} s)");
                if (w.FacingOff > MaxFacingOffDegrees) problems.Add($"{name}: faces {w.FacingOff:F1} deg off the code pose (limit {MaxFacingOffDegrees})");
                if (w.FootBelow > MaxUnderIceMetres) problems.Add($"{name}: lowest foot {w.FootBelow * 100f:F1} cm under the code pose's (limit 1)");
            }
            Debug.Log("[ClipFacing]\n" + report);
            Assert.IsEmpty(problems, string.Join("\n", problems) + "\n" + report + Diag);
        }
    }
}
