# Writes the rink boards and the arena around them as OBJ+MTL, in metres, Y up, plus the crowd texture crowd.png.
#   py -3 Tools/make_arena.py Assets/Resources/Models
# Needs Pillow and numpy (pip install pillow numpy).
#
# The sizes come from the Rink Layout (Tools/rink_layout.py, a copy of RinkLayout.cs); the game fits each model's outer
# box to RinkBoards.Box and ArenaStands.Box:
#   Rink (inside face of the boards): the full NHL rink, 200 x 85 ft with 28 ft corners; the near end boards are 11 ft
#   behind the Net Face (z = +3.35) and the far end boards 11 ft behind the far goal line (z = -57.61).
#   boards.obj: white boards 1.07 m high and 0.2 m thick with a yellow kick plate and a blue top rail, glass panels on top
#   (2.44 m high round the ends and corners, 1.83 m along the sides) with thin stanchions; no glass in front of the
#   players' benches. Outer box: the outline + 0.2 m, y 0 to the end glass top. The "Glass" part is made see-through by
#   the game.
#   arena.obj: a dark floor round the boards, two tiers of seats full of fans (crowd.png on the riser faces) all round the
#   bowl, an LED ribbon board between the tiers ("Ribbon", made to glow by the game), a back wall and a roof at 24 m.
#   Near centre ice the stands step back BENCH_DEPTH for the two empty players' benches (one side) and the two empty
#   penalty boxes (the other). Outer box: the outline + 31.2 m all round, y 0 to 24.
# Everything here is made from scratch for Hockey Game Test (no outside source). CC0.
import math, os, random, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # finds vector3.py even when run with -I
from vector3 import cross, dot, sub
import rink_layout as layout

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HW, EZ, NZ, R = layout.HALF_WIDTH, layout.NEAR_END_BOARDS_Z, layout.FAR_END_BOARDS_Z, layout.CORNER_RADIUS
T = 0.2                                     # boards thickness
BOARD_H, KICK_H, RAIL_H = layout.BOARD_HEIGHT, layout.KICK_PLATE_TOP, 0.06
GLASS_END, GLASS_SIDE = layout.END_GLASS_TOP, layout.SIDE_GLASS_TOP   # glass tops (m above the ice)
PANEL = 1.5                                 # glass panel width along straight boards
POST = 0.05                                 # stanchion width
REACH = 31.0                                # arena outer box = boards outer box + REACH all round
OUTER = T + REACH                           # outer wall's offset from the rink outline
ROOF = 24.0
CROWD_TILE = 8.0                            # metres of seats per crowd.png repeat

CX = HW - R                                 # corner centres at (+-CX, EZ - R) and (+-CX, NZ + R)
ZE, ZN = EZ - R, NZ + R

# Benches and penalty boxes: (side, z from, z to). The stands step back BENCH_DEPTH there.
OPENINGS = [(layout.BENCH_SIDE, a, b) for a, b in layout.BENCHES] + [(-layout.BENCH_SIDE, a, b) for a, b in layout.PENALTY_BOXES]
STEP = 0.25                                 # the stands step back over this length at each end of an opening


# ---------- the rink outline: a rounded rectangle, sampled once and offset outward for every ring ----------
def outline_samples():
    """Points round the inside of the boards as (cx, cz, nx, nz): point = (cx, cz) + R * outward normal (nx, nz),
    so a ring offset by o is (cx, cz) + (R + o) * normal. It starts at the middle of the near end boards (behind the
    near net, like RinkLayout.OnBoards) and goes toward +x. Straight runs are cut into about PANEL lengths, and also
    at each end of a bench or penalty box (and STEP outside it); corners into 16 steps."""
    pts = []

    def arc(cx, cz, a0, a1, steps=16):
        for i in range(steps):
            a = a0 + (a1 - a0) * i / steps
            pts.append((cx, cz, math.cos(a), math.sin(a)))

    def line(p0, p1, n):
        length = math.dist(p0, p1)
        cuts = {0.0}
        if n[0] != 0:                       # a side run: cut at the openings on this side
            for side, a, b in OPENINGS:
                if side != n[0]: continue
                for zc in (a - STEP, a, b, b + STEP):
                    t = (zc - p0[1]) / (p1[1] - p0[1])
                    if 0 < t < 1: cuts.add(t)
        cuts = sorted(cuts) + [1.0]
        for t0, t1 in zip(cuts, cuts[1:]):
            steps = max(1, round((t1 - t0) * length / PANEL))
            for i in range(steps):
                t = t0 + (t1 - t0) * i / steps
                x, z = p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t
                pts.append((x - n[0] * R, z - n[1] * R, n[0], n[1]))

    # Clockwise seen from above (x right, z up the page): from behind the near net toward +x, down the +x side and back.
    line((0.0, EZ), (CX, EZ), (0, 1))
    arc(CX, ZE, math.pi / 2, 0)
    line((HW, ZE), (HW, ZN), (1, 0))
    arc(CX, ZN, 2 * math.pi, 1.5 * math.pi)
    line((CX, NZ), (-CX, NZ), (0, -1))
    arc(-CX, ZN, 1.5 * math.pi, math.pi)
    line((-HW, ZN), (-HW, ZE), (-1, 0))
    arc(-CX, ZE, math.pi, math.pi / 2)
    line((-CX, EZ), (0.0, EZ), (0, 1))
    return pts


SAMPLES = outline_samples()


def in_opening(x, z):
    """True for a point on the outline (or pushed out from it) beside a bench or penalty box."""
    return any(abs(x) >= HW - 1e-6 and (x > 0) == (side > 0) and a - 1e-6 <= z <= b + 1e-6 for side, a, b in OPENINGS)


def in_bench(x, z):
    """True beside a players' bench (no glass in front of it)."""
    return any(abs(x) >= HW - 1e-6 and (x > 0) == (layout.BENCH_SIDE > 0) and a - 1e-6 <= z <= b + 1e-6
               for a, b in layout.BENCHES)


def ring(offset):
    """Points (x, z) of the outline pushed out by offset metres. From the stands' front wall on, it steps back
    BENCH_DEPTH beside the benches and penalty boxes (the outer wall stays put)."""
    pts = []
    for cx, cz, nx, nz in SAMPLES:
        o = offset
        if FRONT <= offset < OUTER and in_opening(cx + nx * R, cz + nz * R): o += layout.BENCH_DEPTH
        pts.append((cx + nx * (R + o), cz + nz * (R + o)))
    return pts


def lengths(points):
    """Distance along the closed ring to each point (and the full length at the end)."""
    d = [0.0]
    for i in range(len(points)):
        d.append(d[-1] + math.dist(points[i], points[(i + 1) % len(points)]))
    return d


def tall_glass(x, z):
    """Higher glass round the ends and corners, lower along the straight sides."""
    return z > ZE or z < ZN


# ---------- OBJ writer ----------


class Obj:
    def __init__(self):
        self.v, self.vt, self.parts = [], [], []

    def part(self, name, material):
        self.parts.append((name, material, []))

    def poly(self, points, facing, uvs=None):
        """One flat polygon (3 or 4 points, convex) wound so it faces along facing (its front is drawn)."""
        n = cross(sub(points[1], points[0]), sub(points[2], points[0]))
        if dot(n, facing) < 0:
            points = points[::-1]
            uvs = uvs[::-1] if uvs else None
        base = len(self.v) + 1
        self.v += points
        if uvs:
            tb = len(self.vt) + 1
            self.vt += uvs
            idx = [f"{base + i}/{tb + i}" for i in range(len(points))]
        else:
            idx = [str(base + i) for i in range(len(points))]
        faces = self.parts[-1][2]
        for i in range(1, len(points) - 1):
            faces.append((idx[0], idx[i], idx[i + 1]))

    def box(self, centre, half):
        """Axis-aligned box: all six faces facing out."""
        cx, cy, cz = centre
        hx, hy, hz = half
        for axis in range(3):
            for sign in (-1, 1):
                n = [0, 0, 0]; n[axis] = sign
                corners = []
                for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                    p = [0, 0, 0]; p[axis] = sign
                    others = [k for k in range(3) if k != axis]
                    p[others[0]], p[others[1]] = a, b
                    corners.append((cx + p[0] * hx, cy + p[1] * hy, cz + p[2] * hz))
                self.poly(corners, tuple(n))

    def wall(self, offset, y0, y1, inward=True, uv_v=None, u_shift=0.0):
        """A vertical band round the whole outline at offset, from y0 to y1, facing the rink (or away).
        With uv_v=(v0, v1) it is textured: u runs CROWD_TILE metres per repeat along the ring."""
        pts = ring(offset)
        d = lengths(pts)
        for i in range(len(pts)):
            (x0, z0), (x1, z1) = pts[i], pts[(i + 1) % len(pts)]
            mx, mz = (x0 + x1) / 2, (z0 + z1) / 2
            facing = (-(mx - centre_x(mx)), 0, -(mz - centre_z(mz)))
            if not inward: facing = (-facing[0], 0, -facing[2])
            quad = [(x0, y0, z0), (x1, y0, z1), (x1, y1, z1), (x0, y1, z0)]
            uvs = None
            if uv_v:
                u0, u1 = (d[i] + u_shift) / CROWD_TILE, (d[i + 1] + u_shift) / CROWD_TILE
                uvs = [(u0, uv_v[0]), (u1, uv_v[0]), (u1, uv_v[1]), (u0, uv_v[1])]
            self.poly(quad, facing, uvs)

    def flat(self, off0, off1, y, up=True):
        """A horizontal band round the outline between two offsets, at height y, facing up (or down)."""
        a, b = ring(off0), ring(off1)
        for i in range(len(a)):
            j = (i + 1) % len(a)
            quad = [(a[i][0], y, a[i][1]), (a[j][0], y, a[j][1]), (b[j][0], y, b[j][1]), (b[i][0], y, b[i][1])]
            self.poly(quad, (0, 1 if up else -1, 0))

    def write(self, folder, name, header, materials):
        with open(os.path.join(folder, name + ".obj"), "w", newline="\n") as o:
            o.write(f"# {header} Made for Hockey Game Test by Tools/make_arena.py. CC0.\nmtllib {name}.mtl\n")
            for x, y, z in self.v:
                o.write(f"v {x:.4f} {y:.4f} {z:.4f}\n")
            for u, v in self.vt:
                o.write(f"vt {u:.5f} {v:.5f}\n")
            for part, material, faces in self.parts:
                o.write(f"g {part}\nusemtl {material}\ns off\n")
                for t in faces:
                    o.write("f %s %s %s\n" % t)
        with open(os.path.join(folder, name + ".mtl"), "w", newline="\n") as o:
            o.write(f"# {header} materials. CC0.\n")
            for material, colour, texture in materials:
                r, g, b = (lin(c) for c in colour)
                o.write(f"newmtl {material}\nKd {r:.4f} {g:.4f} {b:.4f}\nKs 0.1 0.1 0.1\nNs 20\n")
                if texture: o.write(f"map_Kd {texture}\n")
        return sum(len(f) for _, _, f in self.parts)


def centre_x(x): return max(-CX, min(CX, x))       # nearest point on the line between the corner centres
def centre_z(z): return max(ZN, min(ZE, z))


def lin(c):
    """sRGB 0..255 to the linear 0..1 value Unity reads from an mtl Kd."""
    c = c / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


# ---------- boards.obj ----------
def boards(folder):
    o = Obj()
    o.part("KickPlate", "KickPlate"); o.wall(0, 0, KICK_H)
    o.part("Boards", "Boards")
    o.wall(0, KICK_H, BOARD_H - RAIL_H)
    o.wall(T, 0, BOARD_H, inward=False)
    o.part("Rail", "Rail")
    o.wall(0, BOARD_H - RAIL_H, BOARD_H)
    o.flat(0, T, BOARD_H)

    # Glass panels stand on the middle of the boards' top, facing the rink; none in front of the players' benches.
    mid = ring(T / 2)
    glass = [not in_bench((mid[i][0] + mid[(i + 1) % len(mid)][0]) / 2, (mid[i][1] + mid[(i + 1) % len(mid)][1]) / 2)
             for i in range(len(mid))]
    o.part("Glass", "Glass")
    for i in range(len(mid)):
        if not glass[i]: continue
        (x0, z0), (x1, z1) = mid[i], mid[(i + 1) % len(mid)]
        top = GLASS_END if tall_glass((x0 + x1) / 2, (z0 + z1) / 2) else GLASS_SIDE
        facing = (-((x0 + x1) / 2 - centre_x((x0 + x1) / 2)), 0, -((z0 + z1) / 2 - centre_z((z0 + z1) / 2)))
        o.poly([(x0, BOARD_H, z0), (x1, BOARD_H, z1), (x1, top, z1), (x0, top, z0)], facing)

    # Stanchions between the panels on the straight runs (the corners are bent glass).
    o.part("Stanchions", "Stanchion")
    for i, (cx, cz, nx, nz) in enumerate(SAMPLES):
        x, z = mid[i]
        straight = abs(nx) == 1 or abs(nz) == 1
        if not straight or not (glass[i] or glass[i - 1]): continue
        prev = mid[i - 1]
        top = max(GLASS_END if tall_glass(x, z) else GLASS_SIDE,
                  GLASS_END if tall_glass((x + prev[0]) / 2, (z + prev[1]) / 2) else GLASS_SIDE)
        o.box((x, (BOARD_H + top) / 2, z), (POST / 2, (top - BOARD_H) / 2, POST / 2))

    n = o.write(folder, "boards", "Rink boards and glass.", [
        ("Boards", (236, 238, 240), None),
        ("KickPlate", (232, 190, 40), None),
        ("Rail", (20, 60, 150), None),
        ("Glass", (215, 235, 235), None),
        ("Stanchion", (150, 155, 160), None),
    ])
    print(f"boards.obj written, {n} triangles")


# ---------- arena.obj ----------
FRONT = 1.0            # stands' front wall offset from the rink outline
FRONT_H = 1.6          # front wall height (above the boards, so nothing behind the boards shows through the glass)
ROW_D = 0.85
LOWER_ROWS, LOWER_RISE = 20, 0.42
RIBBON_H = 1.2
UPPER_ROWS, UPPER_RISE = 13, 0.55


def arena(folder):
    rnd = random.Random(25)
    o = Obj()
    o.part("Floor", "Floor"); o.flat(T, FRONT, 0.02)
    o.part("StandsFront", "Wall"); o.wall(FRONT, 0, FRONT_H)

    treads = []
    off, y = FRONT, FRONT_H
    o.part("Crowd", "Crowd")
    for i in range(LOWER_ROWS):
        treads.append((off, off + ROW_D, y))
        off += ROW_D
        o.wall(off, y, y + LOWER_RISE, uv_v=(0, 1), u_shift=rnd.uniform(0, CROWD_TILE))
        y += LOWER_RISE
    ribbon = (off, y)
    y += RIBBON_H
    for i in range(UPPER_ROWS):
        treads.append((off, off + ROW_D, y))
        off += ROW_D
        o.wall(off, y, y + UPPER_RISE, uv_v=(0, 1), u_shift=rnd.uniform(0, CROWD_TILE))
        y += UPPER_RISE
    assert off < OUTER, (off, OUTER)
    treads.append((off, OUTER, y))

    # The empty players' benches and penalty boxes: a bench seat along the back of each, a wall at each end.
    o.part("Benches", "Bench")
    back = FRONT + layout.BENCH_DEPTH
    for side, a, b in OPENINGS:
        o.box((side * (HW + back - 0.25), 0.45 / 2, (a + b) / 2), (0.25, 0.45 / 2, (b - a) / 2 - 0.05))
        for zc in (a, b):
            o.box((side * (HW + (T + back) / 2), 0.6, zc), ((back - T) / 2, 0.6, 0.04))

    o.part("Ribbon", "Ribbon"); o.wall(ribbon[0], ribbon[1], ribbon[1] + RIBBON_H)
    o.part("Aisles", "Concrete")
    for a, b, h in treads: o.flat(a, b, h)
    o.part("BackWall", "Wall"); o.wall(OUTER, y, ROOF)

    # Roof: a fan from the middle of the rink to the outer wall's top, facing down.
    o.part("Ceiling", "Ceiling")
    top = ring(OUTER)
    mid = (0.0, ROOF, (EZ + NZ) / 2)
    for i in range(len(top)):
        (x0, z0), (x1, z1) = top[i], top[(i + 1) % len(top)]
        o.poly([mid, (x0, ROOF, z0), (x1, ROOF, z1)], (0, -1, 0))

    n = o.write(folder, "arena", "Arena stands round the rink.", [
        ("Floor", (45, 47, 52), None),
        ("Wall", (28, 30, 36), None),
        ("Crowd", (255, 255, 255), "crowd.png"),
        ("Ribbon", (40, 110, 230), None),
        ("Bench", (52, 56, 66), None),
        ("Concrete", (38, 40, 46), None),
        ("Ceiling", (16, 17, 20), None),
    ])
    print(f"arena.obj written, {n} triangles")


# ---------- crowd.png: one row of seated fans, CROWD_TILE metres wide, wrapping left to right ----------
def crowd(folder):
    rnd = random.Random(2025)
    PX = 512                                          # pixels per metre while drawing
    W, H = int(CROWD_TILE * PX), int(0.62 * PX)       # one riser's worth of people (heads and shoulders)
    img = Image.new("RGB", (W, H), (30, 30, 36))
    d = ImageDraw.Draw(img)
    seat = 0.5 * PX
    shirts = [(178, 24, 40)] * 8 + [(235, 235, 235)] * 3 + [(25, 30, 60)] * 3 + [(20, 20, 22)] * 2 + \
             [(60, 90, 160), (210, 170, 40), (90, 90, 95), (140, 30, 50)]
    skins = [(240, 200, 170), (220, 175, 140), (190, 140, 105), (150, 105, 75), (100, 70, 50)]
    hairs = [(30, 22, 18), (60, 40, 25), (110, 80, 45), (200, 170, 110), (20, 20, 20), (140, 140, 140)]
    n = int(CROWD_TILE / 0.5)
    for k in range(n):
        cx = (k + 0.5) * seat
        # The seat back shows behind everyone (and alone where the seat is empty).
        d.rounded_rectangle([cx - 0.21 * PX, 0.30 * H, cx + 0.21 * PX, H], 18, fill=(120, 20, 30))
        if rnd.random() < 0.1: continue               # an empty seat
        shift = rnd.uniform(-0.05, 0.05) * PX
        top = rnd.uniform(0.12, 0.26) * H
        shirt = tuple(min(255, int(c * rnd.uniform(0.85, 1.1))) for c in rnd.choice(shirts))
        head_r = rnd.uniform(0.085, 0.1) * PX
        skin, hair, cap = rnd.choice(skins), rnd.choice(hairs), rnd.random() < 0.15
        for dx in (-W, 0, W):                         # wrapped copies, so the tile repeats seamlessly
            x = cx + dx + shift
            d.rounded_rectangle([x - 0.2 * PX, top + 1.7 * head_r, x + 0.2 * PX, H + 20], int(0.08 * PX), fill=shirt)
            d.ellipse([x - head_r, top, x + head_r, top + 2.2 * head_r], fill=skin)
            d.chord([x - head_r, top - 2, x + head_r, top + 1.6 * head_r], 180, 360, fill=hair)
            if cap:
                d.chord([x - head_r * 1.05, top - 6, x + head_r * 1.05, top + 1.3 * head_r], 180, 360, fill=shirt)
    img = img.filter(ImageFilter.GaussianBlur(1.2))
    a = np.asarray(img).astype(np.float32) * 0.85     # arenas keep the seats dimmer than the ice
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).resize((2048, 256), Image.LANCZOS)
    img.save(os.path.join(folder, "crowd.png"))
    print("crowd.png written")


if __name__ == "__main__":
    out = sys.argv[1]
    boards(out)
    arena(out)
    crowd(out)
