# Makes ice-markings.jpg, the texture over the whole 60 m x 60 m ice sheet made by Tools/make_ice.py:
# white rink ice with faint skate scuffs and NHL markings, lined up with the net. The Net Face is at z = 0 on
# x = 0; the shooter skates in from -z. Image left = -x, right = +x, top = +z (behind the net), bottom = -z.
# Lines are a texture (not geometry) so they fade smoothly with mipmaps far away instead of breaking into dots.
#
# Scuffs: ambientCG "Ice002" (CC0), https://ambientcg.com/view?id=Ice002 , file Ice002_1K-JPG_Roughness.jpg
# from https://ambientcg.com/get?file=Ice002_1K-JPG.zip , tiled every SCUFF_TILE metres and turned near-white.
# Needs Pillow and numpy (pip install pillow numpy).
# Run: python Tools/make_ice_texture.py <path>/Ice002_1K-JPG_Roughness.jpg Assets/Resources/Models
import os, sys
import numpy as np
from PIL import Image, ImageOps

src, out = sys.argv[1], sys.argv[2]
N = 4096                          # pixels across; 60 m / 4096 = 1.5 cm per pixel
HALF = 30.0                       # ice runs -30..30 m on x and z
PX = 2 * HALF / N                 # metres per pixel
SCUFF_TILE = 3.0                  # metres per repeat of the scuff texture
FT = 0.3048                       # metres per foot

GOAL_LINE_W = 2 / 12 * FT         # 2 in red goal line, centred on the Net Face plane
WIDE_W = 1 * FT                   # 12 in blue line and centre red line
BLUE_NEAR = -64 * FT              # near edge of the blue line, 64 ft out from the goal line
CENTRE_Z = -89 * FT               # centre red line, 89 ft out
CREASE_HALF_W = 4 * FT            # crease is 8 ft wide (the net is 6 ft)
CREASE_R = 6 * FT                 # crease arc: 6 ft radius about the middle of the goal line
THIN_W = 2 / 12 * FT              # crease outline and faceoff circles
CIRCLE_X, CIRCLE_Z, CIRCLE_R = 22 * FT, -20 * FT, 15 * FT   # end-zone faceoff circles
NEUTRAL_DOT_Z = BLUE_NEAR - 5 * FT                         # neutral-zone faceoff dots
DOT_R = 1 * FT                    # 2 ft faceoff dots

ICE = np.array([238, 243, 248], np.float32)
RED = np.array([200, 16, 46], np.float32)
BLUE = np.array([0, 56, 166], np.float32)
CREASE_BLUE = np.array([115, 173, 235], np.float32)

c = (np.arange(N, dtype=np.float32) + 0.5) * PX - HALF
x = c[None, :]                    # columns: -x to +x
z = -c[:, None]                   # rows: +z at the top


def cover(sdf):
    """Antialiased coverage (0..1) of a shape from its signed distance in metres (negative inside)."""
    return np.clip(0.5 - sdf / PX, 0.0, 1.0)


# Scuffed ice base.
grey = ImageOps.autocontrast(Image.open(src).convert("L"), cutoff=1)
tile_px = round(SCUFF_TILE / PX)
tile = np.asarray(grey.resize((tile_px, tile_px), Image.LANCZOS), np.float32) / 255.0
reps = N // tile_px + 1
scuff = np.tile(tile, (reps, reps))[:N, :N]
img = np.broadcast_to(ICE, (N, N, 3)) * (1.0 - 0.07 * scuff)[..., None]


def paint(colour, coverage):
    global img
    img = img * (1.0 - coverage[..., None]) + colour * coverage[..., None]


r = np.sqrt(x * x + z * z)
crease_sides = np.maximum(np.abs(x) - CREASE_HALF_W, r - CREASE_R)   # inside both the 8 ft band and the 6 ft disc
crease = np.maximum(crease_sides, z)                                  # ... and in front of the goal line
paint(CREASE_BLUE, cover(crease))
paint(RED, cover(np.maximum(crease, -(crease_sides + THIN_W))))       # outline along the sides and arc

paint(RED, cover(np.abs(z) - GOAL_LINE_W / 2) * np.ones_like(x))
paint(BLUE, cover(np.abs(z - (BLUE_NEAR - WIDE_W / 2)) - WIDE_W / 2) * np.ones_like(x))
paint(RED, cover(np.abs(z - CENTRE_Z) - WIDE_W / 2) * np.ones_like(x))
for sx in (-1, 1):
    rc = np.sqrt((x - sx * CIRCLE_X) ** 2 + (z - CIRCLE_Z) ** 2)
    paint(RED, cover(np.abs(rc - CIRCLE_R) - THIN_W / 2))
    for dz in (CIRCLE_Z, NEUTRAL_DOT_Z):
        paint(RED, cover(np.sqrt((x - sx * CIRCLE_X) ** 2 + (z - dz) ** 2) - DOT_R))

Image.fromarray(np.clip(img + 0.5, 0, 255).astype(np.uint8)).save(
    os.path.join(out, "ice-markings.jpg"), quality=92, subsampling=0, optimize=True)
print(N, "x", N, "written")
