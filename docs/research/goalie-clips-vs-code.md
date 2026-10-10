# Goalie set-up and save motion: code-driven poses or animation clips plus IK?

## Question

Should the goalie's set-up and save motion stay code-driven (bones posed from code, two-bone IK), or use real save animation clips blended with IK so the gear still meets the puck exactly? Context: Unity 6000.6.5f1, a Humanoid rig (`goalie-dressing.fbx`), no controller, no Animation Rigging package installed, no Adobe login.

## Short answer (recommendation)

- No source I found offers a free, login-free, hockey-goalie clip set. The closest free sets (Quaternius, CMU, Bandai Namco) have no goalie clips that I could confirm. The only goalie-like clips found are paid ones on the Asset Store.
- Stay code-driven for the save itself, because the save must hit the puck line exactly and your pose and IK pipeline already does that.
- If you want more natural motion, take the hybrid route: author or fetch a few clips (idle, crouch, butterfly drop) and sample them as pose sources inside the existing pipeline. Keep your two-bone IK in `LateUpdate` as the final correction.
- Skip Animator IK for the pads and gear. It only has hand and foot goals [1][2].
- Animation Rigging is a viable but heavier second choice. It is now a core package in Unity 6.6 [3].

## Sources of clips

| Source | Licence | Login | Format | Goalie-relevant clips |
|---|---|---|---|---|
| Quaternius Universal Animation Library | CC0, personal and commercial use allowed [4] | No login stated on the page [4] | FBX, glTF (GLB), OBJ in the free tiers [4] | None listed. It has 120+ clips: locomotion in 8 directions, jog, sprint, crawl, swim, push, sit, death, combat, gun and emotes. The closest is crawling [4]. It would fit your Quaternius body. Whether the rig is Humanoid-compatible is unverified. |
| Mixamo (Adobe) | Free to use with an Adobe ID. Royalty-free for personal, commercial and non-profit projects, including games [5]. A secondary 2022 Adobe community post bans redistributing raw files as an asset pack and use for ML training (unverified in current terms) [6]. | Adobe ID required [5] | FBX (not confirmed from the FAQ page, which returned 403 to my fetch) | Out per the owner. I could not confirm any goalie clip from a primary source. |
| CMU Mocap Database | "Free for use in research projects"; may go into commercial products but may not be resold directly, even converted. This is a secondary mirror, because the CMU site failed a certificate check (unverified) [7]. | No | Original is ASF/AMC. BVH conversions are third-party [7]. | Unconfirmed. It has sports and general motions; I did not verify a goalie or crouch/drop set. |
| Bandai Namco Research Motion dataset | CC BY-NC 4.0 per the repo page (one secondary source says NC-ND; unverified) [8][9]. Non-commercial is fine for you. | No | BVH [8] | No. Dataset 1 covers daily activities, fighting and dancing. Dataset 2 covers locomotion and hand actions [8]. |
| Kenney | CC0 generally. The animation pack page returned 404, so unverified. | No | n/a | None confirmed. |
| Rokoko | The free TV and magic packs: use in any project, including commercial, per Rokoko's pages [10]. The free Motion Library moves: secondhand report only (unverified) [10]. | Rokoko Studio account is probably needed for the library (unverified) | FBX and BVH [10] | None found. |
| ActorCore (Reallusion) | Free motions require registration. A 2022 staff reply says games need a free Mass Distribution licence, and motions cannot be resold, even edited (secondary, unverified) [11]. | Yes | Not verified | Not verified. |
| Sketchfab CC-BY rigged models | Not verified. The licence help page returned 404. | Download usually needs an account (unverified) | n/a | None searched. |
| Unity Asset Store (paid) | Standard Unity Asset Store EULA. The asset must be embedded in your product, not a substantial portion of it, and cannot be redistributed on its own [12]. | Unity account | Mecanim or legacy, varies | "Hockey Goalie Animated" ($19, no description, so clip list and rig unverified) [13]. "Soccer Character - Goalie" ($30) has dive left and right, idle, kick, pass, run, tackle and throw [14]. Studio33 Soccer 8 ($900) lists keeper save clips [14]. I found no free goalie pack. |

Honest summary: none of the free, login-free sources has a goalie-specific clip that I could confirm. A soccer keeper dive is not a hockey butterfly or pad save.

## Retargeting to the Humanoid rig

- Unity's Avatar is "an interface for retargeting animation from one rig to another". Bone mapping is done in the Avatar Mapping tab. Required bones are drawn as solid circles and optional ones as dotted circles, and Unity can interpolate optional bones [15]. Once each Avatar is set up, the same humanoid clips can be reused across models [16].
- Humanoid animation works in muscle space, not bone space. Retargeting is a Humanoid feature, and Generic rigs do not retarget [17].
- Unity imports FBX, DAE, DXF and OBJ natively. It also imports Maya, Blender, Modo and Cheetah3D files by converting them to FBX. BVH is not listed, so a BVH dataset (CMU, Bandai, Rokoko exports) needs conversion to FBX in Blender or similar before import [18]. FBX is the recommended format [18].
- Root motion: Body Transform is the centre of mass. The Root Transform is its Y-plane projection and moves the GameObject. Use Bake Into Pose (Position XZ, Rotation) for stationary clips like idles. "Based Upon: Feet" reduces floating [19].
- I could not verify these from primary pages (the muscle and settings page returned 404). Treat them as unverified:
  - Foot IK is a per-clip import option.
  - Twist bones and fingers are optional Humanoid slots.
  - Gear bones (pads, blocker, glove) outside the Humanoid mapping are not retargeted. They follow their parent bone only unless a clip also animates them.
- Practical risk: a soccer or human clip applied to a bulky pad-and-gear body will make the pads and the stick clip through each other. The muscle limits can be tightened per Avatar, but I did not verify the details.

## Blending clips with IK at runtime

- **Animator IK.**
  - It is supported "for a humanoid character with a correctly configured Avatar" [1].
  - Turn on IK Pass on the layer, then set targets in `OnAnimatorIK` [1].
  - `SetIKPosition` sets a world-space goal, and the weight blends from the animated pose to the goal [2].
  - The only goals are `LeftFoot`, `RightFoot`, `LeftHand` and `RightHand` [20].
  - `SetIKHintPosition` exists for bend hints [21]. The hints are `LeftKnee`, `RightKnee`, `LeftElbow` and `RightElbow` [28], so a knee hint can steer which way a leg (and the pad on it) bends, but the goal is still the foot.
  - So the blocker and glove can reach the puck. The pad "reach" can only be done by a foot goal, not by a pad or knee goal, and it cannot move the torso or a gear bone.
  - It needs a Controller and an Animator pass. Your current setup has `runtimeAnimatorController = null`.
- **Animation Rigging.**
  - It is a set of constraints for procedural motion, for example IK and Aim constraints that correct animation [3].
  - The Two Bone IK constraint takes Root, Mid and Tip, a Target, an optional Hint, plus Target Position, Rotation and Hint weights. It also has Maintain Target Offset and an Auto Setup menu item [22].
  - The Rig Builder must sit on the same GameObject as the Animator. It builds a Playable Graph that runs after the Animator's normal evaluation [23].
  - Version 1.4.1 is listed for Unity 6000.5, and in 6.6.0 the package became "a core package embedded in Unity" [3][24]. Since you run 6000.6.5f1, it should not need a separate install (unverified for your project: check the Package Manager).
  - The pages I fetched do not say whether it supports Humanoid or Generic. The manual for 1.4 lists its requirement as Unity 2023.2 or later [24]. The common claim that it works on Generic rigs is unverified here.
  - Since it works on Transforms, it can drive the pad and gear bones, not just hands and feet.
- **Simplest option: play, then override in `LateUpdate`.**
  - `LateUpdate` runs "after all Update functions have been called" [25]. The page does not state where the Animator evaluates; the flowchart is an image (unverified from text).
  - The `OnAnimatorIK` callback is part of the regular animation update [26].
  - Order caveat: if a controller plays clips, writing `localRotation` in `LateUpdate` overrides them each frame, which is what you want for the final IK correction.
- **Posing from clips without a controller.**
  - `AnimationClip.SampleAnimation(go, time)` applies the sampled clip to the GameObject and overwrites any animated properties. Unity itself recommends the Animation interface for performance. It is useful for jumping to arbitrary frames [27].
  - It is cheap to use for a few snapshots, such as a "ready" pose or butterfly-drop frames. Then you can read the resulting bone rotations into your existing snapshot and Slerp system.
  - The Playables API (`AnimationClipPlayable`) can also play clips without a controller. My fetch of its page returned nothing, so treat details as unverified.
  - Whether `SampleAnimation` handles Humanoid (muscle) clips correctly without an Animator is not stated on its page (unverified). Test it.

## Routes compared

| Route | Cost | Risk | Notes |
|---|---|---|---|
| A. Code-driven only (today) | None | Stiff, robotic look. Every new pose costs hand-tuning. | The save meets the puck exactly. No new dependency. |
| B. Clips plus Animator IK | Medium: build a Controller, an IK Pass, state logic, sourced clips | Hand and foot goals only, so no pad or gear reach. Retarget quality unknown. It replaces your `controller = null` pipeline. | Weakest fit for hockey gear. |
| C. Clips plus Animation Rigging | Medium to high: Rig Builder, constraints on the goalie hierarchy, learn the package | Package in transition (core in 6.6). Needs an Animator. Possible overlap with your two-bone IK. | Can drive gear bones. |
| D. Hybrid: sample clips as pose sources, correct with existing IK in `LateUpdate` | Low to medium: import and retarget a few clips, add a sampling step | Clip quality limited by the lack of goalie clips. `SampleAnimation` Humanoid behaviour needs a test. | Keeps the exact puck contact. Reuses your Slerp and IK code. |

## Recommendation

1. Keep the save reach and puck contact code-driven (route A or D). It is the only route that guarantees the gear meets the puck line.
2. If the stance looks too stiff, try route D on one pose first (for example a crouch or butterfly drop). Source it from Quaternius (CC0, no login) and a Blender hand edit, or from a paid goalie pack, rather than from Mixamo.
3. Do not adopt Animator IK for the gear. It cannot target pads, and a goalie is mostly pads.
4. Consider Animation Rigging only if you later want constraints on the gear bones that code cannot easily express.
5. Because no free goalie clips exist, the real cost of any clip route is authoring or editing a butterfly and a save, not wiring them in.

## Open questions

- Does Quaternius's rig import as Humanoid, and does it match your Avatar (unverified)?
- Does `SampleAnimation` on a Humanoid clip work with `runtimeAnimatorController = null`? (A 10-line test would answer it.)
- What clips does "Hockey Goalie Animated" ($19, Asset Store) contain? Its rig type is unknown.
- Is the Bandai licence NC or NC-ND (conflicting sources)? ND would block edited motion.
- Is Animation Rigging already present in the 6000.6.5f1 project as a core package?
- Where does Animator evaluation fall relative to `LateUpdate` (flowchart only)?

## Sources

1. https://docs.unity3d.com/6000.0/Documentation/Manual/InverseKinematics.html
2. https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animator.SetIKPosition.html
3. https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/index.html
4. https://quaternius.com/packs/universalanimationlibrary.html
5. https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html (page returned 403; claim comes from a WebSearch summary of it)
6. https://community.adobe.com/questions-696/mixamo-faq-licensing-royalties-ownership-eula-and-tos-589400
7. https://data.4tu.nl/datasets/0448aab2-3332-449f-a8e2-d208cb58c7df (secondary mirror of the CMU notice)
8. https://github.com/BandaiNamcoResearchInc/Bandai-Namco-Research-Motiondataset
9. https://krita-artists.org/t/bandai-namco-motion-datasets-repository/43132 (secondary, NC-ND claim)
10. https://www.cgchannel.com/2020/03/get-150-free-mocap-moves-from-rokokos-motion-library (secondary) and https://rokoko.com/resources/rokoko-mocap-free-tv-themed-animations
11. https://forum.reallusion.com/PrintTopic465481.aspx (secondary, 2022 forum reply)
12. https://unity.com/legal/as-terms
13. https://assetstore.unity.com/packages/3d/characters/hockey-goalie-animated-199878
14. https://assetstore.unity.com/packages/3d/characters/humanoids/humans/soccer-character-goalie-18347 and https://assetstore.unity.com/packages/3d/animations/studio33-interactive-soccer-8-210013 (from search summaries)
15. https://docs.unity3d.com/6000.0/Documentation/Manual/class-Avatar.html
16. https://docs.unity3d.com/6000.0/Documentation/Manual/AvatarCreationandSetup.html
17. https://docs.unity3d.com/6000.0/Documentation/Manual/AnimationOverview.html
18. https://docs.unity3d.com/6000.0/Documentation/Manual/3D-formats.html
19. https://docs.unity3d.com/6000.0/Documentation/Manual/RootMotion.html
20. https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AvatarIKGoal.html
21. https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animator.SetIKHintPosition.html
22. https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/constraints/TwoBoneIKConstraint.html
23. https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/RiggingWorkflow.html
24. https://docs.unity3d.com/Packages/com.unity.animation.rigging@6.6/changelog/CHANGELOG.html and https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.4/manual/index.html
25. https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour.LateUpdate.html
26. http://docs.unity3d.com/6000.0/Documentation/Manual/execution-order.html
27. https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AnimationClip.SampleAnimation.html
28. https://docs.unity3d.com/ScriptReference/AvatarIKHint.html
