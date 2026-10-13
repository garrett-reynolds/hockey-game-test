using System.IO;
using UnityEditor;
using UnityEngine;

namespace HockeyGame.Editor
{
    /// <summary>
    /// Pictures of the water bottle on the net (#163), for Garrett's eye check: the shooter's view, a side view, a long-lens profile and a close view. Menu:
    /// Hockey Game/Picture Test/Water Bottle Pictures. It builds a rink in the open Editor's edit mode (no Play, no scene change; the
    /// objects are destroyed after), draws no crowd (the crowd draws only in Play mode, and its Drawn is switched off too, rule 6) and
    /// mutes the Editor's audio meanwhile. Writes t163-shooter.png, t163-side.png, t163-profile.png and t163-close.png only when the folder
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

        /// <summary>
        /// The knock (#164) in four pictures, side by side in t164-knock-strip.png: the bottle on the net, then 0.25, 0.5 and 1.0 s after
        /// Knock, from the rink's side. Physics is stepped by hand (Physics.Simulate at the Shot's fixed step); no Play, crowd off, audio muted.
        /// </summary>
        [MenuItem("Hockey Game/Picture Test/Water Bottle Knock Strip")]
        public static void TakeKnockStrip()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "_notes"));
            if (!Directory.Exists(folder)) return;
            float[] times = { 0f, 0.25f, 0.5f, 1.0f };
            const int w = 800, h = 450;
            bool muted = EditorUtility.audioMasterMute;
            SimulationMode oldMode = Physics.simulationMode;
            EditorUtility.audioMasterMute = true;
            Physics.simulationMode = SimulationMode.Script;
            Transform rink = null;
            var target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var strip = new Texture2D(w * times.Length, h, TextureFormat.RGB24, false);
            try
            {
                rink = RinkBuilder.Build(out Camera camera);
                foreach (Crowd crowd in rink.GetComponentsInChildren<Crowd>(true)) crowd.Drawn = false;
                camera.aspect = w / (float)h;
                ShotColliders.Build(rink); // the ice and net colliders, which the rink makes only for a Shot
                WaterBottle bottle = WaterBottle.In(rink);
                Physics.SyncTransforms();
                bottle.Knock();
                float elapsed = 0f;
                for (int i = 0; i < times.Length; i++)
                {
                    while (elapsed + PhysicsShot.StepSeconds / 2f < times[i]) { Physics.Simulate(PhysicsShot.StepSeconds); elapsed += PhysicsShot.StepSeconds; }
                    Vector3 p = bottle.Root.transform.position;
                    Debug.Log($"Knock strip: {elapsed:F3} s, bottle at {p.x:F2}, {p.y:F2}, {p.z:F2}");
                    Place(camera, new Vector3(7f, 1.0f, 1.5f), new Vector3(0f, 1.0f, 1.5f), 25f);
                    camera.targetTexture = target;
                    camera.Render();
                    RenderTexture.active = target;
                    strip.ReadPixels(new Rect(0, 0, w, h), w * i, 0);
                    RenderTexture.active = null;
                    camera.targetTexture = null;
                }
                strip.Apply();
                File.WriteAllBytes(Path.Combine(folder, "t164-knock-strip.png"), strip.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = null;
                Physics.simulationMode = oldMode;
                Object.DestroyImmediate(strip);
                Object.DestroyImmediate(target);
                if (rink != null) Object.DestroyImmediate(rink.gameObject);
                EditorUtility.UnloadUnusedAssetsImmediate();
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
