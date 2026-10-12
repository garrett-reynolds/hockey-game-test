# Reach in time decides the Shot, and the Set-up is a visible hint

_Supersedes docs/adr/0001 and docs/adr/0002._

_What decides the Shot changed in [docs/adr/0004](0004-unity-physics-decides-the-shot.md): Unity physics and the puck's real path decide it. The Set-up, the hint and how the goalie moves still hold._

ADR 0001 and ADR 0002 kept a hidden Goalie Pose, picked before the Shot, as what decided it: a Zone table on the Net Face said Save or Goal, and speed only nudged one roll. The result was fixed before the puck moved, the shooter could not read the goalie, and what the goalie did on the Shot did not look like racing the puck. With spec #61 (map #49) the Goalie Pose and the Zones are gone. Reach in time decides every Shot on the Net Face: a Save when the Reach Time (Reaction Time plus his quickest gear's move into the puck's line) is no longer than the Flight Time, otherwise a Goal. Where the Shot goes still decides a Miss or a Frame Hit.

Each Shot the goalie shows an honest Set-up: a Clean Set-up or one Set-up Mistake (Cheat, Depth or Late Settle). The Set-up is a visible hint before the Shot, so this ends ADR 0001's "no hint before the Shot" rule: reading the goalie and shooting hard at a Gap is the skill. The only hidden variation left is the small Reaction Time wobble, so a close call cannot be solved.

## Consequences

- The goalie's motion uses the race's numbers: he starts after the Reaction Time and moves the winning gear at the race speeds, so on a Goal he is seen to arrive just too late. The Save Reaction is the gear that wins the race, or, when his gear is already in the puck's line, the gear for where the Shot meets the Net Face.
- Shot speed matters only through the Flight Time. The Contested chance settings and the Top speed setting are gone from the settings screen and the settings file (old files still load; the old keys are ignored).
- Easy mode glows the Gaps a puck can fit through, measured by the same goalie reach as the race. It is a hint only and changes no numbers.
- The Annoyed Gesture comes from the Late Margin (a Stick Slap when he was only just too late, otherwise a Slump), not from where the Shot went.
- The developer block goalie (forceGoalieBlocks) is one block that stands across the Net Face where the goalie stands. It draws no Zones or poses.
- The Round's random rolls are the Set-up deck, the Attack Side shuffle, the Cheat post side and the Reaction Time wobble per Shot, in the order written on Round.
