# The goalie reacts on the Shot instead of showing its pose first

_What the goalie reacts into changed in docs/adr/0002: a Save Reaction to where the Shot goes, not the Goalie Pose. The timing below still holds._

The goalie used to move into the Round's Goalie Pose during the slow-down, so the player read the gap and shot at it. Garrett changed this on 2026-10-09: the goalie holds the Ready Stance through the skate-in and slow-down, and only on the Shot does it react into the Goalie Pose, so the player gets no hint of the pose before shooting. The Goalie Pose still decides every Shot exactly as before; only when the character shows it changed. Easy mode's green glow on the Open Zones still shows before the Shot, as the deliberate help for that mode.

## Consequences

- The reaction takes 0.2 s of real time (`Breakaway.GoalieReactSeconds`), eased out. The on-screen puck flies at the Shot's real speed (distance ÷ speed, no floor; #46), so a hard Shot can reach the Net Face in under 0.2 s. Then the reaction is quicker: the same ease over the puck's flight time instead (`Breakaway.GoalieSet`), as a real goalie gets less time on a hard shot. Either way the goalie is fully posed when the puck reaches the Net Face, so a SAVE stops on the posed gear and a GOAL! goes through a real gap. (Until #46 the flight was never shorter than 0.3 s, which made every Shot over about 70 km/h look the same.)
- A saved puck's stop point is measured on the fully posed gear at the moment of the Shot, while the goalie is still in the Ready Stance.
- No Shot in time (TOO LATE): the goalie stays in the Ready Stance; there is no Shot to react to. A Miss or Frame Hit is a real Shot, so the goalie still reacts into the pose.
- With the `forceGoalieBlocks=true` developer flag the blocks follow the same timing: neutral block shapes (the same for every pose) from the skate-in until the Shot, then they morph into the pose's blocks over the same 0.2 s.
