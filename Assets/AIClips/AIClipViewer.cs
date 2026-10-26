// TEST ONLY: shows one goalie and a list of every AI clip in Assets/AIClips.
// Add a new .anim to Assets/AIClips and it shows up in the list next Play.
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AIClipViewer : MonoBehaviour
{
    public string ClipFolder = "Assets/AIClips";
    // One-state controller; its clip is swapped for the picked one.
    public RuntimeAnimatorController BaseController;

    static readonly int StateHash = Animator.StringToHash("Clip");

    readonly List<AnimationClip> _clips = new List<AnimationClip>();
    Animator _animator;
    AnimatorOverrideController _override;
    AnimationClip _slot;
    int _selected = -1;
    bool _loop = true;
    float _speed = 1f;
    Vector2 _scroll;
    GUIStyle _button, _label;

    void Start()
    {
#if UNITY_EDITOR
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipFolder }))
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) continue;
            var settings = UnityEditor.AnimationUtility.GetAnimationClipSettings(clip);
            if (!settings.loopTime)
            {
                settings.loopTime = true;
                UnityEditor.AnimationUtility.SetAnimationClipSettings(clip, settings);
            }
            _clips.Add(clip);
        }
#endif
        _clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        _animator = GetComponent<Animator>();
        _animator.applyRootMotion = false;
        _override = new AnimatorOverrideController(BaseController);
        _slot = _override.animationClips[0];
        _animator.runtimeAnimatorController = _override;
        if (_clips.Count > 0) Play(0);
    }

    void Play(int index)
    {
        _selected = index;
        _override[_slot] = _clips[index];
        _animator.speed = _speed;
        _animator.Play(StateHash, 0, 0f);
    }

    float ClipTime()
    {
        if (_selected < 0) return 0f;
        var info = _animator.GetCurrentAnimatorStateInfo(0);
        var length = _clips[_selected].length;
        return _loop ? (info.normalizedTime % 1f) * length : Mathf.Min(info.normalizedTime, 1f) * length;
    }

    void Update()
    {
        if (_selected < 0) return;
        if (!_loop && _animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f) _animator.speed = 0f;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;
        if (kb.downArrowKey.wasPressedThisFrame && _selected < _clips.Count - 1) Play(_selected + 1);
        if (kb.upArrowKey.wasPressedThisFrame && _selected > 0) Play(_selected - 1);
        if (kb.spaceKey.wasPressedThisFrame) Play(_selected);
    }

    void OnGUI()
    {
        if (_button == null)
        {
            _button = new GUIStyle(GUI.skin.button) { fontSize = 20, alignment = TextAnchor.MiddleLeft };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
        }
        GUILayout.BeginArea(new Rect(10, 10, 420, Screen.height - 20), GUI.skin.box);
        GUILayout.Label("Goalie clips (" + _clips.Count + "). Up/Down to pick, Space to restart.", _label);
        _scroll = GUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _clips.Count; i++)
        {
            var text = (i == _selected ? "> " : "   ") + _clips[i].name;
            if (GUILayout.Button(text, _button, GUILayout.Height(40))) Play(i);
        }
        GUILayout.EndScrollView();
        if (_selected >= 0)
        {
            GUILayout.Label(string.Format("Time {0:0.00} / {1:0.00} s", ClipTime(), _clips[_selected].length), _label);
            if (GUILayout.Button("Restart", _button, GUILayout.Height(40))) Play(_selected);
            var loop = GUILayout.Toggle(_loop, " Loop", _button, GUILayout.Height(40));
            if (loop != _loop) { _loop = loop; Play(_selected); }
            GUILayout.Label("Speed " + _speed.ToString("0.00"), _label);
            var s = GUILayout.HorizontalSlider(_speed, 0.1f, 1.5f, GUILayout.Height(30));
            if (!Mathf.Approximately(s, _speed)) { _speed = s; _animator.speed = _speed; }
        }
        GUILayout.EndArea();
    }
}
