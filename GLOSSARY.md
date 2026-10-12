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

**Attack Side**:
The side the player approaches the net from on one Shot: left, middle, or right. It decides the angle the goalie plays in his Set-up.
_Avoid_: Angle, lane

**Set-up**:
Where the goalie is set for one Shot when the shooting window opens: his spot across the crease and his depth in it. The player can see it, and it is either a Clean Set-up or has one Set-up Mistake.
_Avoid_: position

**Clean Set-up**:
A Set-up on the line from the shooter to the net centre, at his normal depth, settled in time.

**Set-up Mistake**:
A visible gap in a Set-up that the shooter can read: a Cheat (set off the angle line toward one post, opening the other side), Depth (set too deep, showing more net high and in the corners) or a Late Settle (still sliding across when the shooting window opens, the gap closing over about a second).
_Avoid_: Weakness, tell, hole

**Gap**:
Part of the Net Face the goalie's set gear leaves open, so the shooter can see it. A Set-up Mistake makes a bigger Gap; a Clean Set-up leaves only small ones. A Shot into a Gap is not a sure Goal: the goalie may still get his gear in the puck's way.
_Avoid_: hole, weak spot

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
The goalie's race against the puck: he is expected to get there when the Reach Time is no longer than the Flight Time. It drives how he moves and the hints (Easy mode, the Annoyed Gesture), but it does not decide the Shot: the puck's real path does, and the puck can still beat a gear that arrives in time, or hit one that does not.
_Avoid_: Save chance

**Goal**:
A Shot whose whole puck crosses the goal line, however it got there (straight in, off a post, off the goalie).
_Avoid_: Score

**Ready Stance**:
The goalie's crouch before the Shot, the same for every Set-up and Attack Side. The goalie holds it through the skate-in and slow-down, reacts out of it on the Shot, and keeps it when there is no Shot in time. In it the goalie turns on its spot to face the shooter (up to 30 degrees either way) and shuffles across and in or out to play the angle and get set into his Set-up, and it keeps that turn through its reaction. In it he shows Stance Life; the goalie character also stands in it on the start and end screens.
_Avoid_: Stance (on its own), neutral pose, idle pose

**Save Reaction**:
What the goalie character does with his gear on the Shot: a glove save (the catching glove, on the shooter's right), a blocker save (the blocker, on the shooter's left), a pad save (the pad on the puck's side, down in the butterfly or standing), a stick save (along the ice) or a body save. It is the gear that reaches the puck's line first in Reach in time; when his gear is already in the puck's line, it is the gear for where the Shot meets the Net Face (glove or blocker mid to high, a pad low, the stick along the ice through the middle, the body in the middle). On a Save the gear reaches the puck's line, the puck stops on it, and the save sound and the rebound are that gear's. On a Goal he moves the same way at his real speed and is seen to arrive just too late. On a Miss he only reaches toward the puck and never touches it. Once the puck has got there the goalie's head follows it (a pad save watches the rebound).
_Avoid_: Save pose, save animation

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
The goalie character's small, steady motion in the Ready Stance: breathing, weight shifts from skate to skate (the skates stay put) and now and then a stick tap. It runs on real time (so it carries on at full speed through the slow-down) from a clock alone, never from the Set-up, the Attack Side or the Shot, so it gives no hint. It fades out as he reacts (none once he has fully reacted) and back in during Recovery.
_Avoid_: Idle animation, idle pose (Idle is the start screen's phase)

**Save**:
A Shot whose puck touches the goalie's gear and does not go in, however it got there (straight on, or off a post first).
_Avoid_: Stop, block

**Miss**:
A Shot whose puck neither goes in nor touches the goalie's gear (wide, or a Frame Hit that stays out), or no Shot in time. It uses one of the Round's 6 Shots.

**Whistle**:
The moment the game decides a Shot. For a Goal it is when the whole puck crosses the goal line. Otherwise it is the first of: the puck has stopped, the puck has gone (behind the goal line outside the net, or out of view), or a set time after the Shot runs out. Nothing the puck does after the Whistle changes the result, so a rebound that rolls in late is not a Goal.
_Avoid_: Call, end of the Shot

**Frame Hit**:
A Shot whose puck strikes a post or the crossbar. It pings and the puck bounces off the frame; where it then goes decides a Goal or a Miss. Should the stick, the blocker or a skate stand in front of a Save Reaction's gear on the puck's line, the puck stops on that and sounds like it.
_Avoid_: Iron, post shot

**Video Board**:
The big screen hung in front of the near end's crowd, at the Ribbon Board's height. It shows the score during the skate-in, one dim still colour from goalie set to the Shot, and the Replay after a Shot.
_Avoid_: Jumbotron, scoreboard, centre-hung

**Ribbon Board**:
The thin LED strip that runs around the stands. It shows ads, and words such as GOAL! after a Shot.
_Avoid_: LED ribbon, ticker

**Score Box**:
The small score in a corner of the screen, like on TV. It stays on screen all Round.
_Avoid_: HUD, score bug

**Replay**:
The slow-motion view of a Shot from behind the net, played on both the Video Board and the Centre Board after a Goal, a Save or a Frame Hit (never after a wide Miss or no Shot in time).
_Avoid_: Instant replay, highlight

**Goal Show**:
What the arena does on a Goal: the goal horn, the red goal light, GOAL! on the Ribbon Board and the roof lights pulsing red. It starts with the horn, when the puck lands, and the lights pulse until the result hold ends (about 4.5 s, through the Replay). It is the same on every Goal.
_Avoid_: Goal celebration, light show

**Arena Show**:
What the whole arena does at each moment of a Round, worked out from the Round alone: its phase and the time in it, the last Shot's result, a new best and the score. It decides the Video Board, the Ribbon Board, the crowd, the Goal Show lights, the camera, the Clap Chant and when the Calm Area holds still; every arena part only follows it.
_Avoid_: Arena state machine, show controller, effects

**Rink Layout**:
The one source of the rink's NHL sizes and places: the rink and its corners, the lines and the trapezoid, the boards and glass, the ad slots and ice logos, the Calm Area's bounds, the far net, the benches and penalty boxes, and where the Video Board and Centre Board hang. Nothing else holds its own copy of a size.
_Avoid_: Rink dimensions, rink config

**Calm Area**:
The part of the arena behind the net, as wide as the back of the trapezoid, from the ice to the top of the view at the Shot. From when the goalie is set until the Shot it stays still, soft and dim, so the goalie and each Gap are easy to read.
_Avoid_: Read zone, quiet zone, background

**Centre Board**:
The four-screen box hung high over centre ice. Every screen shows the same picture as the Video Board.
_Avoid_: Jumbotron, scoreboard, centre-hung

**Team Skin**:
One package that holds a team's look and sound: the team colours, the centre-ice logo and roof banners, the crowd's jersey colours, the Video Board and Score Box look, and the goal horn, goal song and Clap Chant. A setting picks the skin before play. A skin never changes the ice lines, the brands, the rink shape, the goalie, the Goal Show timing or the Calm Area.
_Avoid_: Theme, team pack

**League Skin**:
The generic look, and the first Team Skin: no real team, with the "All-Star Hockey" placeholder mark.
_Avoid_: Default skin, generic theme

**Clap Chant**:
The crowd's "Let's go!" chant with its clap-clap, clap-clap-clap. It repeats with a quiet gap on the start screen and plays once at Round over, after the cheer or horn; never in the skate-in. A Team Skin can swap it, for example for "Let's go Bears!".
_Avoid_: Cheer (the crowd cheers on a Goal), chant loop
