# The Reach Limit, not the Flight Time, leaves Gaps open at normal Shot speeds

_Adds to [docs/adr/0003](0003-reach-in-time-decides-the-shot.md). Physics still decides the Shot ([docs/adr/0004](0004-unity-physics-decides-the-shot.md)); PuckMotion is not extended._

Most players shoot at 30 to 50 km/h, and very few go over 100. At 30 to 50 km/h the Flight Time is 0.34 to 0.57 s, which is 3 to 5 times the Reaction Time (about 0.12 s). With only a push limit (Max push), the goalie's quickest gear got to almost any puck line in time, so a well-aimed Shot into a Gap was still saved and scoring was very hard (play-test 2026-10-13, spec #190).

The Reach Limit (#194) is the most distance any piece of gear (hand, pad, stick, and the push across) can move from its Set-up place during one Shot. `GearSpeeds.MoveSeconds` returns infinity for a move past it, so Reach in time finds no gear that reaches and the Shot is a Goal by the race. The goalie is then shown by the existing "no gear can reach" pose (a reach that stops short and stays off the puck's line), and the physics puck, which decides the Shot, meets no gear on that line. The Reach Limit is a Dev setting (`RoundSettings.ReachLimitMetres`, saved as `goalieReachLimitMetres`, range 0.2 to 1.5 m in 0.05 m steps), applied when a Round starts.

## Considered Options

- Make the goalie slower when the Shot is slower: rejected by Garrett (2026-10-15). He picked "limit how far the goalie can move".
- Raise the Reaction Time: rejected, since it also changes fast Shots and the Save Reaction timing.
- Extend PuckMotion to model a missed save: rejected by ADR 0004.

## Consequences

- At 30 to 50 km/h a Shot aimed beyond the gear's limit scores whatever the Flight Time; a Shot at the goalie is still saved. Shot speed still helps a little: less Reaction Time left to move inside the limit.
- A Set-up Mistake (Cheat, Depth, Late Settle) moves the Set-up place, so it opens more net than a Clean Set-up.
- The default is 0.595 m, tuned with the scoring measure (`GapShotTests.Scoring_measure_goal_rate_at_30_40_50_kmh`): Gap Shots score 28, 32 and 43 % at 30, 40 and 50 km/h (target 25 to 45 %). The rate is steep in the limit (0.59 m gave 34, 38, 47 %; 0.60 m gave 21, 25, 38 %), so retune with the measure after any change to the goalie's gear or hit shapes. Shots at a cell where gear is already in the line still score 8 to 13 % (target 5 % or less); the Reach Limit does not change them.
- Gear already in the puck's line at the Shot is unchanged (it skips the Reaction Time); the limit only applies to moves.
