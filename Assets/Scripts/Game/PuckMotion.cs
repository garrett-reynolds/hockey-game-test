using UnityEngine;

namespace HockeyGame
{
    /// <summary>What the puck hit: each feels different (see PuckMotion.Feel).</summary>
    public enum PuckSurface { Pad, Blocker, Catcher, Stick, Skate, Body, Frame }

    /// <summary>
    /// The puck's motion after it hits something, in pure code (no GameObjects, no physics engine). Hit reflects the
    /// velocity about the surface normal at the contact with a per-surface bounce and grip, and gives the puck tumble from
    /// where on its rim the surface caught it. Rotation is the puck's pivot: its local Y is the flat-face normal.
    /// </summary>
    public sealed class PuckMotion
    {
        /// <summary>The on-screen puck: about 2x a real puck so it reads at 9 m.</summary>
        public const float Diameter = 0.15f;
        public const float Thickness = 0.05f;
        const float Radius = Diameter / 2f;
        /// <summary>Share of the speed a puck leaving a goalie or the frame always has back toward the shooter (-z).</summary>
        const float BackShare = 0.35f;
        /// <summary>Share of the rigid-disc tumble a hit gives (the full figure spins the puck too fast to read).</summary>
        const float TumbleShare = 0.1f;
        const float MaxTumble = 40f;   // rad/s, about 6 turns a second
        const float OffGearSpeed = 1f; // m/s: a puck that lands on top of gear slides off it at least this fast
        const float RestingSpeed = 0.3f; // m/s into a surface: slower than this it slides along it instead of bouncing
        const int WedgedSteps = 36;      // 0.15 s held fast in a crease of the gear: it stays there
        int _wedged;

        /// <summary>
        /// How a surface sends the puck back. Bounce: share of the speed into the surface it keeps out of it.
        /// Grip: share of the speed along the surface it keeps. Speeds clamp to Min..Max m/s so a rebound reads on screen.
        /// Steer: the surface is angled this much to the shooter's left (the blocker turns pucks to the corner).
        /// MaxUp: the most upward speed it can leave with (the stick keeps it low).
        /// </summary>
        public readonly struct Feel
        {
            public readonly float Bounce, Grip, Min, Max, Steer, MaxUp;
            public readonly bool Holds;
            public Feel(float bounce, float grip, float min, float max, float steer = 0f, float maxUp = 2.5f, bool holds = false)
            {
                Bounce = bounce; Grip = grip; Min = min; Max = max; Steer = steer; MaxUp = maxUp; Holds = holds;
            }
        }

        public static Feel FeelOf(PuckSurface surface)
        {
            switch (surface)
            {
                case PuckSurface.Pad: return new Feel(0.55f, 0.75f, 3f, 6f);
                case PuckSurface.Blocker: return new Feel(0.4f, 0.85f, 3f, 5.5f, steer: 0.8f);
                case PuckSurface.Catcher: return new Feel(0f, 0f, 0f, 0f, holds: true);
                case PuckSurface.Stick: return new Feel(0.3f, 0.6f, 2f, 4f, maxUp: 0.3f);
                case PuckSurface.Skate: return new Feel(0.45f, 0.7f, 2f, 5f);
                case PuckSurface.Body: return new Feel(0.25f, 0.5f, 1.5f, 4f);
                default: return new Feel(0.8f, 0.95f, 4f, 9f, maxUp: 4f); // Frame: posts and crossbar ping it away fast
            }
        }

        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Quaternion Rotation { get; private set; }
        /// <summary>World angular velocity, rad/s.</summary>
        public Vector3 AngularVelocity { get; private set; }
        /// <summary>The catching glove has it: it stays where it was caught.</summary>
        public bool Held { get; private set; }

        /// <param name="spin">Flat spin about the face normal, rad/s.</param>
        public PuckMotion(Vector3 position, Quaternion rotation, Vector3 velocity, float spin = 0f, PuckObstacles obstacles = null)
        {
            Position = position;
            Rotation = rotation;
            Velocity = velocity;
            AngularVelocity = rotation * Vector3.up * spin;
            _obstacles = obstacles;
        }

        readonly PuckObstacles _obstacles;
        /// <summary>How close the puck's centre comes to gear or the frame it bounces off after the first contact.</summary>
        public const float CollisionRadius = 0.03f;

        /// <summary>The puck hits a surface (normal: unit, facing the puck) where it is now.</summary>
        public void Hit(Vector3 normal, PuckSurface surface) => Bounce(normal, surface, true);

        /// <param name="first">The Shot's own contact: the rebound speed clamps to the surface's Min..Max so it reads on
        /// screen, and the catching glove holds. Later knocks (off the obstacles) only slow it.</param>
        void Bounce(Vector3 normal, PuckSurface surface, bool first)
        {
            Feel feel = FeelOf(first || surface != PuckSurface.Catcher ? surface : PuckSurface.Body);
            if (feel.Holds)
            {
                Held = true;
                Velocity = Vector3.zero;
                AngularVelocity = Vector3.zero;
                return;
            }
            Vector3 v = Velocity;
            Vector3 n = feel.Steer > 0f ? (normal + Vector3.left * feel.Steer).normalized : normal;
            float into = Vector3.Dot(v, n);
            if (into >= 0f) return; // already leaving
            Vector3 along = v - into * n;
            Vector3 after = -feel.Bounce * into * n + feel.Grip * along;
            float speed = first ? Mathf.Clamp(after.magnitude, feel.Min, feel.Max) : Mathf.Min(after.magnitude, feel.Max);
            after = after.sqrMagnitude > 1e-8f ? after.normalized * speed : n * speed;
            if (first || n.z < 0f) after = TowardShooter(after);
            if (after.y > feel.MaxUp) after.y = feel.MaxUp;
            if (!first && n.y > 0.5f)
            {
                // Landed on top of gear (a pad down on the ice): it slides off, down the slope and toward the shooter, to the ice.
                var across = new Vector2(after.x, after.z);
                if (across.magnitude < OffGearSpeed)
                {
                    Vector2 off = new Vector2(n.x, n.z - 0.5f).normalized * OffGearSpeed;
                    after = new Vector3(off.x, after.y, off.y);
                }
            }

            // Tumble: the impulse acts at the point of the puck's rim (or face) that met the surface.
            Vector3 axis = Rotation * Vector3.up;
            Vector3 rim = Vector3.ProjectOnPlane(-n, axis);
            Vector3 contact = (rim.sqrMagnitude > 1e-8f ? rim.normalized * Radius * rim.magnitude : Vector3.zero)
                              - axis * (Thickness / 2f) * Mathf.Sign(Vector3.Dot(n, axis));
            const float inertia = (3f * Radius * Radius + Thickness * Thickness) / 12f; // per unit mass, about a diameter
            Vector3 tumble = Vector3.Cross(contact, after - v) / inertia * TumbleShare;
            if (tumble.magnitude > MaxTumble) tumble = tumble.normalized * MaxTumble;
            Vector3 spin = Vector3.Project(AngularVelocity, axis) * feel.Grip;
            AngularVelocity = spin + tumble;
            Velocity = after;
        }

        /// <summary>Keeps at least BackShare of the speed going back toward the shooter (-z), the rest kept in its direction.</summary>
        static Vector3 TowardShooter(Vector3 v)
        {
            float speed = v.magnitude;
            float z = -BackShare * speed;
            if (v.z <= z) return v;
            var rest = new Vector2(v.x, v.y);
            float left = Mathf.Sqrt(Mathf.Max(0f, speed * speed - z * z));
            rest = rest.sqrMagnitude > 1e-8f ? rest.normalized * left : new Vector2(0f, left);
            return new Vector3(rest.x, rest.y, z);
        }

        /// <summary>Lying flat and still on the ice.</summary>
        public bool Settled { get; private set; }
        /// <summary>Down on the ice for good: sliding and wobbling flat (or settled).</summary>
        public bool OnIce { get; private set; }

        /// <summary>Fixed step: the motion is the same whatever the frame rate (it only depends on the time since the hit).</summary>
        public const float Step = 1f / 240f;
        const float Gravity = 9.81f;
        const float AirSpin = 0.3f;        // share of the tumble the air takes away each second
        const float IceBounce = 0.35f;     // share of the falling speed it bounces back up with
        const float IceGrip = 0.7f;        // share of the sliding speed it keeps at each bounce
        const float IceTumble = 0.5f;      // share of the tumble it keeps at each bounce
        const float LandSpeed = 0.6f;      // m/s down: slower than this, it stays down
        const float SlideFriction = 0.5f;  // ice friction coefficient (a scaled-up cartoon puck stops within the result hold)
        const float WobbleSeconds = 0.2f;  // the tilt shrinks by e every this long
        const float WobbleRate = 12f;      // rad/s the tilt goes round at first; faster as it flattens (like a spun coin)
        const float FlatSpinDecay = 2f;    // share of the flat spin the ice takes each second

        double _elapsed;
        long _steps;
        // On the ice: the tilt (radians) goes round the vertical; Base is the puck lying flat.
        Quaternion _base;
        float _tilt0, _phase, _iceTime, _flatSpin;

        /// <summary>Moves the puck on by this many seconds (any frame length; it steps at Step).</summary>
        public void Advance(float seconds)
        {
            _elapsed += seconds;
            long due = (long)System.Math.Floor(_elapsed / Step + 0.01); // 0.01: float frame lengths that add up to a step boundary land on it
            while (_steps < due)
            {
                _steps++;
                if (Held || Settled) continue;
                if (OnIce) Slide(Step);
                else Fly(Step);
            }
        }

        void Fly(float dt)
        {
            Vector3 v = Velocity;
            v.y -= Gravity * dt;
            Vector3 p = Position + v * dt;
            Vector3 w = AngularVelocity * (1f - AirSpin * dt);
            Quaternion r = Turn(Rotation, w, dt);

            if (_obstacles != null)
            {
                Vector3 from = Position;
                for (int i = 0; _obstacles.Cast(from, p, CollisionRadius, out Vector3 at, out Vector3 normal, out PuckSurface surface); i++)
                {
                    float into = -Vector3.Dot(v, normal);
                    if (into > RestingSpeed)
                    {
                        Position = at;
                        Velocity = v;
                        AngularVelocity = w;
                        Rotation = r;
                        Bounce(normal, surface, false);
                        _wedged = 0;
                        return;
                    }
                    // Lying against it: slide along it (off it, if it is the top of gear).
                    v += into * normal;
                    if (normal.y > 0.5f && new Vector2(v.x, v.z).magnitude < OffGearSpeed)
                    {
                        Vector2 off = new Vector2(normal.x, normal.z - 0.5f).normalized * OffGearSpeed;
                        v = new Vector3(off.x, v.y, off.y);
                    }
                    p = at + Vector3.ProjectOnPlane(p - at, normal);
                    from = at;
                    if (i == 2) { p = at; v = Vector3.zero; break; } // in a crease of the gear
                }
                // Held fast in a crease of the gear for a while: it stays there, as it lies.
                _wedged = (p - Position).sqrMagnitude < 1e-10f ? _wedged + 1 : 0;
                if (_wedged > WedgedSteps)
                {
                    Settled = true;
                    Velocity = Vector3.zero;
                    AngularVelocity = Vector3.zero;
                    return;
                }
            }

            Vector3 axis = r * Vector3.up;
            float lowest = p.y - LowestBelowCentre(axis);
            if (lowest < 0f)
            {
                p.y -= lowest;
                if (v.y < -LandSpeed)
                {
                    v.y = -IceBounce * v.y;
                    v.x *= IceGrip;
                    v.z *= IceGrip;
                    w = Vector3.Project(w, axis) + Vector3.ProjectOnPlane(w, axis) * IceTumble;
                }
                else
                {
                    Position = p;
                    Velocity = new Vector3(v.x, 0f, v.z);
                    Rotation = r;
                    Land(axis, w);
                    return;
                }
            }
            Position = p;
            Velocity = v;
            AngularVelocity = w;
            Rotation = r;
        }

        static float LowestBelowCentre(Vector3 axis)
        {
            float sin = Mathf.Sqrt(Mathf.Max(0f, 1f - axis.y * axis.y));
            return Radius * sin + Thickness / 2f * Mathf.Abs(axis.y);
        }

        static Quaternion Turn(Quaternion r, Vector3 w, float dt)
        {
            float rate = w.magnitude;
            return rate < 1e-6f ? r : Quaternion.AngleAxis(rate * dt * Mathf.Rad2Deg, w / rate) * r;
        }

        /// <summary>Down for good: it lies on whichever face is nearer the ice and wobbles flat from the tilt it landed at.</summary>
        void Land(Vector3 axis, Vector3 w)
        {
            OnIce = true;
            Vector3 flat = axis.y >= 0f ? Vector3.up : Vector3.down;
            _tilt0 = Vector3.Angle(flat, axis) * Mathf.Deg2Rad;
            Vector3 k = Vector3.Cross(flat, axis);
            if (k.sqrMagnitude < 1e-10f) k = Vector3.right;
            _phase = Mathf.Atan2(k.z, k.x);
            _base = Quaternion.FromToRotation(axis, flat) * Rotation;
            _flatSpin = Vector3.Dot(w, Vector3.up);
            _iceTime = 0f;
            AngularVelocity = Vector3.up * _flatSpin;
        }

        void Slide(float dt)
        {
            _iceTime += dt;
            float tilt = _tilt0 * Mathf.Exp(-_iceTime / WobbleSeconds);
            _phase += WobbleRate * (1f + 2f * (1f - tilt / Mathf.Max(_tilt0, 1e-6f))) * dt;
            _flatSpin *= 1f - FlatSpinDecay * dt;
            _base = Quaternion.AngleAxis(_flatSpin * dt * Mathf.Rad2Deg, Vector3.up) * _base;

            // Friction slows it; the rim scraping while it wobbles slows it more.
            Vector3 v = Velocity;
            float speed = v.magnitude;
            float slow = SlideFriction * Gravity * (1f + 2f * Mathf.Sin(tilt)) * dt;
            v = speed > slow ? v * (1f - slow / speed) : Vector3.zero;
            Vector3 p = Position + v * dt;
            if (_obstacles != null && _obstacles.Cast(Position, p, CollisionRadius, out _, out Vector3 normal, out _))
            {
                // Slid into gear lying on the ice: comes back off it along the ice.
                Vector3 across = new Vector3(normal.x, 0f, normal.z);
                if (across.sqrMagnitude > 1e-6f)
                {
                    across.Normalize();
                    float into = Vector3.Dot(v, across);
                    if (into < 0f) v -= (1f + FeelOf(PuckSurface.Body).Bounce) * into * across;
                }
                p = Position;
            }

            bool flat = tilt < 1f * Mathf.Deg2Rad;
            if (flat) tilt = 0f;
            var k = new Vector3(Mathf.Cos(_phase), 0f, Mathf.Sin(_phase));
            Quaternion r = Quaternion.AngleAxis(tilt * Mathf.Rad2Deg, k) * _base;
            p.y = LowestBelowCentre(r * Vector3.up);
            Position = p;
            Velocity = v;
            Rotation = r;
            AngularVelocity = Vector3.up * _flatSpin;
            if (flat && v == Vector3.zero)
            {
                Settled = true;
                AngularVelocity = Vector3.zero;
            }
        }
    }
}
