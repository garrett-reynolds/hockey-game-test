using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HockeyGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace HockeyGame.Editor
{
    public static partial class PictureTestTool
    {
        /// <summary>Captures one real six-view side into a new owned destination, without changing saved picture runs.</summary>
        public static string CapturePairSide(string outputFolder, PictureCaptureContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            RequireHash(context.sourceCommit, 40);
            RequireHash(context.sourceDiffSha256, 64);
            RequireHash(context.capturePayloadSha256, 64);
            if (string.IsNullOrWhiteSpace(outputFolder)) throw new ArgumentException("An output folder is required.", nameof(outputFolder));
            string output = Path.GetFullPath(outputFolder);
            string parent = Path.GetDirectoryName(output);
            if (!Directory.Exists(parent)) throw new DirectoryNotFoundException("The output parent must exist.");
            if (Directory.Exists(output) || File.Exists(output)) throw new IOException("The output already exists.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scenes before a side capture.");

            var setup = EditorSceneManager.GetSceneManagerSetup();
            bool shaders = ShaderUtil.allowAsyncCompilation;
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = null;
            Texture2D texture = null;
            Camera ownedCamera = null;
            var created = new List<string>();
            bool ownsFolder = false, complete = false;
            string lockPath = output + ".capture.lock";
            using var ownership = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            try
            {
                if (Directory.Exists(output) || File.Exists(output)) throw new IOException("The output already exists.");
                Directory.CreateDirectory(output);
                ownsFolder = true;
                ShaderUtil.allowAsyncCompilation = false;
                EditorSceneManager.OpenScene(RinkScene, OpenSceneMode.Single);
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
                texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                var rig = new Rig(target);
                ownedCamera = rig.Camera;
                // The older real Rig has no IDisposable API; the newer real Rig owns physics effects.
                using var rigLifetime = (object)rig as IDisposable;
                var views = new List<PictureSideView>();
                foreach (PicturePoint point in Points.Where(point => point.SetUp.IsMistake))
                {
                    NetFaceGaps gaps = rig.Pose(point);
                    foreach (bool easy in new[] { true, false })
                    {
                        rig.Goalie.ShowGaps(easy ? gaps : NetFaceGaps.None);
                        if (!rig.CrowdDrawn) throw new InvalidOperationException("The side capture needs the actual crowd drawn.");
                        string id = ViewName(point, easy);
                        ViewSample sample = SampleView(rig, target, texture, id, gaps);
                        views.Add(ExportSample(sample, id, output, texture, created));
                    }
                }
                if (views.Count != 6) throw new InvalidOperationException("A side must contain six real views.");
                var manifest = new PictureSideManifest {
                    schema = "hockey-picture-capture-v1", sourceCommit = context.sourceCommit,
                    sourceDiffSha256 = context.sourceDiffSha256, capturePayloadSha256 = context.capturePayloadSha256,
                    width = target.width, height = target.height, frozen = RuntimeInputs(rig, target), views = views.ToArray()
                };
                string path = Path.Combine(output, "manifest.json");
                WriteOwned(path, Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest, true)), created);
                complete = true;
                return path;
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = shaders;
                RenderTexture.active = previous;
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                if (ownedCamera != null && ownedCamera.targetTexture == target) ownedCamera.targetTexture = null;
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                try
                {
                    if (setup.Length > 0 && setup.All(scene => !string.IsNullOrEmpty(scene.path)))
                        EditorSceneManager.RestoreSceneManagerSetup(setup);
                    else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                finally
                {
                    if (!complete && ownsFolder)
                    {
                        foreach (string file in created) File.Delete(file);
                        if (!Directory.EnumerateFileSystemEntries(output).Any()) Directory.Delete(output);
                    }
                    ownership.Dispose();
                    File.Delete(lockPath);
                }
            }
        }

        /// <summary>The normal seen-image and mask passes, in their original order and row direction.</summary>
        static Color32[] Capture(Camera camera, RenderTexture target, Texture2D texture, int cullingMask, bool asSeen, Action<Camera> observe = null)
        {
            int mask = camera.cullingMask;
            CameraClearFlags flags = camera.clearFlags;
            Color background = camera.backgroundColor;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            bool post = data.renderPostProcessing;
            try
            {
                camera.cullingMask = cullingMask;
                if (!asSeen)
                {
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = ClearColour;
                    data.renderPostProcessing = false;
                }
                camera.targetTexture = target;
                camera.Render();
                observe?.Invoke(camera);
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                texture.Apply();
                return texture.GetPixels32();
            }
            finally
            {
                RenderTexture.active = null;
                camera.cullingMask = mask;
                camera.clearFlags = flags;
                camera.backgroundColor = background;
                data.renderPostProcessing = post;
            }
        }

        sealed class ViewSample
        {
            public Color32[] Picture;
            public bool[] Goalie, Glow, GearSeen, Calm, Gear;
            public PictureSideCamera Camera;
            public readonly List<GapSample> Gaps = new List<GapSample>();
        }

        sealed class GapSample
        {
            public string Name;
            public List<int> Cells;
            public bool[] Region, Excluded;
        }

        // One sampling path serves the normal measures and the raw side export.
        static ViewSample SampleView(Rig rig, RenderTexture target, Texture2D texture, string view, NetFaceGaps gaps)
        {
            PictureSideCamera camera = null;
            Color32[] picture = Capture(rig.Camera, target, texture, ~0, true, seen => camera = CameraInputs(seen));
            Color32[] again = Capture(rig.Camera, target, texture, ~0, true);
            if (PictureMeasures.ChangedShare(picture, again) > 0)
                throw new InvalidOperationException("View " + view + " rendered differently twice in a row: the picture test cannot compare it.");
            bool[] goalie = PictureMeasures.Differs(Capture(rig.Camera, target, texture, 1 << GoalieLayer, false), ClearColour);
            bool[] glow = PictureMeasures.Differs(Capture(rig.Camera, target, texture, 1 << GlowLayer, false), ClearColour);
            bool[] gearSeen = PictureMeasures.Without(goalie, glow);
            bool[] inFront = (bool[])goalie.Clone();
            PictureMeasures.AddTo(inFront, glow);
            bool[] calm = PictureMeasures.Without(CalmArea(rig.Camera, view), inFront);
            bool[] gear = PictureMeasures.And(gearSeen, PictureMeasures.Polygon(NetFaceCorners(rig.Camera, 0, 0, 100, 100), Width, Height));
            if (PictureMeasures.InnerPixels(calm, Width, Height) == 0)
                throw new InvalidOperationException("View " + view + " shows no Calm Area: its contrast and edges cannot be measured.");
            var sample = new ViewSample { Picture = picture, Goalie = goalie, Glow = glow, GearSeen = gearSeen, Calm = calm, Gear = gear, Camera = camera };
            var isGap = new bool[NetFaceGaps.Cells];
            for (int column = 0; column < NetFaceGaps.Columns; column++)
            for (int row = 0; row < NetFaceGaps.Rows; row++)
                isGap[NetFaceGaps.Index(column, row)] = gaps.IsGap(column, row);
            foreach (List<int> cells in PictureMeasures.Groups(isGap, NetFaceGaps.Columns, NetFaceGaps.Rows))
            {
                var region = new bool[Width * Height];
                foreach (int cell in cells)
                {
                    NetFaceGaps.Cell(cell % NetFaceGaps.Columns, cell / NetFaceGaps.Columns, out double minX, out double minY, out double maxX, out double maxY);
                    PictureMeasures.AddTo(region, PictureMeasures.Polygon(NetFaceCorners(rig.Camera, minX, minY, maxX, maxY), Width, Height));
                }
                sample.Gaps.Add(new GapSample { Name = PictureMeasures.GapName(cells, NetFaceGaps.Columns), Cells = cells,
                    Region = region, Excluded = PictureMeasures.Without(region, gearSeen) });
            }
            return sample;
        }

        static PictureSideView ExportSample(ViewSample sample, string id, string output, Texture2D texture, List<string> created)
        {
            var raw = new byte[sample.Picture.Length * 4];
            for (int i = 0; i < sample.Picture.Length; i++)
            {
                Color32 p = sample.Picture[i];
                raw[i * 4] = p.r; raw[i * 4 + 1] = p.g; raw[i * 4 + 2] = p.b; raw[i * 4 + 3] = p.a;
            }
            PictureSideBlob Mask(string name, bool[] mask) => SaveBlob(output, id + "-" + name + ".mask", mask.Select(value => value ? (byte)1 : (byte)0).ToArray(), created, mask.Count(value => value));
            texture.SetPixels32(sample.Picture);
            texture.Apply();
            WriteOwned(Path.Combine(output, id + ".png"), texture.EncodeToPNG(), created);
            float[] luma = PictureMeasures.Luma(sample.Picture);
            return new PictureSideView {
                id = id, camera = sample.Camera, image = SaveBlob(output, id + ".rgba", raw, created),
                masks = new PictureSideMasks { goalie = Mask("goalie", sample.Goalie), glow = Mask("glow", sample.Glow),
                    gearSeen = Mask("gearSeen", sample.GearSeen), calm = Mask("calm", sample.Calm), gear = Mask("gear", sample.Gear) },
                gaps = sample.Gaps.Select((gap, index) => new PictureSideGap { id = gap.Name, cellIndices = gap.Cells.OrderBy(cell => cell).ToArray(),
                    projectedCellRegionMask = Mask("gap-" + index + "-region", gap.Region), gearExcludedMask = Mask("gap-" + index + "-excluded", gap.Excluded) }).ToArray(),
                calmContrast = PictureMeasures.Contrast(luma, sample.Calm), calmEdges = PictureMeasures.Edges(luma, sample.Calm, Width, Height)
            };
        }

        static PictureSideBlob SaveBlob(string output, string name, byte[] bytes, List<string> created, int trueCount = 0)
        {
            WriteOwned(Path.Combine(output, name), bytes, created);
            using var sha = SHA256.Create();
            return new PictureSideBlob { path = name, byteCount = bytes.Length, trueCount = trueCount,
                sha256 = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() };
        }

        static void WriteOwned(string path, byte[] bytes, List<string> created)
        {
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created.Add(path);
            file.Write(bytes, 0, bytes.Length);
        }

        static void RequireHash(string value, int length)
        {
            if (value == null || value.Length != length || value.Any(c => !(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')))
                throw new ArgumentException("Source identity must be lowercase hex with the required length.");
        }

        static PictureSideCamera CameraInputs(Camera camera)
        {
            Vector3 p = camera.transform.position;
            Quaternion r = camera.transform.rotation;
            Matrix4x4 projection = camera.projectionMatrix;
            var values = new float[16];
            for (int i = 0; i < values.Length; i++) values[i] = projection[i];
            return new PictureSideCamera { cameraPosition = new[] { p.x, p.y, p.z }, cameraRotation = new[] { r.x, r.y, r.z, r.w },
                cameraProjection = values, cameraFieldOfView = camera.fieldOfView, cameraPixelWidth = camera.pixelWidth, cameraPixelHeight = camera.pixelHeight };
        }

        static PictureSideRuntime RuntimeInputs(Rig rig, RenderTexture target)
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return new PictureSideRuntime {
                unityVersion = Application.unityVersion, host = SystemInfo.deviceName, gpu = SystemInfo.graphicsDeviceName,
                gpuDriver = SystemInfo.graphicsDeviceVersion, graphicsApi = SystemInfo.graphicsDeviceType.ToString(), colorSpace = QualitySettings.activeColorSpace.ToString(),
                qualitySettings = JsonUtility.ToJson(new SideQuality { active = QualitySettings.GetQualityLevel(),
                    settings = File.ReadAllText(Path.Combine(project, "ProjectSettings/QualitySettings.asset")).Replace("\r\n", "\n"),
                    pipeline = AssetIdentity(QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline) }),
                renderSettings = JsonUtility.ToJson(new SideRender { fog = RenderSettings.fog, fogMode = RenderSettings.fogMode.ToString(), fogColor = RenderSettings.fogColor,
                    fogDensity = RenderSettings.fogDensity, fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance,
                    ambientMode = RenderSettings.ambientMode.ToString(), ambientSky = RenderSettings.ambientSkyColor, ambientEquator = RenderSettings.ambientEquatorColor,
                    ambientGround = RenderSettings.ambientGroundColor, ambientIntensity = RenderSettings.ambientIntensity, ambientLight = RenderSettings.ambientLight,
                    reflectionMode = RenderSettings.defaultReflectionMode.ToString(), reflectionResolution = RenderSettings.defaultReflectionResolution,
                    reflectionIntensity = RenderSettings.reflectionIntensity, reflectionBounces = RenderSettings.reflectionBounces, halo = RenderSettings.haloStrength,
                    flare = RenderSettings.flareStrength, flareFade = RenderSettings.flareFadeSpeed, skybox = AssetIdentity(RenderSettings.skybox),
                    customReflection = ReflectionInputs(RenderSettings.customReflectionTexture) }),
                crowdDrawn = rig.CrowdDrawn, poseInputs = new PictureSidePoseInputs { windowSeconds = WindowSeconds, lifeSeconds = LifeSeconds, eyeNudge = EyeNudge,
                    poseNames = Points.Where(point => point.SetUp.IsMistake).Select(point => point.Name).ToArray() },
                targetFormat = target.format.ToString(), targetReadWrite = target.sRGB ? RenderTextureReadWrite.sRGB.ToString() : RenderTextureReadWrite.Linear.ToString(), antiAliasing = target.antiAliasing
            };
        }

        static string AssetIdentity(UnityEngine.Object asset)
        {
            if (asset == null) return "none";
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("A render asset must have a stable saved identity.");
            string fullPath = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), path);
            if (!File.Exists(fullPath))
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
                    throw new InvalidOperationException("A built-in render asset must have a stable identity.");
                return guid + ":" + localId + ":" + path + ":" + asset.name;
            }
            using var sha = SHA256.Create();
            return AssetDatabase.AssetPathToGUID(path) + ":" + path + ":" + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(fullPath))).Replace("-", "").ToLowerInvariant();
        }

        static string ReflectionInputs(Texture texture)
        {
            if (texture == null) return "none";
            // Freeze the actual target recipe. Its rendered arena pixels are output, not frozen input.
            var render = texture as RenderTexture;
            return JsonUtility.ToJson(new SideReflection { width = texture.width, height = texture.height,
                dimension = texture.dimension.ToString(), format = render != null ? render.format.ToString() : texture.graphicsFormat.ToString(),
                mipCount = texture.mipmapCount, filter = texture.filterMode.ToString(), wrap = texture.wrapMode.ToString(),
                depth = render != null ? render.depth : 0, antiAliasing = render != null ? render.antiAliasing : 1 });
        }

        [Serializable] sealed class SideQuality { public int active; public string settings, pipeline; }
        [Serializable] sealed class SideRender
        {
            public bool fog;
            public string fogMode, ambientMode, reflectionMode, skybox, customReflection;
            public Color fogColor, ambientSky, ambientEquator, ambientGround, ambientLight;
            public float fogDensity, fogStart, fogEnd, ambientIntensity, reflectionIntensity, halo, flare, flareFade;
            public int reflectionResolution, reflectionBounces;
        }
        [Serializable] sealed class SideReflection
        {
            public string dimension, format, filter, wrap;
            public int width, height, mipCount, depth, antiAliasing;
        }
    }

    /// <summary>Actual source identity supplied by the owned caller. Frozen comparison identities are not defaults.</summary>
    [Serializable] public sealed class PictureCaptureContext
    {
        public string sourceCommit, sourceDiffSha256, capturePayloadSha256;
    }

    [Serializable] public sealed class PictureSideManifest
    {
        public string schema, sourceCommit, sourceDiffSha256, capturePayloadSha256;
        public int width, height;
        public PictureSideRuntime frozen;
        public PictureSideView[] views;
    }

    [Serializable] public sealed class PictureSideRuntime
    {
        public string unityVersion, host, gpu, gpuDriver, graphicsApi, colorSpace;
        // Full serialized snapshots of the actual settings used for these renders.
        public string qualitySettings, renderSettings;
        public bool crowdDrawn;
        public PictureSidePoseInputs poseInputs;
        public string targetFormat, targetReadWrite;
        public int antiAliasing;
    }

    [Serializable] public sealed class PictureSidePoseInputs
    {
        public double windowSeconds;
        public float lifeSeconds, eyeNudge;
        public string[] poseNames;
    }

    [Serializable] public sealed class PictureSideView
    {
        public string id;
        public PictureSideCamera camera;
        public PictureSideBlob image;
        public PictureSideMasks masks;
        public PictureSideGap[] gaps;
        public double calmContrast, calmEdges;
    }

    [Serializable] public sealed class PictureSideCamera
    {
        // Sample with the seen-image target assigned, before Capture restores camera state.
        public float[] cameraPosition, cameraRotation, cameraProjection;
        public float cameraFieldOfView;
        public int cameraPixelWidth, cameraPixelHeight;
    }

    [Serializable] public sealed class PictureSideMasks
    {
        public PictureSideBlob goalie, glow, gearSeen, calm, gear;
    }

    [Serializable] public sealed class PictureSideBlob
    {
        public string path, sha256;
        public int byteCount, trueCount;
    }

    [Serializable] public sealed class PictureSideGap
    {
        public string id;
        public int[] cellIndices;
        public PictureSideBlob projectedCellRegionMask, gearExcludedMask;
    }
}
