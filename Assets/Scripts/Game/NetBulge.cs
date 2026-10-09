using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Makes the netting catch a goal. When the netting part carries its own mesh (the net model, see Tools/make_supplied_net.py),
    /// its vertices bulge back around the hit point like a damped spring, ripple outward, and settle back to rest.
    /// The bulge is biggest at the hit point, fades with distance, and is pinned where the netting meets the posts,
    /// crossbar and base frame. Faster Shots bulge more. Positions are world metres with the rink at the origin
    /// (Net Face at z = 0, back of the net toward +z), as in ShotEffects.
    /// A netting part with no mesh of its own (the box net placeholder) shakes as one piece instead, as before.
    /// </summary>
    public sealed class NetBulge
    {
        const float SpringHz = 2.5f;
        const float Damping = 0.25f;               // fraction of critical damping
        const float RadiusMetres = 0.3f;           // falloff: about 1/e at this distance from the hit point
        const float RippleMetresPerSecond = 3f;    // how fast the bulge spreads outward
        const float PinMetres = 0.12f;             // fades to nothing this close to the ice and to the posts/crossbar
        const float MetresPerKmh = 0.0016f;
        const float MinMetres = 0.04f, MaxMetres = 0.22f;
        const float SettleSeconds = 2.3f;          // inside the 2.5 s result; the spring is spent by then
        const float ShakeSeconds = 0.8f;           // box net placeholder
        const float ShakeMetres = 0.04f;
        const float ColumnMetres = 0.03f;          // BackOfNet looks this far across and up for the netting

        readonly Transform _netting;
        readonly Vector3 _nettingRest;
        readonly Mesh _mesh;      // null = shake the whole netting part
        readonly Vector3[] _rest, _moved;
        readonly float[] _weight, _delay;
        Vector3 _localPush;   // one metre toward the back of the net, in the netting's local space
        float _amplitude;
        int _hitVertex;       // the netting vertex nearest the hit point: the puck rides it
        float _time;
        bool _moving;

        /// <summary>True from a Hit until the netting has settled back to rest.</summary>
        public bool Moving => _moving;

        /// <summary>How far (metres) the netting at the hit point is pushed back right now; 0 for the box net.</summary>
        public float BackAtHit { get; private set; }

        /// <param name="netting">The netting part (NetBuilder.FindNetting); null = nothing to move.</param>
        public NetBulge(Transform netting)
        {
            if (netting == null) return;
            _netting = netting;
            _nettingRest = netting.localPosition;
            var filter = netting.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            // Our own copy, so the imported mesh asset is never changed.
            _mesh = Object.Instantiate(filter.sharedMesh);
            _mesh.name = filter.sharedMesh.name + " (bulge)";
            _mesh.MarkDynamic();
            filter.sharedMesh = _mesh;
            _rest = _mesh.vertices;
            _moved = new Vector3[_rest.Length];
            _weight = new float[_rest.Length];
            _delay = new float[_rest.Length];
            // Room for the biggest bulge both ways, so it is never culled and bounds need no per-frame update.
            Vector3 push = netting.InverseTransformVector(Vector3.forward) * MaxMetres;
            Bounds b = _mesh.bounds;
            b.Expand(new Vector3(Mathf.Abs(push.x), Mathf.Abs(push.y), Mathf.Abs(push.z)) * 2f);
            _mesh.bounds = b;
        }

        /// <summary>World z where a goal at this Net Face point (world x, y metres) meets the netting at the back of the net.</summary>
        /// The deepest netting within a few centimetres of that point across and up (else the nearest netting); the net depth for the box net.
        public float BackOfNet(float x, float y)
        {
            if (_mesh == null) return RinkBuilder.NetDepth;
            Matrix4x4 toWorld = _netting.localToWorldMatrix;
            float back = float.NegativeInfinity, nearest = float.MaxValue, nearestZ = RinkBuilder.NetDepth;
            for (int i = 0; i < _rest.Length; i++)
            {
                Vector3 w = toWorld.MultiplyPoint3x4(_rest[i]);
                float dx = w.x - x, dy = w.y - y;
                if (Mathf.Abs(dx) <= ColumnMetres && Mathf.Abs(dy) <= ColumnMetres && w.z > back) back = w.z;
                float r = dx * dx + dy * dy;
                if (r < nearest) { nearest = r; nearestZ = w.z; }
            }
            return float.IsNegativeInfinity(back) ? nearestZ : back; // in a hole wider than the column: the nearest strand
        }

        /// <summary>The puck hits the netting at this point at this Shot speed.</summary>
        public void Hit(Vector3 point, float speedKmh)
        {
            if (_netting == null) return;
            _time = 0f;
            _moving = true;
            if (_mesh == null) return;
            _localPush = _netting.InverseTransformVector(Vector3.forward);
            _amplitude = Mathf.Clamp(speedKmh * MetresPerKmh, MinMetres, MaxMetres);
            Matrix4x4 toWorld = _netting.localToWorldMatrix;
            float nearest = float.MaxValue;
            for (int i = 0; i < _rest.Length; i++)
            {
                Vector3 w = toWorld.MultiplyPoint3x4(_rest[i]);
                float r = Vector3.Distance(w, point);
                if (r < nearest) { nearest = r; _hitVertex = i; }
                float pin = Mathf.SmoothStep(0f, 1f, w.y / PinMetres) * Mathf.SmoothStep(0f, 1f, w.z / PinMetres);
                _weight[i] = Mathf.Exp(-(r * r) / (RadiusMetres * RadiusMetres)) * pin;
                _delay[i] = r / RippleMetresPerSecond;
            }
        }

        /// <summary>Advance by real seconds.</summary>
        public void Step(float dt)
        {
            if (!_moving) return;
            _time += dt;
            if (_mesh == null) { Shake(); return; }
            if (_time >= SettleSeconds) { Reset(); return; }
            float w0 = 2f * Mathf.PI * SpringHz;
            float wd = w0 * Mathf.Sqrt(1f - Damping * Damping);
            for (int i = 0; i < _rest.Length; i++)
            {
                float tau = _time - _delay[i];
                float back = tau <= 0f || _weight[i] < 1e-4f
                    ? 0f
                    : _amplitude * _weight[i] * Mathf.Exp(-Damping * w0 * tau) * Mathf.Sin(wd * tau);
                _moved[i] = _rest[i] + _localPush * back;
                if (i == _hitVertex) BackAtHit = back;
            }
            _mesh.vertices = _moved;
        }

        void Shake()
        {
            float left = ShakeSeconds - _time;
            if (left <= 0f) { Reset(); return; }
            float wobble = Mathf.Sin(left * 70f) * ShakeMetres * left / ShakeSeconds;
            _netting.localPosition = _nettingRest + new Vector3(wobble * 0.5f, wobble * 0.3f, wobble);
        }

        /// <summary>Back to rest now.</summary>
        public void Reset()
        {
            if (_netting == null) return;
            if (_mesh == null) _netting.localPosition = _nettingRest;
            else if (_moving) _mesh.vertices = _rest;
            _moving = false;
            BackAtHit = 0f;
        }
    }
}
