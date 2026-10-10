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

**Open Zone**:
A Zone where a Shot always scores on this attack.
_Avoid_: Hole, weak spot

**Covered Zone**:
A Zone where a Shot is always saved on this attack.
_Avoid_: Blocked zone, closed zone

**Contested Zone**:
A Zone where a Shot may score or be saved; a faster Shot is more likely to score.
_Avoid_: 50/50 zone, half-open zone

**Attack Side**:
The side the player approaches the net from on one Shot: left, middle, or right. It decides which Zones are Open, Covered, or Contested.
_Avoid_: Angle, lane

**Goalie Pose**:
The Round's hidden setup for one Shot. Each Attack Side has its own Goalie Poses, and each pose fixes which Zones are Open, Covered, or Contested, so it decides whether the Shot is a Save, a Goal or a Miss. Left and right are always from the shooter's view. The goalie character never shows it: the player sees the Ready Stance and then the goalie's reaction to where the Shot goes (see docs/adr/0002). Only the developer block goalie (forceGoalieBlocks) draws it.
_Avoid_: Stance (on its own it is ambiguous: say Goalie Pose or Ready Stance), position

**Ready Stance**:
The goalie's crouch before the Shot, the same for every Goalie Pose and Attack Side. The goalie holds it through the skate-in and slow-down, reacts out of it on the Shot, and keeps it when there is no Shot in time. In it the goalie turns on its spot to face the shooter (up to 30 degrees either way), and it keeps that turn through its reaction. In it he shows Stance Life; the goalie character also stands in it on the start and end screens.
_Avoid_: Neutral pose, idle pose

**Save Reaction**:
What the goalie character does on a Save, chosen only from where the Shot meets the Net Face: a glove save (the catching glove, on the shooter's right, mid to high), a blocker save (the blocker, on the shooter's left, mid to high), a pad save (low: the pad on the puck's side, down in the butterfly or standing), a stick save (along the ice through the middle) or a body save (the middle). The chosen gear reaches the puck's line, the puck stops on it, and the save sound and the rebound are that gear's. The same point always gets the same reaction, whatever the Goalie Pose. On a Goal or a Miss the goalie only reaches toward the puck and never touches it. Once the puck has got there the goalie's head follows it (a pad save watches the rebound).
_Avoid_: Save pose, save animation

**Annoyed Gesture**:
What the goalie character does on a GOAL! once the puck has met the netting: after a late reach and a look back into the net, he either slumps and shakes his head or slaps his stick on the ice, chosen by the Shot's Zone (column + row even: slump, odd: stick slap). He shows its end pose, then Recovery.
_Avoid_: Goal animation, celebration

**Relief**:
What the goalie character does on a Miss (wide or a Frame Hit) after his head has followed the puck going by: a small breath out and a glance back at the post on the puck's side, then Recovery.
_Avoid_: Miss animation

**Recovery**:
What the goalie character does once his reaction has played out (a Save's follow-through, with the catching glove held up with the puck in it or the blocker carried on out; an Annoyed Gesture; Relief): he glides back across the crease to his spot and comes back up into the Ready Stance, facing the same shooter, all before the result hold ends. The next Shot (or the end screen) starts from there, so he never snaps.
_Avoid_: Reset, return to idle

**Stance Life**:
The goalie character's small, steady motion in the Ready Stance: breathing, weight shifts from skate to skate (the skates stay put) and now and then a stick tap. It runs on game time from a clock alone, never from the Goalie Pose, the Attack Side or the Shot, so it gives no hint. It fades out as he reacts (none once he has fully reacted) and back in during Recovery.
_Avoid_: Idle animation, idle pose (Idle is the start screen's phase)

**Miss**:
A Shot that does not reach the Net Face, or no Shot in time. It uses one of the Round's 6 Shots.

**Frame Hit**:
A Miss whose position lands on a post or the crossbar: just outside the Net Face, within the frame's 0.08 m thickness. It pings and the puck deflects off the frame, but it is still a Miss. The goalie's reach on a Miss keeps off the puck, so a Frame Hit always rings off the frame. Should the stick, the blocker or a skate stand in front of a Save Reaction's gear on the puck's line, the puck stops on that and sounds like it.
_Avoid_: Iron, post shot
