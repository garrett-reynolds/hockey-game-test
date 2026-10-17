# Avoid unchanged Easy mode refreshes

## Problem Statement

The user reports lag during play. One source-level finding is that EasyGlow.Update merges three measured cells into a new NetFaceGaps every frame, including frames whose measured values have not changed. GoalieView.ShowGaps skips work only when it receives the same object. A new object therefore triggers ApplyGaps and 96 cell SetActive calls even when the hint is unchanged.

## Agreed scope

Remove this redundant refresh while preserving every gap value, update range, sweep order, shot rule, and displayed hint. The user has authorized performance improvements. This narrow change adds no new product choice.

## Solution

NetFaceGaps.With returns the current immutable gap set when the selected measured cells already match. A changed selected cell still produces a separate immutable gap set. Differences outside the selected range must not trigger a replacement.

## User Stories

1. As a player, I want an unchanged hint to avoid repeated view work.
2. As a player, I want a changed gap to appear on the same update as before.
3. As a player, I want Easy mode to keep the same game rules and hint values.

## Implementation Decisions

Keep the existing public API and the existing ShowGaps reference check. Change only the pure gap merge. Keep all constructor defensive copies. Do not change the crowd, shadows, timing, or physics in this slice.

## Testing Decisions

Add focused NUnit tests for unchanged selected cells, differences outside the selected range, changed selected cells, clamped and empty ranges, counts, and immutability. Run the actual pure C# source and tests with one worker without Unity. Confirm the new unchanged-result test fails before the fix and passes after it. Inspect the ShowGaps caller to confirm the retained reference takes its existing early return.

## Out of Scope

No crowd detail reduction. No test-control changes. No open-project edits. No claim that whole-game lag is fixed without a native before/after check.

## Further Notes

Two native baseline attempts ended at the host CPU guard before frame measurement. This pure logic slice has its own direct regression signal. Whole-game timing and native view proof remain open. This is a local draft; no issue or PR is published.
