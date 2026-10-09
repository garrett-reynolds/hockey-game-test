# Writes the low-poly goalie catcher OBJ+MTL (save-glove), shown for the SAVE result.
# (The goalie character's own gear comes from Tools/make_lifelike_goalie.py.)
# Run: python Tools/make_goalie_gear.py Assets/Resources/Models
# Conventions for every piece: metres, Y up, the side that faces the shooter is -Z, centred near the origin.
# The game scales each piece to its slot (ModelFiles.FitTo), so sizes here only set the proportions.
# Each part is convex; faces are wound counter-clockwise seen from outside (checked against the part centre).
import math, os, sys

WHITE = "GearWhite"
BLUE = "GearBlue"
TAN = "GearTan"
DARK = "GearDark"
COLOURS = {WHITE: (0.92, 0.92, 0.9), BLUE: (0.12, 0.2, 0.55), TAN: (0.62, 0.42, 0.22), DARK: (0.08, 0.08, 0.09)}


class Obj:
    def __init__(self, name):
        self.name, self.v, self.faces = name, [], {}

    def part(self, verts, faces, mat):
        base = len(self.v)
        self.v.extend(verts)
        cx = sum(p[0] for p in verts) / len(verts)
        cy = sum(p[1] for p in verts) / len(verts)
        cz = sum(p[2] for p in verts) / len(verts)
        for f in faces:
            a, b, c = (verts[i] for i in f[:3])
            n = cross(sub(b, a), sub(c, a))
            m = [sum(verts[i][k] for i in f) / len(f) for k in range(3)]
            if dot(n, (m[0] - cx, m[1] - cy, m[2] - cz)) < 0:
                f = list(reversed(f))
            self.faces.setdefault(mat, []).append([base + i + 1 for i in f])

    def box(self, x0, y0, z0, x1, y1, z1, mat):
        v = [(x, y, z) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)]
        f = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
        self.part(v, f, mat)

    def ellipsoid(self, c, r, mat, seg=16, rings=10, front_only=False):
        # front_only: keep the -Z half (a shell facing the shooter), closed by a flat back.
        v, f = [], []
        lon_n = seg
        for i in range(rings + 1):
            th = math.pi * i / rings
            for j in range(lon_n + 1 if front_only else lon_n):
                ph = (math.pi * j / lon_n + math.pi) if front_only else 2 * math.pi * j / lon_n
                v.append((c[0] + r[0] * math.sin(th) * math.cos(ph), c[1] + r[1] * math.cos(th), c[2] + r[2] * math.sin(th) * math.sin(ph)))
        cols = lon_n + 1 if front_only else lon_n
        for i in range(rings):
            for j in range(cols - 1 if front_only else cols):
                a = i * cols + j
                b = i * cols + (j + 1) % cols
                f.append((a, b, b + cols, a + cols) if i not in (0, rings - 1) else
                         ((a, b + cols, a + cols) if i == 0 else (a, b, a + cols)))
        if front_only:  # flat back across the cut (z = c.z)
            centre = len(v); v.append((c[0], c[1], c[2]))
            edge = [i * cols for i in range(rings + 1)] + [i * cols + cols - 1 for i in range(rings, -1, -1)]
            for k in range(len(edge)):
                f.append((centre, edge[k], edge[(k + 1) % len(edge)]))
        self.part(v, f, mat)

    def cylinder(self, c, rx, rz, y0, y1, mat, seg=12):
        v = [(c[0] + rx * math.cos(2 * math.pi * i / seg), y, c[2] + rz * math.sin(2 * math.pi * i / seg)) for y in (y0, y1) for i in range(seg)]
        f = [(i, (i + 1) % seg, seg + (i + 1) % seg, seg + i) for i in range(seg)]
        f.append(tuple(range(seg)))
        f.append(tuple(range(seg, 2 * seg)))
        self.part(v, f, mat)

    def write(self, out_dir, file_name, note):
        mtl = file_name + ".mtl"
        with open(os.path.join(out_dir, file_name + ".obj"), "w") as o:
            o.write(f"# {note} Made for Hockey Game Test by Tools/make_goalie_gear.py. CC0.\nmtllib {mtl}\no {self.name}\n")
            for x, y, z in self.v:
                o.write(f"v {x:.5f} {y:.5f} {z:.5f}\n")
            for mat, faces in self.faces.items():
                o.write(f"usemtl {mat}\ns 1\n")
                for face in faces:
                    o.write("f " + " ".join(str(i) for i in face) + "\n")
        with open(os.path.join(out_dir, mtl), "w") as o:
            o.write(f"# {note} materials. CC0.\n")
            for mat in self.faces:
                r, g, b = COLOURS[mat]
                o.write(f"newmtl {mat}\nKd {r} {g} {b}\nKs 0.05 0.05 0.05\nNs 10\n")
        tris = sum(len(f) - 2 for fs in self.faces.values() for f in fs)
        print(file_name, len(self.v), "verts", tris, "tris")


def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def catcher():
    # Trapper: rounded palm 0.30 wide x 0.32 tall, tan pocket web at the top, white cuff at the wrist.
    o = Obj("SaveGlove")
    o.ellipsoid((0, 0.02, 0.02), (0.15, 0.15, 0.06), WHITE, seg=18, rings=10)   # body
    o.ellipsoid((0, 0.05, -0.02), (0.10, 0.10, 0.03), TAN, seg=14, rings=8)     # pocket
    o.box(-0.025, 0.06, -0.06, 0.025, 0.20, -0.02, TAN)                          # T-trap web
    o.box(-0.08, 0.15, -0.06, 0.08, 0.19, -0.02, TAN)
    o.box(-0.11, -0.20, -0.03, 0.11, -0.11, 0.07, BLUE)                          # cuff
    return o


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    catcher().write(out, "save-glove", "Goalie catching glove (save glove).")


main()
