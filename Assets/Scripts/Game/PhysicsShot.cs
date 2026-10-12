using System;
using System.Collections.Generic;
using HockeyGame.Core;
using Unity.Collections;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// One Shot flown by Unity physics (ADR 0004): launches the physics puck (PhysicsPuck) and, after each physics step, gives
    /// the Whistle rule the puck's facts, read from the Rigidbody (Interpolate makes the transform lag). Tells Whistled once,
    /// with the result. Physics itself is stepped by whoever owns it: the game's physics loop, or a test (Physics.Simulate).
    /// After the Whistle it keeps watching the puck (crossing, hits, the view) but judges no more.
    /// Dispose when done: it stops listening for contacts and removes the puck.
    /// </summary>
    public sealed class PhysicsShot : IDisposable
    {
        /// <summary>The physics step (seconds): 1/120 s, as Fixed Timestep in ProjectSettings/TimeManager.asset.</summary>
        public const float StepSeconds = 1f / 120f;

        const float W = (float)NetFaceMapping.WidthMetres;
        const float H = (float)NetFaceMapping.HeightMetres;

        readonly Camera _camera;
        readonly WhistleRule _rule;
        readonly HashSet<Collider> _ownShapes = new HashSet<Collider>();
        readonly Plane[] _view = new Plane[6];
        bool _touched;

        /// <param name="parent">Where the puck goes in the scene.</param>
        /// <param name="camera">The shooter's camera: the launch starts below its view, and "gone" includes out of its view.</param>
        /// <param name="random">Turns, tips and spins the puck at launch; seeded in tests so Shots repeat.</param>
        /// <param name="findFile">The puck model lookup, for tests (as ModelFiles).</param>
        public PhysicsShot(Transform parent, Camera camera, RoundSettings settings, IRandomSource random, Func<string, GameObject> findFile = null)
        {
            _camera = camera;
            _rule = new WhistleRule(settings);
            Puck = PhysicsPuck.Make(parent, findFile);
            Puck.gameObject.SetActive(false); // not seen and not hit until the launch
            foreach (Collider shape in Puck.GetComponentsInChildren<Collider>(true)) _ownShapes.Add(shape);
            Settings = settings;
            Random = random;
            Physics.ContactEvent += OnContacts;
        }

        public RoundSettings Settings { get; }
        public IRandomSource Random { get; }
        public Rigidbody Puck { get; }

        /// <summary>Where the puck was launched from (lifted for its tip), and where it was aimed (on the Net Face).</summary>
        public Vector3 Start { get; private set; }
        public Vector3 Target { get; private set; }
        public Vector3 LaunchVelocity { get; private set; }
        public bool Launched { get; private set; }

        public double SecondsSinceShot { get; private set; }
        /// <summary>The puck's centre has passed the Net Face plane (z = 0), at any time this Shot.</summary>
        public bool CrossedNetFace { get; private set; }
        /// <summary>The puck has hit something other than the ice (a post, the crossbar, the catch), at any time this Shot.</summary>
        public bool Touched => _touched;
        /// <summary>The first thing other than the ice the puck hit this Shot (null before).</summary>
        public Collider FirstTouched { get; private set; }
        /// <summary>The push of that first hit (N s): how hard it was, for its sound.</summary>
        public Vector3 FirstTouchImpulse { get; private set; }
        /// <summary>The last facts given to the Whistle rule.</summary>
        public PuckFacts LastFacts { get; private set; }

        public bool HasWhistled => _rule.HasWhistled;
        /// <summary>The result once the Whistle has gone (Goal, Save or Miss); null before.</summary>
        public ShotResultKind? Result => _rule.Result;
        /// <summary>Seconds from the Shot to the Whistle; NaN before.</summary>
        public double WhistleSeconds { get; private set; } = double.NaN;

        /// <summary>Told once, on the step the Whistle goes, with the result.</summary>
        public event Action<ShotResultKind> Whistled;

        /// <summary>
        /// Told on each physics step the puck pushes off something other than the ice (a post, the crossbar, the catch), before
        /// and after the Whistle: what it hit and the push (N s). Contacts with no push (only foreseen) are not told.
        /// </summary>
        public event Action<Collider, Vector3> Hit;

        /// <summary>
        /// Launches the puck at Net Face spot (x, y) (0-100) at speedKmh: from just below the camera's view, as worked out now.
        /// The same for a real Shot and a Test Shot. Once per PhysicsShot.
        /// </summary>
        public void Launch(double x, double y, double speedKmh)
        {
            if (Launched) throw new InvalidOperationException("A PhysicsShot launches once: make a new one for the next Shot.");
            Launched = true;
            Target = PhysicsPuck.Target(x, y);
            Puck.gameObject.SetActive(true);
            LaunchVelocity = PhysicsPuck.Launch(Puck, PhysicsPuck.Start(_camera, Settings), Target, speedKmh, StepSeconds, Random);
            Start = Puck.position;
        }

        /// <summary>
        /// Call after each physics step of stepSeconds: reads the puck and gives the Whistle rule this step's facts. Returns
        /// true once the Whistle has gone. Nothing changes after the Whistle (the puck flies on, judged no more).
        /// </summary>
        public bool AfterStep(float stepSeconds)
        {
            if (!Launched) return false;
            SecondsSinceShot += stepSeconds;
            if (Puck.position.z > 0f) CrossedNetFace = true; // the body, not the transform (Interpolate: the transform lags)
            if (_rule.HasWhistled) return true;
            return Judge(Facts(SecondsSinceShot));
        }

        /// <summary>
        /// Sounds the Whistle now if it has not gone: the cap, taken from the Shot's own clock (as if WhistleCapSeconds had
        /// passed). For a Shot the physics stopped feeding, so the Round is never left waiting. No-op before the launch.
        /// </summary>
        public void WhistleNow()
        {
            if (!Launched || _rule.HasWhistled) return;
            Judge(Facts(Math.Max(SecondsSinceShot, Settings.WhistleCapSeconds)));
        }

        /// <summary>
        /// The puck may go from the screen: it is out of the camera's view now, after it has crossed the Net Face or hit
        /// something. Before that it is never out of sight: it starts below the view, and a slow Shot may lob over its top.
        /// </summary>
        public bool OutOfSight => Launched && (CrossedNetFace || _touched) && OutOfView(Puck.position);

        PuckFacts Facts(double secondsSinceShot)
        {
            Vector3 p = Puck.position;
            bool wholeInNet = p.z > PhysicsPuck.Radius && Mathf.Abs(p.x) < W / 2f && p.y < H && ShotColliders.InsideCatch(p);
            bool behindOutside = p.z > 0f && !ShotColliders.InsideCatch(p);
            // Out of view counts once the puck has crossed the Net Face or hit something: it starts below the view, and a lob
            // may go over its top on the way in.
            bool outOfView = (CrossedNetFace || _touched) && OutOfView(p);
            return new PuckFacts(secondsSinceShot, wholeInNet, gearTouched: false, // #111 gives the goalie hit shapes
                Puck.linearVelocity.magnitude, behindOutside || outOfView);
        }

        bool Judge(PuckFacts facts)
        {
            LastFacts = facts;
            if (!_rule.Step(facts)) return false;
            WhistleSeconds = facts.SecondsSinceShot;
            Whistled?.Invoke(_rule.Result.Value);
            return true;
        }

        /// <summary>The whole puck is outside the camera's view: its centre is more than a radius outside one of the view's sides.</summary>
        bool OutOfView(Vector3 p)
        {
            GeometryUtility.CalculateFrustumPlanes(_camera, _view);
            foreach (Plane side in _view)
                if (side.GetDistanceToPoint(p) < -PhysicsPuck.Radius) return true;
            return false;
        }

        void OnContacts(PhysicsScene scene, NativeArray<ContactPairHeader>.ReadOnly headers)
        {
            if (!Launched) return;
            for (int i = 0; i < headers.Length; i++)
            for (int j = 0; j < headers[i].pairCount; j++)
            {
                ContactPair pair = headers[i].GetContactPair(j);
                Collider other = _ownShapes.Contains(pair.collider) ? pair.otherCollider : _ownShapes.Contains(pair.otherCollider) ? pair.collider : null;
                // A real hit pushes: ContinuousSpeculative also reports contacts it only foresaw, with no push, up to a step's travel away.
                // The ice is known by its material, not its name: anything else with a push is a hit.
                if (other == null || other.sharedMaterial == ShotColliders.Ice || pair.impulseSum == Vector3.zero) continue;
                if (!_touched)
                {
                    _touched = true;
                    FirstTouched = other;
                    FirstTouchImpulse = pair.impulseSum;
                }
                Hit?.Invoke(other, pair.impulseSum);
            }
        }

        public void Dispose()
        {
            Physics.ContactEvent -= OnContacts;
            if (Puck != null) SceneObjects.DestroySafely(Puck.gameObject);
        }
    }
}
