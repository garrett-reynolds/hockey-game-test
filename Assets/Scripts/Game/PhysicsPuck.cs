using System;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The physics puck (ADR 0004) and how a Shot launches it. Unity physics flies it; the Whistle rule judges its path
    /// (PhysicsShot). One launch for real Shots and Test Shots: only where the Net Face spot and the speed come from differs.
    /// </summary>
    public static class PhysicsPuck
    {
        public const float Diameter = (float)Breakaway.PuckDiameterMetres;
        public const float Thickness = (float)Breakaway.PuckThicknessMetres;
        public const float Radius = Diameter / 2f;
        public const float Mass = 0.17f;

        /// <summary>Spin about the puck's own axis at launch (rad/s), one way or the other at random.</summary>
        public const float SpinRadiansPerSecond = 25f;
        /// <summary>The puck leaves flat, tipped at random by up to this much (degrees).</summary>
        public const float MaxTiltDegrees = 10f;

        static PhysicsMaterial _material;

        /// <summary>No bounce and full grip, both combined by Minimum: what the puck hits decides its own bounce and grip.</summary>
        public static PhysicsMaterial Material => _material != null ? _material : _material = new PhysicsMaterial("Puck")
        {
            hideFlags = HideFlags.DontUnloadUnusedAsset, // kept: a Unity null check would not see an unloaded one through ??=
            bounciness = 0f, dynamicFriction = 1f, staticFriction = 1f,
            frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum,
        };

        /// <summary>
        /// A new physics puck under parent, drawn at its real size (the puck model, or a dark disc without one). Its hit shape is
        /// a hexagon: 3 boxes turned 0, 60 and 120 degrees, their corners on the rim. Each box reports its contacts (PhysicsShot
        /// listens for them) and lets them be changed (PuckReach). findFile is for tests, as ModelFiles.
        /// </summary>
        public static Rigidbody Make(Transform parent, Func<string, GameObject> findFile = null)
        {
            var puck = new GameObject("Physics Puck");
            puck.transform.SetParent(parent, false);
            for (int i = 0; i < 3; i++)
            {
                var part = new GameObject("Hit Shape " + i);
                part.transform.SetParent(puck.transform, false);
                part.transform.localRotation = Quaternion.Euler(0f, 60f * i, 0f);
                var box = part.AddComponent<BoxCollider>();
                box.size = new Vector3(2f * Radius * Mathf.Cos(Mathf.PI / 6f), Thickness, Radius);
                box.sharedMaterial = Material;
                box.providesContacts = true;
                box.hasModifiableContacts = true; // PuckReach ignores the contacts it foresees but never reaches (#114)
            }
            ModelFiles.Place(puck.transform, ModelFiles.Puck, PlaceholderPuck, findFile, new Vector3(Diameter, Thickness, Diameter));

            var rb = puck.AddComponent<Rigidbody>();
            rb.mass = Mass;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // #103: ContinuousDynamic failed the tunnel test
            rb.solverIterations = 12;
            rb.solverVelocityIterations = 4;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.sleepThreshold = 0f;
            rb.maxAngularVelocity = 100f;
            rb.linearDamping = 0f; // no air drag in flight
            return rb;
        }

        /// <summary>
        /// A puck only to look at, under parent: drawn as the physics puck is (the puck model at real size, or a dark disc), with
        /// no Rigidbody and no hit shapes, so nothing in physics meets it. The Replay's stand-in (ReplayView.ReplayPhysicsPuck).
        /// </summary>
        public static GameObject MakeLook(Transform parent, Func<string, GameObject> findFile = null)
        {
            var puck = new GameObject("Replay Puck");
            puck.transform.SetParent(parent, false);
            ModelFiles.Place(puck.transform, ModelFiles.Puck, PlaceholderPuck, findFile, new Vector3(Diameter, Thickness, Diameter));
            return puck;
        }

        /// <summary>Today's black cylinder puck, used when Assets/Resources/Models has no puck model.</summary>
        internal static GameObject PlaceholderPuck()
        {
            var puck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            puck.name = "Puck Placeholder";
            RinkBuilder.Paint(puck, new Color(0.05f, 0.05f, 0.05f));
            return puck;
        }

        /// <summary>
        /// Where a Shot starts, worked out from the camera now (Breakaway.StartBelowView): on the ice straight ahead of the shooter,
        /// just below the bottom edge of the view, so the puck flies up into view. The Round's Breakaway.ShotStart is the same
        /// point for its own eye.
        /// </summary>
        public static Vector3 Start(Camera camera, RoundSettings settings)
        {
            Transform t = camera.transform;
            RinkPoint p = Breakaway.StartBelowView(t.position.x, t.position.y, t.position.z, t.forward.x, t.forward.y, t.forward.z,
                camera.fieldOfView, settings.ShotStartMarginMetres);
            return new Vector3((float)p.X, (float)p.Y, (float)p.Z);
        }

        /// <summary>
        /// Where the puck is aimed for Net Face spot (x, y) (0-100): on the Net Face, raised to the puck's half-thickness when
        /// lower (the puck cannot fly lower than it lies).
        /// </summary>
        public static Vector3 Target(double x, double y)
        {
            RinkPoint spot = RinkPoint.OnNetFace(x, y);
            return new Vector3((float)spot.X, Mathf.Max((float)spot.Y, Thickness / 2f), 0f);
        }

        /// <summary>
        /// The launch velocity that takes the puck from start to target at the reported speed: the flight time is distance /
        /// speed (ShotFlight.Seconds, as the Flight Time), plus the upward speed that cancels gravity over that time, so it crosses
        /// the target when the Round expects. No air drag. stepSeconds is the physics step: half a step of gravity more makes
        /// up for the step-by-step fall (Unity adds gravity before it moves), so the crossing is exact on the step grid too.
        /// </summary>
        public static Vector3 Velocity(Vector3 start, Vector3 target, double speedKmh, float stepSeconds)
        {
            Vector3 d = target - start;
            float time = (float)ShotFlight.Seconds(d.magnitude, speedKmh);
            return d / time + Vector3.up * (0.5f * -Physics.gravity.y * (time + stepSeconds));
        }

        /// <summary>
        /// Lays the puck at start, flat and turned at random, tipped by up to MaxTiltDegrees and lifted so its low rim edge stays
        /// on the ice, then sets it going (Velocity, from where it lies) toward target at speedKmh, spinning about its own axis.
        /// random gives values in [0, 1): the same values give the same launch. Returns the launch velocity.
        /// </summary>
        public static Vector3 Launch(Rigidbody puck, Vector3 start, Vector3 target, double speedKmh, float stepSeconds, IRandomSource random)
        {
            float turn = (float)random.Next() * 360f;
            float tilt = (float)random.Next() * MaxTiltDegrees;
            float tiltAround = (float)random.Next() * 360f;
            float spin = random.Next() < 0.5 ? -SpinRadiansPerSecond : SpinRadiansPerSecond;
            Quaternion tipped = Quaternion.AngleAxis(tilt, Quaternion.Euler(0f, tiltAround, 0f) * Vector3.right) * Quaternion.Euler(0f, turn, 0f);
            Vector3 at = start + Vector3.up * (Radius * Mathf.Sin(tilt * Mathf.Deg2Rad)); // a tipped puck's low edge reaches this much lower
            Vector3 velocity = Velocity(at, target, speedKmh, stepSeconds);

            puck.transform.SetPositionAndRotation(at, tipped);
            puck.position = at;
            puck.rotation = tipped;
            puck.linearVelocity = velocity;
            puck.angularVelocity = tipped * Vector3.up * spin;
            return velocity;
        }
    }
}
