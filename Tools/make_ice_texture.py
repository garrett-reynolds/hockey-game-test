# Makes ice-markings.jpg, the texture over the whole square ice sheet made by Tools/make_ice.py: white rink ice with
# faint skate scuffs and the full set of NHL markings, both ends. Sizes and places come from Tools/rink_layout.py (the
# Rink Layout). The near Net Face is at z = 0 on x = 0; the shooter skates in from -z; the far end mirrors the near end
# about the centre line. Image left = -x, right = +x, top = +z (behind the near net), bottom = -z (the far end).
# Lines are a texture (not geometry) so they fade smoothly with mipmaps far away instead of breaking into dots.
#
# Scuffs: ambientCG "Ice002" (CC0), https://ambientcg.com/view?id=Ice002 , file Ice002_1K-JPG_Roughness.jpg
# from https://ambientcg.com/get?file=Ice002_1K-JPG.zip , tiled every SCUFF_TILE metres and turned near-white.
# Needs Pillow and numpy (pip install pillow numpy).
# Run: python Tools/make_ice_texture.py <path>/Ice002_1K-JPG_Roughness.jpg Assets/Resources/Models
import os, sys
import numpy as np
from PIL import Image, ImageOps
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # finds rink_layout.py even when run with -I
from rink_layout import *

src, out = sys.argv[1], sys.argv[2]
N = 4096                          # pixels across; 64.4 m / 4096 = 1.6 cm per pixel
HALF = ICE_SIZE / 2               # ice runs -HALF..HALF on x, and ICE_CENTRE_Z -HALF..+HALF on z
PX = ICE_SIZE / N                 # metres per pixel
SCUFF_TILE = 3.0                  # metres per repeat of the scuff texture
SCUFF_ORIGIN = (-30.0, 30.0)      # world (x, z) where a scuff tile starts: the same scuffs as on the old 60 m sheet

NEAR_GOAL_Z, FAR_GOAL_Z = 0.0, mirror(0.0)

ICE = np.array([238, 243, 248], np.float32)
RED = np.array([200, 16, 46], np.float32)
BLUE = np.array([0, 56, 166], np.float32)
CREASE_BLUE = np.array([115, 173, 235], np.float32)

c = (np.arange(N, dtype=np.float32) + 0.5) * PX - HALF
x = c[None, :]                    # columns: -x to +x
z = ICE_CENTRE_Z - c[:, None]     # rows: +z at the top


def cover(sdf):
    """Antialiased coverage (0..1) of a shape from its signed distance in metres (negative inside)."""
    return np.clip(0.5 - sdf / PX, 0.0, 1.0)


# Scuffed ice base: the scuff tile repeats every SCUFF_TILE metres from SCUFF_ORIGIN.
grey = ImageOps.autocontrast(Image.open(src).convert("L"), cutoff=1)
tile_px = 256
tile = np.asarray(grey.resize((tile_px, tile_px), Image.LANCZOS), np.float32) / 255.0
cols = (((x[0] - SCUFF_ORIGIN[0]) / SCUFF_TILE % 1.0) * tile_px).astype(np.int32)
rows = (((SCUFF_ORIGIN[1] - z[:, 0]) / SCUFF_TILE % 1.0) * tile_px).astype(np.int32)
scuff = tile[rows[:, None], cols[None, :]]
img = np.broadcast_to(ICE, (N, N, 3)) * (1.0 - 0.07 * scuff)[..., None]


def paint(colour, coverage):
    global img
    img = img * (1.0 - coverage[..., None]) + colour * coverage[..., None]


def band(z0, width):
    """A line right across the ice, centred on z0."""
    return cover(np.abs(z - z0) - width / 2) * np.ones_like(x)


def ring(cx, cz, r):
    return cover(np.abs(np.sqrt((x - cx) ** 2 + (z - cz) ** 2) - r) - THIN_LINE / 2)


def spot(cx, cz, diameter):
    return cover(np.sqrt((x - cx) ** 2 + (z - cz) ** 2) - diameter / 2)


def segment(x0, z0, x1, z1):
    """A thin line from (x0, z0) to (x1, z1)."""
    dx, dz = x1 - x0, z1 - z0
    t = np.clip(((x - x0) * dx + (z - z0) * dz) / (dx * dx + dz * dz), 0.0, 1.0)
    return cover(np.sqrt((x - x0 - t * dx) ** 2 + (z - z0 - t * dz) ** 2) - THIN_LINE / 2)


for goal, out_dir in ((NEAR_GOAL_Z, -1.0), (FAR_GOAL_Z, 1.0)):   # out_dir: from the goal line toward centre ice
    ahead = (z - goal) * out_dir                                   # metres in front of this goal line
    r = np.sqrt(x * x + (z - goal) ** 2)
    crease_sides = np.maximum(np.abs(x) - CREASE_HALF_WIDTH, r - CREASE_RADIUS)   # inside the 8 ft band and the 6 ft disc
    crease = np.maximum(crease_sides, -ahead)                                     # ... and in front of the goal line
    paint(CREASE_BLUE, cover(crease))
    paint(RED, cover(np.maximum(crease, -(crease_sides + THIN_LINE))))            # outline along the sides and arc
    paint(RED, band(goal, THIN_LINE))
    boards = goal - out_dir * (NEAR_END_BOARDS_Z - NEAR_GOAL_Z)
    for sx in (-1, 1):                                                            # the trapezoid behind the net
        paint(RED, segment(sx * TRAPEZOID_GOAL_LINE_WIDTH / 2, goal, sx * TRAPEZOID_END_BOARDS_WIDTH / 2, boards))
        dot_z = goal + out_dir * -NEAR_END_ZONE_DOT_Z
        paint(RED, ring(sx * END_ZONE_DOT_X, dot_z, CIRCLE_RADIUS))
        paint(RED, spot(sx * END_ZONE_DOT_X, dot_z, SPOT_DIAMETER))

for blue in (NEAR_BLUE_LINE_Z, mirror(NEAR_BLUE_LINE_Z)):
    paint(BLUE, band(blue, WIDE_LINE))
for dz in (NEAR_NEUTRAL_DOT_Z, mirror(NEAR_NEUTRAL_DOT_Z)):
    for sx in (-1, 1):
        paint(RED, spot(sx * END_ZONE_DOT_X, dz, SPOT_DIAMETER))

paint(RED, band(CENTRE_LINE_Z, WIDE_LINE))
paint(BLUE, ring(0.0, CENTRE_LINE_Z, CIRCLE_RADIUS))
paint(BLUE, spot(0.0, CENTRE_LINE_Z, CENTRE_SPOT_DIAMETER))
# The referee's crease: a half circle against the boards on the penalty box side, at the centre line.
paint(RED, ring(-BENCH_SIDE * HALF_WIDTH, CENTRE_LINE_Z, REFEREE_CREASE_RADIUS) * (np.abs(x) <= HALF_WIDTH))

Image.fromarray(np.clip(img + 0.5, 0, 255).astype(np.uint8)).save(
    os.path.join(out, "ice-markings.jpg"), quality=92, subsampling=0, optimize=True)
print(N, "x", N, "written")
