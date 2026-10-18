# Merge visible crowd slices without loosening culling

## Evidence

Plan b84716980292423ba7cdd769cd7800d1 completed in 83.91 seconds with 14 native tests passed and all eight variant images exactly matching. Shooter: 8 sectors submitted 48 calls and 3657 fans, versus 96 calls and the same 3657 fans at 16 sectors. Paired median crowd cost was 2.71109 versus 3.05822 ms, lower in all five paired rounds. Up-ice: 8 sectors submitted 10909 fans versus 9588 at 16 and cost 5.40582 versus 4.90447 ms, worse in all five rounds. A global switch to 8 sectors is not justified.

## Production change

Keep the 16-sector layout and its existing per-slice visibility checks. Merge adjacent slices from the same coarse 8-sector pair only when BOTH are visible and their instance ranges are contiguous. Preserve outfit/detail/tier boundaries. Keep instance order, buffers, fans, mesh, material, animation, and appearance unchanged. Use the first slice's start property and combined count/bounds. Never merge in an invisible neighbor. Do not regroup or allocate per camera/frame.

The key is (group * 2 + tier) * sectorCount + sector. A coarse pair shares key / 2. This pair rule is valid for each supported even sector count, but production stays at 16. A diagnostic switch may retain the exact old submission path for same-process baseline comparison. The intended production default enables safe visible-pair merging after validation.

## Measurement and checks

Add a focused native test that renders merge off/on and proves exact pixels and submitted fan count while reducing submissions in the shooter view. Include warm bounds, changed parent transforms, and enable where useful. Retain all existing correctness tests. Pure checks protect pair boundaries and contiguous ranges where feasible.

Extend the existing small Editor comparison tool to compare baseline versus merging with the SAME 16-sector layout and camera views. Reuse timing/image helpers. Keep five rotated paired rounds, 30 measured renders per side, two settling frames, fixed crowd state, repeated reference image, and exact image checks. Record both modes' calls/fans/per-round costs. Keep the original sector sweep explicitly unmerged so it remains reproducible. Do not add shader or gameplay changes.

Compile real current source into a fresh DLL, check zero compiler errors, review, then save a new approved native plan that runs focused crowd tests followed by this comparison. If the comparison proves a useful saving without a view regression, validate the whole game before applying to the user's project or claiming the lag fixed. Read test-load.md before every control action. Do not reuse grants or touch other runs. Prior GPU crash risk remains disclosed; CPU limits do not cap GPU load.

## Local ticket

Blocked by: completed sector comparison.

- [x] Implement merging only visible adjacent coarse-pair slices with contiguous ranges.
- [x] Native proof: all 15 crowd tests passed. Submitted fans and rendered pixels stayed identical in both comparison views.
- [x] Default culling remains 16 sectors; no per-frame regroup/allocation.
- [x] Four pure policy checks, six pure launcher checks, clean full-source compile, and code reviews pass. Integrated as 0c73c18a1e0223c4ccf811840e3e8fdd2b7468fc.
- [x] Approved native comparison showed 44.6% lower shooter crowd cost and 6.9% lower up-ice crowd cost. Both views met the useful-gain gate.
- [x] Full-game check completed and improved, but some limits remain unmet. Six checked runtime files applied to the closed target project with backups. See performance-result.md.
- [ ] Human play check confirms whether the reported lag is resolved. Overall frame-budget acceptance remains open.
