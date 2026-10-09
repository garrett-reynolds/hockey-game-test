# Writes the hockey net and goal light models as OBJ+MTL, in metres, Y up.
#   python Tools/make_net.py Assets/Resources/Models
# Net: the opening (Net Face) is 1.83 m wide and 1.22 m high at z = 0, centred on x = 0, bottom on the ice (y = 0).
# Red posts and crossbar (0.08 m thick) stand just outside the opening, centred on z = 0. White padded base frame and
# white mesh netting run back to z = 0.6 m (the game's net depth). Nothing sticks out past the posts or behind 0.6 m,
# because the game fits the model's outer box to exactly that frame.
# Goal light: grey housing with a red dome, 0.26 m square, 0.31 m high, base at y = 0.
import math, os, sys

W, H, D, P = 1.83, 1.22, 0.6, 0.08   # opening width, height, net depth, post thickness
STRAND = 0.008                       # netting strand thickness
MESH = 0.075                         # netting hole size (about)
TUBE = 0.035                         # base frame tube radius
BEND = 0.15                          # base frame back corner radius


def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def norm(a): return mul(a, 1.0 / math.sqrt(dot(a, a)))


class Obj:
    def __init__(self):
        self.v, self.parts = [], []   # parts: (name, material, faces)

    def part(self, name, material):
        self.parts.append((name, material, []))

    def solid(self, points, faces):
        """Adds a convex solid; turns each face to point away from the solid's centre."""
        base = len(self.v) + 1
        self.v.extend(points)
        c = mul(sum_points(points), 1.0 / len(points))
        out = self.parts[-1][2]
        for f in faces:
            p = [points[i] for i in f]
            n = (0.0, 0.0, 0.0)
            for k in range(1, len(p) - 1):
                n = add(n, cross(sub(p[k], p[0]), sub(p[k + 1], p[0])))
            mid = mul(sum_points(p), 1.0 / len(p))
            if dot(n, sub(mid, c)) < 0: f = f[::-1]
            for k in range(1, len(f) - 1):   # triangles
                out.append((base + f[0], base + f[k], base + f[k + 1]))

    def cylinder(self, a, b, r, seg=16, up=None, turn=0.0):
        d = norm(sub(b, a))
        if up is None:
            up = (1, 0, 0) if abs(d[1]) > 0.9 else ((0, 0, 1) if abs(d[0]) > 0.9 else (1, 0, 0))
        u = norm(sub(up, mul(d, dot(up, d))))
        v = cross(d, u)
        pts = []
        for end in (a, b):
            for i in range(seg):
                t = 2 * math.pi * i / seg + turn
                pts.append(add(end, add(mul(u, r * math.cos(t)), mul(v, r * math.sin(t)))))
        faces = [[0 + i, 0 + (i + 1) % seg, seg + (i + 1) % seg, seg + i] for i in range(seg)]
        faces.append(list(range(seg)))
        faces.append(list(range(seg, 2 * seg)))
        self.solid(pts, faces)

    def bar(self, a, b, side, up=None):
        """Square bar of the given side from a to b, faces lined up with the axes."""
        self.cylinder(a, b, side / math.sqrt(2), seg=4, up=up, turn=math.pi / 4)

    def sphere(self, c, r, rings=8, seg=16):
        pts = [add(c, (0, r, 0))]
        for j in range(1, rings):
            phi = math.pi * j / rings
            for i in range(seg):
                t = 2 * math.pi * i / seg
                pts.append(add(c, (r * math.sin(phi) * math.cos(t), r * math.cos(phi), r * math.sin(phi) * math.sin(t))))
        pts.append(add(c, (0, -r, 0)))
        bottom = len(pts) - 1
        ring = lambda j, i: 1 + (j - 1) * seg + i % seg
        faces = [[0, ring(1, i), ring(1, i + 1)] for i in range(seg)]
        for j in range(1, rings - 1):
            faces += [[ring(j, i), ring(j + 1, i), ring(j + 1, i + 1), ring(j, i + 1)] for i in range(seg)]
        faces += [[bottom, ring(rings - 1, i + 1), ring(rings - 1, i)] for i in range(seg)]
        self.solid(pts, faces)

    def write(self, folder, name, header, materials):
        with open(os.path.join(folder, name + ".obj"), "w", newline="\n") as o:
            o.write(f"# {header} Made for Hockey Game Test by Tools/make_net.py. CC0.\nmtllib {name}.mtl\n")
            for x, y, z in self.v:
                o.write(f"v {x:.5f} {y:.5f} {z:.5f}\n")
            for part, material, faces in self.parts:
                o.write(f"g {part}\nusemtl {material}\n")
                for t in faces:
                    o.write("f %d %d %d\n" % t)
        with open(os.path.join(folder, name + ".mtl"), "w", newline="\n") as o:
            o.write(f"# {header} materials. CC0.\n")
            for material, (r, g, b), ns in materials:
                o.write(f"newmtl {material}\nKd {r} {g} {b}\nKs 0.2 0.2 0.2\nNs {ns}\n")
        return sum(len(f) for _, _, f in self.parts)


def sum_points(ps):
    s = (0.0, 0.0, 0.0)
    for p in ps: s = add(s, p)
    return s


def steps(lo, hi, size):
    n = max(1, round((hi - lo) / size))
    return [lo + (hi - lo) * k / n for k in range(n + 1)]


def strand(m, a, b):
    """A netting strand from a to b, cut into pieces about one hole long so the game can bend it (the goal bulge)."""
    n = max(1, round(math.sqrt(dot(sub(b, a), sub(b, a))) / MESH))
    for k in range(n):
        m.bar(add(a, mul(sub(b, a), k / n)), add(a, mul(sub(b, a), (k + 1) / n)), STRAND)


def net():
    m = Obj()
    x = W / 2 + P / 2   # post centre line
    r = P / 2
    m.part("LeftPost", "PostRed")
    m.cylinder((-x, 0, 0), (-x, H + r, 0), r)
    m.part("RightPost", "PostRed")
    m.cylinder((x, 0, 0), (x, H + r, 0), r)
    m.part("Crossbar", "PostRed")
    m.cylinder((-x, H + r, 0), (x, H + r, 0), r)
    m.sphere((-x, H + r, 0), r)
    m.sphere((x, H + r, 0), r)

    # Base frame: a padded tube on the ice from each post back, round the back corners, across the back.
    m.part("BaseFrame", "FrameWhite")
    zb = D - TUBE
    path = [(-x, TUBE, 0.0), (-x, TUBE, zb - BEND)]
    for k in range(1, 7):
        t = math.pi / 2 * k / 6
        path.append((-x + BEND - BEND * math.cos(t), TUBE, zb - BEND + BEND * math.sin(t)))
    path += [(x - BEND + BEND * math.cos(math.pi / 2 * k / 6), TUBE, zb - BEND + BEND * math.sin(math.pi / 2 * k / 6))
             for k in range(6, -1, -1)]
    path.append((x, TUBE, 0.0))
    for a, b in zip(path, path[1:]):
        if a != b: m.cylinder(a, b, TUBE, up=(0, 1, 0))
    for p in path[1:-1]:
        m.sphere(p, TUBE, rings=6, seg=12)

    # Netting: a grid of thin strands on the back, both sides and the roof.
    m.part("Netting", "NetWhite")
    s = STRAND
    zf, zr = P / 2, D - s / 2       # front edge (behind the posts) and back panel
    ys = steps(s / 2, H, MESH)
    xs = steps(-W / 2, W / 2, MESH)
    zs = steps(zf, zr, MESH)
    for xx in xs: strand(m, (xx, s / 2, zr), (xx, H, zr))                 # back
    for yy in ys: strand(m, (-W / 2, yy, zr), (W / 2, yy, zr))
    for side in (-1, 1):                                                  # sides
        xx = side * (W / 2 + s / 2)
        for zz in zs: strand(m, (xx, s / 2, zz), (xx, H, zz))
        for yy in ys: strand(m, (xx, yy, zf), (xx, yy, zr))
    yr = H + s / 2                                                        # roof
    for xx in xs: strand(m, (xx, yr, zf), (xx, yr, zr))
    for zz in zs: strand(m, (-W / 2, yr, zz), (W / 2, yr, zz))
    return m


def goal_light():
    m = Obj()
    m.part("Housing", "HousingGrey")
    m.bar((0, 0, 0), (0, 0.1, 0), 0.26)
    m.cylinder((0, 0.1, 0), (0, 0.12, 0), 0.11, seg=24)
    m.part("Dome", "DomeRed")
    m.cylinder((0, 0.12, 0), (0, 0.22, 0), 0.09, seg=24)
    m.sphere((0, 0.22, 0), 0.09, rings=10, seg=24)
    return m


if __name__ == "__main__":
    out = sys.argv[1]
    n = net().write(out, "net", "Hockey net.", [
        ("PostRed", (0.75, 0.04, 0.04), 60), ("FrameWhite", (0.92, 0.92, 0.92), 20), ("NetWhite", (0.86, 0.86, 0.86), 10)])
    g = goal_light().write(out, "goal-light", "Goal light.", [
        ("HousingGrey", (0.25, 0.25, 0.27), 30), ("DomeRed", (0.6, 0.03, 0.03), 80)])
    print("net", n, "tris; goal light", g, "tris")
