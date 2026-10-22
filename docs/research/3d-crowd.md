# Free 3D crowd options for Unity (#73, map #71)

## Question

How can the game show a real 3D crowd of thousands on a strong PC? This covers four parts:

- Free models with a usable licence (CC0 / CC-BY, no unknown .unitypackage), rigged or posable, in FBX/OBJ/glTF.
- How big crowds are rendered in Unity URP: GPU instancing, vertex-animation textures (VAT), LOD and impostors. What each costs.
- How the crowd can cheer, stand and wave on a Goal.
- The firm rule: crowd motion must not make the goalie or the Gaps harder to read.

## Short answer (recommendation)

- **Models:** Use Quaternius **Universal Base Characters**. They are CC0, come in FBX/OBJ/glTF, and have a Humanoid rig [1]. Add Quaternius **Universal Animation Library 2**, which is also CC0; its free tier includes a `WAVE` clip [3]. Decimate a few body variants to about 1–2k triangles each.
- **Rendering:** Do not use one `SkinnedMeshRenderer` per fan. Unity does not GPU-instance skinned meshes [5]. Instead:
  1. Bake each clip to a **vertex animation texture** offline.
  2. Draw the fans as static meshes with `Graphics.RenderMeshInstanced` or `Graphics.RenderMeshIndirect` [6].
  3. Read the VAT in a custom URP shader.

  This takes a handful of draw calls for the whole bowl.
- **Cheer / stand / wave:**
  - Bake one VAT per state: sit-idle, clap, stand-cheer, wave.
  - The shader picks or blends the state per instance.
  - A random time offset per instance keeps the fans out of step.
  - A wave ripple is a delay set by seat position.
- **Firm rule:** The stand behind the net stays near-still during the skate-in. Big motion fires only after a Goal.

## Candidate model and animation sources

| Source | Licence | Format | Rig / animation | Fit |
|---|---|---|---|---|
| Quaternius Universal Base Characters | CC0, "free to use in personal, educational and commercial projects" [1] | FBX, OBJ, glTF (Standard); .blend in Source [1] | Humanoid rig that retargets in any engine; no clips; 6 bodies (Superhero/Regular/Teen × M/F) plus 20 hairstyles; about 13k triangles each [1] | **Best body base.** Must be decimated for a crowd. |
| Quaternius Ultimate Modular Characters | CC0 [2] | FBX, OBJ, Blend; glTF named in the text [2] | 11 characters, 24 animations (clip names not listed; rig not stated on the page) [2] | Adds variety in outfits. Check the clips after download. |
| Quaternius Universal Animation Library 2 | CC0 [3] | Standard export for Unity/Godot/Unreal; GLB for Unreal; .blend in the paid Source [3] | The free Standard tier has 42 named clips, including **`WAVE`** and `IDLE_POSE_ARMS`; no clap, cheer or sit in the free list [3]. UAL 1 (CC0) lists "sitting" and "emotes" but no clip names [4] | Wave and idle are free. Clap, cheer and sit are not confirmed. |
| Kenney Mini Characters | CC0 [12] | Not listed on the page [12] | "Animation" is a listed feature [12] | Stylised. **Unverified** formats; a fallback only. |
| Mixamo (Adobe) | Royalty-free in games with a free Adobe ID. Raw files must not be redistributed. **Unverified:** Adobe's FAQ returned 403; this comes from search snippets and Adobe community posts [13] | FBX | Large library that includes clapping and cheering | Not CC. The repo is private today. Raw Mixamo FBX files committed to git become a problem if the repo ever goes public. |

**Clips gap.** No CC0 source I checked names a **sit-idle, clap, stand-cheer or sit-to-stand** clip. Options, in order:

1. Author 3–4 short clips in Blender on the Quaternius Humanoid rig: sit-idle, clap, stand-cheer, wave. These are simple, looping, upper-body moves. This fits "art made by scripts".
2. Check the 24 clips in Ultimate Modular Characters after download.
3. Use Mixamo as a non-CC fallback, with the redistribution caveat above.

## How to render thousands in URP

### Facts from Unity docs (Unity 6)

- GPU instancing draws many copies of a mesh and material in one call. "Skinned Mesh Renderer components are not supported." [5]
- In URP, GPU instancing works with custom shaders only if the SRP Batcher is disabled for them [5].
- `Graphics.RenderMeshInstanced` and `Graphics.RenderMeshIndirect` draw many instances from script.
  - The shader gets the instance with `GetIndirectInstanceID(svInstanceID)`.
  - Culling uses the single `RenderParams.worldBounds` you supply, not per instance [6].
  - Indirect drawing needs compute-shader support [6].
- The **GPU Resident Drawer** instances `MeshRenderer` GameObjects automatically through `BatchRendererGroup`.
  - It needs Forward+ and BatchRendererGroup variants set to "Keep All" [7].
  - This project's PC renderer already uses Forward+ (`PC_Renderer.asset` `m_RenderingMode: 2`), with the drawer on (`PC_RPAsset.asset` `m_GPUResidentDrawerMode: 1`).
  - It is still static meshes only, so animated fans need VAT in their shader either way.
- Entities Graphics mesh deformation (skinning) is an **experimental** feature whose API may change [8]. It is not recommended here.
- `SkinnedMeshRenderer` cost grows with bones per vertex. Skinning stops off-screen unless Update When Offscreen is on [9]. That is fine for a few hundred fans, not for thousands.

### Vertex animation textures (VAT)

- **The idea.** Bake each frame's vertex positions (and normals) into a texture. A vertex shader samples it by time, so the CPU does no bone work.
- **Unity's own write-up.** Unity's "Animation Instancing" post describes this approach because skinned meshes cannot be instanced [10]. (The page now redirects and 404s; this is from the search snippet only.)
- **Benchmark (Unreal, not Unity).** A published stadium crowd, Unreal on an RTX 3070 at 1080p, with 6,072 spectators [11]:

  | Method | GPU time | Draw calls | Game thread |
  |---|---|---|---|
  | VAT + instancing | 17.5 ms | 276 | 0.66 ms |
  | Skeletal meshes | 32 ms | 21,369 | 34 ms |

  - Each state (Idle, Clap, Yell, Stand, Wave) is its own VAT bake, blended in the shader.
  - A per-instance random start offset avoids unison.
  - A wave is timed from each agent's ID and a sweep duration.
  - A per-instance hue and shirt colour give variety.
  - Take this as evidence for the technique, not a Unity measurement.
- **Community Unity repos.**
  - Mefodei/Render-Crowd-Of-Animated-Characters bakes to texture plus instancing. Its author claims 10,000 soldiers in about 20 draw calls.
  - mkrebser/GPUInstance does compute-driven skinned instancing with GPU LOD and culling.

  Both are unvetted: check the licence and the Unity 6 status before use.
- **Bakers.**
  - SideFX Labs has a VAT exporter and a Unity tutorial. Its URP support is not confirmed from current docs [14].
  - A baker is also easy to write as an Editor script: sample `AnimationClip.SampleAnimation`, then `SkinnedMeshRenderer.BakeMesh` per frame, and write the vertices to an RGBAHalf texture.

### Rough cost here (estimate)

- **Seats.** About **13,000 seat positions** in the full ring around today's half rink. This is my estimate from the `Tools/make_arena.py` constants: 20 + 13 rows, `ROW_D` 0.85 m, the ring's mean perimeter of about 200 m, and about 0.5 m per seat.
- **Triangles.** At 13k triangles per fan (raw Universal Base Characters) that is about 170M triangles, which is too many. Decimated to about 1–2k triangles, it is **about 15–25M triangles**.
  - That is a few instanced draws (one per body variant × LOD) with near-zero CPU.
  - A strong gaming PC handles it.
  - Each VAT texture is small: vertices × frames, for example 1.5k × 60 RGBAHalf ≈ 0.7 MB per clip.
- **LOD and impostors.**
  - Use about 1.5k triangles for the lower tier.
  - Use about 300–500 triangles, or camera-facing cards or impostors, for the upper tier and far side.
  - Impostors save more but cost texture memory and look flat up close [15]. The end stand behind the net is about 10–35 m from the player, so it should be real meshes.

## Cheer, stand and wave on a Goal

- **Per-instance data** (one structured buffer): seat position, body variant, shirt/hue tint, random phase, current state, and state start time.
- **States:**
  - `SitIdle` is the default: low amplitude, random phase.
  - `Clap` and `Cheer` (stand + arms up) start on a Goal.
  - `Wave` fires on a Goal for some fans. A stadium-style ripple uses a per-seat delay = angle around the ring ÷ sweep speed [11].
- **Switching.** C# writes a "Goal at time t" value. The shader computes each fan's state from `time − t − seatDelay`. No per-fan CPU work is needed. A short crossfade between two VAT samples hides the snap.
- **Team colours.** Tint the shirt through a mask channel. That keeps the hook for a team skin later.

## The firm rule: keep the goalie readable

- During the skate-in (22 m → 9 m), the **end stand behind the net** stays in `SitIdle` only:
  - small amplitude
  - desynchronised
  - no waving, standing or flashing colours
- Big motion (stand, cheer, wave ripple) starts only **after** a Goal, once the read is over.
- The mixed shirt colours behind the net should be lower in contrast than the goalie. One option is a slight darkening or desaturation of that sector.
- **Test it:** view from 9 m and 22 m at eye height 1.5 m, with the crowd off, still and animated.

## Sources

1. Quaternius, Universal Base Characters: https://quaternius.com/packs/universalbasecharacters.html
2. Quaternius, Ultimate Modular Characters: https://quaternius.com/packs/ultimatemodularcharacters.html
3. Quaternius, Universal Animation Library 2 (itch.io): https://quaternius.itch.io/universal-animation-library-2
4. Quaternius, Universal Animation Library: https://quaternius.com/packs/universalanimationlibrary.html and https://quaternius.itch.io/universal-animation-library
5. Unity Manual 6000.0, GPU instancing: https://docs.unity3d.com/6000.0/Documentation/Manual/GPUInstancing.html
6. Unity Scripting API, Graphics.RenderMeshIndirect: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Graphics.RenderMeshIndirect.html
7. Unity Manual (URP), GPU Resident Drawer: https://docs.unity3d.com/6000.0/Documentation/Manual/urp/gpu-resident-drawer.html
8. Entities Graphics, Mesh deformations (Experimental): https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.3/manual/mesh_deformations.html
9. Unity Manual, Skinned Mesh Renderer: https://docs.unity3d.com/6000.0/Documentation/Manual/class-SkinnedMeshRenderer.html
10. Unity blog, Animation Instancing for SkinnedMeshRenderer: https://blog.unity.com/engine-platform/animation-instancing-for-skinnedmeshrenderer (now redirects to a 404)
11. 80.lv, 6,000 agents on one game thread (VAT + HISM stadium crowd, Unreal): https://80.lv/articles/6-000-agents-on-one-game-thread-novel-high-performance-framework-for-gpu-driven-crowd-systems
12. Kenney, Mini Characters: https://kenney.nl/assets/mini-characters
13. Adobe, Mixamo FAQ (403 to the fetcher): https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html, and the Adobe community thread https://community.adobe.com/t5/mixamo-discussions/mixamo-faq-licensing-royalties-ownership-eula-and-tos/m-p/13234775
14. SideFX, Vertex Animation Textures in Unity: https://www.sidefx.com/tutorials/vertex-animation-textures-in-unity/
15. Kavan et al., Polypostors (impostor memory and close-up limits): https://users.cs.utah.edu/~ladislav/kavan08polypostor/kavan08polypostor.html
16. Community repos (unvetted): https://github.com/Mefodei/Render-Crowd-Of-Animated-Characters, https://github.com/mkrebser/gpuinstance
