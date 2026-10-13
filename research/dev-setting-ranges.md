# Safe ranges for the dev settings (research for #138)

Read from main at `ab161c2` (code and tests only; nothing was run). Defaults come from `Assets/Scripts/Core/RoundSettings.cs` (RS below).

How to read "min / max": **Hard** means code or a test shows a break outside it (evidence given). **Soft** means a judgement, because the code sets no limit (marked "judgement"; Garrett can move it). All paths are under `Assets/`; `T:` is `Assets/Tests/EditMode/`.

## Table

| Setting | Default | Min | Max | Step | Why (evidence) |
|---|---|---|---|---|---|
| ReactionSeconds | 0.12 | 0.04 (soft) | 0.30 (soft) | 0.01 | Round rolls `Reaction + Wobble*(2r-1)` per Shot (Scripts/Core/Round.cs:256). Below the Wobble it would go negative, and the goalie would move before the Shot (`moving = phaseSeconds - ReactionSeconds`, Breakaway.cs:105). Default pinned by T:RoundReachTests.cs:38-44 (a default change breaks that test, not a tuned run). Upper 0.30 is judgement. |
| ReactionWobbleSeconds | 0.04 | 0 | min(0.10, ReactionSeconds) | 0.01 | Hard: wobble must be <= ReactionSeconds (Round.cs:256). 0 is used by many tests (T:GoalieAngleViewTests.cs:107, T:GoalieHitShapesTests.cs:510), so 0 is safe. 0.10 is judgement. |
| HandMetresPerSecond | 8 | LegMetresPerSecond | 12 (soft) | 0.5 | RS comment: "hands fastest, legs slower" (RoundSettings.cs:36-37). Gear speed used in IGoalieReach.cs:68-70. Keep Hand >= Leg so the order holds. Max is judgement. |
| LegMetresPerSecond | 6 | 3 (soft) | HandMetresPerSecond | 0.5 | Same lines. Min is judgement. |
| MaxPushMetres | 0.6 | ShuffleMaxMetres (0.3) | 0.9 (soft) | 0.05 | Past it `MoveSeconds` is infinity, so no gear reaches (IGoalieReach.cs:83). Below ShuffleMax no T-push can exist. Goal mouth is 6 ft = 1.83 m (half 0.915 m; the 6 ft figure is standard hockey, not read from code), so 0.9 keeps a push inside the net. Soft. |
| ShuffleMaxMetres | 0.3 | 0.1 (soft) | MaxPushMetres | 0.05 | A move of this size or less is a Shuffle, longer a T-push (SkatePush.cs:36). Default pinned T:RoundReachTests.cs:41; T:PushPoseTests.cs:32 tests the switch at this size. |
| PushAccelMetresPerSecondSquared | 40 | 20 (soft) | 60 (soft) | 5 | Sets push time `sqrt(2d(1/ap+1/ag))` (SkatePush.cs:46) and push share `ag/(ap+ag)` (PushPose.cs:88). T:GoalieRecoveryTests.cs:357-364 allows a speed step of `PushAccel*dt^2 + 0.03` using the default 40 (a higher value is a bigger visible step at the cut, so keep near 40). Default pinned T:RoundReachTests.cs:42. Range is judgement. |
| GlideDecelMetresPerSecondSquared | 20 | 10 (soft) | 40 (soft) | 5 | Same formulas. Test T:GoalieAngleTests.cs:226 checks speed change <= GlideDecel*dt. Pinned T:RoundReachTests.cs:43. Range is judgement. |
| SkateInSeconds | 2.5 | 1.5 (soft) | 5.0 | 0.5 | Skate speed is 14.318 m / SkateIn (T:BreakawayTests.cs:236, Breakaway.cs:57-63), so 1.5 s is 9.5 m/s. 5.0 is the largest value a test uses (T:BreakawayTests.cs:234). Min is judgement. |
| ShootingWindowSeconds | 3.0 | 1.5 (soft) | 6.0 | 0.5 | Must stay longer than LateSettleSeconds (1.0) so the Late Settle settles inside it (SetUp.cs:92, T:EasyGlowTests.cs:58, :210). 6.0 is the largest value tested (T:BreakawayTests.cs:234). Ring fraction divides by it (Breakaway.cs:115), so never 0. Min is judgement. |
| ResultHoldSeconds | 2.5 | 2.4 | 5.0 (soft) | 0.1 | Only applies to results with no Replay (Wide Miss, Too late); Goal, Save and Frame Hit use the fixed ReplayTiming.HoldSeconds 4.5 s (+ ShotFade.LeadSeconds 0.35 s before Shots 2-6) (Round.cs:123-128; ReplayTiming.cs:15-17; T:ReplayTimingTests.cs:82). Hard min 2.4 = AfterShot.RecoveredBy (0.5 + 1.9, AfterShot.cs:84): the goalie must be back in Ready Stance when the hold ends, else the next Shot starts mid-reaction (AfterShot.cs:52-53). The hold also has to be longer than ShotFade.LeadSeconds 0.35 s (ShotFade.cs:20), which 2.4 satisfies. Default pinned T:AfterShotTests.cs:163. Max is judgement. The Replay and ShotFade lengths are constants, not settings: do not expose them. |
| NormalDepthMetres | 0.2 | 0.15 | 0.25 | 0.01 | ADR 0005: "tune it between 0.15 and 0.25 m" (docs/adr/0005-depth-mistake-sets-on-the-goal-line.md:5). The Depth mistake sits on the goal line (depth 0) whatever this is (SetUp.cs:84), so it never breaks the Depth gap. A test uses 0.4 (T:GoalieReachTests.cs:36), but ADR range wins. |
| CheatMetres | 0.3 | 0.1 (soft) | 0.6 (soft) | 0.05 | Spot = angle.X +/- Cheat (SetUp.cs:90). No code limit. Judgement: keep below the post (0.915 m) and below MaxPush + a margin so the opened side stays beatable. |
| LateSettleMetres | 0.4 | 0.2 (soft) | 0.6 (soft) | 0.05 | Offset the Late Settle starts at (SetUp.cs:92, BreakawayView.cs:229). Judgement; keep <= MaxPushMetres so the settle is a push the push model shows. |
| LateSettleSeconds | 1.0 | 0.4 | min(2.0, ShootingWindowSeconds - 1.0) | 0.1 | T:EasyGlowTests.cs:67 uses 0.4, so 0.4 is a tested floor; the Easy glow waits until this many seconds into the window (EasyGlow.cs:7, :23). Hard: must be < ShootingWindowSeconds or the settle never finishes (SetUp.cs:92 clamps u at 1). Upper 2.0 is judgement. |
| PushOutMetres (visual, const) | 0.37 | 0.30 | 0.40 | 0.01 | The #130 band: out/back 0.30/0.065 to 0.40/0.086 and 0.37/0.10 pass, only 0.40/0.10 fails (a Shot ends sunk in the tilted Right Pad at t 0.067, a tunnelling case) (Scripts/Game/GoalieSkatePush.cs:18-24). Keep 3 cm from the failing corner. It is a `const`, so it is not a dev setting today. Do not expose without running T:GoalieHitShapesTests `..._push_held_on`. |
| PushBackMetres (visual, const) | 0.07 | 0.065 | 0.10 | 0.005 | Same lines. Out and Back must never both sit at 0.40 / 0.10. |

## Notes

- Hard constraints between settings: Wobble <= Reaction; Leg <= Hand; ShuffleMax <= MaxPush; LateSettleSeconds < ShootingWindowSeconds; LateSettleMetres <= MaxPush; ResultHold >= 2.4.
- RoundSettings is shared by reference with the live Round (inventory note), so a change mid-Round hits the live Round. Apply changes only between Rounds, or clamp every value on read.
- `RoundReachTests.cs:38-44` pins the defaults of Wobble, Hand, Leg, ShuffleMax, PushAccel, GlideDecel and MaxPush; `AfterShotTests.cs:163` pins ResultHold. Tuned runs are fine, but changing a default must update those tests.
- Not found in code or tests: any limit on Hand/Leg speed, Reaction upper bound, Cheat size. Those ranges are judgement and need a Play check.
