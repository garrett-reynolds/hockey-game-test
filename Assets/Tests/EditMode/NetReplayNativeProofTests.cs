using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HockeyGame.Core.Tests
{
    public class NetReplayNativeProofTests
    {
        [UnityTest, Category("Slow"), Explicit("Owned native proof only; requires HOCKEY_NET_PROOF output folder.")]
        public IEnumerator Actual_cloth_impact_shape_plays_after_the_live_net_settles()
        {
            string folder = Environment.GetEnvironmentVariable("HOCKEY_NET_PROOF");
            Assert.IsTrue(!string.IsNullOrEmpty(folder) && Path.IsPathRooted(folder), "Set an absolute HOCKEY_NET_PROOF folder.");
            Assert.IsFalse(Directory.Exists(folder) || File.Exists(folder), "Use a new proof folder.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            yield return new EnterPlayMode(false);
            Directory.CreateDirectory(folder);
            float oldCapture = Time.captureDeltaTime;
            bool oldMute = EditorUtility.audioMasterMute;
            bool oldBackground = Application.runInBackground;
            GameObject root = null;
            NetCloth net = null;
            ReplayNetting replay = null;
            RenderTexture target = null;
            Texture2D image = null;
            var ownedAssets = new System.Collections.Generic.List<Object>();
            var report = new System.Text.StringBuilder("Native solver impact demonstration. This is not footage of the old build.\nSettled reference shows the live net; playback shows stored impact shapes.\nStep=1/60 seconds; two kinematic puck paths; no crowd; no game benchmark.\n");
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                EditorUtility.audioMasterMute = true;
                Application.runInBackground = true;
                target = new RenderTexture(ReplayView.PictureWidth, ReplayView.PictureHeight, 24) { antiAliasing = 1 };
                image = new Texture2D(ReplayView.PictureWidth, ReplayView.PictureHeight, TextureFormat.RGB24, false);
                report.AppendLine("Camera uses ReplayView.CameraSpot, FieldOfView, AimPoint for shooter (0,1.6,-5); board size=512x280.\nRoot=(4,0,7), yaw=25 degrees. Live and replay views use the same camera and puck position.\n");
                for (int point = 0; point < 2; point++)
                {
                    root = new GameObject("Native net proof");
                    root.transform.SetPositionAndRotation(new Vector3(4f, 0f, 7f), Quaternion.Euler(0f, 25f, 0f));
                    net = new NetCloth(root.transform);
                    var source = net.Cloth.GetComponent<SkinnedMeshRenderer>();
                    replay = new ReplayNetting(source, () => net.Cloth.vertices);
                    var camera = new GameObject("Proof camera", typeof(Camera)).GetComponent<Camera>();
                    camera.transform.SetParent(root.transform, false);
                    camera.transform.localPosition = ReplayView.CameraSpot;
                    camera.transform.LookAt(root.transform.TransformPoint(ReplayView.AimPoint(new Vector3(0f, 1.6f, -5f))));
                    camera.fieldOfView = ReplayView.FieldOfView;
                    camera.aspect = ReplayView.PictureWidth / (float)ReplayView.PictureHeight;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.85f, 0.9f, 0.93f);
                    camera.targetTexture = target;
                    var cameraData = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(camera);
                    cameraData.renderPostProcessing = false;
                    cameraData.renderShadows = false;
                    cameraData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
                    Mesh ice = new Mesh { name = "Proof ice" };
                    ice.vertices = new[] { new Vector3(-8, -0.005f, -8), new Vector3(-8, -0.005f, 8), new Vector3(8, -0.005f, 8), new Vector3(8, -0.005f, -8) };
                    ice.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                    ice.RecalculateNormals();
                    AddSurface("Light ice", root.transform, ice, new Color(0.88f, 0.94f, 0.97f), ownedAssets);
                    var light = new GameObject("Proof light", typeof(Light)).GetComponent<Light>();
                    light.transform.SetParent(root.transform, false);
                    light.type = LightType.Directional;
                    light.intensity = 1.5f;
                    light.transform.rotation = Quaternion.Euler(40f, -25f, 0f);
                    var puck = new GameObject("Fixed path puck", typeof(Rigidbody), typeof(SphereCollider));
                    puck.transform.SetParent(root.transform, false);
                    puck.GetComponent<SphereCollider>().radius = 0.05f;
                    var body = puck.GetComponent<Rigidbody>();
                    AddSurface("Visible puck", puck.transform, PuckMesh(), new Color(0.025f, 0.03f, 0.035f), ownedAssets);
                    body.isKinematic = true;
                    body.useGravity = false;
                    float x = point == 0 ? -0.35f : 0.35f;
                    float y = point == 0 ? 0.55f : 0.85f;
                    body.position = root.transform.TransformPoint(new Vector3(x, y, 0.3f));
                    net.Track(body);
                    for (int frame = 0; frame < 60; frame++) yield return null;
                    Vector3[] rest = net.Cloth.vertices;
                    replay.Begin();
                    double peakTime = 0;
                    float peak = 0;
                    long recordTicks = 0, applyTicks = 0;
                    int recordCalls = 0;
                    var liveShapes = new Vector3[8][];
                    var livePixels = new Color32[8][];
                    var puckPositions = new Vector3[8];
                    var stopwatch = new System.Diagnostics.Stopwatch();
                    for (int frame = 0; frame <= 90; frame++)
                    {
                        float z = frame < 30 ? Mathf.Lerp(0.3f, 0.69f, frame / 30f) : 0.69f;
                        body.position = root.transform.TransformPoint(new Vector3(x, y, z));
                        net.Follow(body.position);
                        yield return null; // Unity runs the real Cloth solver between frames.
                        double seconds = frame / 60.0;
                        if (frame % 3 == 0)
                        {
                            stopwatch.Restart();
                            replay.Record(seconds);
                            stopwatch.Stop();
                            recordTicks += stopwatch.ElapsedTicks;
                            recordCalls++;
                        }
                        if (frame >= 18 && frame <= 39 && frame % 3 == 0)
                        {
                            int index = (frame - 18) / 3;
                            liveShapes[index] = net.Cloth.vertices;
                            puckPositions[index] = body.position;
                            livePixels[index] = Save(camera, target, image, Path.Combine(folder, "point-" + point + "-live-impact-" + index.ToString("00") + ".png"));
                        }
                        float moved = MaxDelta(rest, net.Cloth.vertices);
                        if (moved > peak && frame % 3 == 0) { peak = moved; peakTime = seconds; }
                    }
                    Assert.Greater(peak, 0.01f, "Native cloth must move at least 1 cm at impact.");
                    // Move the proxy away and let the live cloth settle before showing recorded frames.
                    body.position = root.transform.TransformPoint(new Vector3(3f, 3f, 3f));
                    net.Follow(body.position);
                    for (int frame = 0; frame < 120; frame++) yield return null;
                    string prefix = "point-" + point;
                    Save(camera, target, image, Path.Combine(folder, prefix + "-live-settled-reference.png"));
                    float previous = -1;
                    float playbackChange = 0;
                    for (int frame = 0; frame < 8; frame++)
                    {
                        double at = 0.3 + frame * 0.05;
                        stopwatch.Restart();
                        replay.Apply(at);
                        stopwatch.Stop();
                        applyTicks += stopwatch.ElapsedTicks;
                        Vector3[] played = replay.Look.GetComponent<MeshFilter>().sharedMesh.vertices;
                        float shapeError = MaxDelta(liveShapes[frame], played);
                        Assert.LessOrEqual(shapeError, 0.00001f, "Each played vertex must match the actual recorded native shape at the same time.");
                        float moved = MaxDelta(rest, played);
                        if (previous >= 0) playbackChange = Mathf.Max(playbackChange, Mathf.Abs(moved - previous));
                        previous = moved;
                        body.position = puckPositions[frame];
                        Color32[] playedPixels = Save(camera, target, image, Path.Combine(folder, prefix + "-playback-" + frame.ToString("00") + ".png"));
                        long pixelError = 0;
                        for (int pixel = 0; pixel < playedPixels.Length; pixel++)
                        {
                            Color32 a = livePixels[frame][pixel], b = playedPixels[pixel];
                            pixelError += Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);
                        }
                        double meanChannelError = pixelError / (playedPixels.Length * 3.0);
                        report.AppendLine($"point={point}; frame={frame}; recordedSeconds={at:F6}; maxDisplacementMetres={moved:F6}; maxVertexErrorMetres={shapeError:F8}; livePlaybackMeanChannelError255={meanChannelError:F6}");
                    }
                    Assert.Greater(playbackChange, 0.0001f, "Playback must show changing recorded shapes.");
                    report.AppendLine($"point={point}; impact=({x},{y},0.69); peakMetres={peak:F6}; samples={replay.SampleCount}");
                    double tickMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    long retainedPositionBytes = (long)replay.SampleCount * rest.Length * 12;
                    report.AppendLine($"point={point}; recordCalls={recordCalls}; recordTotalMs={recordTicks * tickMs:F4}; recordMeanMs={recordTicks * tickMs / recordCalls:F4}; applyCalls=8; applyTotalMs={applyTicks * tickMs:F4}; applyMeanMs={applyTicks * tickMs / 8:F4}; positionBytes={retainedPositionBytes}; estimateExcludesArrayOverhead=true; fullFrameBenchmark=false");
                    replay.Dispose(); replay = null;
                    net.Dispose(); net = null;
                    Object.Destroy(root); root = null;
                    yield return null;
                }
                File.WriteAllText(Path.Combine(folder, "proof.txt"), report.ToString());
            }
            finally
            {
                replay?.Dispose();
                net?.Dispose();
                if (root != null) Object.Destroy(root);
                RenderTexture.active = null;
                if (target != null) Object.Destroy(target);
                if (image != null) Object.Destroy(image);
                foreach (Object asset in ownedAssets) if (asset != null) Object.Destroy(asset);
                Time.captureDeltaTime = oldCapture;
                EditorUtility.audioMasterMute = oldMute;
                Application.runInBackground = oldBackground;
            }
            yield return new ExitPlayMode();
        }

        static float MaxDelta(Vector3[] a, Vector3[] b)
        {
            Assert.AreEqual(a.Length, b.Length);
            float value = 0;
            for (int i = 0; i < a.Length; i++) value = Mathf.Max(value, Vector3.Distance(a[i], b[i]));
            return value;
        }

        static Color32[] Save(Camera camera, RenderTexture target, Texture2D image, string file)
        {
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(file, image.EncodeToPNG());
            return image.GetPixels32();
        }

        static void AddSurface(string name, Transform parent, Mesh mesh, Color colour, System.Collections.Generic.List<Object> owned)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0.15f);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            owned.Add(mesh);
            owned.Add(material);
        }

        static Mesh PuckMesh()
        {
            const int sides = 24;
            var vertices = new Vector3[sides * 2 + 2];
            var triangles = new int[sides * 12];
            vertices[sides * 2] = new Vector3(0, -0.0125f, 0);
            vertices[sides * 2 + 1] = new Vector3(0, 0.0125f, 0);
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides;
                vertices[i] = new Vector3(Mathf.Cos(angle) * 0.0381f, -0.0125f, Mathf.Sin(angle) * 0.0381f);
                vertices[i + sides] = vertices[i] + Vector3.up * 0.025f;
                int next = (i + 1) % sides, t = i * 12;
                int[] face = { i, next, i + sides, next, next + sides, i + sides, sides * 2, next, i, sides * 2 + 1, i + sides, next + sides };
                Array.Copy(face, 0, triangles, t, 12);
            }
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int swap = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = swap;
            }
            var mesh = new Mesh { name = "Proof puck 76.2 by 25 mm" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
