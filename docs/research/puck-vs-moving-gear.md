# Can a fast puck pass through moving goalie gear?

Unity 6000.6.5f1, built-in 3D physics (PhysX). Researched 2026-10-12.

## Short answer

Yes. At 45 m/s and 50 Hz the puck moves 0.9 m per physics step, about 36 times its 2.5 cm thickness. Discrete detection will skip thin gear. The built-in CCD modes do not fully fix this for animated kinematic gear:

- Sweep CCD (Continuous / ContinuousDynamic) is linear only. It ignores rotation [1][2].
- Unity's `Rigidbody.collisionDetectionMode` page says only ContinuousSpeculative "can also be used on kinematic objects" [3]. PhysX says the same of its speculative CCD [4].
- Speculative CCD can miss contacts and make ghost contacts [1][5].

So do not rely on CCD alone. Use thick gear colliders, a smaller step for the puck, and a per-step sweep from the puck as the guaranteed fallback.

## Project context

- Fixed Timestep 0.02 s, contact offset 0.01, solver iterations 6/1, SimulationMode 0 (FixedUpdate).
- Goalie has no animator controller (`GoalieCharacter.cs:60`). Bones are posed from code in `RoundDriver.Update` (`RoundDriver.cs:59`), once per render frame.
- Puck: thin cylinder, 2.5 cm tall, 7.6 cm wide, up to ~45 m/s. Collider type not confirmed (see CCD shape limit below).
- Plan: colliders on bones, kinematic rigidbodies moved by the pose.

## Findings

### 1. What each CCD mode does

- Unity has two algorithms behind three options. Continuous and ContinuousDynamic are sweep-based. ContinuousSpeculative is speculative [1].
- Continuous: static (no-Rigidbody) geometry only. ContinuousDynamic: static and dynamic [3][2].
- ContinuousSpeculative: static and dynamic, cheaper, "can also be used on kinematic objects" [3].
- Unity's CCD page says CCD works only with Box, Sphere and Capsule colliders [1]. A cylinder mesh or convex mesh collider on the puck may not get CCD. Unverified for the puck here; check its collider.
- Unity calls CCD "a safety net", not a guarantee. Its own advice if results are still off: reduce the timestep [1].

### 2. Sweep CCD (Continuous, ContinuousDynamic)

- Sweeps the body along its current velocity, finds time of impact, may sub-step [2].
- Unity: sweep modes "can't detect collisions that occur as a result of the physics body rotating". It points to speculative CCD for flipper-like cases [2].
- PhysX: sweep CCD "operates using only linear motion". It is "best-effort". With the default 1 pass, time left after the first impact is dropped [4].
- PhysX contrasts it with speculative CCD, which is "legal" on kinematic actors. That implies kinematic actors are not covered by sweep CCD [4]. This is an inference, not a direct statement.
- PhysX: "paper-thin/tiny objects should be avoided if possible." Their CCD velocity threshold is easily exceeded and time gets dropped [4]. A 2.5 cm puck is in that zone.
- Unity does not say whether a ContinuousDynamic puck is swept against a kinematic target. The Unity pages are silent [2][3].

### 3. Speculative CCD (ContinuousSpeculative)

- Grows each body's broad-phase bounds by its linear and angular motion. Picks up potential contacts for the next step and feeds them to the solver [5].
- It does handle rotation, which sweep CCD does not [5][4].
- Ghost contacts: it uses closest-point contacts, so normals can be wrong. A fast sphere can treat a flat surface as a ramp and jump [5].
- Can still miss: if the solver gives a body enough energy to leave the inflated bounds, it can tunnel [5][4].
- Unity's pages say nothing about kinematic bodies and speculative CCD [5]. The bounds are built from the body's motion, which for a teleported body is probably nil. This is my inference, unverified.
- Forum (community, no Unity staff in thread): a fast kinematic body moved with `MovePosition` gave ghost hits under Continuous, ContinuousDynamic and Speculative. One user says 3D `MovePosition` behaves like a teleport. Another says Unity 6 seems to switch kinematic bodies set to Continuous over to speculative CCD. Both are unofficial [6].

### 4. How kinematic bodies move in PhysX

- A kinematic actor is driven by a target pose each step. It "will appear to have infinite mass" and pushes dynamic bodies away. Nothing pushes it back [7].
- Moving by target lets it push dynamic bodies. A teleport (set global pose) means it "would not push away other dynamic actors in its path" and passes through them [7].
- Unity `Rigidbody.MovePosition` is for kinematic bodies. It moves the body during the physics step ("in each FixedUpdate call"), follows the interpolation setting, and teleports only via `Rigidbody.position` [8]. The page says nothing about collisions.
- By default PhysX does not report kinematic-vs-kinematic or kinematic-vs-static contacts [4]. Not an issue for puck (dynamic) vs gear.

### 5. Transforms set in Update

- Unity syncs transform changes to PhysX automatically before each physics step. Calling `Physics.SyncTransforms()` in FixedUpdate is discouraged [9].
- `SyncTransforms` is for when you change a transform in Update/LateUpdate and run a physics query straight away (e.g. a raycast) [9].
- Consequence: bones posed in `Update` do reach PhysX, but only as the pose at the step start. A collider on a plain transform change (no Rigidbody, or a Rigidbody moved by transform) is a teleport. Between steps the gear jumps from pose A to pose B. PhysX never sees it sweep through the space between (consistent with [7]; the exact behaviour for animated child colliders is not spelled out by Unity: unverified).
- Animator `AnimatePhysics` mode does not apply (no controller). I could not load the AnimatorUpdateMode page (404), so I cite no claim from it.
- `Physics.autoSyncTransforms`: the doc URL returned 404. Unverified; leave it at the default.

### 6. Compound vs many rigidbodies

- No primary source found for either choice. Not verified.
- Reasoning only: one rigidbody per moving gear piece lets each move by `MovePosition`/`MoveRotation` with its own target. Child colliders under a parent move with the parent's transform. Pick per-piece bodies for the fast pieces (pads, stick, glove, blocker).

## What fails and why

1. Discrete puck vs thin gear: 0.9 m per step skips 5 to 15 cm gear.
2. ContinuousDynamic puck vs kinematic gear: sweep is linear and the target may not be swept at all [2][4]. Gear rotation (stick, glove swing) is never covered [2][4].
3. Gear posed by transform in `Update`: seen as a teleport, so no push and no velocity [7].
4. ContinuousSpeculative: handles rotation and kinematic bodies, but gives ghost contacts and can still miss [5][6].
5. Thin gear and puck: dropped CCD time for tiny bodies [4]. Wrong collider type may disable CCD [1].
6. Skinned bones can move far between steps; a hit point can exist only between two sampled poses.

## Recommended setup

1. Puck: Rigidbody, ContinuousDynamic as a baseline. Confirm it has a Box or Capsule collider (a flat box, or capsule oriented like a puck) so CCD applies [1]. Try ContinuousSpeculative as an A/B and compare ghost hits.
2. Gear: each fast piece has its own kinematic Rigidbody with a simple box or capsule collider. Set interpolation to None for gear used in physics (the puck can interpolate).
3. Move gear in `FixedUpdate`, not `Update`: copy the bone pose into a target and call `MovePosition` / `MoveRotation` on the kinematic body [8]. This gives PhysX a target pose each step, so it pushes the puck instead of teleporting [7]. Keep the visual bone pose in Update as now, and sample the latest pose in FixedUpdate. Do not call `SyncTransforms` in FixedUpdate [9].
4. Thickness: make gear colliders at least 8 to 10 cm thick, larger than the puck's 7.6 cm width and more than a few puck thicknesses. Pad out thin parts (blade, glove edge) with a thicker invisible collider. Stay clear of paper-thin shapes [4].
5. Timestep: 0.02 s is too coarse for 45 m/s. Consider 0.005 to 0.01 s (100 to 200 Hz) if CPU allows. Unity recommends a smaller step when CCD alone is not accurate [1]. Cost is a trade-off, not measured here.
6. Fallback, the reliable one: in the puck's `FixedUpdate`, before the step, run a `Physics.SphereCast` or `CapsuleCast` (or `Rigidbody.SweepTest`) along `velocity * fixedDeltaTime` against the gear layer. Cast against the gear colliders' poses for this step. If it hits, place the puck at the hit point and reflect or stop it in code. This does not depend on CCD behaviour with kinematic bodies. Rotation of the gear still only matters at its pose now, so keep the gear colliders thick to cover the swing between steps.
7. Layers: put gear on its own layer, collide only with puck and body layers, so casts and CCD stay cheap.

Limit of the fallback: the cast sees the gear at its pose for this step only. Gear that sweeps into the puck's path during the step is caught only if its collider is thick enough to overlap that path at one of the two sampled poses. Thick colliders and a smaller step cover this.

## Recommendation

Yes, it can pass through. Make hits reliable with: kinematic Rigidbody per fast gear piece moved by `MovePosition`/`MoveRotation` in `FixedUpdate` (not posed by transform in `Update`); thick box/capsule gear colliders (8 to 10 cm, padded); puck on a Box/Capsule collider with ContinuousDynamic (A/B against ContinuousSpeculative); a 0.01 s or smaller fixed step; and a per-step puck sweep against the gear layer as the guarantee. Prove it with the 1000-shot test rig below before the spec fixes the numbers.

## Test plan

1. Test rig: fire the puck at 10, 25, 45 m/s at a stationary gear collider, then at one moving at 3 and 8 m/s, then rotating (stick swing).
2. Run 1000 shots per case, count passes through (puck beyond gear plane, no contact event).
3. Matrix: puck mode (Discrete, ContinuousDynamic, ContinuousSpeculative) x gear driven by transform in Update vs `MovePosition` in FixedUpdate x step 0.02 vs 0.01 vs 0.005 x with/without the cast fallback.
4. Record ghost contacts too: puck deflects with no visible gear nearby. Log contact normals.
5. Pass criterion: 0 misses at 45 m/s with the fallback on. Report which cases need the fallback.
6. Repeat with the real skinned goalie in a normal Round. Capture the puck path in the editor.

## Not verified

- Unity's own statement of sweep CCD against kinematic targets.
- Animator update mode and `autoSyncTransforms` behaviour (pages gave 404).
- Compound vs many rigidbodies guidance.
- Puck collider type in the project.
- Any Unity staff post on this exact case. The one forum thread found has community answers only [6].

## Sources

1. Unity Manual, Continuous collision detection: https://docs.unity3d.com/Manual/ContinuousCollisionDetection.html
2. Unity Manual, Sweep-based CCD: https://docs.unity3d.com/Manual/sweep-based-ccd.html
3. Unity API, Rigidbody.collisionDetectionMode and CollisionDetectionMode: https://docs.unity3d.com/ScriptReference/Rigidbody-collisionDetectionMode.html , https://docs.unity3d.com/ScriptReference/CollisionDetectionMode.html
4. NVIDIA PhysX 5.3.1, Advanced Collision Detection: https://nvidia-omniverse.github.io/PhysX/physx/5.3.1/docs/AdvancedCollisionDetection.html
5. Unity Manual, Speculative CCD: https://docs.unity3d.com/Manual/speculative-ccd.html
6. Unity discussions (community only), ghost collisions with kinematic CCD: https://discussions.unity.com/t/continuous-collision-detection-produces-ghost-collisions/1543337
7. NVIDIA PhysX 5.3.1, Rigid Body Dynamics (kinematic actors): https://nvidia-omniverse.github.io/PhysX/physx/5.3.1/docs/RigidBodyDynamics.html
8. Unity API, Rigidbody.MovePosition: https://docs.unity3d.com/ScriptReference/Rigidbody.MovePosition.html
9. Unity API, Physics.SyncTransforms: https://docs.unity3d.com/ScriptReference/Physics.SyncTransforms.html
