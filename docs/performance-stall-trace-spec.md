# Locate frame stalls before another game change

## Problem

The bounds validation completed with 12 passing native checks, but no proven speed gain. FrameBudgetTool reports combined game-step/render-call CPU wall time. Its GPU value includes render calls and a readback wait. Neither identifies actual CPU execution or the stage behind a stall. Crowd-off samples also stalled. The game lag remains unresolved.

## Agreed scope

Continue the existing performance diagnosis. Add an optional diagnostic trace to the existing full-game command. Preserve the default frame report, verdict, shots, scene, rendering, and game behavior. Do not change the live Unity project or weaken the test runner.

## Predictions

1. Scheduling or native waits: a slow frame has high wall time but little current-thread CPU execution. This does not prove which external source caused the wait.
2. Rendering: the render-call or GPU-readback stage dominates the slow frames.
3. Game work or managed collection: the game-step stage dominates, or slow frames coincide with managed collection increments. Correlation alone does not prove the cause.

## Trace seam

Use the existing FrameBudgetTool game-step, replay-render, shooter-render, and readback boundaries. An explicit command argument enables the trace. Record frame index, shot, phase, crowd mode, stage wall times, current-thread CPU times where supported, and managed collection changes. Mark unsupported CPU sampling explicitly. Keep off-crowd frames as well as on-crowd frames. Write the trace after measured work; do not perform file I/O per frame. Include enough context to exclude warm-up/settling frames when reviewing it.

The trace is diagnostic evidence. It must never turn the upper-bound frame results into a claim of actual live FPS or GPU-only duration. An untraced run must retain its existing path and results.

## Checks and limits

Use pure checks for trace data, invariant numeric serialization, unsupported timing values, and disabled behavior. Compile the changed Editor source against this Unity installation before asking for a native run. Review source and launcher before running Unity. Native execution needs its own saved plan and explicit approval. Keep one worker, 25 percent job CPU, 95 percent total CPU for 10 seconds, and a bounded approved duration. No other run may be stopped.

## Out of scope

Graphics reductions, gameplay changes, driver changes, stop/cleanup actions, remote tracker writes, and changes to the open game project.

## Ticket: trace the stalled frame

Blocked by: none. This is the next diagnostic slice of the existing performance task.

- [x] Opt-in trace preserves default reports and verdicts.
- [x] Trace separates game step, both cameras, and readback.
- [x] Current-thread CPU availability and collection deltas are explicit.
- [x] Both crowd modes and warm-up/shot context are present.
- [x] Pure checks and clean compile check pass. Both pure check sets passed four cases each. Fresh compiler proof has exit 0 and zero errors.
- [x] Review finds no blocking issue. Integrated commit 71d67d4cb90ec9b1966a3e76a44bb0c5cbd838bd.
- [x] Approved native retry 4bd143298e894fa8836abe8e335f3bba completed with trace. Rendering dominates most slow-frame time; crowd adds roughly 5 ms. Thread-CPU quantization and the synthetic Editor loop limit attribution. Live game lag remains unresolved.
