using System;
using System.Collections.Generic;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>Records the rendered cloth shape, not another simulation. Playback has no cloth solver or colliders.</summary>
    public sealed class ReplayNetting : IDisposable
    {
        // 256 samples cover the default clock bound (4.8 s), including the bottle tail.
        // Longer custom shot clocks can exhaust this cap and hold the last shape.
        // The current ~1700-vertex net costs at most 5 MiB of positions. One cloth read per saved sample.
        public const int MaxSamples = 256;
        public const double SampleInterval = 1.0 / 30.0;
        readonly SkinnedMeshRenderer _source;
        readonly Mesh _shown;
        readonly Func<Vector3[]> _vertices;
        readonly List<double> _times = new List<double>();
        readonly List<Vector3[]> _shapes = new List<Vector3[]>();
        Vector3[] _between;
        bool _wasHidden, _applied;

        public ReplayNetting(SkinnedMeshRenderer source, Func<Vector3[]> vertices)
        {
            _source = source;
            _vertices = vertices;
            _shown = UnityEngine.Object.Instantiate(source.sharedMesh);
            _shown.name = "Recorded net shape";
            _shown.MarkDynamic();
            Look = new GameObject("Replay Netting");
            Look.transform.SetParent(source.transform, false);
            Look.AddComponent<MeshFilter>().sharedMesh = _shown;
            var renderer = Look.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (Look != null) Look.SetActive(false);
        }

        public GameObject Look { get; }
        public int SampleCount => _times.Count;

        public void Begin()
        {
            Restore();
            _times.Clear();
            _shapes.Clear();
        }

        public void Record(double seconds)
        {
            if (_applied || _source == null || _times.Count >= MaxSamples) return;
            if (_times.Count > 0 && seconds - _times[_times.Count - 1] < SampleInterval) return;
            // Cloth.vertices supplies simulation positions; BakeMesh can omit cloth deformation.
            // NetCloth uses a welded grid with one cloth vertex per mesh vertex (no duplicate seams).
            Vector3[] shape = _vertices();
            if (shape == null || shape.Length != _shown.vertexCount) return;
            _times.Add(seconds);
            _shapes.Add(shape);
        }

        public void Apply(double seconds)
        {
            if (_times.Count == 0 || _source == null) return;
            int next = _times.BinarySearch(seconds);
            if (next < 0) next = ~next;
            next = Mathf.Min(next, _times.Count - 1);
            int previous = Math.Max(0, next - 1);
            float blend = next == previous ? 0f : Mathf.Clamp01((float)((seconds - _times[previous]) / (_times[next] - _times[previous])));
            Vector3[] from = _shapes[previous], to = _shapes[next];
            if (_between == null || _between.Length != from.Length) _between = new Vector3[from.Length];
            for (int i = 0; i < from.Length; i++) _between[i] = Vector3.LerpUnclamped(from[i], to[i], blend);
            _shown.vertices = _between;
            _shown.RecalculateBounds();
            if (!_applied) _wasHidden = _source.forceRenderingOff;
            _source.forceRenderingOff = true; // keep the live solver running
            Look.SetActive(true);
            _applied = true;
        }

        public void Restore()
        {
            if (_applied && _source != null) _source.forceRenderingOff = _wasHidden;
            if (Look != null) Look.SetActive(false);
            _applied = false;
        }

        public void Dispose()
        {
            Restore();
            SceneObjects.DestroySafely(Look);
            SceneObjects.DestroySafely(_shown);
            _shapes.Clear();
        }
    }
}
