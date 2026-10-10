# The goalie reacts on the Shot instead of showing its pose first

_What the goalie reacts into changed in docs/adr/0002: a Save Reaction to where the Shot goes, not the Goalie Pose. The timing below still holds._

The goalie used to move into the Round's Goalie Pose during the slow-down, so the player read the gap and shot at it. Garrett changed this on 2026-10-09: the goalie holds the Ready Stance through the skate-in and slow-down, and only on the Shot does it react into the Goalie Pose, so the player gets no hint of the pose before shooting. The Goalie Pose still decides every Shot exactly as before; only when the character shows it changed. Easy mode's green glow on the Open Zones still shows before the Shot, as the deliberate help for that mode.

## Consequences

- The reaction takes 0.2 s of real time (`Breakaway.GoalieReactSeconds`), eased out. The on-screen puck flight is never shorter than 0.3 s (`ShotFlight.MinSeconds`), so the goalie is fully posed before even the fastest puck reaches the Net Face, and a SAVE stops on the posed gear and a GOAL! goes through a real gap.
- A saved puck's stop point is measured on the fully posed gear at the moment of the Shot, while the goalie is still in the Ready Stance.
- No Shot in time (TOO LATE): the goalie stays in the Ready Stance; there is no Shot to react to. A Miss or Frame Hit is a real Shot, so the goalie still reacts into the pose.
- With the `forceGoalieBlocks=true` developer flag the blocks follow the same timing: neutral block shapes (the same for every pose) from the skate-in until the Shot, then they morph into the pose's blocks over the same 0.2 s.
