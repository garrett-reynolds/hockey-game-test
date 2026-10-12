# Makes the game-worn ice art (#88, prototype look D "Game worn"):
#   ice-smoothness.png  the smoothness map for the ice sheet (URP Lit: smoothness = map alpha x the ice's gloss). Alpha is
#                       1 on the ice and CALM_GLOSS in the Calm Area (matte: no bright reflections behind the goalie),
#                       with a soft CALM_EDGE metres edge. Laid over the same square sheet as ice-markings.jpg
#                       (Tools/make_ice_texture.py): image left = -x, right = +x, top = +z (behind the near net).
#   ice-wear.png        the see-through skate-mark and snow layer RinkIce lays just over the ice, under the ice logos:
#                       faint skate strides all over and snow piled along the boards, none in the Calm Area. Covers the
#                       rink inside the boards: left = -HALF_WIDTH, right = +HALF_WIDTH, top = the near end boards,
#                       bottom = the far end boards.
# Sizes and places come from Tools/rink_layout.py (the Rink Layout). Fixed random seed: the same art on every run.
# Needs Pillow and numpy (pip install pillow numpy).
# Run: python Tools/make_ice_wear.py Assets/Resources/Models
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # finds rink_layout.py even when run with -I
from rink_layout import *

out = sys.argv[1]
CALM_GLOSS = 0.3          # Calm Area ice: this share of the ice's gloss
CALM_EDGE = 0.5           # metres: the soft edge of the matte patch, centred on the Calm Area's edge
CALM_CLEAR = 0.3          # metres: no marks or snow this far out round the Calm Area either
MARK = (168, 184, 204, 51)   # a faint blue-grey stride (alpha 0.2)
SNOW = (255, 255, 255, 140)  # white snow (alpha 0.55)


def calm_share(x, z):
    """0 outside the Calm Area, 1 inside, ramping over CALM_EDGE metres centred on its edge."""
    in_x = np.clip((CALM_AREA_HALF_WIDTH - np.abs(x)) / CALM_EDGE + 0.5, 0.0, 1.0)
    in_z = np.clip((z - CALM_AREA_FRONT_Z) / CALM_EDGE + 0.5, 0.0, 1.0)
    return in_x * in_z


# ---------- smoothness map: the whole ice square ----------
N = 1024
HALF = ICE_SIZE / 2
c = (np.arange(N, dtype=np.float32) + 0.5) * ICE_SIZE / N - HALF
x = c[None, :]
z = ICE_CENTRE_Z - c[:, None]
alpha = 1.0 - (1.0 - CALM_GLOSS) * calm_share(x, z)
smooth = np.zeros((N, N, 4), np.uint8)
smooth[..., 3] = np.round(alpha * 255).astype(np.uint8)   # R = 0: not metal
Image.fromarray(smooth, "RGBA").save(os.path.join(out, "ice-smoothness.png"), optimize=True)

# ---------- skate marks and snow: the rink inside the boards ----------
W, H = 1024, 2048
X0, X1, Z0, Z1 = -HALF_WIDTH, HALF_WIDTH, FAR_END_BOARDS_Z, NEAR_END_BOARDS_Z
SX, SZ = W / (X1 - X0), H / (Z1 - Z0)   # pixels per metre across and along the rink
rng = np.random.default_rng(88)
R = rng.random


def px(wx, wz):
    return ((wx - X0) * SX, (Z1 - wz) * SZ)


marks = Image.new("L", (W, H), 0)
draw = ImageDraw.Draw(marks)
# Skate strides: gentle arcs, a long way up and down the ice, more in the middle lanes.
for _ in range(1800):
    sx, sz = (R() + R() - 1.0) * (HALF_WIDTH - 1.0), Z0 + 0.5 + (Z1 - Z0 - 1.0) * R()
    heading = (0.0 if R() < 0.7 else R() * np.pi) + (R() - 0.5) * 0.8
    bend, length = (R() - 0.5) * 0.5, 1.0 + R() * 2.5
    t = np.arange(0.0, length, 0.05)
    h = heading + bend * t
    draw.line([px(sx + np.sin(a) * d, sz + np.cos(a) * d) for a, d in zip(h, t)], fill=MARK[3], width=1)

snow = Image.new("L", (W, H), 0)
draw = ImageDraw.Draw(snow)


def blob(wx, wz, radius):
    cx, cy = px(wx, wz)
    draw.ellipse([cx - radius * SX, cy - radius * SZ, cx + radius * SX, cy + radius * SZ], fill=SNOW[3])


# Snow: soft white piles along the side boards and both end boards, and a few stop sprays.
for _ in range(1800):
    side = -1.0 if R() < 0.5 else 1.0
    blob(side * (HALF_WIDTH - 0.15 - R() * 0.5), Z0 + (Z1 - Z0) * R(), 0.08 + R() * 0.12)
for end, inward in ((Z1, -1.0), (Z0, 1.0)):
    for _ in range(450):
        blob((R() - 0.5) * 2.0 * (HALF_WIDTH - 0.3), end + inward * (0.15 + R() * 0.4), 0.08 + R() * 0.1)
for _ in range(60):
    sx, sz, h = (R() - 0.5) * 2.0 * (HALF_WIDTH - 2.0), Z0 + 2.0 + (Z1 - Z0 - 3.0) * R(), R() * 2 * np.pi
    for t in np.arange(0.0, 0.9, 0.03):
        blob(sx + np.sin(h) * t, sz + np.cos(h) * t, 0.06 + 0.1 * (1.0 - t))
snow = snow.filter(ImageFilter.GaussianBlur(1.5))   # soft edges

m = np.asarray(marks, np.float32) / 255.0
s = np.asarray(snow, np.float32) / 255.0
cx = X0 + (np.arange(W, dtype=np.float32) + 0.5) / SX
cz = Z1 - (np.arange(H, dtype=np.float32) + 0.5) / SZ
wx, wz = cx[None, :], cz[:, None]
# Nothing in or just round the Calm Area, and nothing outside the boards' rounded corners.
calm = (np.abs(wx) < CALM_AREA_HALF_WIDTH + CALM_CLEAR) & (wz > CALM_AREA_FRONT_Z - CALM_CLEAR)
corner_z = np.clip(wz, Z0 + CORNER_RADIUS, Z1 - CORNER_RADIUS)
corner_x = np.clip(wx, X0 + CORNER_RADIUS, X1 - CORNER_RADIUS)
inside = np.hypot(wx - corner_x, wz - corner_z) <= CORNER_RADIUS
keep = (~calm & inside).astype(np.float32)
m, s = m * keep, s * keep

a = np.maximum(m, s)
snow_share = np.where(a > 0, s / np.maximum(m + s, 1e-6), 0.0)[..., None]
rgb = np.array(MARK[:3], np.float32) * (1 - snow_share) + np.array(SNOW[:3], np.float32) * snow_share
wear = np.dstack([rgb, a[..., None] * 255.0])
Image.fromarray(np.clip(wear + 0.5, 0, 255).astype(np.uint8), "RGBA").save(os.path.join(out, "ice-wear.png"), optimize=True)
print("ice-smoothness.png", N, "x", N, "and ice-wear.png", W, "x", H, "written")
