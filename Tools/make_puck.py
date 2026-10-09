# Writes a low-poly hockey puck OBJ+MTL: 0.15 m across, 0.05 m thick, thickness on Y, centred at origin.
# Chamfered edges and a ridged grip band round the side.
import math, sys, os
out = sys.argv[1]
SEG = 96
R, H, C = 0.075, 0.025, 0.003      # radius, half thickness, chamfer
GRIP_R = R - 0.0015                 # ridge valleys
band = 0.6 * H                      # grip band half height
# profile rings top to bottom: (radius, y, ridged)
rings = [(R - C, H, False), (R, H - C, False), (R, band, False),
         (R, band * 0.999, True), (R, -band * 0.999, True),
         (R, -band, False), (R, -H + C, False), (R - C, -H, False)]
v = []
def ring_verts(r, y, ridged):
    idx = len(v) + 1
    for i in range(SEG):
        a = 2 * math.pi * i / SEG
        rr = GRIP_R if (ridged and i % 2) else r
        v.append((rr * math.cos(a), y, rr * math.sin(a)))
    return idx
starts = [ring_verts(*rg) for rg in rings]
top_c = len(v) + 1; v.append((0, H, 0))
bot_c = len(v) + 1; v.append((0, -H, 0))
f = []
# top cap (normal +Y): CCW seen from above
s = starts[0]
for i in range(SEG):
    j = (i + 1) % SEG
    f.append((top_c, s + j, s + i))
# side strips
for k in range(len(starts) - 1):
    a, b = starts[k], starts[k + 1]
    for i in range(SEG):
        j = (i + 1) % SEG
        f.append((a + i, a + j, b + j)); f.append((a + i, b + j, b + i))
s = starts[-1]
for i in range(SEG):
    j = (i + 1) % SEG
    f.append((bot_c, s + i, s + j))
with open(os.path.join(out, "puck.obj"), "w") as o:
    o.write("# Hockey puck, made for Hockey Game Test. CC0.\nmtllib puck.mtl\no Puck\n")
    for x, y, z in v: o.write(f"v {x:.6f} {y:.6f} {z:.6f}\n")
    o.write("usemtl PuckRubber\ns 1\n")
    for t in f: o.write("f %d %d %d\n" % t)
with open(os.path.join(out, "puck.mtl"), "w") as o:
    o.write("# Hockey puck material. CC0.\nnewmtl PuckRubber\nKd 0.006 0.006 0.006\nKs 0.1 0.1 0.1\nNs 20\n")
print(len(v), "verts", len(f), "tris")
