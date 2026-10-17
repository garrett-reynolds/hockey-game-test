# 01: Skip unchanged Easy mode gap refreshes

**What to build:** Keep the same Easy mode hints while avoiding view refreshes when the measured cells have not changed.

**Blocked by:** None for the focused pure logic change. A whole-game completion claim is blocked by fresh native proof.

**Status:** focused change implemented; native proof pending

- [x] An unchanged merge retains the current gap set, letting the existing view early return apply.
- [x] Changed selected cells produce the same hint values and counts as before.
- [x] Cells outside the selected range stay unchanged.
- [x] Existing gap sets remain immutable.
- [x] Focused checks fail before the fix and pass after it.
- [x] No game code in the open project changes.
- [ ] Whole-game frame time and native view checks pass before calling the lag fixed.
