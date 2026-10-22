# Reuse unchanged crowd bounds

## Problem and evidence

The full game remains over budget. The paired native crowd check measured player-view crowd cost of 4.4503 ms for bodies and 4.2087 ms for flat far fans. The flat-card saving is only 0.2417 ms in that view. The up-ice difference is larger, but both modes still exceed the 2 ms crowd target. Preserve the 3D look rather than adopt a low-value quality cut.

Crowd.Draw currently rebuilds every slice's world bounds with nine native TransformPoint calls for every camera. It also uploads unchanged world matrices to all materials for every camera. The rink usually stays still. The shader already skips its old-clip sample after an animation blend ends, so do not change the shader based on that rejected theory.

## Scope

Retain each slice's world bounds and each material's crowd-to-world matrix until the exact full local-to-world matrix changes. Include parent transforms, nonuniform and negative scales, and shear. Invalidate on fill and enable. Do not consume or modify Transform.hasChanged. Keep fan positions, body meshes, shaders, materials' other values, seating, draw order, camera filtering, gameplay, and detail levels unchanged.

## Check path

Use the existing rendered-camera seam to compare a previously drawn crowd after transform changes against a fresh crowd at the same transform. Keep the existing visibility, far-card and disable/enable test. The current native over-budget result is the performance RED signal; the new functional tests protect culling correctness and are not claimed to fail the correct uncached implementation.

After review, run focused native CrowdViewTests, then the same paired CrowdCostTool under one sequential, one-worker, bounded, approved plan. Compare with the saved native crowd-cost.json. Report both unchanged correctness and measured cost. Do not claim improvement if the difference is within noise or moves cost elsewhere.

## Ticket

Status: implementation and correctness review complete. Native tests passed. Timing does not prove a speed gain. The game lag remains unresolved.

- [x] Cache unchanged crowd bounds and world matrix uploads.
- [x] Exact matrix changes invalidate cached bounds.
- [x] Parent moves, turns, scale, shear, and re-enable preserve visibility against a fresh reference.
- [x] Focused native tests pass: 12 passed, 0 failed, 0 skipped.
- [x] Paired native before/after data captured. No clear speed gain; do not claim success.
- [x] Recheck full-game budget: still over limits. The lag is not fixed.
- [x] Keep the open Unity project unchanged until the change is ready to apply.
