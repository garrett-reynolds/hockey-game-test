using System;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Plays what a Shot did: the puck flies from just below the camera to the Shot's spot on the Net Face
    /// (time from ShotFlight), then Goal (into the net, the netting bulges and the puck drops, red goal light), Save (a goalie block slides
    /// to the spot, the puck bounces away), Miss (the puck flies on past the net) or Too Late (no puck).
    /// Watches the Round and tells it how long the flight takes (Round.DelayResult). Uses real (unscaled) time. No colliders.
    /// </summary>
    public sealed class ShotEffects
    {
        const float H = (float)NetFaceMapping.HeightMetres;
        const float W = (float)NetFaceMapping.WidthMetres;
        const float PuckDiameter = 0.15f;            // about 2x a real puck so it reads at 9 m
        const float PuckThickness = 0.05f;
        const float GloveDepth = 0.1f;
        const float GloveGap = 0.02f;                // between the save block and the pose blocks
        const float GloveZ = GoalieView.BlockZ - GoalieView.BlockDepth / 2f - GloveGap - GloveDepth / 2f; // in front of the pose blocks
        const float SaveZ = GloveZ - GloveDepth / 2f - PuckThickness;
        const float MissCarrySeconds = 0.8f;
        const float CaughtSeconds = 0.25f;           // a goal puck rests in the netting bulge this long, then drops
        const float BaseRailMetres = 0.07f;          // the net's back base-frame tube on the ice (Tools/make_net.py: 2 x TUBE)
        const float Gravity = 9.81f;
        static readonly Color SaveBlockColour = new Color(0.45f, 0.6f, 1f); // lighter than the GoalieView blocks so the slide shows on a Covered Zone

        enum Stage { None, Flying, IntoNet, InNet, Bouncing, Carrying }

        readonly Camera _camera;
        readonly GameObject _puck;
        readonly GameObject _glove;
        readonly GoalLight _goalLight;
        readonly NetBulge _net;        // the netting catches a goal
        readonly bool _boards;         // a missed puck comes back off the end boards (only when there are boards)

        readonly ShotResultWatch _newResult = new ShotResultWatch();

        /// <summary>True on the frame the puck reaches the Net Face (or passes it on a Miss). Sound times the result to this.</summary>
        public bool PuckArrivedThisFrame { get; private set; }
        ShotResult _result;
        Stage _stage;
        float _time;          // seconds in the current stage
        float _flightSeconds;
        Vector3 _start, _target, _velocity;
        Vector3 _gloveFrom, _gloveTo;

        /// <param name="findFile">Model file lookup for the puck and save glove; null = Assets/Resources/Models (see ModelFiles).</param>
        public ShotEffects(Transform rink, Camera camera, Func<string, GameObject> findFile = null)
        {
            _camera = camera;
            _boards = RinkBoards.Present(rink);

            // The flight moves and turns this pivot; its local Y is the puck's flat-face normal.
            _puck = new GameObject("Puck");
            _puck.transform.SetParent(rink, false);
            GameObject puckModel = ModelFiles.Load(ModelFiles.Puck, PlaceholderPuck, findFile);
            puckModel.transform.SetParent(_puck.transform, false);
            ModelFiles.FitTo(puckModel.transform, new Vector3(PuckDiameter, PuckThickness, PuckDiameter));
            ModelFiles.RemoveColliders(puckModel);

            // The save block slides this pivot; the save-glove model (or the old light blue block) fills 0.3 x 0.25 x GloveDepth.
            _glove = new GameObject("Save Block");
            _glove.transform.SetParent(rink, false);
            GameObject gloveModel = ModelFiles.Load(ModelFiles.SaveGlove, PlaceholderSaveBlock, findFile);
            gloveModel.transform.SetParent(_glove.transform, false);
            ModelFiles.FitTo(gloveModel.transform, new Vector3(0.3f, 0.25f, GloveDepth));
            ModelFiles.RemoveColliders(gloveModel);

            _goalLight = new GoalLight(rink);

            _net = new NetBulge(NetBuilder.FindNetting(rink));

            Clear();
        }

        /// <summary>Today's light blue save block, used when Assets/Resources/Models has no save-glove model.</summary>
        static GameObject PlaceholderSaveBlock()
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Save Block Placeholder";
            RinkBuilder.Paint(block, SaveBlockColour);
            return block;
        }

        /// <summary>Today's black cylinder puck, used when Assets/Resources/Models has no puck model.</summary>
        static GameObject PlaceholderPuck()
        {
            var puck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            puck.name = "Puck Placeholder";
            RinkBuilder.Paint(puck, new Color(0.05f, 0.05f, 0.05f));
            return puck;
        }

        /// <summary>
        /// Call once per frame with real seconds. Starts effects when the Round records a new Shot result, and
        /// delays the Round's result by the puck flight so the hold runs its full length after the puck lands.
        /// </summary>
        public void Update(Round round, float realSeconds)
        {
            PuckArrivedThisFrame = false;
            bool isNew = _newResult.TakeNew(round);
            if (round.Phase != RoundPhase.ResultHold)
            {
                if (_stage != Stage.None || _net.Moving || _glove.activeSelf) Clear();
                return;
            }
            if (isNew)
            {
                Play(round.LastResult.Value);
                if (_stage == Stage.Flying) round.DelayResult(_flightSeconds);
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
                        if (p.z >= RinkBuilder.NetDepth - PuckThickness)
                        {
                            p.z = RinkBuilder.NetDepth - PuckThickness;
                            _stage = Stage.InNet;
                            _time = 0f;
                            _velocity = Vector3.zero;
                            _net.Hit(new Vector3(p.x, p.y, RinkBuilder.NetDepth), (float)_result.SpeedKmh);
                        }
                        _puck.transform.position = p;
                    }
                    break;
                case Stage.InNet:
                    {
                        // Rides the bulge at the back of the net, then drops to the ice.
                        // While it drops it eases forward so it lands in front of the back base rail, not inside it.
                        Vector3 p = _puck.transform.position;
                        float clear = Mathf.Clamp01((_time - CaughtSeconds) / 0.2f);
                        p.z = RinkBuilder.NetDepth - Mathf.Lerp(PuckThickness, BaseRailMetres + PuckThickness / 2f, clear) + _net.BackAtHit;
                        if (_time > CaughtSeconds && p.y > PuckDiameter / 2f)
                        {
                            _velocity.y -= Gravity * dt;
                            p.y = Mathf.Max(p.y + _velocity.y * dt, PuckDiameter / 2f);
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
                    {
                        Vector3 p = _puck.transform.position + _velocity * dt;
                        if (_boards) RinkBoards.BounceOffEndBoards(ref p, ref _velocity, PuckDiameter / 2f);
                        _puck.transform.position = p;
                    }
                    if (_time > MissCarrySeconds) { _puck.SetActive(false); _stage = Stage.None; }
                    break;
            }
            _net.Step(dt);
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

        void SetGoalLight(bool on) => _goalLight.SetOn(on);

        void Clear()
        {
            _stage = Stage.None;
            _puck.SetActive(false);
            _glove.SetActive(false);
            SetGoalLight(false);
            _net.Reset();
        }
    }
}
