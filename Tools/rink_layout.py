# The Rink Layout numbers the Python art tools build to, copied by hand from Assets/Scripts/Core/RinkLayout.cs (the one
# source). RinkLayoutTests checks the NAME = number lines below match it: change both together.
# Metres, in the scene's frame: x right of the Net Face centre, y up, z toward the near net (near goal line at z = 0;
# the rink grows toward the player, -z, and the far end mirrors the near end about the centre line).

FT = 0.3048

# The rink (inside face of the boards).
HALF_WIDTH = 12.955
CORNER_RADIUS = 8.53
NEAR_END_BOARDS_Z = 3.35
FAR_END_BOARDS_Z = -57.61
CENTRE_LINE_Z = -27.13
NEAR_BLUE_LINE_Z = -19.6596

# Boards and glass (metres above the ice).
BOARD_HEIGHT = 1.07
KICK_PLATE_TOP = 0.20
END_GLASS_TOP = 3.51
SIDE_GLASS_TOP = 2.90

# The arena's roof (metres above the ice): the Centre Board hangs under it.
ROOF_HEIGHT = 30.0

# The ice sheet: a square centred on the centre line.
ICE_SIZE = 64.4
ICE_CENTRE_Z = -27.13

# Markings.
THIN_LINE = 2 / 12 * FT
WIDE_LINE = 1 * FT
CIRCLE_RADIUS = 15 * FT
SPOT_DIAMETER = 2 * FT
CENTRE_SPOT_DIAMETER = 1 * FT
END_ZONE_DOT_X = 22 * FT
NEAR_END_ZONE_DOT_Z = -20 * FT
NEAR_NEUTRAL_DOT_Z = NEAR_BLUE_LINE_Z - WIDE_LINE / 2 - 5 * FT
REFEREE_CREASE_RADIUS = 10 * FT
CREASE_RADIUS = 6 * FT
CREASE_HALF_WIDTH = 4 * FT
TRAPEZOID_GOAL_LINE_WIDTH = 6.7056
TRAPEZOID_END_BOARDS_WIDTH = 8.5344

# Benches on side BENCH_SIDE (-1: x < 0), penalty boxes on the other; each (z from, z to), BENCH_DEPTH behind the boards.
BENCH_SIDE = -1
BENCH_DEPTH = 1.8
BENCH_LENGTH = 6.1
BENCH_GAP = 2.44
PENALTY_BOX_LENGTH = 3.66
TIMEKEEPER_LENGTH = 2.44

# The stands (metres): front wall offset from the boards and its height; lower-tier rows (depth, rise, count); the Ribbon
# Board ring sits on the fascia after the lower rows (RibbonOffset = STANDS_FRONT + LOWER_ROWS * ROW_DEPTH out,
# RibbonBottom = STANDS_FRONT_HEIGHT + LOWER_ROWS * LOWER_RISE up) and is RIBBON_HEIGHT tall.
STANDS_FRONT = 1.0
STANDS_FRONT_HEIGHT = 1.6
ROW_DEPTH = 0.85
LOWER_ROWS = 20
LOWER_RISE = 0.42
RIBBON_HEIGHT = 1.2


def _pair(gap, length):
    """Two openings either side of the centre line, gap apart, far one first."""
    return [(CENTRE_LINE_Z - gap / 2 - length, CENTRE_LINE_Z - gap / 2), (CENTRE_LINE_Z + gap / 2, CENTRE_LINE_Z + gap / 2 + length)]


BENCHES = _pair(BENCH_GAP, BENCH_LENGTH)
PENALTY_BOXES = _pair(TIMEKEEPER_LENGTH, PENALTY_BOX_LENGTH)


def mirror(z):
    """The same place at the other end of the rink."""
    return 2 * CENTRE_LINE_Z - z
