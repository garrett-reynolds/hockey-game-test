using System.IO;
using HockeyGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HockeyGame.Editor
{
    /// <summary>
    /// Pictures of the Video Board's result word with the Replay off (#145, owed by #159): SAVE, OFF THE POST!, OFF THE BAR!,
    /// 2.5 s into the result hold, crowd off, audio muted. The camera looks straight at the Video Board (with the Replay off the game's
    /// camera stays on the net, so this view exists only here). Menu: Hockey Game/Picture Test/Replay Off Result Word Pictures.
    /// Writes t159-replayoff-save/post/bar.png only when the folder hockey-wt/_notes exists next to the project's slot.
    /// </summary>
    public static class ReplayOffPictureTool
    {
        const int Width = 1280, Height = 720;

        [MenuItem("Hockey Game/Picture Test/Replay Off Result Word Pictures")]
        public static void Take()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "_notes"));
            SceneSetup[] scenes = EditorSceneManager.GetSceneManagerSetup();
            bool muted = EditorUtility.audioMasterMute;
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            EditorUtility.audioMasterMute = true; // rule 6: crowd off and audio muted
            ShaderUtil.allowAsyncCompilation = false;
            RenderTexture target = null;
            Texture2D texture = null;
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Rink.unity", OpenSceneMode.Single);
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
                texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                RinkBuilder.Build(out Camera camera);
                Transform rink = GameObject.Find("Rink").transform;
                var crowd = rink.GetComponentInChildren<Crowd>();
                if (crowd != null) crowd.Drawn = false;
                var boards = rink.GetComponentInChildren<VideoBoards>();
                camera.targetTexture = target;

                Vector3 centre = boards.CentreOf(0), facing = boards.FacingOf(0), up = boards.UpOf(0);
                Vector2 size = boards.SizeOf(0);
                camera.fieldOfView = 40f;
                float half = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float distance = Mathf.Max(size.x * 0.5f * 1.15f / (half * Width / Height), size.y * 0.5f * 1.15f / half);
                camera.transform.position = centre + facing * distance;
                camera.transform.rotation = Quaternion.LookRotation(-facing, up);

                var results = new[] { (ArenaResult.Save, "save"), (ArenaResult.FrameHitPost, "post"), (ArenaResult.FrameHitBar, "bar") };
                foreach (var (result, name) in results)
                {
                    var input = new ArenaShowInput
                    {
                        Phase = RoundPhase.ResultHold, PhaseSeconds = 2.5, Result = result, ResultHidden = false, ResultDelaySeconds = 0,
                        Goals = 2, ShotNumber = 3, ShotsPerRound = 6, NoReplay = true
                    };
                    ArenaState arena = ArenaShow.At(input);
                    boards.Show(arena, 2.5);
                    camera.Render();
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    texture.Apply();
                    RenderTexture.active = null;
                    string path = Path.Combine(folder, "t159-replayoff-" + name + ".png");
                    if (Directory.Exists(folder)) File.WriteAllBytes(path, texture.EncodeToPNG());
                    Debug.Log("Replay off picture: " + path + " (board " + arena.VideoBoard + ", \"" + boards.HeadlineOn(0) + "\" / \"" + boards.DetailsOn(0) + "\")");
                }
            }
            finally
            {
                EditorUtility.audioMasterMute = muted;
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                RenderTexture.active = null;
                if (texture != null) Object.DestroyImmediate(texture);
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }
                if (scenes.Length > 0 && System.Array.TrueForAll(scenes, s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(scenes);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }
        }
    }
}
