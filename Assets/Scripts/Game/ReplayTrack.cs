using System.Collections.Generic;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// What was seen of a Shot, for the Replay: the poses of some scene parts (the puck, the save block, the goalie and every
    /// part under him), recorded each frame against the Shot's clock. Apply puts the parts in their recorded poses at any
    /// recorded moment, between frames by blending; Restore puts back the live poses Apply replaced. Each part listed is kept
    /// in world space (the puck can change parent when the glove holds it); the parts under it in their parent's space.
    /// It plays back what was drawn: it never works out the Shot again.
    /// </summary>
    public sealed class ReplayTrack
    {
        readonly List<Transform> _parts = new List<Transform>();
        readonly List<bool> _world = new List<bool>();
        readonly List<double> _times = new List<double>();
        readonly List<Vector3> _positions = new List<Vector3>();
        readonly List<Quaternion> _rotations = new List<Quaternion>();
        readonly List<bool> _active = new List<bool>();
        Vector3[] _livePositions;
        Quaternion[] _liveRotations;
        bool[] _liveActive;
        bool _applied;

        /// <summary>Moments recorded since Begin.</summary>
        public int SampleCount => _times.Count;
        /// <summary>The scene parts the track holds poses for.</summary>
        public int PartCount => _parts.Count;
        /// <summary>True while the parts show a recorded pose (between Apply and Restore).</summary>
        public bool Applied => _applied;

        /// <summary>Starts a new recording of these parts and everything under them (a part under another listed part counts once, as listed).</summary>
        public void Begin(params Transform[] roots)
        {
            Restore();
            _parts.Clear();
            _world.Clear();
            _times.Clear();
            _positions.Clear();
            _rotations.Clear();
            _active.Clear();
            var listed = new HashSet<Transform>();
            foreach (Transform root in roots)
                if (root != null) listed.Add(root);
            foreach (Transform root in roots)
                if (root != null) Add(root, true, listed);
            _livePositions = new Vector3[_parts.Count];
            _liveRotations = new Quaternion[_parts.Count];
            _liveActive = new bool[_parts.Count];
        }

        void Add(Transform part, bool world, HashSet<Transform> listed)
        {
            if (_parts.Contains(part)) return;
            _parts.Add(part);
            _world.Add(world);
            foreach (Transform child in part)
                if (!listed.Contains(child)) Add(child, false, listed);
        }

        /// <summary>Records every part's pose now, at seconds on the Shot's clock. A moment no later than the last one is skipped.</summary>
        public void Record(double seconds)
        {
            if (_applied || (_times.Count > 0 && seconds <= _times[_times.Count - 1])) return;
            _times.Add(seconds);
            for (int i = 0; i < _parts.Count; i++)
            {
                Transform t = _parts[i];
                _positions.Add(_world[i] ? t.position : t.localPosition);
                _rotations.Add(_world[i] ? t.rotation : t.localRotation);
                _active.Add(t.gameObject.activeSelf);
            }
        }

        /// <summary>
        /// Shows the parts as they were at seconds on the Shot's clock (held at the first or last moment outside the
        /// recording), keeping their live poses for Restore. Does nothing before anything is recorded.
        /// </summary>
        public void Apply(double seconds)
        {
            if (_times.Count == 0) return;
            if (!_applied)
            {
                for (int i = 0; i < _parts.Count; i++)
                {
                    Transform t = _parts[i];
                    _livePositions[i] = _world[i] ? t.position : t.localPosition;
                    _liveRotations[i] = _world[i] ? t.rotation : t.localRotation;
                    _liveActive[i] = t.gameObject.activeSelf;
                }
                _applied = true;
            }
            int a = 0;
            while (a < _times.Count - 1 && _times[a + 1] <= seconds) a++;
            int b = Mathf.Min(a + 1, _times.Count - 1);
            float blend = b == a || seconds <= _times[a] ? 0f : (float)((seconds - _times[a]) / (_times[b] - _times[a]));
            int n = _parts.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 position = Vector3.Lerp(_positions[a * n + i], _positions[b * n + i], blend);
                Quaternion rotation = Quaternion.Slerp(_rotations[a * n + i], _rotations[b * n + i], blend);
                Set(i, position, rotation, _active[a * n + i]);
            }
        }

        /// <summary>Puts back the live poses Apply replaced (nothing to do if it has not).</summary>
        public void Restore()
        {
            if (!_applied) return;
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i] != null) Set(i, _livePositions[i], _liveRotations[i], _liveActive[i]);
            _applied = false;
        }

        void Set(int i, Vector3 position, Quaternion rotation, bool active)
        {
            Transform t = _parts[i];
            if (t.gameObject.activeSelf != active) t.gameObject.SetActive(active);
            if (_world[i]) t.SetPositionAndRotation(position, rotation);
            else t.SetLocalPositionAndRotation(position, rotation);
        }
    }
}
