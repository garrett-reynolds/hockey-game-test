using System.IO;
using System.Reflection;
using HockeyGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace HockeyGame.Editor
{
    /// <summary>
    /// Pictures of the Settings page (owed by #159): the Player part (with the Replay row, #145) and the Dev part (17 rows, the last four
    /// cards in the right column, #148 + #156 + #164). A UI Toolkit panel draws only in Play mode, so the tool enters Play mode in an empty
    /// scene (no RoundDriver, so nothing reads or writes the player's save file), builds the page on a fresh SaveData, draws it into a
    /// 1920 x 1080 RenderTexture and leaves Play mode. Menu: Hockey Game/Picture Test/Settings Page Pictures (returns at once; the
    /// pictures arrive a few seconds later). Writes t159-settings-player.png and t159-settings-dev.png only when the folder
    /// hockey-wt/_notes exists next to the project's slot.
    /// </summary>
    [InitializeOnLoad]
    public static class SettingsPictureTool
    {
        const int Width = 1920, Height = 1080, SettleFrames = 6;
        const string Flag = "t159.settingsPictures", MuteKey = "t159.settingsPictures.mute", ScenePath = "t159.settingsPictures.scene";

        static RenderTexture _target;
        static Texture2D _texture;
        static SettingsScreen _screen;
        static GameObject _host;
        static int _stage, _frames;

        static SettingsPictureTool()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("Hockey Game/Picture Test/Settings Page Pictures")]
        public static void Take()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                if (!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) continue;
                Debug.LogError("Settings pictures: an open scene has unsaved changes. Save it first; the tool would throw them away. Nothing was changed.");
                return;
            }
            SessionState.SetBool(Flag, true);
            SessionState.SetBool(MuteKey, EditorUtility.audioMasterMute);
            SessionState.SetString(ScenePath, EditorSceneManager.GetActiveScene().path);
            EditorUtility.audioMasterMute = true; // rule 6
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
            _giveUpAt = EditorApplication.timeSinceStartup + 20;
            EditorApplication.update += GiveUpIfPlayNeverStarts;
        }

        static double _giveUpAt;

        // If Play never starts (compile error, cancelled), clear the flag and put the mute and scene back, so the next Play is not hijacked.
        static void GiveUpIfPlayNeverStarts()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode && EditorApplication.timeSinceStartup < _giveUpAt) return;
            EditorApplication.update -= GiveUpIfPlayNeverStarts;
            if (!SessionState.GetBool(Flag, false)) return;
            Debug.LogError("Settings pictures: Play mode did not start; flag cleared.");
            RestoreEditor();
        }

        static void RestoreEditor()
        {
            SessionState.SetBool(Flag, false);
            EditorUtility.audioMasterMute = SessionState.GetBool(MuteKey, false);
            string scene = SessionState.GetString(ScenePath, "");
            if (!string.IsNullOrEmpty(scene)) EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            else EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single); // an untitled start: a fresh default scene
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Flag, false))
            {
                Application.runInBackground = true;
                _stage = 0;
                _frames = 0;
                EditorApplication.update += Step;
            }
            else if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Flag, false))
            {
                RestoreEditor();
            }
        }

        static void Step()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "_notes"));
            try
            {
                if (_stage == 0)
                {
                    _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    _texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                    _screen = new SettingsScreen(new SaveData());
                    _host = GameObject.Find("Settings page");
                    var settings = _host.GetComponent<UIDocument>().panelSettings;
                    settings.targetTexture = _target;
                    settings.clearColor = true;
                    settings.colorClearValue = new Color(0.07f, 0.09f, 0.14f, 1f);
                    _screen.Open();
                    _stage = 1;
                    _frames = 0;
                }
                else if (++_frames >= SettleFrames)
                {
                    if (_stage == 1)
                    {
                        Shoot(Path.Combine(folder, "t159-settings-player.png"), "Player");
                        // #197: two changed rows (Master volume down 3 steps, Easy mode on): amber marks, SAVE lit.
                        var menu = _screen.Menu;
                        menu.Select(0);
                        for (int i = 0; i < 3; i++) menu.Change(-1);
                        for (int i = 0; i < menu.RowCount; i++)
                            if (menu.Label(i) == "Easy mode") { menu.Select(i); menu.Activate(); }
                        Call("Refresh");
                        _stage = 3;
                        _frames = 0;
                    }
                    else if (_stage == 3)
                    {
                        Shoot(Path.Combine(folder, "t197-marks.png"), "two unsaved rows");
                        Call("SaveNow");
                        Call("Refresh");
                        _stage = 4;
                        _frames = 0;
                    }
                    else if (_stage == 4)
                    {
                        Shoot(Path.Combine(folder, "t197-saved.png"), "right after SAVE");
                        // #198: one unsaved change, then a close asks "Save changes?".
                        _screen.Menu.Select(0);
                        _screen.Menu.Change(-1);
                        _screen.Menu.RequestClose();
                        Call("Refresh");
                        Call("SyncConfirm");
                        _stage = 5;
                        _frames = 0;
                    }
                    else if (_stage == 5)
                    {
                        Shoot(Path.Combine(folder, "t198-box.png"), "Save changes? box open");
                        _screen.Menu.ConfirmDiscard();
                        Call("SyncConfirm");
                        _screen.Menu.ShowPart(SettingsPart.Dev);
                        // Rebuild is private (the page rebuilds itself on a key or click, which this tool must not simulate); a rename fails here and is logged.
                        Call("Rebuild");
                        _stage = 2;
                        _frames = 0;
                    }
                    else if (_stage == 2)
                    {
                        Shoot(Path.Combine(folder, "t159-settings-dev.png"), "Dev (" + _screen.Menu.RowCount + " rows)");
                        _screen.ForceHoverRow = 0; // #199: a tip on the top row (no mouse; Pointer refreshes the page)
                        _screen.Pointer(new Vector2(-1f, -1f), false, false);
                        _stage = 3;
                        _frames = 0;
                    }
                    else if (_stage == 3)
                    {
                        Shoot(Path.Combine(folder, "t199-top.png"), "tip on the top Dev row");
                        _screen.ForceHoverRow = _screen.Menu.RowCount - 1; // and on the bottom row
                        _screen.Pointer(new Vector2(-1f, -1f), false, false);
                        _stage = 4;
                        _frames = 0;
                    }
                    else if (_stage == 4)
                    {
                        Shoot(Path.Combine(folder, "t199-bottom.png"), "tip on the bottom Dev row");
                        Finish();
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Settings pictures failed: " + e);
                Finish();
            }
        }

        // The page's private methods (it refreshes itself on a key or click, which this tool must not simulate); a rename fails here and is logged.
        static void Call(string method)
        {
            typeof(SettingsScreen).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_screen, null);
        }

        static void Shoot(string path, string what)
        {
            RenderTexture.active = _target;
            _texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            _texture.Apply();
            RenderTexture.active = null;
            if (Directory.Exists(Path.GetDirectoryName(path))) File.WriteAllBytes(path, _texture.EncodeToPNG());
            Debug.Log("Settings picture: " + path + " (" + what + ")");
        }

        static void Finish()
        {
            EditorApplication.update -= Step;
            if (_host != null) Object.Destroy(_host);
            if (_texture != null) Object.Destroy(_texture);
            if (_target != null) _target.Release();
            _host = null;
            _texture = null;
            _target = null;
            _screen = null;
            EditorApplication.ExitPlaymode();
        }
    }
}
