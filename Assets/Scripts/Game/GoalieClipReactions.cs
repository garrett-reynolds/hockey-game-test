using System;
using System.Collections.Generic;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// PROTOTYPE: AI-made humanoid clips on the goalie. After a Goal one of three clips, after a Save the glove clip (Glove) or the
    /// cheer (Blocker, Pads, Stick, Body), and an idle clip in Idle and RoundOver. A Miss keeps the procedural Relief.
    /// <para>
    /// The Animator is off (no controller) except in a clip window. At the window's start the bones' local poses are kept
    /// (Pre/Post: Post runs after BreakawayView.Step). While the clip owns the pose (Owns), GoalieView.PoseCharacter skips every
    /// procedural bone writer. The clip is played by hand: the clip time is set and the Animator evaluated once a frame, so no
    /// loop setting and no Animator speed drift matter. The pose blends in from the kept pose and, at the end, out to the code
    /// pose over BlendSeconds. The clips are found with AssetDatabase in the Editor only; with a clip or the controller missing,
    /// nothing plays and the procedural gesture stays.
    /// </para>
    /// </summary>
    public sealed class GoalieClipReactions
    {
        public const string SlumpClip = "clip-16-slump", StickSlapClip = "clip-17-stick-slap", LookBackClip = "clip-18-look-back";
        public const string GloveClip = "clip-20-show-hand-up", CheerClip = "clip-30-cheer-point", IdleClip = "clip-31-idle-shoulder-roll";
        public static readonly string[] GoalClips = { SlumpClip, StickSlapClip, LookBackClip };
        public const string ControllerPath = "Assets/AIClips/AIClipViewer.controller";
        const string ClipFolder = "Assets/AIClips";
        static readonly int StateHash = Animator.StringToHash("Clip");

        /// <summary>The blend times to choose from (seconds, in and out alike); a key in Play cycles them (BreakawayView).
        /// 0.2 s looked snapped-in, not human (Garrett, 2026-10-14).</summary>
        public static readonly float[] BlendChoices = { 0.5f, 0.8f, 1.2f };
        public const float DefaultBlendSeconds = 0.8f;
        /// <summary>Seconds to blend in from the code pose and back out, eased at both ends (Ease).</summary>
        public float BlendSeconds { get; private set; } = DefaultBlendSeconds;

        /// <summary>The next of BlendChoices (after the last, the first); a clip already running keeps blending at its new pace.</summary>
        public float CycleBlend()
        {
            int i = Array.IndexOf(BlendChoices, BlendSeconds);
            BlendSeconds = BlendChoices[(i + 1) % BlendChoices.Length];
            return BlendSeconds;
        }

        /// <summary>Ease in and out (smoothstep): slow away from one pose, slow into the other.</summary>
        public static float Ease(float w) { w = Mathf.Clamp01(w); return w * w * (3f - 2f * w); }

        /// <summary>The short note shown on screen when the blend changes.</summary>
        public static string BlendNote(float seconds) => "Clip blend " + seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s";
        /// <summary>The fastest a long clip is played to fit what is left of the hold.</summary>
        public const float MaxSpeed = 2.5f;
        /// <summary>A hold with less than this left (seconds) gets no clip.</summary>
        public const float MinWindowSeconds = 0.35f;
        /// <summary>Seconds after the Whistle a Goal or Save clip starts (with or without a Replay).</summary>
        public const double AfterWhistleSeconds = 0.3;
        /// <summary>Idle clip: first plays this long into Idle / RoundOver, then every IdleRepeatSeconds after it ends.</summary>
        public const double IdleFirstSeconds = 1.0, IdleRepeatSeconds = 6.0;

        enum Mode { Off, In, Play, Out }

        readonly Animator _animator;
        readonly Transform[] _bones;       // every humanoid bone the avatar maps
        readonly bool[] _isHips;
        readonly Quaternion[] _snapRot, _codeRot, _animRot;
        readonly Vector3[] _snapPos, _codePos, _animPos;
        readonly bool _hasBones;
        readonly Transform _hips, _leftLeg, _rightLeg;
        readonly Transform[] _feet;        // feet and toes: the lowest one stands on the ice

        // The AI clips keep the facing and height they were made with (Root "Based upon Original"): half of them faced away
        // and all put the skates under the ice. Each clip is turned and lifted once, at its first frame, onto the code pose.
        Vector3 _snapHipsWorld, _animHips0;
        float _snapYaw, _snapFootY, _dy;
        Quaternion _yawFix = Quaternion.identity;
        bool _aligned;

        AnimatorOverrideController _override;
        AnimationClip _slot;
        RuntimeAnimatorController _baseController;
        readonly Dictionary<string, AnimationClip> _loaded = new Dictionary<string, AnimationClip>();
        bool _controllerLoaded;

        Mode _mode = Mode.Off;
        AnimationClip _clip;
        float _t, _speed = 1f, _outAt, _w;
        bool _holdsWindow;           // a Goal or Save clip: held on its last frame until the hold ends
        bool _savedRootMotion, _savedEnabled;
        AnimatorCullingMode _savedCulling;
        RoundPhase _lastPhase = RoundPhase.Idle;
        bool _playedThisHold;
        int _goalCycle;
        double _nextIdleAt = IdleFirstSeconds;
        double _phaseClock;          // real seconds in this phase, counted here (Round.PhaseSecondsElapsed stands still in Idle and RoundOver)

        // What the Play check reads.
        public string LastKind { get; private set; } = "";
        public string LastClipName { get; private set; } = "";
        public string LastController { get; private set; } = "";
        public float LastSpeed { get; private set; }
        public float LastStartTime { get; private set; } = -1f;   // Time.unscaledTime
        public float LastEndTime { get; private set; } = -1f;
        public float LastHoldLeftAtStart { get; private set; }
        public float LastClipLength { get; private set; }
        public int Starts { get; private set; }
        public int Fallbacks { get; private set; }
        public string AnimatorClipSeen { get; private set; } = "";
        public string AnimatorControllerSeen { get; private set; } = "";
        public float LastRestoreErrorDegrees { get; private set; } = -1f;
        /// <summary>How far releasing the Animator moved the bones from the code pose (degrees), before the code pose was put back.</summary>
        public float LastReleaseShiftDegrees { get; private set; } = -1f;
        /// <summary>The turn and lift put on the last clip at its first frame.</summary>
        public float LastYawFixDegrees { get; private set; }
        public float LastLiftMetres { get; private set; }
        /// <summary>Over the last clip: the most the body faced away from the code pose (degrees) and the lowest foot against the code pose's lowest foot (metres, below 0 = under).</summary>
        public float LastMaxFacingOffDegrees { get; private set; }
        public float LastMinFootGapMetres { get; private set; }

        /// <summary>The clips are on (the K toggle; set by every Post). Off until the first Post, so code that never runs the
        /// clips (the pose tests) keeps the code reactions even where Assets/AIClips is present.</summary>
        public bool Use { get; set; }
        /// <summary>Tests: take the clips as found (true) or missing (false) without Assets/AIClips; null asks the AssetDatabase.</summary>
        public bool? ClipsFoundOverride;

        /// <summary>
        /// True when a clip plays for this result in place of the code-made after-Shot reaction (GoalieView then shows none of
        /// its look back, gestures, head shake, head turn or follow-through): the clips are on, it is a Goal (an Annoyed
        /// Gesture) or a Save (None), and the clips are there. A Miss (Relief) has no clip and keeps its code reaction.
        /// </summary>
        public bool ReplacesReaction(AfterShotGesture gesture)
        {
            if (!Use || !(AfterShot.IsGoal(gesture) || gesture == AfterShotGesture.None)) return false;
            if (ClipsFoundOverride.HasValue) return ClipsFoundOverride.Value;
            if (!EnsureOverride()) return false;
            string[] names = gesture == AfterShotGesture.None ? new[] { GloveClip, CheerClip } : GoalClips;
            foreach (string name in names)
                if (Load(name) == null) return false;
            return true;
        }

        /// <summary>True while the clip owns the bones (blending in and playing): the procedural writers must not run.</summary>
        public bool Owns => _mode == Mode.In || _mode == Mode.Play;
        /// <summary>True from the clip's start to the end of its blend out.</summary>
        public bool Active => _mode != Mode.Off;

        public GoalieClipReactions(Animator animator)
        {
            _animator = animator;
            var list = new List<Transform>();
            var hips = new List<bool>();
            if (animator != null && animator.isHuman)
                for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                {
                    Transform bone = animator.GetBoneTransform((HumanBodyBones)i);
                    if (bone == null) continue;
                    list.Add(bone);
                    hips.Add(i == (int)HumanBodyBones.Hips);
                }
            _bones = list.ToArray();
            _isHips = hips.ToArray();
            _hasBones = _bones.Length > 0;
            var feet = new List<Transform>();
            if (_hasBones)
            {
                _hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                _leftLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                _rightLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                foreach (var b in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes })
                {
                    Transform t = animator.GetBoneTransform(b);
                    if (t != null) feet.Add(t);
                }
            }
            _feet = feet.ToArray();
            _snapRot = new Quaternion[_bones.Length]; _codeRot = new Quaternion[_bones.Length]; _animRot = new Quaternion[_bones.Length];
            _snapPos = new Vector3[_bones.Length]; _codePos = new Vector3[_bones.Length]; _animPos = new Vector3[_bones.Length];
        }

        /// <summary>Before the code poses the goalie this frame: during a blend out the kept pose goes back first, so nothing of the clip is left on a bone the code does not write.</summary>
        public void Pre()
        {
            if (_mode == Mode.Out) Put(_snapRot, _snapPos);
        }

        /// <summary>After the code posed the goalie this frame (BreakawayView.Step): starts, plays and ends the clip window.</summary>
        public void Post(Round round, bool use, float realDt)
        {
            Use = use;
            if (!_hasBones) return;
            RoundPhase phase = round.Phase;
            realDt = Mathf.Min(realDt, 0.1f); // the first frame of a Play reports the whole start-up as its delta; a stall must not skip the clip
            _phaseClock += realDt;
            if (phase != _lastPhase)
            {
                _lastPhase = phase;
                _phaseClock = 0.0;
                _nextIdleAt = IdleFirstSeconds;
                if (phase != RoundPhase.ResultHold) _playedThisHold = false;
                if (_mode == Mode.In || _mode == Mode.Play) BeginOut();
            }
            if (!_animator.gameObject.activeInHierarchy)
            {
                if (_mode != Mode.Off) HardStop();
                return;
            }
            if (_mode == Mode.Off)
            {
                if (use) TryStart(round);
                if (_mode == Mode.Off) return;
            }
            else if (!use) BeginOut();

            Step(realDt);
            // The idle clip repeats a while after it ends.
            if (_mode == Mode.Off && LastKind.StartsWith("Idle") && double.IsPositiveInfinity(_nextIdleAt))
                _nextIdleAt = _phaseClock + IdleRepeatSeconds;
        }

        void TryStart(Round round)
        {
            RoundPhase phase = round.Phase;
            string name = null;
            string kind = null;
            float window = 0f;
            bool holds = false;
            if (phase == RoundPhase.ResultHold)
            {
                if (_playedThisHold || !round.ResultShowing || !round.LastResult.HasValue || !round.LastResult.Value.HasShot) return;
                ShotResult result = round.LastResult.Value;
                if (result.Kind == ShotResultKind.Miss) return; // the procedural Relief stays
                double landed = round.PhaseSecondsElapsed - round.ResultDelaySeconds;
                if (landed < AfterWhistleSeconds) return; // at the Goal or Save, Replay or not (the Replay records and shows the clip's start)
                if (result.Kind == ShotResultKind.Goal)
                {
                    name = GoalClips[_goalCycle % GoalClips.Length];
                    kind = "Goal";
                }
                else
                {
                    SaveReaction reaction = round.LastRace.HasValue ? round.LastRace.Value.SaveReaction : SaveReactions.For(result.X, result.Y);
                    name = reaction == SaveReaction.Glove ? GloveClip : CheerClip;
                    kind = "Save " + reaction;
                }
                window = (float)round.ResultHoldSecondsLeft;
                holds = true;
                _playedThisHold = true; // one try per hold, played or not
                if (window < MinWindowSeconds) return;
            }
            else if (phase == RoundPhase.Idle || phase == RoundPhase.RoundOver)
            {
                if (_phaseClock < _nextIdleAt) return; // the Round's own clock does not run in Idle / RoundOver (it waits for a Press)
                name = IdleClip;
                kind = "Idle " + phase;
                _nextIdleAt = double.PositiveInfinity; // set again when it ends
            }
            else return;

            AnimationClip clip = Load(name);
            if (clip == null || !EnsureOverride())
            {
                Fallbacks++;
                if (kind.StartsWith("Idle")) _nextIdleAt = _phaseClock + IdleRepeatSeconds;
                return; // missing: the procedural gesture stays
            }
            if (kind == "Goal") _goalCycle++;
            Begin(clip, kind, holds, window);
        }

        void Begin(AnimationClip clip, string kind, bool holdsWindow, float windowSeconds)
        {
            // Keep the code pose as it stands (the code ran this frame, before Post).
            Take(_snapRot, _snapPos);
            if (_hips != null)
            {
                _snapHipsWorld = _hips.position;
                _snapYaw = BodyYaw();
                _snapFootY = LowestFootY();
            }
            _aligned = false;
            LastMaxFacingOffDegrees = 0f;
            LastMinFootGapMetres = float.MaxValue;
            _savedRootMotion = _animator.applyRootMotion;
            _savedEnabled = _animator.enabled;
            _savedCulling = _animator.cullingMode;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _override[_slot] = clip;
            _animator.runtimeAnimatorController = _override;
            _animator.enabled = false; // evaluated by hand, once a frame (Step)
            _clip = clip;
            _holdsWindow = holdsWindow;
            _t = 0f;
            _w = 0f;
            float length = Mathf.Max(0.05f, clip.length);
            if (holdsWindow)
            {
                float room = Mathf.Max(0.1f, windowSeconds - BlendSeconds); // the clip plays until the blend out starts
                _speed = Mathf.Clamp(length / room, 1f, MaxSpeed);
                _outAt = room;
            }
            else
            {
                _speed = 1f;
                _outAt = length;
            }
            _animator.speed = _speed;
            _mode = Mode.In;
            Starts++;
            LastKind = kind;
            LastClipName = clip.name;
            LastController = _animator.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.name : "";
            LastSpeed = _speed;
            LastClipLength = length;
            LastHoldLeftAtStart = windowSeconds;
            LastStartTime = Time.unscaledTime;
            LastEndTime = -1f;
            LastRestoreErrorDegrees = -1f;
            Debug.Log($"[AiClips] start {kind}: {clip.name} (length {length:F2} s, speed {_speed:F2}, window {(holdsWindow ? windowSeconds.ToString("F2") + " s" : "idle")}) at {LastStartTime:F2}");
        }

        void BeginOut()
        {
            if (_mode == Mode.In || _mode == Mode.Play) _mode = Mode.Out;
        }

        void Step(float dt)
        {
            _t += dt;
            float pos = Mathf.Min(_t * _speed, _clip.length);
            if (_t >= _outAt) BeginOut();
            if (_mode == Mode.Out) Take(_codeRot, _codePos); // the code pose this frame (the code ran, ungated, after Pre)
            // 0.999: a looping clip asset would wrap at 1 and show its first frame
            _animator.Play(StateHash, 0, Mathf.Min(0.999f, pos / Mathf.Max(0.05f, _clip.length)));
            _animator.Update(0f);
            AnimatorControllerSeen = _animator.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.name : "";
            var infos = _animator.GetCurrentAnimatorClipInfo(0);
            AnimatorClipSeen = infos.Length > 0 ? infos[0].clip.name : "";
            Align();
            Take(_animRot, _animPos);
            switch (_mode)
            {
                case Mode.In:
                    _w = Mathf.Min(1f, _w + dt / BlendSeconds);
                    Blend(_snapRot, _snapPos, _animRot, _animPos, Ease(_w));
                    KeepOnIce();
                    if (_w >= 1f) _mode = Mode.Play;
                    break;
                case Mode.Play:
                    Put(_animRot, _animPos);
                    break;
                case Mode.Out:
                    _w = Mathf.Max(0f, _w - dt / BlendSeconds);
                    Blend(_codeRot, _codePos, _animRot, _animPos, Ease(_w));
                    KeepOnIce();
                    if (_w <= 0f) Finish();
                    break;
            }
        }

        /// <summary>The bones hold the clip pose (just evaluated): turn it to the code pose's facing, put its hips over the
        /// code pose's hips and lift its lowest foot to the code pose's lowest foot. The lift is found at the first frame and
        /// kept for the whole clip, so the clip's own crouches still show.</summary>
        void Align()
        {
            if (_hips == null) return;
            if (!_aligned)
            {
                _yawFix = Quaternion.AngleAxis(Mathf.DeltaAngle(BodyYaw(), _snapYaw), Vector3.up);
                _animHips0 = _hips.position;
                _dy = _snapFootY - LowestFootY();
                _aligned = true;
                LastYawFixDegrees = Mathf.DeltaAngle(BodyYaw(), _snapYaw);
                LastLiftMetres = _dy;
            }
            Vector3 p = _hips.position;
            Vector3 off = _yawFix * (p - _animHips0);
            // The facing is locked every frame (the AI idle turned the hips up to 102 deg): the goalie stays square to the
            // shooter; the spine, head and arms still turn as the clip has them.
            Quaternion square = Quaternion.AngleAxis(Mathf.DeltaAngle(BodyYaw(), _snapYaw), Vector3.up);
            _hips.SetPositionAndRotation(new Vector3(_snapHipsWorld.x + off.x, p.y + _dy, _snapHipsWorld.z + off.z), square * _hips.rotation);
            float under = _snapFootY - LowestFootY(); // a weight shift (stick slap) put a skate 2 cm into the ice: lift it out
            if (under > 0f) _hips.position += Vector3.up * under;
            LastMaxFacingOffDegrees = Mathf.Max(LastMaxFacingOffDegrees, Mathf.Abs(Mathf.DeltaAngle(BodyYaw(), _snapYaw)));
            LastMinFootGapMetres = Mathf.Min(LastMinFootGapMetres, LowestFootY() - _snapFootY);
        }

        /// <summary>A blend of two poses that each stand on the ice can still dip a skate into it (up to 7 cm, measured in the 0.2 s
        /// blend in, #183): lift the hips so the lowest foot is no lower than the code pose's.</summary>
        void KeepOnIce()
        {
            if (_hips == null) return;
            float under = _snapFootY - LowestFootY();
            if (under > 0f) _hips.position += Vector3.up * under;
        }

        /// <summary>The way the body faces, in degrees about up: square to the line between the upper legs (any rig's bone axes).</summary>
        float BodyYaw()
        {
            Vector3 right = _rightLeg != null && _leftLeg != null ? _rightLeg.position - _leftLeg.position : _hips.right;
            Vector3 fwd = Vector3.Cross(right, Vector3.up);
            return Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        }

        float LowestFootY()
        {
            float y = float.MaxValue;
            foreach (Transform f in _feet) y = Mathf.Min(y, f.position.y);
            return _feet.Length > 0 ? y : _hips.position.y;
        }

        void Finish()
        {
            // The bones hold the code pose now (weight 0). The kept pose is not needed: Pre put it back before the code wrote this frame.
            Release();
            LastEndTime = Time.unscaledTime;
            // Putting the Animator back (enabled again, no controller) moved bones by up to a few degrees in the first Play check:
            // measure it, then put the code pose back on top so the code pose is exactly what shows.
            float worst = 0f;
            for (int i = 0; i < _bones.Length; i++) worst = Mathf.Max(worst, Quaternion.Angle(_bones[i].localRotation, _codeRot[i]));
            LastReleaseShiftDegrees = worst;
            Put(_codeRot, _codePos);
            worst = 0f;
            for (int i = 0; i < _bones.Length; i++) worst = Mathf.Max(worst, Quaternion.Angle(_bones[i].localRotation, _codeRot[i]));
            LastRestoreErrorDegrees = worst;
            Debug.Log($"[AiClips] end {LastKind}: {LastClipName} at {LastEndTime:F2} (window {LastEndTime - LastStartTime:F2} s)");
            _mode = Mode.Off;
        }

        void Release()
        {
            _animator.runtimeAnimatorController = null;
            _animator.enabled = _savedEnabled;
            _animator.applyRootMotion = _savedRootMotion;
            _animator.cullingMode = _savedCulling;
            _animator.speed = 1f;
        }

        /// <summary>The goalie went away (hidden) mid-clip: the kept pose goes back and the Animator off, at once.</summary>
        void HardStop()
        {
            Release();
            Put(_snapRot, _snapPos);
            _mode = Mode.Off;
            LastEndTime = Time.unscaledTime;
        }

        void Take(Quaternion[] rot, Vector3[] pos)
        {
            for (int i = 0; i < _bones.Length; i++)
            {
                rot[i] = _bones[i].localRotation;
                pos[i] = _bones[i].localPosition;
            }
        }

        void Put(Quaternion[] rot, Vector3[] pos)
        {
            for (int i = 0; i < _bones.Length; i++)
            {
                _bones[i].localRotation = rot[i];
                if (_isHips[i]) _bones[i].localPosition = pos[i];
            }
        }

        void Blend(Quaternion[] fromRot, Vector3[] fromPos, Quaternion[] toRot, Vector3[] toPos, float w)
        {
            for (int i = 0; i < _bones.Length; i++)
            {
                _bones[i].localRotation = Quaternion.Slerp(fromRot[i], toRot[i], w);
                if (_isHips[i]) _bones[i].localPosition = Vector3.Lerp(fromPos[i], toPos[i], w);
            }
        }

        bool EnsureOverride()
        {
            if (_override != null) return true;
            if (!_controllerLoaded)
            {
                _controllerLoaded = true;
#if UNITY_EDITOR
                _baseController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
#endif
            }
            if (_baseController == null) return false;
            _override = new AnimatorOverrideController(_baseController) { name = _baseController.name + " (override)" };
            if (_override.animationClips.Length == 0) { _override = null; return false; }
            _slot = _override.animationClips[0];
            return true;
        }

        AnimationClip Load(string name)
        {
            if (_loaded.TryGetValue(name, out AnimationClip cached)) return cached;
            AnimationClip clip = null;
#if UNITY_EDITOR
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets(name + " t:AnimationClip", new[] { ClipFolder }))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var found = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (found != null && found.name == name) { clip = found; break; }
            }
#endif
            _loaded[name] = clip;
            return clip;
        }
    }
}
