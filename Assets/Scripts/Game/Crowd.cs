using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HockeyGame
{
    /// <summary>
    /// The 3D crowd: a fan in every seat of both tiers all round the bowl (CrowdSeats), each one of 17 body types wearing
    /// one of the 12 Quaternius Modular outfits, with its own skin, hair and team top (CrowdLooks). Near fans (the lower tier in the
    /// near half) use the full body (about 1.5k triangles), far fans (the upper tier and the far half) the light one (about
    /// 400), or flat camera-facing cards when FarCards is on. Every fan is an instance of a static body mesh; the fans are
    /// kept in one per-fan buffer, sorted into slices by body, level and part of the bowl, and each slice the camera can
    /// see is drawn in one call (Graphics.RenderMeshPrimitives) with the crowd shader, which poses each fan from its body's
    /// vertex animation texture.
    /// It follows the Arena Show: Show moves each zone's clips on (CrowdMotion) and sets its light; the shader works out
    /// each fan's pose. The fans behind the near net (the Calm Area zone) sit still and dim from goalie set to the Shot.
    /// </summary>
    [ExecuteAlways]
    public sealed class Crowd : MonoBehaviour
    {
        /// <summary>Folder under Assets/Resources with the crowd's body files and shaders.</summary>
        public const string Folder = "Crowd";
        public const string ShaderFile = "CrowdFan", CardShaderFile = "CrowdCard";
        /// <summary>The body files (Tools/make_crowd.py): [outfit (BodyType.Base, 0..11), level (0 near, 1 far)].</summary>
        public static readonly string[,] BodyFiles =
        {
            { "fan-m-casual-hoodie-near", "fan-m-casual-hoodie-far" }, { "fan-m-casual-2-near", "fan-m-casual-2-far" },
            { "fan-m-suit-near", "fan-m-suit-far" }, { "fan-m-worker-near", "fan-m-worker-far" },
            { "fan-m-punk-near", "fan-m-punk-far" }, { "fan-m-farmer-near", "fan-m-farmer-far" },
            { "fan-w-casual-near", "fan-w-casual-far" }, { "fan-w-suit-near", "fan-w-suit-far" },
            { "fan-w-formal-near", "fan-w-formal-far" }, { "fan-w-worker-near", "fan-w-worker-far" },
            { "fan-w-punk-near", "fan-w-punk-far" }, { "fan-w-adventurer-near", "fan-w-adventurer-far" }
        };
        /// <summary>The clips every body file holds, in CrowdClip order.</summary>
        public static readonly string[] ClipNames =
            { "sit-idle", "wave", "stand-cheer", "oh", "clap", "miss", "stand-cheer-right", "oh-back" };
        /// <summary>The palette's size (CrowdFanData.hlsl _Palette).</summary>
        const int PaletteSize = 16;
        /// <summary>The longest step a zone's clips take in one Show (a pause or a hitch does not jump the fans).</summary>
        const double MaxStep = 0.25;
        /// <summary>Each fan turns a little off straight ahead (radians, either way).</summary>
        const float YawSpread = 0.15f;
        /// <summary>The bowl is cut into this many slices round its middle (per tier) so a camera draws only what it sees.</summary>
        const int Sectors = 16;
        /// <summary>A fan reaches this far round its seat (metres) and this high, standing with its arms up.</summary>
        const float Reach = 0.8f, Top = 2.5f;

        [StructLayout(LayoutKind.Sequential)]
        struct FanData
        {
            public Vector4 Place; // x, y, z (crowd space), yaw
            public Vector4 Shirt; // the team top: rgb (linear), phase 0..1
            public Vector4 Skin;  // rgb (linear), zone
            public Vector4 Hair;  // rgb (linear), height
            public Vector4 Look;  // body shape, 0, waves (1) or not (0), team top (1) or not (0)
            public Vector4 Acts;  // the fan's own picks for a Goal and an Oh (filled by a later task; zero)
        }

        /// <summary>A slice of the fan buffer drawn in one call: one body at one level, one part of the bowl.</summary>
        sealed class Slice
        {
            public int Group, Start, Count;
            public Bounds Bounds;
            public MaterialPropertyBlock Props;
        }

        static readonly int FansId = Shader.PropertyToID("_Fans"), VatId = Shader.PropertyToID("_Vat"),
            ClipInfoId = Shader.PropertyToID("_ClipInfo"), RowsPerShapeId = Shader.PropertyToID("_RowsPerShape"),
            ZonesId = Shader.PropertyToID("_Zones"), ZoneBlendId = Shader.PropertyToID("_ZoneBlend"),
            ZoneLightId = Shader.PropertyToID("_ZoneLight"), ZoneContrastId = Shader.PropertyToID("_ZoneContrast"),
            FlatToneId = Shader.PropertyToID("_FlatTone"), CrowdToWorldId = Shader.PropertyToID("_CrowdToWorld"),
            FanStartId = Shader.PropertyToID("_FanStart"), OhSpreadId = Shader.PropertyToID("_OhSpread"),
            CardsId = Shader.PropertyToID("_Cards"), CardSizeId = Shader.PropertyToID("_CardSize"),
            CardSeatedTopId = Shader.PropertyToID("_CardSeatedTop"), PaletteId = Shader.PropertyToID("_Palette");

        static readonly ArenaZone[] Zones = { ArenaZone.CalmArea, ArenaZone.Near, ArenaZone.Far };

        // Groups: outfit * 2 + level (the row-major order of BodyFiles).
        readonly CrowdBody[] _bodies = new CrowdBody[BodyFiles.Length];
        readonly Material[] _materials = new Material[BodyFiles.Length], _cardMaterials = new Material[BodyFiles.Length];
        Mesh _card;
        readonly List<Slice> _slices = new List<Slice>();
        FanData[] _data;
        GraphicsBuffer _fans;
        FanSeat[] _seats;
        FanLook[] _looks;
        Bounds _bounds;
        readonly CrowdMotion[] _motion = new CrowdMotion[3];
        readonly float[] _light = { 1f, 1f, 1f }, _contrast = { 1f, 1f, 1f };
        readonly Vector4[] _zones = new Vector4[3];
        readonly Plane[] _planes = new Plane[6];
        double _lastSeconds = double.NaN;

        public int FanCount => _seats?.Length ?? 0;
        /// <summary>Fan i's seat and look (in the crowd's own order).</summary>
        public FanSeat SeatOf(int fan) => _seats[fan];
        public FanLook LookOf(int fan) => _looks[fan];
        /// <summary>The body of one outfit (0..11) at one level (0 near, 1 far).</summary>
        public CrowdBody BodyOf(int outfit, int level) => _bodies[outfit * 2 + level];
        /// <summary>The fans' bounds in the crowd's (the rink's) space.</summary>
        public Bounds Bounds => _bounds;
        /// <summary>The clip the zone's fans play (the fans who do not wave sit-idle instead of waving).</summary>
        public CrowdClip ClipIn(ArenaZone zone) => _motion[(int)zone].Clip;
        /// <summary>Where the zone's fans are in their clip (seconds played; it stops while they are still).</summary>
        public double ClockIn(ArenaZone zone) => _motion[(int)zone].Clock;
        /// <summary>The zone's fans' brightness (1 = as lit).</summary>
        public float LightIn(ArenaZone zone) => _light[(int)zone];
        /// <summary>How much of their own colour and shading the zone's fans show (1 = all).</summary>
        public float ContrastIn(ArenaZone zone) => _contrast[(int)zone];
        /// <summary>Off: no fans are drawn (the crowd GPU cost tool draws with and without).</summary>
        public bool Drawn { get; set; } = true;
        /// <summary>On: far fans are flat camera-facing cards instead of the light body (the first cut if the crowd goes
        /// over its 2 ms GPU limit).</summary>
        public bool FarCards { get; set; }
        /// <summary>Fans drawn for the last camera (the slices it could see).</summary>
        public int LastDrawnFans { get; private set; }

        /// <summary>Adds the crowd under rink, in the skin's home jersey (null: the League Skin's), or nothing when a body
        /// file or a shader is missing.</summary>
        public static Crowd Build(Transform rink, TeamSkin skin = null)
        {
            var shader = Resources.Load<Shader>(Folder + "/" + ShaderFile);
            var cardShader = Resources.Load<Shader>(Folder + "/" + CardShaderFile);
            var bodies = new CrowdBody[BodyFiles.Length];
            bool ok = shader != null && cardShader != null;
            for (int g = 0; g < bodies.Length && ok; g++)
            {
                bodies[g] = CrowdBody.Load(BodyFiles[g / 2, g % 2]);
                ok = bodies[g] != null && HasClips(bodies[g]) && (g % 2 == 0 || bodies[g].Cards != null);
            }
            if (!ok)
            {
                Debug.LogWarning("No 3D crowd: a body file in Assets/Resources/" + Folder + " (Tools/make_crowd.sh) or a crowd shader is missing.");
                foreach (CrowdBody b in bodies) b?.Destroy();
                return null;
            }

            var root = new GameObject("Crowd");
            root.transform.SetParent(rink, false);
            var crowd = root.AddComponent<Crowd>();
            crowd.Fill(bodies, shader, cardShader, new System.Random(95), TeamSkin.OrLeague(skin).HomeJerseyColour);
            return crowd;
        }

        static bool HasClips(CrowdBody body)
        {
            if (body.Clips.Length != ClipNames.Length) return false;
            for (int i = 0; i < ClipNames.Length; i++)
                if (body.Clips[i].Name != ClipNames[i] || body.Clips[i].Loops != CrowdMotion.Loops((CrowdClip)i)) return false;
            return true;
        }

        void Fill(CrowdBody[] bodies, Shader shader, Shader cardShader, System.Random random, ArenaColour homeJersey)
        {
            var seconds = new double[ClipNames.Length];
            for (int i = 0; i < seconds.Length; i++) seconds[i] = bodies[0].Clips[i].Seconds;
            for (int z = 0; z < 3; z++) _motion[z] = new CrowdMotion(seconds);
            _card = CardMesh();

            for (int g = 0; g < bodies.Length; g++)
            {
                CrowdBody body = _bodies[g] = bodies[g];
                Vector4[] palette = PaletteOf(body);
                var clipInfo = new Vector4[ClipNames.Length];
                for (int i = 0; i < clipInfo.Length; i++)
                {
                    CrowdBody.Clip c = body.Clips[i];
                    clipInfo[i] = new Vector4(c.FirstRow, c.Frames, c.FramesPerSecond, c.Loops ? 1 : 0);
                }
                _materials[g] = Configure(new Material(shader) { name = "Crowd " + BodyFiles[g / 2, g % 2] }, clipInfo);
                _materials[g].SetTexture(VatId, body.Vat);
                _materials[g].SetFloat(RowsPerShapeId, body.RowsPerShape);
                _materials[g].SetVectorArray(PaletteId, palette);
                if (body.Cards != null)
                {
                    _cardMaterials[g] = Configure(new Material(cardShader) { name = "Crowd cards " + BodyFiles[g / 2, g % 2] }, clipInfo);
                    _cardMaterials[g].SetVectorArray(PaletteId, palette);
                    _cardMaterials[g].SetTexture(CardsId, body.Cards);
                    _cardMaterials[g].SetVector(CardSizeId, new Vector4(body.CardSize.x, body.CardSize.y, body.Cards.width / 2f, body.Cards.height));
                    _cardMaterials[g].SetFloat(CardSeatedTopId, body.CardSeatedTop);
                }
            }

            FanSeat[] seats = CrowdSeats.All();
            FanLook[] looks = CrowdLooks.Make(seats, CrowdSeats.Neighbours(seats), random, homeJersey);
            var yaw = new float[seats.Length];
            for (int i = 0; i < yaw.Length; i++) yaw[i] = (float)seats[i].Yaw + YawSpread * (2f * (float)random.NextDouble() - 1f);

            // Sort the fans into slices: group (outfit and level), then part of the bowl.
            var order = new int[seats.Length];
            var keys = new long[seats.Length];
            for (int i = 0; i < seats.Length; i++)
            {
                order[i] = i;
                keys[i] = (long)SliceKey(seats[i], looks[i]) * seats.Length + i; // stable
            }
            Array.Sort(keys, order);

            _seats = new FanSeat[seats.Length];
            _looks = new FanLook[seats.Length];
            _data = new FanData[seats.Length];
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = -min;
            Slice slice = null;
            int lastKey = -1;
            for (int k = 0; k < order.Length; k++)
            {
                int i = order[k];
                FanSeat s = _seats[k] = seats[i];
                FanLook l = _looks[k] = looks[i];
                BodyType type = CrowdLooks.BodyTypes[l.Body];
                var place = new Vector3((float)s.X, (float)s.Y, (float)s.Z);
                _data[k] = new FanData
                {
                    Place = new Vector4(place.x, place.y, place.z, yaw[i]),
                    Shirt = Linear(l.Shirt, (float)l.Phase),
                    Skin = Linear(l.Skin, (int)s.Zone),
                    Hair = Linear(l.Hair, (float)l.Height),
                    Look = new Vector4(type.Shape, 0, l.Waves ? 1 : 0, l.TeamTop ? 1 : 0),
                    Acts = Vector4.zero
                };
                int key = SliceKey(s, l);
                if (key != lastKey)
                {
                    slice = new Slice { Group = Group(s, l), Start = k, Props = new MaterialPropertyBlock() };
                    slice.Props.SetFloat(FanStartId, k);
                    slice.Bounds = new Bounds(place, Vector3.zero);
                    _slices.Add(slice);
                    lastKey = key;
                }
                slice.Count++;
                slice.Bounds.Encapsulate(place);
                min = Vector3.Min(min, place);
                max = Vector3.Max(max, place);
            }
            foreach (Slice sl in _slices) sl.Bounds.SetMinMax(sl.Bounds.min - new Vector3(Reach, 0, Reach), sl.Bounds.max + new Vector3(Reach, Top, Reach));
            _bounds = new Bounds();
            _bounds.SetMinMax(min - new Vector3(Reach, 0, Reach), max + new Vector3(Reach, Top, Reach));
            MakeBuffer();
        }

        static int Group(FanSeat s, FanLook l) => CrowdLooks.BodyTypes[l.Body].Base * 2 + (CrowdSeats.IsNearFan(s) ? 0 : 1);

        static int SliceKey(FanSeat s, FanLook l)
        {
            double angle = Math.Atan2(s.Z - RinkLayout.CentreLineZ, s.X) + Math.PI;
            int sector = Math.Min(Sectors - 1, (int)(angle / (2 * Math.PI) * Sectors));
            int tier = s.Row < RinkLayout.LowerRows ? 0 : 1;
            return (Group(s, l) * 2 + tier) * Sectors + sector;
        }

        /// <summary>The body's palette for the shaders: per entry the linear rgb (Own and TeamTop colours moved off any
        /// reserved colour; Skin and Hair take the fan's own, so zero here) and the kind in w; 16 entries.</summary>
        static Vector4[] PaletteOf(CrowdBody body)
        {
            var palette = new Vector4[PaletteSize];
            for (int i = 0; i < body.Palette.Length; i++)
            {
                CrowdBody.PaletteEntry p = body.Palette[i];
                bool coloured = p.Kind == CrowdBody.PaintKind.Own || p.Kind == CrowdBody.PaintKind.TeamTop;
                palette[i] = coloured
                    ? Linear(CrowdLooks.SafeOutfitColour(new ArenaColour(p.Colour.r, p.Colour.g, p.Colour.b)), (float)p.Kind)
                    : new Vector4(0, 0, 0, (float)p.Kind);
            }
            return palette;
        }

        static Material Configure(Material m, Vector4[] clipInfo)
        {
            m.SetVectorArray(ClipInfoId, clipInfo);
            m.SetFloat(OhSpreadId, (float)CrowdMotion.OhSpreadSeconds);
            m.SetColor(FlatToneId, ((Color)RibbonBoards.ColourOf(CrowdLooks.FlatTone)).linear);
            return m;
        }

        static Mesh CardMesh()
        {
            var mesh = new Mesh { name = "Crowd card" };
            mesh.SetVertices(new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) });
            mesh.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4);
            return mesh;
        }

        void MakeBuffer()
        {
            if (_data == null || _fans != null) return;
            _fans = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _data.Length, Marshal.SizeOf<FanData>());
            _fans.SetData(_data);
            foreach (Material m in _materials) if (m != null) m.SetBuffer(FansId, _fans);
            foreach (Material m in _cardMaterials) if (m != null) m.SetBuffer(FansId, _fans);
        }

        void ReleaseBuffer()
        {
            _fans?.Release();
            _fans = null;
        }

        static Vector4 Linear(ArenaColour c, float w)
        {
            Color linear = ((Color)RibbonBoards.ColourOf(c)).linear;
            return new Vector4(linear.r, linear.g, linear.b, w);
        }

        /// <summary>Follows the Arena Show: each zone's clips move on (CrowdMotion; the first Show starts straight in its
        /// clips) and its fans take its light (CrowdLooks.Light). seconds: a running clock (real time).</summary>
        public void Show(ArenaState state, double seconds)
        {
            bool first = double.IsNaN(_lastSeconds);
            double step = first ? 0 : Math.Max(0, Math.Min(MaxStep, seconds - _lastSeconds));
            _lastSeconds = seconds;
            var blend = new Vector4();
            var light = new Vector4(1, 1, 1, 1);
            var contrast = new Vector4(1, 1, 1, 1);
            foreach (ArenaZone zone in Zones)
            {
                int z = (int)zone;
                CrowdState s = state.CrowdIn(zone);
                CrowdMotion m = _motion[z];
                if (first) m.Snap(s);
                else m.Step(s, step);
                _light[z] = (float)CrowdLooks.Light(s);
                _zones[z] = new Vector4((int)m.Clip, (float)m.Clock, (int)m.FromClip, (float)m.FromClock);
                blend[z] = (float)m.Blend;
                light[z] = _light[z];
                contrast[z] = _contrast[z] = (float)CrowdLooks.Contrast(s);
            }
            foreach (Material mat in AllMaterials())
            {
                mat.SetVectorArray(ZonesId, _zones);
                mat.SetVector(ZoneBlendId, blend);
                mat.SetVector(ZoneLightId, light);
                mat.SetVector(ZoneContrastId, contrast);
            }
        }

        IEnumerable<Material> AllMaterials()
        {
            foreach (Material m in _materials) if (m != null) yield return m;
            foreach (Material m in _cardMaterials) if (m != null) yield return m;
        }

        void OnEnable()
        {
            MakeBuffer();
            RenderPipelineManager.beginCameraRendering += Draw;
        }

        /// <summary>Lets go of the GPU buffer too (and so before a domain reload); OnEnable makes it again.</summary>
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Draw;
            ReleaseBuffer();
        }

        /// <summary>Draws the fans this camera can see, a slice per call, just before the camera renders, so a Camera.Render
        /// with no player loop (the picture test) gets the fans too.</summary>
        void Draw(ScriptableRenderContext context, Camera camera)
        {
            LastDrawnFans = 0;
            if (!Drawn || _fans == null || double.IsNaN(_lastSeconds)
                || camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection) return;
            Matrix4x4 toWorld = transform.localToWorldMatrix;
            foreach (Material m in AllMaterials()) m.SetMatrix(CrowdToWorldId, toWorld);
            GeometryUtility.CalculateFrustumPlanes(camera, _planes);
            foreach (Slice s in _slices)
            {
                Bounds world = WorldBounds(s.Bounds);
                if (!GeometryUtility.TestPlanesAABB(_planes, world)) continue;
                bool card = FarCards && s.Group % 2 == 1;
                var rp = new RenderParams(card ? _cardMaterials[s.Group] : _materials[s.Group])
                {
                    camera = camera,
                    worldBounds = world,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    layer = gameObject.layer,
                    matProps = s.Props
                };
                Graphics.RenderMeshPrimitives(rp, card ? _card : _bodies[s.Group].Mesh, 0, s.Count);
                LastDrawnFans += s.Count;
            }
        }

        Bounds WorldBounds(Bounds local)
        {
            var world = new Bounds(transform.TransformPoint(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                world.Encapsulate(transform.TransformPoint(new Vector3(
                    (i & 1) == 0 ? local.min.x : local.max.x,
                    (i & 2) == 0 ? local.min.y : local.max.y,
                    (i & 4) == 0 ? local.min.z : local.max.z)));
            return world;
        }

        void OnDestroy()
        {
            ReleaseBuffer();
            foreach (Material m in AllMaterials()) SceneObjects.DestroySafely(m);
            foreach (CrowdBody b in _bodies) b?.Destroy();
            if (_card != null) SceneObjects.DestroySafely(_card);
        }
    }
}
