using System;
using System.Runtime.InteropServices;
using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// The 3D crowd (first test, #94): real fans in the lower stand behind the near net (CrowdSeats), in place of the
    /// arena's flat crowd picture there. Every fan is one instance of a static body mesh, all drawn in one call
    /// (Graphics.RenderMeshPrimitives) with the crowd shader, which poses each fan from its clip's vertex animation texture.
    /// A per-fan buffer holds each fan's seat, random phase, shirt, skin and hair (CrowdLooks).
    /// It follows the Arena Show: Show sets each zone's clip clock and light; the shader works out each fan's pose. The
    /// fans behind the near net (the Calm Area zone) sit still and dim from goalie set to the Shot.
    /// </summary>
    [ExecuteAlways]
    public sealed class Crowd : MonoBehaviour
    {
        /// <summary>Folder under Assets/Resources with the crowd's body files and shader.</summary>
        public const string Folder = "Crowd";
        /// <summary>The first body (Tools/make_crowd.py makes it from a Quaternius base character).</summary>
        public const string BodyFile = "fan-superhero-male";
        public const string ShaderFile = "CrowdFan";
        public const string SitIdle = "sit-idle";
        /// <summary>The longest step a zone's clip clock takes in one Show (a pause or a hitch does not jump the fans).</summary>
        const double MaxStep = 0.25;
        /// <summary>The fans' size spread (1 = the body's own 1.8 m).</summary>
        const float MinScale = 0.92f, MaxScale = 1.02f;
        /// <summary>Each fan turns a little off straight ahead (radians, either way).</summary>
        const float YawSpread = 0.15f;

        [StructLayout(LayoutKind.Sequential)]
        struct FanData
        {
            public Vector4 Place; // x, y, z (crowd space), yaw
            public Vector4 Shirt; // rgb (linear), phase 0..1
            public Vector4 Skin;  // rgb (linear), zone
            public Vector4 Hair;  // rgb (linear), scale
        }

        static readonly int FansId = Shader.PropertyToID("_Fans"), VatId = Shader.PropertyToID("_Vat"),
            ClipSecondsId = Shader.PropertyToID("_ClipSeconds"), ClipFramesId = Shader.PropertyToID("_ClipFrames"),
            ZoneClockId = Shader.PropertyToID("_ZoneClock"), ZoneLightId = Shader.PropertyToID("_ZoneLight"),
            CrowdToWorldId = Shader.PropertyToID("_CrowdToWorld"), FanCountId = Shader.PropertyToID("_FanCount"),
            CopyStepId = Shader.PropertyToID("_CopyStep");

        static readonly ArenaZone[] Zones = { ArenaZone.CalmArea, ArenaZone.Near, ArenaZone.Far };

        CrowdBody _body;
        CrowdBody.Clip _clip;
        Material _material;
        GraphicsBuffer _fans;
        Bounds _bounds;
        FanSeat[] _seats;
        readonly double[] _clock = new double[3];
        readonly float[] _light = { 1f, 1f, 1f };
        double _lastSeconds = double.NaN;

        public int FanCount => _seats?.Length ?? 0;
        public FanSeat SeatOf(int fan) => _seats[fan];
        public CrowdBody Body => _body;
        /// <summary>The fans' bounds in the crowd's (the rink's) space.</summary>
        public Bounds Bounds => _bounds;
        /// <summary>Where the zone's fans are in their clip (seconds played; it stops while they are still).</summary>
        public double ClockIn(ArenaZone zone) => _clock[(int)zone];
        /// <summary>The zone's fans' brightness (1 = as lit).</summary>
        public float LightIn(ArenaZone zone) => _light[(int)zone];
        /// <summary>Off: no fans are drawn (the crowd GPU cost tool draws with and without).</summary>
        public bool Drawn { get; set; } = true;
        /// <summary>How many times over each fan is drawn (1). The crowd GPU cost tool draws more, each copy of the stand
        /// beside the last, to guess the full crowd's cost.</summary>
        public int Copies { get; set; } = 1;

        /// <summary>Adds the crowd under rink, or nothing when the body file or the shader is missing.</summary>
        public static Crowd Build(Transform rink)
        {
            CrowdBody body = CrowdBody.Load(BodyFile);
            var shader = Resources.Load<Shader>(Folder + "/" + ShaderFile);
            if (body == null || shader == null || body.ClipNamed(SitIdle) == null)
            {
                Debug.LogWarning("No 3D crowd: Assets/Resources/" + Folder + "/" + BodyFile + ".bytes or its shader is missing.");
                body?.Destroy();
                return null;
            }

            var root = new GameObject("Crowd");
            root.transform.SetParent(rink, false);
            var crowd = root.AddComponent<Crowd>();
            crowd.Fill(body, shader, CrowdSeats.BehindNearNet(), new System.Random(94));
            crowd.Show(ArenaShow.At(new ArenaShowInput { Phase = RoundPhase.Idle }), 0.0);
            return crowd;
        }

        void Fill(CrowdBody body, Shader shader, FanSeat[] seats, System.Random random)
        {
            _body = body;
            _clip = body.ClipNamed(SitIdle);
            _seats = seats;
            _material = new Material(shader) { name = "Crowd" };
            _material.SetTexture(VatId, _clip.Texture);
            _material.SetFloat(ClipSecondsId, _clip.Seconds);
            _material.SetFloat(ClipFramesId, _clip.Frames);
            _material.SetFloat(FanCountId, seats.Length);
            _material.SetFloat(CopyStepId, (float)(CrowdSeats.SeatsPerRowBehindNearNet * CrowdSeats.SeatWidth));

            FanLook[] looks = CrowdLooks.Make(seats.Length, random);
            var data = new FanData[seats.Length];
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = -min;
            for (int i = 0; i < seats.Length; i++)
            {
                FanSeat s = seats[i];
                var place = new Vector3((float)s.X, (float)s.Y, (float)s.Z);
                float yaw = (float)s.Yaw + YawSpread * (2f * (float)random.NextDouble() - 1f);
                float scale = Mathf.Lerp(MinScale, MaxScale, (float)random.NextDouble());
                data[i] = new FanData
                {
                    Place = new Vector4(place.x, place.y, place.z, yaw),
                    Shirt = Linear(looks[i].Shirt, (float)looks[i].Phase),
                    Skin = Linear(looks[i].Skin, (int)s.Zone),
                    Hair = Linear(looks[i].Hair, scale)
                };
                min = Vector3.Min(min, place);
                max = Vector3.Max(max, place);
            }
            // A seated fan reaches about 0.6 m round its seat and 1.5 m up.
            _bounds = new Bounds();
            _bounds.SetMinMax(min - new Vector3(0.6f, 0f, 0.8f), max + new Vector3(0.6f, 1.6f, 0.6f));
            _fans = new GraphicsBuffer(GraphicsBuffer.Target.Structured, data.Length, Marshal.SizeOf<FanData>());
            _fans.SetData(data);
            _material.SetBuffer(FansId, _fans);
        }

        static Vector4 Linear(ArenaColour c, float w)
        {
            Color linear = ((Color)RibbonBoards.ColourOf(c)).linear;
            return new Vector4(linear.r, linear.g, linear.b, w);
        }

        /// <summary>Follows the Arena Show: each zone's clip clock runs at its state's speed (CrowdLooks.Motion) and its
        /// fans take its light (CrowdLooks.Light). seconds: a running clock (real time).</summary>
        public void Show(ArenaState state, double seconds)
        {
            double step = double.IsNaN(_lastSeconds) ? 0 : Math.Max(0, Math.Min(MaxStep, seconds - _lastSeconds));
            _lastSeconds = seconds;
            foreach (ArenaZone zone in Zones)
            {
                CrowdState s = state.CrowdIn(zone);
                _clock[(int)zone] = (_clock[(int)zone] + step * CrowdLooks.Motion(s)) % _clip.Seconds; // float-safe in the shader
                _light[(int)zone] = (float)CrowdLooks.Light(s);
            }
            _material.SetVector(ZoneClockId, new Vector4((float)_clock[0], (float)_clock[1], (float)_clock[2], 0));
            _material.SetVector(ZoneLightId, new Vector4(_light[0], _light[1], _light[2], 1));
        }

        void OnEnable() => RenderPipelineManager.beginCameraRendering += Draw;

        void OnDisable() => RenderPipelineManager.beginCameraRendering -= Draw;

        /// <summary>Draws every fan for this camera in one call, just before the camera renders, so a Camera.Render with no
        /// player loop (the picture test) gets the fans too.</summary>
        void Draw(ScriptableRenderContext context, Camera camera)
        {
            if (!Drawn || _fans == null || camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection) return;
            _material.SetMatrix(CrowdToWorldId, transform.localToWorldMatrix);
            var rp = new RenderParams(_material)
            {
                camera = camera,
                worldBounds = WorldBounds(),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer
            };
            Graphics.RenderMeshPrimitives(rp, _body.Mesh, 0, _seats.Length * Math.Max(1, Copies));
        }

        Bounds WorldBounds()
        {
            var world = new Bounds(transform.TransformPoint(_bounds.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                world.Encapsulate(transform.TransformPoint(new Vector3(
                    (i & 1) == 0 ? _bounds.min.x : _bounds.max.x,
                    (i & 2) == 0 ? _bounds.min.y : _bounds.max.y,
                    (i & 4) == 0 ? _bounds.min.z : _bounds.max.z)));
            return world;
        }

        void OnDestroy()
        {
            _fans?.Release();
            _fans = null;
            SceneObjects.DestroySafely(_material);
            _body?.Destroy();
        }
    }
}
