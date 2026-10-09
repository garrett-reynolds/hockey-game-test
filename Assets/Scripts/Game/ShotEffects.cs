using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Plays what a Shot did: the puck flies from just below the camera to the Shot's spot on the Net Face
    /// (time from ShotFlight), then Goal (into the net, net shakes, red goal light), Save (a goalie block slides
    /// to the spot, the puck bounces away), Miss (the puck flies on past the net) or Too Late (no puck).
    /// Watches the Round; the Round does not know about the flight. Uses real (unscaled) time. No colliders.
    /// </summary>
    public sealed class ShotEffects
    {
        const float H = (float)NetFaceMapping.HeightMetres;
        const float W = (float)NetFaceMapping.WidthMetres;
        const float NetDepth = 0.6f;                 // matches RinkBuilder
        const float PuckDiameter = 0.15f;            // about 2x a real puck so it reads at 9 m
        const float PuckThickness = 0.05f;
        const float GloveZ = -0.2f;                  // in front of the pose blocks (z -0.13..-0.03)
        const float GloveDepth = 0.1f;
        const float SaveZ = GloveZ - GloveDepth / 2f - PuckThickness;
        const float MissCarrySeconds = 0.8f;
        const float ShakeSeconds = 0.8f;
        const float ShakeMetres = 0.04f;
        const float Gravity = 9.81f;
        static readonly Color SaveBlockColour = new Color(0.45f, 0.6f, 1f); // lighter than the GoalieView blocks so the slide shows on a Covered Zone
        static readonly Color LightOff = new Color(0.25f, 0.05f, 0.05f);
        static readonly Color LightOn = new Color(1f, 0.1f, 0.1f);

        enum Stage { None, Flying, IntoNet, InNet, Bouncing, Carrying }

        readonly Camera _camera;
        readonly GameObject _puck;
        readonly GameObject _glove;
        readonly Material _lightMaterial;
        readonly Light _lightGlow;
        readonly Transform[] _netParts;
        readonly Vector3[] _netRest;

        int _seenResults;

        /// <summary>True on the frame the puck reaches the Net Face (or passes it on a Miss). Sound times the result to this.</summary>
        public bool PuckArrivedThisFrame { get; private set; }
        ShotResult _result;
        Stage _stage;
        float _time;          // seconds in the current stage
        float _flightSeconds;
        Vector3 _start, _target, _velocity;
        Vector3 _gloveFrom, _gloveTo;
        float _shakeLeft;

        public ShotEffects(Transform rink, Camera camera)
        {
            _camera = camera;

            _puck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _puck.name = "Puck";
            _puck.transform.SetParent(rink);
            _puck.transform.localScale = new Vector3(PuckDiameter, PuckThickness / 2f, PuckDiameter); // cylinder is 2 high
            Object.Destroy(_puck.GetComponent<Collider>());
            RinkBuilder.Paint(_puck, new Color(0.05f, 0.05f, 0.05f));

            _glove = RinkBuilder.Cube(rink, "Save Block", Vector3.zero, new Vector3(0.3f, 0.25f, GloveDepth), SaveBlockColour);

            var lamp = RinkBuilder.Cube(rink, "Goal Light", new Vector3(0f, H + 0.3f, NetDepth / 2f), new Vector3(0.25f, 0.25f, 0.25f), LightOff);
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null)
            {
                _lightMaterial = new Material(unlit);
                lamp.GetComponent<Renderer>().sharedMaterial = _lightMaterial;
            }
            else
            {
                _lightMaterial = lamp.GetComponent<Renderer>().sharedMaterial;
            }
            _lightGlow = new GameObject("Goal Light Glow").AddComponent<Light>();
            _lightGlow.transform.SetParent(rink);
            _lightGlow.transform.position = lamp.transform.position + new Vector3(0f, 0f, -0.4f);
            _lightGlow.type = LightType.Point;
            _lightGlow.color = LightOn;
            _lightGlow.range = 4f;
            _lightGlow.intensity = 3f;

            string[] names = { "Back", "Left Side", "Right Side", "Roof" };
            _netParts = new Transform[names.Length];
            _netRest = new Vector3[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                _netParts[i] = rink.Find(names[i]);
                if (_netParts[i] != null) _netRest[i] = _netParts[i].localPosition;
            }

            Clear();
        }

        /// <summary>Call once per frame with real seconds. Starts effects when the Round records a new Shot result.</summary>
        public void Update(Round round, float realSeconds)
        {
            PuckArrivedThisFrame = false;
            int count = round.Results.Count;
            if (count < _seenResults || round.Phase != RoundPhase.ResultHold)
            {
                if (_stage != Stage.None || _shakeLeft > 0f || _glove.activeSelf) Clear();
                _seenResults = count;
                return;
            }
            if (count > _seenResults)
            {
                _seenResults = count;
                if (round.LastResult.HasValue) Play(round.LastResult.Value);
            }
            Step(realSeconds);
        }

        void Play(ShotResult result)
        {
            Clear();
            _result = result;
            if (!result.HasShot) return; // Too Late: no puck

            Transform cam = _camera.transform;
            _start = cam.position + cam.forward * 1.0f - cam.up * 0.35f;
            NetFaceMapping.ToMetres(result.X, result.Y, out double right, out double up);
            float z = result.Kind == ShotResultKind.Save ? SaveZ : 0f;
            _target = new Vector3((float)right, (float)up, z);
            _flightSeconds = (float)ShotFlight.Seconds(Vector3.Distance(_start, _target), result.SpeedKmh);
            _velocity = (_target - _start) / _flightSeconds;

            _puck.transform.position = _start;
            _puck.transform.rotation = Quaternion.LookRotation(Vector3.up, -_velocity.normalized); // flat face toward the camera
            _puck.SetActive(true);
            _stage = Stage.Flying;
            _time = 0f;

            if (result.Kind == ShotResultKind.Save)
            {
                // The block starts at the centre of the Zone the puck hits and slides to the puck spot.
                NetFaceMapping.ZoneRectangle(Zones.Column(result.X), Zones.Row(result.Y), 1f,
                    out float minX, out float minY, out float maxX, out float maxY);
                _gloveFrom = new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, GloveZ);
                _gloveTo = new Vector3(_target.x, _target.y, GloveZ);
                _glove.transform.position = _gloveFrom;
                _glove.SetActive(true);
            }
        }

        void Step(float dt)
        {
            _time += dt;
            switch (_stage)
            {
                case Stage.Flying:
                    if (_glove.activeSelf)
                        _glove.transform.position = Vector3.Lerp(_gloveFrom, _gloveTo, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_time / (_flightSeconds * 0.8f))));
                    if (_time < _flightSeconds)
                    {
                        _puck.transform.position = _start + _velocity * _time;
                        break;
                    }
                    _puck.transform.position = _target;
                    Arrive();
                    break;
                case Stage.IntoNet:
                    {
                        Vector3 p = _puck.transform.position + _velocity * dt;
                        if (p.z >= NetDepth - PuckThickness)
                        {
                            p.z = NetDepth - PuckThickness;
                            _stage = Stage.InNet;
                            _shakeLeft = ShakeSeconds;
                        }
                        _puck.transform.position = p;
                    }
                    break;
                case Stage.Bouncing:
                    _velocity.y -= Gravity * dt;
                    {
                        Vector3 p = _puck.transform.position + _velocity * dt;
                        if (p.y < PuckThickness / 2f) { p.y = PuckThickness / 2f; _velocity.y = -_velocity.y * 0.4f; }
                        _puck.transform.position = p;
                    }
                    break;
                case Stage.Carrying:
                    _puck.transform.position += _velocity * dt;
                    if (_time > MissCarrySeconds) { _puck.SetActive(false); _stage = Stage.None; }
                    break;
            }
            Shake(dt);
        }

        void Arrive()
        {
            PuckArrivedThisFrame = true;
            _time = 0f;
            switch (_result.Kind)
            {
                case ShotResultKind.Goal:
                    SetGoalLight(true);
                    // Keep going into the net, kept inside the side panels and roof.
                    _velocity = new Vector3(0f, 0f, Mathf.Max(_velocity.magnitude * 0.5f, 3f));
                    var p = _puck.transform.position;
                    p.x = Mathf.Clamp(p.x, -W / 2f + PuckDiameter, W / 2f - PuckDiameter);
                    p.y = Mathf.Clamp(p.y, PuckThickness, H - PuckDiameter);
                    _puck.transform.position = p;
                    _stage = Stage.IntoNet;
                    break;
                case ShotResultKind.Save:
                    // Bounce back toward the shooter and off to the side the puck was already heading.
                    float side = _target.x >= 0f ? 1f : -1f;
                    float speed = Mathf.Clamp(_velocity.magnitude * 0.3f, 2f, 8f);
                    _velocity = new Vector3(side * speed * 0.6f, speed * 0.5f, -speed);
                    _stage = Stage.Bouncing;
                    break;
                default: // Miss: carry on past the net
                    _stage = Stage.Carrying;
                    break;
            }
        }

        void Shake(float dt)
        {
            if (_shakeLeft <= 0f) return;
            _shakeLeft -= dt;
            float strength = Mathf.Max(_shakeLeft, 0f) / ShakeSeconds;
            float wobble = Mathf.Sin(_shakeLeft * 70f) * ShakeMetres * strength;
            for (int i = 0; i < _netParts.Length; i++)
                if (_netParts[i] != null)
                    _netParts[i].localPosition = _netRest[i] + new Vector3(wobble * 0.5f, wobble * 0.3f, wobble);
        }

        void SetGoalLight(bool on)
        {
            _lightMaterial.color = on ? LightOn : LightOff;
            _lightGlow.enabled = on;
        }

        void Clear()
        {
            _stage = Stage.None;
            _puck.SetActive(false);
            _glove.SetActive(false);
            SetGoalLight(false);
            _shakeLeft = 0f;
            for (int i = 0; i < _netParts.Length; i++)
                if (_netParts[i] != null) _netParts[i].localPosition = _netRest[i];
        }
    }
}
