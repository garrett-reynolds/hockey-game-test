# Fast thin puck physics in Unity 6.6 (PhysX) - research for issue #100

Scope: 76 mm x 25 mm, ~170 g puck, up to ~45 m/s, Unity built-in 3D physics. Goal is "looks right". Items marked **[judgement]** are my starting values, not sourced; **[test]** means verify in the Editor.

## Recommendation

1. Raise the physics rate from 50 Hz to **120 Hz** (Fixed Timestep 1/120 = 0.00833). Fall back to 1/240 only if tunnelling or jitter remains. Timestep is the first line of defence; CCD is the safety net (Unity says CCD is "not a guarantee" and suggests a smaller timestep for precision: [CCD manual](https://docs.unity3d.com/6000.0/Documentation/Manual/ContinuousCollisionDetection.html)).
2. Puck Rigidbody: **ContinuousDynamic** (sweep-based; hits static and dynamic). Static geometry needs no mode. Try **ContinuousSpeculative** only if sweep CCD gives ghost hits or stalls **[test]**.
3. Make boards, glass and floor colliders **thick** (0.2 m or more, not a 1 cm plane), so one step can never skip them.
4. Puck collider: a **low-poly convex MeshCollider cylinder** (16-24 sides, under 255 triangles), with a **flat-top/bottom chamfer-free** profile. Fall back to a **compound of 3 rotated BoxColliders** if sweep CCD ignores the mesh (see Gotchas). **[judgement]**
5. Solver: keep PGS, but raise iterations on the puck (position 12, velocity 4). `maxAngularVelocity` = 100 on the puck. Interpolation = Interpolate.
6. Net: Cloth only sees sphere/capsule colliders, so the puck needs a proxy sphere/capsule collider for the cloth only. Cloth is one-way (it never pushes the puck), so the net's grip on the puck must be added in code or built without Cloth (see Cloth section).

## Why: travel per step

| Fixed dt | Hz | Puck travel at 45 m/s | vs 25 mm thickness | vs ~60 mm post |
|---|---|---|---|---|
| 0.02 (current) | 50 | 900 mm | 36x | 15x |
| 1/120 | 120 | 375 mm | 15x | 6x |
| 1/240 | 240 | 188 mm | 7.5x | 3x |

(Arithmetic: 45 m/s x dt.) Even at 240 Hz the puck moves more than its own diameter (76 mm) per step, so discrete detection alone can still miss a 60 mm post. CCD is required at any rate. At 120 Hz the cost is 2.4x the physics ticks of today; reasonable for a one-puck game **[judgement]**.

Unity: Fixed Timestep is `Time.fixedDeltaTime` ([API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Time-fixedDeltaTime.html)). Keep Max Allowed Timestep at 0.333 or lower it to ~0.05 to avoid catch-up spirals **[judgement]**.

## Collision detection modes

Source: [CollisionDetectionMode](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/CollisionDetectionMode.html)
- Discrete: no CCD.
- Continuous: sweep CCD against **static** colliders only.
- ContinuousDynamic: sweep CCD against static and dynamic.
- ContinuousSpeculative: speculative CCD against static and dynamic.

Facts from the [CCD manual](https://docs.unity3d.com/6000.0/Documentation/Manual/ContinuousCollisionDetection.html): CCD is supported only for Box, Sphere and Capsule colliders (so a convex mesh puck may get no sweep CCD; **[test]**). Speculative CCD enlarges the broad-phase AABB from linear and angular motion and feeds all potential contacts to the solver, so it can produce false collisions ([speculative CCD page, via search](https://docs.unity3d.com/Manual/speculative-ccd.html)). The sweep page for the same topic is the same URL family.

Choice for the puck: the puck hits static geometry (boards, glass, posts) and the dynamic goalie/skater bodies, so **ContinuousDynamic** covers both. Plain Continuous would miss fast skater-puck hits.

## Solver and global settings (for this project)

| Setting | Current | Recommended start | Note |
|---|---|---|---|
| Fixed Timestep | 0.02 | 0.008333 (1/120) | change |
| Default Solver Iterations | 6 | 6 global; set 12 on puck via `Rigidbody.solverIterations` | per-body override |
| Default Solver Velocity Iterations | 1 | 1 global; 4 on puck | PhysX recommends 1 velocity iteration by default ([PhysX 5.1 docs](https://nvidia-omniverse.github.io/PhysX/physx/5.1.3/docs/RigidBodyDynamics.html)); more helps bounce accuracy **[judgement]** |
| Solver Type | 0 (PGS) | try 1 (TGS) **[test]** | PhysX: TGS is the default since 5.1, a bit slower, converges better (same doc). Check Unity 6.6 exposes it and that it fixes edge jitter. |
| Default Contact Offset | 0.01 | 0.01, try 0.005 | Offset is a distance; a 25 mm puck with 10 mm offset gets a 20 mm combined skin. Lower if the puck "hovers" **[judgement]** |
| Bounce Threshold | 2 | 2 (or 1) | A 25 mm puck dropping lightly should not bounce; fine |
| Default Max Depenetration Velocity | 10 | 10 | Lowering avoids puck pop-out from thick geometry **[judgement]** |
| Sleep Threshold | 0.005 | 0.005; puck uses `sleepThreshold = 0` while in play | See sleep gotcha |
| Max Angular Speed | 50 (project default); Unity API default 7 | 100 on puck | Unity clamps to this max ([maxAngularVelocity](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody-maxAngularVelocity.html)). A spinning puck at ~20 rev/s = 125 rad/s; 100 is a looks-right cap **[judgement]** |
| Friction Type | 0 (patch) | 0, or enable Improved Patch Friction **[test]** | Edge-on rolling and flat sliding both depend on friction patches |
| Rigidbody Interpolation | n/a | Interpolate | Needed because physics (120 Hz) and render rates differ **[judgement]** |
| Mass | n/a | 0.17 kg | Puck weighs 5.5-6 oz (156-170 g): [Globe and Mail quoting the NHL rulebook](https://www.theglobeandmail.com/opinion/the-puck-remains-the-same/article554735) |

Keep the puck's mass within ~1:100 of what it hits where possible; very heavy goalie/skater kinematic bodies are safest (kinematic, not dynamic) **[judgement]**.

## Collider shape

Real puck: 1 in (25.4 mm) thick, 3 in (76.2 mm) across ([Globe and Mail](https://www.theglobeandmail.com/opinion/the-puck-remains-the-same/article554735)).

| Option | Flat on ice | Tilts / flips / rolls on edge | CCD | Verdict |
|---|---|---|---|---|
| Convex MeshCollider, 16-24 sided cylinder | Flat face rests well, but large flat faces can jitter in PhysX contact generation | Yes, true edges | May be unsupported by sweep CCD | Best look; **[test]** CCD |
| Compound of 3 BoxColliders rotated 0/60/120 deg (hexagonal prism) | Good | Rolls like a hexagon, "clicky" on edge | Supported | Best CCD safety; slightly polygonal roll **[judgement]** |
| Capsule on its side | Rolls like a cylinder only along one axis | Wrong end caps | Supported | Not a puck |
| Single Box | Very stable | Wrong: square corners catch | Supported | Use only as CCD helper |
| Sphere | Rolls forever | Never lies flat | Supported | Never as main collider |

Facts: convex mesh colliders are limited to 255 triangles, and only convex meshes may collide with other mesh colliders ([MeshCollider manual](https://docs.unity3d.com/6000.0/Documentation/Manual/class-MeshCollider.html)); the same page says primitives are more efficient and stable for moving bodies. Because the rink (boards, glass) is static, it can be non-convex mesh or boxes; use box colliders for glass/boards where possible **[judgement]**.

Tilt/flip/roll behaviour comes from the contact geometry plus a slightly low `angularDamping` (0.05) and an off-centre hit; no extra code needed **[judgement]**. Use `Rigidbody.centerOfMass` at the geometric centre and keep the inertia tensor default, because the auto tensor for a flat disc is correct (greater inertia about the vertical axis) **[judgement]**.

## Physic materials

Unity: `PhysicsMaterial` has bounciness (0 = none, 1 = no energy loss), dynamicFriction, staticFriction, frictionCombine, bounceCombine ([API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PhysicsMaterial.html)). Combine modes: Average, Minimum, Multiply, Maximum; when two materials differ the higher priority wins in order **Average < Minimum < Multiply < Maximum** ([PhysicsMaterialCombine](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PhysicsMaterialCombine.html)).

Real data: a rubber puck on ice is about mu 0.06-0.1 ([Physics World worked example](https://physicsworld.com/a/physics-on-ice/), a science fair page; assumptions, not lab measurements). Coefficient of restitution for puck targeted at about 0.4 in a puck compound patent, tested at 24 C and -10 C ([US 5,330,184](https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/5330184)); WPI report uses ASTM F2117-10 drop tests on frozen pucks ([WPI](https://digital.wpi.edu/downloads/hh63sx19v?locale=en)). I found no peer-reviewed ice-friction value for rubber pucks, and the 2025 *Friction* review says ice-friction results are hard to compare across methods ([review PDF](https://www.sciopen.com/local/article_pdf/10.26599/FRICT.2025.9441076.pdf)).

All values below are **[judgement]** starting points, tuned for feel, using the real data above only as a guide. Put the same combine modes on **both** materials in a pair so priority is unambiguous.

| Surface | Bounciness | Dynamic fric | Static fric | Friction combine | Bounce combine | Notes |
|---|---|---|---|---|---|---|
| Puck | 0.4 | 0.1 | 0.12 | Minimum | Minimum | Hard rubber, low-friction base |
| Ice | 0.0 | 0.05 | 0.06 | Minimum | Minimum | Puck-ice mu ends at 0.05; do not let Multiply shrink it to 0.005 |
| Boards (dasher) | 0.5 | 0.2 | 0.25 | Minimum | Maximum | Lively; use Maximum on boards to win over puck 0.4 -> result 0.5 |
| Glass | 0.45 | 0.1 | 0.12 | Minimum | Maximum | Slightly deader and slicker than boards |
| Posts / crossbar | 0.55 | 0.2 | 0.25 | Minimum | Maximum | Steel ring: "ping", puck deflects sharply |
| Net (Cloth proxy) | 0.0 | 0.4 | 0.5 | Maximum | Minimum | Soaks the puck; real damping comes from Cloth |
| Goalie pads / skater | 0.2 | 0.3 | 0.35 | Average | Minimum | Dead bounce |

Mixed-mode note: the highest-priority mode wins, so a puck with Minimum and boards with Maximum gets **Maximum** for bounce. That is the intent above but check the board result is 0.5, not 0.4 **[test]**. Because PhysX applies the bounce threshold (2 m/s here), slow touches stay dead even at 0.5.

## Cloth net: two-way interaction limits

- Cloth only collides with CapsuleColliders (`capsuleColliders`) and sphere pairs (`sphereColliders`, `ClothSphereColliderPair`); the sphere limit is 32 ([Cloth](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Cloth.html), [Cloth.sphereColliders](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Cloth-sphereColliders.html)). A cylinder/mesh/box puck is invisible to the cloth.
- Implication: give the puck a **child proxy** (a sphere of ~38 mm radius, or a capsule) listed in the Cloth, with no Rigidbody of its own and set to a layer ignored by other physics. A sphere sits inside the real puck flat, so the net will bulge a bit more than a flat disc would **[judgement]**.
- **Unity Cloth is one-way.** The manual says Cloth does not "apply forces back to the world" and "the simulation is still one-way: cloth reacts to those bodies but doesn't apply forces back" ([Cloth manual](https://docs.unity3d.com/6000.0/Documentation/Manual/class-Cloth.html)). So the map's two-way netting must be faked or built another way: on net contact, detect with a trigger volume behind the goal line and apply drag/damping to the puck, and drive the cloth with the proxy **[judgement]**. A truly two-way net needs a non-Cloth approach (e.g. a mesh of small rigid bodies on joints); that is a separate question.
- At 45 m/s the proxy can pass through cloth in one step (375 mm at 120 Hz). Keep the puck from reaching the net at full speed (mesh drag zone), or slow the net's trigger volume, and keep cloth capsules thick **[judgement]**.
- Cloth runs on the fixed step; verify its cost at 120 Hz **[test]**.

## Risks and gotchas

- **Speculative CCD ghost contacts**: documented false collisions that push a body off course ([speculative CCD page](https://docs.unity3d.com/Manual/speculative-ccd.html)). Forum: ghost collisions are also reported with ContinuousDynamic ([Unity forum](https://discussions.unity.com/t/continuous-collision-detection-produces-ghost-collisions/1543337)). Mitigation: thick, convex-friendly static colliders; no tiny gaps between glass/board panels (overlap them).
- **Sweep CCD limited to Box/Sphere/Capsule** ([CCD manual](https://docs.unity3d.com/6000.0/Documentation/Manual/ContinuousCollisionDetection.html)). A convex mesh puck may fall back to discrete. If tunnelling persists, switch to the box compound or add a box/capsule CCD helper.
- **Flat-face jitter**: a flat convex mesh face resting on a flat surface can chatter. Fix: slightly bevel the rim, raise solver iterations, avoid zero friction, try TGS, set `angularDamping` ~0.05-0.2 **[judgement]**.
- **Cylinder edge rolling**: a true cylinder with enough angular velocity stands on edge and rolls far; real pucks do that but briefly. Use friction and angular drag to stop it, or use a hexagonal compound **[judgement]**.
- **Sleep**: a stationary puck sleeps; the Sleep Threshold (0.005) is per mass-normalised kinetic energy and a sleeping body does not wake from a kinematic contact reliably. PhysX: an actor sleeps when kinetic energy stays below a threshold for a time ([PhysX docs](https://nvidia-omniverse.github.io/PhysX/physx/5.1.3/docs/RigidBodyDynamics.html)). Use `Rigidbody.sleepThreshold = 0` or call `WakeUp()` on play.
- **Angular speed clamp**: a faster-than-max spin gets clamped before each step ([maxAngularVelocity](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody-maxAngularVelocity.html)); raise on the puck or edge-on rolls look sluggish.
- **Bounce threshold**: low-speed bounces vanish under 2 m/s; correct for a puck.
- **Material priority surprise**: mixing combine modes silently picks the higher-priority one; put identical modes on both sides where you want a predictable result.
- **Penetration pop**: Max Depenetration Velocity of 10 m/s can launch a puck out of a bad overlap; keep it low-ish.

## Sources

- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/CollisionDetectionMode.html
- https://docs.unity3d.com/6000.0/Documentation/Manual/ContinuousCollisionDetection.html
- https://docs.unity3d.com/Manual/speculative-ccd.html (via search summary; the 6000.0 URL 404ed)
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody-maxAngularVelocity.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PhysicsMaterial.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PhysicsMaterialCombine.html
- https://docs.unity3d.com/6000.0/Documentation/Manual/class-MeshCollider.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Cloth.html
- https://docs.unity3d.com/6000.0/Documentation/Manual/class-Cloth.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Cloth-sphereColliders.html
- https://nvidia-omniverse.github.io/PhysX/physx/5.1.3/docs/RigidBodyDynamics.html
- https://discussions.unity.com/t/continuous-collision-detection-produces-ghost-collisions/1543337
- https://www.theglobeandmail.com/opinion/the-puck-remains-the-same/article554735
- https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/5330184
- https://digital.wpi.edu/downloads/hh63sx19v?locale=en
- https://physicsworld.com/a/physics-on-ice/
- https://www.sciopen.com/local/article_pdf/10.26599/FRICT.2025.9441076.pdf

Not verified from primary sources: Unity 6.6-specific Physics settings pages (TGS availability, Time.fixedDeltaTime page fetched only by URL), boards/glass restitution values (judgement only).
