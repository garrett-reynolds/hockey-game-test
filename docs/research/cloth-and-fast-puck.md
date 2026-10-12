# Can Unity cloth and a fast puck push on each other?

Research for [#99](https://github.com/garrett-reynolds/hockey-game-test/issues/99), part of map [#98](https://github.com/garrett-reynolds/hockey-game-test/issues/98). The answers are for this project's Unity version, **6000.6.5f1** (`ProjectSettings/ProjectVersion.txt`), and come from the 6000.6 manual and scripting API.

## Short answer

- **No, built-in Cloth cannot push back.** Cloth is one-way: the puck can push the netting, but the netting can never slow, stop or drop the puck.
- **A 45 m/s puck needs a real physics catcher.** Cloth does not stop the puck, so it cannot "let it through by mistake". The puck has to be stopped by ordinary colliders, which must be thick and paired with continuous collision on the puck.
- **Recommendation:** an invisible physics catcher stops and drops the puck. A built-in Cloth netting reacts to the puck one-way, for looks only. Obi Cloth is the paid fallback if that looks wrong.

## 1. Built-in Cloth is one-way

The Unity 6000.6 manual, [Cloth](https://docs.unity3d.com/6000.6/Documentation/Manual/class-Cloth.html), says:

> "Cloth does not react to all colliders in a scene, nor does it apply forces back to the world. … Even after that, the simulation is still one-way: cloth reacts to those bodies but doesn't apply forces back."

> "you can only use three types of colliders with cloth: a sphere, a capsule, and conical capsule colliders, constructed using two sphere colliders. These restrictions all exist to help boost performance."

> "Cloth is unable to simply collide with arbitrary world geometry, and now will only interact with the colliders specified in either the Capsule Colliders or Sphere Colliders arrays."

What this means for us:

- The [Cloth scripting API](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Cloth.html) has no contact callback and no force output. All it exposes is `vertices`, `normals` and settings such as `capsuleColliders`, `sphereColliders`, `collisionMassScale`, `enableContinuousCollision` and `useVirtualParticles`.
- Faking two-way by reading `Cloth.vertices` every frame and pushing the puck ourselves is possible. But that means writing our own contact code, which is the kind of thing ADR 0004 moved away from.
- A disc puck has no matching cloth collider. It needs **proxy shapes** for the cloth, such as a few `SphereCollider`s or a short capsule placed inside the puck and listed in the Cloth's `sphereColliders`/`capsuleColliders`.
- Cloth was [designed "with character clothing in mind"](https://docs.unity3d.com/2018.4/Documentation/Manual/UpgradeGuide5-Physics.html) (Unity 5 upgrade guide).
- **Deprecation risk:** a Unity Product Manager (ashley_unity, DOTS + Physics) [wrote on 2024-09-26](https://discussions.unity.com/t/feedback-request-deprecation-of-unity-cloth/1526223) that "we are considering the deprecation of Unity Cloth in a future version of Unity". No replacement was named. The 6000.6 manual page shows no deprecation notice. This does not block us, but keep the cloth part small and easy to swap.

## 2. Can a 45 m/s puck pass through by mistake?

**This project's numbers.** `TimeManager.asset` sets Fixed Timestep to 0.02 s (50 Hz). `DynamicsManager.asset` sets Default Solver Iterations to 6 and Velocity Iterations to 1.

- At 45 m/s the puck moves **0.9 m per physics step**. That is about the whole depth of an NHL net (roughly 1.0–1.1 m at the base, and shallower at the top).
- With Discrete collision, a puck can be in front of the net mesh in one step and behind the back of the net in the next. It would never touch it.

**Physics (what stops the puck).** Cloth never stops the puck, so the only question is whether the catcher colliders do.

- [`CollisionDetectionMode.ContinuousDynamic`](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/CollisionDetectionMode.ContinuousDynamic.html) on the puck "Prevent[s] this Rigidbody from passing through static mesh geometry, and through other Rigidbodies which have continuous collision detection enabled, when it is moving fast."
- [Sweep-based CCD](https://docs.unity3d.com/6000.6/Documentation/Manual/sweep-based-ccd.html) only sweeps linear motion: "they can't detect collisions that occur as a result of the physics body rotating". A spinning puck that tilts or flips could still clip an edge. The manual points to speculative CCD for rotating bodies.
- [CCD manual](https://docs.unity3d.com/6000.6/Documentation/Manual/ContinuousCollisionDetection.html): "CCD is supported for Box, Sphere and Capsule colliders." It also says CCD "does not always deliver physically accurate collision results, so you might still consider decreasing the physics timestep". Unity has no cylinder primitive. **Whether a convex `MeshCollider` puck is covered by CCD is unconfirmed here.** A prototype should test it (or use a compound of primitives).
- Practical rule: give the catcher **thickness** (boxes, not planes or single-sided meshes) and keep its colliders static or kinematic. Then a missed sweep still lands inside a solid shape.

**Visual (cloth poking through the puck).** Cloth particles can slip past fast proxy spheres.

- [`Cloth.enableContinuousCollision`](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Cloth-enableContinuousCollision.html) is "needed to detect collisions between fast-moving objects". It traces particle and capsule paths, and it is "around 2x more computationally expensive than discrete collision".
- `useVirtualParticles` adds "one virtual particle per triangle to improve collision stability" ([manual](https://docs.unity3d.com/6000.6/Documentation/Manual/class-Cloth.html)).
- If the puck pokes through the cloth for a frame, it shows. It does not change the Shot result.

## 3. Options for a netting that slows and drops the puck

None of the frame-time costs below were measured. For each option this lists the things that drive the cost; a short prototype should measure them.

### A. Invisible physics catcher + one-way built-in Cloth (recommended)

- **How it works:**
  - **Catcher:** hidden, thick colliders shaped like the inside of the net: a back box, side boxes and a roof box, with a `PhysicsMaterial` that has low bounce and high friction.
  - **Slowing the puck:** an optional trigger volume in the net that raises the puck's `linearDamping` while it is inside. The puck then gives, slows and drops to the ice.
  - **Netting:** a built-in Cloth mesh whose `sphereColliders`/`capsuleColliders` are the puck's proxy shapes. It bulges when the puck hits it, but only for looks.
  - The cloth must hang slightly in front of the catcher, so the visible bulge and the hidden stop agree.
- **Work:** low to medium. Everything is built-in. The tuning is about lining up the cloth bulge with the catcher.
- **Cost drivers:** the cloth's particle count (keep the net mesh coarse), `clothSolverFrequency`, and continuous collision on the cloth (about 2x). The catcher costs almost nothing.
- **Risks:**
  - The cloth stops at its anchor points instead of wrapping around the puck. Once the puck stops, the cloth falls back on its own rather than being held by the puck.
  - The Cloth deprecation risk above.

### B. Spring-and-joint net of rigidbodies

- **How it works:** a grid of small Rigidbody nodes joined by `SpringJoint`/`ConfigurableJoint`. A rope or line renderer draws the mesh. This is truly two-way through PhysX.
- **Work:** medium to high. You build the grid, tune the springs and avoid explosions.
- **Cost drivers:** the number of bodies and joints (a 20×10 net is about 200 bodies and about 400 joints) and the solver iterations.
- **Risks:**
  - The [Joint and Ragdoll stability](https://docs.unity3d.com/6000.6/Documentation/Manual/RagdollStability.html) page says: "Avoid large differences in the masses between Rigidbody components connected by Joints". It suggests 10–20 solver iterations for jitter, which is above this project's 6/1.
  - A 0.17 kg puck at 45 m/s hitting light nodes is a large impulse.
  - Small node colliders are easy for the puck to slip between, so the puck would also need CCD against the nodes. Those nodes would need CCD too, per the ContinuousDynamic quote above.

### C. Obi Cloth (paid asset, Virtual Method)

- **How it works:** a particle-based cloth with "full two-way coupled interaction between Obi particles and rigidbodies". It works with "pretty much all standard Unity colliders". It uses speculative contacts, so tunneling is "almost non-existant" and contacts are resolved "even at high speeds" ([Obi 7 collisions manual](https://obi.virtualmethodstudio.com/manual/7.0/collisions.html)).
- **Work:** low to medium for setup and the learning curve. It adds a third-party dependency.
- **Cost drivers:** the particle count and the backend.
  - The default is Burst on the CPU, which needs the Burst, Collections, Mathematics and Jobs packages.
  - There is also a Compute (GPU) backend. Obi notes that the "minimum cost of a simulation is usually higher on the GPU, so small simulations often run better on the CPU" ([Obi backends](https://obi.virtualmethodstudio.com/manual/7.0/backends.html)). One net is a small simulation, so use Burst.
- **Unconfirmed:**
  - The price was about **$47, as listed on the [Asset Store](https://assetstore.unity.com/packages/tools/physics/obi-cloth-81333). This comes from a search snippet and was not verified.**
  - Support for Unity 6000.6 is **not confirmed**. Check the store page before buying.

### Not recommended

- **Faking two-way with built-in Cloth** (reading `Cloth.vertices` and applying forces to the puck). This is our own contact code again, which goes against ADR 0004.

## Recommendation

Use **option A**:

1. Hidden thick catcher colliders, plus an optional damping trigger, to stop and drop the puck.
2. `ContinuousDynamic` on the puck.
3. A built-in Cloth netting that reacts one-way to sphere or capsule proxies on the puck, with `enableContinuousCollision` on.

**Build a prototype first** to check that:

- the bulge looks right at 45 m/s;
- the puck never leaves the catcher with the puck collider we pick (convex mesh vs a compound of primitives);
- the frame time is acceptable.

If the netting looks wrong (it does not wrap or hold the puck), move to **option C, Obi Cloth**, after confirming Unity 6000.6 support. Avoid option B unless both A and C fail.
