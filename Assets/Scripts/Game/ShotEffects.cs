using System;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Plays what a Shot did: the puck flies from just below the camera to the Shot's spot on the Net Face
    /// (time from ShotFlight), spinning flat, then Goal (into the net, the netting bulges and the puck drops, red goal light), Save (on the goalie character's gear, or a goalie block slides
    /// to the spot; the puck deflects off the surface it hit, or the catching glove holds it), Miss (the puck flies on past the net, or
    /// deflects off a post or the crossbar) or Too Late (no puck). The deflection is PuckMotion (see #38).
    /// Watches the Round and tells it how long the flight takes (Round.DelayResult). Uses real (unscaled) time. No colliders.
    /// </summary>
    public sealed class ShotEffects
    {
        const float H = (float)NetFaceMapping.HeightMetres;
        const float W = (float)NetFaceMapping.WidthMetres;
        const float PuckDiameter = PuckMotion.Diameter;
        const float PuckThickness = PuckMotion.Thickness;
        const float FlightSpin = 25f;                // rad/s about the face: about 4 turns a second, slow enough to read at 60 fps
        const float GloveDepth = 0.1f;
        const float GloveGap = 0.02f;                // between the save block and the goalie block
        const float GloveZ = GoalieView.BlockZ - GoalieView.BlockDepth / 2f - GloveGap - GloveDepth / 2f; // in front of the goalie block
        const float SaveZ = GloveZ - GloveDepth / 2f - PuckThickness;
        const float MissCarrySeconds = 0.8f;
        const float CaughtSeconds = 0.25f;           // a goal puck rests in the netting bulge this long, then drops
        const float BaseRailMetres = 0.07f;          // the net's back base tube and skirt on the ice: copied by hand from Tools/make_supplied_net.py's output, change both together
        static readonly Color SaveBlockColour = new Color(0.45f, 0.6f, 1f); // lighter than the GoalieView block so the slide shows in front of it

        enum Stage { None, Flying, IntoNet, InNet, Deflecting, Carrying, OnGoalie } // OnGoalie: held in the catching glove, moved by GoalieView.Carry

        readonly Camera _camera;
        readonly GameObject _puck;
        readonly GameObject _glove;
        /// <summary>The red goal light above the net, built off. The Goal Show (Arena Show) turns it on and off, not the puck.</summary>
        public GoalLight GoalLight { get; }
        readonly NetBulge _net;        // the netting catches a goal
        readonly bool _boards;         // a missed puck comes back off the end boards (only when there are boards)
        readonly GoalieView _goalie;   // with the goalie character shown, a save stops on its gear (no save block)

        readonly ShotResultWatch _newResult = new ShotResultWatch();

        /// <summary>True on the frame the puck reaches the Net Face (or passes it on a Miss). Sound times the result to this.</summary>
        public bool PuckArrivedThisFrame { get; private set; }

        /// <summary>
        /// On a Save or a Frame Hit, from the Shot frame on: the sound of the surface the puck visibly hits (ContactSurface).
        /// It differs from the result's own sound when the stick or a skate makes a pad save, or gear in front of the frame
        /// takes a Frame Hit (RoundSoundCues plays it instead). null otherwise.
        /// </summary>
        public SoundCue? ContactSound => _hasContact ? SoundOf(_contactSurface) : (SoundCue?)null;

        /// <summary>On a Save or a Frame Hit, from the Shot frame on: the surface the puck deflects off (null otherwise).</summary>
        public PuckSurface? ContactSurface => _hasContact ? _contactSurface : (PuckSurface?)null;

        /// <summary>The result sound for what the puck hits: wood for the blocker, stick and skates, a thud for pads and body.</summary>
        public static SoundCue SoundOf(PuckSurface surface)
        {
            switch (surface)
            {
                case PuckSurface.Catcher: return SoundCue.GloveCatch;
                case PuckSurface.Blocker: case PuckSurface.Stick: case PuckSurface.Skate: return SoundCue.BlockerHit;
                case PuckSurface.Frame: return SoundCue.PostPing;
                default: return SoundCue.SaveImpact; // pads, mask, visor, body
            }
        }
        ShotResult _result;
        Stage _stage;
        float _time;          // seconds in the current stage
        float _clock;         // seconds since the Shot
        float _arrivedAt, _nettedAt; // _clock when the puck reached the Net Face and when a goal met the netting (-1: not yet)
        float _flightSeconds;
        float _backZ;          // world z where a goal puck meets the netting at the back of the net
        Vector3 _start, _target, _velocity;
        Vector3 _gloveFrom, _gloveTo;
        Quaternion _flightTurn;  // the puck's flight attitude: flat face toward the camera
        Vector3 _netPoint;       // the Shot's spot on the Net Face
        bool _onGoalie;          // the save stops on the goalie character's gear
        PuckObstacles _obstacles; // the frame (and posed goalie) a deflected puck can hit
        PuckMotion _motion;
        static readonly string[] LineGear = { GearPiece.Stick, GearPiece.Blocker, GearPiece.LeftSkate, GearPiece.RightSkate };
        static readonly string[] GoalieGear = { GearPiece.LeftPad, GearPiece.RightPad, GearPiece.Blocker, GearPiece.Catcher, GearPiece.Stick,
            GearPiece.LeftSkate, GearPiece.RightSkate, GearPiece.Mask, GearPiece.Visor, GearPiece.Body };
        bool _hasContact;         // a Save or a Frame Hit: the puck deflects off this surface (worked out in Play)
        Vector3 _contactNormal;
        PuckSurface _contactSurface;

        /// <param name="findFile">Model file lookup for the puck and save glove; null = Assets/Resources/Models (see ModelFiles).</param>
        /// <param name="goalie">The goalie; when it shows the character, a save stops on the character's gear.</param>
        public ShotEffects(Transform rink, Camera camera, Func<string, GameObject> findFile = null, GoalieView goalie = null)
        {
            _camera = camera;
            _goalie = goalie;
            _boards = RinkBoards.Present(rink);

            // The flight moves and turns this pivot; its local Y is the puck's flat-face normal.
            _puck = new GameObject("Puck");
            _puck.transform.SetParent(rink, false);
            ModelFiles.Place(_puck.transform, ModelFiles.Puck, PlaceholderPuck, findFile, new Vector3(PuckDiameter, PuckThickness, PuckDiameter));

            // The save block slides this pivot; the save-glove model (or the old light blue block) fills 0.3 x 0.25 x GloveDepth.
            _glove = new GameObject("Save Block");
            _glove.transform.SetParent(rink, false);
            ModelFiles.Place(_glove.transform, ModelFiles.SaveGlove, PlaceholderSaveBlock, findFile, new Vector3(0.3f, 0.25f, GloveDepth));

            GoalLight = new GoalLight(rink);

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
                if (round.LastRace.HasValue) _goalie?.StandFor(round.LastRace.Value); // he reacts from where the race measured him
                Play(round.LastResult.Value, round.LastRace, round.Settings.LateMarginLimitSeconds);
                if (_stage == Stage.Flying) round.DelayResult(_flightSeconds);
                // The puck starts where it is on the Shot frame, with the Round's result-hold clock at 0, whichever way the
                // Shot came in (a USB Shot before this call, a Test Shot after it): its flight keeps time with the goalie's
                // move, which runs on the same clock as the race timed it (Breakaway.GoalieSet, #63).
                realSeconds = 0f;
            }
            Step(realSeconds);
        }

        SoundCue _saveSound; // the save sound of the Shot's Save Reaction (Play)

        void Play(ShotResult result, ShotRace? race, double lateMarginLimitSeconds)
        {
            SaveReaction? reaction = race?.SaveReaction;
            Clear();
            _result = result;
            _saveSound = SaveReactions.Sound(reaction ?? SaveReactions.For(result.X, result.Y)); // the gear the race put on the puck's line
            _clock = 0f;
            _arrivedAt = _nettedAt = -1f;
            if (!result.HasShot) return; // Too Late: no puck

            Transform cam = _camera.transform;
            _start = cam.position + cam.forward * (float)Breakaway.PuckAheadMetres - cam.up * (float)Breakaway.PuckBelowMetres; // as Breakaway.ShotStart
            NetFaceMapping.ToMetres(result.X, result.Y, out double right, out double up);
            const float FrameFrontZ = -NetBuilder.Post / 2f - PuckThickness / 2f; // puck centre touching the front of a post or the crossbar
            float z = result.Kind == ShotResultKind.Save ? SaveZ
                : NetFrame.At(result.X, result.Y) != FrameHit.None ? FrameFrontZ : 0f; // a Frame Hit stops on the frame's face
            _target = new Vector3((float)right, (float)up, z);
            _netPoint = new Vector3((float)right, (float)up, 0f);
            _goalie?.React(result, _start, reaction, race?.LateMarginSeconds ?? double.PositiveInfinity, lateMarginLimitSeconds); // the goalie character's Save Reaction from the race (or reach) to where this Shot goes
            bool onGoalie = false; // a save on the goalie character stops on its reaction's gear, with no save block
            if (result.Kind == ShotResultKind.Save && _goalie != null && _goalie.SaveStop(_start, _netPoint, PuckThickness, out Vector3 stop))
            {
                _target = stop;
                onGoalie = true;
            }
            _onGoalie = onGoalie;
            _obstacles = null;
            if (result.Kind == ShotResultKind.Save || NetFrame.At(result.X, result.Y) != FrameHit.None)
            {
                // Measured now, on the goalie's reaction as it ends, so the rebound meets the gear where the save was made.
                _obstacles = new PuckObstacles();
                NetBuilder.AddFrame(_obstacles);
                _goalie?.AddObstacles(_obstacles);
            }
            bool onLine = false;
            bool frameHit = result.Kind != ShotResultKind.Save && _obstacles != null;
            if (((onGoalie && _saveSound != SoundCue.GloveCatch) || frameHit)
                && _obstacles.Cast(_start, _target, PuckThickness / 2f, out Vector3 first, out _contactNormal, out _contactSurface, frameHit ? GoalieGear : LineGear))
            {
                // A Frame Hit the goalie's gear is in front of: it knocks off that gear, still a Miss. (A Miss reach keeps off
                // the puck's line, #42, so this only guards against gear left on it.)
                if (_contactSurface == PuckSurface.Catcher) _contactSurface = PuckSurface.Body; // no catch on a Miss
                // The stick or a skate is on the puck line in front of the Save Reaction's gear: the puck stops there, not
                // through it, and deflects off its real face. The catching glove always catches.
                _target = first;
                onLine = true;
            }
            _hasContact = _obstacles != null;
            if (_hasContact && !onLine) Contact(result);
            _flightSeconds = (float)ShotFlight.Seconds(Vector3.Distance(_start, _target), result.SpeedKmh);
            _velocity = (_target - _start) / _flightSeconds;

            _puck.transform.position = _start;
            _flightTurn = Quaternion.LookRotation(Vector3.up, -_velocity.normalized); // flat face toward the camera
            _puck.transform.rotation = _flightTurn;
            _puck.SetActive(true);
            _stage = Stage.Flying;
            _time = 0f;

            if (result.Kind == ShotResultKind.Save && !onGoalie)
            {
                // The block starts across from where the race measured the goalie (his Set-up spot; the Net Face centre
                // without a race) at the puck's height and slides to the puck spot.
                float spotX = race.HasValue ? (float)race.Value.Spot.X : 0f;
                _gloveFrom = new Vector3(spotX, _target.y, GloveZ);
                _gloveTo = new Vector3(_target.x, _target.y, GloveZ);
                _glove.transform.position = _gloveFrom;
                _glove.SetActive(true);
            }
        }

        void Step(float dt)
        {
            _time += dt;
            _clock += dt;
            StepPuck(dt);
            // The goalie carries on from its reach once the puck has got there (#43): it watches the puck, gestures.
            if (_stage != Stage.None || _arrivedAt >= 0f)
                _goalie?.Watch(_puck.transform.position, _arrivedAt < 0f ? -1f : _clock - _arrivedAt, _nettedAt < 0f ? -1f : _clock - _nettedAt);
        }

        void StepPuck(float dt)
        {
            switch (_stage)
            {
                case Stage.Flying:
                    if (_glove.activeSelf)
                        _glove.transform.position = Vector3.Lerp(_gloveFrom, _gloveTo, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_time / (_flightSeconds * 0.8f))));
                    if (_time < _flightSeconds)
                    {
                        _puck.transform.position = _start + _velocity * _time;
                        _puck.transform.rotation = FlightRotation(_time);
                        break;
                    }
                    _puck.transform.position = _target;
                    _puck.transform.rotation = FlightRotation(_flightSeconds);
                    float past = _time - _flightSeconds;
                    Arrive();
                    if (_stage == Stage.Deflecting) Deflect(past); // the motion runs from the moment of contact, whatever the frame length
                    break;
                case Stage.IntoNet:
                    {
                        Vector3 p = _puck.transform.position + _velocity * dt;
                        if (p.z >= _backZ - PuckThickness)
                        {
                            p.z = _backZ - PuckThickness;
                            _stage = Stage.InNet;
                            _time = 0f;
                            _nettedAt = _clock;
                            _velocity = Vector3.zero;
                            _net.Hit(new Vector3(p.x, p.y, _backZ), (float)_result.SpeedKmh);
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
                        p.z = _backZ - Mathf.Lerp(PuckThickness, BaseRailMetres + PuckThickness / 2f, clear) + _net.BackAtHit;
                        if (_time > CaughtSeconds && p.y > PuckDiameter / 2f)
                        {
                            _velocity.y -= PuckMotion.Gravity * dt;
                            p.y = Mathf.Max(p.y + _velocity.y * dt, PuckDiameter / 2f);
                        }
                        _puck.transform.position = p;
                    }
                    break;
                case Stage.Deflecting:
                    Deflect(dt);
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
            _arrivedAt = _flightSeconds;
            _time = 0f;
            switch (_result.Kind)
            {
                case ShotResultKind.Goal:
                    // Keep going into the net, kept inside the side panels and roof.
                    _velocity = new Vector3(0f, 0f, Mathf.Max(_velocity.magnitude * 0.5f, 3f));
                    var p = _puck.transform.position;
                    p.x = Mathf.Clamp(p.x, -W / 2f + PuckDiameter, W / 2f - PuckDiameter);
                    p.y = Mathf.Clamp(p.y, PuckThickness, H - PuckDiameter);
                    _puck.transform.position = p;
                    _backZ = _net.BackOfNet(p.x, p.y); // the netting's back slopes: a high goal meets it sooner
                    _stage = Stage.IntoNet;
                    break;
                case ShotResultKind.Save:
                    StartDeflection(_contactNormal, _contactSurface);
                    break;
                default:
                    if (NetFrame.At(_result.X, _result.Y) == FrameHit.None) { _stage = Stage.Carrying; break; } // wide Miss: carry on past the net
                    StartDeflection(_contactNormal, _contactSurface); // off the frame, or the gear in front of it
                    break;
            }
        }

        /// <summary>The surface a Save or Frame Hit not met on the puck line deflects off, and its normal at the stop.</summary>
        void Contact(ShotResult result)
        {
            if (result.Kind == ShotResultKind.Save)
            {
                SoundCue contact = _saveSound;
                if (_onGoalie && _goalie.SaveSurface(_obstacles, _target, out _contactNormal, out _contactSurface)) return;
                // The save block: its face is square to the shooter; the gear it stands for decides the feel.
                _contactNormal = Vector3.back;
                _contactSurface = contact == SoundCue.GloveCatch ? PuckSurface.Catcher : contact == SoundCue.BlockerHit ? PuckSurface.Blocker : PuckSurface.Pad;
                return;
            }
            // Off the round frame: the tube normal where the puck met it.
            FrameHit hit = NetFrame.At(result.X, result.Y);
            _obstacles.Nearest(_target, hit == FrameHit.LeftPost ? NetBuilder.LeftPostPiece : hit == FrameHit.RightPost ? NetBuilder.RightPostPiece : NetBuilder.CrossbarPiece, out _contactNormal, out _);
            _contactSurface = PuckSurface.Frame;
        }

        Quaternion FlightRotation(float seconds) => _flightTurn * Quaternion.AngleAxis(FlightSpin * seconds * Mathf.Rad2Deg, Vector3.up);

        void StartDeflection(Vector3 normal, PuckSurface surface)
        {
            _motion = new PuckMotion(_target, _puck.transform.rotation, _velocity, FlightSpin, _obstacles);
            _motion.Hit(normal, surface);
            _stage = Stage.Deflecting;
        }

        void Deflect(float dt)
        {
            _motion.Advance(dt);
            _puck.transform.position = _motion.Position;
            _puck.transform.rotation = _motion.Rotation;
            // Held in the catching glove: from now on it moves with him as he follows through and recovers (#44). No other gear
            // carries it: a puck at rest on other gear drops off to the ice (PuckMotion, #48).
            if (_goalie != null && _goalie.HasCharacter && _motion.Held)
            {
                _goalie.Carry(_puck.transform);
                _stage = Stage.OnGoalie;
            }
        }

        void Clear()
        {
            _stage = Stage.None;
            _hasContact = false;
            _puck.SetActive(false);
            _goalie?.Carry(null);
            _glove.SetActive(false);
            _net.Reset();
        }
    }
}
