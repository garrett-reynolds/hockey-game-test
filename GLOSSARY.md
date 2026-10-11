# Hockey Game

A 3D hockey game where the player shoots a real puck. Tracking electronics report each real shot to the game, and the game decides the outcome.

## Language

**Round**:
One complete game, made of exactly 6 Shots.
_Avoid_: Game, match, session

**Shot**:
One real puck shot, reported by the tracking electronics as a speed and a Net Face position.
_Avoid_: Attempt, throw

**Test Shot**:
A Shot made with the mouse instead of a real puck, for testing and demos. It follows the same rules as a real Shot.
_Avoid_: Fake shot, mouse shot, debug shot

**Net Face**:
The goal opening, as a flat plane. Shot positions are measured on it from its bottom-left corner.
_Avoid_: Goal mouth, target

**Zone**:
One box of the 3x3 grid that divides the Net Face.
_Avoid_: Block, cell, square
Going away with the Goalie Pose (map: the goalie reacts to the Shot, #49): name a place on the Net Face by its position, or in plain words such as "high on the glove side".

**Open Zone**:
A Zone where a Shot always scores on this attack.
_Avoid_: Hole, weak spot
Going away with the Goalie Pose (map #49); see Gap.

**Covered Zone**:
A Zone where a Shot is always saved on this attack.
_Avoid_: Blocked zone, closed zone
Going away with the Goalie Pose (map #49).

**Contested Zone**:
A Zone where a Shot may score or be saved; a faster Shot is more likely to score.
_Avoid_: 50/50 zone, half-open zone
Going away with the Goalie Pose; Reach in time replaces it, and speed then matters only through the Flight Time (map: the goalie reacts to the Shot, #49).

**Attack Side**:
The side the player approaches the net from on one Shot: left, middle, or right. It decides the angle the goalie plays in his Set-up (and, until the Goalie Pose goes, which Zones are Open, Covered, or Contested).
_Avoid_: Angle, lane

**Goalie Pose**:
The Round's hidden setup for one Shot. Each Attack Side has its own Goalie Poses, and each pose fixes which Zones are Open, Covered, or Contested, so it decides whether the Shot is a Save, a Goal or a Miss. Left and right are always from the shooter's view. The goalie character never shows it: the player sees the Ready Stance and then the goalie's reaction to where the Shot goes (see docs/adr/0002). Only the developer block goalie (forceGoalieBlocks) draws it.
_Avoid_: Stance (on its own it is ambiguous: say Goalie Pose or Ready Stance), position
Being replaced by Set-up (map: the goalie reacts to the Shot, #49).

**Set-up**:
Where the goalie is set for one Shot when the shooting window opens: his spot across the crease and his depth in it. The player can see it, and it is either a Clean Set-up or has one Set-up Mistake.
_Avoid_: Goalie Pose (the old hidden rule), position

**Clean Set-up**:
A Set-up on the line from the shooter to the net centre, at his normal depth, settled in time.

**Set-up Mistake**:
A visible gap in a Set-up that the shooter can read: a Cheat (set off the angle line toward one post, opening the other side), Depth (set too deep, showing more net high and in the corners) or a Late Settle (still sliding across when the shooting window opens, the gap closing over about a second).
_Avoid_: Weakness, tell, hole

**Gap**:
Part of the Net Face the goalie's set gear leaves open, so the shooter can see it. A Set-up Mistake makes a bigger Gap; a Clean Set-up leaves only small ones. A Shot into a Gap is not a sure Goal: Reach in time still decides.
_Avoid_: Open Zone (the old hidden rule), hole, weak spot

**Easy mode**:
A player setting that glows each Gap a puck can fit through, from when the goalie is set until the Shot. It only makes the Gap easier to see; it does not change who wins the reach.
_Avoid_: Assist, aim help

**Reaction Time**:
How long the goalie waits after the Shot before he starts to move. It is about the same every Shot, with a small hidden variation, so a close call cannot be solved.
_Avoid_: Reflex, delay

**Reach Time**:
The Reaction Time plus the time the goalie needs to move his quickest gear into the puck's line, measured where the puck passes him. Hands (glove, blocker) move fastest, legs (pads, stick) slower, and a push across with the whole body slowest. When his gear is already in the puck's line at the Shot, the Reach Time is zero.
_Avoid_: Save time

**Flight Time**:
How long the puck takes from the Shot to the goalie. A faster or closer Shot has a shorter Flight Time.
_Avoid_: Travel time

**Reach in time**:
The rule that decides a Shot on the Net Face: a Save when the Reach Time is no longer than the Flight Time, otherwise a Goal. Shot speed matters only through the Flight Time. It replaces the Goalie Pose and Zone rule (map: the goalie reacts to the Shot, #49). It does not decide a Miss or a Frame Hit: where the Shot goes decides those.
_Avoid_: Save chance, Contested roll

**Ready Stance**:
The goalie's crouch before the Shot, the same for every Goalie Pose and Attack Side. The goalie holds it through the skate-in and slow-down, reacts out of it on the Shot, and keeps it when there is no Shot in time. In it the goalie turns on its spot to face the shooter (up to 30 degrees either way) and, once Set-ups replace Goalie Poses, shuffles across and in or out to get set, and it keeps that turn through its reaction. In it he shows Stance Life; the goalie character also stands in it on the start and end screens.
_Avoid_: Neutral pose, idle pose

**Save Reaction**:
What the goalie character does on a Save, chosen only from where the Shot meets the Net Face: a glove save (the catching glove, on the shooter's right, mid to high), a blocker save (the blocker, on the shooter's left, mid to high), a pad save (low: the pad on the puck's side, down in the butterfly or standing), a stick save (along the ice through the middle) or a body save (the middle). The chosen gear reaches the puck's line, the puck stops on it, and the save sound and the rebound are that gear's. The same point always gets the same reaction, whatever the Goalie Pose. On a Goal or a Miss the goalie only reaches toward the puck and never touches it. Once the puck has got there the goalie's head follows it (a pad save watches the rebound).
_Avoid_: Save pose, save animation
Changing with Reach in time: the gear that reaches the puck's line first makes the Save, and on a Goal the goalie moves at his real speed and is seen to arrive too late (map: the goalie reacts to the Shot, #49).

**Annoyed Gesture**:
What the goalie character does on a GOAL! once the puck has met the netting: after a late reach and a look back into the net, he either slaps his stick on the ice (he was only just too late: his Reach Time was longer than the Flight Time by less than about 0.05 s) or slumps and shakes his head (he was beaten clearly). The gesture tells the shooter how close the race was. He shows its end pose, then Recovery.
_Avoid_: Goal animation, celebration

**Relief**:
What the goalie character does on a Miss (wide or a Frame Hit) after his head has followed the puck going by: a small breath out and a glance back at the post on the puck's side, then Recovery.
_Avoid_: Miss animation

**Recovery**:
What the goalie character does once his reaction has played out (a Save's follow-through, with the catching glove held up with the puck in it or the blocker carried on out; an Annoyed Gesture; Relief): he glides back across the crease to his spot and comes back up into the Ready Stance, facing the same shooter, all before the result hold ends. The next Shot (or the end screen) starts from there, so he never snaps.
_Avoid_: Reset, return to idle

**Stance Life**:
The goalie character's small, steady motion in the Ready Stance: breathing, weight shifts from skate to skate (the skates stay put) and now and then a stick tap. It runs on real time (so it carries on at full speed through the slow-down) from a clock alone, never from the Goalie Pose, the Attack Side or the Shot, so it gives no hint. It fades out as he reacts (none once he has fully reacted) and back in during Recovery.
_Avoid_: Idle animation, idle pose (Idle is the start screen's phase)

**Miss**:
A Shot that does not reach the Net Face, or no Shot in time. It uses one of the Round's 6 Shots.

**Frame Hit**:
A Miss whose position lands on a post or the crossbar: just outside the Net Face, within the frame's 0.08 m thickness. It pings and the puck deflects off the frame, but it is still a Miss. The goalie's reach on a Miss keeps off the puck, so a Frame Hit always rings off the frame. Should the stick, the blocker or a skate stand in front of a Save Reaction's gear on the puck's line, the puck stops on that and sounds like it.
_Avoid_: Iron, post shot
