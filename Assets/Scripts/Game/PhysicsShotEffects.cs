using System;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Plays a Shot with Unity physics (ADR 0004), and gives the Round its Whistle. On each Shot (a real Shot or a Test Shot,
    /// the same) it launches a new physics puck (PhysicsShot) from just below the camera's view; after each physics step it
    /// gives the Whistle rule the puck's facts, and at the Whistle it tells the Round the result (Round.Whistle).
    /// <para>
    /// At the Whistle: a GOAL! (the line crossing) turns on the goal light; the goalie's after-shot motion (AfterShot) starts
    /// from it, with the real result (GoalieView.Judge), so a Miss shows Relief. The Annoyed Gesture waits for the puck to
    /// meet the netting (the catch). Post and crossbar hits sound from the physics contacts (HitSound).
    /// </para>
    /// <para>
    /// The puck stays until the next Shot, which replaces it. It is hidden only once it is out of the camera's view after it
    /// has crossed the Net Face or hit something (PhysicsShot.OutOfSight): it flies up into view, and a slow Shot's lob is not
    /// hidden in the air. Builds the Shot colliders (ShotColliders) under the rink.
    /// </para>
    /// Interim: the goalie has no hit shapes yet (#111), so the puck passes through him and every Shot that is not a GOAL!
    /// is a Miss. He still moves as the Round's race timed him (Reach in time). The scripted flight (ShotEffects) is not used.
    /// </summary>
    public sealed class PhysicsShotEffects : IDisposable
    {
        /// <summary>
        /// A Shot still with no Whistle this long (real seconds) after the cap (RoundSettings.WhistleCapSeconds) gets the cap's
        /// Whistle from its own clock (PhysicsShot.WhistleNow): the Round is never left waiting if the physics stops feeding it.
        /// </summary>
        public const float NoWhistleGraceSeconds = 0.5f;

        /// <summary>A hit on a post or the crossbar pushes at least this hard (N s) to ring: not a puck resting on it.</summary>
        public const float PingImpulse = 0.05f;

        /// <summary>At most one ping in this long (physics seconds): a puck rattling on the frame rings once.</summary>
        public const float PingGapSeconds = 0.1f;

        readonly Transform _rink;
        readonly Camera _camera;
        readonly IRandomSource _random;
        readonly GoalieView _goalie;
        readonly Func<string, GameObject> _findFile;
        readonly GoalLight _goalLight;
        readonly ShotResultWatch _newResult = new ShotResultWatch();

        Round _round;          // the Round of the current Shot
        int _shotNumber;       // that Round's ShotsSubmitted when the current Shot was launched
        float _clock;          // real seconds since the current Shot
        float _whistledAt = -1f, _nettedAt = -1f; // _clock at the Whistle and when the puck first met the catch (-1: not yet)
        double _lastPing = double.NegativeInfinity; // the Shot's physics seconds at its last ping
        SoundCue? _pendingHit; // a hit sound from the physics steps since the last frame

        /// <param name="random">Turns, tips and spins each puck at launch (PhysicsPuck.Launch).</param>
        /// <param name="goalie">The goalie: reacts as the Round's race timed him, and after the Whistle as the result says.</param>
        /// <param name="findFile">Model file lookup for the puck and the goal light; null = Assets/Resources/Models (ModelFiles).</param>
        public PhysicsShotEffects(Transform rink, Camera camera, IRandomSource random, GoalieView goalie = null, Func<string, GameObject> findFile = null)
        {
            _rink = rink;
            _camera = camera;
            _random = random;
            _goalie = goalie;
            _findFile = findFile;
            ShotColliders.Build(rink);
            _goalLight = new GoalLight(rink, findFile);
        }

        /// <summary>The current Shot's physics puck and its Whistle; null before the first Shot.</summary>
        public PhysicsShot Shot { get; private set; }

        /// <summary>
        /// The current Shot's Flight Time (ShotFlight: from its launch to its Net Face spot), which the goalie's ease-in is timed by
        /// (Breakaway.GoalieSet), the same before and after the Whistle. 0 before the first Shot.
        /// </summary>
        public float FlightSeconds { get; private set; }

        /// <summary>The goal light is on: from a GOAL!'s Whistle (the line crossing) to the end of its result hold.</summary>
        public bool GoalLightOn { get; private set; }

        /// <summary>The sound of a hit the puck made in the physics steps before this frame (a post or the crossbar: PostPing); null for none.</summary>
        public SoundCue? HitSound { get; private set; }

        /// <summary>
        /// Call once per frame with real seconds. Launches the puck when the Round is given a Shot, keeps the goalie's
        /// after-shot motion on the Whistle's clock, and turns the goal light off when the result hold ends.
        /// </summary>
        public void Update(Round round, float realSeconds)
        {
            HitSound = _pendingHit;
            _pendingHit = null;
            bool newShot = _newResult.TakeNewShot(round);
            _newResult.TakeNew(round); // results come from the Whistle; Too Late has no puck and leaves the last one be
            if (newShot)
            {
                Launch(round);
                realSeconds = 0f; // the Shot frame: the clock starts with the Round's result hold, whichever way the Shot came in
            }
            if (Shot == null) return;
            _clock += realSeconds;

            // No hang: a Shot the physics stopped feeding still gets the cap's Whistle.
            if (!Shot.HasWhistled && _clock >= round.Settings.WhistleCapSeconds + NoWhistleGraceSeconds) Shot.WhistleNow();

            if (round.Phase != RoundPhase.ResultHold)
            {
                SetGoalLight(false);
                return;
            }
            _goalie?.Watch(Shot.Puck.transform.position, _whistledAt < 0f ? -1f : _clock - _whistledAt, _nettedAt < 0f ? -1f : _clock - _nettedAt);
        }

        /// <summary>
        /// Call after each physics step of stepSeconds (the game's: after FixedUpdate's step; a test's: after Physics.Simulate).
        /// Gives the Whistle rule the puck's facts, and hides the puck once it is out of sight.
        /// </summary>
        public void AfterPhysicsStep(float stepSeconds)
        {
            if (Shot == null) return;
            Shot.AfterStep(stepSeconds);
            if (Shot.Puck.gameObject.activeSelf && Shot.OutOfSight) Shot.Puck.gameObject.SetActive(false);
        }

        void Launch(Round round)
        {
            Replace();
            SubmittedShot shot = round.LastShot.Value;
            _round = round;
            _shotNumber = round.ShotsSubmitted;
            _clock = 0f;
            _whistledAt = _nettedAt = -1f;
            _lastPing = double.NegativeInfinity;
            SetGoalLight(false);

            Shot = new PhysicsShot(_rink, _camera, round.Settings, _random, _findFile);
            Shot.Whistled += OnWhistled;
            Shot.Hit += OnHit;
            Shot.Launch(shot.X, shot.Y, shot.SpeedKmh); // also off the Net Face: judged by the same rule
            FlightSeconds = (float)ShotFlight.Seconds(Vector3.Distance(Shot.Start, Shot.Target), shot.SpeedKmh);

            // The goalie moves as the Round's race timed him (Reach in time): the race's result (ScriptedShot) picks his reach.
            if (round.LastRace.HasValue) _goalie?.StandFor(round.LastRace.Value);
            ShotRace? race = round.LastRace;
            _goalie?.React(ScriptedShot.Result(round), Shot.Start, race?.SaveReaction, race?.LateMarginSeconds ?? double.PositiveInfinity,
                round.Settings.LateMarginLimitSeconds);
        }

        /// <summary>The last Shot goes: its Whistle first if it never had one (the Round ignores it unless it is still waiting for that Shot).</summary>
        void Replace()
        {
            if (Shot == null) return;
            Shot.WhistleNow();
            Shot.Whistled -= OnWhistled;
            Shot.Hit -= OnHit;
            Shot.Dispose();
            Shot = null;
        }

        void OnWhistled(ShotResultKind kind)
        {
            _whistledAt = _clock;
            if (_round.AwaitingWhistle && _round.ShotsSubmitted == _shotNumber) _round.Whistle(kind); // only for the Shot it waits for
            if (kind == ShotResultKind.Goal) SetGoalLight(true);
            _goalie?.Judge(kind);
        }

        void OnHit(Collider other, Vector3 impulse)
        {
            if (other.sharedMaterial == ShotColliders.Catch)
            {
                if (_nettedAt < 0f) _nettedAt = _clock; // the puck meets the netting
                return;
            }
            if (other.sharedMaterial != ShotColliders.Frame || impulse.magnitude < PingImpulse) return;
            if (Shot.SecondsSinceShot - _lastPing < PingGapSeconds) return;
            _lastPing = Shot.SecondsSinceShot;
            _pendingHit = SoundCue.PostPing;
        }

        void SetGoalLight(bool on)
        {
            GoalLightOn = on;
            _goalLight.SetOn(on);
        }

        /// <summary>Removes the puck and stops listening for its contacts (the Shot colliders and the goal light stay with the rink).</summary>
        public void Dispose() => Replace();
    }
}
