# Jersey and net replay work

User request: build GitHub issues #220 and #221.

## Scope

- #220: visible cloth texture, an original generic front crest, and a back number on the goalie jersey. Use sample number 31. Keep the existing rig and its motion.
- #221: make net movement visible during slow-motion replay and improve its separation from the ice. Inspect the cause before choosing a resolution increase.
- Keep the validated crowd performance changes from 21b1ba0.
- Preserve the user's package, settings, imported assets, and open Unity session.

## Source findings

- The goalie has separate skinned clothing renderers. Cloth detail can use cached maps on the existing jersey material without adding renderers.
- ReplayView uses a 512 by 280 picture. ReplayTrack saves transform poses, but not cloth mesh motion. Increasing picture size alone cannot restore missing motion.
- The net currently uses white cords. Contrast against the ice needs a visual check.

## Work ownership

- Integration: C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\source
- Jersey worker: sibling jersey worktree.
- Net worker: sibling net worktree.
- Proof worker: editor-only proof tools in the integration worktree.
- Live user project: C:\Users\garrett.reynolds\Projects\Personal\hockey-wt\111. Do not edit while Unity is open.
- Before hashes: C:\Users\garrett.reynolds\Projects\Personal\hockey-visual-20261017\live-before-hashes.json

## Checks still required

- Source review and fresh compiler proof.
- Focused native checks under a new saved and approved test plan.
- Actual before/after views for jersey and replay movement.
- Measure added rendering/recording cost without rerunning unrelated suites.
- User visual check before closing either issue.

No heavy test approval from the earlier performance work applies to this change.
