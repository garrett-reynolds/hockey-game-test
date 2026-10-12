# Unity physics moves the puck and decides the Shot

_Replaces the deciding part of [docs/adr/0003](0003-reach-in-time-decides-the-shot.md): Reach in time no longer decides a Shot. The Set-up, the visible hint and how the goalie moves stay as ADR 0003 says._

The puck was moved by our own code, built up ticket by ticket (straight-line flight, then deflections, spin, real speed and gear rebounds), because the result was picked first and the puck only had to show it. That left the puck stopping dead in the netting and dropping straight down, and passing through the boards, with no way to make it behave like a real puck without writing a physics engine. Garrett decided on 2026-10-12 (map: puck behaves organically) that Unity's built-in physics moves the puck from the Shot onwards, the netting is cloth that the puck and the netting push on each other, and the puck's real path decides the Shot: a Goal when the whole puck crosses the goal line, a Save or a Miss otherwise. The Shot is launched so that, with gravity, it crosses the Net Face at the reported position and speed.

## Considered Options

- Keep Reach in time deciding and steer the physics puck to the picked result: rejected, since steering fights the physics that makes it look real.
- Use Unity physics only for the netting, boards and glass, and keep our own code for the goalie, posts and ice: rejected, since two hit systems would disagree and be harder to tune.

## Consequences

- The goalie still moves by Reach in time (Reaction Time, then his gear at the race speeds), but his gear needs physics hit shapes, and whether it really touches the puck decides a Save.
- Easy mode's Gap glow and the Annoyed Gesture still come from the Reach in time numbers, as a guess; physics may disagree with them on a close Shot.
- A Frame Hit can now end a Goal (off the post and in).
- Our own puck code (PuckMotion, PuckObstacles, the scripted flight and goal drop in ShotEffects) goes away.
