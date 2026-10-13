# The goalie sets 0.2 m out, and a Depth mistake sets on the goal line

_Changes the Set-up numbers of [docs/adr/0003](0003-reach-in-time-decides-the-shot.md) (normal depth, the Depth mistake). Reach in time, the Set-up and the visible hint stay as ADR 0003 and [0004](0004-unity-physics-decides-the-shot.md) say._

Garrett's Play check on 2026-10-12 found a goalie set 0.6 m out too far from the goal line. A real goalie stays near it, so the normal depth is now 0.2 m (`NormalDepthMetres`, one setting; tune it between 0.15 and 0.25 m). The Depth mistake sets him on the goal line (depth 0), so tuning the normal depth never breaks it, and a Depth goalie is centred on the line. The Round decks stay unchanged.

## Considered Options

- Keep a separate `DepthMetres` that is subtracted from the normal depth: rejected, since it breaks when the normal depth is tuned (and at 0.2 m it would be the same number anyway).
- Drop the Depth mistake: rejected, since the Round decks still ask for it.

## Consequences

- The Depth gap is 0.2 m, smaller than before (0.45 m), so it shows less net high and in the corners.
- Reach Time and the Easy-mode hints follow from the new spots with no other change.
- Fallback: if the gap is too small to read in Play, Garrett picks again and the Depth mistake changes to something else readable.
