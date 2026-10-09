using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HockeyGame.Editor
{
    /// <summary>Recreates Assets/Scenes/Rink.unity (a RoundDriver on an empty object, with the arena fog) and makes it the first Build Settings scene.</summary>
    public static class RinkSceneTool
    {
        const string ScenePath = "Assets/Scenes/Rink.unity";

        [MenuItem("Hockey Game/Build Rink Scene")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Round Driver").AddComponent<RoundDriver>();
            ArenaLighting.SetFog(); // saved with the scene, so the build keeps the fog shaders
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }
    }
}
