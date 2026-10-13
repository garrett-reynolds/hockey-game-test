using System.IO;
using UnityEditor;
using UnityEngine;

namespace HockeyGame.Editor
{
    /// <summary>
    /// Pictures of the water bottle on the net (#163), for Garrett's eye check: the shooter's view, a side view and a close view. Menu:
    /// Hockey Game/Picture Test/Water Bottle Pictures. It builds a rink in the open Editor's edit mode (no Play, no scene change; the
    /// objects are destroyed after), draws no crowd (the crowd draws only in Play mode, and its Drawn is switched off too, rule 6) and
    /// mutes the Editor's audio meanwhile. Writes t163-shooter.png, t163-side.png and t163-close.png only when the folder
    /// hockey-wt/_notes exists next to the project's slot.
    /// </summary>
    public static class WaterBottlePictureTool
    {
        const int Width = 1600, Height = 900;

        [MenuItem("Hockey Game/Picture Test/Water Bottle Pictures")]
        public static void Take()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "_notes"));
            if (!Directory.Exists(folder)) return;
            bool muted = EditorUtility.audioMasterMute;
            EditorUtility.audioMasterMute = true;
            Transform rink = null;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                rink = RinkBuilder.Build(out Camera camera);
                foreach (Crowd crowd in rink.GetComponentsInChildren<Crowd>(true)) crowd.Drawn = false;
                camera.aspect = Width / (float)Height;
                Transform bottle = rink.Find(WaterBottle.ObjectName);
                Bounds look = bottle.GetComponentInChildren<Renderer>().bounds;
                foreach (Renderer r in bottle.GetComponentsInChildren<Renderer>()) look.Encapsulate(r.bounds);
                Vector3 at = look.center;

                Shoot(camera, target, texture, Path.Combine(folder, "t163-shooter.png")); // the camera as the game places it
                Place(camera, new Vector3(5f, 1.5f, at.z), at, 45f);
                Shoot(camera, target, texture, Path.Combine(folder, "t163-side.png"));
                // A long lens from the rink's side: a flat side profile of the net and the bottle (about 2.7 mm a pixel).
                Place(camera, new Vector3(8f, 0.9f, 0.3f), new Vector3(0f, 0.9f, 0.3f), 18f);
                Shoot(camera, target, texture, Path.Combine(folder, "t163-profile.png"));
                Place(camera, at + new Vector3(0.1f, 0.35f, -0.45f), at, 30f);
                Shoot(camera, target, texture, Path.Combine(folder, "t163-close.png"));
            }
            finally
            {
                RenderTexture.active = null;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(target);
                if (rink != null) Object.DestroyImmediate(rink.gameObject);
                EditorUtility.UnloadUnusedAssetsImmediate(); // frees the materials the rink build made
                EditorUtility.audioMasterMute = muted;
            }
        }

        static void Place(Camera camera, Vector3 position, Vector3 lookAt, float fieldOfView)
        {
            camera.transform.position = position;
            camera.transform.LookAt(lookAt);
            camera.fieldOfView = fieldOfView;
        }

        static void Shoot(Camera camera, RenderTexture target, Texture2D texture, string path)
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
    }
}
