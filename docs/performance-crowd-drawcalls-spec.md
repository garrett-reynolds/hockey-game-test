# Compare crowd draw-call grouping without a quality cut

## Problem

The approved trace retry completed in 172.83 seconds. Crowd-on versus crowd-off median combined wall times were 11.2109 versus 6.6829 ms in skate-in and 13.0608 versus 7.8634 ms in replay. Rendering dominates much of the slow-frame time. The old flat-card comparison gave little player-view benefit. Preserve the 3D crowd.

## Narrow experiment

The current crowd splits each outfit/detail/tier group into 16 angle sectors. Every visible slice makes a separate RenderMeshPrimitives call. Compare 4, 8, 16, and 32 sectors in the same Unity process and views. Fewer sectors may lower draw-call overhead but draw more off-screen fans. More sectors may do the reverse. Measure this tradeoff; do not guess the best default.

Keep the default at 16. Keep every fan's exact seat, yaw, appearance, body, animation phase, and actions. Regroup the existing per-fan data and update its GPU buffer; do not reroll random values or reload body textures for each variant. Invalidate world bounds after regrouping. Reject invalid sector counts before mutation. Do not change shader, gameplay, or the live Unity project.

## Seam and report

Use Crowd's existing diagnostic seams and one small Editor benchmark entry. Record each variant's sector count, visible slice/draw-call count, submitted fan count, all per-round timings, and paired no-crowd cost in the existing shooter and up-ice views. Rotate configuration order each round to reduce order/heat bias. Use bodies only; do not repeat cards. Use the existing GPU-readback seam and label times as Editor wall upper bounds.

Check rendered-image agreement against the 16-sector reference at both fixed views using the same crowd state. Save image-difference counts and maximum channel error. A mismatch must block any claim that a grouping preserves appearance. More submitted off-screen fans are acceptable; lost visible content is not.

## Checks

Pure tests must protect stable grouping and retained per-fan data where feasible. Native crowd tests must cover regrouping after a warm cache and after transform changes/enable, not just initial build. Compile real changed source with actual Unity libraries into a fresh DLL. Require compiler exit 0, no error lines, and a new output. Review before native execution.

Native runs need a new saved plan and explicit approval. Keep one worker, the 25 percent CPU cap, and the approved host-guard rule. GPU load is not capped; the prior trace attempt suffered a graphics crash and its cause is not proved. Preserve all prior evidence and do not touch another run.

## Local ticket

Blocked by: completed stall trace. Scope is one diagnostic comparison of the existing performance task.

- [x] Regroup existing fans without state changes; default stays 16. Native image proof remains open.
- [x] Count mesh submissions and visible submitted fans.
- [x] Implement 4/8/16/32-sector comparison with rotated paired rounds in two existing views.
- [x] Implement image-difference records against a repeated reference.
- [x] Pure checks, clean real-source compile, and code review pass. Integrated as a2bc7a84c3342caabbd2c7897df3b1773558edf4. Native plan b84716980292423ba7cdd769cd7800d1 awaits approval.
- [x] Approved native comparison completed. Fourteen view tests passed and all images matched. Eight sectors helped the shooter view but hurt up-ice. Reject a global default change; preserve 16-sector culling and pursue visible-pair merging.
- [ ] A separate full-game check proves any selected production change before claiming the lag fixed.
