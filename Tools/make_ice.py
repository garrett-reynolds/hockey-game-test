# Writes the ice model ice.obj + ice.mtl: a 60 m x 60 m white ice sheet with NHL rink markings as flat
# raised strips, lined up with the net. The Net Face is at z = 0 on x = 0; the shooter skates in from -z.
# Y is up. The ice top is at y = 0; markings sit a few millimetres above it so they never flicker.
# The ice texture ice-detail.png (tiled every TILE metres) is made by Tools/make_ice_detail.py.
# Run: python Tools/make_ice.py Assets/Resources/Models
import math, os, sys

out = sys.argv[1]
HALF = 30.0                       # ice runs -30..30 m on x and z (the skate-in starts 22 m out)
TILE = 3.0                        # metres per repeat of the ice detail texture
FILL_Y, LINE_Y = 0.006, 0.012     # crease fill, then the painted lines above it
FT = 0.3048                       # metres per foot

GOAL_LINE_W = 2 / 12 * FT         # 2 in red goal line, centred on the Net Face plane
BLUE_W = 1 * FT                   # 12 in blue line and centre red line
BLUE_NEAR = -64 * FT              # near edge of the blue line, 64 ft out from the goal line
CENTRE_Z = -89 * FT               # centre red line, 89 ft out
CREASE_HALF_W = 4 * FT            # crease is 8 ft wide (net is 6 ft)
CREASE_R = 6 * FT                 # crease arc: 6 ft radius about the middle of the goal line
OUTLINE_W = 2 / 12 * FT           # red crease outline and faceoff circles
CIRCLE_X, CIRCLE_Z, CIRCLE_R = 22 * FT, -20 * FT, 15 * FT   # end-zone faceoff circles
NEUTRAL_DOT_Z = BLUE_NEAR - 5 * FT                         # neutral-zone faceoff dots
DOT_R = 1 * FT                    # 2 ft faceoff dots
SEG = 96

verts, uvs, groups = [], [], []   # groups: (object name, material, [faces])


def vert(x, y, z, uv=(0.0, 0.0)):
    verts.append((x, y, z)); uvs.append(uv)
    return len(verts)


def tri(faces, a, b, c):
    """Adds a triangle wound so its normal points up (+Y)."""
    (ax, _, az), (bx, _, bz), (cx, _, cz) = verts[a - 1], verts[b - 1], verts[c - 1]
    ny = (bz - az) * (cx - ax) - (bx - ax) * (cz - az)
    faces.append((a, b, c) if ny > 0 else (a, c, b))


def rect(faces, x0, z0, x1, z1, y):
    p = [vert(x0, y, z0), vert(x0, y, z1), vert(x1, y, z1), vert(x1, y, z0)]
    tri(faces, p[0], p[1], p[2]); tri(faces, p[0], p[2], p[3])


def fan(faces, cx, cz, outline, y):
    """Fills a convex outline [(x, z)...] with a fan from (cx, cz)."""
    c = vert(cx, y, cz)
    idx = [vert(x, y, z) for x, z in outline]
    for i in range(len(idx)):
        tri(faces, c, idx[i], idx[(i + 1) % len(idx)])


def ring(faces, cx, cz, r, w, y):
    """A circle line of width w, centred on radius r."""
    inner = [vert(cx + (r - w / 2) * math.cos(2 * math.pi * i / SEG), y, cz + (r - w / 2) * math.sin(2 * math.pi * i / SEG)) for i in range(SEG)]
    outer = [vert(cx + (r + w / 2) * math.cos(2 * math.pi * i / SEG), y, cz + (r + w / 2) * math.sin(2 * math.pi * i / SEG)) for i in range(SEG)]
    for i in range(SEG):
        j = (i + 1) % SEG
        tri(faces, inner[i], outer[i], outer[j]); tri(faces, inner[i], outer[j], inner[j])


def band(faces, points, w, y):
    """A line of width w along a polyline, lying just inside it, with square joints."""
    for (x0, z0), (x1, z1) in zip(points, points[1:]):
        dx, dz = x1 - x0, z1 - z0
        n = math.hypot(dx, dz)
        ox, oz = -dz / n * w / 2, dx / n * w / 2
        p = [vert(x0 - ox, y, z0 - oz), vert(x0 + ox, y, z0 + oz), vert(x1 + ox, y, z1 + oz), vert(x1 - ox, y, z1 - oz)]
        tri(faces, p[0], p[1], p[2]); tri(faces, p[0], p[2], p[3])
    for x, z in points[1:-1]:
        rect(faces, x - w / 2, z - w / 2, x + w / 2, z + w / 2, y)


def group(name, material):
    faces = []
    groups.append((name, material, faces))
    return faces


# Ice sheet: two triangles, UVs tile the detail texture.
f = group("Ice", "Ice")
p = [vert(x, 0.0, z, (x / TILE, z / TILE)) for x, z in ((-HALF, -HALF), (-HALF, HALF), (HALF, HALF), (HALF, -HALF))]
tri(f, p[0], p[1], p[2]); tri(f, p[0], p[2], p[3])

# Crease: straight sides 4 ft either side of the middle, closed by the 6 ft arc (meets the sides about 4.5 ft out).
side_z = -math.sqrt(CREASE_R ** 2 - CREASE_HALF_W ** 2)
a0 = math.atan2(side_z, CREASE_HALF_W)   # arc from the right side round to the left side, through -z (-pi/2)
a1 = math.atan2(side_z, -CREASE_HALF_W)
angles = [a0 + (a1 - a0) * i / 48 for i in range(49)]
arc =[(CREASE_R * math.cos(t), CREASE_R * math.sin(t)) for t in angles]
crease = [(CREASE_HALF_W, 0.0)] + arc + [(-CREASE_HALF_W, 0.0)]
fan(group("Crease", "CreaseBlue"), 0.0, side_z / 2, crease, FILL_Y)
band(group("CreaseOutline", "LineRed"), [(CREASE_HALF_W - OUTLINE_W / 2, 0.0)] +
     [(x * (1 - OUTLINE_W / 2 / CREASE_R), z * (1 - OUTLINE_W / 2 / CREASE_R)) for x, z in arc[1:-1]] +
     [(-CREASE_HALF_W + OUTLINE_W / 2, 0.0)], OUTLINE_W, LINE_Y)

rect(group("GoalLine", "LineRed"), -HALF, -GOAL_LINE_W / 2, HALF, GOAL_LINE_W / 2, LINE_Y)
rect(group("BlueLine", "LineBlue"), -HALF, BLUE_NEAR - BLUE_W, HALF, BLUE_NEAR, LINE_Y)
rect(group("CentreLine", "LineRed"), -HALF, CENTRE_Z - BLUE_W / 2, HALF, CENTRE_Z + BLUE_W / 2, LINE_Y)

f = group("FaceoffCircles", "LineRed")
for sx in (-1, 1):
    ring(f, sx * CIRCLE_X, CIRCLE_Z, CIRCLE_R, OUTLINE_W, LINE_Y)
f = group("FaceoffDots", "LineRed")
for sx in (-1, 1):
    for z in (CIRCLE_Z, NEUTRAL_DOT_Z):
        fan(f, sx * CIRCLE_X, z, [(sx * CIRCLE_X + DOT_R * math.cos(2 * math.pi * i / 48), z + DOT_R * math.sin(2 * math.pi * i / 48)) for i in range(48)], LINE_Y)

with open(os.path.join(out, "ice.obj"), "w") as o:
    o.write("# Hockey ice with rink markings, made for Hockey Game Test by Tools/make_ice.py. CC0.\nmtllib ice.mtl\n")
    for x, y, z in verts: o.write(f"v {x:.5f} {y:.5f} {z:.5f}\n")
    for u, v in uvs: o.write(f"vt {u:.5f} {v:.5f}\n")
    o.write("vn 0 1 0\n")
    for name, material, faces in groups:
        o.write(f"g {name}\nusemtl {material}\ns off\n")
        for t in faces: o.write("f " + " ".join(f"{i}/{i}/1" for i in t) + "\n")
with open(os.path.join(out, "ice.mtl"), "w") as o:
    # Unity reads Kd as linear colour, so these are the linear values of sRGB red (200, 16, 46),
    # blue (0, 56, 166) and crease blue (115, 173, 235). Use "g" groups: Unity merges "o" objects into one mesh.
    o.write("# Hockey ice materials. CC0.\n"
            "newmtl Ice\nKd 1 1 1\nKs 0.15 0.15 0.15\nNs 40\nmap_Kd ice-detail.png\n"
            "newmtl LineRed\nKd 0.578 0.005 0.027\nKs 0.1 0.1 0.1\nNs 40\n"
            "newmtl LineBlue\nKd 0.0 0.04 0.38\nKs 0.1 0.1 0.1\nNs 40\n"
            "newmtl CreaseBlue\nKd 0.171 0.418 0.83\nKs 0.1 0.1 0.1\nNs 40\n")
print(len(verts), "verts", sum(len(g[2]) for g in groups), "tris")
