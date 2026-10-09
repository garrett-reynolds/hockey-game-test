# Converts the hockey net model Garrett supplied ("Hockey Gates model.FBX", one 898k-face mesh, source unknown) into
# Assets/Resources/Models/net.fbx, in Blender, headless.
#
# Run (Git Bash; pass Windows paths, C:/..., not /c/...; copy the source to a short path first if its path is long):
#   "/c/Program Files/Blender Foundation/Blender 4.5/blender.exe" -b --factory-startup --disable-autoexec \
#     --python-exit-code 1 --python Tools/make_supplied_net.py -- \
#     --in <folder>/net_src.fbx --out Assets/Resources/Models/net.fbx
#
# What it does:
# - Splits the one mesh into its loose parts and sorts them by shape: the bent tube (posts and crossbar), the post feet,
#   the base tube, the skirt, the top tube and its padded roll, the centre back support, the lacing, and about 8,100
#   short netting strands (each a 96-face tube between two knots).
# - Writes named parts the game uses: LeftPost, RightPost, Crossbar, FrameCorners (the two bends), PostFeet, BaseFrame,
#   TopFrame, BackSupport (red); Skirt, TopPad (white/grey); Netting (white, one mesh, so NetBulge can bend it).
#   The lacing between the netting and the posts is dropped (it cannot be seen at 9 m).
# - Rebuilds each netting strand as one flat strip (two-sided, 4 triangles) from knot to knot, turned to face the
#   shooter's camera, and decimates the skirt, roll and tubes. The whole net is about 42k triangles (was 898k).
# - Fits it to the game in metres: the Net Face opening (1.83 m x 1.22 m) is exactly inside the posts and crossbar,
#   which are made 0.08 m thick (NetFrame) and stand centred on the Net Face plane; the back of the netting is 0.6 m
#   behind it (RinkBuilder.NetDepth). The depth is squashed (about x0.53) to that net depth; the posts and crossbar
#   keep round sections. The base, skirt and netting flare out past the posts, as on a real net.
# Axes here: x across, y back (depth), z up, as the source; the parts are turned half round about z when written so
# that in Unity x is across and z is back.
import argparse, math, os, sys
import bpy
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
parser = argparse.ArgumentParser()
parser.add_argument("--in", dest="src", required=True, help="the supplied Hockey Gates model.FBX")
parser.add_argument("--out", required=True, help="output FBX, e.g. Assets/Resources/Models/net.fbx")
args = parser.parse_args(argv)

HALF_W, H, DEPTH, POST = 1.83 / 2, 1.22, 0.6, 0.08   # Net Face, net depth, post thickness (metres)
STRAND = 0.006                                       # netting strip width (metres)
CAMERA = np.array([0.0, -9.0, 1.5])                  # the shooter's camera (Unity 0, 1.5, -9) in these axes

# ---- load -------------------------------------------------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.abspath(args.src))
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
if len(meshes) != 1:
    sys.exit("expected one mesh in %s, found %d" % (args.src, len(meshes)))
src = meshes[0]
bpy.context.view_layer.objects.active = src
src.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = src.data
co = np.empty(len(me.vertices) * 3)
me.vertices.foreach_get("co", co)
co = co.reshape(-1, 3)
edges = np.empty(len(me.edges) * 2, dtype=np.int64)
me.edges.foreach_get("vertices", edges)
edges = edges.reshape(-1, 2)
loop_start = np.empty(len(me.polygons), dtype=np.int64)
loop_total = np.empty(len(me.polygons), dtype=np.int64)
me.polygons.foreach_get("loop_start", loop_start)
me.polygons.foreach_get("loop_total", loop_total)
loop_vert = np.empty(len(me.loops), dtype=np.int64)
me.loops.foreach_get("vertex_index", loop_vert)
print("NET source", len(co), "vertices", len(loop_start), "faces")

# ---- loose parts (connected components by label propagation) ---------------------------------------------------
label = np.arange(len(co))
while True:
    a, b = label[edges[:, 0]], label[edges[:, 1]]
    m = np.minimum(a, b)
    before = label.copy()
    np.minimum.at(label, edges[:, 0], m)
    np.minimum.at(label, edges[:, 1], m)
    label = label[label]  # pointer jumping
    if np.array_equal(label, before):
        break
face_label = label[loop_vert[loop_start]]
parts = {}
for lab in np.unique(face_label):
    faces = np.nonzero(face_label == lab)[0]
    vs = np.nonzero(label == lab)[0]
    lo, hi = co[vs].min(0), co[vs].max(0)
    parts[lab] = dict(faces=faces, verts=vs, lo=lo, hi=hi, size=hi - lo)
print("NET loose parts", len(parts))


def kind(p):
    s, lo, hi, n = p["size"], p["lo"], p["hi"], len(p["faces"])
    if n == 96 and s.max() < 0.08:
        return "strand"
    if s[0] > 1.5 and s[2] > 1.2 and s[1] < 0.035:
        return "lace"
    if s[0] > 1.5 and s[2] > 1.2 and s[1] < 0.07:
        return "tube"
    if s[0] < 0.1 and hi[2] < 0.2:
        return "foot"
    if s[0] > 2.0 and s[1] > 1.0 and hi[2] < 0.05:
        return "base"
    if s[0] > 2.0 and s[1] > 1.0 and hi[2] < 0.2:
        return "skirt"
    if lo[2] > 1.15 and n < 6000:
        return "topframe"
    if lo[2] > 1.15:
        return "toppad"
    if s[0] < 0.1 and s[2] > 1.0:
        return "back"
    return "other"


groups = {}
for p in parts.values():
    groups.setdefault(kind(p), []).append(p)
print("NET kinds", {k: (len(v), sum(len(p["faces"]) for p in v)) for k, v in groups.items()})
expect = {"tube": 1, "lace": 1, "foot": 2, "base": 1, "skirt": 2, "topframe": 1, "toppad": 2, "back": 1}
for k, n in expect.items():
    if len(groups.get(k, [])) != n:
        sys.exit("expected %d %s part(s), found %d" % (n, k, len(groups.get(k, []))))
if "other" in groups or len(groups["strand"]) < 5000:
    sys.exit("unexpected parts: %s" % {k: len(v) for k, v in groups.items()})

# ---- the frame tube: post centres, crossbar centre, bend -------------------------------------------------------
tube = groups["tube"][0]
tv = co[tube["verts"]]
mid_x = (tube["lo"][0] + tube["hi"][0]) / 2
# The straight posts and crossbar have rings only at their ends, so read the sizes off the end rings.
low = tv[tv[:, 2] < 0.5]
left, right = low[low[:, 0] < mid_x], low[low[:, 0] > mid_x]
xl, xr = (left[:, 0].min() + left[:, 0].max()) / 2, (right[:, 0].min() + right[:, 0].max()) / 2
r0 = (left[:, 0].max() - left[:, 0].min()) / 2
yc = (left[:, 1].min() + left[:, 1].max()) / 2
zc = tv[:, 2].max() - r0
# The bend starts at the highest ring whose outer face is still straight (on a bend ring the outer face moves in).
straight = tv[(tv[:, 0] < mid_x) & (tv[:, 0] < xl - r0 + 5e-4)]
zb = straight[:, 2].max()
rb = zc - zb  # bend radius (of the centreline)
mid_x = (xl + xr) / 2
print("NET tube: posts x %.4f %.4f radius %.4f depth %.4f crossbar z %.4f bend from z %.4f radius %.4f"
      % (xl, xr, r0, yc, zc, zb, rb))

strand_pts = np.concatenate([co[p["verts"]] for p in groups["strand"]])
back_y = strand_pts[:, 1].max()
SX = (2 * (HALF_W + POST / 2)) / (xr - xl)   # post centre lines to +-(Net Face half width + half a post)
SZ = (H + POST / 2) / zc                     # crossbar centre line to the Net Face top + half a post
SD = DEPTH / (back_y - yc)                   # netting back to the net depth
print("NET scale across %.4f up %.4f depth %.4f" % (SX, SZ, SD))


def fit(p):
    """Source point(s) to game metres (these axes)."""
    p = np.atleast_2d(p)
    return np.stack([(p[:, 0] - mid_x) * SX, (p[:, 1] - yc) * SD, p[:, 2] * SZ], axis=1)


def tube_centre(p):
    """The point on the tube's centre line nearest each source point, and which part it belongs to."""
    c = p.copy()
    part = np.empty(len(p), dtype=object)
    for i, (x, y, z) in enumerate(p):
        if z <= zb:
            c[i] = (xl if x < mid_x else xr, yc, z)
            part[i] = "LeftPost" if x < mid_x else "RightPost"
        elif xl + rb <= x <= xr - rb:
            c[i] = (x, yc, zc)
            part[i] = "Crossbar"
        else:
            bx = xl + rb if x < mid_x else xr - rb
            d = np.array([x - bx, z - zb])
            d = d / max(np.linalg.norm(d), 1e-9)
            c[i] = (bx + rb * d[0], yc, zb + rb * d[1])
            part[i] = "FrameCorners"
    return c, part


def round_section(p, radius_scale):
    """Fits tube points so the section stays round, with its radius scaled (game metres)."""
    c, part = tube_centre(p)
    return fit(c) + (p - c) * radius_scale, part


# ---- build output meshes -----------------------------------------------------------------------------------------
def material(name, rgb, rough):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = 0.0
    m.diffuse_color = (*rgb, 1.0)
    return m


RED = material("Frame Red", (0.45, 0.006, 0.006), 0.35)  # linear; Unity shows it as about (0.7, 0.07, 0.07)
WHITE = material("Net White", (0.9, 0.9, 0.9), 0.8)
SKIRT = material("Skirt White", (0.78, 0.78, 0.8), 0.6)
PAD = material("Pad White", (0.88, 0.88, 0.88), 0.7)

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
out_objects = []
bounds = {}


def faces_of(ps):
    for p in ps:
        for f in p["faces"]:
            s, n = loop_start[f], loop_total[f]
            yield loop_vert[s:s + n]


def make(name, verts, faces, mat, decimate=None, final=True):
    """A new object from the vertices (game axes: x across, y back, z up) and faces."""
    bounds[name] = (np.min(verts, 0), np.max(verts, 0))
    if final:
        # The FBX export (forward -Z, up Y) and Unity's import turn Blender's X and Y round, so turn them round here
        # (a half turn about Z, which keeps the faces' winding): Unity then gets x across and z back.
        verts = np.asarray(verts) * np.array([-1.0, -1.0, 1.0])
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([tuple(v) for v in verts], [], [tuple(int(i) for i in f) for f in faces])
    mesh.validate()
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    if decimate:
        mod = obj.modifiers.new("decimate", "DECIMATE")
        mod.ratio = decimate
        bpy.ops.object.modifier_apply(modifier=mod.name)
    tri = obj.modifiers.new("tri", "TRIANGULATE")
    bpy.ops.object.modifier_apply(modifier=tri.name)
    out_objects.append(obj)
    return obj


def part_mesh(ps):
    """Source parts -> (source vertices, faces indexing them)."""
    vs = np.concatenate([p["verts"] for p in ps])
    index = {int(v): i for i, v in enumerate(vs)}
    return co[vs], [[index[int(v)] for v in f] for f in faces_of(ps)]


def decimated_source(ps, ratio):
    """Decimates source parts in place (source units), returns (vertices, faces)."""
    v, f = part_mesh(ps)
    obj = make("tmp", v, f, RED, ratio, final=False)
    out_objects.remove(obj)
    verts = np.array([tuple(x.co) for x in obj.data.vertices])
    faces = [list(p.vertices) for p in obj.data.polygons]
    bpy.data.objects.remove(obj, do_unlink=True)
    return verts, faces


def clamp(v, front=None):
    v[:, 2] = np.maximum(v[:, 2], 0.0)                 # nothing below the ice
    v[:, 1] = np.minimum(v[:, 1], DEPTH)               # nothing behind the net depth
    if front is not None:
        v[:, 1] = np.maximum(v[:, 1], front)
    return v


# Posts, crossbar and the bends: round sections 0.08 m across; split by where each face sits on the centre line.
k = (POST / 2) / r0
v, f = part_mesh(groups["tube"])
fv, fpart = round_section(v, k)
fv[fv[:, 2] < 0.01, 2] = 0.0  # posts stand on the ice
fv = clamp(fv)
centroid_part = tube_centre(np.array([v[list(face)].mean(0) for face in f]))[1]
for name in ("LeftPost", "RightPost", "Crossbar", "FrameCorners"):
    sel = [face for face, pn in zip(f, centroid_part) if pn == name]
    used = sorted({i for face in sel for i in face})
    remap = {old: new for new, old in enumerate(used)}
    make(name, fv[used], [[remap[i] for i in face] for face in sel], RED)

# Post feet: round, scaled with the posts.
v, f = decimated_source(groups["foot"], 0.25)
fv, _ = round_section(v, k)
make("PostFeet", clamp(fv), f, RED)

for name, kind_, mat, ratio in (("BaseFrame", "base", RED, 0.4), ("Skirt", "skirt", SKIRT, 0.15),
                                ("TopFrame", "topframe", RED, 0.2), ("TopPad", "toppad", PAD, 0.06),
                                ("BackSupport", "back", RED, None)):
    if ratio:
        v, f = decimated_source(groups[kind_], ratio)
    else:
        v, f = part_mesh(groups[kind_])
    make(name, clamp(fit(v)), f, mat)

# Netting: one two-sided strip per strand, knot to knot, facing the shooter.
verts, faces = [], []
for p in groups["strand"]:
    rings = co[p["verts"]].reshape(-1, 12, 3).mean(1)          # ring centres along the strand
    d = np.linalg.norm(rings[:, None] - rings[None], axis=2)
    i, j = np.unravel_index(d.argmax(), d.shape)
    a, b = fit(np.stack([rings[i], rings[j]]))
    along = b - a
    length = np.linalg.norm(along)
    if length < 1e-5:
        continue
    along /= length
    a, b = a - along * STRAND / 4, b + along * STRAND / 4   # a little overlap at the knots
    side = np.cross(along, CAMERA - (a + b) / 2)
    if np.linalg.norm(side) < 1e-6:
        side = np.cross(along, [0.0, 0.0, 1.0])
    side = side / np.linalg.norm(side) * STRAND / 2
    n = len(verts)
    quad = [a - side, b - side, b + side, a + side]
    verts += quad + quad
    faces += [[n, n + 1, n + 2, n + 3], [n + 7, n + 6, n + 5, n + 4]]
nv = clamp(np.array(verts), front=0.0)
make("Netting", nv, faces, WHITE)

# ---- report and export -------------------------------------------------------------------------------------------
total = 0
for o in out_objects:
    for poly in o.data.polygons:
        poly.use_smooth = o.name != "Netting"   # round tubes and soft skirt; flat strips
    t = sum(len(p.vertices) - 2 for p in o.data.polygons)
    total += t
    lo, hi = bounds[o.name]
    print("NET part %-12s %6d tris  x %.3f..%.3f  depth %.3f..%.3f  up %.3f..%.3f"
          % (o.name, t, lo[0], hi[0], lo[1], hi[1], lo[2], hi[2]))
print("NET total triangles", total)

bpy.ops.object.select_all(action="SELECT")
out = os.path.abspath(args.out)
os.makedirs(os.path.dirname(out), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=out, use_selection=False, object_types={"MESH"}, apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
    bake_anim=False, path_mode="STRIP", embed_textures=False, mesh_smooth_type="FACE", use_mesh_modifiers=True)
print("NET wrote", out)
