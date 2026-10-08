# Hockey Game

A 3D hockey game where the player shoots a real puck. Tracking electronics report each real shot to the game, and the game decides the outcome.

## Language

**Round**:
One complete game, made of exactly 6 Shots.
_Avoid_: Game, match, session

**Shot**:
One real puck shot, reported by the tracking electronics as a speed and a Net Face position.
_Avoid_: Attempt, throw

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

**Miss**:
A Shot that does not reach the Net Face, or no Shot in time. It uses one of the Round's 6 Shots.
